using NUnit.Framework;
using Receive.Core;

namespace Receive.Tests
{
    /// <summary>評価と罰の規則。成功・聞き漏らし・誤認・根拠のない断定・不明の申告・歪曲を区別できること。</summary>
    public class EvaluationTests
    {
        private static Scenario WithCondition(int level = 2)
        {
            return TestKit.Find(level, s => s.Key.Unit(Slot.Condition).HasValue && TestKit.NoVague(s));
        }

        private static Scenario WithPlainPlace()
        {
            return TestKit.Find(3, s =>
            {
                var u = s.Key.Unit(Slot.Place);
                return u.State == KeyState.Stated && u.Decoy == null && TestKit.NoVague(s);
            });
        }

        // ---- 正しい回答 ----

        [Test]
        public void 正しい回答は成就し罰がない()
        {
            var sc = TestKit.Find(3, TestKit.NoVague);
            var c = TestKit.HearAll(sc);
            var j = Evaluator.Evaluate(sc, c, TestKit.Model(sc, c));
            Assert.AreEqual(Outcome.Fulfilled, j.Outcome);
            Assert.AreEqual(Rank.Meikyo, j.Rank);
            Assert.AreEqual(0, j.Count(Severity.A) + j.Count(Severity.B) + j.Count(Severity.C));
            Assert.AreEqual(0, j.LanternLoss);
            Assert.IsTrue(j.SealOk);
        }

        // ---- 重要情報の聞き漏らし ----

        [Test]
        public void 最重要の条件を言われていないとすると聞き漏らしで歪みになる()
        {
            var sc = WithCondition();
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(Slot.Condition, Mark.NotSaid);
            var j = Evaluator.Evaluate(sc, c, sheet);
            var f = TestKit.Of(j, Slot.Condition);
            Assert.AreEqual(FindingType.UnawareOmission, f.Type);
            Assert.AreEqual(Severity.A, f.Severity);
            Assert.AreEqual(Outcome.Distorted, j.Outcome);
            Assert.IsTrue(f.QuoteVerbatim);
            StringAssert.Contains("ただし", f.SpeakerQuote);
        }

        [Test]
        public void 重要度が中の欄の聞き漏らしは灯火を1つ減らすだけ()
        {
            var sc = WithPlainPlace();
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(Slot.Place, Mark.NotSaid);
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.UnawareOmission, TestKit.Of(j, Slot.Place).Type);
            Assert.AreEqual(Severity.B, TestKit.Of(j, Slot.Place).Severity);
            Assert.AreEqual(Outcome.Fulfilled, j.Outcome);
            Assert.AreEqual(Rank.Nigori, j.Rank);
            Assert.AreEqual(1, j.LanternLoss);
        }

        // ---- 不明の適切な申告（聞き漏らしと区別する） ----

        [Test]
        public void 分からないと正直に申告すれば罰はなく未達になる()
        {
            var sc = WithCondition();
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(Slot.Condition, Mark.Unknown);
            var j = Evaluator.Evaluate(sc, c, sheet);
            var f = TestKit.Of(j, Slot.Condition);
            Assert.AreEqual(FindingType.DeclaredGap, f.Type);
            Assert.AreEqual(Severity.None, f.Severity);
            Assert.AreEqual(Outcome.Incomplete, j.Outcome);
            Assert.AreEqual(0, j.LanternLoss);
            Assert.AreEqual(0, j.Count(Severity.A));
        }

        [Test]
        public void 同じ欠落でも申告と聞き漏らしは別の種類として記録される()
        {
            var sc = WithCondition();
            var c = TestKit.HearAll(sc);
            var declared = TestKit.Model(sc, c); declared.Set(Slot.Condition, Mark.Unknown);
            var omitted = TestKit.Model(sc, c); omitted.Set(Slot.Condition, Mark.NotSaid);
            var jd = Evaluator.Evaluate(sc, c, declared);
            var jo = Evaluator.Evaluate(sc, c, omitted);
            Assert.AreNotEqual(TestKit.Of(jd, Slot.Condition).Type, TestKit.Of(jo, Slot.Condition).Type);
            Assert.AreNotEqual(jd.Outcome, jo.Outcome);
            // 「分からないと知る」の技能は、申告したほうだけが成功になる
            Assert.Greater(jd.Skills[(int)Skill.KnowUnknown].Ok, jo.Skills[(int)Skill.KnowUnknown].Ok);
        }

        [Test]
        public void 言われていないのに分からないとしても罰はない()
        {
            var sc = TestKit.Find(2, TestKit.NoVague);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            var slot = TestKit.FirstUnsaid(sc);
            sheet.Set(slot, Mark.Unknown);
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.FalseGap, TestKit.Of(j, slot).Type);
            Assert.AreEqual(Severity.None, TestKit.Of(j, slot).Severity);
            Assert.AreEqual(Outcome.Fulfilled, j.Outcome);
        }

        // ---- 情報の一部だけを誤認 ----

        [Test]
        public void 日だけを取り違えると日だけが誤認になり他は正しいまま()
        {
            var sc = TestKit.Find(3, s => TestKit.NoVague(s) && s.Key.Unit(Slot.Day).Decoy == null);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(Slot.Day, Mark.Heard, TestKit.OtherValue(Slot.Day, sc.Key.Unit(Slot.Day).Value));
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.Misheard, TestKit.Of(j, Slot.Day).Type);
            Assert.AreEqual(Severity.A, TestKit.Of(j, Slot.Day).Severity);
            foreach (var s in Lexicon.AllSlots)
            {
                if (s == Slot.Day) continue;
                var t = TestKit.Of(j, s).Type;
                Assert.IsTrue(t == FindingType.Correct || t == FindingType.CorrectlyUnknown, s + " は正しいはず: " + t);
            }
            Assert.AreEqual(Outcome.Distorted, j.Outcome);
        }

        [Test]
        public void 場所だけを取り違えると中程度のミスになる()
        {
            var sc = WithPlainPlace();
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(Slot.Place, Mark.Heard, TestKit.OtherValue(Slot.Place, sc.Key.Unit(Slot.Place).Value));
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.Misheard, TestKit.Of(j, Slot.Place).Type);
            Assert.AreEqual(Severity.B, TestKit.Of(j, Slot.Place).Severity);
            Assert.AreEqual(Outcome.Fulfilled, j.Outcome);
            Assert.AreEqual(1, j.Count(Severity.B));
        }

        [Test]
        public void ただし書きの片方だけ違うときは原因が記録される()
        {
            var sc = WithCondition();
            var u = sc.Key.Unit(Slot.Condition);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            string wrongAction = Lexicon.Actions[0].Id == u.Value2 ? Lexicon.Actions[1].Id : Lexicon.Actions[0].Id;
            sheet.Set(Slot.Condition, Mark.Heard, u.Value, wrongAction);
            var f = TestKit.Of(Evaluator.Evaluate(sc, c, sheet), Slot.Condition);
            Assert.AreEqual(FindingType.Misheard, f.Type);
            Assert.AreEqual(Cause.PartOfCondition, f.Cause);
        }

        [Test]
        public void 言い直す前の値を答えると早合点として記録される()
        {
            var sc = TestKit.Find(4, s => TestKit.Has(s, TrapKind.Correction) && TestKit.NoVague(s));
            InfoUnit u = null;
            foreach (var x in sc.Key.Units) if (x.Decoy != null) u = x;
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(u.Slot, Mark.Heard, u.Decoy);
            var j = Evaluator.Evaluate(sc, c, sheet);
            var f = TestKit.Of(j, u.Slot);
            Assert.AreEqual(FindingType.Misheard, f.Type);
            Assert.AreEqual(Cause.PreCorrection, f.Cause);
            Assert.Less(j.Skills[(int)Skill.ListenToEnd].Ok, j.Skills[(int)Skill.ListenToEnd].Total);
        }

        // ---- 根拠のない推測・断定 ----

        [Test]
        public void 言われていないことを聞いたこととして渡すと根拠のない断定で歪みになる()
        {
            var sc = TestKit.Find(2, TestKit.NoVague);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            var slot = TestKit.FirstUnsaid(sc);
            sheet.Set(slot, Mark.Heard, Lexicon.For(slot)[0].Id, Lexicon.Actions[0].Id);
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.Fabrication, TestKit.Of(j, slot).Type);
            Assert.AreEqual(Severity.A, TestKit.Of(j, slot).Severity);
            Assert.AreEqual(Outcome.Distorted, j.Outcome);
        }

        [Test]
        public void 言われていないことへの推し量りは軽微なミスにとどまる()
        {
            var sc = TestKit.Find(2, TestKit.NoVague);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            var slot = TestKit.FirstUnsaid(sc);
            sheet.Set(slot, Mark.Inferred, Lexicon.For(slot)[0].Id, Lexicon.Actions[0].Id);
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.UnneededGuess, TestKit.Of(j, slot).Type);
            Assert.AreEqual(Severity.C, TestKit.Of(j, slot).Severity);
            Assert.AreEqual(Outcome.Fulfilled, j.Outcome);
            Assert.AreEqual(Rank.Sumi, j.Rank);
        }

        [Test]
        public void 話し手の推測や伝聞を確かなこととして渡すと根拠のない断定になる()
        {
            var sc = TestKit.Find(3, s => (TestKit.Has(s, TrapKind.Guess) || TestKit.Has(s, TrapKind.Hearsay)) && TestKit.NoVague(s));
            InfoUnit u = null;
            foreach (var x in sc.Key.Units) if (x.State == KeyState.Guess || x.State == KeyState.Hearsay) u = x;
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(u.Slot, Mark.Heard, u.Value);
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.Overconfident, TestKit.Of(j, u.Slot).Type);
            Assert.AreEqual(Severity.B, TestKit.Of(j, u.Slot).Severity);
            Assert.Less(j.Skills[(int)Skill.FactVsGuess].Ok, j.Skills[(int)Skill.FactVsGuess].Total);
        }

        [Test]
        public void はっきり言われたことを推し量りにすると中程度で成就はする()
        {
            var sc = TestKit.Find(2, s => TestKit.NoVague(s) && s.Key.Unit(Slot.Day).Decoy == null);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(Slot.Day, Mark.Inferred, sc.Key.Unit(Slot.Day).Value);
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.Underconfident, TestKit.Of(j, Slot.Day).Type);
            Assert.AreEqual(Severity.B, TestKit.Of(j, Slot.Day).Severity);
            Assert.AreEqual(Outcome.Fulfilled, j.Outcome);
            Assert.AreEqual(Rank.Nigori, j.Rank);
        }

        [Test]
        public void すべて分からないとしても成就にはならず歪みにもならない()
        {
            var sc = TestKit.Find(3, TestKit.NoVague);
            var c = TestKit.HearAll(sc);
            var sheet = new AnswerSheet();
            foreach (var s in Lexicon.AllSlots) sheet.Set(s, Mark.Unknown);
            for (int i = 0; i < sc.Key.Probes.Count; i++) sheet.Replies.Add(ProbeReply.NotSure);
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(Outcome.Incomplete, j.Outcome);
            Assert.AreEqual(0, j.Count(Severity.A));
        }

        // ---- 最後まで聞く ----

        [Test]
        public void 話を切り上げて聞かなかった条件を言われていないとすると原因が切り上げになる()
        {
            var sc = TestKit.Find(3, s => TestKit.Has(s, TrapKind.Afterthought) && TestKit.NoVague(s)
                                         && s.Key.Unit(Slot.Condition).Beat == s.Script.Beats.Count - 1);
            var c = new Conversation(sc);
            while (c.BeatsHeard < sc.Script.Beats.Count - 1) c.Listen(); // 結びの言葉まで聞いて立ち去る
            c.Leave();
            Assert.IsTrue(c.CutOff);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(Slot.Condition, Mark.NotSaid);
            var j = Evaluator.Evaluate(sc, c, sheet);
            var f = TestKit.Of(j, Slot.Condition);
            Assert.AreEqual(FindingType.UnawareOmission, f.Type);
            Assert.AreEqual(Cause.CutOff, f.Cause);
            Assert.IsTrue(f.Unheard);
            Assert.AreEqual(Outcome.Distorted, j.Outcome);
            Assert.AreEqual(0, j.Skills[(int)Skill.ListenToEnd].Ok);
        }

        // ---- 適切な聞き返し ----

        [Test]
        public void あいまいな言葉を確かめなければ未達_確かめれば成就()
        {
            var sc = TestKit.Find(3, s => TestKit.Has(s, TrapKind.Vague));
            var silent = TestKit.HearAll(sc);
            var j1 = Evaluator.Evaluate(sc, silent, TestKit.Model(sc, silent));
            Assert.AreEqual(Outcome.Incomplete, j1.Outcome);
            Assert.AreEqual(1, j1.Count(FindingType.UnresolvedVague));
            Assert.AreEqual(0, j1.LanternLoss);
            Assert.AreEqual(0, j1.Skills[(int)Skill.Questioning].Ok);
            Assert.AreEqual(1, j1.Skills[(int)Skill.Questioning].Total);

            var asked = TestKit.HearAllAndAskVague(sc);
            var j2 = Evaluator.Evaluate(sc, asked, TestKit.Model(sc, asked));
            Assert.AreEqual(Outcome.Fulfilled, j2.Outcome);
            Assert.AreEqual(Rank.Meikyo, j2.Rank);
            Assert.AreEqual(1, j2.Skills[(int)Skill.Questioning].Ok);
        }

        [Test]
        public void あいまいな言葉を確かめずに決めつけると根拠のない断定になる()
        {
            var sc = TestKit.Find(3, s => TestKit.Has(s, TrapKind.Vague));
            InfoUnit u = null;
            foreach (var x in sc.Key.Units) if (x.State == KeyState.Vague) u = x;
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(u.Slot, Mark.Heard, u.Value); // たまたま当たっていても、根拠はない
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.Fabrication, TestKit.Of(j, u.Slot).Type);
            Assert.AreEqual(Outcome.Distorted, j.Outcome);
        }

        // ---- 歪めずに伝える（受け手の問い返し） ----

        [Test]
        public void 受け手が足した中身に同意すると歪曲になる()
        {
            var sc = TestKit.Find(2, s => TestKit.NoVague(s) && UnsaidProbe(s) >= 0);
            int i = UnsaidProbe(sc);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Replies[i] = ProbeReply.Agree;
            var j = Evaluator.Evaluate(sc, c, sheet);
            var f = TestKit.ProbeFinding(j, sc.Key.Probes[i].Slot);
            Assert.IsNotNull(f);
            Assert.AreEqual(FindingType.Distortion, f.Type);
            Assert.AreEqual(Severity.A, f.Severity);
            Assert.AreEqual(Outcome.Distorted, j.Outcome);
            Assert.Less(j.Skills[(int)Skill.FaithfulRelay].Ok, j.Skills[(int)Skill.FaithfulRelay].Total);
            // 申し送りの欄そのものは正しいまま。受け取りの誤りとは別に数える
            Assert.AreEqual(FindingType.CorrectlyUnknown, TestKit.Of(j, sc.Key.Probes[i].Slot).Type);
        }

        [Test]
        public void 受け取りの誤りと同じ中身への同意は二重に数えない()
        {
            var sc = TestKit.Find(2, s => TestKit.NoVague(s) && UnsaidProbe(s) >= 0);
            int i = UnsaidProbe(sc);
            var p = sc.Key.Probes[i];
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Set(p.Slot, Mark.Heard, p.Value, p.Value2);
            sheet.Replies[i] = ProbeReply.Agree;
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.AreEqual(FindingType.Fabrication, TestKit.Of(j, p.Slot).Type);
            Assert.IsNull(TestKit.ProbeFinding(j, p.Slot));
            Assert.AreEqual(1, j.Count(Severity.A));
        }

        [Test]
        public void 正しく受け取ったことを問い返しで覆すと歪曲になる()
        {
            var sc = TestKit.Find(2, s => TestKit.NoVague(s) && FaithfulProbe(s) >= 0);
            int i = FaithfulProbe(sc);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Replies[i] = ProbeReply.Disagree;
            var j = Evaluator.Evaluate(sc, c, sheet);
            var f = TestKit.ProbeFinding(j, sc.Key.Probes[i].Slot);
            Assert.IsNotNull(f);
            Assert.AreEqual(FindingType.Retraction, f.Type);
            Assert.AreEqual(0, j.Count(Severity.A));
        }

        [Test]
        public void 推測を確かなこととして言い換えられて同意すると歪曲になる()
        {
            var sc = TestKit.Find(3, s => TestKit.NoVague(s) && GuessProbe(s) >= 0);
            int i = GuessProbe(sc);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sheet.Replies[i] = ProbeReply.Agree;
            var j = Evaluator.Evaluate(sc, c, sheet);
            var f = TestKit.ProbeFinding(j, sc.Key.Probes[i].Slot);
            Assert.IsNotNull(f);
            Assert.AreEqual(FindingType.Distortion, f.Type);
            Assert.AreEqual(Severity.B, f.Severity);
        }

        private static int UnsaidProbe(Scenario s)
        {
            for (int i = 0; i < s.Key.Probes.Count; i++) if (!s.Key.Unit(s.Key.Probes[i].Slot).HasValue) return i;
            return -1;
        }

        private static int FaithfulProbe(Scenario s)
        {
            for (int i = 0; i < s.Key.Probes.Count; i++)
            {
                var p = s.Key.Probes[i]; var u = s.Key.Unit(p.Slot);
                if (u.State == KeyState.Stated && u.Importance == Importance.Critical && p.Value == u.Value && p.Value2 == u.Value2) return i;
            }
            return -1;
        }

        private static int GuessProbe(Scenario s)
        {
            for (int i = 0; i < s.Key.Probes.Count; i++)
            {
                var p = s.Key.Probes[i]; var u = s.Key.Unit(p.Slot);
                if ((u.State == KeyState.Guess || u.State == KeyState.Hearsay) && p.Value == u.Value) return i;
            }
            return -1;
        }

        // ---- 技能は別々に測る ----

        [Test]
        public void 七つの技能は別々に数えられ一つのミスは関係する技能だけを下げる()
        {
            var sc = TestKit.Find(3, s => TestKit.NoVague(s) && (TestKit.Has(s, TrapKind.Guess) || TestKit.Has(s, TrapKind.Hearsay)));
            var c = TestKit.HearAll(sc);
            var good = Evaluator.Evaluate(sc, c, TestKit.Model(sc, c));
            foreach (var s in new[] { Skill.ListenToEnd, Skill.Accuracy, Skill.NoFilling, Skill.FactVsGuess, Skill.KnowUnknown, Skill.FaithfulRelay })
            {
                Assert.Greater(good.Skills[(int)s].Total, 0, SkillInfo.Name(s) + " に機会がない");
                Assert.AreEqual(good.Skills[(int)s].Total, good.Skills[(int)s].Ok, SkillInfo.Name(s));
            }

            var sheet = TestKit.Model(sc, c);
            var slot = TestKit.FirstUnsaid(sc);
            sheet.Set(slot, Mark.Heard, Lexicon.For(slot)[0].Id, Lexicon.Actions[0].Id);
            var bad = Evaluator.Evaluate(sc, c, sheet);
            Assert.Less(bad.Skills[(int)Skill.NoFilling].Ok, good.Skills[(int)Skill.NoFilling].Ok);
            Assert.AreEqual(good.Skills[(int)Skill.Accuracy].Ok, bad.Skills[(int)Skill.Accuracy].Ok);
            Assert.AreEqual(good.Skills[(int)Skill.FactVsGuess].Ok, bad.Skills[(int)Skill.FactVsGuess].Ok);
            Assert.AreEqual(good.Skills[(int)Skill.ListenToEnd].Ok, bad.Skills[(int)Skill.ListenToEnd].Ok);
        }

        // ---- 公平性の歯止め ----

        [Test]
        public void AI由来の指摘は致命的にならず参考扱いになる()
        {
            var ai = new Finding { Type = FindingType.Misheard, Severity = Severity.A, Source = EvidenceSource.AI };
            Rules.CapBySource(ai);
            Assert.AreEqual(Severity.B, ai.Severity);
            Assert.IsTrue(ai.Advisory);

            var det = new Finding { Type = FindingType.Misheard, Severity = Severity.A, Source = EvidenceSource.Deterministic };
            Rules.CapBySource(det);
            Assert.AreEqual(Severity.A, det.Severity);
            Assert.IsFalse(det.Advisory);
        }

        [Test]
        public void 出題後に評価データが変わっていたら無効になり罰は出ない()
        {
            var sc = TestKit.Find(2, TestKit.NoVague);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sc.Key.Unit(Slot.Day).Value = TestKit.OtherValue(Slot.Day, sc.Key.Unit(Slot.Day).Value); // 後付けの変更
            var j = Evaluator.Evaluate(sc, c, sheet);
            Assert.IsFalse(j.SealOk);
            Assert.AreEqual(Outcome.Void, j.Outcome);
            Assert.AreEqual(0, j.Findings.Count);
            var journey = new Journey(Mode.Trial, 2);
            Assert.AreEqual(StepResult.Void, journey.Apply(j));
            Assert.AreEqual(Journey.LanternsMax, journey.Lanterns);
        }

        [Test]
        public void すべてのミスの指摘には実際に語られた言葉の引用が付く()
        {
            var rng = new Rng(77);
            int checkedFindings = 0;
            for (long seed = 1; seed <= 300; seed++)
            {
                var sc = ScenarioGenerator.Generate(seed, 1 + (int)(seed % 5));
                var c = new Conversation(sc);
                int hear = rng.Range(1, sc.Script.Beats.Count);
                for (int i = 0; i < hear; i++) c.Listen();
                if (rng.Chance(0.5)) c.Ask(rng.Pick(Lexicon.AllSlots));
                c.Leave();
                var sheet = new AnswerSheet();
                foreach (var s in Lexicon.AllSlots)
                {
                    var m = (Mark)rng.Next(4);
                    sheet.Set(s, m, rng.Pick(Lexicon.For(s)).Id, rng.Pick(Lexicon.Actions).Id);
                }
                foreach (var p in sc.Key.Probes) sheet.Replies.Add((ProbeReply)rng.Next(3));
                var j = Evaluator.Evaluate(sc, c, sheet);
                foreach (var f in j.Findings)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(f.SpeakerQuote));
                    Assert.IsFalse(string.IsNullOrEmpty(f.PlayerQuote));
                    Assert.IsFalse(string.IsNullOrEmpty(f.Explanation));
                    if (!f.QuoteVerbatim)
                    {
                        // 引用がないのは、話し手が何も言っていない欄だけ
                        Assert.IsFalse(sc.Key.Unit(f.Slot).HasValue, "言葉があるのに引用がない: " + f.Slot);
                        continue;
                    }
                    foreach (var part in f.SpeakerQuote.Split(new[] { " …… " }, System.StringSplitOptions.None))
                        Assert.IsTrue(Spoken(sc, c, part), "引用が記録にない: " + part);
                    checkedFindings++;
                }
            }
            Assert.Greater(checkedFindings, 1000);
        }

        private static bool Spoken(Scenario sc, Conversation c, string text)
        {
            foreach (var b in sc.Script.Beats) if (b.Text == text) return true;
            foreach (var l in c.Transcript) if (l.FromSpeaker && l.Text == text) return true;
            return false;
        }

        [Test]
        public void 罰の規則表は種類と重要度で決まる()
        {
            Assert.AreEqual(Severity.A, Rules.SeverityOf(FindingType.Misheard, Importance.Critical, false));
            Assert.AreEqual(Severity.B, Rules.SeverityOf(FindingType.Misheard, Importance.Major, false));
            Assert.AreEqual(Severity.C, Rules.SeverityOf(FindingType.Misheard, Importance.Minor, false));
            Assert.AreEqual(Severity.A, Rules.SeverityOf(FindingType.UnawareOmission, Importance.Critical, false));
            Assert.AreEqual(Severity.A, Rules.SeverityOf(FindingType.Fabrication, Importance.Minor, true));
            Assert.AreEqual(Severity.None, Rules.SeverityOf(FindingType.DeclaredGap, Importance.Critical, false));
            Assert.AreEqual(Severity.None, Rules.SeverityOf(FindingType.UnresolvedVague, Importance.Critical, true));
            Assert.AreEqual(Severity.None, Rules.SeverityOf(FindingType.FalseGap, Importance.Critical, true));
            Assert.AreEqual(Severity.B, Rules.SeverityOf(FindingType.Underconfident, Importance.Critical, false));
            Assert.AreEqual(Severity.C, Rules.SeverityOf(FindingType.UnneededGuess, Importance.Critical, true));
            Assert.AreEqual(Severity.A, Rules.SeverityOf(FindingType.Distortion, Importance.Major, true));
            Assert.AreEqual(Severity.B, Rules.SeverityOf(FindingType.Distortion, Importance.Major, false));
        }

        [Test]
        public void 申し送りは印と中身がそろうまで確定できない()
        {
            var sheet = new AnswerSheet();
            Assert.IsNotNull(sheet.Validate());
            foreach (var s in Lexicon.AllSlots) sheet.Set(s, Mark.NotSaid);
            Assert.IsNull(sheet.Validate());
            sheet.Set(Slot.Day, Mark.Heard);
            Assert.IsNotNull(sheet.Validate());
            sheet.Set(Slot.Day, Mark.Heard, "d_1");
            Assert.IsNull(sheet.Validate());
            sheet.Set(Slot.Condition, Mark.Heard, "c_rain");
            Assert.IsNotNull(sheet.Validate());
        }
    }
}
