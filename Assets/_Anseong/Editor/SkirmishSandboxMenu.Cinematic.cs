using System.Collections.Generic;
using System.IO;
using System.Linq;
using Anseong.Pilot;
using Anseong.Skirmish;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 등장 영상(힉스필드)에 넣을 기준 사진을 찍는다. 무기 도형은 빼고 찍는다(대검과 검 모델이 아직 없다).
    /// 사진은 레포 밖 anseong-steel-next/cinematic-ref/ 아래에 저장한다.
    /// </summary>
    public static partial class SkirmishSandboxMenu
    {
        static string CinematicFolder(string name)
        {
            // 맵 프로젝트(anseong-steel-next/Game)에서는 한 칸 위, v2에서는 옆 폴더.
            string root = Directory.Exists("../cinematic-ref") ? "../cinematic-ref" : "../anseong-steel-next/cinematic-ref";
            string path = Path.GetFullPath(Path.Combine(root, name));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>창 없는 Unity에서는 스킨이 다시 계산되지 않으므로, 지금 자세를 굳은 메시로 바꿔 한 장 찍는다.</summary>
        static void CinematicShot(Camera camera, string path, int width, int height, Vector3 eye, Vector3 lookAt, float fov)
        {
            var frozen = new List<GameObject>();
            foreach (var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
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
            var target = new RenderTexture(width, height, 24) { antiAliasing = 8 };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.fieldOfView = fov;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            foreach (var copy in frozen)
            {
                copy.transform.parent.GetComponent<SkinnedMeshRenderer>().enabled = true;
                Object.DestroyImmediate(copy.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(copy);
            }
        }

        /// <summary>로봇의 방패를 다 편 상태로 만든다(전달본의 ShieldController를 이름으로 찾아 부른다).</summary>
        static void OpenShield(Transform mech)
        {
            var shield = mech.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c => c != null && c.GetType().Name == "ShieldController");
            if (shield == null) return;
            var type = shield.GetType();
            type.GetMethod("SetDeployed")?.Invoke(shield, new object[] { true });
            type.GetMethod("Advance")?.Invoke(shield, new object[] { 10f });
        }

        static void RemoveTemporaryWeapons()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && (t.name == "Robot sword (임시)" || t.name == "Boss sword (임시)")) Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>
        /// 우리 로봇 기준 사진: 회색 단색 배경, 방패를 편 모습. 세로 네 장(정면, 앞 비스듬히, 옆, 뒤)과 올려다본 가로 두 장.
        /// -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.CaptureRobotReference
        /// </summary>
        public static void CaptureRobotReference()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.58f);
                var key = new GameObject("Key light").AddComponent<Light>();
                key.type = LightType.Directional; key.intensity = 1.4f; key.shadows = LightShadows.Soft;
                key.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
                var fill = new GameObject("Fill light").AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = 0.5f; fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(20f, -40f, 0f);

                var mech = new GameObject("Mech 35m").transform;
                var arms = Robot(mech, out _);
                if (arms == null) throw new System.Exception("로봇 프리팹을 찾지 못했습니다: " + RobotPrefab);
                arms.SetPose(false, PilotSide.Left, false);
                arms.SetShield(true, false);
                arms.Apply(0f, true);
                OpenShield(mech);

                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                camera.nearClipPlane = 0.5f; camera.farClipPlane = 600f;
                string folder = CinematicFolder("로봇");
                // 로봇은 +Z를 본다. 정면 사진은 +Z 쪽에서 찍는다.
                var middle = Vector3.up * (RobotHeight * 0.5f);
                const float far = 82f;
                (string name, float yaw)[] turns = { ("로봇-1-정면", 0f), ("로봇-2-앞-비스듬히", 35f), ("로봇-3-옆", 90f), ("로봇-4-뒤", 180f) };
                foreach (var (name, yaw) in turns)
                    CinematicShot(camera, $"{folder}/{name}.png", 1536, 2048, middle + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * far, middle, 28f);
                CinematicShot(camera, $"{folder}/로봇-5-올려다봄-왼쪽.png", 1920, 1080, Quaternion.Euler(0f, 30f, 0f) * Vector3.forward * 24f + Vector3.up * 2f, Vector3.up * 19.5f, 64f);
                CinematicShot(camera, $"{folder}/로봇-6-올려다봄-오른쪽.png", 1920, 1080, Quaternion.Euler(0f, -40f, 0f) * Vector3.forward * 27f + Vector3.up * 2f, Vector3.up * 19f, 60f);
                Debug.Log($"{LogPrefix} ROBOT_REFERENCE_OK {folder}");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>보스 모델에 사람형 동작 한 장면을 입힌다(발은 바닥에, 자리는 그대로). 동작 파일이 없으면 false.</summary>
        static bool PoseBoss(Transform bossRoot, string clipFile, float normalizedTime)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(clipFile).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            var animator = bossRoot.GetComponentInChildren<Animator>();
            if (clip == null || animator == null) return false;
            var puppet = bossRoot.GetComponent<BossPuppet>();
            if (puppet != null) Object.DestroyImmediate(puppet);
            var model = animator.transform;
            var position = model.localPosition; var rotation = model.localRotation;
            animator.applyRootMotion = false;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * normalizedTime);
            AnimationMode.EndSampling();
            // 동작에 든 이동은 버리고 서 있던 자리로 되돌린 뒤, 발을 바닥에 맞춘다.
            model.localPosition = position; model.localRotation = rotation;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                var drift = hips.position - bossRoot.position;
                drift.y = 0f;
                model.position -= drift;
            }
            if (MeasureHeight(model.gameObject, out float min, out _)) model.position += Vector3.up * (bossRoot.position.y - min);
            return true;
        }

        /// <summary>
        /// 맵 위 장면 사진 세 장(가로 16:9, 무기 없이): 벽돌 건물 뒤에서 걸어 나오는 보스, 고로 앞에서 마주 선 둘, 로봇 눈높이에서 본 보스.
        /// 맵 프로젝트에서만 된다. -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.CaptureCinematicScenes
        /// </summary>
        public static void CaptureCinematicScenes()
        {
            try
            {
                bool heli = UseHelicopter;
                UseHelicopter = false;
                var settings = SkirmishSettings.BossAct1OnMap();
                Build(settings, true, out var view, out var camera);
                UseHelicopter = heli;
                OpenMapForCapture(camera);
                RemoveTemporaryWeapons();
                var session = new SkirmishSession(settings);
                session.SetReady(true, true);
                for (int i = 0; i < 10; i++) { session.SetMech(default); session.SetGunner(default); session.Tick(0.02f); }
                view.TelegraphGlow = false;
                view.Show(session, Vector3.right, 0f);

                // 조종석, 계기, P2 자리, 손 표시는 사진에 넣지 않는다.
                var input = Object.FindAnyObjectByType<SkirmishLocalInput>();
                input.PlaceCockpit(Vector3.zero, false);
                if (view.GunnerRig != null) view.GunnerRig.gameObject.SetActive(false);
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
                foreach (var line in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) line.enabled = false;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t != null && (t.name.EndsWith("hand marker") || t.name == "Charger telegraph glow" || t.name == "Charger guard plate")) t.gameObject.SetActive(false);

                var mech = view.Mech;
                OpenShield(mech);
                var boss = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Boss (보스)");
                const string Motions = "Assets/_Anseong/Art/BossMecha/GreatSword/";
                string folder = CinematicFolder("장면");
                camera.farClipPlane = 9000f;
                var facing = Quaternion.Euler(0f, settings.mechYaw, 0f);
                var robotAt = settings.mechStart; var bossAt = settings.grunts[0].start;

                // 1. 벽돌 건물(동서 265~323, 남북 630~690) 모퉁이를 돌아 도로로 나오는 보스. 도로 위 로봇 쪽에서 본다.
                // 기본 렌더링에서는 금속성이 높은 재질이 흐린 하늘 아래에서 거의 검게 나온다. 사진용으로 낮춘다.
                if (BuiltIn)
                    foreach (var material in boss.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                    {
                        material.SetFloat("_Metallic", 0.1f);
                        material.SetTexture("_MetallicGlossMap", null);
                        material.DisableKeyword("_METALLICGLOSSMAP");
                        material.SetFloat("_Glossiness", 0.35f);
                    }
                var corner = new Vector3(285f, 0f, 618f);
                boss.SetPositionAndRotation(corner, Quaternion.LookRotation(new Vector3(200f, 0f, 632f) - corner));
                bool walk = PoseBoss(boss, Motions + "great sword walk.fbx", 0.3f);
                mech.gameObject.SetActive(false);
                CinematicShot(camera, $"{folder}/장면-1-건물-뒤에서-나오는-보스.png", 1920, 1080, new Vector3(190f, 10f, 668f), new Vector3(280f, 20f, 622f), 40f);
                CinematicShot(camera, $"{folder}/장면-1b-멀리서-본-보스.png", 1920, 1080, new Vector3(104f, 30f, 628f), new Vector3(283f, 20f, 620f), 26f);
                mech.gameObject.SetActive(true);

                // 2. 고로 앞에서 마주 선 로봇과 보스. 남쪽 낮은 자리에서 북쪽(고로)을 보고 찍는다.
                boss.SetPositionAndRotation(bossAt, Quaternion.LookRotation(robotAt - bossAt));
                bool idle = PoseBoss(boss, Motions + "great sword idle.fbx", 0.2f);
                var between = (robotAt + bossAt) * 0.5f;
                CinematicShot(camera, $"{folder}/장면-2-고로-앞에서-마주-섬.png", 1920, 1080, between + new Vector3(0f, 7f, -62f), between + Vector3.up * 21f, 55f);
                CinematicShot(camera, $"{folder}/장면-2b-마주-섬-비스듬히.png", 1920, 1080, between + new Vector3(-48f, 10f, -50f), between + Vector3.up * 20f, 55f);

                // 3. 로봇 눈높이에서 본 보스(로봇은 사진에서 뺀다).
                mech.gameObject.SetActive(false);
                var eye = robotAt + facing * LastMechEye;
                CinematicShot(camera, $"{folder}/장면-3-로봇-눈높이에서-본-보스.png", 1920, 1080, eye, bossAt + Vector3.up * 25f, 70f);
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                Debug.Log($"{LogPrefix} CINEMATIC_SCENES_OK walk={walk} idle={idle} {folder}");
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
