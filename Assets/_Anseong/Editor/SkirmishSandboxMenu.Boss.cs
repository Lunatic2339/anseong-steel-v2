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
        const float BossSwordLength = 26f;

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
        public static void CaptureBossAct1()
        {
            try
            {
                var settings = SkirmishSettings.BossAct1();
                Build(settings, false, out var view, out var camera);
                Directory.CreateDirectory(Path.GetDirectoryName(BossAct1ScenePath));
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), BossAct1ScenePath)) throw new System.Exception("scene save failed");
                var session = new SkirmishSession(settings);
                session.SetReady(true, true);
                const string folder = "Logs/BossAct1Capture";
                Directory.CreateDirectory(folder);
                const int W = 1600, H = 1000;
                var target = new RenderTexture(W, H, 24) { antiAliasing = 4 };
                var image = new Texture2D(W, H, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                var aim = Vector3.forward;
                var localInput = Object.FindAnyObjectByType<SkirmishLocalInput>();
                var holoHud = Object.FindAnyObjectByType<MechHoloHud>();
                var skins = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                // 자폭 드론이 뜨는 12초까지는 막기만 한다(자동 조작이 다 하면 12초에 1막이 끝나 버린다).
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
                Vector3 MechEye() => session.State.mech + LastMechEye;
                Vector3 Center() => SkirmishSession.GruntCenter(session.State, settings);
                void Pilot(bool on)
                {
                    localInput.PlaceCockpit(MechEye(), on);
                    camera.transform.position = MechEye();
                    holoHud.Show(session.State, settings, session.Events, on, 0f);
                }
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
                    Shot(name + "-p1", MechEye(), Center() + Vector3.up * 6f, 90f);
                    Pilot(false);
                    Shot(name + "-p2", settings.gunnerPost, mech + new Vector3(0f, 22f, 11f), 90f);
                    Shot(name + "-side", mech + new Vector3(-70f, 24f, 11f), mech + new Vector3(0f, 19f, 11f), 60f);
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
                attack = true;
                Until(st => st.phase == SkirmishPhase.Cleared, "1막 끝");
                Three("6-act-end");

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
