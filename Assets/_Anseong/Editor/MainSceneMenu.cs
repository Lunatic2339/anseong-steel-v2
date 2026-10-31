using System.Collections.Generic;
using System.Linq;
using Anseong.Flow;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M1-07: Main.unity에 M1 결과(로봇·보스·조종석 XR·입력·전투·안내판·조절 메뉴)를 모으고 윤주 맵을 같이 불러오게 한다.
    /// 여러 번 실행해도 된다(있는 것은 그대로 두고 연결만 다시). 씬 저장은 사람이 한다(Main은 선우만).
    /// </summary>
    public static class MainSceneMenu
    {
        const string LogPrefix = "[MainSceneMenu]";
        const string MainScenePath = "Assets/_Anseong/Scenes/Main.unity";
        // 윤주 E-01 맵(yunju 브랜치 Sandbox_ReferenceLayout, 전투 공간 X±150·Z±250을 비워 둔 배치). 바꾸면 여기만 고친다.
        const string EnvironmentScenePath = "Assets/_Anseong/Scenes/Sandbox/Sandbox_ReferenceLayout.unity";
        const float BossDistance = 40f;          // 플레이어 로봇 루트 → 보스 루트 (m), 샌드박스와 같다
        const float CameraFarClip = 8000f;       // 맵 먼 산까지(윤주 로봇 눈높이 카메라와 같은 값)

        [MenuItem("Anseong/Setup/Build M1 Main", priority = 1)]
        static void BuildM1Main()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }
            if (!RobotPrefabMenu.Build()) return;

            var player = PlaceOnce(scene, RobotPrefabMenu.PlayerPrefabPath, Vector3.zero, 0f);
            var boss = PlaceOnce(scene, RobotPrefabMenu.BossPrefabPath, Vector3.forward * BossDistance, 180f);
            if (player == null || boss == null) return;

            if (!EyeHeightSandboxMenu.AttachXROrigin(scene)) return;
            var origin = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).First();
            if (origin.Camera != null)
            {
                Undo.RecordObject(origin.Camera, "Build M1 Main");
                origin.Camera.farClipPlane = Mathf.Max(origin.Camera.farClipPlane, CameraFarClip);
            }
            if (!SceneSetupMenu.AddPilotInputToActiveScene()) return;

            var environment = AddEnvironmentLoader(scene);
            var restart = AddSessionRestart(scene);
            var network = NetworkSessionMenu.AddToScene(scene);
            var buildList = EnsureBuildScenes();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LogPrefix} Main 구성 완료: 로봇·보스({BossDistance} m)·조종석 XR·입력·전투. 맵: {environment}. 재시작: {restart}. " +
                      $"네트워크: {network}. Build Profiles 씬 목록: {buildList}. Ctrl+S로 Main을 저장하세요.", origin);
        }

        /// <summary>프리팹 인스턴스가 씬에 없을 때만 만든다. 있으면 그대로 둔다.</summary>
        static GameObject PlaceOnce(Scene scene, string prefabPath, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Build M1 Main", $"프리팹이 없습니다: {prefabPath}", "확인");
                return null;
            }
            var existing = scene.GetRootGameObjects()
                .FirstOrDefault(root => PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab);
            if (existing != null) return existing;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Undo.RegisterCreatedObjectUndo(instance, "Build M1 Main");
            return instance;
        }

        static string AddEnvironmentLoader(Scene scene)
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScenePath);
            var loader = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnvironmentLoader>(true)).FirstOrDefault();
            if (loader == null)
            {
                var go = new GameObject("Environment");
                Undo.RegisterCreatedObjectUndo(go, "Build M1 Main");
                SceneManager.MoveGameObjectToScene(go, scene);
                loader = go.AddComponent<EnvironmentLoader>();
            }
            var sun = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(l => l.type == LightType.Directional);
            var serialized = new SerializedObject(loader);
            serialized.FindProperty("environmentScene").stringValue = map != null ? map.name : string.Empty;
            serialized.FindProperty("mainLight").objectReferenceValue = sun;
            serialized.ApplyModifiedProperties();
            return map != null ? $"{map.name}(Play하면 같이 불러옴)" : $"맵 씬이 없어 불러오지 않음({EnvironmentScenePath})";
        }

        static string AddSessionRestart(Scene scene)
        {
            var restart = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SessionRestart>(true)).FirstOrDefault();
            if (restart != null) return "이미 있음(R)";
            var go = new GameObject("Session Restart");
            Undo.RegisterCreatedObjectUndo(go, "Build M1 Main");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<SessionRestart>();
            return "키보드 R";
        }

        /// <summary>Build Profiles 씬 목록 맨 앞에 Main, 그다음 맵. 다른 씬은 그대로 둔다.</summary>
        static string EnsureBuildScenes()
        {
            var wanted = new List<string> { MainScenePath };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScenePath) != null) wanted.Add(EnvironmentScenePath);
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in wanted) scenes.Add(new EditorBuildSettingsScene(path, true));
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !wanted.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
            return string.Join(", ", wanted.Select(System.IO.Path.GetFileNameWithoutExtension));
        }
    }
}
