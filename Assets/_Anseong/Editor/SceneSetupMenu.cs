using System.Linq;
using Anseong.Pilot;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 씬 구성을 돕는 에디터 메뉴. 씬 저장은 사람이 한다.
    /// </summary>
    public static class SceneSetupMenu
    {
        const string PilotInputPath = "Assets/_Anseong/Runtime/Pilot/PilotInput.inputactions";
        const string LeftHandMap = "Left Hand";
        const string RightHandMap = "Right Hand";
        const string PositionActionName = "Position";
        const string RotationActionName = "Rotation";
        const string TrackingStateActionName = "Tracking State";
        const float ProbeCubeSize = 0.1f;

        [MenuItem("Anseong/Setup/Add Tracking Probes")]
        static void AddTrackingProbes()
        {
            var scene = SceneManager.GetActiveScene();
            var origins = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<XROrigin>(true))
                .ToArray();
            if (origins.Length == 0)
            {
                EditorUtility.DisplayDialog("Add Tracking Probes",
                    $"'{scene.name}' 씬에 XR Origin이 없습니다.\nHierarchy 우클릭 → XR → XR Origin (VR)을 먼저 추가하세요.", "확인");
                return;
            }
            if (origins.Length > 1)
                Debug.LogWarning($"[SceneSetupMenu] XR Origin이 {origins.Length}개입니다. 첫 번째({origins[0].name})에만 추가합니다.", origins[0]);

            var references = AssetDatabase.LoadAllAssetsAtPath(PilotInputPath).OfType<InputActionReference>().ToArray();
            if (references.Length == 0)
            {
                EditorUtility.DisplayDialog("Add Tracking Probes", $"입력 액션 에셋을 찾지 못했습니다: {PilotInputPath}", "확인");
                return;
            }

            var origin = origins[0];
            var parent = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;

            var created = 0;
            created += AddProbe(origin, parent, references, LeftHandMap, "Left Hand Probe") ? 1 : 0;
            created += AddProbe(origin, parent, references, RightHandMap, "Right Hand Probe") ? 1 : 0;

            if (created > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[SceneSetupMenu] 트래킹 프로브 {created}개 추가 (이미 있던 것은 건너뜀). 씬을 저장하세요.", origin);
        }

        static bool AddProbe(XROrigin origin, Transform parent, InputActionReference[] references, string mapName, string objectName)
        {
            var position = FindReference(references, mapName, PositionActionName);
            var rotation = FindReference(references, mapName, RotationActionName);
            var trackingState = FindReference(references, mapName, TrackingStateActionName);
            if (position == null || rotation == null || trackingState == null)
            {
                Debug.LogError($"[SceneSetupMenu] '{mapName}' 맵의 액션을 찾지 못했습니다: {PilotInputPath}");
                return false;
            }

            var existing = origin.GetComponentsInChildren<TrackedPoseProbe>(true)
                .FirstOrDefault(probe => probe.PositionAction == position);
            if (existing != null)
            {
                Debug.Log($"[SceneSetupMenu] '{mapName}' 프로브가 이미 있습니다: {existing.name}", existing);
                return false;
            }

            var anchor = new GameObject(objectName);
            anchor.transform.SetParent(parent, false);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Probe Cube";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(anchor.transform, false);
            cube.transform.localScale = Vector3.one * ProbeCubeSize;

            var probe = anchor.AddComponent<TrackedPoseProbe>();
            var serialized = new SerializedObject(probe);
            serialized.FindProperty("positionAction").objectReferenceValue = position;
            serialized.FindProperty("rotationAction").objectReferenceValue = rotation;
            serialized.FindProperty("trackingStateAction").objectReferenceValue = trackingState;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(anchor, "Add Tracking Probe");
            return true;
        }

        static InputActionReference FindReference(InputActionReference[] references, string mapName, string actionName)
        {
            return references.FirstOrDefault(r =>
                r.action != null && r.action.actionMap != null &&
                r.action.actionMap.name == mapName && r.action.name == actionName);
        }
    }
}
