using System.Collections.Generic;
using System.IO;
using System.Linq;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Pilot;
using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// AI(Meshy) 모델을 게임 칸에 넣는다. `Art/AI/&lt;이름&gt;/&lt;이름&gt;.fbx`(+ `_BaseColor`·`_Normal`·`_Metallic`·`_Emission` png)를 읽어
    /// 방향(가장 긴 쪽 = 손가락 쪽 +Y 등)·크기(팔뚝 길이 배수 또는 m)·원점(가운데 또는 바닥)을 맞춘 프리팹을 `Art/AI/Prefabs/`에 만들고
    /// 우리 무기 4칸·드론·철판·보스 주먹 포탄 칸에 넣는다. AI 모델은 원점·방향이 제멋대로라 Blender 손질 대신 여기서 맞춘다.
    /// 'Add Match Flow'(그래서 Build Full Game)가 부른다. 모델이 없으면 그 칸은 그대로 둔다(도형).
    /// </summary>
    public static class AiModelFit
    {
        const string Root = "Assets/_Anseong/Art/AI";
        const string PrefabFolder = Root + "/Prefabs";

        enum Align { None, LongestUp, ThinnestForward }
        enum Origin { Center, Bottom }

        struct Spec
        {
            public string Name;
            public Align Align;
            public Origin Origin;
            /// <summary>가장 긴 쪽 길이. 손에 드는 것은 팔뚝 길이 배수, 나머지는 m.</summary>
            public float Size;
            public bool InHand;
            public bool FlipWideEndDown;
        }

        static readonly Spec[] Specs =
        {
            new Spec { Name = "Weapon_Sword", Align = Align.LongestUp, Size = 1.9f, InHand = true, FlipWideEndDown = true },
            new Spec { Name = "Weapon_Shield", Align = Align.ThinnestForward, Size = 1.5f, InHand = true },
            new Spec { Name = "Weapon_Cannon", Align = Align.LongestUp, Size = 1.1f, InHand = true, FlipWideEndDown = true },
            new Spec { Name = "Weapon_Scanner", Align = Align.LongestUp, Size = 0.8f, InHand = true, FlipWideEndDown = true },
            new Spec { Name = "Drone", Align = Align.None, Size = 1f },   // DroneDirector가 droneSize(m)를 곱한다
            new Spec { Name = "Checkup_SteelPlate", Align = Align.ThinnestForward, Origin = Origin.Bottom, Size = 10f },
            new Spec { Name = "Boss_FistShell", Align = Align.None, Size = 1f },
            // 보스 전용 장식(BossDressing이 가슴~머리 거리 배수로 키운다)과 전투장 둘레 구조물(SceneryDressing이 m로 키운다): 가장 긴 쪽 1 m.
            new Spec { Name = "Boss_Crest", Align = Align.None, Size = 1f },
            new Spec { Name = "Boss_Pauldron", Align = Align.None, Size = 1f },
            new Spec { Name = "Boss_BackSpines", Align = Align.None, Size = 1f },
            new Spec { Name = "Boss_CoreHousing", Align = Align.ThinnestForward, Size = 1f },
            new Spec { Name = "Scenery_BlastFurnace", Align = Align.None, Origin = Origin.Bottom, Size = 1f },
            new Spec { Name = "Scenery_GantryCrane", Align = Align.None, Origin = Origin.Bottom, Size = 1f },
            new Spec { Name = "Scenery_GasHolder", Align = Align.None, Origin = Origin.Bottom, Size = 1f },
            new Spec { Name = "Scenery_Chimney", Align = Align.LongestUp, Origin = Origin.Bottom, Size = 1f },
        };

        [MenuItem("Anseong/Art/Fit AI Models (Meshy → 칸)")]
        static void Menu()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Debug.Log("[AiModelFit] " + Apply(scene.GetRootGameObjects()) + ". 씬을 저장하세요.");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>프리팹을 만들고(있으면 다시 만듦) 씬의 칸에 넣는다. 한 줄 요약을 돌려준다.</summary>
        public static string Apply(GameObject[] roots)
        {
            if (!AssetDatabase.IsValidFolder(Root)) return "AI 모델 폴더 없음(" + Root + ")";
            MeasureHand(out var handScale, out var forearm);
            var prefabs = new Dictionary<string, GameObject>();
            var notes = new List<string>();
            foreach (var spec in Specs)
            {
                var prefab = Build(spec, handScale, forearm, out var note);
                if (prefab != null) prefabs[spec.Name] = prefab;
                notes.Add(spec.Name + (prefab != null ? " ✓" : " –(" + note + ")"));
            }

            var weapons = roots.SelectMany(r => r.GetComponentsInChildren<PlayerWeaponVisuals>(true)).FirstOrDefault();
            if (weapons != null)
            {
                var s = new SerializedObject(weapons);
                Set(s, "swordPrefab", prefabs, "Weapon_Sword");
                Set(s, "shieldPrefab", prefabs, "Weapon_Shield");
                Set(s, "cannonPrefab", prefabs, "Weapon_Cannon");
                Set(s, "scannerPrefab", prefabs, "Weapon_Scanner");
                s.ApplyModifiedProperties();
            }
            var drones = roots.SelectMany(r => r.GetComponentsInChildren<DroneDirector>(true)).FirstOrDefault();
            if (drones != null)
            {
                var s = new SerializedObject(drones);
                Set(s, "dronePrefab", prefabs, "Drone");
                s.ApplyModifiedProperties();
            }
            var checkup = roots.SelectMany(r => r.GetComponentsInChildren<CheckupDirector>(true)).FirstOrDefault();
            if (checkup != null)
            {
                var s = new SerializedObject(checkup);
                Set(s, "platePrefab", prefabs, "Checkup_SteelPlate");
                s.ApplyModifiedProperties();
            }
            var bossWeapons = roots.SelectMany(r => r.GetComponentsInChildren<BossWeaponVisuals>(true)).FirstOrDefault();
            if (bossWeapons != null)
            {
                var s = new SerializedObject(bossWeapons);
                Set(s, "shellPrefab", prefabs, "Boss_FistShell");
                s.ApplyModifiedProperties();
            }
            if (bossWeapons != null) notes.Add(DressBoss(bossWeapons, prefabs));
            if (roots.Length > 0) notes.Add(DressScenery(roots, prefabs));
            AssetDatabase.SaveAssets();
            return "AI 모델: " + string.Join(", ", notes);
        }

        // ── 보스 장식: 자리·크기는 가슴~머리 거리 배수(보스 기준 x 오른쪽, y 위, z 앞). 캡처를 보며 여기 숫자를 고친다. ──
        struct Dress
        {
            public string Label, Model, Bone;
            public Vector3 Offset, Euler;
            public float Size;
            public bool Mirror;
        }

        static readonly Dress[] BossDress =
        {
            // 11/13 run12 화면 맞춤: 몸통 뼈는 Spine2가 없어 Spine(허리) 기준. 어깨 장갑은 너무 커서 머리가 셋처럼 보여 줄임.
            new Dress { Label = "Crest", Model = "Boss_Crest", Bone = RobotSandboxUtility.HeadBone, Offset = new Vector3(0f, 0.6f, 0f), Size = 1.5f },
            new Dress { Label = "Pauldron R", Model = "Boss_Pauldron", Bone = RobotSandboxUtility.RightPrefix + "Arm", Offset = new Vector3(0.3f, 0.05f, 0.05f), Size = 1.15f },
            new Dress { Label = "Pauldron L", Model = "Boss_Pauldron", Bone = RobotSandboxUtility.LeftPrefix + "Arm", Offset = new Vector3(-0.3f, 0.05f, 0.05f), Size = 1.15f, Mirror = true },
            new Dress { Label = "Back Spines", Model = "Boss_BackSpines", Bone = RobotSandboxUtility.SpineBone, Offset = new Vector3(0f, 0.95f, -0.45f), Size = 1.6f },
            new Dress { Label = "Core Housing", Model = "Boss_CoreHousing", Bone = RobotSandboxUtility.SpineBone, Offset = new Vector3(0f, 0f, 0.4f), Size = 0.75f },
        };

        static string DressBoss(BossWeaponVisuals visuals, Dictionary<string, GameObject> prefabs)
        {
            var boss = visuals.transform;
            var dressing = boss.GetComponent<BossDressing>();
            if (dressing == null) dressing = boss.gameObject.AddComponent<BossDressing>();
            var from = new SerializedObject(visuals);
            var s = new SerializedObject(dressing);
            s.FindProperty("chest").objectReferenceValue = from.FindProperty("chest").objectReferenceValue;
            s.FindProperty("head").objectReferenceValue = from.FindProperty("head").objectReferenceValue;
            var list = s.FindProperty("pieces");
            list.arraySize = BossDress.Length;
            var filled = 0;
            for (var i = 0; i < BossDress.Length; i++)
            {
                var d = BossDress[i];
                var e = list.GetArrayElementAtIndex(i);
                prefabs.TryGetValue(d.Model, out var prefab);
                var bone = RobotSandboxUtility.FindBone(boss, d.Bone);
                e.FindPropertyRelative("label").stringValue = d.Label;
                e.FindPropertyRelative("bone").objectReferenceValue = bone;
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                e.FindPropertyRelative("offset").vector3Value = d.Offset;
                e.FindPropertyRelative("euler").vector3Value = d.Euler;
                e.FindPropertyRelative("size").floatValue = d.Size;
                e.FindPropertyRelative("mirror").boolValue = d.Mirror;
                if (prefab != null && bone != null) filled++;
            }
            s.ApplyModifiedPropertiesWithoutUndo();
            return $"보스 장식 {filled}/{BossDress.Length}";
        }

        // ── 전투장 둘레 구조물: 자리(m, 씬 원점 기준. 우리 로봇이 원점에서 +Z를 본다), 세로축 회전, 가장 긴 쪽 크기(m). ──
        struct Scenery
        {
            public string Label, Model;
            public Vector3 Position;
            public float Yaw, Size;
        }

        static readonly Scenery[] SceneryTable =
        {
            // 11/13 run12: 위에서 본 맵 기준. 전투 마당 X -160~150·Z -270~290, 동쪽 X 240부터 바다, 서쪽 X -200부터 시가지.
            // 북쪽(조종사 정면 배경)에 고로·굴뚝, 동쪽 부두에 크레인, 남쪽(뒤)에 가스 탱크.
            new Scenery { Label = "Blast Furnace A", Model = "Scenery_BlastFurnace", Position = new Vector3(-60f, 0f, 400f), Yaw = 20f, Size = 150f },
            new Scenery { Label = "Blast Furnace B", Model = "Scenery_BlastFurnace", Position = new Vector3(90f, 0f, 430f), Yaw = 200f, Size = 130f },
            new Scenery { Label = "Chimney A", Model = "Scenery_Chimney", Position = new Vector3(195f, 0f, 330f), Size = 170f },
            new Scenery { Label = "Chimney B", Model = "Scenery_Chimney", Position = new Vector3(-140f, 0f, 350f), Yaw = 90f, Size = 150f },
            new Scenery { Label = "Gantry Crane A", Model = "Scenery_GantryCrane", Position = new Vector3(215f, 0f, 230f), Yaw = 90f, Size = 110f },
            new Scenery { Label = "Gantry Crane B", Model = "Scenery_GantryCrane", Position = new Vector3(215f, 0f, -200f), Yaw = 90f, Size = 100f },
            new Scenery { Label = "Gas Holder A", Model = "Scenery_GasHolder", Position = new Vector3(-80f, 0f, -380f), Size = 90f },
            new Scenery { Label = "Gas Holder B", Model = "Scenery_GasHolder", Position = new Vector3(90f, 0f, -360f), Yaw = 45f, Size = 80f },
        };

        const string SceneryRootName = "Scenery (AI)";

        static string DressScenery(GameObject[] roots, Dictionary<string, GameObject> prefabs)
        {
            var dressing = roots.SelectMany(r => r.GetComponentsInChildren<SceneryDressing>(true)).FirstOrDefault();
            if (dressing == null)
            {
                var go = new GameObject(SceneryRootName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, roots[0].scene);
                dressing = go.AddComponent<SceneryDressing>();
            }
            var s = new SerializedObject(dressing);
            var list = s.FindProperty("pieces");
            list.arraySize = SceneryTable.Length;
            var filled = 0;
            for (var i = 0; i < SceneryTable.Length; i++)
            {
                var d = SceneryTable[i];
                var e = list.GetArrayElementAtIndex(i);
                prefabs.TryGetValue(d.Model, out var prefab);
                e.FindPropertyRelative("label").stringValue = d.Label;
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                e.FindPropertyRelative("position").vector3Value = d.Position;
                e.FindPropertyRelative("yaw").floatValue = d.Yaw;
                e.FindPropertyRelative("size").floatValue = d.Size;
                if (prefab != null) filled++;
            }
            s.ApplyModifiedPropertiesWithoutUndo();
            return $"둘레 구조물 {filled}/{SceneryTable.Length}";
        }

        static void Set(SerializedObject s, string property, Dictionary<string, GameObject> prefabs, string name)
        {
            var p = s.FindProperty(property);
            if (p != null && prefabs.TryGetValue(name, out var prefab)) p.objectReferenceValue = prefab;
        }

        /// <summary>플레이어 로봇 프리팹에서 손 뼈 크기 배율과 팔뚝 길이(m)를 잰다. 없으면 1, 10.</summary>
        static void MeasureHand(out float handScale, out float forearm)
        {
            handScale = 1f;
            forearm = 10f;
            var robot = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabMenu.PlayerPrefabPath);
            if (robot == null) return;
            var arm = robot.GetComponentsInChildren<RobotArmDriver>(true).FirstOrDefault(a => a.HandBone != null);
            if (arm == null) return;
            var hand = arm.HandBone;
            handScale = Mathf.Max(1e-5f, hand.lossyScale.x);
            if (hand.parent != null) forearm = Mathf.Max(0.01f, Vector3.Distance(hand.position, hand.parent.position));
        }

        static GameObject Build(Spec spec, float handScale, float forearm, out string note)
        {
            note = string.Empty;
            var modelPath = $"{Root}/{spec.Name}/{spec.Name}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                note = "모델 없음";
                return null;
            }
            var material = BuildMaterial(spec.Name);

            var root = new GameObject(spec.Name);
            try
            {
                var pivot = new GameObject("Fit").transform;
                pivot.SetParent(root.transform, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var a in instance.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
                if (material != null)
                    foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();

                var points = Vertices(instance, root.transform);
                if (points.Count == 0)
                {
                    note = "메시 없음";
                    return null;
                }
                // 1) 방향
                var size = BoundsOf(points, Quaternion.identity).size;
                var rotation = Quaternion.identity;
                if (spec.Align == Align.LongestUp)
                {
                    if (size.x >= size.y && size.x >= size.z) rotation = Quaternion.Euler(0f, 0f, 90f);
                    else if (size.z >= size.y && size.z >= size.x) rotation = Quaternion.Euler(-90f, 0f, 0f);
                }
                else if (spec.Align == Align.ThinnestForward)
                {
                    if (size.x <= size.y && size.x <= size.z) rotation = Quaternion.Euler(0f, 90f, 0f);
                    else if (size.y <= size.x && size.y <= size.z) rotation = Quaternion.Euler(90f, 0f, 0f);
                }
                var bounds = BoundsOf(points, rotation);
                // 넓은 끝(손잡이·몸통)이 아래(손 쪽)로 오게.
                if (spec.FlipWideEndDown && WideEndIsUp(points, rotation, bounds))
                {
                    rotation = Quaternion.Euler(180f, 0f, 0f) * rotation;
                    bounds = BoundsOf(points, rotation);
                }
                // 2) 크기: 가장 긴 쪽 = Size(손에 드는 것은 팔뚝 배수 → 손 뼈 기준 단위로)
                var longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                var target = spec.InHand ? spec.Size * forearm / handScale : spec.Size;
                var scale = target / Mathf.Max(1e-6f, longest);
                // 3) 원점
                var anchor = spec.Origin == Origin.Bottom ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) : bounds.center;
                pivot.localRotation = rotation;
                pivot.localScale = Vector3.one * scale;
                pivot.localPosition = -anchor * scale;

                if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder(Root, "Prefabs");
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{spec.Name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static List<Vector3> Vertices(GameObject instance, Transform space)
        {
            var points = new List<Vector3>();
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = filter.sharedMesh.vertices;
                var step = Mathf.Max(1, vertices.Length / 4000);
                for (var i = 0; i < vertices.Length; i += step) points.Add(toSpace.MultiplyPoint3x4(vertices[i]));
            }
            foreach (var skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null) continue;
                var toSpace = space.worldToLocalMatrix * skinned.transform.localToWorldMatrix;
                foreach (var v in skinned.sharedMesh.vertices) points.Add(toSpace.MultiplyPoint3x4(v));
            }
            return points;
        }

        static Bounds BoundsOf(List<Vector3> points, Quaternion rotation)
        {
            var b = new Bounds(rotation * points[0], Vector3.zero);
            for (var i = 1; i < points.Count; i++) b.Encapsulate(rotation * points[i]);
            return b;
        }

        /// <summary>위쪽 1/4의 단면이 아래쪽 1/4보다 넓은가(검 손잡이·포 몸통이 위에 있으면 뒤집는다).</summary>
        static bool WideEndIsUp(List<Vector3> points, Quaternion rotation, Bounds bounds)
        {
            float top = 0f, bottom = 0f;
            var lowCut = bounds.min.y + bounds.size.y * 0.25f;
            var highCut = bounds.max.y - bounds.size.y * 0.25f;
            foreach (var p in points)
            {
                var q = rotation * p;
                var radius = new Vector2(q.x - bounds.center.x, q.z - bounds.center.z).magnitude;
                if (q.y <= lowCut) bottom = Mathf.Max(bottom, radius);
                else if (q.y >= highCut) top = Mathf.Max(top, radius);
            }
            return top > bottom * 1.15f;
        }

        /// <summary>같은 폴더의 텍스처로 URP Lit 재질을 만든다(FBX 안 재질이 텍스처를 못 찾는 경우를 피함). 텍스처가 없으면 null.</summary>
        static Material BuildMaterial(string name)
        {
            var folder = $"{Root}/{name}";
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_BaseColor.png");
            if (baseMap == null) return null;
            var path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) return null;
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", baseMap);
            // 손에 드는 무기는 흰 Meshy 색 그대로면 본체(회색 강철)와 따로 놀아 장난감처럼 보인다(11/13 자동 점검) → 어두운 강철빛으로 누른다.
            material.SetColor("_BaseColor", name.StartsWith("Weapon_") ? new Color(0.5f, 0.52f, 0.56f) : Color.white);
            var normalPath = $"{folder}/{name}_Normal.png";
            if (File.Exists(normalPath))
            {
                var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.EnableKeyword("_NORMALMAP");
            }
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Metallic.png");
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            material.SetFloat("_Smoothness", 0.45f);
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Emission.png");
            if (emission != null)
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", Color.white * 2f);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
