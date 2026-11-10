using System.Linq;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Net;
using Anseong.UI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M4-01: 판 흐름(<see cref="MatchFlow"/>)을 전투 서버 오브젝트에, 판 흐름 글자판(<see cref="MatchFlowPanel"/>)을 XR 카메라 아래에 붙인다.
    /// 'Add Network Session'(그래서 Rebuild·Build Main도)이 이것을 부른다. 여러 번 실행해도 된다. 씬 저장은 사람이 한다.
    /// </summary>
    public static class MatchFlowMenu
    {
        const string LogPrefix = "[MatchFlowMenu]";
        const string PanelName = "MatchFlowPanel";
        // 눈앞 위쪽(테스트 안내판은 아래쪽).
        static readonly Vector3 PanelOffset = new Vector3(0f, 0.16f, 0.9f);
        const int PanelFontSize = 64;
        const float PanelCharacterSize = 0.0024f;

        [MenuItem("Anseong/Setup/Add Match Flow", priority = 3)]
        static void AddToActiveScene()
        {
            var result = AddToScene(SceneManager.GetActiveScene());
            Debug.Log($"{LogPrefix} {result}. 씬을 저장하세요.");
        }

        /// <summary>판 흐름 + 글자판. 무엇을 했는지 한 줄로 돌려준다.</summary>
        public static string AddToScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var server = roots.SelectMany(r => r.GetComponentsInChildren<CombatServer>(true)).FirstOrDefault();
            if (server == null) return "전투 서버가 없어 판 흐름을 붙이지 않음(Add Network Session 먼저)";
            var flow = server.GetComponent<MatchFlow>();
            var made = flow == null;
            if (made) flow = Undo.AddComponent<MatchFlow>(server.gameObject);
            // M4-04: 드론(같은 NetworkObject).
            var drones = server.GetComponent<DroneDirector>();
            if (drones == null) drones = Undo.AddComponent<DroneDirector>(server.gameObject);
            var droneSerialized = new SerializedObject(drones);
            if (droneSerialized.FindProperty("shapeMaterial").objectReferenceValue == null)
                droneSerialized.FindProperty("shapeMaterial").objectReferenceValue = CombatSetup.GetOrCreateUnlit(WeaponShapePath, Color.white);
            droneSerialized.ApplyModifiedProperties();
            // M4-05: 기체 점검(같은 NetworkObject).
            var checkup = server.GetComponent<CheckupDirector>();
            if (checkup == null) checkup = Undo.AddComponent<CheckupDirector>(server.gameObject);
            var checkupSerialized = new SerializedObject(checkup);
            if (checkupSerialized.FindProperty("shapeMaterial").objectReferenceValue == null)
                checkupSerialized.FindProperty("shapeMaterial").objectReferenceValue = CombatSetup.GetOrCreateUnlit(WeaponShapePath, Color.white);
            checkupSerialized.ApplyModifiedProperties();
            var serialized = new SerializedObject(flow);
            serialized.FindProperty("combat").objectReferenceValue = server;
            serialized.ApplyModifiedProperties();

            var panel = AddPanel(roots, flow);
            var weapons = AddPlayerWeapons(roots);
            var hud = AddHud(roots);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"판 흐름({(made ? "새로 붙임" : "있던 것 다시 연결")}), 글자판: {panel}, 우리 무기: {weapons}, HUD: {hud}";
        }

        static readonly Vector3 HudOffset = new Vector3(-0.22f, -0.05f, 0.9f);

        /// <summary>M4-06: 조종사 HUD(글자, 한쪽에만 보이는 공격·드론 방향, 기체 HP). XR 카메라 왼쪽 아래.</summary>
        static string AddHud(GameObject[] roots)
        {
            var origin = roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            var camera = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            if (camera == null) return "XR 카메라가 없어 안 만듦";
            var hud = camera.GetComponentInChildren<PilotHud>(true);
            if (hud == null)
            {
                var text = MakeText(camera, "PilotHud", HudOffset, TextAnchor.UpperLeft, TextAlignment.Left);
                hud = text.gameObject.AddComponent<PilotHud>();
                var s = new SerializedObject(hud);
                s.FindProperty("text").objectReferenceValue = text;
                s.ApplyModifiedProperties();
            }
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("director").objectReferenceValue =
                roots.SelectMany(r => r.GetComponentsInChildren<CombatDirector>(true)).FirstOrDefault();
            serialized.ApplyModifiedProperties();
            return "XR 카메라 왼쪽 아래";
        }

        static TextMesh MakeText(Transform camera, string name, Vector3 offset, TextAnchor anchor, TextAlignment alignment)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add Match Flow");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one / Mathf.Max(1e-6f, camera.lossyScale.x);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = PanelFontSize;
            text.characterSize = PanelCharacterSize;
            text.anchor = anchor;
            text.alignment = alignment;
            text.richText = true;
            text.color = Color.white;
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }

        /// <summary>M4-03: 플레이어 로봇 루트에 무기 겉모습(방패·검·스캐너·포 도형).</summary>
        static string AddPlayerWeapons(GameObject[] roots)
        {
            var arm = roots.SelectMany(r => r.GetComponentsInChildren<Anseong.Pilot.RobotArmDriver>(true)).FirstOrDefault();
            if (arm == null) return "로봇 팔이 없어 안 붙임";
            var root = arm.transform.root.gameObject;
            var visuals = root.GetComponent<PlayerWeaponVisuals>();
            if (visuals == null) visuals = Undo.AddComponent<PlayerWeaponVisuals>(root);
            var s = new SerializedObject(visuals);
            if (s.FindProperty("shapeMaterial").objectReferenceValue == null)
                s.FindProperty("shapeMaterial").objectReferenceValue = CombatSetup.GetOrCreateUnlit(WeaponShapePath, Color.white);
            s.ApplyModifiedProperties();
            return "도형";
        }

        const string WeaponShapePath = "Assets/_Anseong/Art/Debug/BossWeaponShape.mat";

        static string AddPanel(GameObject[] roots, MatchFlow flow)
        {
            var origin = roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            var camera = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            if (camera == null) return "XR 카메라가 없어 만들지 않음";
            var panel = camera.GetComponentInChildren<MatchFlowPanel>(true);
            if (panel == null)
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var go = new GameObject(PanelName);
                Undo.RegisterCreatedObjectUndo(go, "Add Match Flow Panel");
                go.transform.SetParent(camera, false);
                go.transform.localPosition = PanelOffset;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one / Mathf.Max(1e-6f, camera.lossyScale.x);
                var text = go.AddComponent<TextMesh>();
                text.font = font;
                text.fontSize = PanelFontSize;
                text.characterSize = PanelCharacterSize;
                text.anchor = TextAnchor.LowerCenter;
                text.alignment = TextAlignment.Center;
                text.richText = true;
                text.color = Color.white;
                text.text = "판 흐름";
                var renderer = go.GetComponent<MeshRenderer>();
                if (font != null) renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                panel = go.AddComponent<MatchFlowPanel>();
                var s = new SerializedObject(panel);
                s.FindProperty("text").objectReferenceValue = text;
                s.ApplyModifiedProperties();
            }
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("flow").objectReferenceValue = flow;
            serialized.FindProperty("director").objectReferenceValue =
                roots.SelectMany(r => r.GetComponentsInChildren<CombatDirector>(true)).FirstOrDefault();
            serialized.ApplyModifiedProperties();
            return "XR 카메라 위쪽";
        }
    }
}
