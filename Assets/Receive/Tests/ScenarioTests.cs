using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Receive.Core;

namespace Receive.Tests
{
    /// <summary>シナリオ生成。重複しないこと、評価データと出題内容が食い違わないこと、必ず解けること。</summary>
    public class ScenarioTests
    {
        private const int SeedsPerLevel = 400;

        private static IEnumerable<Scenario> Many()
        {
            for (int level = 1; level <= ScenarioGenerator.MaxLevel; level++)
                for (long seed = 1; seed <= SeedsPerLevel; seed++)
                    yield return ScenarioGenerator.Generate(seed, level);
        }

        [Test]
        public void 同じ種からは同じシナリオができる()
        {
            var a = ScenarioGenerator.Generate(12345, 4);
            var b = ScenarioGenerator.Generate(12345, 4);
            Assert.AreEqual(a.Key.Canonical(), b.Key.Canonical());
            Assert.AreEqual(a.Seal, b.Seal);
            Assert.AreEqual(a.Script.Beats.Count, b.Script.Beats.Count);
            for (int i = 0; i < a.Script.Beats.Count; i++) Assert.AreEqual(a.Script.Beats[i].Text, b.Script.Beats[i].Text);
        }

        // ---- シナリオの重複 ----

        [Test]
        public void 出題済みのシナリオは二度と出ない()
        {
            var seeds = new Rng(2026);
            var seen = new HashSet<string>();
            var recent = new List<string>();
            for (int i = 0; i < 600; i++)
            {
                var sc = ScenarioGenerator.GenerateUnique(seeds, 1 + i % 5, seen, new HashSet<string>(recent));
                Assert.IsTrue(seen.Add(sc.Fingerprint), i + " 本目が重複した");
                Assert.IsFalse(recent.Contains(sc.ContentKey), i + " 本目が直近と近い内容だった");
                recent.Add(sc.ContentKey);
                if (recent.Count > 12) recent.RemoveAt(0);
            }
        }

        [Test]
        public void 種が違えば内容はほぼ必ず違う()
        {
            var fps = new HashSet<string>();
            int n = 0;
            foreach (var sc in Many()) { fps.Add(sc.Fingerprint); n++; }
            Assert.Greater(fps.Count, n * 97 / 100, "重複が多すぎる: " + fps.Count + " / " + n);
        }

        // ---- 評価基準と正解データの整合性 ----

        [Test]
        public void 評価データは台本の言葉と一致する()
        {
            foreach (var sc in Many())
            {
                string id = "種 " + sc.Seed + " 段 " + sc.Script.Level + ": ";
                var beats = sc.Script.Beats;
                Assert.AreEqual(Lexicon.AllSlots.Length, sc.Key.Units.Count, id);
                int unsaid = 0;
                foreach (var u in sc.Key.Units)
                {
                    Assert.AreEqual(sc.Key.Units.IndexOf(u), (int)u.Slot, id);
                    if (!u.HasValue)
                    {
                        unsaid++;
                        Assert.AreEqual(-1, u.Beat, id + u.Slot);
                        Assert.IsNull(u.Value, id + u.Slot);
                        continue;
                    }
                    Assert.IsTrue(u.Beat >= 1 && u.Beat < beats.Count, id + u.Slot + " の区切りが範囲外");
                    string said = beats[u.Beat].Text;
                    if (u.State == KeyState.Vague)
                    {
                        StringAssert.Contains(u.VagueWord == "いつもと同じ数" ? "いつもと同じ" : u.VagueWord, said, id + u.Slot);
                        Assert.IsFalse(said.Contains(Lexicon.Text(u.Value)), id + "あいまいな欄の中身が台本に出ている");
                    }
                    else if (u.Slot == Slot.Condition)
                    {
                        StringAssert.Contains(Lexicon.Text(u.Value), said, id);
                        StringAssert.Contains(Lexicon.Find(u.Value2).Te, said, id);
                    }
                    else if (u.Slot != Slot.Task)
                    {
                        StringAssert.Contains(Lexicon.Text(u.Value), said, id + u.Slot);
                    }
                    if (u.State == KeyState.Guess) StringAssert.Contains("たぶん", said, id);
                    if (u.State == KeyState.Hearsay) StringAssert.Contains("人づて", said, id);
                    if (u.Decoy != null)
                    {
                        Assert.AreNotEqual(u.Value, u.Decoy, id);
                        Assert.IsTrue(u.DecoyBeat >= 1 && u.DecoyBeat <= u.Beat, id + "言い直しが言い違いより前にある");
                        StringAssert.Contains(Lexicon.Text(u.Decoy), beats[u.DecoyBeat].Text, id);
                        Assert.IsTrue(u.Late, id);
                    }
                }
                Assert.GreaterOrEqual(unsaid, 1, id + "言われていない欄が1つもない");

                // 重要度は出題前に決まっている
                Assert.AreEqual(Importance.Critical, sc.Key.Unit(Slot.Person).Importance, id);
                Assert.AreEqual(Importance.Critical, sc.Key.Unit(Slot.Task).Importance, id);
                Assert.AreEqual(Importance.Critical, sc.Key.Unit(Slot.Day).Importance, id);
                if (sc.Key.Unit(Slot.Condition).HasValue) Assert.AreEqual(Importance.Critical, sc.Key.Unit(Slot.Condition).Importance, id);

                // 言われていない欄について、台本は何も言っていない
                foreach (var b in beats)
                {
                    if (!sc.Key.Unit(Slot.Time).HasValue) Assert.IsFalse(b.Text.Contains("刻は") || b.Text.Contains("頃合いは"), id + "刻");
                    if (!sc.Key.Unit(Slot.Place).HasValue) Assert.IsFalse(b.Text.Contains("場所は") || b.Text.Contains("落ち合う先は"), id + "場所");
                    if (!sc.Key.Unit(Slot.Quantity).HasValue) Assert.IsFalse(b.Text.Contains("数は"), id + "数");
                    if (!sc.Key.Unit(Slot.Condition).HasValue) Assert.IsFalse(b.Text.Contains("ただし"), id + "ただし書き");
                }
                if (!sc.Key.Unit(Slot.Quantity).HasValue)
                    foreach (var q in Lexicon.Quantities)
                        Assert.IsFalse(beats[sc.Key.Unit(Slot.Person).Beat].Text.Contains(q.Text), id + "頼みの文に数が出ている");

                // 区切りの並び
                Assert.AreEqual(BeatKind.Greeting, beats[0].Kind, id);
                int closings = 0, closingAt = -1;
                foreach (var b in beats) if (b.Kind == BeatKind.Closing) { closings++; closingAt = b.Index; }
                Assert.AreEqual(1, closings, id);
                foreach (var b in beats)
                    Assert.AreEqual(b.Kind == BeatKind.Afterthought, b.Index > closingAt, id + "結びの後は追い声だけ");
                Assert.Greater(sc.Script.Patience, 0, id);

                // 問い返しは出題前に3つ固定されている
                Assert.AreEqual(3, sc.Key.Probes.Count, id);
                var slots = new HashSet<Slot>();
                foreach (var p in sc.Key.Probes)
                {
                    Assert.IsTrue(slots.Add(p.Slot), id + "問い返しの欄が重なっている");
                    Assert.IsNotNull(Lexicon.Find(p.Value), id);
                    Assert.IsFalse(string.IsNullOrEmpty(p.Text), id);
                }

                // 封印は評価データの内容と一致している
                Assert.AreEqual(Seal.Compute(sc.Key.Canonical()), sc.Seal, id);
            }
        }

        [Test]
        public void 封印は評価データが1つでも変わると変わる()
        {
            var sc = ScenarioGenerator.Generate(9, 3);
            string before = Seal.Compute(sc.Key.Canonical());
            sc.Key.Unit(Slot.Person).Importance = Importance.Minor;
            Assert.AreNotEqual(before, Seal.Compute(sc.Key.Canonical()));
        }

        [Test]
        public void 出題内容は評価データを持たない()
        {
            foreach (var f in typeof(ScenarioScript).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(AnswerKey), f.FieldType);
                Assert.AreNotEqual(typeof(InfoUnit), f.FieldType);
                Assert.AreNotEqual(typeof(List<InfoUnit>), f.FieldType);
                Assert.AreNotEqual(typeof(List<Probe>), f.FieldType);
            }
            foreach (var f in typeof(Beat).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                Assert.IsTrue(f.FieldType == typeof(int) || f.FieldType == typeof(string) || f.FieldType == typeof(BeatKind));
        }

        // ---- 必ず解ける（あいまいな出題で失敗させない） ----

        [Test]
        public void どのシナリオも最後まで聞いて確かめれば成就できる()
        {
            foreach (var sc in Many())
            {
                var c = TestKit.HearAllAndAskVague(sc);
                var j = Evaluator.Evaluate(sc, c, TestKit.Model(sc, c));
                string id = "種 " + sc.Seed + " 段 " + sc.Script.Level;
                Assert.AreEqual(Outcome.Fulfilled, j.Outcome, id);
                Assert.AreEqual(Rank.Meikyo, j.Rank, id);
                Assert.AreEqual(0, j.LanternLoss, id);
                Assert.Greater(c.Patience + 1, 0, id);
            }
        }

        [Test]
        public void あいまいな欄を確かめるだけの間合いは必ずある()
        {
            foreach (var sc in Many())
            {
                int vague = 0;
                foreach (var u in sc.Key.Units) if (u.State == KeyState.Vague) vague++;
                Assert.LessOrEqual(vague, sc.Script.Patience);
            }
        }

        // ---- 出題の偏り ----

        [Test]
        public void 罠のない素直な話が一定の割合で混ざる()
        {
            for (int level = 2; level <= ScenarioGenerator.MaxLevel; level++)
            {
                int plain = 0;
                for (long seed = 1; seed <= 1000; seed++)
                    if (ScenarioGenerator.Generate(seed, level).Key.Traps.Count == 0) plain++;
                Assert.IsTrue(plain >= 150 && plain <= 450, "段 " + level + " の罠なしの数: " + plain);
            }
        }

        [Test]
        public void 段が上がると情報と罠が増える()
        {
            double[] info = new double[6], traps = new double[6];
            foreach (var sc in Many())
            {
                int n = 0;
                foreach (var u in sc.Key.Units) if (u.HasValue) n++;
                info[sc.Script.Level] += n;
                traps[sc.Script.Level] += sc.Key.Traps.Count;
            }
            Assert.Less(info[1], info[3]);
            Assert.Less(info[3], info[5] + SeedsPerLevel * 0.3);
            Assert.Less(traps[1], traps[3]);
            Assert.Less(traps[3], traps[5]);
        }

        [Test]
        public void すべての罠の型と人格が実際に出題される()
        {
            var kinds = new HashSet<TrapKind>();
            var personas = new HashSet<string>();
            var tasks = new HashSet<string>();
            foreach (var sc in Many())
            {
                foreach (var t in sc.Key.Traps) kinds.Add(t);
                personas.Add(sc.Script.PersonaId);
                tasks.Add(sc.Key.Unit(Slot.Task).Value);
            }
            Assert.AreEqual(7, kinds.Count);
            Assert.AreEqual(Persona.All.Count, personas.Count);
            Assert.AreEqual(Lexicon.Tasks.Count, tasks.Count);
        }

        [Test]
        public void 語彙に重複や欠けがない()
        {
            var ids = new HashSet<string>();
            foreach (var s in Lexicon.AllSlots)
            {
                var texts = new HashSet<string>();
                foreach (var t in Lexicon.For(s))
                {
                    Assert.IsTrue(ids.Add(t.Id), "識別子の重複: " + t.Id);
                    Assert.IsTrue(texts.Add(t.Text), "表記の重複: " + t.Text);
                    Assert.AreSame(t, Lexicon.Find(t.Id));
                }
            }
            foreach (var a in Lexicon.Actions)
            {
                Assert.IsTrue(ids.Add(a.Id));
                Assert.IsFalse(string.IsNullOrEmpty(a.Te));
            }
        }
    }
}
