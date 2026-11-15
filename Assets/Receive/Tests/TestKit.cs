using System;
using Receive.Core;

namespace Receive.Tests
{
    /// <summary>テストの共通部品。</summary>
    internal static class TestKit
    {
        /// <summary>条件に合うシナリオを、種を順に試して探す。</summary>
        public static Scenario Find(int level, Func<Scenario, bool> pred, long start = 1)
        {
            for (long seed = start; seed < start + 40000; seed++)
            {
                var sc = ScenarioGenerator.Generate(seed, level);
                if (pred(sc)) return sc;
            }
            throw new InvalidOperationException("条件に合うシナリオが見つからない");
        }

        public static bool Has(Scenario sc, TrapKind t) { return sc.Key.Traps.Contains(t); }

        public static bool NoVague(Scenario sc)
        {
            foreach (var u in sc.Key.Units) if (u.State == KeyState.Vague) return false;
            return true;
        }

        /// <summary>最後まで聞いて、話を終える。</summary>
        public static Conversation HearAll(Scenario sc)
        {
            var c = new Conversation(sc);
            while (c.BeatsHeard < sc.Script.Beats.Count) c.Listen();
            c.Leave();
            return c;
        }

        /// <summary>最後まで聞き、あいまいな欄はすべて聞き返す。</summary>
        public static Conversation HearAllAndAskVague(Scenario sc)
        {
            var c = new Conversation(sc);
            while (c.BeatsHeard < sc.Script.Beats.Count) c.Listen();
            foreach (var u in sc.Key.Units) if (u.State == KeyState.Vague) c.Ask(u.Slot);
            c.Leave();
            return c;
        }

        public static AnswerSheet Model(Scenario sc, Conversation c)
        {
            var sheet = new AnswerSheet();
            ModelAnswer.Fill(sc, c, sheet);
            return sheet;
        }

        /// <summary>申し送りの欄に対する判定（問い返しの判定は含まない）。</summary>
        public static Finding Of(Judgement j, Slot s)
        {
            foreach (var f in j.Findings) if (f.Slot == s && !f.FromProbe) return f;
            throw new InvalidOperationException("判定がない: " + s);
        }

        public static Finding ProbeFinding(Judgement j, Slot s)
        {
            foreach (var f in j.Findings) if (f.Slot == s && f.FromProbe) return f;
            return null;
        }

        public static int ProbeIndex(Scenario sc, Slot s)
        {
            for (int i = 0; i < sc.Key.Probes.Count; i++) if (sc.Key.Probes[i].Slot == s) return i;
            return -1;
        }

        public static string OtherValue(Slot s, string not)
        {
            foreach (var t in Lexicon.For(s)) if (t.Id != not) return t.Id;
            throw new InvalidOperationException();
        }

        public static Slot FirstUnsaid(Scenario sc)
        {
            foreach (var u in sc.Key.Units) if (!u.HasValue) return u.Slot;
            throw new InvalidOperationException("言われていない欄がない");
        }
    }
}
