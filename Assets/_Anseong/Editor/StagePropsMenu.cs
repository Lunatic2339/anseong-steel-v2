using System.Collections.Generic;
using System.Linq;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Pilot;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 정비고와 맵 장치를 단순 도형(상자·원통)으로 조립해 씬에 세우고 칸에 연결한다(11/13 "겉만 있다" 보완, 모델이 오기 전 실물).
    /// - 정비고(<see cref="CheckupBay"/>): 조종석 앞을 가린 벽·출격 문 두 짝 + 공 던지는 정비 로봇 + 강철 공 → <see cref="CheckupDirector"/>.
    /// - 크레인(<see cref="CraneRig"/>): 싸움터 위를 가로지르는 들보·대차·매달린 코일 → <see cref="MapEvents"/>.
    /// - 컨베이어 다리(칸마다 <see cref="BreakableProp"/>)·사일로 셋(<see cref="BreakableProp"/>) → <see cref="MapEvents"/>.
    /// - 공이 오는 쪽 글자(XR 카메라 아래), 무장 장착 불꽃(플레이어 로봇 아래).
    /// 자리는 조종사 로봇이 처음 서는 곳 기준(m, +Z = 정면). 마당(X -160~150, Z -270~290) 안, 둘레 구조물(AiModelFit.SceneryTable) 안쪽.
    /// 'Add Match Flow'(그래서 Build M1 Main·Build Full Game)가 부른다. 여러 번 실행하면 지우고 다시 만든다. 씬 저장은 사람이 한다.
    /// 재질은 Art/Environment/Props/*.mat(낡은 도장 강철 셰이더 IndustrialProp, 없으면 URP Lit. 에디터에서만 셰이더를 찾는다), 먼지·불꽃은 VfxBuilder의 파티클 재질.
    /// </summary>
    public static class StagePropsMenu
    {
        const string LogPrefix = "[StagePropsMenu]";
        const string RootName = "Stage Props (정비고·맵 장치)";
        const string MaterialParent = "Assets/_Anseong/Art/Environment";
        const string MaterialFolder = MaterialParent + "/Props";
        const string FacadeMaterialPath = "Assets/_Anseong/Art/Environment/Facade/IndustrialFacade.mat";
        const string SmokeMaterialPath = "Assets/_Anseong/Art/VFX/Materials/VFX_Smoke_Blend.mat";
        const string SparkMaterialPath = "Assets/_Anseong/Art/VFX/Materials/VFX_Spark_Add.mat";
        // 소품 재질 셰이더(낡은 도장 강철). 없으면 URP Lit 단색으로 만든다.
        const string PropShaderPath = MaterialFolder + "/IndustrialProp.shader";

        // 정비고(조종사 로봇 기준 m): 안쪽 반폭, 뒷벽 Z, 문 Z, 벽 높이. 문은 눈높이(약 60 m)에서 120 m 밖 보스를 가릴 만큼 높다.
        const float HangarHalf = 46f, HangarBack = -38f, HangarDoor = 78f, HangarHeight = 86f;
        const float ThrowerZ = 56f;
        // 크레인: 레일 X(좌우), 들보 높이.
        const float CraneRailX = 156f, CraneGirderY = 113f, CraneHomeZ = 250f, CraneHomeX = 70f;
        // 컨베이어 다리: 싸움터 서쪽 옆을 따라(문이 열리는 자리 밖).
        const float ConveyorX = -108f, ConveyorStartZ = 0f, ConveyorBayLength = 20f;
        const int ConveyorBays = 10;
        // 사일로: 보스가 처음 서는 자리(120 m) 뒤.
        const float SiloZ = 215f;
        static readonly float[] SiloX = { -38f, 0f, 38f };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        static Material steel, paint, hazard, hazardStripe, rust, concrete, dark, lamp, wall, coilSteel, smoke, spark;
        static Shader propShader;
        // 원통·공에 쓰는 짝 재질(무늬를 둘레로 감는다). 상자용 재질 → 둥근 것용.
        static readonly Dictionary<Material, Material> RoundOf = new Dictionary<Material, Material>();
        // 이 이름이 들어간 노란 도형은 사선 경고 띠 재질로 바꾼다(넓은 몸통은 그냥 노란 도장).
        static readonly string[] StripeNames = { "Hazard Band", "Stripe", "Bumper", "End Stop", "Tower Band", "Warning Block" };

        [MenuItem("Anseong/Setup/Build Stage Props (정비고·맵 장치)", priority = 5)]
        static void BuildInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            Debug.Log($"{LogPrefix} {Build(scene)}. 씬을 저장하세요.");
        }

        /// <summary>소품을 만들고 칸에 연결한다. 무엇을 했는지 한 줄로 돌려준다.</summary>
        public static string Build(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var mapEvents = roots.SelectMany(r => r.GetComponentsInChildren<MapEvents>(true)).FirstOrDefault();
            var checkup = roots.SelectMany(r => r.GetComponentsInChildren<CheckupDirector>(true)).FirstOrDefault();
            if (mapEvents == null && checkup == null) return "소품: 맵 연출·기체 점검이 없어 만들지 않음(Add Match Flow 먼저)";
            if (!EnsureMaterials()) return "소품: URP Lit 셰이더를 찾지 못해 만들지 않음";

            // 전에 만든 묶음은 지우고 새로 만든다.
            var old = new List<GameObject>();
            if (mapEvents != null && new SerializedObject(mapEvents).FindProperty("propsRoot").objectReferenceValue is GameObject oldRoot) old.Add(oldRoot);
            if (checkup != null && new SerializedObject(checkup).FindProperty("bay").objectReferenceValue is CheckupBay oldBay && oldBay != null)
                old.Add(oldBay.transform.root.gameObject);
            foreach (var go in old.Where(g => g != null).Distinct().ToArray()) Undo.DestroyObjectImmediate(go);

            // 기준: 조종사 로봇이 처음 서는 자리와 방향.
            var arm = roots.SelectMany(r => r.GetComponentsInChildren<RobotArmDriver>(true)).FirstOrDefault();
            var player = arm != null ? arm.transform.root : null;
            var rootGo = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            Undo.RegisterCreatedObjectUndo(rootGo, "Build Stage Props");
            var root = rootGo.transform;
            if (player != null) root.SetPositionAndRotation(new Vector3(player.position.x, player.position.y, player.position.z), Quaternion.Euler(0f, player.eulerAngles.y, 0f));

            var bay = BuildHangar(root);
            var crane = BuildCrane(root);
            var bays = BuildConveyor(root);
            var silos = BuildSilos(root);

            if (checkup != null)
            {
                var s = new SerializedObject(checkup);
                s.FindProperty("bay").objectReferenceValue = bay;
                if (s.FindProperty("ballCallout").objectReferenceValue == null)
                    s.FindProperty("ballCallout").objectReferenceValue = MakeCallout(roots);
                s.ApplyModifiedProperties();
            }
            if (mapEvents != null)
            {
                var s = new SerializedObject(mapEvents);
                s.FindProperty("propsRoot").objectReferenceValue = rootGo;
                s.FindProperty("crane").objectReferenceValue = crane;
                SetArray(s.FindProperty("conveyorBays"), bays);
                SetArray(s.FindProperty("silos"), silos);
                s.ApplyModifiedProperties();
            }
            var sparks = AddMountSparks(roots, player);
            EditorSceneManager.MarkSceneDirty(scene);
            var parts = rootGo.GetComponentsInChildren<MeshRenderer>(true).Length;
            return $"소품: 정비고·정비 로봇 + 크레인·코일 + 컨베이어 {bays.Length}칸 + 사일로 {silos.Length}통(도형 {parts}개, 먼지 재질 {(smoke != null ? "있음" : "없음")}), 장착 불꽃: {sparks}";
        }

        // ── 정비고 ─────────────────────────────────────────────────────────

        static CheckupBay BuildHangar(Transform root)
        {
            var bayRoot = Group(root, "Hangar Bay", Vector3.zero);
            var bay = bayRoot.gameObject.AddComponent<CheckupBay>();
            var walls = Group(bayRoot, "Walls (출격 뒤 내려감)", Vector3.zero);
            var depth = HangarDoor - HangarBack;
            var midZ = (HangarDoor + HangarBack) * 0.5f;

            // 바닥: 콘크리트 판 + 주행선 + 정비 로봇 승강구 테두리.
            Box(walls, "Floor", new Vector3(HangarHalf * 2f, 0.4f, depth), new Vector3(0f, 0.2f, midZ), concrete);
            Box(walls, "Floor Line C", new Vector3(1.2f, 0.06f, depth - 12f), new Vector3(0f, 0.43f, midZ), hazard);
            foreach (var s in Sides)
                Box(walls, "Floor Line", new Vector3(0.8f, 0.06f, depth - 12f), new Vector3(s * 24f, 0.43f, midZ), hazard);
            foreach (var s in Sides)
            {
                Box(walls, "Hatch Frame", new Vector3(1.4f, 0.3f, 27f), new Vector3(s * 12.5f, 0.55f, ThrowerZ), dark);
                Box(walls, "Hatch Frame", new Vector3(26.4f, 0.3f, 1.4f), new Vector3(0f, 0.55f, ThrowerZ + s * 13.5f), dark);
            }

            foreach (var s in Sides)
            {
                var side = Group(walls, s < 0f ? "Wall L" : "Wall R", Vector3.zero);
                Box(side, "Panel", new Vector3(3f, HangarHeight, depth), new Vector3(s * (HangarHalf + 1.5f), HangarHeight * 0.5f, midZ), wall);
                Box(side, "Top Beam", new Vector3(5f, 4f, depth + 4f), new Vector3(s * (HangarHalf + 1f), HangarHeight + 2f, midZ), steel);
                Box(side, "Kerb", new Vector3(2f, 3f, depth), new Vector3(s * (HangarHalf - 0.8f), 1.5f, midZ), concrete);
                Box(side, "Hazard Band", new Vector3(0.6f, 3f, depth), new Vector3(s * (HangarHalf - 0.1f), 7.5f, midZ), hazard);
                // 점검 통로 두 층 + 난간.
                foreach (var y in new[] { 38f, 64f })
                {
                    Box(side, "Catwalk", new Vector3(5f, 0.6f, depth), new Vector3(s * (HangarHalf - 2.5f), y, midZ), steel);
                    Box(side, "Rail Top", new Vector3(0.35f, 0.35f, depth), new Vector3(s * (HangarHalf - 5f), y + 3f, midZ), hazard);
                    Box(side, "Rail Mid", new Vector3(0.25f, 0.25f, depth), new Vector3(s * (HangarHalf - 5f), y + 1.6f, midZ), paint);
                }
                // 기둥 + 천장 골조(벽에서 뻗은 외팔보·가새·작업등). 가운데는 비워 벽이 내려갈 때 기체에 걸리지 않는다.
                for (var z = HangarBack + 4f; z <= HangarDoor - 2f; z += 14.5f)
                {
                    Box(side, "Column", new Vector3(3.5f, HangarHeight, 3f), new Vector3(s * (HangarHalf - 1.2f), HangarHeight * 0.5f, z), steel);
                    Box(side, "Roof Bracket", new Vector3(14f, 3f, 2.4f), new Vector3(s * (HangarHalf - 8.5f), HangarHeight - 1.5f, z), steel);
                    Beam(side, "Bracket Brace", new Vector3(s * (HangarHalf - 3f), HangarHeight - 16f, z), new Vector3(s * (HangarHalf - 14f), HangarHeight - 3f, z), 1.4f, paint);
                    Box(side, "Work Lamp", new Vector3(2.4f, 0.8f, 2.4f), new Vector3(s * (HangarHalf - 13f), HangarHeight - 3.5f, z), lamp);
                    foreach (var y in new[] { 38f, 64f })
                        Box(side, "Rail Post", new Vector3(0.3f, 3f, 0.3f), new Vector3(s * (HangarHalf - 5f), y + 1.5f, z + 7f), paint);
                }
            }

            // 뒷벽.
            Box(walls, "Back Panel", new Vector3(HangarHalf * 2f + 6f, HangarHeight, 3f), new Vector3(0f, HangarHeight * 0.5f, HangarBack - 1.5f), wall);
            Box(walls, "Back Top Beam", new Vector3(HangarHalf * 2f + 10f, 4f, 5f), new Vector3(0f, HangarHeight + 2f, HangarBack - 1f), steel);
            Box(walls, "Back Hazard Band", new Vector3(HangarHalf * 2f, 3f, 0.6f), new Vector3(0f, 7.5f, HangarBack + 0.2f), hazard);
            foreach (var x in new[] { -30f, -10f, 10f, 30f })
                Box(walls, "Back Column", new Vector3(3f, HangarHeight, 3.5f), new Vector3(x, HangarHeight * 0.5f, HangarBack + 1.2f), steel);
            foreach (var y in new[] { 38f, 64f })
                Box(walls, "Back Girt", new Vector3(HangarHalf * 2f, 2f, 1.5f), new Vector3(0f, y, HangarBack + 0.6f), paint);

            // 문틀: 문이 옆으로 밀려 나갈 윗보·바닥 레일(정비고 폭의 두 배) + 끝 기둥.
            var track = HangarHalf * 4f + 12f;
            Box(walls, "Door Header", new Vector3(track, 6f, 6f), new Vector3(0f, HangarHeight + 3f, HangarDoor + 1.5f), steel);
            Box(walls, "Door Header Stripe", new Vector3(HangarHalf * 2f, 1.2f, 0.5f), new Vector3(0f, HangarHeight - 0.6f, HangarDoor - 0.4f), hazard);
            Box(walls, "Door Sill", new Vector3(track, 0.5f, 5f), new Vector3(0f, 0.25f, HangarDoor + 1.5f), steel);
            foreach (var s in Sides)
                Box(walls, "Header Post", new Vector3(4f, HangarHeight + 6f, 6f), new Vector3(s * (track * 0.5f - 2f), (HangarHeight + 6f) * 0.5f, HangarDoor + 1.5f), steel);

            // 문 두 짝(안쪽 면에 보강재·가로띠·경고 띠·이음매 등).
            var leafWidth = HangarHalf + 0.4f;
            var doors = new Transform[2];
            for (var d = 0; d < 2; d++)
            {
                var s = Sides[d];
                var door = Group(walls, s < 0f ? "Door L" : "Door R", new Vector3(s * leafWidth * 0.5f, 0f, HangarDoor + 1.25f));
                doors[d] = door;
                Box(door, "Leaf", new Vector3(leafWidth, HangarHeight, 2.2f), new Vector3(0f, HangarHeight * 0.5f, 0f), paint);
                foreach (var y in new[] { 14f, 32f, 50f, 68f })
                    Box(door, "Girt", new Vector3(leafWidth, 1.6f, 0.8f), new Vector3(0f, y, -1.4f), steel);
                foreach (var x in new[] { -17f, -6f, 6f, 17f })
                    Box(door, "Stiffener", new Vector3(1.2f, HangarHeight, 0.7f), new Vector3(x, HangarHeight * 0.5f, -1.35f), steel);
                const int blocks = 8;
                for (var k = 0; k < blocks; k++)
                    Box(door, "Warning Block", new Vector3(leafWidth / blocks - 0.3f, 5f, 0.5f),
                        new Vector3(-leafWidth * 0.5f + (k + 0.5f) * leafWidth / blocks, 4f, -1.5f), k % 2 == 0 ? hazard : dark);
                Box(door, "Seam Lamp", new Vector3(1f, 10f, 0.6f), new Vector3(-s * (leafWidth * 0.5f - 1.4f), 46f, -1.5f), lamp);
                Box(door, "Roller Housing", new Vector3(leafWidth, 2.5f, 3.2f), new Vector3(0f, HangarHeight - 1.25f, 0f), dark);
            }
            var doorCentre = Group(bayRoot, "Door Centre", new Vector3(0f, HangarHeight * 0.5f, HangarDoor));

            // 먼지(내려가지 않는 묶음).
            var fx = Group(bayRoot, "Fx", Vector3.zero);
            var dustColor = new Color(0.56f, 0.52f, 0.46f, 0.5f);
            var doorFx = new List<Object>
            {
                Dust(fx, "Door Dust", new Vector3(0f, 2f, HangarDoor + 1f), new Vector3(HangarHalf * 2f, 2f, 6f), 26, 8f, 16f, 6f, 3.5f, dustColor, 0f, 1.2f),
            };
            var sinkFx = new List<Object>
            {
                Dust(fx, "Sink Dust L", new Vector3(-(HangarHalf + 1.5f), 2f, midZ), new Vector3(6f, 2f, depth), 26, 9f, 18f, 7f, 4f, dustColor, 0f, 1.5f, 3f, 4.5f),
                Dust(fx, "Sink Dust R", new Vector3(HangarHalf + 1.5f, 2f, midZ), new Vector3(6f, 2f, depth), 26, 9f, 18f, 7f, 4f, dustColor, 0f, 1.5f, 3f, 4.5f),
                Dust(fx, "Sink Dust Back", new Vector3(0f, 2f, HangarBack), new Vector3(HangarHalf * 2f, 2f, 6f), 20, 9f, 18f, 7f, 4f, dustColor, 0f, 2f, 4f),
                Dust(fx, "Sink Dust Door", new Vector3(0f, 2f, HangarDoor + 1f), new Vector3(HangarHalf * 4f, 2f, 6f), 30, 9f, 18f, 7f, 4f, dustColor, 0f, 2f, 4f),
            };

            // 정비 로봇: 무한궤도 대차 + 격자 기둥 + 도는 포탑 + 던지는 팔. 로컬 +Z가 조종사 쪽을 보게 돌려 세운다.
            var thrower = Group(bayRoot, "Maintenance Robot", new Vector3(0f, 0f, ThrowerZ));
            thrower.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var lift = Group(thrower, "Lift", Vector3.zero);
            Box(lift, "Carriage", new Vector3(16f, 4f, 20f), new Vector3(0f, 2.6f, 0f), steel);
            foreach (var s in Sides)
            {
                Box(lift, "Track Pod", new Vector3(3.2f, 3.6f, 23f), new Vector3(s * 8.6f, 1.8f, 0f), dark);
                Box(lift, "Bumper", new Vector3(16f, 1.6f, 0.8f), new Vector3(0f, 2.6f, s * 10.3f), hazard);
            }
            foreach (var sx in Sides)
            foreach (var sz in Sides)
                Box(lift, "Mast Column", new Vector3(1.3f, 30f, 1.3f), new Vector3(sx * 3.2f, 19.6f, sz * 3.2f), paint);
            foreach (var y in new[] { 12f, 20f, 28f })
            foreach (var s in Sides)
            {
                Box(lift, "Mast Ring", new Vector3(7.6f, 0.8f, 0.8f), new Vector3(0f, y, s * 3.2f), paint);
                Box(lift, "Mast Ring", new Vector3(0.8f, 0.8f, 7.6f), new Vector3(s * 3.2f, y, 0f), paint);
            }
            for (var k = 0; k < 3; k++)
            {
                float y0 = 5f + k * 8f, y1 = y0 + 8f;
                var flip = k % 2 == 0 ? 1f : -1f;
                foreach (var s in Sides)
                {
                    Beam(lift, "Mast Brace", new Vector3(-3.2f * flip, y0, s * 3.2f), new Vector3(3.2f * flip, y1, s * 3.2f), 0.6f, steel);
                    Beam(lift, "Mast Brace", new Vector3(s * 3.2f, y0, -3.2f * flip), new Vector3(s * 3.2f, y1, 3.2f * flip), 0.6f, steel);
                }
            }
            Box(lift, "Mast Deck", new Vector3(11f, 1f, 11f), new Vector3(0f, 34.5f, 0f), steel);
            foreach (var s in Sides)
            {
                Box(lift, "Deck Rail", new Vector3(11f, 0.3f, 0.3f), new Vector3(0f, 37f, s * 5.3f), hazard);
                Box(lift, "Deck Rail", new Vector3(0.3f, 0.3f, 11f), new Vector3(s * 5.3f, 37f, 0f), hazard);
                foreach (var sz in Sides)
                    Box(lift, "Deck Post", new Vector3(0.3f, 2.2f, 0.3f), new Vector3(s * 5.3f, 36f, sz * 5.3f), hazard);
            }
            var turret = Group(lift, "Turret", new Vector3(0f, 35f, 0f));
            Cyl(turret, "Slew Ring", 8f, 2f, new Vector3(0f, 1f, 0f), dark);
            Box(turret, "Machine House", new Vector3(6.4f, 5f, 8f), new Vector3(0f, 4.5f, -1.5f), hazard);
            Box(turret, "Window", new Vector3(4.4f, 1.6f, 0.3f), new Vector3(0f, 5.4f, 2.55f), dark);
            Box(turret, "Counterweight", new Vector3(5.4f, 3.2f, 4f), new Vector3(0f, 3.6f, -7.4f), dark);
            Box(turret, "Beacon", new Vector3(1f, 1f, 1f), new Vector3(0f, 7.5f, -1.5f), lamp);
            var throwArm = Group(turret, "Arm", new Vector3(0f, 5f, 3.2f));
            Box(throwArm, "Boom", new Vector3(1.8f, 1.8f, 14f), new Vector3(0f, 0f, 7f), hazard);
            foreach (var s in Sides)
                Beam(throwArm, "Boom Stay", new Vector3(s * 1.6f, 2.2f, 0.5f), new Vector3(s * 0.9f, 0.9f, 12f), 0.4f, steel);
            Box(throwArm, "Boom Stripe", new Vector3(1.9f, 1.9f, 1.2f), new Vector3(0f, 0f, 11f), dark);
            Cyl(throwArm, "Cup", 6.6f, 1f, new Vector3(0f, 0.4f, 14.5f), dark);
            for (var k = 0; k < 3; k++)
            {
                var a = k * 120f * Mathf.Deg2Rad;
                Box(throwArm, "Cup Claw", new Vector3(0.8f, 2.6f, 0.8f), new Vector3(Mathf.Sin(a) * 3f, 1.8f, 14.5f + Mathf.Cos(a) * 3f), steel);
            }
            var ballRest = Group(throwArm, "Ball Rest", new Vector3(0f, 3.4f, 14.5f));

            // 강철 공(지름 5 m) + 띠 둘.
            var ball = Group(bayRoot, "Steel Ball", new Vector3(0f, 44f, ThrowerZ - 14f));
            Prim(PrimitiveType.Sphere, ball, "Ball", new Vector3(5f, 5f, 5f), Vector3.zero, Quaternion.identity, coilSteel);
            Cyl(ball, "Band", 5.15f, 0.5f, Vector3.zero, dark);
            Cyl(ball, "Band", 5.15f, 0.5f, Vector3.zero, dark, new Vector3(0f, 0f, 90f));
            var blockFx = Sparks(fx, "Ball Block Sparks", 46, 14f, 34f, 0.5f, 1.1f, 0.9f, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.55f, 0.2f));
            var missFx = Dust(fx, "Ball Miss Dust", Vector3.zero, new Vector3(5f, 5f, 5f), 12, 4f, 8f, 5f, 1.6f, new Color(0.4f, 0.4f, 0.42f, 0.5f), 0f);

            var so = new SerializedObject(bay);
            so.FindProperty("walls").objectReferenceValue = walls;
            so.FindProperty("leftDoor").objectReferenceValue = doors[0];
            so.FindProperty("rightDoor").objectReferenceValue = doors[1];
            so.FindProperty("doorCenter").objectReferenceValue = doorCentre;
            SetArray(so.FindProperty("doorFx"), doorFx);
            SetArray(so.FindProperty("sinkFx"), sinkFx);
            so.FindProperty("doorTravel").floatValue = leafWidth + 0.6f;
            so.FindProperty("lift").objectReferenceValue = lift;
            so.FindProperty("turret").objectReferenceValue = turret;
            so.FindProperty("arm").objectReferenceValue = throwArm;
            so.FindProperty("ballRest").objectReferenceValue = ballRest;
            so.FindProperty("ball").objectReferenceValue = ball;
            so.FindProperty("blockFx").objectReferenceValue = blockFx;
            so.FindProperty("missFx").objectReferenceValue = missFx;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bay;
        }

        // ── 크레인 ─────────────────────────────────────────────────────────

        static CraneRig BuildCrane(Transform root)
        {
            var craneRoot = Group(root, "Yard Crane", Vector3.zero);
            var rig = craneRoot.gameObject.AddComponent<CraneRig>();
            const float railFrom = -29f, railTo = 271f;
            foreach (var s in Sides)
            {
                Box(craneRoot, "Rail", new Vector3(3f, 1.2f, railTo - railFrom), new Vector3(s * CraneRailX, 0.6f, (railFrom + railTo) * 0.5f), dark);
                Box(craneRoot, "Rail Bed", new Vector3(8f, 0.5f, railTo - railFrom), new Vector3(s * CraneRailX, 0.25f, (railFrom + railTo) * 0.5f), concrete);
                Box(craneRoot, "End Stop", new Vector3(6f, 6f, 3f), new Vector3(s * CraneRailX, 3f, railFrom), hazard);
                Box(craneRoot, "End Stop", new Vector3(6f, 6f, 3f), new Vector3(s * CraneRailX, 3f, railTo), hazard);
            }

            var bridge = Group(craneRoot, "Bridge", new Vector3(0f, 0f, CraneHomeZ));
            var legTop = CraneGirderY - 5f;
            foreach (var s in Sides)
            {
                var x = s * CraneRailX;
                Box(bridge, "Bogie", new Vector3(7f, 5f, 36f), new Vector3(x, 3.7f, 0f), dark);
                Beam(bridge, "Leg", new Vector3(x, 5f, -15f), new Vector3(x, legTop, -4f), 5f, rust);
                Beam(bridge, "Leg", new Vector3(x, 5f, 15f), new Vector3(x, legTop, 4f), 5f, rust);
                Box(bridge, "Leg Tie", new Vector3(4f, 3f, 24f), new Vector3(x, 38f, 0f), rust);
                Box(bridge, "Leg Tie", new Vector3(4f, 3f, 16f), new Vector3(x, 74f, 0f), rust);
                Beam(bridge, "Leg Brace", new Vector3(x, 7f, -14.5f), new Vector3(x, 37f, 11.5f), 1.6f, paint);
                Beam(bridge, "Leg Brace", new Vector3(x, 7f, 14.5f), new Vector3(x, 37f, -11.5f), 1.6f, paint);
                Beam(bridge, "Leg Brace", new Vector3(x, 39f, -11.5f), new Vector3(x, 73f, 7.6f), 1.4f, paint);
                Beam(bridge, "Leg Brace", new Vector3(x, 39f, 11.5f), new Vector3(x, 73f, -7.6f), 1.4f, paint);
                Box(bridge, "End Tie", new Vector3(8f, 10f, 16f), new Vector3(x, CraneGirderY, 0f), rust);
                Box(bridge, "Lift Shaft", new Vector3(1.6f, legTop - 6f, 1.6f), new Vector3(s * (CraneRailX - 3.6f), (legTop + 6f) * 0.5f, 0f), paint);
                Box(bridge, "Warning Lamp", new Vector3(2f, 2f, 2f), new Vector3(x, CraneGirderY + 6f, 0f), lamp);
            }
            var span = CraneRailX * 2f + 6f;
            foreach (var s in Sides)
            {
                Box(bridge, "Girder", new Vector3(span, 9f, 4f), new Vector3(0f, CraneGirderY, s * 5f), rust);
                Box(bridge, "Girder Stripe", new Vector3(span, 1f, 4.3f), new Vector3(0f, CraneGirderY - 4.3f, s * 5f), hazard);
                Box(bridge, "Girder Rail", new Vector3(span, 0.4f, 0.4f), new Vector3(0f, CraneGirderY + 6.9f, s * 7.6f), paint);
                Box(bridge, "Girder Walk", new Vector3(span, 0.4f, 1.6f), new Vector3(0f, CraneGirderY + 4.4f, s * 7.6f), steel);
                for (var x = -span * 0.5f + 12f; x < span * 0.5f; x += 26f)
                    Box(bridge, "Girder Post", new Vector3(0.4f, 2.4f, 0.4f), new Vector3(x, CraneGirderY + 5.7f, s * 7.6f), paint);
            }
            Box(bridge, "Cab", new Vector3(7f, 6f, 8f), new Vector3(-CraneRailX + 16f, CraneGirderY - 8f, 0f), hazard);
            Box(bridge, "Cab Window", new Vector3(7.2f, 2f, 6f), new Vector3(-CraneRailX + 16f, CraneGirderY - 7.4f, 0f), dark);

            // 대차 + 고리 + 줄.
            var trolley = Group(bridge, "Trolley", new Vector3(CraneHomeX, CraneGirderY + 5.6f, 0f));
            Box(trolley, "Frame", new Vector3(16f, 2.5f, 16f), new Vector3(0f, 0.25f, 0f), dark);
            Box(trolley, "Hoist House", new Vector3(10f, 6f, 9f), new Vector3(0f, 4.5f, 0f), hazard);
            Box(trolley, "House Roof", new Vector3(11f, 0.6f, 10f), new Vector3(0f, 7.8f, 0f), dark);
            Cyl(trolley, "Drum", 3.6f, 10f, new Vector3(0f, 1.6f, 0f), steel, new Vector3(0f, 0f, 90f));
            foreach (var sx in Sides)
            foreach (var sz in Sides)
                Beam(trolley, "Hoist Cable", new Vector3(sx * 3f, -1f, sz * 1.8f), new Vector3(sx * 3f, -13.8f, sz * 1.8f), 0.45f, dark);
            var hook = Group(trolley, "Hook", new Vector3(0f, -16f, 0f));
            Box(hook, "Block", new Vector3(9f, 4.5f, 5f), Vector3.zero, hazard);
            Box(hook, "Block Stripe", new Vector3(9.1f, 1f, 5.1f), Vector3.zero, dark);
            Box(hook, "Spreader", new Vector3(12.5f, 1.6f, 1.8f), new Vector3(0f, -3.4f, 0f), steel);
            var beacon = Group(hook, "Beacon", Vector3.zero);
            foreach (var s in Sides)
            {
                Box(beacon, "Lamp", new Vector3(1.8f, 1.8f, 1.8f), new Vector3(s * 5.4f, 0.4f, 0f), lamp);
                Box(beacon, "Lamp", new Vector3(1.8f, 1.8f, 1.8f), new Vector3(0f, 0.4f, s * 3.4f), lamp);
            }
            const float hang = -30.5f;
            var slings = Group(trolley, "Slings", Vector3.zero);
            foreach (var s in Sides)
            {
                Beam(slings, "Sling", new Vector3(s * 5.4f, -20f, 0f), new Vector3(s * 5.4f, hang + 1.5f, 0f), 0.6f, dark);
                Box(slings, "Sling Plate", new Vector3(0.8f, 4f, 4f), new Vector3(s * 5.4f, hang, 0f), steel);
            }
            Cyl(slings, "Sling Pin", 1.6f, 11.2f, new Vector3(0f, hang, 0f), steel, new Vector3(0f, 0f, 90f));
            var hangPoint = Group(trolley, "Hang Point", new Vector3(0f, hang, 0f));

            // 강철 코일(지름 14 m, 폭 9 m, 축 = 로컬 X).
            var coil = Group(craneRoot, "Steel Coil", new Vector3(CraneHomeX, CraneGirderY + 5.6f + hang, CraneHomeZ));
            Cyl(coil, "Coil", 14f, 9f, Vector3.zero, coilSteel, new Vector3(0f, 0f, 90f));
            Cyl(coil, "Eye", 5.2f, 9.15f, Vector3.zero, dark, new Vector3(0f, 0f, 90f));
            foreach (var x in new[] { -3f, 0f, 3f })
                Cyl(coil, "Strap", 14.25f, 0.5f, new Vector3(x, 0f, 0f), dark, new Vector3(0f, 0f, 90f));

            var fx = Group(craneRoot, "Fx", Vector3.zero);
            var impact = Dust(fx, "Coil Impact", Vector3.zero, new Vector3(16f, 6f, 16f), 30, 10f, 22f, 16f, 3.5f, new Color(0.5f, 0.47f, 0.43f, 0.55f), 0f);
            if (impact != null)
                Sparks(impact.transform, "Sparks", 70, 20f, 55f, 0.6f, 1.6f, 1.2f, new Color(1f, 0.92f, 0.6f), new Color(1f, 0.5f, 0.15f), 0f);
            var land = Dust(fx, "Coil Land", Vector3.zero, new Vector3(22f, 3f, 22f), 34, 12f, 26f, 14f, 4.5f, new Color(0.56f, 0.52f, 0.46f, 0.55f), 0f, 0.6f);

            var so = new SerializedObject(rig);
            so.FindProperty("bridge").objectReferenceValue = bridge;
            so.FindProperty("trolley").objectReferenceValue = trolley;
            so.FindProperty("hangPoint").objectReferenceValue = hangPoint;
            so.FindProperty("hook").objectReferenceValue = hook;
            so.FindProperty("coil").objectReferenceValue = coil;
            so.FindProperty("slings").objectReferenceValue = slings.gameObject;
            so.FindProperty("beacon").objectReferenceValue = beacon.gameObject;
            so.FindProperty("impactFx").objectReferenceValue = impact;
            so.FindProperty("landFx").objectReferenceValue = land;
            so.FindProperty("homeZ").floatValue = CraneHomeZ;
            so.FindProperty("homeX").floatValue = CraneHomeX;
            so.FindProperty("bridgeRange").vector2Value = new Vector2(railFrom + 20f, railTo - 20f);
            so.FindProperty("trolleyRange").vector2Value = new Vector2(-CraneRailX + 26f, CraneRailX - 26f);
            so.ApplyModifiedPropertiesWithoutUndo();
            beacon.gameObject.SetActive(false);
            return rig;
        }

        // ── 컨베이어 다리 ──────────────────────────────────────────────────

        static Object[] BuildConveyor(Transform root)
        {
            var line = Group(root, "Conveyor Bridge", new Vector3(ConveyorX, 0f, 0f));
            var random = new System.Random(4107);
            var result = new List<Object>();
            var length = ConveyorBayLength;
            for (var i = 0; i < ConveyorBays; i++)
            {
                var bayRoot = Group(line, "Bay " + (i + 1), new Vector3(0f, 0f, ConveyorStartZ + length * (i + 0.5f)));
                var prop = bayRoot.gameObject.AddComponent<BreakableProp>();
                var body = Group(bayRoot, "Body", Vector3.zero);
                // 받침 다리(A자 골조).
                foreach (var s in Sides)
                {
                    Box(body, "Footing", new Vector3(5f, 1.6f, 5f), new Vector3(s * 7f, 0.8f, 0f), concrete);
                    Beam(body, "Leg", new Vector3(s * 7f, 1.6f, 0f), new Vector3(s * 3.6f, 32f, 0f), 2.2f, paint);
                }
                Box(body, "Leg Tie", new Vector3(11.4f, 1f, 1f), new Vector3(0f, 17f, 0f), steel);
                Beam(body, "Leg Brace", new Vector3(-6.3f, 5f, 0f), new Vector3(4.2f, 29f, 0f), 0.9f, steel);
                Beam(body, "Leg Brace", new Vector3(6.3f, 5f, 0f), new Vector3(-4.2f, 29f, 0f), 0.9f, steel);
                Box(body, "Cap", new Vector3(10f, 1.6f, 2.6f), new Vector3(0f, 32.2f, 0f), steel);
                // 벨트 통로(닫힌 통로 + 창 띠 + 경고 띠 + 옆 통로·난간).
                Box(body, "Floor Truss", new Vector3(7.4f, 1.2f, length), new Vector3(0f, 33.5f, 0f), steel);
                Box(body, "Gallery", new Vector3(7f, 5f, length - 0.3f), new Vector3(0f, 36.6f, 0f), paint);
                Box(body, "Window Band", new Vector3(7.15f, 1.1f, length - 4f), new Vector3(0f, 37.4f, 0f), dark);
                Box(body, "Hazard Band", new Vector3(7.2f, 0.5f, length - 0.3f), new Vector3(0f, 34.5f, 0f), hazard);
                Box(body, "Walkway", new Vector3(1.8f, 0.3f, length), new Vector3(4.5f, 34.2f, 0f), steel);
                Box(body, "Walk Rail", new Vector3(0.25f, 0.25f, length), new Vector3(5.3f, 35.8f, 0f), hazard);
                foreach (var z in new[] { -length * 0.33f, 0f, length * 0.33f })
                    Box(body, "Walk Post", new Vector3(0.25f, 1.5f, 0.25f), new Vector3(5.3f, 35f, z), hazard);

                // 떨어져 나가는 조각: 지붕 두 장, 옆판 두 장, 쏟아지는 광석 덩이.
                var pieces = new List<Object>
                {
                    Box(body, "Roof A", new Vector3(7.8f, 0.5f, length * 0.5f - 0.3f), new Vector3(0f, 39.35f, -length * 0.25f), rust),
                    Box(body, "Roof B", new Vector3(7.8f, 0.5f, length * 0.5f - 0.3f), new Vector3(0f, 39.35f, length * 0.25f), rust),
                    Box(body, "Side Panel", new Vector3(0.4f, 4.4f, 8f), new Vector3(3.72f, 36.6f, -4.5f), paint),
                    Box(body, "Side Panel", new Vector3(0.4f, 4.4f, 8f), new Vector3(-3.72f, 36.6f, 4.5f), paint),
                };
                for (var k = 0; k < 5; k++)
                {
                    var size = 1.8f + (float)random.NextDouble() * 1.6f;
                    var at = new Vector3(((float)random.NextDouble() - 0.5f) * 3.4f, 35.4f + (float)random.NextDouble() * 1.4f, ((float)random.NextDouble() - 0.5f) * (length - 5f));
                    pieces.Add(Box(body, "Ore", new Vector3(size, size * 0.8f, size * 1.1f), at, dark));
                }
                var dust = Dust(bayRoot, "Dust", new Vector3(0f, 33f, 0f), new Vector3(12f, 8f, length), 22, 9f, 18f, 9f, 4f, new Color(0.5f, 0.46f, 0.42f, 0.55f), 0f, 0.5f);
                if (dust != null)
                    Sparks(dust.transform, "Sparks", 30, 12f, 30f, 0.5f, 1.3f, 0.9f, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.5f, 0.15f), 0f);

                var so = new SerializedObject(prop);
                so.FindProperty("body").objectReferenceValue = body;
                SetArray(so.FindProperty("pieces"), pieces);
                SetArray(so.FindProperty("dust"), new List<Object> { dust });
                so.FindProperty("tiltDegrees").floatValue = 15f;
                so.FindProperty("squash").vector3Value = new Vector3(1f, 0.86f, 1f);
                so.FindProperty("sagMeters").floatValue = 8f;
                so.FindProperty("pieceSpeed").floatValue = 12f;
                so.FindProperty("pieceUp").floatValue = 5f;
                so.ApplyModifiedPropertiesWithoutUndo();
                result.Add(prop);
            }
            // 양 끝 환적탑(부서지지 않음).
            foreach (var z in new[] { ConveyorStartZ - 9f, ConveyorStartZ + length * ConveyorBays + 9f })
            {
                Box(line, "Transfer Tower", new Vector3(16f, 52f, 16f), new Vector3(0f, 26f, z), wall);
                Box(line, "Tower Roof", new Vector3(17.4f, 1f, 17.4f), new Vector3(0f, 52.5f, z), rust);
                Box(line, "Tower Band", new Vector3(16.3f, 2f, 16.3f), new Vector3(0f, 6f, z), hazard);
            }
            return result.ToArray();
        }

        // ── 사일로 ─────────────────────────────────────────────────────────

        static Object[] BuildSilos(Transform root)
        {
            var group = Group(root, "Silos", new Vector3(0f, 0f, SiloZ));
            Box(group, "Plinth", new Vector3(SiloX[SiloX.Length - 1] - SiloX[0] + 40f, 2f, 42f), new Vector3((SiloX[0] + SiloX[SiloX.Length - 1]) * 0.5f, 1f, 0f), concrete);
            var random = new System.Random(8815);
            var result = new List<Object>();
            for (var i = 0; i < SiloX.Length; i++)
            {
                var siloRoot = Group(group, "Silo " + (i + 1), new Vector3(SiloX[i], 2f, 0f));
                var prop = siloRoot.gameObject.AddComponent<BreakableProp>();
                var body = Group(siloRoot, "Body", Vector3.zero);
                Cyl(body, "Base", 32f, 3f, new Vector3(0f, 1.5f, 0f), concrete);
                Cyl(body, "Shell", 30f, 62f, new Vector3(0f, 34f, 0f), paint);
                Cyl(body, "Hazard Band", 30.3f, 2.5f, new Vector3(0f, 6f, 0f), hazard);
                foreach (var y in new[] { 12f, 24f, 36f, 48f, 60f })
                    Cyl(body, "Hoop", 30.5f, 0.9f, new Vector3(0f, y, 0f), steel);
                Cyl(body, "Roof Lip", 31f, 0.6f, new Vector3(0f, 65.3f, 0f), steel);
                Cyl(body, "Roof 1", 27f, 3f, new Vector3(0f, 67f, 0f), steel);
                Cyl(body, "Roof 2", 19f, 3f, new Vector3(0f, 70f, 0f), steel);
                // 앞면(조종사 쪽 = -Z) 계단탑·층계참·배관.
                Box(body, "Stair Tower", new Vector3(2f, 62f, 1.2f), new Vector3(0f, 34f, -15.8f), dark);
                foreach (var y in new[] { 18f, 34f, 50f })
                {
                    Box(body, "Landing", new Vector3(6f, 0.4f, 3f), new Vector3(0f, y, -16.8f), steel);
                    Box(body, "Landing Rail", new Vector3(6f, 0.3f, 0.3f), new Vector3(0f, y + 2.2f, -18.2f), hazard);
                }
                Cyl(body, "Pipe", 1.8f, 62f, new Vector3(9.4f, 34f, -12.6f), rust);

                // 떨어져 나가는 조각: 덧댄 철판 넷, 지붕 꼭대기·환기구, 쏟아지는 덩이.
                var pieces = new List<Object>();
                var angles = new[] { -42f, -17f, 14f, 38f };
                var heights = new[] { 24f, 46f, 30f, 54f };
                for (var k = 0; k < angles.Length; k++)
                {
                    var a = angles[k] * Mathf.Deg2Rad;
                    var outward = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
                    pieces.Add(Prim(PrimitiveType.Cube, body, "Patch Plate", new Vector3(8f, 10f, 0.5f), outward * 15.15f + Vector3.up * heights[k],
                        Quaternion.LookRotation(outward, Vector3.up), rust));
                }
                pieces.Add(Cyl(body, "Roof 3", 10f, 3f, new Vector3(0f, 73f, 0f), steel));
                pieces.Add(Cyl(body, "Vent", 3f, 3.5f, new Vector3(0f, 76f, 0f), dark));
                for (var k = 0; k < 5; k++)
                {
                    var size = 2.4f + (float)random.NextDouble() * 2.2f;
                    var at = new Vector3(((float)random.NextDouble() - 0.5f) * 12f, 20f + (float)random.NextDouble() * 28f, -4f - (float)random.NextDouble() * 6f);
                    pieces.Add(Box(body, "Chunk", new Vector3(size, size * 0.8f, size * 1.1f), at, dark));
                }
                var dustColor = new Color(0.6f, 0.57f, 0.5f, 0.55f);
                var dust = Dust(siloRoot, "Dust", new Vector3(0f, 34f, -13f), new Vector3(28f, 48f, 8f), 34, 12f, 24f, 12f, 5f, dustColor, 0f, 0.5f);
                var baseDust = Dust(siloRoot, "Base Dust", new Vector3(0f, 3f, -15f), new Vector3(34f, 3f, 12f), 22, 12f, 24f, 10f, 5f, dustColor, 0.3f, 1.1f);
                if (dust != null)
                    Sparks(dust.transform, "Sparks", 40, 14f, 36f, 0.6f, 1.5f, 1f, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.5f, 0.15f), 0f);

                var so = new SerializedObject(prop);
                so.FindProperty("body").objectReferenceValue = body;
                SetArray(so.FindProperty("pieces"), pieces);
                SetArray(so.FindProperty("dust"), new List<Object> { dust, baseDust });
                so.FindProperty("tiltDegrees").floatValue = 9f;
                so.FindProperty("squash").vector3Value = new Vector3(1.07f, 0.93f, 1.07f);
                so.FindProperty("sagMeters").floatValue = 0f;
                so.FindProperty("pieceSpeed").floatValue = 15f;
                so.FindProperty("pieceUp").floatValue = 9f;
                so.FindProperty("piecesFlyBack").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                result.Add(prop);
            }
            return result.ToArray();
        }

        // ── 글자·불꽃 ──────────────────────────────────────────────────────

        /// <summary>공이 오는 쪽 글자(XR 카메라 아래). 오른쪽 조종사 PC에서만 CheckupDirector가 켠다.</summary>
        static TextMesh MakeCallout(GameObject[] roots)
        {
            var origin = roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            var camera = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            if (camera == null) return null;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("CheckupBallCallout");
            Undo.RegisterCreatedObjectUndo(go, "Build Stage Props");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = new Vector3(0.17f, 0.04f, 0.9f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one / Mathf.Max(1e-6f, camera.lossyScale.x);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.004f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.richText = true;
            text.color = new Color(1f, 0.8f, 0.2f);
            text.text = string.Empty;
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }

        /// <summary>무장이 끼워지는 순간의 불꽃(플레이어 로봇 아래 파티클 하나, PlayerWeaponVisuals가 자리를 옮겨 터뜨린다).</summary>
        static string AddMountSparks(GameObject[] roots, Transform player)
        {
            var visuals = roots.SelectMany(r => r.GetComponentsInChildren<PlayerWeaponVisuals>(true)).FirstOrDefault();
            if (visuals == null) return "우리 무기가 없어 안 만듦";
            var so = new SerializedObject(visuals);
            var slot = so.FindProperty("mountSparks");
            if (slot.objectReferenceValue != null) return "있던 것";
            var sparks = Sparks(player != null ? player : visuals.transform, "Weapon Mount Sparks", 0, 10f, 26f, 0.35f, 0.8f, 0.7f, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.6f, 0.2f));
            if (sparks == null) return "불꽃 재질이 없어 안 만듦(Build Combat VFX 뒤 다시 실행)";
            Undo.RegisterCreatedObjectUndo(sparks.gameObject, "Build Stage Props");
            slot.objectReferenceValue = sparks;
            so.ApplyModifiedProperties();
            return "새로 만듦";
        }

        // ── 도형 ───────────────────────────────────────────────────────────

        static readonly float[] Sides = { -1f, 1f };

        static Transform Group(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 scale, Vector3 localPosition, Quaternion localRotation, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = scale;
            if (material == hazard && hazardStripe != null && StripeNames.Any(name.Contains)) material = hazardStripe;
            if (type != PrimitiveType.Cube && material != null && RoundOf.TryGetValue(material, out var round)) material = round;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        static Transform Box(Transform parent, string name, Vector3 size, Vector3 localPosition, Material material) =>
            Prim(PrimitiveType.Cube, parent, name, size, localPosition, Quaternion.identity, material);

        /// <summary>원통(축 = 로컬 Y, euler로 눕힌다). diameter·length는 m.</summary>
        static Transform Cyl(Transform parent, string name, float diameter, float length, Vector3 localPosition, Material material, Vector3 euler = default) =>
            Prim(PrimitiveType.Cylinder, parent, name, new Vector3(diameter, length * 0.5f, diameter), localPosition, Quaternion.Euler(euler), material);

        /// <summary>a에서 b까지 가는 각재(비스듬한 다리·가새·줄).</summary>
        static Transform Beam(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var along = b - a;
            var length = along.magnitude;
            var direction = length > 1e-4f ? along / length : Vector3.forward;
            var up = Mathf.Abs(direction.y) > 0.99f ? Vector3.forward : Vector3.up;
            return Prim(PrimitiveType.Cube, parent, name, new Vector3(thickness, thickness, length), (a + b) * 0.5f, Quaternion.LookRotation(direction, up), material);
        }

        static void SetArray(SerializedProperty property, IList<Object> items)
        {
            var kept = items.Where(o => o != null).ToList();
            property.arraySize = kept.Count;
            for (var i = 0; i < kept.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
        }

        // ── 파티클 ─────────────────────────────────────────────────────────

        /// <summary>먼지 구름(상자 모양에서 사방으로 피어오름). burstTimes = 터지는 시각들(초). 재질이 없으면 null.</summary>
        static ParticleSystem Dust(Transform parent, string name, Vector3 localPosition, Vector3 box, int count, float sizeMin, float sizeMax,
            float speed, float life, Color color, params float[] burstTimes)
        {
            if (smoke == null) return null;
            var ps = NewParticles(parent, name, localPosition, smoke, count, burstTimes, life);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.gravityModifier = -0.01f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            shape.randomDirectionAmount = 1f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.06f;
            limit.limit = speed * 0.25f;
            var overLife = ps.colorOverLifetime;
            overLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            overLife.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.7f)));
            return ps;
        }

        /// <summary>불꽃(길게 늘어난 점들이 사방으로 튀어 떨어짐). count 0이면 Emit으로만 쓴다. 재질이 없으면 null.</summary>
        static ParticleSystem Sparks(Transform parent, string name, int count, float speedMin, float speedMax, float sizeMin, float sizeMax,
            float life, Color from, Color to, params float[] burstTimes)
        {
            if (spark == null) return null;
            if (count > 0 && (burstTimes == null || burstTimes.Length == 0)) burstTimes = new[] { 0f };
            var ps = NewParticles(parent, name, Vector3.zero, spark, count, count > 0 ? burstTimes : new float[0], life);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(from, to);
            main.gravityModifier = 1.5f;
            main.maxParticles = Mathf.Max(main.maxParticles, 200);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.5f;
            var overLife = ps.colorOverLifetime;
            overLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            overLife.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 2f;
            return ps;
        }

        static ParticleSystem NewParticles(Transform parent, string name, Vector3 localPosition, Material material, int count, float[] burstTimes, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var last = burstTimes != null && burstTimes.Length > 0 ? burstTimes.Max() : 0f;
            var main = ps.main;
            main.duration = Mathf.Max(0.5f, last + 0.5f);
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(16, count * Mathf.Max(1, burstTimes != null ? burstTimes.Length : 1) * 2);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            if (burstTimes != null && burstTimes.Length > 0 && count > 0)
                emission.SetBursts(burstTimes.Select(t => new ParticleSystem.Burst(t, (short)count)).ToArray());
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ── 재질 ───────────────────────────────────────────────────────────

        static bool EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialParent)) AssetDatabase.CreateFolder("Assets/_Anseong/Art", "Environment");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(MaterialParent, "Props");
            propShader = AssetDatabase.LoadAssetAtPath<Shader>(PropShaderPath);
            RoundOf.Clear();
            // 낡음: 모서리 벗겨짐, 녹, 아래쪽 때, 패널 크기(m).
            steel = Lit("Prop_Steel", new Color(0.27f, 0.29f, 0.32f), 0.75f, 0.42f, wear: 0.35f, rustAmount: 0.3f, grime: 0.25f, panel: 8f);
            paint = Lit("Prop_PaintedSteel", new Color(0.5f, 0.54f, 0.57f), 0.45f, 0.35f, wear: 0.5f, rustAmount: 0.24f, grime: 0.3f, panel: 6f);
            hazard = Lit("Prop_HazardYellow", new Color(0.92f, 0.66f, 0.08f), 0.25f, 0.4f, wear: 0.55f, rustAmount: 0.18f, grime: 0.3f, panel: 5f);
            hazardStripe = Lit("Prop_HazardStripe", new Color(0.92f, 0.66f, 0.08f), 0.25f, 0.4f, wear: 0.45f, rustAmount: 0.12f, grime: 0.2f, panel: 12f, stripe: 3f);
            rust = Lit("Prop_OxideRed", new Color(0.5f, 0.2f, 0.13f), 0.35f, 0.3f, wear: 0.4f, rustAmount: 0.38f, grime: 0.3f, panel: 7f);
            concrete = Lit("Prop_Concrete", new Color(0.47f, 0.47f, 0.45f), 0f, 0.15f, wear: 0f, rustAmount: 0f, grime: 0.35f, panel: 10f);
            dark = Lit("Prop_DarkRubber", new Color(0.08f, 0.085f, 0.09f), 0.2f, 0.25f, wear: 0.25f, rustAmount: 0f, grime: 0.1f, panel: 6f);
            coilSteel = Lit("Prop_CoilSteel", new Color(0.66f, 0.68f, 0.72f), 0.95f, 0.62f, wear: 0f, rustAmount: 0.1f, grime: 0f, panel: 0.6f);
            lamp = Lit("Prop_LampAmber", new Color(1f, 0.62f, 0.12f), 0f, 0.5f, new Color(1f, 0.55f, 0.1f) * 3f);
            if (steel == null) return false;
            wall = WallMaterial();
            // 먼지·불꽃 재질은 전투 이펙트 것(VfxBuilder)을 같이 쓴다. 없으면 만들게 하고, 그래도 없으면 파티클 없이.
            VfxBuilder.RingMaterial();
            smoke = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            spark = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>정비고 벽·환적탑: 맵 건물과 같은 골강판 외벽 재질의 복사본(없으면 칠한 강철).</summary>
        static Material WallMaterial()
        {
            var path = $"{MaterialFolder}/Prop_HangarWall.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var facade = AssetDatabase.LoadAssetAtPath<Material>(FacadeMaterialPath);
                if (facade == null) return paint;
                material = new Material(facade);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, new Color(0.5f, 0.53f, 0.56f));
            if (material.HasProperty("_WindowStrength")) material.SetFloat("_WindowStrength", 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 소품 재질(있으면 값만 다시 맞춘다). 소품 셰이더(IndustrialProp)가 있고 빛나는 재질이 아니면 그 셰이더로,
        /// 아니면 URP Lit 단색으로 만든다. 셰이더는 에디터에서만 찾는다(런타임 Shader.Find 금지).
        /// 소품 셰이더 재질은 원통·공용 짝(_Round)도 같이 만들어 <see cref="RoundOf"/>에 넣는다.
        /// </summary>
        static Material Lit(string name, Color baseColor, float metallic, float smoothness, Color emission = default,
            float wear = 0.4f, float rustAmount = 0.2f, float grime = 0.25f, float panel = 6f, float stripe = 0f)
        {
            var glowing = emission.maxColorComponent > 0f;
            var shader = !glowing && propShader != null ? propShader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            var material = MaterialAsset(name, shader);
            Fill(material, false);
            if (shader == propShader)
            {
                var round = MaterialAsset(name + "_Round", shader);
                Fill(round, true);
                RoundOf[material] = round;
            }
            return material;

            void Fill(Material target, bool isRound)
            {
                target.SetColor(BaseColorId, baseColor);
                target.SetFloat(MetallicId, metallic);
                target.SetFloat(SmoothnessId, smoothness);
                if (shader == propShader)
                {
                    target.SetFloat("_WearStrength", wear);
                    target.SetFloat("_RustAmount", rustAmount);
                    target.SetFloat("_GrimeStrength", grime);
                    target.SetFloat("_PanelSize", panel);
                    target.SetFloat("_Stripe", stripe > 0f ? 1f : 0f);
                    if (stripe > 0f) target.SetFloat("_StripeSize", stripe);
                    target.SetFloat("_Round", isRound ? 1f : 0f);
                }
                if (glowing)
                {
                    target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    target.SetColor(EmissionColorId, emission);
                    target.SetKeyword(new LocalKeyword(target.shader, "_EMISSION"), true);
                }
                EditorUtility.SetDirty(target);
            }
        }

        static Material MaterialAsset(string name, Shader shader)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            return material;
        }
    }
}
