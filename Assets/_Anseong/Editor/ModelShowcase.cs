using System.IO;
using System.Linq;
using Anseong.Pilot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 모델 점검용 사진(모델링 스레드). 플레이 없이 빈 씬에 우리 로봇·보스 프리팹을 세우고 무기 프리팹을 게임과 같은 방식으로
    /// (손 뼈에 회전 없이, 손가락 쪽으로 along만큼) 붙여 여러 각도에서 찍는다. 방향·크기·재질이 맞는지 보는 용도이고 전투 화면은 아니다.
    /// 결과: `Logs/ModelShowcase/*.png`. 명령줄 `-executeMethod Anseong.EditorTools.ModelShowcase.CaptureBatch`(-nographics 없이).
    /// </summary>
    public static class ModelShowcase
    {
        const string PrefabFolder = "Assets/_Anseong/Art/AI/Prefabs";
        const string OutDir = "Logs/ModelShowcase";
        const int Width = 1600, Height = 1000;

        [MenuItem("Anseong/Art/Model Showcase (사진 찍기)")]
        static void Menu()
        {
            Capture();
            Debug.Log("[ModelShowcase] 저장: " + OutDir);
        }

        public static void CaptureBatch()
        {
            AiModelFit.Apply(new GameObject[0]);
            Capture();
            EditorApplication.Exit(0);
        }

        static void Capture()
        {
            Directory.CreateDirectory(OutDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cameraObject = Object.FindAnyObjectByType<Camera>();
            var camera = cameraObject != null ? cameraObject : new GameObject("Camera").AddComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.farClipPlane = 3000f;
            camera.nearClipPlane = 0.3f;
            camera.fieldOfView = 40f;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            // 우리 로봇: 검·방패 / 포·스캐너
            var player = Spawn(RobotPrefabMenu.PlayerPrefabPath, Vector3.zero);
            if (player != null)
            {
                var left = Hand(player, PilotSide.Left);
                var right = Hand(player, PilotSide.Right);
                var set1 = new[] { InHand("Weapon_Shield", left, 0.2f), InHand("Weapon_Sword", right, 1.3f) };
                Shots(camera, target, readback, player, "player_sword_shield", left, right);
                foreach (var g in set1) if (g != null) Object.DestroyImmediate(g);
                InHand("Weapon_Scanner", left, 0.5f);
                InHand("Weapon_Cannon", right, 0.5f);
                Shots(camera, target, readback, player, "player_cannon_scanner", left, right);
                Object.DestroyImmediate(player);
            }

            // 보스: 팔뚝 포 둘 + 검 + 방패 + 핵 (BossWeaponVisuals.Attach와 같은 자리)
            var boss = Spawn(RobotPrefabMenu.BossPrefabPath, Vector3.zero);
            if (boss != null)
            {
                var root = boss.transform;
                var leftHand = RobotSandboxUtility.FindBone(root, RobotSandboxUtility.LeftPrefix + RobotSandboxUtility.HandSuffix);
                var rightHand = RobotSandboxUtility.FindBone(root, RobotSandboxUtility.RightPrefix + RobotSandboxUtility.HandSuffix);
                InHand("Weapon_Shield_Boss", leftHand, 0.2f);
                InHand("Weapon_Sword_Boss", rightHand, 1.3f);
                foreach (var hand in new[] { leftHand, rightHand })
                {
                    if (hand == null || hand.parent == null) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Boss_ArmCannon.prefab");
                    if (prefab == null) continue;
                    var bone = hand.parent;
                    var along = hand.position - bone.position;
                    var go = Object.Instantiate(prefab);
                    go.transform.SetPositionAndRotation(bone.position + along * 0.5f, Quaternion.FromToRotation(Vector3.up, along.normalized));
                    go.transform.SetParent(bone, true);
                    go.transform.localScale = prefab.transform.localScale;
                }
                var spine = RobotSandboxUtility.FindBone(root, RobotSandboxUtility.SpineBone);
                var head = RobotSandboxUtility.FindBone(root, RobotSandboxUtility.HeadBone);
                var core = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Boss_Core.prefab");
                if (spine != null && head != null && core != null)
                {
                    var size = Vector3.Distance(spine.position, head.position);
                    var go = Object.Instantiate(core);
                    go.transform.SetPositionAndRotation(spine.position + root.forward * size * 0.36f, root.rotation);
                    go.transform.SetParent(spine, true);
                }
                Shots(camera, target, readback, boss, "boss", leftHand, rightHand);
                Object.DestroyImmediate(boss);
            }
            target.Release();
        }

        static GameObject Spawn(string path, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[ModelShowcase] 프리팹 없음: " + path);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = position;
            return go;
        }

        static Transform Hand(GameObject robot, PilotSide side)
        {
            var arm = robot.GetComponentsInChildren<RobotArmDriver>(true).FirstOrDefault(a => a.Side == side && a.HandBone != null);
            if (arm != null) return arm.HandBone;
            return RobotSandboxUtility.FindBone(robot.transform, (side == PilotSide.Left ? RobotSandboxUtility.LeftPrefix : RobotSandboxUtility.RightPrefix) + RobotSandboxUtility.HandSuffix);
        }

        /// <summary>PlayerWeaponVisuals.Attach와 같은 식.</summary>
        static GameObject InHand(string name, Transform hand, float along)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
            if (prefab == null || hand == null)
            {
                Debug.LogWarning("[ModelShowcase] 없음: " + name);
                return null;
            }
            var unit = hand.parent != null ? Vector3.Distance(hand.position, hand.parent.position) : 1f;
            var go = Object.Instantiate(prefab);
            go.transform.SetParent(hand, false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.position = hand.position + hand.up * unit * along;
            return go;
        }

        static void Shots(Camera camera, RenderTexture target, Texture2D readback, GameObject robot, string name, Transform left, Transform right)
        {
            var renderers = robot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var t = robot.transform;
            var h = bounds.size.y;
            var center = bounds.center;
            // 온몸: 앞 왼쪽·앞 오른쪽·뒤
            Shot(camera, target, readback, center + (t.forward * 1.6f + t.right * 0.9f + Vector3.up * 0.15f) * h, center, name + "_front_a");
            Shot(camera, target, readback, center + (t.forward * 1.6f - t.right * 0.9f + Vector3.up * 0.15f) * h, center, name + "_front_b");
            Shot(camera, target, readback, center + (-t.forward * 1.5f + t.right * 0.8f + Vector3.up * 0.3f) * h, center, name + "_back");
            // 손 가까이(바깥쪽 앞에서)
            if (left != null) Shot(camera, target, readback, left.position + (t.forward * 0.45f - t.right * 0.5f + Vector3.up * 0.1f) * h, left.position - Vector3.up * 0.08f * h, name + "_left_hand");
            if (right != null) Shot(camera, target, readback, right.position + (t.forward * 0.45f + t.right * 0.5f + Vector3.up * 0.1f) * h, right.position - Vector3.up * 0.08f * h, name + "_right_hand");
            // 가슴 가까이
            Shot(camera, target, readback, center + (t.forward * 0.7f + Vector3.up * 0.25f) * h, center + Vector3.up * 0.18f * h, name + "_chest");
        }

        static void Shot(Camera camera, RenderTexture target, Texture2D readback, Vector3 from, Vector3 lookAt, string name)
        {
            camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(lookAt - from, Vector3.up));
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = previous;
            camera.targetTexture = null;
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), readback.EncodeToPNG());
        }
    }
}
