using System;
using System.Collections.Generic;

namespace Receive.Core
{
    public enum Mode { Practice, Trial }

    public enum StepResult
    {
        Advanced,      // 次の試練へ
        Retry,         // 未達。罰なし。別の話でやり直す
        RunFailed,     // 歪み、または灯火が尽きた。結び灯籠へ戻る
        RunCompleted,  // 道行きを終えた
        PracticeDone,  // 稽古。罰も進行もない
        Void           // 無効。何も変えない
    }

    /// <summary>道行き。結び灯籠から次の結び灯籠までの、試練の連なり。</summary>
    public sealed class Journey
    {
        public const int LanternsMax = 3;
        public const int Length = 3;

        public Mode Mode { get; private set; }
        public int Level { get; private set; }
        public int Lanterns { get; private set; }
        public int Step { get; private set; }
        public bool Over { get; private set; }
        public bool Completed { get; private set; }

        public Journey(Mode mode, int level)
        {
            Mode = mode;
            Level = Math.Max(1, Math.Min(ScenarioGenerator.MaxLevel, level));
            Lanterns = LanternsMax;
        }

        public StepResult Apply(Judgement j)
        {
            if (j.Outcome == Outcome.Void) return StepResult.Void;
            if (Mode == Mode.Practice) return StepResult.PracticeDone;
            if (j.Outcome == Outcome.Distorted) { Over = true; return StepResult.RunFailed; }
            Lanterns = Math.Max(0, Lanterns - j.LanternLoss);
            if (Lanterns == 0) { Over = true; return StepResult.RunFailed; }
            if (j.Outcome == Outcome.Incomplete) return StepResult.Retry;
            Step++;
            if (Step >= Length) { Over = true; Completed = true; return StepResult.RunCompleted; }
            return StepResult.Advanced;
        }
    }

    public sealed class TrialRecord
    {
        public long Time;
        public long Seed;
        public string Fingerprint = "", ContentKey = "";
        public int Level;
        public string Mode = "", Outcome = "", Rank = "";
        public bool CutOff;
        public int A, B, C;
        public int Questions;
        public int[] SkillOk = new int[SkillInfo.Count];
        public int[] SkillTotal = new int[SkillInfo.Count];
        public List<string> Findings = new List<string>();  // 種類|欄|重さ|原因
        public List<string> Traps = new List<string>();
    }

    /// <summary>聴き帳。訓練の記録。技能ごとの機会と成功を、試練ごとに残す。</summary>
    public sealed class TrainingLog
    {
        public const int Version = 1;
        public readonly List<TrialRecord> Trials = new List<TrialRecord>();
        public int UnlockedLevel = 1;
        public int RunsCompleted, RunsFailed;

        public HashSet<string> SeenFingerprints()
        {
            var s = new HashSet<string>();
            foreach (var t in Trials) s.Add(t.Fingerprint);
            return s;
        }

        public List<string> RecentContentKeys(int n)
        {
            var r = new List<string>();
            for (int i = Trials.Count - 1; i >= 0 && r.Count < n; i--) r.Add(Trials[i].ContentKey);
            return r;
        }

        /// <summary>試練の結果を残す。無効になった試練は成績に数えない（出題済みの印だけ残す）。</summary>
        public TrialRecord Record(Scenario sc, Conversation conv, Judgement j, Mode mode, long timeTicks)
        {
            var r = new TrialRecord
            {
                Time = timeTicks, Seed = sc.Seed, Fingerprint = sc.Fingerprint, ContentKey = sc.ContentKey,
                Level = sc.Script.Level, Mode = mode.ToString(), Outcome = j.Outcome.ToString(), Rank = j.Rank.ToString(),
                CutOff = j.CutOff, A = j.Count(Severity.A), B = j.Count(Severity.B), C = j.Count(Severity.C),
                Questions = conv.Questions.Count,
            };
            for (int i = 0; i < SkillInfo.Count; i++) { r.SkillOk[i] = j.Skills[i].Ok; r.SkillTotal[i] = j.Skills[i].Total; }
            foreach (var f in j.Findings)
                if (f.Type != FindingType.Correct && f.Type != FindingType.CorrectlyUnknown)
                    r.Findings.Add(f.Type + "|" + f.Slot + "|" + f.Severity + "|" + f.Cause);
            foreach (var t in sc.Key.Traps) r.Traps.Add(t.ToString());
            Trials.Add(r);
            return r;
        }

        public void ApplyRun(StepResult result, int level)
        {
            if (result == StepResult.RunCompleted)
            {
                RunsCompleted++;
                if (level >= UnlockedLevel && UnlockedLevel < ScenarioGenerator.MaxLevel) UnlockedLevel = level + 1;
            }
            else if (result == StepResult.RunFailed) RunsFailed++;
        }

        private static bool Counts(TrialRecord t) { return t.Outcome != Outcome.Void.ToString(); }

        /// <summary>技能ごとの通算。lastN を指定すると直近の試練だけを見る。</summary>
        public Tally SkillTally(Skill s, int lastN = 0)
        {
            var tally = new Tally();
            int seen = 0;
            for (int i = Trials.Count - 1; i >= 0; i--)
            {
                if (!Counts(Trials[i])) continue;
                if (lastN > 0 && seen >= lastN) break;
                seen++;
                tally.Ok += Trials[i].SkillOk[(int)s];
                tally.Total += Trials[i].SkillTotal[(int)s];
            }
            return tally;
        }

        public Dictionary<string, int> ErrorCounts()
        {
            var d = new Dictionary<string, int>();
            foreach (var t in Trials)
            {
                if (!Counts(t)) continue;
                foreach (var f in t.Findings)
                {
                    string type = f.Split('|')[0];
                    int n; d.TryGetValue(type, out n); d[type] = n + 1;
                }
            }
            return d;
        }

        public int CountOutcome(Outcome o)
        {
            int n = 0;
            foreach (var t in Trials) if (t.Outcome == o.ToString()) n++;
            return n;
        }

        // ---- 保存と読み込み ----

        public string ToJson()
        {
            var trials = new List<object>();
            foreach (var t in Trials)
            {
                trials.Add(new Dictionary<string, object>
                {
                    { "time", t.Time }, { "seed", t.Seed }, { "fp", t.Fingerprint }, { "content", t.ContentKey },
                    { "level", t.Level }, { "mode", t.Mode }, { "outcome", t.Outcome }, { "rank", t.Rank },
                    { "cutOff", t.CutOff }, { "a", t.A }, { "b", t.B }, { "c", t.C }, { "questions", t.Questions },
                    { "skillOk", new List<int>(t.SkillOk) }, { "skillTotal", new List<int>(t.SkillTotal) },
                    { "findings", t.Findings }, { "traps", t.Traps },
                });
            }
            return MiniJson.Write(new Dictionary<string, object>
            {
                { "version", Version }, { "unlockedLevel", UnlockedLevel },
                { "runsCompleted", RunsCompleted }, { "runsFailed", RunsFailed }, { "trials", trials },
            });
        }

        /// <summary>読み込み。壊れていたら空の聴き帳を返す（遊べなくなるより、記録を失うほうがまし）。</summary>
        public static TrainingLog FromJson(string json)
        {
            var log = new TrainingLog();
            if (string.IsNullOrEmpty(json)) return log;
            try
            {
                var root = (Dictionary<string, object>)MiniJson.Parse(json);
                log.UnlockedLevel = Math.Max(1, Math.Min(ScenarioGenerator.MaxLevel, Int(root, "unlockedLevel", 1)));
                log.RunsCompleted = Int(root, "runsCompleted", 0);
                log.RunsFailed = Int(root, "runsFailed", 0);
                object ts;
                if (root.TryGetValue("trials", out ts))
                {
                    foreach (var o in (List<object>)ts)
                    {
                        var d = (Dictionary<string, object>)o;
                        var t = new TrialRecord
                        {
                            Time = Long(d, "time"), Seed = Long(d, "seed"), Fingerprint = Str(d, "fp"), ContentKey = Str(d, "content"),
                            Level = Int(d, "level", 1), Mode = Str(d, "mode"), Outcome = Str(d, "outcome"), Rank = Str(d, "rank"),
                            CutOff = d.ContainsKey("cutOff") && d["cutOff"] is bool && (bool)d["cutOff"],
                            A = Int(d, "a", 0), B = Int(d, "b", 0), C = Int(d, "c", 0), Questions = Int(d, "questions", 0),
                        };
                        CopyInts(d, "skillOk", t.SkillOk);
                        CopyInts(d, "skillTotal", t.SkillTotal);
                        CopyStrings(d, "findings", t.Findings);
                        CopyStrings(d, "traps", t.Traps);
                        log.Trials.Add(t);
                    }
                }
            }
            catch (Exception)
            {
                return new TrainingLog();
            }
            return log;
        }

        private static int Int(Dictionary<string, object> d, string k, int def)
        {
            object v;
            return d.TryGetValue(k, out v) && v is long ? (int)(long)v : def;
        }

        private static long Long(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v is long ? (long)v : 0;
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v is string ? (string)v : "";
        }

        private static void CopyInts(Dictionary<string, object> d, string k, int[] into)
        {
            object v;
            if (!d.TryGetValue(k, out v)) return;
            var l = v as List<object>;
            if (l == null) return;
            for (int i = 0; i < into.Length && i < l.Count; i++) if (l[i] is long) into[i] = (int)(long)l[i];
        }

        private static void CopyStrings(Dictionary<string, object> d, string k, List<string> into)
        {
            object v;
            if (!d.TryGetValue(k, out v)) return;
            var l = v as List<object>;
            if (l == null) return;
            foreach (var o in l) if (o is string) into.Add((string)o);
        }
    }
}
