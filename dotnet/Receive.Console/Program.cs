// 端末版。Unity と同じ中核（Assets/Receive/Core）で、同じ試練を文字だけで遊ぶ。
// Unity が無い環境でも、最後まで遊べること・規則が動くことを確かめるためのもの。
//
//   dotnet run --project dotnet/Receive.Console            … 遊ぶ
//   dotnet run --project dotnet/Receive.Console -- sample 5 3   … 段3のシナリオを5本、正解つきで表示
//   dotnet run --project dotnet/Receive.Console -- demo         … 自動の手本を表示
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Receive.Core;

internal static class Program
{
    private static string _logPath;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        _logPath = Environment.GetEnvironmentVariable("RECEIVE_LOG")
                   ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".receive", "kikicho.json");
        if (args.Length > 0 && args[0] == "sample") { Sample(args); return 0; }
        if (args.Length > 0 && args[0] == "demo") { Demo(); return 0; }
        Play();
        return 0;
    }

    // ---------- 表示用 ----------

    private static void Sample(string[] args)
    {
        int n = args.Length > 1 ? int.Parse(args[1]) : 3;
        int level = args.Length > 2 ? int.Parse(args[2]) : 3;
        long baseSeed = args.Length > 3 ? long.Parse(args[3]) : 1000;
        for (int i = 0; i < n; i++)
        {
            var sc = ScenarioGenerator.Generate(baseSeed + i, level);
            Console.WriteLine("==== 種 " + sc.Seed + " / 段 " + level + " / " + sc.Script.SpeakerName + " / 間合い " + sc.Script.Patience
                              + " / 罠 [" + string.Join(", ", sc.Key.Traps) + "]");
            foreach (var b in sc.Script.Beats) Console.WriteLine("  " + b.Index + " [" + b.Kind + "] " + b.Text);
            Console.WriteLine("  -- 評価データ（封印 " + sc.Seal.Substring(0, 12) + "…）");
            foreach (var u in sc.Key.Units)
                Console.WriteLine("     " + Lexicon.Label(u.Slot).PadRight(6, '　') + u.State + (u.HasValue ? " 「" + Lexicon.Describe(u.Slot, u.Value, u.Value2) + "」" : "")
                                  + (u.HasValue ? " " + u.Importance : "") + (u.Decoy != null ? " 訂正前=" + Lexicon.Text(u.Decoy) : "")
                                  + (u.Late ? " (終わり際)" : "") + (u.DistractorValue != null ? " 紛れ=" + Lexicon.Text(u.DistractorValue) : ""));
            foreach (var p in sc.Key.Probes) Console.WriteLine("     問い返し: " + p.Text);
        }
    }

    private static void Demo()
    {
        var session = new GameSession(new TrainingLog(), 20261115);
        session.StartJourney(Mode.Practice, 3);
        var sc = session.BeginTrial();
        Console.WriteLine("【" + sc.Script.Scene + "】 " + sc.Script.SpeakerName);
        while (session.Talk.BeatsHeard < sc.Script.Beats.Count) Console.WriteLine("  「" + session.Talk.Listen().Text + "」");
        session.Talk.Leave();
        ModelAnswer.Fill(sc, session.Talk, session.Sheet);
        var j = session.Finish(DateTime.UtcNow.Ticks);
        PrintJudgement(session, j);
    }

    // ---------- 遊ぶ ----------

    private static void Play()
    {
        var log = LoadLog();
        var session = new GameSession(log, DateTime.UtcNow.Ticks);
        Console.WriteLine();
        Console.WriteLine("　霧音の国　——　PROJECT RECEIVE（端末版）");
        Console.WriteLine("　この国の霧は墨を溶かす。言葉は、口から口へ運ぶしかない。");
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("[1] 道行きに出る（段 " + log.UnlockedLevel + "）  [2] 稽古  [3] 聴き帳  [q] 終わる");
            string c = Ask("> ");
            if (c == "q" || c == null) return;
            if (c == "3") { PrintLedger(log); continue; }
            if (c != "1" && c != "2") continue;
            session.StartJourney(c == "1" ? Mode.Trial : Mode.Practice, log.UnlockedLevel);
            while (true)
            {
                RunTrial(session);
                SaveLog(log);
                var step = session.LastStep;
                if (step == StepResult.Advanced) { Console.WriteLine("\n先へ進む。灯火 " + Lanterns(session.Journey) + "　試練 " + (session.Journey.Step + 1) + "/" + Journey.Length); continue; }
                if (step == StepResult.Retry) { Console.WriteLine("\n未達。罰はない。別の話で、もう一度。"); continue; }
                if (step == StepResult.RunCompleted) Console.WriteLine("\n道行き成就。霧が晴れ、先の道が見える。");
                if (step == StepResult.RunFailed) Console.WriteLine("\n結び灯籠で目を覚ました。道行きは、はじめから。");
                break;
            }
        }
    }

    private static string Lanterns(Journey j)
    {
        return new string('●', j.Lanterns) + new string('○', Journey.LanternsMax - j.Lanterns);
    }

    private static void RunTrial(GameSession session)
    {
        var sc = session.BeginTrial();
        var talk = session.Talk;
        Console.WriteLine();
        Console.WriteLine("―― " + sc.Script.Scene);
        Console.WriteLine("―― " + sc.Script.SpeakerName + " が、こちらを見ている。");
        if (session.Journey.Mode == Mode.Trial) Console.WriteLine("―― 灯火 " + Lanterns(session.Journey));
        Console.WriteLine("（言葉は一度きり。 Enter=続きを聴く / a=聞き返す / l=承った、と話を終える）");
        Say(talk.Listen());
        while (!talk.Left)
        {
            if (talk.MoodHint != null) Console.WriteLine("　（" + talk.MoodHint + "）");
            string c = Ask("  … ");
            if (c == null || c == "l") { talk.Leave(); break; }
            if (c == "a") { AskSpeaker(talk); continue; }
            Say(talk.Listen());
        }

        Console.WriteLine();
        Console.WriteLine("―― 整理。受け取ったことを、欄ごとに書き出す。");
        var sheet = session.Sheet;
        while (true)
        {
            foreach (var s in Lexicon.AllSlots) FillSlot(sheet, s);
            Console.WriteLine();
            foreach (var s in Lexicon.AllSlots) Console.WriteLine("  " + Lexicon.Label(s) + "：" + sheet.Describe(s));
            string c = Ask("[Enter] 申し送る  [a] 話し手に確かめに戻る  [r] 書き直す > ");
            if (c == "a") { AskSpeaker(talk); continue; }
            if (c == "r") continue;
            if (sheet.Validate() == null) break;
            Console.WriteLine(sheet.Validate());
        }

        Console.WriteLine();
        Console.WriteLine("―― " + sc.Script.ReceiverName + " が、申し送りを聞き終えて問い返す。");
        foreach (var p in sc.Key.Probes)
        {
            Console.WriteLine("  「" + p.Text + "」");
            while (true)
            {
                string c = Ask("   [1] その通りです  [2] いいえ、違います  [3] そこまでは聞いていません > ");
                if (c == "1" || c == "2" || c == "3") { sheet.Replies.Add((ProbeReply)(int.Parse(c) - 1)); break; }
            }
        }
        var j = session.Finish(DateTime.UtcNow.Ticks);
        PrintJudgement(session, j);
    }

    private static void Say(Line line)
    {
        if (line != null) Console.WriteLine("  「" + line.Text + "」");
    }

    private static void AskSpeaker(Conversation talk)
    {
        Console.WriteLine("   何を確かめる？");
        for (int i = 0; i < Lexicon.AllSlots.Length; i++) Console.WriteLine("    [" + (i + 1) + "] " + Lexicon.Question(Lexicon.AllSlots[i]));
        Console.WriteLine("    [9] いまのお言葉を、もう一度  [0] やめる");
        string c = Ask("   > ");
        int n;
        if (!int.TryParse(c, out n) || n == 0) return;
        Say(n == 9 ? talk.AskRepeat() : n >= 1 && n <= 8 ? talk.Ask(Lexicon.AllSlots[n - 1]) : null);
    }

    private static void FillSlot(AnswerSheet sheet, Slot s)
    {
        Console.WriteLine();
        Console.WriteLine("■ " + Lexicon.Label(s) + "　[1] 聞いた  [2] 推し量り  [3] 分からない  [4] 言われていない");
        Mark mark;
        while (true)
        {
            string c = Ask("  印 > ");
            if (c == "1" || c == "2" || c == "3" || c == "4") { mark = (Mark)(int.Parse(c) - 1); break; }
        }
        string v = null, v2 = null;
        if (mark == Mark.Heard || mark == Mark.Inferred)
        {
            v = PickTerm(Lexicon.For(s), s == Slot.Condition ? "どんな場合" : "中身");
            if (s == Slot.Condition) v2 = PickTerm(Lexicon.Actions, "その時の扱い");
        }
        sheet.Set(s, mark, v, v2);
    }

    private static string PickTerm(IReadOnlyList<Term> terms, string title)
    {
        var sb = new StringBuilder("  " + title + "：");
        for (int i = 0; i < terms.Count; i++) sb.Append(" [" + (i + 1) + "]" + terms[i].Text);
        Console.WriteLine(sb.ToString());
        while (true)
        {
            int n;
            if (int.TryParse(Ask("  番号 > "), out n) && n >= 1 && n <= terms.Count) return terms[n - 1].Id;
        }
    }

    private static void PrintJudgement(GameSession session, Judgement j)
    {
        Console.WriteLine();
        Console.WriteLine("════ " + Rules.OutcomeLabel(j.Outcome) + (j.Rank != Rank.None ? "（" + Rules.RankLabel(j.Rank) + "）" : "") + " ════");
        if (j.CutOff) Console.WriteLine("　話を途中で切り上げた。");
        foreach (var f in j.Findings)
        {
            if (f.Type == FindingType.Correct || f.Type == FindingType.CorrectlyUnknown) continue;
            string sev = Rules.SeverityLabel(f.Severity);
            Console.WriteLine("・" + Rules.TypeLabel(f.Type) + (sev.Length > 0 ? "〔" + sev + "〕" : "〔罰なし〕") + "　" + Lexicon.Label(f.Slot));
            Console.WriteLine("　　話し手：" + f.SpeakerQuote);
            Console.WriteLine("　　あなた：" + f.PlayerQuote);
            Console.WriteLine("　　" + f.Explanation);
        }
        Console.WriteLine("―― この試練での技能");
        foreach (var s in SkillInfo.All)
        {
            var t = j.Skills[(int)s];
            Console.WriteLine("　" + SkillInfo.Name(s).PadRight(10, '　') + (t.Total == 0 ? "機会なし" : t.Ok + " / " + t.Total));
        }
    }

    private static void PrintLedger(TrainingLog log)
    {
        Console.WriteLine();
        Console.WriteLine("―― 聴き帳　（試練 " + log.Trials.Count + "　成就 " + log.CountOutcome(Outcome.Fulfilled) + "　未達 " + log.CountOutcome(Outcome.Incomplete)
                          + "　歪み " + log.CountOutcome(Outcome.Distorted) + "）");
        foreach (var s in SkillInfo.All)
        {
            var t = log.SkillTally(s);
            Console.WriteLine("　" + SkillInfo.Name(s).PadRight(10, '　') + (t.Total == 0 ? "まだ機会がない" : t.Ok + " / " + t.Total + "　（" + (100 * t.Ok / t.Total) + "%）"));
        }
        foreach (var kv in log.ErrorCounts()) Console.WriteLine("　　" + kv.Key + " × " + kv.Value);
    }

    private static string Ask(string prompt)
    {
        Console.Write(prompt);
        string s = Console.ReadLine();
        return s == null ? null : s.Trim();
    }

    private static TrainingLog LoadLog()
    {
        try { return File.Exists(_logPath) ? TrainingLog.FromJson(File.ReadAllText(_logPath)) : new TrainingLog(); }
        catch (IOException) { return new TrainingLog(); }
    }

    private static void SaveLog(TrainingLog log)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
        File.WriteAllText(_logPath, log.ToJson());
    }
}
