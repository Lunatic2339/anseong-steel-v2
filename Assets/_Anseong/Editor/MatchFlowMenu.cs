using System.Collections.Generic;
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
            // M4-07: 맵 연출(크레인 코일·컨베이어·사일로). 윤주 맵 자리는 Inspector 칸에 사람이 넣는다.
            var mapEvents = server.GetComponent<MapEvents>();
            if (mapEvents == null) mapEvents = Undo.AddComponent<MapEvents>(server.gameObject);
            var mapSerialized = new SerializedObject(mapEvents);
            if (mapSerialized.FindProperty("shapeMaterial").objectReferenceValue == null)
                mapSerialized.FindProperty("shapeMaterial").objectReferenceValue = CombatSetup.GetOrCreateUnlit(WeaponShapePath, Color.white);
            mapSerialized.ApplyModifiedProperties();
            var serialized = new SerializedObject(flow);
            serialized.FindProperty("combat").objectReferenceValue = server;
            serialized.ApplyModifiedProperties();

            var media = AddMedia(server, flow, checkup);
            var effects = AddEffects(roots, server);
            var panel = AddPanel(roots, flow);
            var weapons = AddPlayerWeapons(roots);
            var hud = AddHud(roots);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"판 흐름({(made ? "새로 붙임" : "있던 것 다시 연결")}), 글자판: {panel}, 우리 무기: {weapons}, HUD: {hud}, 연출: {effects}, 음악·영상: {media}";
        }

        const string MediaSetPath = "Assets/_Anseong/Audio/MatchMedia.asset";

        /// <summary>
        /// M4-08: 배경음악·영상 표(Audio/MatchMedia.asset)를 만들고, 13_ai_tools 이름표대로 프로젝트 안 파일을 찾아 빈 칸만 채운다.
        /// 조종석 앞 화면·음악 재생기(MatchMedia)를 붙이고 판 흐름에 표를 넣는다. 철판 표적 칸도 이름으로 찾아 채운다.
        /// 파일을 나중에 넣었으면 이 메뉴(또는 Build Full Game)를 다시 돌리면 된다.
        /// </summary>
        [MenuItem("Anseong/Setup/Add Match Media (BGM·Video)", priority = 4)]
        static void AddMediaToActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            Debug.Log($"{LogPrefix} {AddToScene(scene)}. 씬을 저장하세요.");
        }

        static string AddMedia(CombatServer server, MatchFlow flow, CheckupDirector checkup)
        {
            var set = AssetDatabase.LoadAssetAtPath<MatchMediaSet>(MediaSetPath);
            if (set == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Anseong/Audio")) AssetDatabase.CreateFolder("Assets/_Anseong", "Audio");
                set = ScriptableObject.CreateInstance<MatchMediaSet>();
                AssetDatabase.CreateAsset(set, MediaSetPath);
            }
            var filled = new List<string>();
            if (set.opening == null && (set.opening = FindByName<AudioClip>("BGM_Opening")) != null) filled.Add("BGM_Opening");
            if (set.battle == null && (set.battle = FindByName<AudioClip>("BGM_Battle")) != null) filled.Add("BGM_Battle");
            if (set.bare == null && (set.bare = FindByName<AudioClip>("BGM_Bare")) != null) filled.Add("BGM_Bare");
            if (set.ending == null && (set.ending = FindByName<AudioClip>("BGM_Ending")) != null) filled.Add("BGM_Ending");
            if (set.lobby == null && (set.lobby = FindByName<UnityEngine.Video.VideoClip>("Video_Lobby")) != null) filled.Add("Video_Lobby");
            if (set.openingVideo == null && (set.openingVideo = FindByName<UnityEngine.Video.VideoClip>("Video_Opening")) != null) filled.Add("Video_Opening");
            if (set.endingVideo == null && (set.endingVideo = FindByName<UnityEngine.Video.VideoClip>("Video_Ending")) != null) filled.Add("Video_Ending");
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            var player = server.GetComponent<MatchMedia>();
            if (player == null) player = Undo.AddComponent<MatchMedia>(server.gameObject);
            var ps = new SerializedObject(player);
            ps.FindProperty("media").objectReferenceValue = set;
            ps.FindProperty("flow").objectReferenceValue = flow;
            if (ps.FindProperty("screenMaterial").objectReferenceValue == null)
                ps.FindProperty("screenMaterial").objectReferenceValue = CombatSetup.GetOrCreateUnlit(ScreenMaterialPath, Color.white);
            ps.ApplyModifiedProperties();

            var fs = new SerializedObject(flow);
            fs.FindProperty("media").objectReferenceValue = set;
            fs.ApplyModifiedProperties();

            var plate = "도형";
            if (checkup != null)
            {
                var cs = new SerializedObject(checkup);
                var slot = cs.FindProperty("platePrefab");
                if (slot.objectReferenceValue == null) slot.objectReferenceValue = FindByName<GameObject>("Checkup_SteelPlate");
                if (slot.objectReferenceValue != null) plate = slot.objectReferenceValue.name;
                cs.ApplyModifiedProperties();
            }
            return $"{MediaSetPath}(새로 채운 칸: {(filled.Count > 0 ? string.Join(", ", filled) : "없음")}), 철판: {plate}";
        }

        const string ScreenMaterialPath = "Assets/_Anseong/Art/Debug/CockpitScreen.mat";

        /// <summary>프로젝트 안에서 이름이 정확히 같은 에셋을 하나 찾는다(Assets/_Anseong 아래 먼저).</summary>
        static T FindByName<T>(string name) where T : Object
        {
            var type = typeof(T) == typeof(GameObject) ? "Model Prefab" : typeof(T).Name;
            foreach (var filter in type.Split(' '))
            {
                var guids = AssetDatabase.FindAssets($"{name} t:{filter}", new[] { "Assets" });
                foreach (var path in guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p.StartsWith("Assets/_Anseong") ? 0 : 1))
                {
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;
                    var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null) return asset;
                }
            }
            return null;
        }

        const string EffectFolderParent = "Assets/_Anseong/Art";
        const string EffectFolderName = "VFX";
        const string EffectSetPath = EffectFolderParent + "/" + EffectFolderName + "/CombatEffects.asset";

        /// <summary>M5-01·03: 효과 표 에셋(유진 VFX 칸) + 효과 재생기(전투 서버 오브젝트, 모든 PC) + 보스 히트스톱.</summary>
        static string AddEffects(GameObject[] roots, CombatServer server)
        {
            if (!AssetDatabase.IsValidFolder(EffectFolderParent + "/" + EffectFolderName)) AssetDatabase.CreateFolder(EffectFolderParent, EffectFolderName);
            var set = AssetDatabase.LoadAssetAtPath<CombatEffectSet>(EffectSetPath);
            var added = 0;
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CombatEffectSet>();
                set.SetEntries(CombatEffectSet.DefaultEntries());
                AssetDatabase.CreateAsset(set, EffectSetPath);
                added = set.Entries.Length;
            }
            else
            {
                var missing = set.MissingDefaults();
                if (missing.Length > 0)
                {
                    set.SetEntries(set.Entries.Concat(missing).ToArray());
                    EditorUtility.SetDirty(set);
                    added = missing.Length;
                }
            }
            AssetDatabase.SaveAssets();

            var player = server.GetComponent<CombatEffectPlayer>();
            if (player == null) player = Undo.AddComponent<CombatEffectPlayer>(server.gameObject);
            var s = new SerializedObject(player);
            s.FindProperty("effects").objectReferenceValue = set;
            s.FindProperty("director").objectReferenceValue =
                roots.SelectMany(r => r.GetComponentsInChildren<CombatDirector>(true)).FirstOrDefault();
            if (s.FindProperty("shapeMaterial").objectReferenceValue == null)
                s.FindProperty("shapeMaterial").objectReferenceValue = CombatSetup.GetOrCreateUnlit(WeaponShapePath, Color.white);
            s.ApplyModifiedProperties();

            var boss = roots.SelectMany(r => r.GetComponentsInChildren<Anseong.Boss.BossBrain>(true)).FirstOrDefault();
            var hitstop = "보스 없음";
            if (boss != null)
            {
                if (boss.GetComponent<Anseong.Boss.BossHitstop>() == null) Undo.AddComponent<Anseong.Boss.BossHitstop>(boss.gameObject);
                hitstop = "보스 히트스톱";
            }
            return $"효과 표 {EffectSetPath}(새 줄 {added}개) + {hitstop}";
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
