using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M1 조종석·입력 확인용 샌드박스 씬 메뉴. 씬 저장은 사람이 한다.
    /// </summary>
    public static class PilotSandboxMenu
    {
        const string SandboxSceneHint = "Assets/_Anseong/Scenes/Sandbox/Sandbox_Pilot.unity";
        const string LogPrefix = "[PilotSandboxMenu]";
        const float BossDistance = 40f;          // 플레이어 로봇 루트 → 보스 루트 (m)
        const float GroundSize = 400f;
        const float UnityPlaneSize = 10f;

        [MenuItem("Anseong/Sandbox/Build Cockpit Root Test")]
        static void BuildCockpitRootTest()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabMenu.PlayerPrefabPath);
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabMenu.BossPrefabPath);
            if (player == null || boss == null)
            {
                EditorUtility.DisplayDialog("Build Cockpit Root Test",
                    "로봇 프리팹이 없습니다. 먼저 'Anseong → Setup → Build Player Robot Prefab'을 실행하세요.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray())
                Object.DestroyImmediate(camera.gameObject);

            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(player, scene);
            playerInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var bossInstance = (GameObject)PrefabUtility.InstantiatePrefab(boss, scene);
            bossInstance.transform.SetPositionAndRotation(Vector3.forward * BossDistance, Quaternion.Euler(0f, 180f, 0f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * (GroundSize / UnityPlaneSize);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LogPrefix} 샌드박스 씬 생성 완료. Hierarchy 우클릭 → XR → XR Origin (VR) 추가 → " +
                      "'Anseong → Sandbox → Attach XR Origin To Cockpit' → (M1-02) 'Anseong → Setup → Add Pilot Input' → " +
                      $"{SandboxSceneHint}로 저장하세요.");
        }
    }
}
