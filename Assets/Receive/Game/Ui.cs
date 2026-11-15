using System;
using UnityEngine.UIElements;

namespace Receive.Game
{
    /// <summary>画面の部品を作る小道具。見た目は Receive.uss の組で決める。</summary>
    internal static class Ui
    {
        private static void Classes(VisualElement e, string cls)
        {
            if (string.IsNullOrEmpty(cls)) return;
            foreach (var c in cls.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
        }

        public static VisualElement Box(string cls, params VisualElement[] kids)
        {
            var e = new VisualElement();
            Classes(e, cls);
            foreach (var k in kids) if (k != null) e.Add(k);
            return e;
        }

        public static Label Text(string text, string cls = null)
        {
            var l = new Label(text);
            l.RemoveFromClassList(Label.ussClassName); // 既定の見た目を外し、こちらの組だけで決める
            l.AddToClassList("t");
            Classes(l, cls);
            return l;
        }

        public static Button Btn(string text, string cls, Action click)
        {
            var b = new Button(click);
            b.text = text;
            b.RemoveFromClassList(Button.ussClassName);
            Classes(b, cls);
            return b;
        }

        public static VisualElement Space(float height)
        {
            var e = new VisualElement();
            e.style.height = height;
            e.style.flexShrink = 0;
            return e;
        }

        public static VisualElement Gap(float width)
        {
            var e = new VisualElement();
            e.style.width = width;
            e.style.flexShrink = 0;
            return e;
        }

        public static VisualElement Rule() { return Box("rule"); }

        /// <summary>縦書き。一文字ずつ縦に積む。</summary>
        public static VisualElement Vertical(string text, string charClass)
        {
            var col = Box("col");
            col.style.alignItems = Align.Center;
            foreach (char c in text)
            {
                var l = new Label(c.ToString());
                l.RemoveFromClassList(Label.ussClassName);
                Classes(l, charClass);
                col.Add(l);
            }
            return col;
        }

        /// <summary>霧の中から現れるように、ゆっくり表示する。</summary>
        public static T FadeIn<T>(T e, int delayMs = 30) where T : VisualElement
        {
            e.AddToClassList("fade");
            e.schedule.Execute(() => e.AddToClassList("in")).StartingIn(delayMs);
            return e;
        }

        /// <summary>割合を示す細い帯。</summary>
        public static VisualElement Meter(int ok, int total)
        {
            var fill = Box("meter-fill");
            fill.style.width = new Length(total <= 0 ? 0 : 100f * ok / total, LengthUnit.Percent);
            return Box("meter", fill);
        }
    }
}
