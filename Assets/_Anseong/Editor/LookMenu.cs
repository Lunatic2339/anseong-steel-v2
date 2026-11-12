using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 화면 분위기(11/12 "객관적으로 퀄리티 있어 보이게"): 후처리(블룸·톤매핑·색 보정) Volume + 거리 안개 + XR 카메라 후처리 켜기.
    /// 65 m 로봇은 멀리 있는 것이 뿌옇게 보여야 크게 느껴진다(공기 원근). 불꽃·예고 빛은 블룸이 있어야 빛처럼 보인다.
    /// 메뉴 'Anseong → Art → Apply Battle Look (후처리·안개)'. 여러 번 실행해도 된다. 값은 Art/Environment/BattleLook.asset(Volume Profile)과
    /// Lighting 창의 Fog에서 고친다. VR 멀미를 피하려고 비네트·모션 블러·화면 흔들림은 넣지 않는다.
    /// </summary>
    public static class LookMenu
    {
        const string ProfileFolder = "Assets/_Anseong/Art/Environment";
        const string ProfilePath = ProfileFolder + "/BattleLook.asset";
        const string VolumeName = "Battle Look (후처리)";

        [MenuItem("Anseong/Art/Apply Battle Look (후처리·안개)")]
        static void ApplyFromMenu()
        {
            var scene = SceneManager.GetActiveScene();
            Debug.Log(Apply(scene) + " 씬을 저장하세요.");
        }

        public static string Apply(Scene scene)
        {
            if (!scene.IsValid()) return "[LookMenu] 열린 씬이 없습니다.";
            if (!AssetDatabase.IsValidFolder(ProfileFolder)) AssetDatabase.CreateFolder("Assets/_Anseong/Art", "Environment");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            var created = false;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                created = true;

                var bloom = Add<Bloom>(profile);
                bloom.threshold.Override(1.0f);
                bloom.intensity.Override(0.55f);
                bloom.scatter.Override(0.62f);

                var tone = Add<Tonemapping>(profile);
                tone.mode.Override(TonemappingMode.Neutral);

                var color = Add<ColorAdjustments>(profile);
                color.postExposure.Override(0.1f);
                color.contrast.Override(12f);
                color.saturation.Override(-6f);

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            var roots = scene.GetRootGameObjects();
            var volume = roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.isGlobal);
            if (volume == null)
            {
                var go = new GameObject(VolumeName);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Battle Look");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
            }
            if (volume.sharedProfile == null) volume.sharedProfile = profile;

            // XR 카메라 후처리.
            var cameras = 0;
            foreach (var origin in roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)))
            {
                if (origin.Camera == null) continue;
                var data = origin.Camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
                cameras++;
            }

            // 거리 안개(이 씬이 활성 씬일 때 쓰인다. Main이 활성 씬).
            var fog = "안개 그대로(활성 씬이 아님)";
            if (SceneManager.GetActiveScene() == scene && !RenderSettings.fog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.0016f;
                RenderSettings.fogColor = new Color(0.60f, 0.61f, 0.63f);
                fog = "안개 켬(지수², 0.0016)";
            }
            else if (RenderSettings.fog) fog = "안개 이미 켜짐(그대로)";

            EditorSceneManager.MarkSceneDirty(scene);
            return $"[LookMenu] 후처리 프로필 {(created ? "만듦" : "있음")}({ProfilePath}), XR 카메라 {cameras}대 후처리 켬, {fog}.";
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
