using System.Collections.Generic;

namespace Receive.Core
{
    /// <summary>
    /// 申し送りを、封印した評価データと、実際に語られた言葉に照らして判定する。
    /// すべて語彙の識別子の照合で決まる。AI は使わない。
    /// </summary>
    public static class Evaluator
    {
        public static Judgement Evaluate(Scenario sc, Conversation conv, AnswerSheet sheet)
        {
            var j = new Judgement { CutOff = conv.CutOff };
            // 封印の確認。出題後に評価データが変わっていたら、罰を出さず無効にする
            j.SealOk = Seal.Compute(sc.Key.Canonical()) == sc.Seal;
            if (!j.SealOk) { j.Outcome = Outcome.Void; return j; }

            var unitFindings = new Dictionary<Slot, Finding>();
            foreach (var u in sc.Key.Units)
            {
                var f = JudgeUnit(sc, conv, sheet, u);
                Rules.CapBySource(f);
                unitFindings[u.Slot] = f;
                j.Findings.Add(f);
            }

            var probeOk = new List<bool>();
            for (int i = 0; i < sc.Key.Probes.Count && i < sheet.Replies.Count; i++)
            {
                var f = JudgeProbe(sc, conv, sheet, i);
                probeOk.Add(f == null);
                if (f != null) { Rules.CapBySource(f); j.Findings.Add(f); }
            }

            TallySkills(sc, conv, sheet, unitFindings, probeOk, j);

            if (j.Count(Severity.A) > 0) j.Outcome = Outcome.Distorted;
            else
            {
                bool incomplete = false;
                foreach (var u in sc.Key.Units)
                {
                    if (!u.HasValue || u.Importance != Importance.Critical) continue;
                    var t = unitFindings[u.Slot].Type;
                    if (t != FindingType.Correct && t != FindingType.Underconfident) incomplete = true;
                }
                j.Outcome = incomplete ? Outcome.Incomplete : Outcome.Fulfilled;
            }
            j.LanternLoss = j.Count(Severity.B);
            if (j.Outcome == Outcome.Fulfilled)
                j.Rank = j.Count(Severity.B) > 0 ? Rank.Nigori : j.Count(Severity.C) > 0 ? Rank.Sumi : Rank.Meikyo;
            return j;
        }

        /// <summary>聞き返しを踏まえた、その欄の実際の状態。あいまいな欄は、聞き返していれば「はっきり言われた」になる。</summary>
        public static KeyState Effective(InfoUnit u, Conversation conv)
        {
            if (u.State == KeyState.Vague && conv.Revealed.Contains(u.Slot)) return KeyState.Stated;
            return u.State;
        }

        private static bool Same(Slot s, string v, string v2, string w, string w2)
        {
            return v == w && (s != Slot.Condition || v2 == w2);
        }

        private static Finding JudgeUnit(Scenario sc, Conversation conv, AnswerSheet sheet, InfoUnit u)
        {
            var e = sheet[u.Slot];
            var f = new Finding { Slot = u.Slot, PlayerQuote = Lexicon.Label(u.Slot) + "：" + sheet.Describe(u.Slot) };
            SetSpeakerQuote(sc, conv, u, f);
            var eff = Effective(u, conv);
            bool hasVal = e.Mark == Mark.Heard || e.Mark == Mark.Inferred;
            bool unsaid = eff == KeyState.NotStated || eff == KeyState.Vague;
            f.Unheard = u.HasValue && !conv.HeardFinal(u);

            if (eff == KeyState.NotStated)
            {
                switch (e.Mark)
                {
                    case Mark.NotSaid:
                        f.Type = FindingType.CorrectlyUnknown;
                        f.Explanation = "この欄について、話し手は何も言っていない。言われていないと見極めた。";
                        break;
                    case Mark.Unknown:
                        f.Type = FindingType.FalseGap;
                        f.Explanation = "この欄について、話し手は何も言っていない。聞き漏らしではなかった。";
                        break;
                    case Mark.Heard:
                        f.Type = FindingType.Fabrication;
                        f.Explanation = "話し手は、この欄について何も言っていない。それを「聞いた」こととして渡した。"
                                        + (u.DistractorValue != null ? "「" + Lexicon.Text(u.DistractorValue) + "」は、別の話の中に出た言葉。" : "");
                        break;
                    default:
                        f.Type = FindingType.UnneededGuess;
                        f.Explanation = "話し手が言っていないことに、自分の推し量りを足した。";
                        break;
                }
            }
            else if (eff == KeyState.Vague)
            {
                string w = "話し手は「" + u.VagueWord + "」としか言っていない。";
                if (e.Mark == Mark.Heard) { f.Type = FindingType.Fabrication; f.Explanation = w + "確かめずに、中身を決めつけた。"; }
                else if (e.Mark == Mark.Inferred) { f.Type = FindingType.UnneededGuess; f.Explanation = w + "確かめずに、自分の推し量りを足した。"; }
                else { f.Type = FindingType.UnresolvedVague; f.Explanation = w + "聞き返せば分かった。"; }
            }
            else
            {
                bool guessy = eff == KeyState.Guess || eff == KeyState.Hearsay;
                bool match = hasVal && Same(u.Slot, e.Value, e.Value2, u.Value, u.Value2);
                if (hasVal && !match)
                {
                    if (u.Decoy != null && e.Value == u.Decoy) f.Cause = Cause.PreCorrection;
                    else if (u.Slot == Slot.Condition && (e.Value == u.Value || e.Value2 == u.Value2)) f.Cause = Cause.PartOfCondition;
                }
                if (f.Unheard && conv.CutOff && f.Cause == Cause.None) f.Cause = Cause.CutOff;

                if (e.Mark == Mark.Heard)
                {
                    if (match && guessy)
                    {
                        f.Type = FindingType.Overconfident;
                        f.Explanation = "話し手は、" + (eff == KeyState.Guess ? "自分の推測" : "人づての話") + "として語った。それを確かなこととして渡した。";
                    }
                    else if (match) { f.Type = FindingType.Correct; f.Explanation = "話し手の言葉どおり。"; }
                    else { f.Type = FindingType.Misheard; f.Explanation = MisheardWhy(f.Cause); }
                }
                else if (e.Mark == Mark.Inferred)
                {
                    if (match && guessy) { f.Type = FindingType.Correct; f.Explanation = "確かでない話を、確かでないものとして受け取った。"; }
                    else if (match) { f.Type = FindingType.Underconfident; f.Explanation = "話し手は、はっきり言っている。それを推し量りとして渡した。"; }
                    else { f.Type = FindingType.HedgedWrong; f.Explanation = "推し量りとして渡したが、" + MisheardWhy(f.Cause); }
                }
                else if (e.Mark == Mark.Unknown)
                {
                    f.Type = FindingType.DeclaredGap;
                    f.Explanation = (f.Cause == Cause.CutOff ? "話を切り上げたため、この言葉を聞いていない。" : "話し手は言っている。")
                                    + "受け取れなかったことを、正直に述べた。";
                }
                else
                {
                    f.Type = FindingType.UnawareOmission;
                    f.Explanation = (f.Cause == Cause.CutOff ? "話を切り上げたため、この言葉を聞いていない。" : "話し手は言っている。")
                                    + "それを「言われていない」とした。";
                }
            }
            f.Severity = Rules.SeverityOf(f.Type, u.Importance, unsaid);
            return f;
        }

        private static string MisheardWhy(Cause c)
        {
            switch (c)
            {
                case Cause.PreCorrection: return "話し手は言い直している。言い直す前の言葉を受け取った。";
                case Cause.PartOfCondition: return "ただし書きの片方が、話し手の言葉と違う。";
                case Cause.CutOff: return "話を切り上げたため、この言葉を聞いていない。中身が違う。";
                default: return "話し手の言葉と、中身が違う。";
            }
        }

        private static void SetSpeakerQuote(Scenario sc, Conversation conv, InfoUnit u, Finding f)
        {
            if (conv.Revealed.Contains(u.Slot))
            {
                for (int i = conv.Transcript.Count - 1; i >= 0; i--)
                {
                    var l = conv.Transcript[i];
                    if (l.FromSpeaker && l.AnswerTo == u.Slot) { f.SpeakerQuote = l.Text; f.QuoteVerbatim = true; return; }
                }
            }
            if (u.Beat >= 0)
            {
                string q = sc.Script.Beats[u.Beat].Text;
                if (u.DecoyBeat >= 0 && u.DecoyBeat != u.Beat) q = sc.Script.Beats[u.DecoyBeat].Text + " …… " + q;
                f.SpeakerQuote = q; f.QuoteVerbatim = true;
                return;
            }
            if (u.DistractorValue != null)
            {
                string word = Lexicon.Text(u.DistractorValue);
                foreach (var b in sc.Script.Beats)
                    if (b.Kind == BeatKind.Chatter && b.Text.Contains(word)) { f.SpeakerQuote = b.Text; f.QuoteVerbatim = true; return; }
            }
            f.SpeakerQuote = "（この欄についての言葉はない）";
            f.QuoteVerbatim = false;
        }

        public static string ReplyLabel(ProbeReply r)
        {
            switch (r)
            {
                case ProbeReply.Agree: return "その通りです";
                case ProbeReply.Disagree: return "いいえ、違います";
                default: return "そこまでは聞いていません";
            }
        }

        /// <summary>問い返しへの正しい答え。話し手の言葉だけから決まる。</summary>
        public static ProbeReply ExpectedReply(Scenario sc, Conversation conv, int index)
        {
            var p = sc.Key.Probes[index];
            var u = sc.Key.Unit(p.Slot);
            var eff = Effective(u, conv);
            if (eff == KeyState.NotStated || eff == KeyState.Vague) return ProbeReply.NotSure;
            bool pm = Same(p.Slot, p.Value, p.Value2, u.Value, u.Value2);
            if (eff == KeyState.Guess || eff == KeyState.Hearsay) return pm ? ProbeReply.NotSure : ProbeReply.Disagree;
            return pm ? ProbeReply.Agree : ProbeReply.Disagree;
        }

        /// <summary>
        /// 問い返しでの歪曲を見る。受け取りの誤り（申し送りの欄で既に数えたもの）とは分けて数える。
        /// 自分の申し送りと食い違う答えをしたときだけ、歪曲とする。
        /// </summary>
        private static Finding JudgeProbe(Scenario sc, Conversation conv, AnswerSheet sheet, int index)
        {
            var p = sc.Key.Probes[index];
            var r = sheet.Replies[index];
            var u = sc.Key.Unit(p.Slot);
            var e = sheet[p.Slot];
            var eff = Effective(u, conv);
            bool unsaid = eff == KeyState.NotStated || eff == KeyState.Vague;
            bool guessy = eff == KeyState.Guess || eff == KeyState.Hearsay;
            bool pm = !unsaid && Same(p.Slot, p.Value, p.Value2, u.Value, u.Value2);
            var truth = ExpectedReply(sc, conv, index);
            bool ownHasValue = e.Mark == Mark.Heard || e.Mark == Mark.Inferred;
            bool ownSame = ownHasValue && Same(p.Slot, e.Value, e.Value2, p.Value, p.Value2);
            bool ownHeardSame = ownSame && e.Mark == Mark.Heard;
            if (r == truth) return null;

            FindingType type; Severity sev; string why;
            if (r == ProbeReply.Agree)
            {
                // 自分も同じ中身を「聞いた」としていたなら、受け取りの誤りとして既に数えている
                if (ownHeardSame) return null;
                type = FindingType.Distortion;
                sev = Rules.SeverityOf(type, u.Importance, unsaid);
                why = unsaid ? "受け手の言い換えには、話し手が言っていない中身が含まれていた。それに同意した。"
                    : guessy && pm ? "話し手は確かでない話として語った。受け手が確かなこととして言い換え、それに同意した。"
                    : "受け手の言い換えは、話し手の言葉と中身が違っていた。それに同意した。";
            }
            else if (truth == ProbeReply.Agree)
            {
                if (!ownHeardSame) return null;
                type = FindingType.Retraction;
                sev = r == ProbeReply.Disagree ? Rules.SeverityOf(type, u.Importance, false) : Severity.C;
                why = r == ProbeReply.Disagree ? "正しく受け取っていたことを、問い返しで自ら覆した。"
                                               : "正しく受け取っていたことを、問い返しであいまいにした。";
            }
            else if (truth == ProbeReply.Disagree && r == ProbeReply.NotSure)
            {
                if (!(e.Mark == Mark.Heard && !ownSame)) return null;
                type = FindingType.Retraction; sev = Severity.C;
                why = "受け手の言い換えは違っていた。違うと分かっていたはずのことを、あいまいにした。";
            }
            else
            {
                if (!(guessy && ownSame)) return null;
                type = FindingType.Retraction; sev = Severity.C;
                why = "確かでない話として受け取っていたことを、問い返しで打ち消した。";
            }

            var f = new Finding { Slot = p.Slot, Type = type, Severity = sev, FromProbe = true, Explanation = why };
            SetSpeakerQuote(sc, conv, u, f);
            f.PlayerQuote = "受け手「" + p.Text + "」→「" + ReplyLabel(r) + "」";
            return f;
        }

        private static void TallySkills(Scenario sc, Conversation conv, AnswerSheet sheet,
                                        Dictionary<Slot, Finding> uf, List<bool> probeOk, Judgement j)
        {
            j.Skills[(int)Skill.ListenToEnd].Add(!conv.CutOff);
            foreach (var u in sc.Key.Units)
            {
                var e = sheet[u.Slot];
                var eff = Effective(u, conv);
                bool hasVal = e.Mark == Mark.Heard || e.Mark == Mark.Inferred;
                if (eff == KeyState.NotStated || eff == KeyState.Vague)
                {
                    j.Skills[(int)Skill.NoFilling].Add(!hasVal);
                    j.Skills[(int)Skill.KnowUnknown].Add(eff == KeyState.Vague ? !hasVal : e.Mark == Mark.NotSaid);
                    continue;
                }
                bool guessy = eff == KeyState.Guess || eff == KeyState.Hearsay;
                bool match = hasVal && Same(u.Slot, e.Value, e.Value2, u.Value, u.Value2);
                if (u.Late) j.Skills[(int)Skill.ListenToEnd].Add(match);
                else j.Skills[(int)Skill.Accuracy].Add(match);
                if (match) j.Skills[(int)Skill.FactVsGuess].Add(guessy ? e.Mark == Mark.Inferred : e.Mark == Mark.Heard);
                else j.Skills[(int)Skill.KnowUnknown].Add(e.Mark == Mark.Unknown);
            }

            for (int i = 0; i < probeOk.Count; i++)
            {
                var p = sc.Key.Probes[i];
                var u = sc.Key.Unit(p.Slot);
                var eff = Effective(u, conv);
                var r = sheet.Replies[i];
                j.Skills[(int)Skill.FaithfulRelay].Add(probeOk[i]);
                if (eff == KeyState.NotStated || eff == KeyState.Vague)
                    j.Skills[(int)Skill.NoFilling].Add(r != ProbeReply.Agree);
                else if ((eff == KeyState.Guess || eff == KeyState.Hearsay) && Same(p.Slot, p.Value, p.Value2, u.Value, u.Value2))
                    j.Skills[(int)Skill.FactVsGuess].Add(r != ProbeReply.Agree);
            }

            foreach (var q in conv.Questions)
            {
                if (q.Kind == QuestionKind.Useful) j.Skills[(int)Skill.Questioning].Add(true);
                else if (q.Kind == QuestionKind.Premature) j.Skills[(int)Skill.Questioning].Add(false);
            }
            // 確かめるべきだったのに確かめなかった
            foreach (var u in sc.Key.Units)
                if (u.State == KeyState.Vague && !conv.Revealed.Contains(u.Slot))
                    j.Skills[(int)Skill.Questioning].Add(false);
        }
    }
}
