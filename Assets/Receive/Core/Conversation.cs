using System.Collections.Generic;

namespace Receive.Core
{
    public enum QuestionKind
    {
        Useful,      // あいまいな点を確かめた／推測や訂正を念押しした／決まっていないことを確かめた
        Confirm,     // はっきり言われたことの念押し（良くも悪くもない）
        Premature,   // これから語られることを先に訊いた（話の腰を折った）
        Repeat,      // 直前の一言をもう一度
        Refused      // 間合いが尽きていた
    }

    public sealed class QuestionEvent
    {
        public Slot? Slot;
        public QuestionKind Kind;
        public int AtBeat;
    }

    public sealed class Line
    {
        public bool FromSpeaker;
        public string Text;
        public int Beat = -1;       // 台本の区切り。聞き返しへの返事は -1
        public Slot? AnswerTo;
    }

    /// <summary>
    /// 会話の進行。話し手の言葉は一度きりで、画面には残らない。
    /// 画面側は「続きがあるか」を知ることができない。最後まで聞いたかどうかは、プレイヤーの行動で決まる。
    /// </summary>
    public sealed class Conversation
    {
        private readonly Scenario _sc;
        private readonly Persona _persona;

        public readonly List<Line> Transcript = new List<Line>();
        public readonly List<QuestionEvent> Questions = new List<QuestionEvent>();
        public readonly HashSet<Slot> Revealed = new HashSet<Slot>();   // 聞き返しで値を得た欄
        public readonly HashSet<Slot> AskedUnstated = new HashSet<Slot>(); // 「決めていない」と確かめた欄

        public int BeatsHeard { get; private set; }
        public int Patience { get; private set; }
        public bool Left { get; private set; }
        public bool CutOff { get; private set; }
        public bool HeardEndNotice { get; private set; }

        public Conversation(Scenario sc)
        {
            _sc = sc;
            _persona = Persona.Find(sc.Script.PersonaId);
            Patience = sc.Script.Patience;
        }

        public Persona Persona { get { return _persona; } }
        public ScenarioScript Script { get { return _sc.Script; } }

        /// <summary>残りの間合いが少ないときだけ、相手の様子として伝える。数は見せない。</summary>
        public string MoodHint { get { return !Left && Patience == 1 ? _persona.MoodLow : null; } }

        /// <summary>続きを聴く。話が終わっていれば、話し手がそう告げる。</summary>
        public Line Listen()
        {
            if (Left) return null;
            Line line;
            if (BeatsHeard < _sc.Script.Beats.Count)
            {
                var b = _sc.Script.Beats[BeatsHeard];
                line = new Line { FromSpeaker = true, Text = b.Text, Beat = b.Index };
                BeatsHeard++;
            }
            else
            {
                HeardEndNotice = true;
                line = new Line { FromSpeaker = true, Text = _persona.Ended };
            }
            Transcript.Add(line);
            return line;
        }

        /// <summary>「承りました」。話の途中なら、残りは聞けなくなる。</summary>
        public void Leave()
        {
            if (Left) return;
            Left = true;
            CutOff = BeatsHeard < _sc.Script.Beats.Count;
        }

        /// <summary>直前の一言をもう一度言ってもらう。間合いを1つ使う。</summary>
        public Line AskRepeat()
        {
            if (!Spend(null)) return Refuse();
            string last = null;
            for (int i = Transcript.Count - 1; i >= 0; i--)
                if (Transcript[i].FromSpeaker && Transcript[i].Beat >= 0) { last = Transcript[i].Text; break; }
            Transcript.Add(new Line { FromSpeaker = false, Text = "いまのお言葉を、もう一度お願いします。" });
            Questions.Add(new QuestionEvent { Kind = QuestionKind.Repeat, AtBeat = BeatsHeard });
            var line = new Line { FromSpeaker = true, Text = last ?? _persona.NotYet };
            Transcript.Add(line);
            return line;
        }

        /// <summary>ある欄について聞き返す。間合いを1つ使う。</summary>
        public Line Ask(Slot slot)
        {
            if (!Spend(slot)) return Refuse();
            var u = _sc.Key.Unit(slot);
            Transcript.Add(new Line { FromSpeaker = false, Text = Lexicon.Question(slot) + "。" });
            // 結びの言葉まで聞いた後は、話は一区切りついている。そこでの聞き返しは先走りではない
            bool talkOver = Left || BeatsHeard > ClosingBeat();
            bool said = u.Beat >= 0 && u.Beat < BeatsHeard;
            string text; QuestionKind kind;

            if (!u.HasValue)
            {
                // 話し手も決めていない。話がまだ続くうちは、これから語られるかどうか分からないので先走りになる
                text = string.Format(_persona.NoneFmt, Lexicon.Label(slot));
                kind = talkOver ? QuestionKind.Useful : QuestionKind.Premature;
                AskedUnstated.Add(slot);
            }
            else if (!said && !talkOver)
            {
                text = _persona.NotYet;
                kind = QuestionKind.Premature;
            }
            else
            {
                Revealed.Add(slot);
                switch (u.State)
                {
                    case KeyState.Vague:
                        text = Phrases.VagueReveal(_persona, slot, u.Value);
                        kind = QuestionKind.Useful;
                        break;
                    case KeyState.Guess:
                        text = _persona.Uncertain + Phrases.Guess(_persona, slot, u.Value);
                        kind = QuestionKind.Useful;
                        break;
                    case KeyState.Hearsay:
                        text = _persona.Uncertain + Phrases.Hearsay(_persona, slot, u.Value);
                        kind = QuestionKind.Useful;
                        break;
                    default:
                        text = _persona.RepeatPrefix + Restate(u);
                        // 訂正のあった欄や、聞いていなかった欄の念押しは役に立つ確認
                        kind = u.Decoy != null || !said ? QuestionKind.Useful : QuestionKind.Confirm;
                        break;
                }
            }
            Questions.Add(new QuestionEvent { Slot = slot, Kind = kind, AtBeat = BeatsHeard });
            var line = new Line { FromSpeaker = true, Text = text, AnswerTo = slot };
            Transcript.Add(line);
            return line;
        }

        private string Restate(InfoUnit u)
        {
            if (u.Slot == Slot.Condition) return Phrases.Condition(_persona, u.Value, u.Value2);
            return Phrases.Plain(_persona, u.Slot, u.Value, 0);
        }

        private int ClosingBeat()
        {
            foreach (var b in _sc.Script.Beats) if (b.Kind == BeatKind.Closing) return b.Index;
            return _sc.Script.Beats.Count - 1;
        }

        private bool Spend(Slot? slot)
        {
            if (Patience <= 0)
            {
                Questions.Add(new QuestionEvent { Slot = slot, Kind = QuestionKind.Refused, AtBeat = BeatsHeard });
                return false;
            }
            Patience--;
            return true;
        }

        private Line Refuse()
        {
            var line = new Line { FromSpeaker = true, Text = _persona.NoPatience };
            Transcript.Add(line);
            return line;
        }

        /// <summary>その欄の最終的な言葉を、プレイヤーが実際に耳にしたか。</summary>
        public bool HeardFinal(InfoUnit u)
        {
            return Revealed.Contains(u.Slot) || (u.Beat >= 0 && u.Beat < BeatsHeard);
        }
    }
}
