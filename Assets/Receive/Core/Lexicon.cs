using System.Collections.Generic;

namespace Receive.Core
{
    public sealed class Term
    {
        public string Id;
        public string Text;
        public string Te;     // 「その時の扱い」のて形
        public string Group;  // 品（thing）か事柄（matter）か

        public Term(string id, string text, string te = null, string group = null)
        {
            Id = id; Text = text; Te = te; Group = group;
        }
    }

    /// <summary>
    /// この世界の語彙。固有の値はここから選ぶ。
    /// 回答もここから選ぶので、表記の揺れや打ち間違いで誤判定が起きない。
    /// </summary>
    public static class Lexicon
    {
        public static readonly IReadOnlyList<Term> Persons = new[]
        {
            new Term("p_genzo", "鍛冶屋の源蔵"), new Term("p_osumi", "紙漉きのお澄"), new Term("p_yasuke", "渡し守の弥助"),
            new Term("p_jinroku", "炭焼きの甚六"), new Term("p_okinu", "機織りのお絹"), new Term("p_soan", "薬師の宗庵"),
            new Term("p_tatsu", "宮大工の辰吉"), new Term("p_otomi", "茶屋のお富"), new Term("p_matahachi", "猟師の又八"),
            new Term("p_yohei", "寺男の与平"), new Term("p_okon", "染物屋のお紺"), new Term("p_tokubei", "桶屋の徳兵衛"),
            new Term("p_toji", "塩売りの藤次"), new Term("p_sayo", "巫女の小夜"),
        };

        public const string TaskDeliver = "t_deliver", TaskCollect = "t_collect", TaskReturn = "t_return",
                            TaskTell = "t_tell", TaskFetch = "t_fetch";

        public static readonly IReadOnlyList<Term> Tasks = new[]
        {
            new Term(TaskDeliver, "届ける"), new Term(TaskCollect, "受け取ってくる"), new Term(TaskReturn, "返してくる"),
            new Term(TaskTell, "伝える"), new Term(TaskFetch, "迎えに行く"),
        };

        public static readonly IReadOnlyList<Term> Items = new[]
        {
            new Term("i_herbs", "薬草の包み", null, "thing"), new Term("i_oil", "油壺", null, "thing"),
            new Term("i_cloth", "反物", null, "thing"), new Term("i_salt", "塩の俵", null, "thing"),
            new Term("i_sickle", "研いだ鎌", null, "thing"), new Term("i_lantern", "提灯", null, "thing"),
            new Term("i_kaki", "干し柿の籠", null, "thing"), new Term("i_box", "文箱", null, "thing"),
            new Term("i_charcoal", "炭の俵", null, "thing"), new Term("i_miso", "味噌の樽", null, "thing"),
            new Term("m_meeting", "寄り合い", null, "matter"), new Term("m_rite", "法要", null, "matter"),
            new Term("m_bridge", "橋の普請", null, "matter"), new Term("m_festival", "祭りの支度", null, "matter"),
            new Term("m_water", "田の水入れ", null, "matter"), new Term("m_snow", "雪囲いの手伝い", null, "matter"),
        };

        public static readonly IReadOnlyList<Term> Quantities = new[]
        {
            new Term("q_1", "一つ"), new Term("q_2", "二つ"), new Term("q_3", "三つ"), new Term("q_4", "四つ"),
            new Term("q_5", "五つ"), new Term("q_6", "六つ"), new Term("q_7", "七つ"), new Term("q_8", "八つ"),
        };

        public static readonly IReadOnlyList<Term> Days = new[]
        {
            new Term("d_today", "今日"), new Term("d_1", "明日"), new Term("d_2", "明後日"), new Term("d_3", "三日後"),
            new Term("d_4", "四日後"), new Term("d_5", "五日後"), new Term("d_market", "次の市の日"), new Term("d_moon", "次の満月の日"),
        };

        public static readonly IReadOnlyList<Term> Times = new[]
        {
            new Term("h_dawn", "夜明け"), new Term("h_morning", "朝"), new Term("h_noon", "昼"), new Term("h_after", "昼過ぎ"),
            new Term("h_evening", "夕方"), new Term("h_dusk", "日暮れ"), new Term("h_night", "夜更け"),
        };

        public static readonly IReadOnlyList<Term> Places = new[]
        {
            new Term("l_ferry", "渡し場"), new Term("l_steps", "古社の石段"), new Term("l_tea", "峠の茶屋"),
            new Term("l_mill", "水車小屋"), new Term("l_gate", "寺の山門"), new Term("l_cross", "里の辻"),
            new Term("l_kiln", "炭焼き小屋"), new Term("l_bridge", "吊り橋のたもと"), new Term("l_bath", "湯屋の裏"),
            new Term("l_cedar", "一本杉"), new Term("l_store", "蔵の前"), new Term("l_shrine", "竹林の祠"),
        };

        public static readonly IReadOnlyList<Term> Triggers = new[]
        {
            new Term("c_rain", "雨が降ったら"), new Term("c_absent", "相手が留守なら"), new Term("c_bridge", "橋が落ちていたら"),
            new Term("c_fog", "霧が濃ければ"), new Term("c_river", "川が増していたら"), new Term("c_dark", "日が暮れてしまったら"),
            new Term("c_gate", "門が閉まっていたら"), new Term("c_guest", "先客がいたら"),
        };

        public static readonly IReadOnlyList<Term> Actions = new[]
        {
            new Term("a_cancel", "取りやめる", "取りやめて"), new Term("a_next", "翌日に延ばす", "翌日に延ばして"),
            new Term("a_report", "引き返して知らせる", "引き返して知らせて"), new Term("a_temple", "寺で待つ", "寺で待って"),
            new Term("a_back", "裏道を回る", "裏道を回って"), new Term("a_note", "言づてだけ残す", "言づてだけ残して"),
            new Term("a_return", "無理をせず戻る", "無理をせず戻って"), new Term("a_dawn", "夜明けを待つ", "夜明けを待って"),
        };

        private static readonly Dictionary<string, Term> ById = BuildIndex();

        private static Dictionary<string, Term> BuildIndex()
        {
            var d = new Dictionary<string, Term>();
            foreach (var list in new[] { Persons, Tasks, Items, Quantities, Days, Times, Places, Triggers, Actions })
                foreach (var t in list) d.Add(t.Id, t);
            return d;
        }

        public static Term Find(string id)
        {
            Term t;
            return id != null && ById.TryGetValue(id, out t) ? t : null;
        }

        public static string Text(string id)
        {
            var t = Find(id);
            return t == null ? "" : t.Text;
        }

        /// <summary>その欄で選べる語。条件の欄は「場合」の語を返す（「扱い」は Actions）。</summary>
        public static IReadOnlyList<Term> For(Slot s)
        {
            switch (s)
            {
                case Slot.Person: return Persons;
                case Slot.Task: return Tasks;
                case Slot.Item: return Items;
                case Slot.Quantity: return Quantities;
                case Slot.Day: return Days;
                case Slot.Time: return Times;
                case Slot.Place: return Places;
                default: return Triggers;
            }
        }

        public static string Label(Slot s)
        {
            switch (s)
            {
                case Slot.Person: return "相手";
                case Slot.Task: return "用向き";
                case Slot.Item: return "品・事柄";
                case Slot.Quantity: return "数";
                case Slot.Day: return "日";
                case Slot.Time: return "刻";
                case Slot.Place: return "場所";
                default: return "ただし書き";
            }
        }

        public static string Question(Slot s)
        {
            switch (s)
            {
                case Slot.Person: return "相手はどなたですか";
                case Slot.Task: return "何をすればよいのですか";
                case Slot.Item: return "何を、でしょうか";
                case Slot.Quantity: return "数はいくつですか";
                case Slot.Day: return "日はいつですか";
                case Slot.Time: return "刻はいつ頃ですか";
                case Slot.Place: return "場所はどちらですか";
                default: return "ただし書きはありますか";
            }
        }

        public static readonly Slot[] AllSlots =
        {
            Slot.Person, Slot.Task, Slot.Item, Slot.Quantity, Slot.Day, Slot.Time, Slot.Place, Slot.Condition
        };

        /// <summary>値を人が読める形にする。条件は「場合、扱い」。</summary>
        public static string Describe(Slot s, string value, string value2)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (s == Slot.Condition) return Text(value) + "、" + Text(value2);
            return Text(value);
        }
    }
}
