using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Receive.Core
{
    public static class Seal
    {
        public static string Compute(string canonical)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    /// <summary>
    /// 端末内で完結するシナリオ生成。AI も通信も使わない。
    /// 人物・状況・固有の値・条件・罠を乱択で組み合わせ、正解と評価基準を出題前に確定して封印する。
    /// </summary>
    public static class ScenarioGenerator
    {
        public const int MaxLevel = 5;

        private sealed class Sentence
        {
            public string Text;
            public List<Slot> Slots = new List<Slot>();
            public List<Slot> DecoySlots = new List<Slot>();
            public bool IsCore, IsChatter;
        }

        public static Scenario Generate(long seed, int level)
        {
            level = Math.Max(1, Math.Min(MaxLevel, level));
            var rng = new Rng(seed * 31 + level);
            var persona = rng.Pick(Persona.All);
            var key = new AnswerKey();
            foreach (var s in Lexicon.AllSlots)
                key.Units.Add(new InfoUnit { Slot = s, State = KeyState.NotStated, Importance = Importance.Major });

            // ---- 何を頼むか ----
            string task = rng.Pick(Lexicon.Tasks).Id;
            Set(key, Slot.Person, rng.Pick(Lexicon.Persons).Id, Importance.Critical);
            Set(key, Slot.Task, task, Importance.Critical);
            bool thing = false;
            if (task != Lexicon.TaskFetch)
            {
                string group = task == Lexicon.TaskTell ? "matter" : "thing";
                var pool = new List<Term>();
                foreach (var t in Lexicon.Items) if (t.Group == group) pool.Add(t);
                Set(key, Slot.Item, rng.Pick(pool).Id, Importance.Critical);
                thing = group == "thing";
            }
            Set(key, Slot.Day, rng.Pick(Lexicon.Days).Id, Importance.Critical);

            double pPlace = level == 1 ? 0.3 : level == 2 ? 0.6 : 0.8;
            double pTime = level == 1 ? 0.2 : level == 2 ? 0.5 : 0.7;
            double pQty = level == 1 ? 0.2 : level == 2 ? 0.4 : 0.6;
            double pCond = level == 1 ? 0.35 : level == 2 ? 0.5 : 0.7;
            bool place = rng.Chance(pPlace), time = rng.Chance(pTime), qty = thing && rng.Chance(pQty), cond = rng.Chance(pCond);
            // 言われていない欄を必ず1つは残す（補わない力を毎回試すため）
            if (place && time && cond && (qty || !thing))
            {
                if (rng.Chance(0.5)) place = false; else time = false;
            }
            if (place) Set(key, Slot.Place, rng.Pick(Lexicon.Places).Id, Importance.Major);
            if (time) Set(key, Slot.Time, rng.Pick(Lexicon.Times).Id, Importance.Major);
            if (qty) Set(key, Slot.Quantity, rng.Pick(Lexicon.Quantities).Id, Importance.Major);
            if (cond)
            {
                var u = key.Unit(Slot.Condition);
                u.State = KeyState.Stated; u.Importance = Importance.Critical;
                u.Value = rng.Pick(Lexicon.Triggers).Id; u.Value2 = rng.Pick(Lexicon.Actions).Id;
            }

            // ---- 罠 ----
            // 罠のない素直な話を約4分の1混ぜる。毎回罠があると、受信ではなく罠探しになる。
            int trapCount;
            if (rng.Chance(0.25)) trapCount = 0;
            else if (level == 1) trapCount = rng.Range(0, 1);
            else if (level == 2) trapCount = 1;
            else if (level == 3) trapCount = rng.Range(1, 2);
            else if (level == 4) trapCount = rng.Range(2, 3);
            else trapCount = 3;

            var allowed = new List<TrapKind> { TrapKind.TailCondition, TrapKind.Distractor };
            if (level >= 2) { allowed.Add(TrapKind.Guess); allowed.Add(TrapKind.Afterthought); }
            if (level >= 3) { allowed.Add(TrapKind.Correction); allowed.Add(TrapKind.Hearsay); allowed.Add(TrapKind.Vague); }
            rng.Shuffle(allowed);

            Slot? afterSlot = null;
            bool tailCond = false, corrLater = false;
            foreach (var trap in allowed)
            {
                if (key.Traps.Count >= trapCount) break;
                bool ok = false;
                switch (trap)
                {
                    case TrapKind.TailCondition:
                        if (key.Unit(Slot.Condition).HasValue && afterSlot != Slot.Condition) { tailCond = true; ok = true; }
                        break;
                    case TrapKind.Afterthought:
                    {
                        var c = Candidates(key, new[] { Slot.Condition, Slot.Time, Slot.Place, Slot.Quantity }, true);
                        if (tailCond) c.Remove(Slot.Condition);
                        if (c.Count > 0) { afterSlot = rng.Pick(c); ok = true; }
                        break;
                    }
                    case TrapKind.Correction:
                    {
                        var c = Candidates(key, new[] { Slot.Day, Slot.Time, Slot.Quantity, Slot.Place }, false);
                        if (afterSlot.HasValue) c.Remove(afterSlot.Value);
                        if (c.Count > 0)
                        {
                            var u = key.Unit(rng.Pick(c));
                            u.Decoy = Other(rng, Lexicon.For(u.Slot), u.Value);
                            corrLater = level >= 4 && rng.Chance(0.6);
                            ok = true;
                        }
                        break;
                    }
                    case TrapKind.Guess:
                    case TrapKind.Hearsay:
                    {
                        var c = Candidates(key, new[] { Slot.Place, Slot.Time, Slot.Quantity }, false);
                        if (c.Count > 0)
                        {
                            var u = key.Unit(rng.Pick(c));
                            u.State = trap == TrapKind.Guess ? KeyState.Guess : KeyState.Hearsay;
                            u.Importance = Importance.Major;
                            ok = true;
                        }
                        break;
                    }
                    case TrapKind.Vague:
                    {
                        var c = Candidates(key, new[] { Slot.Place, Slot.Time, Slot.Quantity }, false);
                        if (afterSlot.HasValue) c.Remove(afterSlot.Value);
                        if (c.Count > 0)
                        {
                            var u = key.Unit(rng.Pick(c));
                            u.State = KeyState.Vague;
                            u.Importance = Importance.Critical; // 確かめなければ務めを果たせない
                            u.VagueWord = Phrases.VagueWord(u.Slot);
                            ok = true;
                        }
                        break;
                    }
                    case TrapKind.Distractor:
                    {
                        var c = new List<Slot>();
                        foreach (var s in new[] { Slot.Time, Slot.Place, Slot.Quantity })
                            if (!key.Unit(s).HasValue) c.Add(s);
                        if (c.Count > 0)
                        {
                            var u = key.Unit(rng.Pick(c));
                            u.DistractorValue = rng.Pick(Lexicon.For(u.Slot)).Id;
                            ok = true;
                        }
                        break;
                    }
                }
                if (ok) key.Traps.Add(trap);
            }

            // ---- 文を作る ----
            var person = key.Unit(Slot.Person); var item = key.Unit(Slot.Item);
            var qtyU = key.Unit(Slot.Quantity); var dayU = key.Unit(Slot.Day); var timeU = key.Unit(Slot.Time);
            bool qtyInline = qtyU.State == KeyState.Stated && qtyU.Decoy == null && afterSlot != Slot.Quantity && rng.Chance(0.6);

            var core = new Sentence { IsCore = true };
            core.Text = (rng.Chance(0.5) ? persona.LeadIn : "") + Phrases.Core(persona, task, person.Value, item.Value, qtyInline ? qtyU.Value : null);
            core.Slots.Add(Slot.Person); core.Slots.Add(Slot.Task);
            if (item.HasValue) core.Slots.Add(Slot.Item);
            if (qtyInline) core.Slots.Add(Slot.Quantity);

            var middle = new List<Sentence>();
            Sentence afterSentence = null, condSentence = null;
            var corrections = new List<Sentence>();
            bool mergeDayTime = timeU.State == KeyState.Stated && timeU.Decoy == null && dayU.Decoy == null
                                && afterSlot != Slot.Time && rng.Chance(0.4);

            foreach (var s in new[] { Slot.Day, Slot.Time, Slot.Place, Slot.Quantity, Slot.Condition })
            {
                var u = key.Unit(s);
                if (!u.HasValue) continue;
                if (s == Slot.Quantity && qtyInline) continue;
                if (s == Slot.Time && mergeDayTime) continue;
                var sen = new Sentence();
                int variant = rng.Next(2);
                if (s == Slot.Condition) sen.Text = Phrases.Condition(persona, u.Value, u.Value2);
                else if (s == Slot.Day && mergeDayTime) { sen.Text = Phrases.DayAndTime(persona, u.Value, timeU.Value); sen.Slots.Add(Slot.Time); }
                else if (u.State == KeyState.Guess) sen.Text = Phrases.Guess(persona, s, u.Value);
                else if (u.State == KeyState.Hearsay) sen.Text = Phrases.Hearsay(persona, s, u.Value);
                else if (u.State == KeyState.Vague) sen.Text = Phrases.Vague(persona, s);
                else if (u.Decoy != null)
                {
                    sen.Text = Phrases.Plain(persona, s, u.Decoy, variant);
                    var fix = new Sentence { Text = Phrases.Correction(persona, s, u.Value, u.Decoy) };
                    fix.Slots.Add(s);
                    if (corrLater) corrections.Add(fix);
                    else { sen.Text += fix.Text; sen.Slots.Add(s); }
                    sen.DecoySlots.Add(s);
                }
                else sen.Text = Phrases.Plain(persona, s, u.Value, variant);

                if (u.Decoy == null) sen.Slots.Add(s);

                if (afterSlot == s) afterSentence = sen;
                else if (s == Slot.Condition) condSentence = sen;
                else middle.Add(sen);
            }
            rng.Shuffle(middle);

            var seq = new List<Sentence>();
            bool coreFirst = level == 1 || rng.Chance(0.6);
            if (coreFirst) { seq.Add(core); seq.AddRange(middle); }
            else { seq.AddRange(middle); seq.Insert(rng.Next(middle.Count + 1), core); }

            // 後からの訂正は、言い違えた文の1〜2つ後に置く
            foreach (var fix in corrections)
            {
                int decoyAt = seq.FindIndex(x => x.DecoySlots.Contains(fix.Slots[0]));
                int at = Math.Min(seq.Count, decoyAt + 1 + rng.Range(1, 2));
                seq.Insert(at, fix);
            }
            if (condSentence != null)
            {
                int coreAt = seq.IndexOf(core);
                if (tailCond) seq.Add(condSentence);
                else seq.Insert(rng.Range(coreAt + 1, seq.Count), condSentence);
            }
            // 無関係な話。言われていない欄の語が、別の話として出てくる
            foreach (var u in key.Units)
            {
                if (u.DistractorValue == null) continue;
                var sen = new Sentence { IsChatter = true, Text = Phrases.Distractor(persona, u.Slot, u.DistractorValue) };
                seq.Insert(rng.Range(1, seq.Count), sen);
            }
            double pTalk = level == 1 ? 0.2 : level == 2 ? 0.4 : 0.5;
            if (rng.Chance(pTalk))
                seq.Insert(rng.Range(0, seq.Count), new Sentence { IsChatter = true, Text = rng.Pick(persona.Smalltalk) });

            // ---- 区切りに並べる ----
            var script = new ScenarioScript
            {
                Level = level,
                PersonaId = persona.Id,
                SpeakerName = rng.Pick(persona.Speakers),
                ReceiverName = rng.Pick(Persona.Receivers),
                Scene = Persona.Scenes[level - 1],
                Patience = level == 1 ? 4 : level <= 3 ? 3 : 2,
            };
            AddBeat(script, BeatKind.Greeting, rng.Pick(persona.Greetings));
            int lastInfoBeat = -1;
            foreach (var sen in seq)
            {
                int idx = AddBeat(script, sen.IsChatter ? BeatKind.Chatter : BeatKind.Info, sen.Text);
                foreach (var s in sen.Slots) key.Unit(s).Beat = idx;
                foreach (var s in sen.DecoySlots) key.Unit(s).DecoyBeat = idx;
                if (!sen.IsChatter) lastInfoBeat = idx;
            }
            AddBeat(script, BeatKind.Closing, rng.Pick(persona.Closings));
            if (afterSentence != null)
            {
                int idx = AddBeat(script, BeatKind.Afterthought, persona.AfterPrefix + afterSentence.Text);
                foreach (var s in afterSentence.Slots) key.Unit(s).Beat = idx;
            }
            foreach (var u in key.Units)
            {
                if (!u.HasValue) continue;
                bool isAfter = afterSlot == u.Slot;
                bool isTail = u.Slot == Slot.Condition && u.Beat == lastInfoBeat && lastInfoBeat >= 3;
                u.Late = u.Decoy != null || isAfter || isTail;
            }

            BuildProbes(rng, key);

            var sc = new Scenario { Seed = seed, Script = script, Key = key };
            sc.ContentKey = task + "|" + person.Value + "|" + item.Value;
            var fp = new StringBuilder(persona.Id);
            foreach (var u in key.Units)
                fp.Append('|').Append((int)u.State).Append(':').Append(u.Value).Append(':').Append(u.Value2).Append(':').Append(u.Decoy);
            foreach (var t in key.Traps) fp.Append('|').Append(t);
            sc.Fingerprint = Seal.Compute(fp.ToString()).Substring(0, 16);
            sc.Seal = Seal.Compute(key.Canonical());
            sc.SealedAtTicks = DateTime.UtcNow.Ticks;
            return sc;
        }

        /// <summary>まだ出していないシナリオを作る。直近と近い内容（相手・用向き・品が同じ）も避ける。</summary>
        public static Scenario GenerateUnique(Rng seeds, int level, ICollection<string> seenFingerprints, ICollection<string> recentContentKeys)
        {
            Scenario last = null;
            for (int i = 0; i < 400; i++)
            {
                long seed = ((long)seeds.Next(int.MaxValue) << 20) ^ seeds.Next(int.MaxValue);
                last = Generate(seed, level);
                if (seenFingerprints != null && seenFingerprints.Contains(last.Fingerprint)) continue;
                if (recentContentKeys != null && recentContentKeys.Contains(last.ContentKey)) continue;
                return last;
            }
            return last;
        }

        private static void Set(AnswerKey key, Slot s, string value, Importance imp)
        {
            var u = key.Unit(s);
            u.State = KeyState.Stated; u.Value = value; u.Importance = imp;
        }

        private static List<Slot> Candidates(AnswerKey key, Slot[] slots, bool allowNonPlain)
        {
            var r = new List<Slot>();
            foreach (var s in slots)
            {
                var u = key.Unit(s);
                if (!u.HasValue) continue;
                if (u.Decoy != null) continue;
                if (!allowNonPlain && u.State != KeyState.Stated) continue;
                if (allowNonPlain && u.State == KeyState.Vague) continue;
                r.Add(s);
            }
            return r;
        }

        private static string Other(Rng rng, IReadOnlyList<Term> list, string not)
        {
            for (int i = 0; i < 50; i++)
            {
                var t = rng.Pick(list);
                if (t.Id != not) return t.Id;
            }
            return list[0].Id == not ? list[1].Id : list[0].Id;
        }

        private static string OtherItem(Rng rng, string not)
        {
            string group = Lexicon.Find(not).Group;
            var pool = new List<Term>();
            foreach (var t in Lexicon.Items) if (t.Group == group && t.Id != not) pool.Add(t);
            return rng.Pick(pool).Id;
        }

        private static int AddBeat(ScenarioScript s, BeatKind kind, string text)
        {
            s.Beats.Add(new Beat { Index = s.Beats.Count, Kind = kind, Text = text });
            return s.Beats.Count - 1;
        }

        /// <summary>受け手の問い返しを3つ決める。出題前に固定し、プレイヤーの申し送りでは変わらない。</summary>
        private static void BuildProbes(Rng rng, AnswerKey key)
        {
            var used = new HashSet<Slot>();

            // 1) 罠に関わる欄。早合点しやすい値を持ち出す
            var trapUnits = new List<InfoUnit>();
            foreach (var u in key.Units)
                if (u.Decoy != null || u.State == KeyState.Guess || u.State == KeyState.Hearsay || u.State == KeyState.Vague
                    || (u.Slot == Slot.Condition && u.HasValue))
                    trapUnits.Add(u);
            if (trapUnits.Count > 0)
            {
                var u = rng.Pick(trapUnits);
                string v = u.Value, v2 = u.Value2;
                if (u.Decoy != null) v = u.Decoy;
                else if (u.Slot == Slot.Condition && rng.Chance(0.5)) v2 = Other(rng, Lexicon.Actions, u.Value2);
                AddProbe(key, used, u.Slot, v, v2);
            }

            // 2) 言われていない欄。無関係な話に出た語があれば、それを持ち出す
            var unsaid = new List<InfoUnit>();
            foreach (var u in key.Units) if (!u.HasValue) unsaid.Add(u);
            if (unsaid.Count > 0)
            {
                InfoUnit u = null;
                foreach (var c in unsaid) if (c.DistractorValue != null) u = c;
                if (u == null) u = rng.Pick(unsaid);
                string v = u.DistractorValue ?? rng.Pick(Lexicon.For(u.Slot)).Id;
                string v2 = u.Slot == Slot.Condition ? rng.Pick(Lexicon.Actions).Id : null;
                AddProbe(key, used, u.Slot, v, v2);
            }

            // 3) 素直に言われた欄。半分はそのまま、半分は値をずらす
            var plain = new List<InfoUnit>();
            foreach (var s in new[] { Slot.Person, Slot.Day, Slot.Item })
            {
                var u = key.Unit(s);
                if (u.State == KeyState.Stated && u.Decoy == null && !used.Contains(s)) plain.Add(u);
            }
            if (plain.Count > 0)
            {
                var u = rng.Pick(plain);
                string v = u.Value;
                if (rng.Chance(0.5)) v = u.Slot == Slot.Item ? OtherItem(rng, u.Value) : Other(rng, Lexicon.For(u.Slot), u.Value);
                AddProbe(key, used, u.Slot, v, null);
            }
            // 問い返しは3つにそろえる。足りなければ、残りの欄から足す
            var rest = new List<InfoUnit>();
            foreach (var u in key.Units) if (!used.Contains(u.Slot) && u.Slot != Slot.Task) rest.Add(u);
            rng.Shuffle(rest);
            foreach (var u in rest)
            {
                if (key.Probes.Count >= 3) break;
                string v, v2 = null;
                if (!u.HasValue)
                {
                    v = rng.Pick(Lexicon.For(u.Slot)).Id;
                    if (u.Slot == Slot.Condition) v2 = rng.Pick(Lexicon.Actions).Id;
                }
                else
                {
                    v = u.Value; v2 = u.Value2;
                    if (rng.Chance(0.5))
                    {
                        if (u.Slot == Slot.Condition) v2 = Other(rng, Lexicon.Actions, u.Value2);
                        else v = u.Slot == Slot.Item ? OtherItem(rng, u.Value) : Other(rng, Lexicon.For(u.Slot), u.Value);
                    }
                }
                AddProbe(key, used, u.Slot, v, v2);
            }
            rng.Shuffle(key.Probes);
        }

        private static void AddProbe(AnswerKey key, HashSet<Slot> used, Slot s, string v, string v2)
        {
            if (!used.Add(s)) return;
            key.Probes.Add(new Probe { Slot = s, Value = v, Value2 = v2, Text = Phrases.ProbeText(s, v, v2) });
        }
    }
}
