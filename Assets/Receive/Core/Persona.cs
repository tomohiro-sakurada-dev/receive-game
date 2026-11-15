using System.Collections.Generic;

namespace Receive.Core
{
    /// <summary>話し手の人格。語尾と決まり文句で話し方を変える。</summary>
    public sealed class Persona
    {
        public string Id, Glyph, Title;
        public string Req;          // て形の後（届けて＋おくれ）
        public string Cop;          // 名詞の後（明後日＋じゃ）
        public string Think;        // 話し手の推測
        public string Hear;         // 伝聞
        public string NotThatFmt;   // 訂正（{0}ではない）
        public string CorrPrefix, AfterPrefix, Ended, NotYet, NoneFmt, NoPatience, RepeatPrefix, Uncertain, LeadIn, MoodLow;
        public string[] Speakers, Greetings, Closings, Smalltalk;

        public static readonly IReadOnlyList<Persona> All = new[]
        {
            new Persona
            {
                Id = "elder", Glyph = "翁", Title = "古老",
                Req = "おくれ", Cop = "じゃ",
                Think = "と思うのじゃが、確かではない", Hear = "と人づてに聞いたが、わしが確かめたわけではない",
                NotThatFmt = "{0}ではないぞ。",
                CorrPrefix = "……いや、待て。言い違えた。", AfterPrefix = "おお、そうじゃ。もう一つあった。",
                Ended = "話はそれだけじゃ。", NotYet = "それはこれから話す。まあ、最後まで聞きなされ。",
                NoneFmt = "{0}は、特に決めておらん。", NoPatience = "もう何度も答えた。これ以上は答えんぞ。",
                RepeatPrefix = "言うたとおりじゃ。", Uncertain = "はっきりとは言えんが、",
                LeadIn = "頼みというのは、こうじゃ。", MoodLow = "古老の眉が、わずかに寄った。",
                Speakers = new[] { "里の古老・与一", "渡りの翁・仁助" },
                Greetings = new[] { "おお、聴き継ぎの者か。ちょうどよいところへ来た。", "聴き継ぎか。ひとつ、頼まれてはくれんか。" },
                Closings = new[] { "頼んだぞ。", "では、頼むぞ。" },
                Smalltalk = new[] { "近ごろは霧が深うてな。書きつけも一晩で溶けてしまう。", "わしも若い頃は、よう言葉を運んだものじゃ。" },
            },
            new Persona
            {
                Id = "hostess", Glyph = "女", Title = "女将",
                Req = "くださいな", Cop = "ですよ",
                Think = "と思うんですけど、確かじゃありません", Hear = "と人づてに聞きましたけど、私が確かめたわけじゃありません",
                NotThatFmt = "{0}じゃありません。",
                CorrPrefix = "……あら、ごめんなさい。言い間違えました。", AfterPrefix = "あ、そうそう。もう一つだけ。",
                Ended = "お話はそれだけですよ。", NotYet = "それは今からお話ししますよ。最後まで聞いてくださいな。",
                NoneFmt = "{0}は、特に決めていないんですよ。", NoPatience = "もう何度もお答えしましたよ。これきりにしてくださいな。",
                RepeatPrefix = "先ほど申したとおり、", Uncertain = "はっきりとは言えませんけど、",
                LeadIn = "お願いというのは、こうなんです。", MoodLow = "女将の笑みが、少しだけ硬くなった。",
                Speakers = new[] { "宿の女将・お滝", "糸屋のお梅" },
                Greetings = new[] { "あら、聴き継ぎさん。ひとつ、お願いできますか。", "聴き継ぎさん、ちょうどよかった。聞いてくださいな。" },
                Closings = new[] { "お願いしますね。", "では、よろしくお願いしますよ。" },
                Smalltalk = new[] { "この霧でしょう。帳面に書いても、朝には読めなくなってしまって。", "今年は冷えるのが早いこと。" },
            },
            new Persona
            {
                Id = "craftsman", Glyph = "匠", Title = "職人",
                Req = "くれ", Cop = "だ",
                Think = "と思うが、確かじゃねえ", Hear = "と人づてに聞いたが、俺が確かめたわけじゃねえ",
                NotThatFmt = "{0}じゃねえ。",
                CorrPrefix = "……いや、違った。言い間違いだ。", AfterPrefix = "おっと、言い忘れた。もう一つある。",
                Ended = "話は終わりだ。", NotYet = "それは今から言う。最後まで聞け。",
                NoneFmt = "{0}は、特に決めてねえ。", NoPatience = "何度も訊くな。もう答えねえぞ。",
                RepeatPrefix = "言ったとおりだ。", Uncertain = "はっきりとは言えねえが、",
                LeadIn = "頼みってのは、こうだ。", MoodLow = "職人が、小さく舌打ちをした。",
                Speakers = new[] { "石工の伝次", "木挽きの長吉" },
                Greetings = new[] { "おう、聴き継ぎか。手が離せねえんだ、頼まれてくれ。", "聴き継ぎだな。いいところに来た。" },
                Closings = new[] { "頼んだぜ。", "じゃあ、任せたぞ。" },
                Smalltalk = new[] { "この霧じゃ、墨も一晩で流れちまう。口で運ぶしかねえ。", "今日は朝から手が冷えていけねえ。" },
            },
            new Persona
            {
                Id = "monk", Glyph = "僧", Title = "僧",
                Req = "くだされ", Cop = "でございます",
                Think = "かと存じますが、確かではございません", Hear = "と人づてに伺いましたが、私が確かめたわけではございません",
                NotThatFmt = "{0}ではございません。",
                CorrPrefix = "……失礼。言い誤りました。", AfterPrefix = "ああ、もう一つございました。",
                Ended = "申すことは以上でございます。", NotYet = "それはこれから申します。どうか最後までお聞きくだされ。",
                NoneFmt = "{0}は、特に定めておりませぬ。", NoPatience = "幾度もお答えしました。これ以上はお答えいたしかねます。",
                RepeatPrefix = "先ほど申しましたとおり、", Uncertain = "しかとは申せませぬが、",
                LeadIn = "お頼みと申しますのは、こうでございます。", MoodLow = "僧は静かに目を伏せた。",
                Speakers = new[] { "山寺の僧・了然", "堂守の慈海" },
                Greetings = new[] { "これは聴き継ぎ殿。ひとつ、お頼みしたいことがございます。", "聴き継ぎ殿。よいところへお越しくだされた。" },
                Closings = new[] { "よろしくお頼み申します。", "では、お頼み申します。" },
                Smalltalk = new[] { "霧は文字を溶かしますが、言葉までは溶かしませぬ。", "今朝は鐘の音が、よく通りました。" },
            },
        };

        public static Persona Find(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return All[0];
        }

        public static readonly string[] Receivers = { "取次の志乃", "取次の清右衛門", "取次の千早" };

        public static readonly string[] Scenes =
        {
            "麓の里。朝もやの残る辻に、人が立っている。",
            "杉の参道。苔むした石段に、霧が降りている。",
            "渓流の寺。雨音が、軒を打っている。",
            "紅葉の峠。関所の屋根に、夕陽が差している。",
            "霧上の山城。雲海の上に、月が出ている。",
        };

        public static readonly string[] Regions = { "麓の里", "杉の参道", "渓流の寺", "紅葉の峠", "霧上の山城" };
    }

    /// <summary>文の組み立て。出題時と、聞き返しへの返事の両方で使う。</summary>
    public static class Phrases
    {
        public static string Core(Persona p, string task, string person, string item, string qtyInline)
        {
            string P = Lexicon.Text(person), I = Lexicon.Text(item);
            string Q = string.IsNullOrEmpty(qtyInline) ? "" : Lexicon.Text(qtyInline);
            switch (task)
            {
                case Lexicon.TaskDeliver: return P + "のところへ、" + I + "を" + Q + "届けて" + p.Req + "。";
                case Lexicon.TaskCollect: return P + "のところから、" + I + "を" + Q + "受け取ってきて" + p.Req + "。";
                case Lexicon.TaskReturn: return P + "に、借りていた" + I + "を" + Q + "返してきて" + p.Req + "。";
                case Lexicon.TaskTell: return P + "に、" + I + "のことを伝えて" + p.Req + "。";
                default: return P + "を迎えに行って" + p.Req + "。";
            }
        }

        public static string SlotNoun(Slot s)
        {
            switch (s)
            {
                case Slot.Day: return "日";
                case Slot.Time: return "刻";
                case Slot.Place: return "場所";
                case Slot.Quantity: return "数";
                case Slot.Person: return "相手";
                case Slot.Item: return "品";
                default: return Lexicon.Label(s);
            }
        }

        /// <summary>はっきり言う文。</summary>
        public static string Plain(Persona p, Slot s, string value, int variant)
        {
            string v = Lexicon.Text(value);
            if (s == Slot.Day && variant == 1) return "頼みたいのは" + v + p.Cop + "。";
            if (s == Slot.Time && variant == 1) return "頃合いは" + v + p.Cop + "。";
            if (s == Slot.Place && variant == 1) return "落ち合う先は" + v + p.Cop + "。";
            if (s == Slot.Task) return "頼みは、" + v + "こと" + p.Cop + "。";
            return SlotNoun(s) + "は" + v + p.Cop + "。";
        }

        public static string DayAndTime(Persona p, string day, string time)
        {
            return "日は" + Lexicon.Text(day) + "、刻は" + Lexicon.Text(time) + p.Cop + "。";
        }

        public static string Condition(Persona p, string trigger, string action)
        {
            return "ただし、" + Lexicon.Text(trigger) + "、" + Lexicon.Find(action).Te + p.Req + "。";
        }

        public static string Guess(Persona p, Slot s, string value)
        {
            string v = Lexicon.Text(value);
            string tail = s == Slot.Quantity ? "で足りる" : "でよい";
            return SlotNoun(s) + "は、たぶん" + v + tail + p.Think + "。";
        }

        public static string Hearsay(Persona p, Slot s, string value)
        {
            return SlotNoun(s) + "は" + Lexicon.Text(value) + "だ" + p.Hear + "。";
        }

        public static string VagueWord(Slot s)
        {
            switch (s)
            {
                case Slot.Place: return "いつもの所";
                case Slot.Time: return "いつもの頃合い";
                default: return "いつもと同じ数";
            }
        }

        public static string Vague(Persona p, Slot s)
        {
            if (s == Slot.Quantity) return "数は、いつもと同じ" + p.Cop + "。";
            return SlotNoun(s) + "は、" + VagueWord(s) + p.Cop + "。";
        }

        public static string VagueReveal(Persona p, Slot s, string value)
        {
            return VagueWord(s) + "とは、" + Lexicon.Text(value) + "のこと" + p.Cop + "。";
        }

        public static string Correction(Persona p, Slot s, string right, string wrong)
        {
            return p.CorrPrefix + SlotNoun(s) + "は" + Lexicon.Text(right) + p.Cop + "。" + string.Format(p.NotThatFmt, Lexicon.Text(wrong));
        }

        public static string Distractor(Persona p, Slot s, string value)
        {
            string v = Lexicon.Text(value);
            switch (s)
            {
                case Slot.Time: return "そういえば、きのう霧が晴れたのは" + v + "のこと" + p.Cop + "。";
                case Slot.Place: return "そういえば、" + v + "のあたりは、いま紅葉が見頃" + p.Cop + "。";
                default: return "そういえば、うちの猫が子を" + v + "産んだの" + p.Cop + "。";
            }
        }

        public static string ProbeText(Slot s, string value, string value2)
        {
            if (s == Slot.Condition)
                return "念のため確かめたい。「" + Lexicon.Describe(s, value, value2) + "」——そういうことだな。";
            return "念のため確かめたい。" + Lexicon.Label(s) + "は「" + Lexicon.Text(value) + "」——そういうことだな。";
        }
    }
}
