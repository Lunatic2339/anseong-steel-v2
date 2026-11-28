using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 받은 동작을 보스 모델에 입혀 한 줄에 여섯 장면씩 찍는다(어느 파일이 어떤 동작인지, 보스 뼈대에 제대로 붙는지 눈으로 보기 위한 것).
    /// -executeMethod Anseong.EditorTools.BossMotionPreview.Run  →  Logs/BossMotions/sheet-N.png
    /// </summary>
    public static class BossMotionPreview
    {
        const int Frames = 10, Cell = 190, Rows = 8;

        public static void Run()
        {
            try
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Anseong/Art/BossMecha/BossMecha.fbx");
                var boss = Object.Instantiate(model);
                var bounds = new Bounds(boss.transform.position, Vector3.zero);
                foreach (var r in boss.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                float height = bounds.size.y;

                var lightObject = new GameObject("Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                lightObject.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

                var cameraObject = new GameObject("Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = height * 0.62f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
                camera.farClipPlane = height * 20f;
                var target = bounds.center;
                // 정면에서 조금 옆, 조금 위.
                var eye = target + Quaternion.Euler(0f, 30f, 0f) * new Vector3(0f, height * 0.15f, height * 5f);
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));

                var clips = new List<AnimationClip>();
                foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { BossMotionImport.Folder.TrimEnd('/') }))
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                        if (o is AnimationClip c && !c.name.StartsWith("__preview")) clips.Add(c);
                clips = clips.OrderBy(c => c.name).ToList();
                string only = System.Environment.GetEnvironmentVariable("BOSS_MOTION_FILTER");
                if (!string.IsNullOrEmpty(only))
                {
                    var words = only.Split(',');
                    clips = clips.Where(c => words.Any(w => c.name.Contains(w))).ToList();
                }
                Debug.Log($"BOSS_MOTION clips={clips.Count} height={height:0.0} human={clips.Count(c => c.isHumanMotion)}");

                Directory.CreateDirectory("Logs/BossMotions");
                foreach (var old in Directory.GetFiles("Logs/BossMotions", "*.png")) File.Delete(old);
                var rt = new RenderTexture(Cell, Cell, 24);
                camera.targetTexture = rt;
                var index = new List<string>();
                Transform blade = null;
                var axis = Vector3.zero;
                for (int page = 0; page * Rows < clips.Count; page++)
                {
                    var sheet = new Texture2D(Cell * Frames, Cell * Rows, TextureFormat.RGB24, false);
                    var fill = Enumerable.Repeat(new Color32(40, 40, 40, 255), Cell * Frames * Cell * Rows).ToArray();
                    sheet.SetPixels32(fill);
                    for (int row = 0; row < Rows && page * Rows + row < clips.Count; row++)
                    {
                        var clip = clips[page * Rows + row];
                        index.Add($"sheet-{page} row {row + 1}: {clip.name} ({clip.length:0.00}s)");
                        for (int f = 0; f < Frames; f++)
                        {
                            float time = clip.length * f / (Frames - 1);
                            if (f == 0)
                            {
                                var bindings = AnimationUtility.GetCurveBindings(clip);
                                var arm = bindings.FirstOrDefault(x => x.propertyName.Contains("Left Arm Down-Up"));
                                var curve = arm.propertyName != null ? AnimationUtility.GetEditorCurve(clip, arm) : null;
                                Debug.Log($"BOSS_CLIP {clip.name} bindings={bindings.Length} arm={(curve != null ? curve.Evaluate(0f) + " -> " + curve.Evaluate(clip.length * 0.5f) : "none")}");
                            }
                            var animator = boss.GetComponent<Animator>();
                            animator.applyRootMotion = false;
                            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                            if (animator.runtimeAnimatorController == null)
                            {
                                var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath("Assets/_Anseong/Art/BossMecha/Motions/BossPreview.controller");
                                foreach (var c in clips) controller.AddMotion(c);
                                animator.runtimeAnimatorController = controller;
                                animator.Rebind();
                            }
                            animator.Play(clip.name, 0, f / (float)(Frames - 1) * 0.999f);
                            animator.Update(0f);
                            // 대검: 두 손이 자루를 쥐고 있다고 보고 왼손에서 오른손 쪽으로 뻗는다.
                            if (blade == null)
                            {
                                blade = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                                blade.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(1f, 0.85f, 0.2f) };
                            }
                            // 대검은 오른손에 쥔다. 날은 주먹의 엄지 쪽으로 나간다(새끼손가락 뿌리에서 가운뎃손가락 뿌리 쪽).
                            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                            if (axis == Vector3.zero)
                            {
                                Transform Find(string n) => boss.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
                                axis = hand.InverseTransformDirection(Find("middle1_base_r_042").position - Find("pinky1_base_r_046").position).normalized;
                                Debug.Log("BOSS_AXIS " + axis.ToString("0.00"));
                            }
                            var right = hand.position;
                            var along = hand.TransformDirection(axis).normalized;
                            blade.localScale = new Vector3(height * 0.02f, height * 0.06f, height * 0.75f);
                            blade.SetPositionAndRotation(right + along * (height * 0.375f), Quaternion.LookRotation(along, Vector3.up));
                            // 창 없는 Unity에서는 뼈를 돌려도 스킨이 다시 계산되지 않는다. 지금 자세를 굳은 메시로 바꿔 그린다.
                            var frozen = new List<GameObject>();
                            foreach (var skin in boss.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            {
                                var mesh = new Mesh();
                                skin.BakeMesh(mesh, true);
                                var copy = new GameObject("Frozen");
                                copy.transform.SetParent(skin.transform, false);
                                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                                copy.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                                skin.enabled = false;
                                frozen.Add(copy);
                            }
                            camera.Render();
                            foreach (var copy in frozen) Object.DestroyImmediate(copy);
                            RenderTexture.active = rt;
                            sheet.ReadPixels(new Rect(0, 0, Cell, Cell), f * Cell, (Rows - 1 - row) * Cell);
                        }
                    }
                    sheet.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes($"Logs/BossMotions/sheet-{page}.png", sheet.EncodeToPNG());
                }
                File.WriteAllLines("Logs/BossMotions/index.txt", index);
                camera.targetTexture = null;
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
