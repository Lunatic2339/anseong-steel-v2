using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>내려받은 메카 모델을 보스로 쓸 수 있는지 보는 시험. 새 장면을 만들어 찍기만 하고 저장하지 않는다.</summary>
    public static class BossMechaTest
    {
        const string Dir = "Assets/_Anseong/Art/BossMecha";
        const string Fbx = Dir + "/BossMecha.fbx";
        const string OutDir = "Logs/BossMechaTest";
        const float Height = 35f;

        public static void Run()
        {
            try { Configure(); Capture(); Debug.Log("[BossMechaTest] OK"); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void RunCapture()
        {
            try { Capture(); Debug.Log("[BossMechaTest] OK"); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Anseong/시험/보스 메카 모델 설정")]
        public static void Configure()
        {
            var rows = File.ReadAllLines(Dir + "/materials.tsv").Select(l => l.Split('\t')).Where(r => r.Length == 3).ToList();
            foreach (var r in rows)
            {
                string path = Dir + "/" + r[2];
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) continue;
                ti.maxTextureSize = 1024;
                if (r[1] == "normal") ti.textureType = TextureImporterType.NormalMap;
                if (r[1] == "orm") ti.sRGBTexture = false;
                ti.SaveAndReimport();
            }
            // glTF의 거칠기(G)·금속(B) 그림을 URP가 읽는 금속(R)·매끈함(A)으로 바꿔 새 그림으로 둔다.
            foreach (var r in rows.Where(r => r[1] == "orm"))
            {
                string src = Dir + "/" + r[2];
                string dst = Dir + "/" + Path.GetFileNameWithoutExtension(r[2]) + "_mask.png";
                if (File.Exists(dst)) continue;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                t.LoadImage(File.ReadAllBytes(src));
                var px = t.GetPixels32();
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(px[i].b, 255, 0, (byte)(255 - px[i].g));
                t.SetPixels32(px); t.Apply();
                File.WriteAllBytes(dst, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(dst);
                var mi = (TextureImporter)AssetImporter.GetAtPath(dst);
                mi.sRGBTexture = false; mi.maxTextureSize = 1024; mi.alphaSource = TextureImporterAlphaSource.FromInput;
                mi.SaveAndReimport();
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            Directory.CreateDirectory(Dir + "/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var g in rows.GroupBy(r => r[0]))
            {
                string mp = Dir + "/Materials/" + g.Key + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, mp); }
                foreach (var r in g)
                {
                    if (r[1] == "base") m.SetTexture("_BaseMap", Load(r[2]));
                    if (r[1] == "normal") { m.SetTexture("_BumpMap", Load(r[2])); m.EnableKeyword("_NORMALMAP"); }
                    if (r[1] == "orm")
                    {
                        m.SetTexture("_MetallicGlossMap", Load(Path.GetFileNameWithoutExtension(r[2]) + "_mask.png"));
                        m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f);
                    }
                    if (r[1] == "emis")
                    {
                        m.SetTexture("_EmissionMap", Load(r[2])); m.SetColor("_EmissionColor", Color.white * 2f);
                        m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    }
                }
                EditorUtility.SetDirty(m);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), g.Key), m);
            }
            AssetDatabase.SaveAssets();
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[BossMechaTest] avatar={(avatar ? avatar.name : "none")} valid={(avatar && avatar.isValid)} human={(avatar && avatar.isHuman)}");
            foreach (var h in importer.humanDescription.human) Debug.Log($"[BossMechaTest] MAP {h.humanName} = {h.boneName}");
        }

        static Texture2D Load(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + file);

        [MenuItem("Anseong/시험/보스 메카 모델 찍기")]
        public static void Capture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, 215f, 0f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 60f;
            ground.GetComponent<Renderer>().sharedMaterial = Flat(new Color(0.32f, 0.33f, 0.34f), false);

            // 보스는 원점에 서서 -X(서쪽)를 본다. 로봇 자리는 서쪽 22.
            // 크기와 방향은 부모가 갖고, 동작은 자식(모델)에만 입힌다.
            var boss = new GameObject("BossRoot");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            model.transform.SetParent(boss.transform, false);
            boss.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            var b0 = MeshBounds(boss);
            float scale = Height / b0.size.y;
            boss.transform.localScale = Vector3.one * scale;
            var b = MeshBounds(boss);
            boss.transform.position = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            b = MeshBounds(boss);
            Debug.Log($"[BossMechaTest] rawSize={b0.size} scale={scale:F2} size35={b.size}");

            var animator = boss.GetComponentInChildren<Animator>();
            Debug.Log($"[BossMechaTest] animator={(animator != null)} human={(animator && animator.isHuman)}");
            var fwd = Vector3.left; var left = Vector3.back;   // 서쪽을 볼 때 왼쪽은 남쪽
            Transform chest = boss.transform;
            if (animator && animator.isHuman)
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            // 등 뒤의 불 셋: 등 가운데에서 뒤로 3 솟게.
            float back = b.extents.x * 0.45f + 3f;
            for (int i = 0; i < 3; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "BackLight" + i;
                s.transform.position = new Vector3(0f, Height * (0.62f + 0.08f * i), 0f) - fwd * back;
                s.transform.localScale = Vector3.one * 2.4f;
                s.GetComponent<Renderer>().sharedMaterial = Flat(new Color(1f, 0.15f, 0.1f), true);
                s.transform.SetParent(chest, true);
            }
            // 우리 로봇 자리 표시(키 35 기둥).
            var robot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            robot.name = "RobotStandIn";
            robot.transform.localScale = new Vector3(8f, 35f, 12f);
            robot.transform.position = fwd * 22f + Vector3.up * 17.5f;
            robot.GetComponent<Renderer>().sharedMaterial = Flat(new Color(0.25f, 0.45f, 0.6f), false);

            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.fieldOfView = 90f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            Directory.CreateDirectory(OutDir);

            // 사람형 동작: Meshy로 만든 일반병 동작 넷(뼈 이름이 Mixamo식). Mixamo 동작이 들어올 길과 같은 길이다.
            var clips = new[] { "Grunt_walk", "Grunt_fire", "Grunt_hit", "Grunt_death" };
            var loaded = new Dictionary<string, AnimationClip>();
            foreach (var n in clips)
            {
                string fp = Dir + "/TestMotions/" + n + ".fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(fp);
                if (mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.SaveAndReimport();
                }
                var av = AssetDatabase.LoadAllAssetsAtPath(fp).OfType<Avatar>().FirstOrDefault();
                loaded[n] = AssetDatabase.LoadAllAssetsAtPath(fp).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                Debug.Log($"[BossMechaTest] {n} avatarValid={(av && av.isValid && av.isHuman)} clip={(loaded[n] ? loaded[n].name : "none")} len={(loaded[n] ? loaded[n].length : 0f):F2} humanMotion={(loaded[n] && loaded[n].humanMotion)}");
            }
            var times = new[] { 0.15f, 0.4f, 0.6f, 0.85f };
            const int W = 480, H = 270;
            var views = new[] { "p1", "p2", "front34", "back34" };
            var sheets = views.Select(_ => new Texture2D(W * times.Length, H * clips.Length, TextureFormat.RGB24, false)).ToArray();
            Shot(cam, fwd * 22f + Vector3.up * 28f, Vector3.up * 24f, 1280, 720, "rest_p1", null, 0, 0);
            Shot(cam, fwd * 11f + left * 37f + Vector3.up * 50f, fwd * 11f + Vector3.up * 22f, 1280, 720, "rest_p2", null, 0, 0);
            for (int ci = 0; ci < clips.Length; ci++)
            for (int ti = 0; ti < times.Length; ti++)
            {
                var clip = loaded[clips[ci]];
                animator.applyRootMotion = false;
                animator.transform.localPosition = Vector3.zero; animator.transform.localRotation = Quaternion.identity;
                if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * times[ti]);
                AnimationMode.EndSampling();
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                animator.transform.position += new Vector3(-hips.x, 0f, -hips.z);
                var fb = MeshBounds(boss);
                animator.transform.position += Vector3.down * fb.min.y;
                var frozen = Freeze(model);
                Debug.Log($"[BossMechaTest] {clips[ci]} t={times[ti]} hand={animator.GetBoneTransform(HumanBodyBones.LeftHand).position} chest={chest.position} light0={GameObject.Find("BackLight0").transform.position} size={MeshBounds(boss).size}");
                int y = (clips.Length - 1 - ci) * H, x = ti * W;
                bool big = ti == 2;
                robot.SetActive(false);
                cam.fieldOfView = 90f;
                Shot(cam, fwd * 22f + Vector3.up * 28f, Vector3.up * 24f, W, H, null, sheets[0], x, y);
                if (big) Shot(cam, fwd * 22f + Vector3.up * 28f, Vector3.up * 24f, 1280, 720, clips[ci] + "_p1", null, 0, 0);
                robot.SetActive(true);
                Shot(cam, fwd * 11f + left * 37f + Vector3.up * 50f, fwd * 11f + Vector3.up * 22f, W, H, null, sheets[1], x, y);
                if (big) Shot(cam, fwd * 11f + left * 37f + Vector3.up * 50f, fwd * 11f + Vector3.up * 22f, 1280, 720, clips[ci] + "_p2", null, 0, 0);
                robot.SetActive(false);
                cam.fieldOfView = 50f;
                Shot(cam, fwd * 50f + left * 32f + Vector3.up * 22f, Vector3.up * 18f, W, H, null, sheets[2], x, y);
                if (big) Shot(cam, fwd * 50f + left * 32f + Vector3.up * 22f, Vector3.up * 18f, 1280, 720, clips[ci] + "_front34", null, 0, 0);
                Shot(cam, -fwd * 50f + left * 32f + Vector3.up * 26f, Vector3.up * 19f, W, H, null, sheets[3], x, y);
                Unfreeze(model, frozen);
            }
            AnimationMode.StopAnimationMode();
            for (int i = 0; i < views.Length; i++) { sheets[i].Apply(); File.WriteAllBytes($"{OutDir}/sheet_{views[i]}.png", sheets[i].EncodeToPNG()); }
        }

        // 창 없는 Unity에서는 한 번의 호출 안에서 스킨이 다시 계산되지 않는다. 그 순간의 모양을 굳혀서 찍는다.
        static List<GameObject> Freeze(GameObject model)
        {
            var list = new List<GameObject>();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                r.BakeMesh(mesh, true);
                var g = new GameObject("Frozen");
                g.transform.SetParent(r.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                g.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                r.enabled = false;
                list.Add(g);
            }
            return list;
        }

        static void Unfreeze(GameObject model, List<GameObject> frozen)
        {
            foreach (var g in frozen) { Object.DestroyImmediate(g.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(g); }
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) r.enabled = true;
        }

        static Bounds MeshBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            Bounds b = default; bool first = true;
            foreach (var r in rs)
            {
                r.BakeMesh(baked, true);
                var m = r.transform.localToWorldMatrix;
                foreach (var v in baked.vertices)
                {
                    var w = m.MultiplyPoint3x4(v);
                    if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
                }
            }
            Object.DestroyImmediate(baked);
            return b;
        }

        static Material Flat(Color c, bool glow)
        {
            var m = new Material(Shader.Find(glow ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        static void Shot(Camera cam, Vector3 from, Vector3 lookAt, int w, int h, string name, Texture2D sheet, int sx, int sy)
        {
            cam.transform.position = from;
            cam.transform.LookAt(lookAt);
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            if (sheet != null) sheet.ReadPixels(new Rect(0, 0, w, h), sx, sy);
            else
            {
                var img = new Texture2D(w, h, TextureFormat.RGB24, false);
                img.ReadPixels(new Rect(0, 0, w, h), 0, 0); img.Apply();
                File.WriteAllBytes($"{OutDir}/{name}.png", img.EncodeToPNG());
                Object.DestroyImmediate(img);
            }
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt);
        }
    }
}
