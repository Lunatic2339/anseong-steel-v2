using System.Linq;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Pilot;
using Anseong.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 전투(M1-05) 구성: 플레이어 주먹 명중 구·전투 판정·소리 표, 보스 맞는 부위·휘청 컨트롤러·HP 막대.
    /// Build Player Robot Prefab(Rebuild Robot + Pilot Input)이 부른다. Mixamo 뼈 이름 탐색은 에디터에서만.
    /// </summary>
    static class CombatSetup
    {
        const string LogPrefix = "[CombatSetup]";
        public const string BossControllerPath = "Assets/_Anseong/Art/Robot/Animation/Robot_Boss.controller";
        const string AudioFolderParent = "Assets/_Anseong";
        const string AudioFolderName = "Audio";
        const string AudioFolder = AudioFolderParent + "/" + AudioFolderName;
        public const string CueSetPath = AudioFolder + "/CombatCues.asset";
        const string DebugFolder = "Assets/_Anseong/Art/Debug";
        const string BarBackPath = DebugFolder + "/BossHealthBar_Back.mat";
        const string BarFillPath = DebugFolder + "/BossHealthBar_Fill.mat";
        const string GuardLightPath = DebugFolder + "/GuardLight.mat";
        // 가드 표시등(조종석 로컬 m): 콘솔 윗면 앞쪽 가운데, 좌우로 하나씩.
        static readonly Vector3 GuardLightSize = new Vector3(0.22f, 0.02f, 0.12f);
        const float GuardLightSpacing = 0.3f;
        const string IdleState = "Idle";
        const string StaggerState = "Stagger";
        const string BodyBone = "mixamorig:Spine2";
        // 플레이어 루트 → 보스 루트 근접 거리(m, 제안값): 로봇 팔 19.4 m × 0.95 + 보스 몸통 반지름 7.8 m. M2에서 확정(05 "근접 거리").
        const float MeleeDistance = 26f;

        // 로봇 키(65 m) 대비 크기.
        const float FistRadiusRatio = 0.06f;        // 로봇 팔 길이 대비 주먹 구 반지름
        const float BodyRadiusRatio = 0.12f;        // 몸통 캡슐 반지름 / 키
        const float BodyHeightRatio = 0.34f;        // 몸통 캡슐 길이 / 키
        const float HeadRadiusRatio = 0.06f;        // 머리 구 반지름 / 키
        const float BarHeightAboveHeadRatio = 0.12f;
        const float BarWidthRatio = 0.35f;
        const float BarThicknessRatio = 0.025f;

        // ── 플레이어 ──

        /// <summary>Arms 오브젝트에 주먹 명중 구, 전투 판정, 소리·진동 재생기를 붙인다.</summary>
        public static string AddPlayerCombat(GameObject arms)
        {
            var drivers = arms.GetComponents<RobotArmDriver>();
            var fists = new FistHitProbe[2];
            foreach (var driver in drivers)
            {
                var serializedArm = new SerializedObject(driver);
                var hand = (Transform)serializedArm.FindProperty("hand").objectReferenceValue;
                var upper = (Transform)serializedArm.FindProperty("upperArm").objectReferenceValue;
                var fore = (Transform)serializedArm.FindProperty("foreArm").objectReferenceValue;
                if (hand == null || upper == null || fore == null) continue;
                var prefix = driver.Side == PilotSide.Left ? RobotSandboxUtility.LeftPrefix : RobotSandboxUtility.RightPrefix;
                var knuckle = hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == prefix + "HandMiddle1");
                var length = Vector3.Distance(upper.position, fore.position) + Vector3.Distance(fore.position, hand.position);

                var go = new GameObject($"FistHitProbe_{driver.Side}");
                go.transform.SetParent(hand, false);
                go.transform.position = knuckle != null ? knuckle.position : hand.position;
                var probe = go.AddComponent<FistHitProbe>();
                var serializedProbe = new SerializedObject(probe);
                serializedProbe.FindProperty("side").enumValueIndex = (int)driver.Side;
                serializedProbe.FindProperty("radius").floatValue = length * FistRadiusRatio;
                serializedProbe.ApplyModifiedPropertiesWithoutUndo();
                fists[(int)driver.Side] = probe;
            }

            var director = arms.AddComponent<CombatDirector>();
            var serialized = new SerializedObject(director);
            serialized.FindProperty("classifier").objectReferenceValue = arms.GetComponent<PunchClassifierDriver>();
            SetArray(serialized.FindProperty("arms"), drivers.Cast<Object>().ToArray());
            SetArray(serialized.FindProperty("fists"), fists.Where(f => f != null).Cast<Object>().ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var player = arms.AddComponent<CombatCuePlayer>();
            var serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("director").objectReferenceValue = director;
            serializedPlayer.FindProperty("cues").objectReferenceValue = GetOrCreateCueSet(out var filled);
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
            return $"전투 판정 + 주먹 명중 구 {fists.Count(f => f != null)}개 + 소리 표(채운 소리 {filled}개) + {AddPlayerGuard(arms, drivers)}";
        }

        /// <summary>가드 판정(M1-06)을 Arms에, 표시등 두 개를 조종석 콘솔 위에 붙인다.</summary>
        static string AddPlayerGuard(GameObject arms, RobotArmDriver[] drivers)
        {
            var guard = arms.AddComponent<GuardDriver>();
            var serialized = new SerializedObject(guard);
            SetArray(serialized.FindProperty("arms"), drivers.Cast<Object>().ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var seat = arms.transform.root.GetComponentInChildren<CockpitSeat>(true);
            if (seat == null)
            {
                Debug.LogWarning($"{LogPrefix} 조종석(CockpitSeat)을 찾지 못해 가드 표시등을 만들지 않습니다.");
                return "가드 판정(표시등 없음)";
            }
            var root = new GameObject("GuardIndicator");
            root.transform.SetParent(seat.transform, false);
            root.transform.localPosition = RobotSandboxUtility.ConsoleTopFront + Vector3.up * (GuardLightSize.y * 0.5f);
            var material = GetOrCreateUnlit(GuardLightPath, Color.white);
            var lights = new Object[2];
            for (var h = 0; h < 2; h++)
            {
                var light = GameObject.CreatePrimitive(PrimitiveType.Cube);
                light.name = h == 0 ? "Light_Left" : "Light_Right";
                Object.DestroyImmediate(light.GetComponent<Collider>());
                light.transform.SetParent(root.transform, false);
                light.transform.localScale = GuardLightSize;
                light.transform.localPosition = Vector3.right * GuardLightSpacing * (h == 0 ? -0.5f : 0.5f);
                var renderer = light.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                lights[h] = renderer;
            }
            var indicator = root.AddComponent<GuardIndicator>();
            var serializedIndicator = new SerializedObject(indicator);
            serializedIndicator.FindProperty("guard").objectReferenceValue = guard;
            SetArray(serializedIndicator.FindProperty("lights"), lights);
            serializedIndicator.ApplyModifiedPropertiesWithoutUndo();
            return "가드 판정 + 표시등 2개";
        }

        /// <summary>씬의 전투 판정에 입력과 보스를 연결한다(Add Pilot Input). 연결한 개수.</summary>
        public static int ConnectScene(PilotHandInput input, PilotCalibrationFlow flow)
        {
            foreach (var guard in Object.FindObjectsByType<GuardDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serializedGuard = new SerializedObject(guard);
                serializedGuard.FindProperty("input").objectReferenceValue = input;
                serializedGuard.FindProperty("calibration").objectReferenceValue = flow;
                serializedGuard.ApplyModifiedProperties();
            }
            var boss = Object.FindAnyObjectByType<BossTarget>(FindObjectsInactive.Include);
            var directors = Object.FindObjectsByType<CombatDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var director in directors)
            {
                var serialized = new SerializedObject(director);
                serialized.FindProperty("input").objectReferenceValue = input;
                serialized.FindProperty("boss").objectReferenceValue = boss;
                serialized.ApplyModifiedProperties();
            }
            if (directors.Length > 0 && boss == null)
                Debug.LogWarning($"{LogPrefix} 씬에 보스(BossTarget)가 없습니다. Robot_Boss 프리팹을 씬에 두면 명중 판정이 됩니다.");
            if (boss != null && directors.Length > 0) PullBossToMelee(boss, directors[0]);
            return directors.Length;
        }

        /// <summary>
        /// Sandbox 씬에서만: 보스가 근접 거리보다 멀면 플레이어 쪽으로 당겨 둔다(M1에는 접근 이동이 없음, M2에서 걸어서 다가감).
        /// 보는 거리 40 m(04)에서는 팔 길이 19 m로 닿지 않아 공격 보정 범위 밖이다.
        /// </summary>
        static void PullBossToMelee(BossTarget boss, CombatDirector director)
        {
            var scene = boss.gameObject.scene;
            if (!scene.path.Replace('\\', '/').Contains("/Scenes/Sandbox/")) return;
            var player = director.transform.root;
            var offset = boss.transform.position - player.position;
            offset.y = 0f;
            if (offset.magnitude <= MeleeDistance + 0.5f || offset.sqrMagnitude < 1e-4f) return;
            Undo.RecordObject(boss.transform, "Pull Boss To Melee");
            var from = offset.magnitude;
            boss.transform.position = player.position + offset.normalized * MeleeDistance + Vector3.up * (boss.transform.position.y - player.position.y);
            Debug.Log($"{LogPrefix} Sandbox 보스를 근접 거리로 옮겼습니다: {from:F1} m → {MeleeDistance:F1} m(제안값, M2에서 팔 길이로 확정).", boss);
        }

        /// <summary>소리 표 에셋. 없으면 S-01 기본 줄로 만들고, 비어 있는 소리 칸은 Audio 폴더에서 같은 이름으로 시작하는 파일로 채운다.</summary>
        static CombatCueSet GetOrCreateCueSet(out int filled)
        {
            filled = 0;
            if (!AssetDatabase.IsValidFolder(AudioFolder)) AssetDatabase.CreateFolder(AudioFolderParent, AudioFolderName);
            var set = AssetDatabase.LoadAssetAtPath<CombatCueSet>(CueSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CombatCueSet>();
                set.SetEntries(CombatCueSet.DefaultEntries());
                AssetDatabase.CreateAsset(set, CueSetPath);
                Debug.Log($"{LogPrefix} 소리 표 생성: {CueSetPath}. 소리 파일을 {AudioFolder}에 넣고 메뉴를 다시 돌리면 이름으로 채운다.", set);
            }
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToArray();
            foreach (var entry in set.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.soundName) || (entry.clips != null && entry.clips.Any(c => c != null))) continue;
                var found = clips.Where(c => c.name == entry.soundName || c.name.StartsWith(entry.soundName + "_")).OrderBy(c => c.name).ToArray();
                if (found.Length == 0) continue;
                entry.clips = found;
                filled++;
            }
            if (filled > 0)
            {
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
            }
            return set;
        }

        static void SetArray(SerializedProperty list, Object[] values)
        {
            list.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ── 보스 ──

        /// <summary>보스 컨트롤러(Idle + Stagger). 매번 새로 만든다(상태는 코드가 고른다, 전환 없음). 휘청 클립이 없으면 Idle만.</summary>
        public static AnimatorController BuildBossController(AnimationClip idleClip, AnimationClip staggerClip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BossControllerPath) != null) AssetDatabase.DeleteAsset(BossControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(BossControllerPath);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState(IdleState);
            idle.motion = idleClip;
            machine.defaultState = idle;
            if (staggerClip != null)
            {
                var stagger = machine.AddState(StaggerState);
                stagger.motion = staggerClip;
            }
            else Debug.LogWarning($"{LogPrefix} 휘청 클립이 없어 보스가 맞아도 휘청하지 않습니다. Robot Art Settings의 보스 휘청 클립이나 {RobotArtSettings.DefaultStaggerPath}를 확인하세요.");
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>보스 루트에 맞는 부위(몸통·머리), 맞았을 때 반응, HP 막대를 붙인다.</summary>
        public static string AddBossCombat(Transform boss, AnimationClip staggerClip)
        {
            var height = RobotSandboxUtility.RobotHeight;
            var target = boss.gameObject.AddComponent<BossTarget>();
            var body = RobotSandboxUtility.FindBone(boss, BodyBone) ?? RobotSandboxUtility.FindBone(boss, RobotSandboxUtility.SpineBone);
            var head = RobotSandboxUtility.FindBone(boss, RobotSandboxUtility.HeadBone);
            var zones = new System.Collections.Generic.List<BossHurtZone>();
            if (body != null)
            {
                var go = ZoneObject("HurtZone_Body", body);
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.radius = height * BodyRadiusRatio;
                capsule.height = height * BodyHeightRatio;
                // 몸통 뼈에서 아래로 조금(배까지 덮게).
                capsule.center = Vector3.down * height * 0.03f;
                zones.Add(Zone(go, BossZone.Body));
            }
            if (head != null)
            {
                var go = ZoneObject("HurtZone_Head", head);
                var sphere = go.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = height * HeadRadiusRatio;
                sphere.center = Vector3.up * height * 0.03f;
                zones.Add(Zone(go, BossZone.Head));
            }
            var serializedTarget = new SerializedObject(target);
            SetArray(serializedTarget.FindProperty("zones"), zones.Cast<Object>().ToArray());
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();

            var reaction = boss.gameObject.AddComponent<BossReactionDriver>();
            var serializedReaction = new SerializedObject(reaction);
            serializedReaction.FindProperty("animator").objectReferenceValue = boss.GetComponentInChildren<Animator>();
            serializedReaction.FindProperty("idleState").stringValue = IdleState;
            serializedReaction.FindProperty("staggerState").stringValue = StaggerState;
            if (staggerClip != null) serializedReaction.FindProperty("boostStaggerSeconds").floatValue = staggerClip.length;
            serializedReaction.ApplyModifiedPropertiesWithoutUndo();

            AddHealthBar(boss, head, height);
            return $"보스 맞는 부위 {zones.Count}개(몸통·머리) + 휘청 {(staggerClip != null ? staggerClip.length.ToString("F1") + "초" : "없음")} + HP 막대";
        }

        static GameObject ZoneObject(string name, Transform bone)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bone, false);
            // 뼈 배율과 무관하게 콜라이더 크기를 m로 쓰려고 배율을 되돌린다(균일 배율 가정).
            var scale = Mathf.Max(1e-6f, bone.lossyScale.x);
            go.transform.localScale = Vector3.one / scale;
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        static BossHurtZone Zone(GameObject go, BossZone zone)
        {
            var hurt = go.AddComponent<BossHurtZone>();
            var serialized = new SerializedObject(hurt);
            serialized.FindProperty("zone").enumValueIndex = (int)zone;
            serialized.FindProperty("surface").objectReferenceValue = go.GetComponent<Collider>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return hurt;
        }

        static void AddHealthBar(Transform boss, Transform head, float height)
        {
            var root = new GameObject("BossHealthBar");
            root.transform.SetParent(boss, false);
            var top = head != null ? boss.InverseTransformPoint(head.position).y : height * 0.9f;
            root.transform.localPosition = Vector3.up * (top + height * BarHeightAboveHeadRatio);
            var width = height * BarWidthRatio;
            var thickness = height * BarThicknessRatio;
            var back = Quad("Back", root.transform, new Vector3(width, thickness, 1f), 0f, GetOrCreateUnlit(BarBackPath, new Color(0.1f, 0.1f, 0.1f)));
            var fill = Quad("Fill", root.transform, new Vector3(width, thickness * 0.7f, 1f), -0.05f, GetOrCreateUnlit(BarFillPath, new Color(0.9f, 0.2f, 0.15f)));
            var bar = root.AddComponent<BossHealthBar>();
            var serialized = new SerializedObject(bar);
            serialized.FindProperty("fill").objectReferenceValue = fill;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            back.SetAsFirstSibling();
        }

        static Transform Quad(string name, Transform parent, Vector3 scale, float z, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localScale = scale;
            // 채움을 바탕보다 시점 쪽(−Z)으로 조금 앞에.
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        static Material GetOrCreateUnlit(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            // 에디터에서만 셰이더를 찾는다(런타임 Shader.Find 금지).
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder(DebugFolder)) AssetDatabase.CreateFolder("Assets/_Anseong/Art", "Debug");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
