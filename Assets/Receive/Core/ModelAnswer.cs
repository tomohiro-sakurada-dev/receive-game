namespace Receive.Core
{
    /// <summary>
    /// 手本の申し送り。聞いた言葉をそのまま受け取った場合の答え。
    /// 出題の検査（どのシナリオも、最後まで聞けば成就できること）と、分析画面での見比べに使う。
    /// </summary>
    public static class ModelAnswer
    {
        public static void Fill(Scenario sc, Conversation talk, AnswerSheet sheet)
        {
            foreach (var u in sc.Key.Units)
            {
                switch (Evaluator.Effective(u, talk))
                {
                    case KeyState.Stated: sheet.Set(u.Slot, Mark.Heard, u.Value, u.Value2); break;
                    case KeyState.Guess:
                    case KeyState.Hearsay: sheet.Set(u.Slot, Mark.Inferred, u.Value, u.Value2); break;
                    case KeyState.Vague: sheet.Set(u.Slot, Mark.Unknown); break;
                    default: sheet.Set(u.Slot, Mark.NotSaid); break;
                }
            }
            sheet.Replies.Clear();
            for (int i = 0; i < sc.Key.Probes.Count; i++) sheet.Replies.Add(Evaluator.ExpectedReply(sc, talk, i));
        }

        /// <summary>その欄の、手本の書き方。</summary>
        public static string Describe(InfoUnit u, Conversation talk)
        {
            switch (Evaluator.Effective(u, talk))
            {
                case KeyState.Stated: return Lexicon.Describe(u.Slot, u.Value, u.Value2) + "〈聞いた〉";
                case KeyState.Guess:
                case KeyState.Hearsay: return Lexicon.Describe(u.Slot, u.Value, u.Value2) + "〈推し量り〉";
                case KeyState.Vague: return "〈分からない〉（聞き返せば分かった）";
                default: return "〈言われていない〉";
            }
        }
    }
}
