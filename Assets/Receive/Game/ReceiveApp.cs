using System;
using System.IO;
using Receive.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Receive.Game
{
    /// <summary>
    /// 起動。どのシーンで再生しても、ここからゲームが立ち上がる。
    /// シーンやプレハブに何も置かなくてよいので、空のプロジェクトを開いて再生するだけで遊べる。
    /// </summary>
    public static class Bootstrap
    {
        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _started = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (_started) return;
            _started = true;
            var go = new GameObject("RECEIVE");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ReceiveApp>();
        }
    }

    /// <summary>ゲーム全体。画面の切り替えと、中核（Receive.Core）への橋渡しだけを受け持つ。規則は中核にある。</summary>
    public sealed partial class ReceiveApp : MonoBehaviour
    {
        private VisualElement _root, _stage, _overlays;
        private Atmosphere _atmo;
        private TrainingLog _log;
        private GameSession _session;
        private string _logPath;
        private int _pace = 1;
        private static readonly int[] PaceMs = { 62, 40, 24 };
        private static readonly string[] PaceNames = { "ゆるり", "ふつう", "はやい" };

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _pace = Mathf.Clamp(PlayerPrefs.GetInt("receive.pace", 1), 0, 2);

            if (Camera.allCamerasCount == 0)
            {
                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(transform, false);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
                cam.cullingMask = 0;
            }

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ReceiveTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 10;

            var docGo = new GameObject("UI");
            docGo.SetActive(false);
            docGo.transform.SetParent(transform, false);
            var doc = docGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            docGo.SetActive(true);

            var host = doc.rootVisualElement;
            host.style.flexGrow = 1;
            _root = new VisualElement();
            _root.AddToClassList("fill");
            _root.AddToClassList("rv");
            var sheet = Resources.Load<StyleSheet>("Receive");
            if (sheet != null) _root.styleSheets.Add(sheet);
            else Debug.LogError("RECEIVE: Resources/Receive.uss が読み込めない。画面の見た目が崩れる。");
            var font = Resources.Load<Font>("Fonts/ShipporiMincho-Medium");
            if (font != null) _root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            else Debug.LogError("RECEIVE: 和文フォントが読み込めない。日本語が表示されないおそれがある。");
            host.Add(_root);

            _atmo = new Atmosphere();
            _root.Add(_atmo);
            _stage = Ui.Box("fill stage");
            _root.Add(_stage);
            _overlays = Ui.Box("fill");
            _overlays.pickingMode = PickingMode.Ignore;
            _root.Add(_overlays);

            _logPath = Path.Combine(Application.persistentDataPath, "kikicho.json");
            _log = LoadLog();
            _session = new GameSession(_log, DateTime.UtcNow.Ticks);
            ShowTitle();
        }

        private void Update()
        {
            if (_atmo != null) _atmo.Tick(Time.unscaledDeltaTime);
        }

        // ---- 記録 ----

        private TrainingLog LoadLog()
        {
            try
            {
                return File.Exists(_logPath) ? TrainingLog.FromJson(File.ReadAllText(_logPath)) : new TrainingLog();
            }
            catch (Exception e)
            {
                Debug.LogWarning("RECEIVE: 聴き帳を読めなかった。新しい聴き帳で始める。 " + e.Message);
                return new TrainingLog();
            }
        }

        private void SaveLog()
        {
            try { File.WriteAllText(_logPath, _log.ToJson()); }
            catch (Exception e) { Debug.LogWarning("RECEIVE: 聴き帳を保存できなかった。 " + e.Message); }
        }

        // ---- 画面の切り替え ----

        private void Show(VisualElement screen)
        {
            CloseOverlay();
            _stage.Clear();
            _stage.Add(Ui.FadeIn(screen));
        }

        private void Overlay(VisualElement panel)
        {
            CloseOverlay();
            var shade = Ui.Box("fill overlay", panel);
            _overlays.pickingMode = PickingMode.Position;
            _overlays.Add(Ui.FadeIn(shade, 10));
        }

        private void CloseOverlay()
        {
            _overlays.Clear();
            _overlays.pickingMode = PickingMode.Ignore;
        }

        private string Region(int level) { return Persona.Regions[Mathf.Clamp(level, 1, Persona.Regions.Length) - 1]; }

        /// <summary>画面の上端。いまの地と、灯火の残り。</summary>
        private VisualElement StatusBar()
        {
            var j = _session.Journey;
            string where = Region(j.Level) + "　" + (j.Mode == Mode.Practice ? "稽古" : "試練 " + (j.Step + 1) + " / " + Journey.Length);
            var bar = Ui.Box("row spread", Ui.Text(where, "t-small t-dim"));
            if (j.Mode == Mode.Trial) bar.Add(Flames(j.Lanterns));
            else bar.Add(Ui.Text("罰のない稽古", "t-small t-dim"));
            return bar;
        }

        private static VisualElement Flames(int lit)
        {
            var row = Ui.Box("row", Ui.Text("灯火", "t-small t-dim"));
            for (int i = 0; i < Journey.LanternsMax; i++)
            {
                var f = Ui.Box("flame");
                if (i >= lit) f.AddToClassList("out");
                row.Add(f);
            }
            return row;
        }

        private static VisualElement Plate(string glyph, string name)
        {
            return Ui.Box("row center",
                Ui.Box("plate-glyph", Ui.Text(glyph, "plate-glyph-text")),
                Ui.Text(name, "t-large"));
        }
    }
}
