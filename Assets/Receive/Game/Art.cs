using UnityEngine;

namespace Receive.Game
{
    /// <summary>
    /// 背景の絵を、起動時に計算で描く。画像ファイルを持たないので、素材なしで世界観を出せる。
    /// 夜の空、霧の向こうの山並み、流れる霧、灯火の光。
    /// </summary>
    internal static class Art
    {
        private static Texture2D Make(int w, int h, Color32[] px, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = wrap;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        private static Color Mix(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }

        /// <summary>夜の空。上は墨、地平に近づくほど霧の青灰色。</summary>
        public static Texture2D Sky()
        {
            const int w = 4, h = 256;
            var top = new Color(0.020f, 0.030f, 0.048f);
            var mid = new Color(0.062f, 0.090f, 0.128f);
            var horizon = new Color(0.215f, 0.262f, 0.300f);
            var ground = new Color(0.035f, 0.047f, 0.062f);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1); // 1 が上
                Color c;
                if (v > 0.62f) c = Mix(mid, top, (v - 0.62f) / 0.38f);
                else if (v > 0.36f) c = Mix(horizon, mid, (v - 0.36f) / 0.26f);
                else c = Mix(ground, horizon, v / 0.36f);
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            return Make(w, h, px, TextureWrapMode.Clamp);
        }

        /// <summary>山並みの影。稜線は濃く、裾は霧に溶ける。</summary>
        public static Texture2D Mountains(float seed, Color ridge, Color foot, float height, float rough)
        {
            const int w = 1024, h = 256;
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float n = Mathf.PerlinNoise(seed + u * 2.1f, seed * 0.37f) * 0.60f
                        + Mathf.PerlinNoise(seed * 1.7f + u * 6.3f, 4.1f) * 0.27f
                        + Mathf.PerlinNoise(seed * 2.9f + u * 19f, 9.7f) * 0.13f * rough;
                float top = Mathf.Clamp01(height * (0.45f + n * 0.95f)) * (h - 2);
                for (int y = 0; y < h; y++)
                {
                    float edge = Mathf.Clamp01(top - y);           // 稜線をなめらかに
                    float depth = top <= 1f ? 0f : 1f - y / top;   // 稜線から裾への深さ
                    Color c = Mix(ridge, foot, depth * depth);
                    c.a = edge * (1f - 0.55f * depth * depth * depth);
                    px[y * w + x] = c;
                }
            }
            return Make(w, h, px, TextureWrapMode.Clamp);
        }

        /// <summary>霧。左右がつながるので、流し続けても継ぎ目が出ない。</summary>
        public static Texture2D Fog(float seed)
        {
            const int w = 512, h = 128;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float fall = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 1.3f);
                for (int x = 0; x < w; x++)
                {
                    float a = Cloud(seed, x, y), b = Cloud(seed, x - w, y);
                    float n = Mathf.Lerp(a, b, x / (float)w);
                    float alpha = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(n * 1.35f - 0.12f), 1.5f) * fall);
                    px[y * w + x] = new Color(0.80f, 0.85f, 0.88f, alpha);
                }
            }
            return Make(w, h, px, TextureWrapMode.Clamp);
        }

        private static float Cloud(float seed, float x, float y)
        {
            return Mathf.PerlinNoise(seed + x * 0.012f, seed + y * 0.035f) * 0.65f
                 + Mathf.PerlinNoise(seed * 2.3f + x * 0.035f, y * 0.09f) * 0.35f;
        }

        /// <summary>丸い光。灯火と月に使う。</summary>
        public static Texture2D Glow(Color color, float power)
        {
            const int s = 128;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                Color c = color;
                c.a = Mathf.Pow(d, power);
                px[y * s + x] = c;
            }
            return Make(s, s, px, TextureWrapMode.Clamp);
        }

        /// <summary>画面の縁を暗く落とす。</summary>
        public static Texture2D Vignette()
        {
            const int s = 128;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy);
                float a = Mathf.Clamp01((d - 0.45f) / 0.75f);
                px[y * s + x] = new Color(0.01f, 0.015f, 0.022f, a * a * 0.9f);
            }
            return Make(s, s, px, TextureWrapMode.Clamp);
        }
    }
}
