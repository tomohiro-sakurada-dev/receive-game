using System;
using System.Collections.Generic;
using Receive.Core;
using UnityEngine.UIElements;

namespace Receive.Game
{
    /// <summary>試練の画面。会話、整理、申し送り（問い返し）、判定。</summary>
    public sealed partial class ReceiveApp
    {
        private IVisualElementScheduledItem _typeJob, _goneJob;

        private void BeginTrial()
        {
            _session.BeginTrial();
            ShowTalk();
        }

        // ---- 語りの表示 ----

        /// <summary>
        /// 言葉を一文字ずつ現す。語り終えると、しばらくして霧に溶ける（dissolve が真のとき）。
        /// まだ現れていない文字も透明なまま置いておくので、行の位置が途中で動かない。
        /// </summary>
        private void Say(Label label, string text, bool dissolve, Action done)
        {
            if (_typeJob != null) _typeJob.Pause();
            if (_goneJob != null) _goneJob.Pause();
            label.RemoveFromClassList("gone");
            int shown = 0, wait = 0;
            label.text = "<color=#00000000>" + text + "</color>";
            _typeJob = label.schedule.Execute(() =>
            {
                if (wait > 0) { wait--; return; }
                if (shown >= text.Length)
                {
                    _typeJob.Pause();
                    label.text = text;
                    if (dissolve)
                        _goneJob = label.schedule.Execute(() => label.AddToClassList("gone")).StartingIn(1700 + text.Length * 95);
                    if (done != null) done();
                    return;
                }
                shown++;
                label.text = text.Substring(0, shown) + "<color=#00000000>" + text.Substring(shown) + "</color>";
                char c = text[shown - 1];
                if (c == '。' || c == '…') wait = 7;
                else if (c == '、') wait = 3;
            }).Every(PaceMs[_pace]);
        }

        // ---- 会話 ----

        private void ShowTalk()
        {
            var sc = _session.Current;
            var talk = _session.Talk;
            _atmo.SetFog(0.72f);
            _atmo.SetLantern(_session.Journey.Mode == Mode.Practice ? 1f : _session.Journey.Lanterns / (float)Journey.LanternsMax);

            var speech = Ui.Text("", "speech");
            var note = Ui.Text(sc.Script.Scene, "narration");
            Button listen = null, ask = null, leave = null;
            Action<bool> enable = on => { listen.SetEnabled(on); ask.SetEnabled(on); leave.SetEnabled(on); };

            Action<Line> speak = line =>
            {
                enable(false);
                Say(speech, line.Text, true, () =>
                {
                    enable(true);
                    if (talk.MoodHint != null) note.text = talk.MoodHint;
                });
            };

            listen = Ui.Btn("続きを聴く", "btn btn-main", () =>
            {
                note.text = "";
                speak(talk.Listen());
            });
            ask = Ui.Btn("聞き返す", "btn", () => ShowAsk(talk, (question, line) =>
            {
                note.text = "あなた「" + question + "」";
                speak(line);
            }));
            leave = Ui.Btn("承りました", "btn", () =>
            {
                if (_typeJob != null) _typeJob.Pause();
                if (_goneJob != null) _goneJob.Pause();
                talk.Leave();
                ShowOrganize();
            });

            var hint = Ui.Text("言葉は一度きり。話が終わったかどうかは、自分で確かめること。", "t-tiny t-dim t-center");
            Show(Ui.Box("screen",
                StatusBar(),
                Ui.Space(70),
                Plate(talk.Persona.Glyph, sc.Script.SpeakerName),
                Ui.Box("speech-box", speech),
                note,
                Ui.Box("grow"),
                Ui.Box("row center", listen, ask, leave),
                Ui.Space(12),
                hint));

            // 最初の一言は、近づいたときに話し手のほうから始まる
            enable(false);
            speech.schedule.Execute(() => speak(talk.Listen())).StartingIn(900);
        }

        /// <summary>聞き返しの選択。問いの一覧はどの会話でも同じなので、出題の手がかりにならない。</summary>
        private void ShowAsk(Conversation talk, Action<string, Line> onAnswer)
        {
            var panel = Ui.Box("overlay-panel");
            panel.Add(Ui.Text("何を確かめるか", "t-large"));
            panel.Add(Ui.Text("聞き返すたびに、相手の辛抱をひとつ使う。", "t-small t-dim"));
            panel.Add(Ui.Space(18));
            var grid = Ui.Box("wrap");
            foreach (var s in Lexicon.AllSlots)
            {
                var slot = s;
                grid.Add(Ui.Btn(Lexicon.Question(slot), "btn", () =>
                {
                    CloseOverlay();
                    onAnswer(Lexicon.Question(slot), talk.Ask(slot));
                }));
            }
            grid.Add(Ui.Btn("いまのお言葉を、もう一度", "btn", () =>
            {
                CloseOverlay();
                onAnswer("いまのお言葉を、もう一度お願いします", talk.AskRepeat());
            }));
            panel.Add(grid);
            panel.Add(Ui.Space(14));
            panel.Add(Ui.Box("row", Ui.Box("grow"), Ui.Btn("やめる", "btn btn-quiet", CloseOverlay)));
            Overlay(panel);
        }

        // ---- 整理 ----

        private void ShowOrganize()
        {
            var sc = _session.Current;
            var talk = _session.Talk;
            var sheet = _session.Sheet;
            _atmo.SetFog(0.6f);

            var rows = Ui.Box("panel");
            var message = Ui.Text("", "t-small t-amber grow t-center");
            var reply = Ui.Text("", "speech");
            reply.style.fontSize = 34;

            foreach (var s in Lexicon.AllSlots)
            {
                var slot = s;
                var entry = sheet[slot];
                var chips = new List<Button>();
                Button value = null, value2 = null;
                Action refresh = () =>
                {
                    for (int i = 0; i < chips.Count; i++)
                        chips[i].EnableInClassList("on", entry.Marked && (int)entry.Mark == i);
                    bool needs = entry.Marked && (entry.Mark == Mark.Heard || entry.Mark == Mark.Inferred);
                    value.style.visibility = needs ? Visibility.Visible : Visibility.Hidden;
                    bool has = !string.IsNullOrEmpty(entry.Value);
                    value.text = has ? Lexicon.Text(entry.Value) : (slot == Slot.Condition ? "どんな場合に" : "中身を選ぶ");
                    value.EnableInClassList("chip-empty", !has);
                    if (value2 != null)
                    {
                        value2.style.visibility = needs ? Visibility.Visible : Visibility.Hidden;
                        bool has2 = !string.IsNullOrEmpty(entry.Value2);
                        value2.text = has2 ? Lexicon.Text(entry.Value2) : "その時はどうする";
                        value2.EnableInClassList("chip-empty", !has2);
                    }
                    message.text = "";
                };

                var row = Ui.Box("slot-row", Ui.Text(Lexicon.Label(slot), "slot-label"));
                foreach (Mark m in new[] { Mark.Heard, Mark.Inferred, Mark.Unknown, Mark.NotSaid })
                {
                    var mark = m;
                    var chip = Ui.Btn(AnswerSheet.MarkLabel(mark), "chip", () =>
                    {
                        sheet.Set(slot, mark, entry.Value, entry.Value2);
                        refresh();
                    });
                    chips.Add(chip);
                    row.Add(chip);
                }
                row.Add(Ui.Gap(24));
                value = Ui.Btn("", "chip chip-value", () => ShowPicker(Lexicon.Label(slot), Lexicon.For(slot), t =>
                {
                    sheet.Set(slot, entry.Mark, t.Id, entry.Value2);
                    refresh();
                }));
                row.Add(value);
                if (slot == Slot.Condition)
                {
                    value2 = Ui.Btn("", "chip chip-value", () => ShowPicker("その時はどうする", Lexicon.Actions, t =>
                    {
                        sheet.Set(slot, entry.Mark, entry.Value, t.Id);
                        refresh();
                    }));
                    row.Add(value2);
                }
                refresh();
                rows.Add(row);
            }

            var confirm = Ui.Btn("話し手に確かめる", "btn", () => ShowAsk(talk, (question, line) =>
            {
                message.text = "";
                Say(reply, line.Text, true, null);
            }));
            var send = Ui.Btn("申し送る", "btn btn-main", () =>
            {
                string problem = sheet.Validate();
                if (problem != null) { message.text = problem; return; }
                if (_typeJob != null) _typeJob.Pause();
                if (_goneJob != null) _goneJob.Pause();
                ShowRelay();
            });

            var replyBox = Ui.Box("center", reply);
            replyBox.style.height = 96;
            Show(Ui.Box("screen",
                StatusBar(),
                Ui.Space(10),
                Ui.Box("row",
                    Ui.Text("整理", "t-head"),
                    Ui.Gap(30),
                    Ui.Text("受け取ったことを、欄ごとに。八つの欄すべてに、印をひとつ。", "t-small t-dim")),
                Ui.Space(10),
                rows,
                replyBox,
                Ui.Box("grow"),
                Ui.Box("row", confirm, message, send)));
        }

        /// <summary>中身を選ぶ。選べる語は、その欄についてこの世界にあるすべての語。会話によって変わらない。</summary>
        private void ShowPicker(string title, IReadOnlyList<Term> terms, Action<Term> onPick)
        {
            var panel = Ui.Box("overlay-panel");
            panel.Add(Ui.Text(title, "t-large"));
            panel.Add(Ui.Space(16));
            var grid = Ui.Box("wrap");
            foreach (var t in terms)
            {
                var term = t;
                grid.Add(Ui.Btn(term.Text, "chip chip-term", () => { CloseOverlay(); onPick(term); }));
            }
            panel.Add(grid);
            panel.Add(Ui.Space(14));
            panel.Add(Ui.Box("row", Ui.Box("grow"), Ui.Btn("やめる", "btn btn-quiet", CloseOverlay)));
            Overlay(panel);
        }

        // ---- 申し送りと、受け手の問い返し ----

        private void ShowRelay()
        {
            var sc = _session.Current;
            var sheet = _session.Sheet;
            sheet.Replies.Clear();
            _atmo.SetFog(0.66f);

            var speech = Ui.Text("", "speech");
            var progress = Ui.Text("", "t-tiny t-dim t-center");
            var buttons = Ui.Box("row center");
            int index = 0;
            Action next = null;
            Action<ProbeReply> answer = r =>
            {
                sheet.Replies.Add(r);
                index++;
                next();
            };
            next = () =>
            {
                buttons.SetEnabled(false);
                if (index >= sc.Key.Probes.Count)
                {
                    if (_typeJob != null) _typeJob.Pause();
                    FinishTrial();
                    return;
                }
                progress.text = "問い返し　" + Kanji(index + 1) + "　／　" + Kanji(sc.Key.Probes.Count);
                Say(speech, sc.Key.Probes[index].Text, false, () => buttons.SetEnabled(true));
            };
            buttons.Add(Ui.Btn(Evaluator.ReplyLabel(ProbeReply.Agree), "btn", () => answer(ProbeReply.Agree)));
            buttons.Add(Ui.Btn(Evaluator.ReplyLabel(ProbeReply.Disagree), "btn", () => answer(ProbeReply.Disagree)));
            buttons.Add(Ui.Btn(Evaluator.ReplyLabel(ProbeReply.NotSure), "btn", () => answer(ProbeReply.NotSure)));

            Show(Ui.Box("screen",
                StatusBar(),
                Ui.Space(70),
                Plate("取", sc.Script.ReceiverName),
                Ui.Box("speech-box", speech),
                Ui.Text(sc.Script.ReceiverName + "は、申し送りを聞き終えると、言い換えて確かめてきた。", "narration"),
                Ui.Box("grow"),
                buttons,
                Ui.Space(12),
                progress));
            speech.schedule.Execute(() => next()).StartingIn(800);
        }

        private void FinishTrial()
        {
            var j = _session.Finish(DateTime.UtcNow.Ticks);
            SaveLog();
            if (j.Outcome == Outcome.Distorted) { _atmo.Whiteout(); _atmo.SetFog(1f); _atmo.SetLantern(0.15f); }
            else if (j.Outcome == Outcome.Fulfilled) { _atmo.SetFog(0.12f); _atmo.SetLantern(1f); }
            else _atmo.SetFog(0.6f);
            ShowVerdict(j);
        }

        // ---- 判定 ----

        private void ShowVerdict(Judgement j)
        {
            var sc = _session.Current;
            var talk = _session.Talk;
            var step = _session.LastStep;

            string color = j.Outcome == Outcome.Fulfilled ? "c-fulfilled" : j.Outcome == Outcome.Distorted ? "c-distorted" : "c-incomplete";
            var left = Ui.Box("col");
            left.style.width = 250;
            left.style.alignItems = Align.Center;
            left.Add(Ui.Vertical(Rules.OutcomeLabel(j.Outcome), "vchar-verdict " + color));
            if (j.Rank != Rank.None)
            {
                left.Add(Ui.Space(10));
                left.Add(Ui.Text("格　" + Rules.RankLabel(j.Rank), "t-large t-center " + color));
            }

            string headline;
            switch (j.Outcome)
            {
                case Outcome.Fulfilled: headline = "言葉は、歪まずに届いた。"; break;
                case Outcome.Incomplete: headline = "偽りは渡していない。だが、務めは果たせていない。"; break;
                case Outcome.Distorted: headline = "言葉が歪んだ。霧が、道を閉ざす。"; break;
                default: headline = "この試練は無効。何も失われない。"; break;
            }
            if (j.CutOff) headline += "　（話を途中で切り上げた）";
            if (step == StepResult.RunCompleted) headline += "　道行き成就。次の地への道が開いた。";
            if (step == StepResult.RunFailed && j.Outcome != Outcome.Distorted) headline += "　灯火が尽きた。";

            var cards = new ScrollView(ScrollViewMode.Vertical);
            cards.style.flexGrow = 1;
            int shownCards = 0;
            foreach (var f in j.Findings)
            {
                if (f.Type == FindingType.Correct || f.Type == FindingType.CorrectlyUnknown) continue;
                cards.Add(Card(f, sc, talk));
                shownCards++;
            }
            if (shownCards == 0 && j.Outcome != Outcome.Void)
                cards.Add(Ui.Box("card card-none", Ui.Text("八つの欄すべてを、話し手の言葉どおりに受け取り、問い返しにも流されなかった。", "quote")));

            var skills = Ui.Box("wrap");
            foreach (var s in SkillInfo.All)
            {
                var t = j.Skills[(int)s];
                var cell = Ui.Box("skill-cell col",
                    Ui.Text(SkillInfo.Name(s), "t-tiny"),
                    Ui.Text(t.Total == 0 ? "機会なし" : t.Ok + " / " + t.Total, "t-small " + (t.Total > 0 && t.Ok < t.Total ? "t-amber" : "t-dim")),
                    Ui.Meter(t.Ok, t.Total));
                skills.Add(cell);
            }

            var actions = Ui.Box("row");
            actions.Add(Ui.Box("grow"));
            switch (step)
            {
                case StepResult.Advanced:
                    actions.Add(Ui.Btn("次の試練へ", "btn btn-main", ShowGate));
                    break;
                case StepResult.Retry:
                    actions.Add(Ui.Btn("別の話で、もう一度", "btn btn-main", ShowGate));
                    break;
                case StepResult.RunFailed:
                    actions.Add(Ui.Btn("里へ戻る", "btn btn-quiet", ShowTitle));
                    actions.Add(Ui.Btn("結び灯籠から、やり直す", "btn btn-main", () => { _session.StartJourney(Mode.Trial, _session.Journey.Level); ShowGate(); }));
                    break;
                case StepResult.RunCompleted:
                    actions.Add(Ui.Btn("里へ戻る", "btn btn-main", ShowTitle));
                    break;
                default:
                    actions.Add(Ui.Btn("里へ戻る", "btn btn-quiet", ShowTitle));
                    actions.Add(Ui.Btn(step == StepResult.PracticeDone ? "もう一度、稽古する" : "もう一度", "btn btn-main", ShowGate));
                    break;
            }

            var right = Ui.Box("col grow",
                Ui.Text(headline, "t-large"),
                Ui.Space(14),
                cards,
                Ui.Space(8),
                Ui.Text("この試練での七つの技", "t-tiny t-dim"),
                skills,
                actions);
            Show(Ui.Box("screen", Ui.Box("row grow", left, Ui.Gap(40), right)));
        }

        /// <summary>誤りの一件。話し手が実際に言った言葉と、自分が渡した言葉を並べる。</summary>
        private static VisualElement Card(Finding f, Scenario sc, Conversation talk)
        {
            string sevClass = f.Severity == Severity.A ? "card-a" : f.Severity == Severity.B ? "card-b" : f.Severity == Severity.C ? "card-c" : "card-none";
            string tagClass = f.Severity == Severity.A ? "tag tag-a" : f.Severity == Severity.B ? "tag tag-b" : "tag";
            string sev = Rules.SeverityLabel(f.Severity);
            var head = Ui.Box("row",
                Ui.Text(Lexicon.Label(f.Slot), "t"),
                Ui.Gap(18),
                Ui.Text(Rules.TypeLabel(f.Type), "t t-amber"),
                Ui.Text(sev.Length > 0 ? sev : "罰なし", tagClass));
            if (f.Advisory) head.Add(Ui.Text("参考", "tag"));

            var card = Ui.Box("card " + sevClass, head, Ui.Space(6));
            string heard = f.QuoteVerbatim ? "「" + f.SpeakerQuote + "」" : f.SpeakerQuote;
            card.Add(Ui.Text((f.Unheard ? "話し手（聞かずに去った言葉）　" : "話し手　") + heard, "quote"));
            card.Add(Ui.Text("あなた　" + f.PlayerQuote, "quote"));
            if (!f.FromProbe) card.Add(Ui.Text("手本　　" + Lexicon.Label(f.Slot) + "：" + ModelAnswer.Describe(sc.Key.Unit(f.Slot), talk), "quote t-dim"));
            card.Add(Ui.Space(4));
            card.Add(Ui.Text(f.Explanation, "t-small t-dim"));
            return card;
        }
    }
}
