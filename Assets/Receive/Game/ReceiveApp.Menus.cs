using System.Collections.Generic;
using Receive.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Receive.Game
{
    /// <summary>題字、心得、結び灯籠、聴き帳。</summary>
    public sealed partial class ReceiveApp
    {
        private void ShowTitle()
        {
            _atmo.SetFog(0.55f);
            _atmo.SetLantern(1f);

            var title = Ui.Box("row center",
                Ui.Vertical("霧音の国", "vchar-small"),
                Ui.Gap(34),
                Ui.Vertical("聴き継ぎ", "vchar"));
            title.style.width = 620;

            var menu = Ui.Box("col");
            menu.style.alignItems = Align.FlexStart;
            menu.style.justifyContent = Justify.Center;
            menu.style.flexGrow = 1;
            menu.Add(Ui.Text("PROJECT RECEIVE", "title-latin"));
            menu.Add(Ui.Space(46));
            menu.Add(Ui.Btn("道行きに出る", "btn btn-menu btn-main", () => { _session.StartJourney(Mode.Trial, _log.UnlockedLevel); ShowGate(); }));
            menu.Add(Ui.Btn("稽古場", "btn btn-menu", () => { _session.StartJourney(Mode.Practice, _log.UnlockedLevel); ShowGate(); }));
            menu.Add(Ui.Btn("聴き帳", "btn btn-menu", ShowLedger));
            menu.Add(Ui.Btn("心得", "btn btn-menu", ShowHowTo));
            menu.Add(Ui.Space(30));
            menu.Add(Ui.Text("いまの道　" + Region(_log.UnlockedLevel) + "（第" + Kanji(_log.UnlockedLevel) + "の地）", "t-small t-dim"));
            Button pace = null;
            pace = Ui.Btn("語りの速さ　" + PaceNames[_pace], "btn btn-quiet", () =>
            {
                _pace = (_pace + 1) % 3;
                PlayerPrefs.SetInt("receive.pace", _pace);
                pace.text = "語りの速さ　" + PaceNames[_pace];
            });
            pace.style.marginLeft = 0;
            menu.Add(Ui.Space(8));
            menu.Add(pace);

            var top = Ui.Box("row grow", title, Ui.Gap(120), menu);
            var lore = Ui.Text("この国の霧は、墨を溶かす。言葉は、口から口へ運ぶしかない。", "lore t-center");
            Show(Ui.Box("screen", top, lore, Ui.Space(10)));
        }

        private static string Kanji(int n)
        {
            string[] k = { "〇", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            return n >= 0 && n < k.Length ? k[n] : n.ToString();
        }

        private void ShowHowTo()
        {
            string[] rules =
            {
                "言葉は一度きり。語り終えた言葉は霧に溶け、読み返せない。",
                "話が終わったかどうかは、自分で確かめる。「続きを聴く」で話し手が終わりを告げるまで、話は終わっていない。",
                "聞き返しには限りがある。これから語られることを先に訊けば、話の腰を折る。",
                "整理では、八つの欄すべてに印を付ける。聞いた・推し量り・分からない・言われていない。",
                "言われていないことを「聞いた」としてはならない。分からないことは、分からないと言えばよい。それに罰はない。",
                "受け手は、言い換えて確かめてくる。話し手の言葉と違えば、流されずに正す。",
            };
            var panel = Ui.Box("panel grow");
            for (int i = 0; i < rules.Length; i++)
            {
                var row = Ui.Box("row", Ui.Text(Kanji(i + 1), "t-amber"), Ui.Gap(26), Ui.Text(rules[i], "grow"));
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 16;
                panel.Add(row);
            }
            panel.Add(Ui.Rule());
            panel.Add(Ui.Text("成就　言葉が歪まずに届いた。先へ進む。", "t-small"));
            panel.Add(Ui.Text("未達　偽りは渡していないが、肝心なことが欠けた。罰はなく、別の話でやり直す。", "t-small"));
            panel.Add(Ui.Text("歪み　誤った言葉を、確かなこととして渡した。結び灯籠まで戻される。", "t-small"));
            panel.Add(Ui.Space(10));
            panel.Add(Ui.Text("灯火は三つ。中ほどの誤りひとつで、ひとつ消える。すべて消えれば、道行きははじめから。", "t-small t-dim"));

            Show(Ui.Box("screen",
                Ui.Text("心得", "t-head"),
                Ui.Space(18),
                panel,
                Ui.Box("row", Ui.Box("grow"), Ui.Btn("戻る", "btn", ShowTitle))));
        }

        /// <summary>結び灯籠。試練の前に立つ場所。</summary>
        private void ShowGate()
        {
            var j = _session.Journey;
            _atmo.SetFog(0.6f);
            _atmo.SetLantern(j.Mode == Mode.Practice ? 1f : j.Lanterns / (float)Journey.LanternsMax);

            var mid = Ui.Box("col grow center");
            mid.Add(Ui.Text("結び灯籠", "t-tiny t-dim t-center"));
            mid.Add(Ui.Space(14));
            mid.Add(Ui.Text(Region(j.Level), "t-head t-center"));
            mid.Add(Ui.Space(26));
            mid.Add(Ui.Text(Persona.Scenes[j.Level - 1], "narration"));
            mid.Add(Ui.Space(40));
            if (j.Mode == Mode.Trial)
            {
                mid.Add(Flames(j.Lanterns));
                mid.Add(Ui.Space(16));
                mid.Add(Ui.Text("試練　" + Kanji(j.Step + 1) + "　／　" + Kanji(Journey.Length), "t-small t-dim t-center"));
            }
            else
            {
                mid.Add(Ui.Text("ここは稽古場。何を誤っても、失うものはない。", "t-small t-dim t-center"));
            }
            mid.Add(Ui.Space(54));
            mid.Add(Ui.Btn("話し手のもとへ", "btn btn-main", BeginTrial));
            mid.Add(Ui.Btn("里へ戻る", "btn btn-quiet", ShowTitle));
            Show(Ui.Box("screen", mid));
        }

        private void ShowLedger()
        {
            _atmo.SetFog(0.45f);
            var head = Ui.Box("row spread",
                Ui.Text("聴き帳", "t-head"),
                Ui.Text("試練 " + _log.Trials.Count + "　　成就 " + _log.CountOutcome(Outcome.Fulfilled) + "　未達 " + _log.CountOutcome(Outcome.Incomplete)
                        + "　歪み " + _log.CountOutcome(Outcome.Distorted) + "　　道行き成就 " + _log.RunsCompleted, "t-small t-dim"));

            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("七つの技。ひとつの点にまとめず、別々に数える。", "t-small t-dim"));
            panel.Add(Ui.Space(10));
            foreach (var s in SkillInfo.All)
            {
                var all = _log.SkillTally(s);
                var recent = _log.SkillTally(s, 10);
                var name = Ui.Text(SkillInfo.Name(s), "t");
                name.style.width = 300;
                var desc = Ui.Text(SkillInfo.Describe(s), "t-tiny t-dim grow");
                string num = all.Total == 0 ? "まだ機会がない" : all.Ok + " / " + all.Total + "　" + (100 * all.Ok / all.Total) + "%";
                var count = Ui.Text(num, "t-small t-right");
                count.style.width = 250;
                var meter = Ui.Meter(recent.Ok, recent.Total);
                meter.style.width = 200;
                meter.style.marginLeft = 20;
                var row = Ui.Box("row", name, desc, count, meter);
                row.style.marginBottom = 12;
                panel.Add(row);
            }
            panel.Add(Ui.Text("帯は、直近十の試練での割合。", "t-tiny t-dim t-right"));

            var errors = Ui.Box("panel-soft wrap");
            errors.style.marginTop = 14;
            var counts = _log.ErrorCounts();
            if (counts.Count == 0) errors.Add(Ui.Text("誤りの記録は、まだない。", "t-small t-dim"));
            foreach (KeyValuePair<string, int> kv in counts)
            {
                FindingType t;
                string label = System.Enum.TryParse(kv.Key, out t) ? Rules.TypeLabel(t) : kv.Key;
                var cell = Ui.Text(label + "　" + kv.Value, "t-small");
                cell.style.marginRight = 40;
                errors.Add(cell);
            }

            Show(Ui.Box("screen",
                head, Ui.Space(16), panel, errors, Ui.Box("grow"),
                Ui.Box("row", Ui.Box("grow"), Ui.Btn("戻る", "btn", ShowTitle))));
        }
    }
}
