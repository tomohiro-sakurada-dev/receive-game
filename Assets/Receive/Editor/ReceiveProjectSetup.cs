using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Receive.EditorTools
{
    /// <summary>
    /// プロジェクトを初めて開いたときに、起動用のシーンと URP の設定を用意する。
    /// シーンや設定のファイルは Unity Editor でしか正しく作れないので、手書きのファイルを置かず、ここで作らせる。
    ///
    /// ゲームそのものは、この処理が失敗しても動く（どのシーンで再生しても Bootstrap が立ち上げる）。
    /// URP の用意は URP の型を名前で探して行うので、URP の版が違ってもコンパイルは通る。
    /// 失敗したときは Console に手作業の手順を出す。
    /// </summary>
    public static class ReceiveProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Receive.unity";
        private const string SettingsDir = "Assets/Settings";
        private const string UrpAssembly = "Unity.RenderPipelines.Universal.Runtime";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath) && GraphicsSettings.defaultRenderPipeline != null) return;
            Setup();
        }

        [MenuItem("RECEIVE/プロジェクトを整える（シーンと URP）")]
        public static void Setup()
        {
            PlayerSettings.productName = "PROJECT RECEIVE";
            EnsureScene();
            EnsureUrp();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureScene()
        {
            try
            {
                if (!File.Exists(ScenePath))
                {
                    var active = SceneManager.GetActiveScene();
                    if (active.isDirty && !string.IsNullOrEmpty(active.path))
                    {
                        Debug.Log("RECEIVE: 開いているシーンに未保存の変更があるため、起動用シーンの作成を見送った。");
                        return;
                    }
                    Directory.CreateDirectory("Assets/Scenes");
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var camGo = new GameObject("Main Camera");
                    camGo.tag = "MainCamera";
                    var cam = camGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
                    camGo.AddComponent<AudioListener>();
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    Debug.Log("RECEIVE: 起動用のシーンを作った。 " + ScenePath);
                }
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            }
            catch (Exception e)
            {
                Debug.LogWarning("RECEIVE: 起動用シーンを作れなかった。空のシーンを開いて再生すれば、ゲームは動く。 " + e.Message);
            }
        }

        private static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            try
            {
                var urpType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " + UrpAssembly);
                if (urpType == null)
                {
                    Debug.LogWarning("RECEIVE: URP のパッケージが見つからない。Package Manager で Universal RP を入れてから、メニュー RECEIVE から整え直すこと。");
                    return;
                }
                Directory.CreateDirectory(SettingsDir);
                string assetPath = SettingsDir + "/Receive_URP.asset";

                // URP が持つ「レンダラー設定を作る」処理を使う。見つからなければ素の設定を作る
                ScriptableObject renderer = null;
                var makeRenderer = urpType.GetMethod("CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (makeRenderer != null)
                {
                    var ps = makeRenderer.GetParameters();
                    var args = new object[ps.Length];
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i == 0) args[i] = assetPath;
                        else if (ps[i].ParameterType.IsEnum) args[i] = Enum.Parse(ps[i].ParameterType, "UniversalRenderer");
                        else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                    }
                    renderer = makeRenderer.Invoke(null, args) as ScriptableObject;
                }
                if (renderer == null)
                {
                    var dataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, " + UrpAssembly);
                    renderer = ScriptableObject.CreateInstance(dataType);
                    AssetDatabase.CreateAsset(renderer, SettingsDir + "/Receive_URP_Renderer.asset");
                }

                MethodInfo create = null;
                foreach (var m in urpType.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    if (m.Name == "Create" && m.GetParameters().Length == 1) create = m;
                var asset = create == null ? null : create.Invoke(null, new object[] { renderer }) as RenderPipelineAsset;
                if (asset == null) throw new InvalidOperationException("URP の設定を作る処理が見つからない");
                AssetDatabase.CreateAsset(asset, assetPath);
                GraphicsSettings.defaultRenderPipeline = asset;
                QualitySettings.renderPipeline = asset;
                Debug.Log("RECEIVE: URP の設定を作り、既定の描画方式に割り当てた。 " + assetPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RECEIVE: URP の設定を自動では作れなかった。手作業の手順: Project ウィンドウで Create > Rendering > URP Asset (with Universal Renderer) を作り、"
                                 + "Project Settings > Graphics の Default Render Pipeline に割り当てる。ゲームは URP の設定がなくても動く。 " + e);
            }
        }
    }
}
