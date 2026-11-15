using UnityEngine;
using UnityEngine.UIElements;

namespace Receive.Game
{
    /// <summary>
    /// 画面の奥にある景色。夜空、月、三重の山並み、流れる霧、灯火。
    /// 霧の濃さは受信の結果に応える。正しく運べば薄れ、歪めれば濃くなる。
    /// </summary>
    internal sealed class Atmosphere : VisualElement
    {
        private readonly VisualElement _fogFar, _fogNear, _lanternGlow, _moon, _whiteout;
        private float _time, _fogX1, _fogX2;
        private float _density = 0.6f, _densityTarget = 0.6f;
        private float _white, _lantern = 1f, _lanternTarget = 1f;

        public Atmosphere()
        {
            AddToClassList("fill");
            pickingMode = PickingMode.Ignore;

            Add(Layer(Art.Sky(), 0, 0, 100, 100, 1f));

            _moon = Layer(Art.Glow(new Color(0.78f, 0.84f, 0.90f), 2.6f), 62, 4, 30, 52, 0.55f);
            Add(_moon);
            Add(Layer(Art.Glow(new Color(0.93f, 0.95f, 0.97f), 9f), 73.2f, 19.5f, 7.6f, 13.5f, 0.9f));

            Add(Layer(Art.Mountains(3.1f, new Color(0.150f, 0.190f, 0.232f), new Color(0.235f, 0.285f, 0.325f), 0.95f, 1.0f), -2, 34, 104, 44, 1f));
            _fogFar = FogBand(11.3f, 44, 34);
            Add(_fogFar);
            Add(Layer(Art.Mountains(7.7f, new Color(0.082f, 0.108f, 0.140f), new Color(0.165f, 0.205f, 0.240f), 0.85f, 1.3f), -2, 48, 104, 40, 1f));
            Add(Layer(Art.Mountains(15.2f, new Color(0.030f, 0.042f, 0.058f), new Color(0.075f, 0.098f, 0.122f), 0.70f, 1.6f), -2, 66, 104, 36, 1f));
            _fogNear = FogBand(23.9f, 60, 46);
            Add(_fogNear);

            _lanternGlow = Layer(Art.Glow(new Color(1.0f, 0.70f, 0.34f), 2.2f), -6, 58, 34, 60, 0.5f);
            Add(_lanternGlow);

            Add(Layer(Art.Vignette(), 0, 0, 100, 100, 1f));

            _whiteout = new VisualElement();
            _whiteout.AddToClassList("fill");
            _whiteout.pickingMode = PickingMode.Ignore;
            _whiteout.style.backgroundColor = new Color(0.82f, 0.86f, 0.88f);
            _whiteout.style.opacity = 0f;
            Add(_whiteout);
        }

        /// <summary>百分率で置く、絵の一枚。</summary>
        private static VisualElement Layer(Texture2D tex, float left, float top, float width, float height, float opacity)
        {
            var e = new VisualElement();
            e.pickingMode = PickingMode.Ignore;
            e.style.position = Position.Absolute;
            e.style.left = new Length(left, LengthUnit.Percent);
            e.style.top = new Length(top, LengthUnit.Percent);
            e.style.width = new Length(width, LengthUnit.Percent);
            e.style.height = new Length(height, LengthUnit.Percent);
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.opacity = opacity;
            return e;
        }

        /// <summary>霧の帯。画面2枚ぶんの幅に同じ絵を並べ、1枚ぶん流れたら元へ戻す。</summary>
        private static VisualElement FogBand(float seed, float top, float height)
        {
            var band = new VisualElement();
            band.pickingMode = PickingMode.Ignore;
            band.style.position = Position.Absolute;
            band.style.left = 0;
            band.style.top = new Length(top, LengthUnit.Percent);
            band.style.width = new Length(200, LengthUnit.Percent);
            band.style.height = new Length(height, LengthUnit.Percent);
            band.style.flexDirection = FlexDirection.Row;
            var tex = Art.Fog(seed);
            for (int i = 0; i < 2; i++)
            {
                var half = new VisualElement();
                half.pickingMode = PickingMode.Ignore;
                half.style.width = new Length(50, LengthUnit.Percent);
                half.style.height = new Length(100, LengthUnit.Percent);
                half.style.backgroundImage = new StyleBackground(tex);
                band.Add(half);
            }
            return band;
        }

        /// <summary>霧の濃さ。0 は晴れ、1 は深い霧。</summary>
        public void SetFog(float density) { _densityTarget = Mathf.Clamp01(density); }

        /// <summary>灯火の明るさ。0〜1。</summary>
        public void SetLantern(float brightness) { _lanternTarget = Mathf.Clamp01(brightness); }

        /// <summary>歪みの瞬間、霧が一気に寄せて画面が白む。</summary>
        public void Whiteout() { _white = 1f; }

        public void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.1f);
            _time += dt;
            _density = Mathf.MoveTowards(_density, _densityTarget, dt * 0.22f);
            _lantern = Mathf.MoveTowards(_lantern, _lanternTarget, dt * 0.8f);
            _white = Mathf.MoveTowards(_white, 0f, dt * 0.45f);

            _fogX1 = Mathf.Repeat(_fogX1 + dt * 0.55f, 50f);
            _fogX2 = Mathf.Repeat(_fogX2 + dt * 1.25f, 50f);
            _fogFar.style.translate = new StyleTranslate(new Translate(new Length(-_fogX1, LengthUnit.Percent), new Length(0, LengthUnit.Pixel), 0));
            _fogNear.style.translate = new StyleTranslate(new Translate(new Length(-_fogX2, LengthUnit.Percent), new Length(0, LengthUnit.Pixel), 0));
            _fogFar.style.opacity = 0.25f + 0.60f * _density;
            _fogNear.style.opacity = 0.18f + 0.75f * _density;
            _moon.style.opacity = 0.62f - 0.40f * _density;

            // 灯火のゆらぎ。規則的に見えないよう、周期の違う波を重ねる
            float flicker = 0.86f + 0.07f * Mathf.Sin(_time * 7.3f) + 0.05f * Mathf.Sin(_time * 12.9f + 1.7f)
                          + 0.03f * Mathf.Sin(_time * 23.1f + 0.4f);
            _lanternGlow.style.opacity = (0.20f + 0.42f * _lantern) * flicker;
            _whiteout.style.opacity = Mathf.SmoothStep(0f, 0.92f, _white);
        }
    }
}
