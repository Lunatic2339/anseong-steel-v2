using System.Collections.Generic;
using System.IO;
using System.Linq;
using Anseong.Skirmish;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>보스전 1막 장면(기획안 2026-11-26). 회색 상자 배경에 보스 모델을 세운다. 맵 위에 놓는 것은 아직 없다.</summary>
    public static partial class SkirmishSandboxMenu
    {
        /// <summary>선우가 내려받은 메카 모델(OSCAR CREATIVO, CC BY-NC 4.0). 없으면 상자로 세운다.</summary>
        const string BossModel = "Assets/_Anseong/Art/BossMecha/BossMecha.fbx";
        public const string BossAct1ScenePath = "Assets/_Anseong/Scenes/Sandbox/Sandbox_BossAct1.unity";
        public const string MapBossAct1ScenePath = "Assets/_Anseong/Scenes/Sandbox/BossAct1_OnMap.unity";
        const float BossSwordLength = 26f;

        /// <summary>선우가 내려받은 헬기 모델(AH-55 Shoebill)을 Blender에서 합쳐 FBX로 바꾼 것. 없으면 상자 조종석을 그대로 쓴다.</summary>
        const string HeliModel = "Assets/_Anseong/Art/Heli/Heli_AH55.fbx";
        /// <summary>
        /// 헬기를 P2 자리에 놓을지. 2026-11-27 선우 확정: 높이 40에서 기수를 15° 숙인 채 떠 있게 한다(수평이면 계기판이 싸움을 가린다).
        /// 끄면 상자 조종석을 쓴다.
        /// </summary>
        public static bool UseHelicopter = true;

        /// <summary>
        /// P2 자리(사수의 머리가 원점)에 헬기를 놓는다. 모델의 gunner_eye가 원점에 오고 기수가 자리의 정면을 향한다.
        /// 포탑(GunTurret)은 조준을 따라 도는 포신 축에 붙이고, 로터 셋은 돌게 한다. 상자 조종석은 지운다.
        /// </summary>
        static bool Helicopter(Transform gunner, Transform barrelPivot)
        {
            if (!UseHelicopter) return false;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HeliModel);
            if (model == null) return false;
            var heli = (GameObject)PrefabUtility.InstantiatePrefab(model);
            heli.name = "Helicopter AH-55 (P2)";
            var all = heli.GetComponentsInChildren<Transform>(true);
            Transform Part(string name) => all.FirstOrDefault(t => t.name == name);
            var eye = Part("gunner_eye"); var rotor = Part("MainRotorLower");
            if (eye == null || rotor == null)
            {
                Debug.LogWarning($"{LogPrefix} 헬기 모델에 gunner_eye나 MainRotorLower가 없어 상자 조종석을 씁니다.");
                Object.DestroyImmediate(heli);
                return false;
            }
            UseBuiltInMaterials(heli);
            // 사수는 로터 축보다 앞에 앉는다. 그 방향을 기수로 삼아 자리의 정면에 맞춘다.
            var nose = eye.position - rotor.position;
            nose.y = 0f;
            heli.transform.rotation = Quaternion.FromToRotation(nose.normalized, gunner.forward) * heli.transform.rotation;
            heli.transform.position += gunner.position - eye.position;

            for (int i = gunner.childCount - 1; i >= 0; i--)
                if (gunner.GetChild(i) != barrelPivot) Object.DestroyImmediate(gunner.GetChild(i).gameObject);
            for (int i = barrelPivot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(barrelPivot.GetChild(i).gameObject);
            heli.transform.SetParent(gunner, true);

            var turret = Part("GunTurret");
            if (turret != null)
            {
                barrelPivot.SetPositionAndRotation(turret.position, gunner.rotation);
                turret.SetParent(barrelPivot, true);
            }
            foreach (var (name, axis, speed) in new[] { ("MainRotorLower", Vector3.up, 1400f), ("MainRotorUpper", Vector3.up, -1400f), ("Propeller", Vector3.forward, 2200f) })
            {
                var part = Part(name);
                if (part != null) part.gameObject.AddComponent<SpinPart>().Bind(gunner, axis, speed);
            }
            MeasureHeli(heli, gunner, eye);
            return true;
        }

        /// <summary>기본 렌더링에서 보스 바탕색에 곱하는 값(1 = 원래 색).</summary>
        const float BossBrightness = 1.4f;

        /// <summary>
        /// 두 로봇이 어둡게 나와서(선우 2026-11-27) 싸우는 자리에만 두는 흰 채움빛 둘. 맵은 건드리지 않는다.
        /// 하나는 로봇 머리 위에서 보스의 앞면을(P1이 보는 면), 하나는 헬기 쪽 위에서 두 로봇의 옆면을(P2가 보는 면) 비춘다.
        /// 스포트라이트라 바닥에는 발밑만 밝아진다.
        /// </summary>
        static void FightLights(SkirmishSettings settings)
        {
            var facing = Quaternion.Euler(0f, settings.mechYaw, 0f);
            var root = new GameObject("Fight lights (로봇 채움빛)").transform;
            void Fill(string name, Vector3 from, Vector3 to, float intensity, float angle)
            {
                var light = new GameObject(name).AddComponent<Light>();
                light.transform.SetParent(root, false);
                var at = settings.mechStart + facing * from;
                light.transform.SetPositionAndRotation(at, Quaternion.LookRotation(settings.mechStart + facing * to - at));
                light.type = LightType.Spot; light.spotAngle = angle; light.range = 120f;
                light.color = new Color(1f, 0.97f, 0.92f); light.intensity = intensity;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
            }
            Fill("Boss front fill", new Vector3(-6f, 52f, -8f), new Vector3(0f, 22f, 22f), 0.9f, 60f);
            Fill("Side fill", new Vector3(40f, 52f, 11f), new Vector3(0f, 20f, 11f), 0.8f, 70f);
        }

        /// <summary>헬기의 크기와 사수 눈 주변을 기록한다(확인용).</summary>
        static void MeasureHeli(GameObject heli, Transform gunner, Transform eye)
        {
            var bounds = new Bounds(gunner.position, Vector3.zero);
            int triangles = 0, renderers = 0, slots = 0;
            var names = new System.Collections.Generic.List<string>();
            foreach (var r in gunner.GetComponentsInChildren<MeshRenderer>(true))
            {
                bounds.Encapsulate(r.bounds);
                renderers++; slots += r.sharedMaterials.Length;
                var filter = r.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;
                if (r.name.StartsWith("Canopy")) names.Add(r.name + ": " + string.Join(", ", r.sharedMaterials.Select(m => m != null ? m.name : "없음")));
            }
            var local = gunner.InverseTransformPoint(bounds.center);
            Debug.Log($"{LogPrefix} HELI_FIT 크기 {bounds.size} / 사수 눈 기준 가운데 {local} / 삼각형 {triangles} / 렌더러 {renderers} / 재질 칸 {slots}");
            foreach (var n in names) Debug.Log($"{LogPrefix} HELI_CANOPY {n}");
        }

        /// <summary>우리 로봇의 오른손에 임시 검(날 길이 14)을 붙인다. 유진의 검 모델이 오면 대체한다. 팔 뼈가 없으면 아무것도 하지 않는다.</summary>
        static void RobotSword(Transform mech, SkirmishMechArms arms)
        {
            if (!ArmBones(mech, false, out var arm)) return;
            var along = arm.hand.TransformDirection(arm.fistForwardLocal).normalized;
            var steel = Lit("RobotBlade", new Color(0.7f, 0.74f, 0.78f));
            steel.SetFloat("_Metallic", 0.8f);
            var blade = Box("Robot sword (임시)", null, Vector3.zero, new Vector3(0.35f, 1.6f, 14f), steel);
            blade.SetPositionAndRotation(arm.hand.position + along * 7.5f, Quaternion.LookRotation(along, Vector3.up));
            blade.SetParent(arm.hand, true);
            arms.BindSword(blade.GetComponent<Renderer>());
        }

        static Transform BossBone(Transform[] all, string prefix) =>
            all.FirstOrDefault(t => t.name.StartsWith(prefix) && !t.name.Contains("scaleCompensation"));

        static bool BossArm(Transform[] all, string side, out BossPuppet.Arm arm)
        {
            arm = new BossPuppet.Arm
            {
                upper = BossBone(all, "arm_stretch_" + side), fore = BossBone(all, "forearm_stretch_" + side), hand = BossBone(all, "hand_" + side),
            };
            return arm.upper != null && arm.fore != null && arm.hand != null;
        }

        /// <summary>
        /// 보스 모델을 height(35m)로 맞춰 세우고, 임시 대검과 팔 자세를 잡는 <see cref="BossPuppet"/>을 붙인다.
        /// 모델이나 팔 뼈가 없으면 null(몸을 기울이는 표현만 남는다).
        /// </summary>
        static BossPuppet Boss(Transform root, float height, Material fallback)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BossModel);
            if (model == null)
            {
                Debug.LogWarning($"{LogPrefix} 보스 모델이 없어 상자로 세웁니다: {BossModel}");
                Box("Body proxy", root, new Vector3(0f, height * 0.5f, 0f), new Vector3(12f, height, 8f), fallback);
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root, false);
            // 맵 프로젝트는 기본 렌더링이라 재질을 그쪽 것으로 바꿔 끼운다(URP에서는 아무것도 하지 않는다).
            UseBuiltInMaterials(instance);
            // 보스의 바탕색이 짙어 기본 렌더링의 흐린 하늘 아래에서 검게 뭉개진다. 금속성을 낮추고 바탕색을 올린다.
            if (BuiltIn)
                foreach (var material in instance.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                {
                    material.SetFloat("_Metallic", 0.1f);
                    material.SetTexture("_MetallicGlossMap", null);
                    material.DisableKeyword("_METALLICGLOSSMAP");
                    material.SetFloat("_Glossiness", 0.35f);
                    material.color = Color.white * BossBrightness;
                    EditorUtility.SetDirty(material);
                }
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            if (MeasureHeight(instance, out float min, out float max))
            {
                float scale = height / (max - min);
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.localPosition = new Vector3(0f, -min * scale, 0f);
                Debug.Log($"{LogPrefix} BOSS_FIT 원래 높이 {max - min:F2} → {height} (배율 {scale:F3})");
            }

            var all = instance.GetComponentsInChildren<Transform>(true);
            // 대검은 보스의 오른손(로봇이 볼 때 왼쪽). 방패는 없다. 왼팔은 자세만 잡는다.
            if (!BossArm(all, "r", out var swordArm) || !BossArm(all, "l", out var shieldArm))
            {
                Debug.LogWarning($"{LogPrefix} 보스 팔 뼈를 찾지 못해 팔 자세는 잡지 않습니다.");
                return null;
            }
            var steel = Lit("BossBlade", new Color(0.55f, 0.57f, 0.6f));
            steel.SetFloat("_Metallic", 0.8f);
            steel.SetFloat("_Smoothness", 0.6f);
            var plate = Lit("BossHilt", new Color(0.16f, 0.2f, 0.14f));
            var edge = Unlit("BossEdge", new Color(0.6f, 1f, 0.3f));

            var sword = new GameObject("Boss sword (임시)").transform;
            sword.SetParent(root, false);
            Box("Blade", sword, new Vector3(0f, 0f, 1f), new Vector3(0.6f, 3.4f, BossSwordLength - 2f), steel);
            Box("Blade edge", sword, new Vector3(0f, 1.75f, 1f), new Vector3(0.2f, 0.25f, BossSwordLength - 2.5f), edge);
            Box("Crossguard", sword, new Vector3(0f, 0f, -BossSwordLength * 0.5f + 1.5f), new Vector3(1f, 5f, 0.8f), plate);

            var puppet = root.gameObject.AddComponent<BossPuppet>();
            puppet.Bind(swordArm, shieldArm, sword, null, BossSwordLength);
            puppet.Show(GruntStep.Idle, GruntAttack.Slam, 0f, new GruntSettings());
            return puppet;
        }

        [MenuItem("Anseong/Sandbox/Build Boss Act 1")]
        static void BuildBossAct1FromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(SkirmishSettings.BossAct1(), out _, out _);
            Directory.CreateDirectory(Path.GetDirectoryName(BossAct1ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), BossAct1ScenePath);
            Debug.Log($"{LogPrefix} 보스전 1막 저장: {BossAct1ScenePath}. Play 뒤 시작 화면에서 1(혼자) / 2(호스트 = 로봇) / 3(클라이언트 = 드론)을 고르세요.");
        }

        /// <summary>
        /// 배치 모드 확인용: 보스전 1막 장면을 만들어 저장하고, 자동 조작으로 돌리며 몇 순간을 P1 눈·P2 눈·옆에서 찍어 Logs/BossAct1Capture에 저장한다.
        /// -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.CaptureBossAct1
        /// </summary>
        public static void CaptureBossAct1() => CaptureBossAct1(false);

        /// <summary>같은 확인을 실제 맵 위 고로 앞에서 한다(맵 프로젝트에서만). -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.CaptureBossAct1OnMap</summary>
        public static void CaptureBossAct1OnMap() => CaptureBossAct1(true);

        [MenuItem("Anseong/Sandbox/Build Boss Act 1 On Map")]
        static void BuildBossAct1OnMapFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(SkirmishSettings.BossAct1OnMap(), true, out _, out _);
            Directory.CreateDirectory(Path.GetDirectoryName(MapBossAct1ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MapBossAct1ScenePath);
            Debug.Log($"{LogPrefix} 보스전 1막(맵 위) 저장: {MapBossAct1ScenePath}. Play를 누르면 맵 장면을 겹쳐 불러옵니다.");
        }

        /// <summary>
        /// 배치 모드 확인용: 보스전 1막(회색 상자) 장면을 만들어 저장하고 개발 빌드를 만든다. 한 PC에서 둘을 띄워 접속을 확인한다.
        /// 실행: AnseongBossAct1.exe -mode solo|host|client [-ip 127.0.0.1] -skirmishAuto 결과.json
        /// -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.BuildBossAct1Player
        /// </summary>
        public static void BuildBossAct1Player()
        {
            try
            {
                Build(SkirmishSettings.BossAct1(), false, out _, out _);
                Directory.CreateDirectory(Path.GetDirectoryName(BossAct1ScenePath));
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), BossAct1ScenePath)) throw new System.Exception("scene save failed");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { BossAct1ScenePath },
                    locationPathName = "Builds/BossAct1/AnseongBossAct1.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new System.Exception("build failed: " + report.summary.result);
                Debug.Log($"{LogPrefix} BOSS_ACT1_BUILD_OK {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime.TotalSeconds:0} s");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void CaptureBossAct1(bool onMap)
        {
            try
            {
                var settings = onMap ? SkirmishSettings.BossAct1OnMap() : SkirmishSettings.BossAct1();
                Build(settings, onMap, out var view, out var camera);
                string scenePath = onMap ? MapBossAct1ScenePath : BossAct1ScenePath;
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath)) throw new System.Exception("scene save failed");
                if (onMap) OpenMapForCapture(camera);
                var session = new SkirmishSession(settings);
                session.SetReady(true, true);
                string folder = onMap ? "Logs/BossAct1CaptureOnMap" : "Logs/BossAct1Capture";
                var facing = Quaternion.Euler(0f, settings.mechYaw, 0f);
                Directory.CreateDirectory(folder);
                const int W = 1600, H = 1000;
                var target = new RenderTexture(W, H, 24) { antiAliasing = 4 };
                var image = new Texture2D(W, H, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                var aim = Vector3.forward;
                var localInput = Object.FindAnyObjectByType<SkirmishLocalInput>();
                var holoHud = Object.FindAnyObjectByType<MechHoloHud>();
                var skins = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                // 자폭 드론이 뜨는 12초까지는 막기만 한다(자동 조작이 다 하면 12초에 1막이 지나가 버린다).
                bool attack = false;
                void Tick()
                {
                    var mech = SkirmishBots.Mech(session.State, settings);
                    if (!attack) mech.punchDamage = 0f;
                    if (SkirmishMath.Direction(ref mech.aim)) aim = mech.aim;
                    session.SetMech(mech);
                    session.SetGunner(attack ? SkirmishBots.Gunner(session.State, settings) : default);
                    session.Tick(0.02f);
                    view.Show(session, aim, 0f);
                }
                Vector3 MechEye() => session.State.mech + facing * LastMechEye;
                Vector3 Center() => SkirmishSession.GruntCenter(session.State, settings);
                void Pilot(bool on)
                {
                    localInput.PlaceCockpit(MechEye(), on);
                    camera.transform.position = MechEye();
                    if (holoHud != null) holoHud.Show(session.State, settings, session.Events, false, 0f);
                }
                var bossHud = Object.FindAnyObjectByType<BossFightHud>();
                // 화면 표시: 자리에 맞는 것을 띄우거나(P1, P2) 밖에서 찍을 때는 숨긴다.
                // 조준점과 약점 표시는 카메라가 보는 쪽을 기준으로 놓이므로, 찍을 방향으로 카메라를 먼저 돌린 뒤에 부른다.
                void Hud(SkirmishSeat seat, bool hide = false)
                {
                    bossHud.Aim = Center() - settings.gunnerPost;
                    bossHud.Show(session.State, settings, seat, false, hide, "", 0.02f);
                }
                void Look(Vector3 eye, Vector3 lookAt) => camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
                void Until(System.Func<SkirmishState, bool> reached, string what)
                {
                    for (int i = 0; i < 6000; i++) { Tick(); if (reached(session.State)) return; }
                    throw new System.Exception("도달하지 못함: " + what + " / 상태 " + session.State.gruntStep);
                }
                void Shot(string name, Vector3 eye, Vector3 lookAt, float fov = 70f)
                {
                    // 창 없는 Unity에서는 뼈를 돌려도 스킨이 다시 계산되지 않는다. 찍는 동안만 지금 자세를 굳은 메시로 바꿔 그린다.
                    var frozen = new List<GameObject>();
                    foreach (var skin in skins)
                    {
                        if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                        var mesh = new Mesh();
                        skin.BakeMesh(mesh, true);
                        var copy = new GameObject("Frozen");
                        copy.transform.SetParent(skin.transform, false);
                        copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = copy.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials = skin.sharedMaterials;
                        renderer.shadowCastingMode = skin.shadowCastingMode;
                        skin.enabled = false;
                        frozen.Add(copy);
                    }
                    camera.fieldOfView = fov;
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
                    camera.Render();
                    foreach (var copy in frozen)
                    {
                        copy.transform.parent.GetComponent<SkinnedMeshRenderer>().enabled = true;
                        Object.DestroyImmediate(copy.GetComponent<MeshFilter>().sharedMesh);
                        Object.DestroyImmediate(copy);
                    }
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    image.Apply();
                    File.WriteAllBytes($"{folder}/{name}.png", image.EncodeToPNG());
                }
                void Three(string name)
                {
                    var mech = session.State.mech;
                    Pilot(true);
                    Look(MechEye(), Center() + Vector3.up * 6f);
                    Hud(SkirmishSeat.Mech);
                    Shot(name + "-p1", MechEye(), Center() + Vector3.up * 6f, 90f);
                    Pilot(false);
                    Look(settings.gunnerPost, mech + facing * new Vector3(0f, 22f, 11f));
                    Hud(SkirmishSeat.Gunner);
                    Shot(name + "-p2", settings.gunnerPost, mech + facing * new Vector3(0f, 22f, 11f), 90f);
                    Hud(SkirmishSeat.Gunner, true);
                    Shot(name + "-side", mech + facing * new Vector3(-70f, 24f, 11f), mech + facing * new Vector3(0f, 19f, 11f), 60f);
                }

                Until(st => st.gruntStep == GruntStep.Idle, "보스가 섬");
                Three("1-stance");
                Until(st => st.gruntAttack == GruntAttack.Slam && st.gruntStep == GruntStep.Hold, "왼쪽 베기 예고");
                Three("2-sword-up");
                Until(st => st.gruntStep == GruntStep.Stagger, "튕겨남");
                for (int i = 0; i < 10; i++) Tick();
                Three("3-open");
                Until(st => st.gruntAttack == GruntAttack.Swing && st.gruntStep == GruntStep.Hold, "오른쪽 베기 예고");
                Three("4-sword-side");
                Until(st => st.shells.Any(x => x.kind == ShellKind.BigShell && Vector3.Distance(x.position, settings.gunnerPost) < 90f), "자폭 드론이 다가옴");
                Pilot(false);
                Shot("5-drone-p2", settings.gunnerPost, session.State.shells.First(x => x.kind == ShellKind.BigShell).position, 90f);
                // P2의 스캔: 버튼을 누른 채 보스를 겨누는 중(고리가 차오름)과 약점이 뜬 순간.
                void ScanTick(bool cancel)
                {
                    var mechInput = SkirmishBots.Mech(session.State, settings);
                    mechInput.punchDamage = 0f;
                    session.SetMech(mechInput);
                    var at = (Center() - settings.gunnerPost).normalized;
                    session.SetGunner(new GunnerInput { aim = at, pointer = at, lockHeld = true, cancel = cancel });
                    session.Tick(0.02f);
                    view.Show(session, aim, 0f);
                }
                for (int i = 0; i < 400 && session.State.lockProgress < 0.6f; i++) ScanTick(false);
                var fight = session.State.mech + facing * new Vector3(0f, 22f, 11f);
                Pilot(false);
                // 고리는 카메라가 보는 쪽을 기준으로 놓이므로 먼저 카메라를 돌려 둔다.
                camera.transform.SetPositionAndRotation(settings.gunnerPost, Quaternion.LookRotation(fight - settings.gunnerPost));
                Hud(SkirmishSeat.Gunner);
                Shot("5b-scan-p2", settings.gunnerPost, fight, 90f);
                // 약점 셋(등, 어깨, 옆구리)이 P2 눈에 보이는지 하나씩 찍는다. 취소하고 놓으면 쏘지 않고 다음 약점으로 넘어간다.
                string[] spots = { "back", "shoulder", "flank" };
                for (int spot = 0; spot < spots.Length; spot++)
                {
                    for (int i = 0; i < 400 && session.State.weakTimer <= 0f; i++) ScanTick(false);
                    Hud(SkirmishSeat.Gunner);
                    Shot($"5c-weak-point-{spot + 1}-{spots[spot]}-p2", settings.gunnerPost, fight, 90f);
                    ScanTick(true);
                    session.SetMech(default);
                    session.SetGunner(default);
                    session.Tick(0.02f);
                }
                attack = true;
                // 막 전환: 2막에 들어가 물러선 순간, 3막에 들어가며 검·방패를 부수는 일격, 맨손이 된 뒤, 싸움 끝.
                Until(st => st.act == 1, "2막 시작");
                for (int i = 0; i < 25; i++) Tick();
                Three("6a-act2-regroup");
                Until(st => st.act == 2 && st.gruntStep == GruntStep.Hold, "검과 방패를 부수는 일격의 예고");
                Three("6b-act3-breaker");
                Until(st => st.swordBroken, "검과 방패를 잃음");
                for (int i = 0; i < 10; i++) Tick();
                Three("6c-act3-bare");
                Until(st => st.phase == SkirmishPhase.Cleared, "싸움 끝");
                Three("6-act-end");
                // P2의 헬기를 밖에서 본 모습과, 사수 자리에서 정면·아래를 본 모습(헬기를 켰을 때만).
                if (UseHelicopter)
                {
                var seat = Quaternion.Euler(0f, settings.gunnerYaw, 0f);
                Shot("8-heli-outside", settings.gunnerPost + seat * new Vector3(16f, 4f, 12f), settings.gunnerPost + seat * new Vector3(0f, 0f, -4f), 60f);
                Shot("8-heli-seat-level", settings.gunnerPost, settings.gunnerPost + seat * Vector3.forward, 90f);
                Shot("8-heli-seat-down", settings.gunnerPost, settings.gunnerPost + seat * new Vector3(0f, -1f, 0.35f), 90f);
                Shot("8-heli-and-robot", session.State.mech + facing * new Vector3(90f, 45f, -60f), (session.State.mech + settings.gunnerPost) * 0.5f + Vector3.up * 5f, 60f);
                }
                if (onMap)
                {
                    // 자리 확인: P1이 고개를 왼쪽(고로 쪽)·오른쪽으로 돌렸을 때와 위에서 본 모습.
                    Pilot(true);
                    Shot("7-p1-look-left", MechEye(), MechEye() + facing * new Vector3(-1f, 0.1f, 0.5f), 90f);
                    Shot("7-p1-look-right", MechEye(), MechEye() + facing * new Vector3(1f, -0.1f, 0.5f), 90f);
                    Pilot(false);
                    Shot("7-top", session.State.mech + new Vector3(0f, 190f, -1f) + facing * new Vector3(0f, 0f, 11f), session.State.mech + facing * new Vector3(0f, 0f, 11f), 60f);
                }

                RenderTexture.active = null;
                camera.targetTexture = null;
                camera.fieldOfView = 70f;
                Debug.Log($"{LogPrefix} BOSS_ACT1_CAPTURE_OK elapsed={session.State.elapsed:0.0} hull={session.State.hull:0} bossHp={session.State.gruntHp:0}");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
