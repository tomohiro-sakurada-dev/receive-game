using System.Collections.Generic;

namespace Receive.Core
{
    public enum ProbeReply { Agree = 0, Disagree = 1, NotSure = 2 }

    public sealed class Entry
    {
        public Slot Slot;
        public bool Marked;
        public Mark Mark;
        public string Value, Value2;
    }

    /// <summary>プレイヤーの申し送り。8つの欄それぞれに確度の印を付け、受け手の問い返しに答える。</summary>
    public sealed class AnswerSheet
    {
        public readonly Entry[] Entries = new Entry[Lexicon.AllSlots.Length];
        public readonly List<ProbeReply> Replies = new List<ProbeReply>();

        public AnswerSheet()
        {
            foreach (var s in Lexicon.AllSlots) Entries[(int)s] = new Entry { Slot = s };
        }

        public Entry this[Slot s] { get { return Entries[(int)s]; } }

        public void Set(Slot s, Mark mark, string value = null, string value2 = null)
        {
            var e = this[s];
            e.Marked = true; e.Mark = mark;
            bool hasValue = mark == Mark.Heard || mark == Mark.Inferred;
            e.Value = hasValue ? value : null;
            e.Value2 = hasValue && s == Slot.Condition ? value2 : null;
        }

        /// <summary>確定できない理由。確定できるなら null。</summary>
        public string Validate()
        {
            foreach (var e in Entries)
            {
                string label = Lexicon.Label(e.Slot);
                if (!e.Marked) return "「" + label + "」の印がまだ付いていない。";
                if (e.Mark == Mark.Heard || e.Mark == Mark.Inferred)
                {
                    if (string.IsNullOrEmpty(e.Value)) return "「" + label + "」の中身がまだ選ばれていない。";
                    if (e.Slot == Slot.Condition && string.IsNullOrEmpty(e.Value2)) return "「ただし書き」の、その時の扱いがまだ選ばれていない。";
                }
            }
            return null;
        }

        public static string MarkLabel(Mark m)
        {
            switch (m)
            {
                case Mark.Heard: return "聞いた";
                case Mark.Inferred: return "推し量り";
                case Mark.Unknown: return "分からない";
                default: return "言われていない";
            }
        }

        public string Describe(Slot s)
        {
            var e = this[s];
            if (!e.Marked) return "（未記入）";
            string v = Lexicon.Describe(s, e.Value, e.Value2);
            return v.Length > 0 ? v + "〈" + MarkLabel(e.Mark) + "〉" : "〈" + MarkLabel(e.Mark) + "〉";
        }
    }

    public enum FindingType
    {
        Correct,          // 正しく受け取った
        CorrectlyUnknown, // 言われていないことを、言われていないと認識した
        Misheard,         // 重要情報の誤認
        UnawareOmission,  // 聞き漏らし（言われたのに「言われていない」とした）
        DeclaredGap,      // 不明の申告（分からないと正直に述べた）
        UnresolvedVague,  // あいまいなまま確かめなかった
        Fabrication,      // 根拠のない断定（言われていないことを、聞いたこととして）
        Overconfident,    // 根拠のない断定（推測・伝聞を、確かなこととして）
        Underconfident,   // はっきり言われたことを、推し量りとして
        HedgedWrong,      // 推し量りとして述べたが、中身が違う
        UnneededGuess,    // 言われていないことに、自分の推し量りを足した
        FalseGap,         // 言われていないのに、聞き漏らしたと思った
        Distortion,       // 歪曲（受け手の言い換えに同意して、根拠のない内容を渡した）
        Retraction        // 歪曲（自分の申し送りを、問い返しで覆した・弱めた）
    }

    public enum Severity { None = 0, C = 1, B = 2, A = 3 }
    public enum Cause { None, CutOff, PreCorrection, PartOfCondition }
    public enum EvidenceSource { Deterministic, AI }
    public enum Outcome { Fulfilled, Incomplete, Distorted, Void }
    public enum Rank { None, Meikyo, Sumi, Nigori }

    public enum Skill
    {
        ListenToEnd = 0,   // 最後まで聞く
        Accuracy = 1,      // 正確に受け取る
        NoFilling = 2,     // 言われていないことを補わない
        FactVsGuess = 3,   // 事実と推測を区別する
        KnowUnknown = 4,   // 分からないことを認識する
        Questioning = 5,   // 適切な時に質問する
        FaithfulRelay = 6  // 歪めずに伝える
    }

    public struct Tally
    {
        public int Ok, Total;
        public void Add(bool ok) { Total++; if (ok) Ok++; }
    }

    public sealed class Finding
    {
        public Slot Slot;
        public FindingType Type;
        public Severity Severity;
        public Cause Cause;
        public EvidenceSource Source = EvidenceSource.Deterministic;
        public bool Advisory;         // AI 由来など、罰の根拠にしない参考情報
        public bool FromProbe;
        public string SpeakerQuote;   // 話し手が実際に言った言葉
        public bool QuoteVerbatim;    // SpeakerQuote が記録上の言葉そのままか
        public bool Unheard;          // 切り上げたため、プレイヤーは聞いていない
        public string PlayerQuote;    // プレイヤーの申し送り、または問い返しへの答え
        public string Explanation;
    }

    public sealed class Judgement
    {
        public Outcome Outcome;
        public Rank Rank;
        public bool SealOk;
        public bool CutOff;
        public int LanternLoss;
        public readonly List<Finding> Findings = new List<Finding>();
        public readonly Tally[] Skills = new Tally[SkillInfo.Count];

        public int Count(Severity s)
        {
            int n = 0;
            foreach (var f in Findings) if (f.Severity == s) n++;
            return n;
        }

        public int Count(FindingType t)
        {
            int n = 0;
            foreach (var f in Findings) if (f.Type == t) n++;
            return n;
        }
    }

    public static class SkillInfo
    {
        public const int Count = 7;
        public static readonly Skill[] All =
        {
            Skill.ListenToEnd, Skill.Accuracy, Skill.NoFilling, Skill.FactVsGuess,
            Skill.KnowUnknown, Skill.Questioning, Skill.FaithfulRelay
        };

        public static string Name(Skill s)
        {
            switch (s)
            {
                case Skill.ListenToEnd: return "最後まで聞く";
                case Skill.Accuracy: return "正確に受け取る";
                case Skill.NoFilling: return "補わない";
                case Skill.FactVsGuess: return "事実と推測を分ける";
                case Skill.KnowUnknown: return "分からないと知る";
                case Skill.Questioning: return "時を得て問う";
                default: return "歪めずに伝える";
            }
        }

        public static string Describe(Skill s)
        {
            switch (s)
            {
                case Skill.ListenToEnd: return "話を切り上げず、終わり際の言葉や言い直しまで受け取れたか";
                case Skill.Accuracy: return "はっきり言われた中身を、そのまま受け取れたか";
                case Skill.NoFilling: return "言われていない欄に、中身を足さなかったか";
                case Skill.FactVsGuess: return "確かなことと、推測・伝聞とを、取り違えなかったか";
                case Skill.KnowUnknown: return "受け取れなかったものを、受け取れなかったと分かっていたか";
                case Skill.Questioning: return "確かめるべきことを、話の腰を折らずに確かめたか";
                default: return "受け手の言い換えに流されず、受け取ったとおりに渡せたか";
            }
        }
    }

    /// <summary>
    /// 罰の規則表。AI は使わない。
    /// 事実の判定（Evaluator）と、罰の重さの決定（この表）を分けてある。
    /// </summary>
    public static class Rules
    {
        /// <summary>ミスの種類と、出題前に決めた重要度から、重さを決める。</summary>
        public static Severity SeverityOf(FindingType type, Importance imp, bool unsaidSlot)
        {
            switch (type)
            {
                case FindingType.Misheard:
                case FindingType.UnawareOmission:
                case FindingType.Overconfident:
                    return imp == Importance.Critical ? Severity.A : imp == Importance.Major ? Severity.B : Severity.C;
                case FindingType.Fabrication:
                    // 言われていない日時・人物・対象・数・場所・条件を事実として渡すのは、重要度によらず致命的
                    return Severity.A;
                case FindingType.Distortion:
                    if (unsaidSlot) return Severity.A;
                    return imp == Importance.Critical ? Severity.A : imp == Importance.Major ? Severity.B : Severity.C;
                case FindingType.HedgedWrong:
                case FindingType.Underconfident:
                    return imp == Importance.Critical ? Severity.B : imp == Importance.Major ? Severity.C : Severity.None;
                case FindingType.Retraction:
                    return imp == Importance.Critical ? Severity.B : Severity.C;
                case FindingType.UnneededGuess:
                    return Severity.C;
                default:
                    return Severity.None;
            }
        }

        /// <summary>
        /// AI の判断だけでは即失敗にしない。AI 由来の指摘は最大でも B に抑え、参考扱いの印を付ける。
        /// （この試作に AI の評価役は入っていない。将来つないだときのための歯止め）
        /// </summary>
        public static void CapBySource(Finding f)
        {
            if (f.Source != EvidenceSource.AI) return;
            f.Advisory = true;
            if (f.Severity == Severity.A) f.Severity = Severity.B;
        }

        public static string TypeLabel(FindingType t)
        {
            switch (t)
            {
                case FindingType.Correct: return "正しく受け取った";
                case FindingType.CorrectlyUnknown: return "言われていないと見極めた";
                case FindingType.Misheard: return "誤認";
                case FindingType.UnawareOmission: return "聞き漏らし";
                case FindingType.DeclaredGap: return "不明の申告";
                case FindingType.UnresolvedVague: return "確かめずじまい";
                case FindingType.Fabrication: return "根拠のない断定";
                case FindingType.Overconfident: return "根拠のない断定";
                case FindingType.Underconfident: return "確かなことを推し量りに";
                case FindingType.HedgedWrong: return "推し量りの中身違い";
                case FindingType.UnneededGuess: return "余計な推し量り";
                case FindingType.FalseGap: return "漏らしたと思い込んだ";
                case FindingType.Distortion: return "歪曲";
                default: return "歪曲（自ら覆した）";
            }
        }

        public static string SeverityLabel(Severity s)
        {
            switch (s)
            {
                case Severity.A: return "致命";
                case Severity.B: return "中";
                case Severity.C: return "軽";
                default: return "";
            }
        }

        public static string OutcomeLabel(Outcome o)
        {
            switch (o)
            {
                case Outcome.Fulfilled: return "成就";
                case Outcome.Incomplete: return "未達";
                case Outcome.Distorted: return "歪み";
                default: return "無効";
            }
        }

        public static string RankLabel(Rank r)
        {
            switch (r)
            {
                case Rank.Meikyo: return "明鏡";
                case Rank.Sumi: return "澄";
                case Rank.Nigori: return "濁";
                default: return "";
            }
        }
    }
}
