using System.Collections.Generic;

namespace Receive.Core
{
    /// <summary>
    /// 1回の遊びの進行をまとめる。画面（Unity）も端末版も、これを通して同じ規則で動く。
    /// 通信も AI も使わない。
    /// </summary>
    public sealed class GameSession
    {
        private readonly Rng _seeds;

        public TrainingLog Log { get; private set; }
        public Journey Journey { get; private set; }
        public Scenario Current { get; private set; }
        public Conversation Talk { get; private set; }
        public AnswerSheet Sheet { get; private set; }
        public Judgement LastJudgement { get; private set; }
        public StepResult LastStep { get; private set; }

        public GameSession(TrainingLog log, long seed)
        {
            Log = log ?? new TrainingLog();
            _seeds = new Rng(seed);
        }

        public void StartJourney(Mode mode, int level)
        {
            Journey = new Journey(mode, level);
            Current = null; Talk = null; Sheet = null; LastJudgement = null;
        }

        /// <summary>新しい試練を始める。同じ話は二度と出ない。評価データはここで封印される。</summary>
        public Scenario BeginTrial()
        {
            Current = ScenarioGenerator.GenerateUnique(_seeds, Journey.Level, Log.SeenFingerprints(),
                                                       new HashSet<string>(Log.RecentContentKeys(12)));
            Talk = new Conversation(Current);
            Sheet = new AnswerSheet();
            LastJudgement = null;
            return Current;
        }

        /// <summary>申し送りと問い返しが済んだら呼ぶ。判定し、聴き帳に残し、道行きを進める。</summary>
        public Judgement Finish(long nowTicks)
        {
            LastJudgement = Evaluator.Evaluate(Current, Talk, Sheet);
            Log.Record(Current, Talk, LastJudgement, Journey.Mode, nowTicks);
            LastStep = Journey.Apply(LastJudgement);
            Log.ApplyRun(LastStep, Journey.Level);
            return LastJudgement;
        }
    }
}
