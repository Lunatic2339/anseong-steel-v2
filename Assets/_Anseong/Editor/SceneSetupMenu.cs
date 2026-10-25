using System.Linq;
using Anseong.Combat;
using Anseong.Pilot;
using Anseong.UI;
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
        const string GripActionName = "Grip";
        const string TriggerActionName = "Trigger";
        const string StickActionName = "Stick";
        const string DevMapName = "Dev";
        const string RecalibrateActionName = "Recalibrate";
        const float ProbeCubeSize = 0.1f;
        // 테스트 안내판: 카메라 기준 위치(m)와 글자 크기. 한 줄 높이 ≈ fontSize × characterSize × 0.1 m.
        static readonly Vector3 PanelOffset = new Vector3(0f, -0.12f, 0.8f);
        const int PanelFontSize = 64;
        const float PanelCharacterSize = 0.0022f;
        // 조절 메뉴: 안내판 왼쪽 위, 시점 쪽으로 조금 돌림(카메라 로컬 m·도).
        static readonly Vector3 TuningOffset = new Vector3(-0.42f, 0.08f, 0.75f);
        const float TuningYawDegrees = -25f;

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

        [MenuItem("Anseong/Setup/Add Pilot Input")]
        static void AddPilotInput() => AddPilotInputToActiveScene();

        /// <summary>
        /// 코드를 받은 뒤 한 번에: Build Player Robot Prefab → Add Pilot Input(지금 열린 씬). 둘 중 하나가 실패하면 거기서 멈춘다.
        /// 열린 씬은 Sandbox 씬이어야 한다(Main은 선우만).
        /// </summary>
        [MenuItem("Anseong/Setup/Rebuild Robot + Pilot Input", priority = 0)]
        static void RebuildAll()
        {
            if (!RobotPrefabMenu.Build()) return;
            if (!AddPilotInputToActiveScene()) return;
            Debug.Log("[SceneSetupMenu] 프리팹 다시 만들기 + Add Pilot Input 완료. 씬을 저장하세요.");
        }

        internal static bool AddPilotInputToActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var origin = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<XROrigin>(true))
                .FirstOrDefault();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("Add Pilot Input",
                    $"'{scene.name}' 씬에 XR Origin이 없습니다.\nHierarchy 우클릭 → XR → XR Origin (VR)을 먼저 추가하세요.", "확인");
                return false;
            }
            var references = AssetDatabase.LoadAllAssetsAtPath(PilotInputPath).OfType<InputActionReference>().ToArray();
            if (references.Length == 0)
            {
                EditorUtility.DisplayDialog("Add Pilot Input", $"입력 액션 에셋을 찾지 못했습니다: {PilotInputPath}", "확인");
                return false;
            }

            var target = origin.gameObject;
            var input = target.GetComponent<PilotHandInput>();
            if (input == null) input = Undo.AddComponent<PilotHandInput>(target);
            var serializedInput = new SerializedObject(input);
            var missing = SetHandActions(serializedInput.FindProperty("leftHand"), references, LeftHandMap);
            missing += SetHandActions(serializedInput.FindProperty("rightHand"), references, RightHandMap);
            serializedInput.ApplyModifiedProperties();

            var flow = target.GetComponent<PilotCalibrationFlow>();
            if (flow == null) flow = Undo.AddComponent<PilotCalibrationFlow>(target);
            var serializedFlow = new SerializedObject(flow);
            serializedFlow.FindProperty("input").objectReferenceValue = input;
            serializedFlow.FindProperty("head").objectReferenceValue = origin.Camera != null ? origin.Camera.transform : null;
            serializedFlow.FindProperty("trackingSpace").objectReferenceValue =
                origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
            var recalibrate = FindReference(references, DevMapName, RecalibrateActionName);
            if (recalibrate == null) missing++;
            serializedFlow.FindProperty("recalibrateAction").objectReferenceValue = recalibrate;
            // 몸 이동 보정 첫 기본값(0.6초)이 씬에 남아 있으면 지금 기본값(0초, 즉시)으로 바꾼다. 일부러 다른 값으로 둔 것은 그대로.
            var followSeconds = serializedFlow.FindProperty("bodyFollow.followSeconds");
            if (followSeconds != null && Mathf.Approximately(followSeconds.floatValue, 0.6f))
            {
                followSeconds.floatValue = 0f;
                Debug.Log("[SceneSetupMenu] 몸 이동 보정 Follow Seconds 0.6 → 0(즉시)으로 바꿨습니다.", origin);
            }
            serializedFlow.ApplyModifiedProperties();

            var drivers = RobotArmSetup.ConnectDrivers(input, flow);
            var combats = CombatSetup.ConnectScene(input);
            var panel = AddTestGuidePanel(origin);
            var tuning = AddTuningMenu(origin);

            EditorSceneManager.MarkSceneDirty(scene);
            if (missing > 0)
                Debug.LogWarning($"[SceneSetupMenu] 찾지 못한 액션이 {missing}개 있습니다: {PilotInputPath}", origin);
            if (drivers == 0)
                Debug.LogWarning("[SceneSetupMenu] 씬에 RobotArmDriver가 없습니다. Robot_Player 프리팹을 다시 만들고(Build Player Robot Prefab) 이 메뉴를 다시 실행하세요.", origin);
            Debug.Log($"[SceneSetupMenu] XR Origin에 PilotHandInput·PilotCalibrationFlow를 연결하고 팔 드라이버 {drivers}개에 입력을 연결했습니다(기본 1인 양팔 모드). " +
                      $"전투 판정 {combats}개에 입력·보스 연결. 테스트 안내판: {panel}. 조절 메뉴(오른손 B): {tuning}. 씬을 저장하세요.", origin);
            return true;
        }

        /// <summary>XR 카메라 앞 아래쪽에 헤드셋에서 보는 테스트 안내판(보정 안내 + 펀치 결과, 개발용)을 만든다. 이미 있으면 그대로 둔다.</summary>
        static string AddTestGuidePanel(XROrigin origin)
        {
            var camera = origin.Camera != null ? origin.Camera.transform : null;
            if (camera == null) return "카메라가 없어 만들지 않음";
            if (camera.GetComponentInChildren<TestGuidePanel>(true) != null) return "이미 있음";

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("TestGuidePanel");
            Undo.RegisterCreatedObjectUndo(go, "Add Test Guide Panel");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = PanelOffset;
            go.transform.localRotation = Quaternion.identity;
            var scale = Mathf.Max(1e-6f, camera.lossyScale.x);
            go.transform.localScale = Vector3.one / scale;
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = PanelFontSize;
            text.characterSize = PanelCharacterSize;
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Left;
            text.richText = true;
            text.color = Color.white;
            text.text = "보정";
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var panel = go.AddComponent<TestGuidePanel>();
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("text").objectReferenceValue = text;
            serialized.FindProperty("calibration").objectReferenceValue = origin.GetComponent<PilotCalibrationFlow>();
            serialized.FindProperty("classifier").objectReferenceValue = Object.FindAnyObjectByType<PunchClassifierDriver>(FindObjectsInactive.Include);
            serialized.ApplyModifiedProperties();
            return "XR 카메라 아래에 만듦";
        }

        /// <summary>헤드셋을 쓴 채 값을 바꾸는 조절 메뉴(개발용). 처음엔 숨김, 오른손 B로 연다.</summary>
        static string AddTuningMenu(XROrigin origin)
        {
            var camera = origin.Camera != null ? origin.Camera.transform : null;
            if (camera == null) return "카메라가 없어 만들지 않음";
            if (camera.GetComponentInChildren<TuningMenu>(true) != null) return "이미 있음";

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("TuningMenu");
            Undo.RegisterCreatedObjectUndo(go, "Add Tuning Menu");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = TuningOffset;
            go.transform.localRotation = Quaternion.Euler(0f, TuningYawDegrees, 0f);
            var scale = Mathf.Max(1e-6f, camera.lossyScale.x);
            go.transform.localScale = Vector3.one / scale;
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = PanelFontSize;
            text.characterSize = PanelCharacterSize;
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Left;
            text.richText = true;
            text.color = Color.white;
            text.text = "조절 메뉴";
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var menu = go.AddComponent<TuningMenu>();
            var serialized = new SerializedObject(menu);
            serialized.FindProperty("text").objectReferenceValue = text;
            serialized.FindProperty("input").objectReferenceValue = origin.GetComponent<PilotHandInput>();
            serialized.ApplyModifiedProperties();
            return "XR 카메라 아래에 만듦";
        }

        static int SetHandActions(SerializedProperty hand, InputActionReference[] references, string mapName)
        {
            var missing = 0;
            void Set(string field, string actionName)
            {
                var reference = FindReference(references, mapName, actionName);
                if (reference == null) missing++;
                hand.FindPropertyRelative(field).objectReferenceValue = reference;
            }
            Set("position", PositionActionName);
            Set("rotation", RotationActionName);
            Set("trackingState", TrackingStateActionName);
            Set("grip", GripActionName);
            Set("trigger", TriggerActionName);
            Set("stick", StickActionName);
            return missing;
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
