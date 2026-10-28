using System.Linq;
using Anseong.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 열린 씬에 PC용 로봇 눈높이 카메라(PcEyeViewCamera)를 붙인다(선우 10/28). 헤드셋 없이 Play로 맵을 조종석 시점에서 본다.
    /// 다른 카메라는 끄지 않고 이 카메라를 가장 위(Depth)에 둔다. 씬 저장은 사람이 한다.
    /// </summary>
    public static class PcEyeViewMenu
    {
        const string MenuTitle = "Add PC Robot Eye View";
        const string ObjectName = "PcRobotEyeView";
        const string LogPrefix = "[PcEyeViewMenu]";
        // 02_architecture: Head 좌석 눈높이 약 49.2 m(대기 자세, M1-01). 플레이어 로봇 원점에서 보스(+Z)를 본다.
        const float EyeHeight = 49.2f;
        const float FarClip = 4000f;
        const float CameraDepth = 50f;

        [MenuItem("Anseong/Sandbox/Add PC Robot Eye View")]
        static void Add()
        {
            var existing = Object.FindAnyObjectByType<PcEyeViewCamera>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorUtility.DisplayDialog(MenuTitle, $"이미 '{existing.name}'이 있습니다. Play를 누르면 그 시점으로 보입니다.", "확인");
                return;
            }
            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, MenuTitle);
            go.transform.SetPositionAndRotation(new Vector3(0f, EyeHeight, 0f), Quaternion.identity);
            var camera = go.AddComponent<Camera>();
            camera.farClipPlane = FarClip;
            camera.depth = Mathf.Max(CameraDepth, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c != camera).Select(c => c.depth + 1f).DefaultIfEmpty(0f).Max());
            go.AddComponent<PcEyeViewCamera>();

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
            Debug.Log($"{LogPrefix} 로봇 눈높이 {EyeHeight} m에 PC 카메라를 붙였습니다. Play → Game 창 클릭(마우스로 둘러보기) → WASD 이동, Q/E 높이, Shift 빠르게, R 처음 자리, Esc 커서 풀기. " +
                      "Ctrl+S로 씬을 저장하세요.", go);
        }
    }
}
