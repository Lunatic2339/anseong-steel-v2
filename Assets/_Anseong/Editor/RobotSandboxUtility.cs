using System.Collections.Generic;
using System.Linq;
using Anseong.Core;
using Anseong.Pilot;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 샌드박스·프리팹 메뉴들이 같이 쓰는 로봇 배치 도우미.
    /// </summary>
    static class RobotSandboxUtility
    {
        public const float RobotHeight = 65f;

        public const string PilotInputPath = "Assets/_Anseong/Runtime/Pilot/PilotInput.inputactions";
        public const string BossTintPath = "Assets/_Anseong/Art/Boss/Boss_Tint.mat";
        const string BossFolderParent = "Assets/_Anseong/Art";
        const string BossFolderName = "Boss";
        static readonly Color BossColor = new Color(0.45f, 0.06f, 0.05f);

        // Mixamo 본 이름 (모든 로봇 모델 공통, 04_art_spec). 런타임은 이 이름을 쓰지 않고 Inspector 참조만 쓴다.
        public const string HeadBone = "mixamorig:Head";
        public const string HeadEndBone = "mixamorig:Head_end";
        public const string SpineBone = "mixamorig:Spine";
        public const string LeftPrefix = "mixamorig:Left";
        public const string RightPrefix = "mixamorig:Right";
        public const string ArmSuffix = "Arm";
        public const string ForeArmSuffix = "ForeArm";
        public const string HandSuffix = "Hand";

        // 좌석 초기값(M0-04). 바닥 = 눈 높이 − PilotEyeHeight. 이후 CockpitSeat Inspector에서 조정한다.
        public const float PilotEyeHeight = 1.6f;       // 서 있는 조종사의 눈 높이 가정 (m)
        const float HeadEyeFraction = 0.4f;             // Head → Head_end 사이 눈 위치 비율
        const float HeadSeatForward = 0f;               // 머리 좌석 앞뒤 보정 (m)
        const float ChestSeatForward = 0f;              // 가슴 좌석 앞뒤 보정 (m). 눈 높이 = 어깨(Arm 본) 높이

        // 조종석 박스 (조종석 로컬, 바닥 중심 = 원점, +Z = 앞)
        public const float SeatSpacing = 2.1f;
        static readonly Vector3 FloorSize = new Vector3(3.6f, 0.05f, 2f);
        static readonly Vector3 SeatMarkSize = new Vector3(0.6f, 0.005f, 0.6f);
        static readonly Vector3 ConsoleSize = new Vector3(3.6f, 0.9f, 0.3f);
        const float ConsoleZ = 1.15f;
        const float WindowZ = 1.25f;
        const float WindowTop = 2.4f;
        const float WindowHalfWidth = 1.85f;
        const float FrameThickness = 0.12f;

        /// <summary>
        /// 렌더러 bounds 높이를 재서 robot을 targetHeight로 균일 확대하고 발을 groundY에 맞춘다.
        /// boundsBefore는 확대 전 측정값.
        /// </summary>
        public static bool FitToHeight(GameObject robot, float targetHeight, out Bounds boundsBefore, out float scale, out float offset, float groundY = 0f)
        {
            scale = 1f;
            offset = 0f;
            if (!TryGetRendererBounds(robot, out boundsBefore))
                return false;

            scale = HeightFit.ScaleForHeight(boundsBefore.size.y, targetHeight);
            var pivotY = robot.transform.position.y;
            offset = HeightFit.GroundOffset(boundsBefore.min.y, pivotY, scale, groundY);
            robot.transform.localScale *= scale;
            robot.transform.position += Vector3.up * offset;
            return true;
        }

        public static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.size.y > 0f;
        }

        public static Transform FindBone(Transform root, string boneName)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == boneName);
        }

        public static InputActionReference FindAction(string mapName, string actionName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(PilotInputPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.actionMap != null &&
                                     r.action.actionMap.name == mapName && r.action.name == actionName);
        }

        /// <summary>
        /// IK·고정 자세가 팔을 bind 자세 밖으로 옮기면 T자세 기준 컬링 bounds 밖으로 손 조각이 나가 깜빡인다.
        /// 매 프레임 실제 자세로 bounds를 다시 잡게 한다(임시. 최종 방법은 A-02 병합 후).
        /// </summary>
        public static int EnableUpdateWhenOffscreen(Transform root)
        {
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in skinned)
            {
                Undo.RecordObject(renderer, "Update When Offscreen");
                renderer.updateWhenOffscreen = true;
            }
            return skinned.Length;
        }

        /// <summary>
        /// 로봇 루트(배율 1, 정면 +Z) 기준 좌석 바닥 오프셋(m).
        /// Head 좌석: 눈 = Head→Head_end 40% 지점. ChestTop 좌석: 눈 = 양 어깨(Arm 본) 높이, 앞뒤는 Spine 본.
        /// poseClip을 주면 그 클립을 여러 시점에서 재생해 본 위치의 평균을 쓴다(실제 게임에서 보이는 자세).
        /// 없으면 지금 자세(기본 자세)에서 잰다. 어느 쪽이든 끝나면 본 자세를 원래대로 되돌린다.
        /// </summary>
        public static bool TryComputeSeatOffsets(Transform robotRoot, out Vector3 headOffset, out Vector3 chestOffset, out string report,
            AnimationClip poseClip = null)
        {
            return TryComputeSeatOffsets(robotRoot, out headOffset, out chestOffset, out _, out report, poseClip);
        }

        /// <summary>위와 같고, Head 본의 기준 위치(루트 원점, 로봇 정면 기준)도 돌려준다. 조종석 흔들림 따라가기의 기준.</summary>
        public static bool TryComputeSeatOffsets(Transform robotRoot, out Vector3 headOffset, out Vector3 chestOffset, out Vector3 headReferenceLocal,
            out string report, AnimationClip poseClip = null)
        {
            headOffset = chestOffset = headReferenceLocal = Vector3.zero;
            var bones = new[]
            {
                FindBone(robotRoot, HeadBone),
                FindBone(robotRoot, HeadEndBone),
                FindBone(robotRoot, SpineBone),
                FindBone(robotRoot, LeftPrefix + ArmSuffix),
                FindBone(robotRoot, RightPrefix + ArmSuffix),
            };
            if (bones.Any(b => b == null))
            {
                report = "좌석 기준 본(Head, Head_end, Spine, LeftArm, RightArm)을 찾지 못했습니다.";
                return false;
            }

            var animator = robotRoot.GetComponentInChildren<Animator>(true);
            var source = "기본 자세";
            Vector3[] positions;
            if (poseClip != null && animator != null)
            {
                positions = AveragePositions(animator.gameObject, poseClip, bones, out var headRange);
                source = $"'{poseClip.name}' 클립 평균 자세(재생 중 Head 움직임 범위 {headRange:F2} m)";
            }
            else
            {
                positions = bones.Select(b => b.position).ToArray();
            }
            var head = positions[0];
            var headEnd = positions[1];
            var spine = positions[2];
            var shoulderY = (positions[3].y + positions[4].y) * 0.5f;

            var toRobot = Quaternion.Inverse(CockpitSeatMath.YawOnly(robotRoot.rotation));

            headReferenceLocal = toRobot * (head - robotRoot.position);

            var headEye = Vector3.Lerp(head, headEnd, HeadEyeFraction);
            var headFloor = headEye + Vector3.down * PilotEyeHeight;
            headOffset = toRobot * (headFloor - robotRoot.position) + Vector3.forward * HeadSeatForward;

            var chestFloor = new Vector3(spine.x, shoulderY - PilotEyeHeight, spine.z);
            chestOffset = toRobot * (chestFloor - robotRoot.position) + Vector3.forward * ChestSeatForward;

            report = $"본 높이({source}): Head {head.y:F1} m, Head_end {headEnd.y:F1} m, Spine {spine.y:F1} m, 어깨 {shoulderY:F1} m. " +
                     $"좌석 눈 높이(키 {PilotEyeHeight} m 가정): Head {headEye.y:F1} m, ChestTop {shoulderY:F1} m. " +
                     $"루트 기준 바닥 오프셋 Head {headOffset}, ChestTop {chestOffset}";
            return true;
        }

        const int PoseSamples = 16;

        /// <summary>clip을 고르게 PoseSamples번 재생해 bones의 평균 월드 위치를 잰다. 끝나면 모든 본을 원래 자세로 되돌린다.</summary>
        static Vector3[] AveragePositions(GameObject model, AnimationClip clip, Transform[] bones, out float headRange)
        {
            var all = model.GetComponentsInChildren<Transform>(true);
            var saved = all.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
            var sums = new Vector3[bones.Length];
            var minHead = Vector3.one * float.MaxValue;
            var maxHead = Vector3.one * float.MinValue;
            try
            {
                for (var i = 0; i < PoseSamples; i++)
                {
                    clip.SampleAnimation(model, clip.length * i / PoseSamples);
                    for (var b = 0; b < bones.Length; b++) sums[b] += bones[b].position;
                    minHead = Vector3.Min(minHead, bones[0].position);
                    maxHead = Vector3.Max(maxHead, bones[0].position);
                }
            }
            finally
            {
                for (var i = 0; i < all.Length; i++)
                {
                    all[i].localPosition = saved[i].localPosition;
                    all[i].localRotation = saved[i].localRotation;
                    all[i].localScale = saved[i].localScale;
                }
            }
            headRange = Vector3.Distance(maxHead, minHead);
            return sums.Select(v => v / PoseSamples).ToArray();
        }

        /// <summary>Head 좌석에서 숨길 자기 로봇 머리 렌더러(정점 절반 이상이 Head·Head_end 본).</summary>
        public static Renderer[] FindHeadRenderers(Transform robotRoot)
        {
            var head = FindBone(robotRoot, HeadBone);
            var headEnd = FindBone(robotRoot, HeadEndBone);
            if (head == null || headEnd == null) return new Renderer[0];
            return FindRenderersMostlyOn(robotRoot, head, headEnd);
        }

        /// <summary>
        /// 정점의 절반 이상이 주어진 본에 가장 크게 묶인 스킨 메시 렌더러(예: 머리 장갑판).
        /// </summary>
        public static Renderer[] FindRenderersMostlyOn(Transform robotRoot, params Transform[] bones)
        {
            var result = new List<Renderer>();
            foreach (var skinned in robotRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skinned.sharedMesh;
                var rendererBones = skinned.bones;
                if (mesh == null || rendererBones == null || mesh.vertexCount == 0) continue;

                var weights = mesh.boneWeights;
                if (weights.Length == 0)
                {
                    // 가중치가 없으면 rootBone 하나에 붙은 메시다.
                    if (bones.Contains(skinned.rootBone)) result.Add(skinned);
                    continue;
                }
                var onBones = 0;
                foreach (var w in weights)
                {
                    var index = w.boneIndex0;
                    if (index >= 0 && index < rendererBones.Length && bones.Contains(rendererBones[index]))
                        onBones++;
                }
                if (onBones * 2 >= weights.Length)
                    result.Add(skinned);
            }
            return result.ToArray();
        }

        /// <summary>
        /// CockpitSeat의 좌석 두 개(Head, ChestTop)·기준 루트·전환 액션·Head 좌석 숨김 목록을 채운다.
        /// </summary>
        public static void ConfigureSeat(CockpitSeat seat, Transform robotRoot, Vector3 headOffset, Vector3 chestOffset,
            Renderer[] headRenderers, InputActionReference switchSeat)
        {
            var serialized = new SerializedObject(seat);
            serialized.FindProperty("robotRoot").objectReferenceValue = robotRoot;
            var seats = serialized.FindProperty("seats");
            seats.arraySize = 2;
            SetSeat(seats.GetArrayElementAtIndex(0), "Head", headOffset, headRenderers);
            SetSeat(seats.GetArrayElementAtIndex(1), "ChestTop", chestOffset, new Renderer[0]);
            serialized.FindProperty("activeSeat").intValue = 0;
            serialized.FindProperty("seatSpacing").floatValue = SeatSpacing;
            serialized.FindProperty("switchSeatAction").objectReferenceValue = switchSeat;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>조종석이 따라 흔들릴 본과 그 기준 위치, 따라가는 비율을 채운다.</summary>
        public static void ConfigureFollow(CockpitSeat seat, Transform bone, Vector3 referenceLocal, float weight = 1f)
        {
            var serialized = new SerializedObject(seat);
            serialized.FindProperty("followBone").objectReferenceValue = bone;
            serialized.FindProperty("followReferenceLocal").vector3Value = referenceLocal;
            serialized.FindProperty("followWeight").floatValue = weight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSeat(SerializedProperty element, string label, Vector3 offset, Renderer[] hidden)
        {
            element.FindPropertyRelative("label").stringValue = label;
            element.FindPropertyRelative("offsetMeters").vector3Value = offset;
            var list = element.FindPropertyRelative("hideWhileSeated");
            list.arraySize = hidden.Length;
            for (var i = 0; i < hidden.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = hidden[i];
        }

        /// <summary>
        /// 조종석 형상. RobotArtSettings에 조종석 프리팹이 있으면 그것을, 없으면 바닥·좌석 표시·콘솔·창틀 박스를 만든다.
        /// </summary>
        public static void CreateCockpitProps(Transform rig)
        {
            var prefab = RobotArtSettings.Load().CockpitPrefab;
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rig);
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;
                Debug.Log($"[RobotSandboxUtility] 조종석 모델: {prefab.name}", prefab);
                return;
            }

            var root = new GameObject("Cockpit Props");
            root.transform.SetParent(rig, false);

            CreateBox(root.transform, "Floor", FloorSize, new Vector3(0f, -FloorSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Seat Mark L", SeatMarkSize, new Vector3(-SeatSpacing * 0.5f, SeatMarkSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Seat Mark R", SeatMarkSize, new Vector3(SeatSpacing * 0.5f, SeatMarkSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Console", ConsoleSize, new Vector3(0f, ConsoleSize.y * 0.5f, ConsoleZ));

            var postSize = new Vector3(FrameThickness, WindowTop, FrameThickness);
            CreateBox(root.transform, "Window Post L", postSize, new Vector3(-WindowHalfWidth, WindowTop * 0.5f, WindowZ));
            CreateBox(root.transform, "Window Post R", postSize, new Vector3(WindowHalfWidth, WindowTop * 0.5f, WindowZ));
            CreateBox(root.transform, "Window Top", new Vector3(WindowHalfWidth * 2f + FrameThickness, FrameThickness, FrameThickness),
                new Vector3(0f, WindowTop, WindowZ));
        }

        static void CreateBox(Transform parent, string name, Vector3 size, Vector3 localPosition)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localScale = size;
            box.transform.localPosition = localPosition;
        }

        /// <summary>플레이어 재질 슬롯(RobotArtSettings)을 적용한다. 맞지 않는 재질은 모델 그대로 둔다.</summary>
        public static void ApplyPlayerMaterials(Transform player)
        {
            var slots = RobotArtSettings.Load().PlayerSlotMaterials;
            if (slots.Length == 0) return;
            var replaced = ApplySlotMaterials(player.GetComponentsInChildren<Renderer>(true), slots, null);
            Debug.Log($"[RobotSandboxUtility] 플레이어 재질 슬롯 적용: {replaced}칸", player);
        }

        /// <summary>
        /// 보스 재질. 보스 재질 슬롯(RobotArtSettings)과 이름이 맞는 재질은 그 슬롯 재질로,
        /// 나머지는 보스 단색(Boss_Tint)으로 바꾼다. 슬롯이 비어 있으면 전부 Boss_Tint.
        /// </summary>
        public static void TintBoss(Transform boss)
        {
            var renderers = boss.GetComponentsInChildren<Renderer>(true);
            var slots = RobotArtSettings.Load().BossSlotMaterials;
            var tint = GetOrCreateBossTint(renderers);
            var replaced = ApplySlotMaterials(renderers, slots, tint);
            if (slots.Length > 0)
                Debug.Log($"[RobotSandboxUtility] 보스 재질 슬롯 적용: {replaced}칸, 나머지는 {(tint != null ? tint.name : "모델 재질")}", boss);
        }

        /// <summary>렌더러 재질 칸마다 이름이 맞는 슬롯 재질로 바꾸고, 맞지 않으면 fallback(없으면 그대로). 슬롯으로 바꾼 칸 수를 돌려준다.</summary>
        static int ApplySlotMaterials(Renderer[] renderers, RobotArtSettings.SlotMaterial[] slots, Material fallback)
        {
            var slotNames = slots.Select(s => s.material != null ? s.slot : null).ToArray();
            var replaced = 0;
            foreach (var r in renderers)
            {
                var materials = r.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var index = materials[i] != null ? MaterialSlotNames.IndexOf(materials[i].name, slotNames) : -1;
                    if (index >= 0)
                    {
                        materials[i] = slots[index].material;
                        replaced++;
                    }
                    else if (fallback != null)
                    {
                        materials[i] = fallback;
                    }
                }
                r.sharedMaterials = materials;
            }
            return replaced;
        }

        static Material GetOrCreateBossTint(Renderer[] renderers)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BossTintPath);
            if (material != null) return material;

            // 로봇 재질과 같은 셰이더(URP Lit)를 쓴다. 텍스처 없이 색만.
            var source = renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null);
            if (source == null)
            {
                Debug.LogWarning("[RobotSandboxUtility] 로봇 재질이 없어 보스 색을 바꾸지 않습니다.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder($"{BossFolderParent}/{BossFolderName}"))
                AssetDatabase.CreateFolder(BossFolderParent, BossFolderName);

            material = new Material(source.shader) { color = BossColor };
            AssetDatabase.CreateAsset(material, BossTintPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RobotSandboxUtility] 보스 색 재질 생성: {BossTintPath}", material);
            return material;
        }
    }
}
