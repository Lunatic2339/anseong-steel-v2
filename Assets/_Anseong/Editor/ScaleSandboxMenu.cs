using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 로봇 모델을 65m로 세워 기준물과 비교하는 샌드박스 씬 메뉴. 씬 저장은 사람이 한다.
    /// </summary>
    public static class ScaleSandboxMenu
    {
        const string IdleClipPath = "Assets/_Anseong/Art/Robot/Animation/Source/Idle.fbx";
        const string IdleControllerPath = "Assets/_Anseong/Art/Robot/Animation/Robot_Idle.controller";
        const string SandboxFolderParent = "Assets/_Anseong/Scenes";
        const string SandboxFolderName = "Sandbox";
        const string SandboxSceneHint = "Assets/_Anseong/Scenes/Sandbox/Sandbox_Scale.unity";
        const string PreviewClipPrefix = "__preview__";
        const string LogPrefix = "[ScaleSandboxMenu]";

        const float RobotHeight = RobotSandboxUtility.RobotHeight;

        const float ReferenceRowZ = -50f;        // 기준물 줄 (로봇 앞)
        const float PersonHeight = 1.8f;
        const float PersonRadius = 0.25f;
        static readonly Vector3 TruckSize = new Vector3(2.5f, 4f, 10f);
        static readonly Vector3 BuildingSize = new Vector3(20f, 30f, 20f);
        const float PersonX = 30f;               // 로봇(X=0)을 가리지 않게 옆으로 비켜 세운다
        const float TruckX = 36f;
        const float BuildingX = 60f;

        const float GroundSize = 400f;           // 바닥 한 변 (m)
        const float UnityPlaneSize = 10f;        // 기본 Plane 한 변
        const float UnityCapsuleHeight = 2f;     // 기본 Capsule 높이
        static readonly Vector3 CameraPosition = new Vector3(0f, 40f, -180f);
        static readonly Vector3 CameraLookAt = new Vector3(0f, 30f, 0f);

        [MenuItem("Anseong/Sandbox/Build Scale Test")]
        static void BuildScaleTest()
        {
            var art = RobotArtSettings.Load();
            var model = art.PlayerModel;
            var idleClip = LoadClip(IdleClipPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Build Scale Test", $"모델을 찾지 못했습니다: {art.PlayerModelPath}", "확인");
                return;
            }
            if (idleClip == null)
            {
                EditorUtility.DisplayDialog("Build Scale Test", $"Idle 클립을 찾지 못했습니다: {IdleClipPath}", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var controller = GetOrCreateIdleController(idleClip);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            PlaceRobot(model, controller, idleClip);
            CreateGround();
            CreateReferences();
            AimCamera(scene);
            EnsureSandboxFolder();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LogPrefix} 스케일 테스트 씬 생성 완료. {SandboxSceneHint}로 저장하세요.");
        }

        [MenuItem("Anseong/Sandbox/Report Robot Stats")]
        static void ReportRobotStats()
        {
            var art = RobotArtSettings.Load();
            ReportStats(art.PlayerModel, art.PlayerModelPath);
            if (art.BossModel != art.PlayerModel)
                ReportStats(art.BossModel, art.BossModelPath);
        }

        static void ReportStats(GameObject model, string modelPath)
        {
            if (model == null)
            {
                Debug.LogError($"{LogPrefix} 모델을 찾지 못했습니다: {modelPath}");
                return;
            }

            var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshRenderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var materialSlots = renderers.Sum(r => r.sharedMaterials.Length);
            var uniqueMaterials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();

            var meshes = skinned.Select(s => s.sharedMesh)
                .Concat(model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh))
                .Where(m => m != null)
                .ToArray();
            long triangles = 0;
            long vertices = 0;
            foreach (var mesh in meshes)
            {
                vertices += mesh.vertexCount;
                for (var i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles)
                        triangles += mesh.GetIndexCount(i) / 3;
            }
            var maxBones = skinned.Length == 0 ? 0 : skinned.Max(s => s.bones.Length);

            Debug.Log($"{LogPrefix} 로봇 모델 통계 ({model.name}): SkinnedMeshRenderer {skinned.Length}, MeshRenderer {meshRenderers.Length}, " +
                      $"재질 슬롯 {materialSlots} / 고유 재질 {uniqueMaterials}, 삼각형 {triangles:N0}, 정점 {vertices:N0}, " +
                      $"렌더러당 최대 본 {maxBones}", model);
        }

        static void PlaceRobot(GameObject model, AnimatorController controller, AnimationClip idleClip)
        {
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(model);
            robot.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(robot, "Build Scale Test");

            if (!RobotSandboxUtility.FitToHeight(robot, RobotHeight, out var bounds, out var scale, out var offset))
            {
                Debug.LogError($"{LogPrefix} {model.name}: 렌더러가 없어 높이를 잴 수 없습니다.", robot);
                return;
            }
            var measured = bounds.size.y;

            var animator = robot.GetComponent<Animator>();
            if (animator == null) animator = robot.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = RobotArtSettings.AvatarOf(model);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var (matched, total) = CountMatchedBindingPaths(robot.transform, idleClip);
            Debug.Log($"{LogPrefix} {model.name}: 측정 높이 {measured:F3} m, 폭 {bounds.size.x:F3} m, 깊이 {bounds.size.z:F3} m " +
                      $"→ 배율 {scale:F3} → 높이 {RobotHeight} m, 바닥 보정 {offset:F3} m. " +
                      $"Idle 바인딩 경로 {matched}/{total} 일치, Avatar {(animator.avatar != null ? animator.avatar.name : "없음")}", robot);
        }

        static (int matched, int total) CountMatchedBindingPaths(Transform root, AnimationClip clip)
        {
            var paths = AnimationUtility.GetCurveBindings(clip)
                .Select(binding => binding.path)
                .Distinct()
                .ToArray();
            var matched = paths.Count(path => path.Length == 0 || root.Find(path) != null);
            return (matched, paths.Length);
        }

        static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith(PreviewClipPrefix));
        }

        static AnimatorController GetOrCreateIdleController(AnimationClip idleClip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(IdleControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(IdleControllerPath);
                controller.AddMotion(idleClip);
                Debug.Log($"{LogPrefix} Idle 컨트롤러 생성: {IdleControllerPath}", controller);
            }
            else
            {
                var state = controller.layers[0].stateMachine.defaultState;
                if (state == null)
                    controller.AddMotion(idleClip);
                else if (state.motion != idleClip)
                    state.motion = idleClip;
                EditorUtility.SetDirty(controller);
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * (GroundSize / UnityPlaneSize);
            Undo.RegisterCreatedObjectUndo(ground, "Build Scale Test");
        }

        static void CreateReferences()
        {
            var root = new GameObject("Scale References");
            Undo.RegisterCreatedObjectUndo(root, "Build Scale Test");

            var person = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            person.name = $"Person {PersonHeight}m";
            person.transform.SetParent(root.transform, false);
            person.transform.localScale = new Vector3(PersonRadius * 2f, PersonHeight / UnityCapsuleHeight, PersonRadius * 2f);
            person.transform.localPosition = new Vector3(PersonX, PersonHeight * 0.5f, ReferenceRowZ);

            CreateBox(root.transform, $"Truck {TruckSize.y}m", TruckSize, TruckX);
            CreateBox(root.transform, $"Building {BuildingSize.y}m", BuildingSize, BuildingX);
        }

        static void CreateBox(Transform parent, string name, Vector3 size, float x)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localScale = size;
            box.transform.localPosition = new Vector3(x, size.y * 0.5f, ReferenceRowZ);
        }

        static void AimCamera(Scene scene)
        {
            var camera = scene.GetRootGameObjects()
                .Select(root => root.GetComponentInChildren<Camera>(true))
                .FirstOrDefault(c => c != null);
            if (camera == null) return;
            camera.transform.position = CameraPosition;
            camera.transform.LookAt(CameraLookAt);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, GroundSize * 2f);
        }

        static void EnsureSandboxFolder()
        {
            var folder = $"{SandboxFolderParent}/{SandboxFolderName}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(SandboxFolderParent, SandboxFolderName);
        }
    }
}
