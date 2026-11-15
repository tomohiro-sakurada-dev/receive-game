using System;
using System.Collections.Generic;
using NUnit.Framework;
using Receive.Core;

namespace Receive.Tests
{
    /// <summary>会話の進行、聞き返し、道行き、記録。AI なしで最後まで動くこと。</summary>
    public class FlowTests
    {
        // ---- 会話 ----

        [Test]
        public void 話が終わった後も聴き続けると話し手が終わりを告げる()
        {
            var sc = ScenarioGenerator.Generate(5, 2);
            var c = new Conversation(sc);
            for (int i = 0; i < sc.Script.Beats.Count; i++) Assert.AreEqual(sc.Script.Beats[i].Text, c.Listen().Text);
            Assert.IsFalse(c.HeardEndNotice);
            Assert.AreEqual(c.Persona.Ended, c.Listen().Text);
            Assert.IsTrue(c.HeardEndNotice);
            c.Leave();
            Assert.IsFalse(c.CutOff);
            Assert.IsNull(c.Listen());
        }

        [Test]
        public void 途中で承ったと言うと切り上げになる()
        {
            var sc = ScenarioGenerator.Generate(5, 2);
            var c = new Conversation(sc);
            c.Listen(); c.Listen();
            c.Leave();
            Assert.IsTrue(c.CutOff);
        }

        // ---- 適切な聞き返し ----

        [Test]
        public void これから語られることを先に訊くと先走りになり間合いも減る()
        {
            var sc = TestKit.Find(2, s => s.Key.Unit(Slot.Day).Beat >= 2);
            var c = new Conversation(sc);
            c.Listen();
            int before = c.Patience;
            var line = c.Ask(Slot.Day);
            Assert.AreEqual(c.Persona.NotYet, line.Text);
            Assert.AreEqual(QuestionKind.Premature, c.Questions[0].Kind);
            Assert.AreEqual(before - 1, c.Patience);
            Assert.IsFalse(c.Revealed.Contains(Slot.Day));
        }

        [Test]
        public void 既に語られたことを訊くと話し手は言い直してくれる()
        {
            var sc = TestKit.Find(2, s => s.Key.Unit(Slot.Day).Decoy == null);
            var c = new Conversation(sc);
            while (c.BeatsHeard < sc.Script.Beats.Count) c.Listen();
            var line = c.Ask(Slot.Day);
            StringAssert.Contains(Lexicon.Text(sc.Key.Unit(Slot.Day).Value), line.Text);
            Assert.AreEqual(QuestionKind.Confirm, c.Questions[0].Kind);
        }

        [Test]
        public void あいまいな言葉を訊くと中身が明かされる()
        {
            var sc = TestKit.Find(3, s => TestKit.Has(s, TrapKind.Vague));
            InfoUnit u = null;
            foreach (var x in sc.Key.Units) if (x.State == KeyState.Vague) u = x;
            var c = new Conversation(sc);
            while (c.BeatsHeard < sc.Script.Beats.Count) c.Listen();
            var line = c.Ask(u.Slot);
            StringAssert.Contains(Lexicon.Text(u.Value), line.Text);
            Assert.IsTrue(c.Revealed.Contains(u.Slot));
            Assert.AreEqual(QuestionKind.Useful, c.Questions[0].Kind);
            Assert.AreEqual(KeyState.Stated, Evaluator.Effective(u, c));
        }

        [Test]
        public void 推測として語られたことは訊き直しても推測のまま()
        {
            var sc = TestKit.Find(3, s => TestKit.Has(s, TrapKind.Guess));
            InfoUnit u = null;
            foreach (var x in sc.Key.Units) if (x.State == KeyState.Guess) u = x;
            var c = new Conversation(sc);
            while (c.BeatsHeard < sc.Script.Beats.Count) c.Listen();
            var line = c.Ask(u.Slot);
            StringAssert.Contains("たぶん", line.Text);
            Assert.AreEqual(KeyState.Guess, Evaluator.Effective(u, c));
        }

        [Test]
        public void 決まっていないことは話の後に訊けば確かめになり話の途中なら先走りになる()
        {
            var sc = TestKit.Find(2, s => s.Script.Beats.Count >= 5);
            var slot = TestKit.FirstUnsaid(sc);
            var early = new Conversation(sc);
            early.Listen();
            early.Ask(slot);
            Assert.AreEqual(QuestionKind.Premature, early.Questions[0].Kind);

            var late = new Conversation(sc);
            while (late.BeatsHeard < sc.Script.Beats.Count) late.Listen();
            var line = late.Ask(slot);
            Assert.AreEqual(string.Format(late.Persona.NoneFmt, Lexicon.Label(slot)), line.Text);
            Assert.AreEqual(QuestionKind.Useful, late.Questions[0].Kind);
        }

        [Test]
        public void 間合いが尽きると答えてもらえない()
        {
            var sc = TestKit.Find(5, s => TestKit.Has(s, TrapKind.Vague));
            InfoUnit u = null;
            foreach (var x in sc.Key.Units) if (x.State == KeyState.Vague) u = x;
            var c = new Conversation(sc);
            while (c.BeatsHeard < sc.Script.Beats.Count) c.Listen();
            for (int i = 0; i < sc.Script.Patience; i++) c.AskRepeat();
            Assert.AreEqual(0, c.Patience);
            var line = c.Ask(u.Slot);
            Assert.AreEqual(c.Persona.NoPatience, line.Text);
            Assert.IsFalse(c.Revealed.Contains(u.Slot));
            Assert.AreEqual(QuestionKind.Refused, c.Questions[c.Questions.Count - 1].Kind);
        }

        [Test]
        public void 間合いの残りは数ではなく相手の様子で伝わる()
        {
            var sc = ScenarioGenerator.Generate(3, 1);
            var c = new Conversation(sc);
            c.Listen();
            Assert.IsNull(c.MoodHint);
            while (c.Patience > 1) c.AskRepeat();
            Assert.AreEqual(c.Persona.MoodLow, c.MoodHint);
        }

        // ---- 道行きと灯火 ----

        private static Judgement J(Outcome o, int b = 0)
        {
            return new Judgement { Outcome = o, LanternLoss = b, SealOk = true };
        }

        [Test]
        public void 成就を三つ重ねると道行きが成る()
        {
            var j = new Journey(Mode.Trial, 1);
            Assert.AreEqual(StepResult.Advanced, j.Apply(J(Outcome.Fulfilled)));
            Assert.AreEqual(StepResult.Advanced, j.Apply(J(Outcome.Fulfilled)));
            Assert.AreEqual(StepResult.RunCompleted, j.Apply(J(Outcome.Fulfilled)));
            Assert.IsTrue(j.Completed);
        }

        [Test]
        public void 歪みは灯火が残っていても即座に道行きを終わらせる()
        {
            var j = new Journey(Mode.Trial, 1);
            j.Apply(J(Outcome.Fulfilled));
            Assert.AreEqual(StepResult.RunFailed, j.Apply(J(Outcome.Distorted)));
            Assert.IsTrue(j.Over);
            Assert.IsFalse(j.Completed);
        }

        [Test]
        public void 未達は罰がなく進みもしない()
        {
            var j = new Journey(Mode.Trial, 1);
            Assert.AreEqual(StepResult.Retry, j.Apply(J(Outcome.Incomplete)));
            Assert.AreEqual(0, j.Step);
            Assert.AreEqual(Journey.LanternsMax, j.Lanterns);
        }

        [Test]
        public void 中程度のミスが重なり灯火が尽きると道行きは失敗する()
        {
            var j = new Journey(Mode.Trial, 1);
            Assert.AreEqual(StepResult.Advanced, j.Apply(J(Outcome.Fulfilled, 1)));
            Assert.AreEqual(2, j.Lanterns);
            Assert.AreEqual(StepResult.RunFailed, j.Apply(J(Outcome.Fulfilled, 2)));
            Assert.AreEqual(0, j.Lanterns);
        }

        [Test]
        public void 稽古には罰も進行もない()
        {
            var j = new Journey(Mode.Practice, 3);
            Assert.AreEqual(StepResult.PracticeDone, j.Apply(J(Outcome.Distorted, 2)));
            Assert.AreEqual(Journey.LanternsMax, j.Lanterns);
            Assert.IsFalse(j.Over);
        }

        // ---- AI なしでの動作 ----

        [Test]
        public void 通信もAIも使わずに道行きを最後まで遊べる()
        {
            var log = new TrainingLog();
            var session = new GameSession(log, 42);
            session.StartJourney(Mode.Trial, 1);
            var seen = new HashSet<string>();
            int guard = 0;
            while (!session.Journey.Over && guard++ < 20)
            {
                var sc = session.BeginTrial();
                Assert.IsTrue(seen.Add(sc.Fingerprint));
                while (session.Talk.BeatsHeard < sc.Script.Beats.Count) session.Talk.Listen();
                foreach (var u in sc.Key.Units) if (u.State == KeyState.Vague) session.Talk.Ask(u.Slot);
                session.Talk.Leave();
                ModelAnswer.Fill(sc, session.Talk, session.Sheet);
                var j = session.Finish(DateTime.UtcNow.Ticks);
                Assert.AreEqual(Outcome.Fulfilled, j.Outcome);
            }
            Assert.AreEqual(StepResult.RunCompleted, session.LastStep);
            Assert.AreEqual(Journey.Length, log.Trials.Count);
            Assert.AreEqual(2, log.UnlockedLevel);
            Assert.AreEqual(1, log.RunsCompleted);
        }

        [Test]
        public void 中核は通信やUnityの部品を参照していない()
        {
            foreach (var a in typeof(GameSession).Assembly.GetReferencedAssemblies())
            {
                string n = a.Name;
                Assert.IsFalse(n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"), n);
                Assert.IsFalse(n.Contains("Net.Http") || n.Contains("Net.Sockets") || n.Contains("Anthropic"), n);
            }
        }

        // ---- 訓練結果の記録 ----

        [Test]
        public void 聴き帳は技能ごとの機会と成功を残し保存しても変わらない()
        {
            var log = new TrainingLog();
            var session = new GameSession(log, 7);
            session.StartJourney(Mode.Practice, 3);
            var rng = new Rng(5);
            for (int i = 0; i < 25; i++)
            {
                var sc = session.BeginTrial();
                int hear = rng.Range(2, sc.Script.Beats.Count);
                for (int k = 0; k < hear; k++) session.Talk.Listen();
                session.Talk.Leave();
                ModelAnswer.Fill(sc, session.Talk, session.Sheet);
                if (rng.Chance(0.5)) session.Sheet.Set(TestKit.FirstUnsaid(sc), Mark.Heard, Lexicon.For(TestKit.FirstUnsaid(sc))[0].Id, Lexicon.Actions[0].Id);
                session.Finish(1000 + i);
            }
            Assert.AreEqual(25, log.Trials.Count);
            Assert.AreEqual(25, log.SeenFingerprints().Count);

            var back = TrainingLog.FromJson(log.ToJson());
            Assert.AreEqual(log.Trials.Count, back.Trials.Count);
            Assert.AreEqual(log.UnlockedLevel, back.UnlockedLevel);
            foreach (var s in SkillInfo.All)
            {
                Assert.AreEqual(log.SkillTally(s).Ok, back.SkillTally(s).Ok, SkillInfo.Name(s));
                Assert.AreEqual(log.SkillTally(s).Total, back.SkillTally(s).Total, SkillInfo.Name(s));
            }
            Assert.AreEqual(log.CountOutcome(Outcome.Distorted), back.CountOutcome(Outcome.Distorted));
            Assert.AreEqual(log.ToJson(), back.ToJson());
            Assert.Greater(log.CountOutcome(Outcome.Distorted), 0);
            Assert.IsTrue(log.ErrorCounts().ContainsKey(FindingType.Fabrication.ToString()));
            Assert.Less(log.SkillTally(Skill.NoFilling).Ok, log.SkillTally(Skill.NoFilling).Total);
            Assert.LessOrEqual(log.SkillTally(Skill.Accuracy, 5).Total, log.SkillTally(Skill.Accuracy).Total);
        }

        [Test]
        public void 壊れた記録を読んでも遊べなくならない()
        {
            Assert.AreEqual(0, TrainingLog.FromJson("{ これは壊れている").Trials.Count);
            Assert.AreEqual(0, TrainingLog.FromJson("").Trials.Count);
            Assert.AreEqual(0, TrainingLog.FromJson(null).Trials.Count);
            Assert.AreEqual(1, TrainingLog.FromJson("[1,2]").UnlockedLevel);
        }

        [Test]
        public void 無効になった試練は成績に数えない()
        {
            var log = new TrainingLog();
            var sc = ScenarioGenerator.Generate(3, 2);
            var c = TestKit.HearAll(sc);
            var sheet = TestKit.Model(sc, c);
            sc.Key.Unit(Slot.Day).Importance = Importance.Minor; // 封印を破る
            var j = Evaluator.Evaluate(sc, c, sheet);
            log.Record(sc, c, j, Mode.Trial, 1);
            Assert.AreEqual(Outcome.Void, j.Outcome);
            Assert.AreEqual(0, log.SkillTally(Skill.Accuracy).Total);
            Assert.AreEqual(1, log.SeenFingerprints().Count);
        }

        [Test]
        public void JSONは日本語や記号を含んでも往復できる()
        {
            var d = new Dictionary<string, object>
            {
                { "名前", "霧音の国「聴き継ぎ」\n二行目\\" }, { "数", 12345678901L }, { "真", true }, { "無", null },
                { "並び", new List<object> { 1L, "二", false } },
            };
            var back = (Dictionary<string, object>)MiniJson.Parse(MiniJson.Write(d));
            Assert.AreEqual(d["名前"], back["名前"]);
            Assert.AreEqual(12345678901L, back["数"]);
            Assert.AreEqual(true, back["真"]);
            Assert.IsNull(back["無"]);
            Assert.AreEqual(3, ((List<object>)back["並び"]).Count);
        }
    }
}
