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
        public const string HipsBone = "mixamorig:Hips";
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
        static readonly Vector3 ConsoleSize = new Vector3(3.6f, 0.5f, 0.3f);   // 선우 10/28: 앞 벽(콘솔) 낮춤 0.9 → 0.5
        const float ConsoleZ = 1.15f;
        /// <summary>기본 조종석 콘솔 윗면의 앞쪽 가운데(조종석 로컬). 조종석 모델(RobotArtSettings)을 쓰면 표시등 위치는 Inspector에서 옮긴다.</summary>
        public static Vector3 ConsoleTopFront => new Vector3(0f, ConsoleSize.y, ConsoleZ - ConsoleSize.z * 0.25f);
        const float WindowZ = 1.25f;
        const float WindowTop = 2.4f;
        const float WindowHalfWidth = 1.85f;
        const float FrameThickness = 0.07f;   // 11/13: 0.12는 시야를 12% 막아 판을 붙여 놓은 것처럼 보임

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
        /// 조종석 비움(CockpitClearZone)이 숨길 수 있는 로봇 몸 렌더러: 스킨 메시 전부에서
        /// 아래팔·손(손가락 포함)에 주로 묶인 것과 except(머리 좌석 숨김 목록)를 뺀다. 주먹은 늘 보이게.
        /// </summary>
        public static Renderer[] FindClearZoneRenderers(Transform robotRoot, Renderer[] except)
        {
            var armBones = new List<Transform>();
            foreach (var prefix in new[] { LeftPrefix, RightPrefix })
            {
                var forearm = FindBone(robotRoot, prefix + ForeArmSuffix);
                if (forearm != null) armBones.AddRange(forearm.GetComponentsInChildren<Transform>(true));
            }
            var arms = new HashSet<Renderer>(FindRenderersMostlyOn(robotRoot, armBones.ToArray()));
            var skip = new HashSet<Renderer>(except ?? new Renderer[0]);
            return robotRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => !arms.Contains(r) && !skip.Contains(r))
                .Cast<Renderer>().ToArray();
        }

        /// <summary>CockpitClearZone의 숨김 후보 목록을 채운다.</summary>
        public static void ConfigureClearZone(CockpitClearZone zone, Renderer[] candidates)
        {
            var serialized = new SerializedObject(zone);
            var list = serialized.FindProperty("candidates");
            list.arraySize = candidates.Length;
            for (var i = 0; i < candidates.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = candidates[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
            cockpitFrame = CockpitMaterial("Cockpit_Frame", new Color(0.07f, 0.08f, 0.09f), Color.black, 0.85f, 0.45f);
            cockpitGlow = CockpitMaterial("Cockpit_Glow", new Color(0.02f, 0.05f, 0.06f), new Color(0.15f, 0.9f, 1f) * 2.2f, 0f, 0.6f);

            CreateBox(root.transform, "Floor", FloorSize, new Vector3(0f, -FloorSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Seat Mark L", SeatMarkSize, new Vector3(-SeatSpacing * 0.5f, SeatMarkSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Seat Mark R", SeatMarkSize, new Vector3(SeatSpacing * 0.5f, SeatMarkSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Console", ConsoleSize, new Vector3(0f, ConsoleSize.y * 0.5f, ConsoleZ));

            var postSize = new Vector3(FrameThickness, WindowTop, FrameThickness);
            CreateBox(root.transform, "Window Post L", postSize, new Vector3(-WindowHalfWidth, WindowTop * 0.5f, WindowZ));
            CreateBox(root.transform, "Window Post R", postSize, new Vector3(WindowHalfWidth, WindowTop * 0.5f, WindowZ));
            CreateBox(root.transform, "Window Top", new Vector3(WindowHalfWidth * 2f + FrameThickness, FrameThickness, FrameThickness),
                new Vector3(0f, WindowTop, WindowZ));

            // 창틀 안쪽 가장자리와 콘솔 위에 가는 빛줄(조종석 계기 느낌). 회색 상자만 있으면 개발용 틀처럼 보인다(11/13 자동 점검).
            var strip = FrameThickness * 0.4f;
            var inset = FrameThickness * 0.5f + strip * 0.5f;
            CreateBox(root.transform, "Glow Post L", new Vector3(strip, WindowTop - FrameThickness, strip), new Vector3(-WindowHalfWidth + inset, WindowTop * 0.5f, WindowZ - inset), cockpitGlow);
            CreateBox(root.transform, "Glow Post R", new Vector3(strip, WindowTop - FrameThickness, strip), new Vector3(WindowHalfWidth - inset, WindowTop * 0.5f, WindowZ - inset), cockpitGlow);
            CreateBox(root.transform, "Glow Top", new Vector3(WindowHalfWidth * 2f - FrameThickness, strip, strip), new Vector3(0f, WindowTop - inset, WindowZ - inset), cockpitGlow);
            CreateBox(root.transform, "Glow Console", new Vector3(ConsoleSize.x * 0.9f, strip, strip), new Vector3(0f, ConsoleSize.y + strip * 0.5f, ConsoleZ - ConsoleSize.z * 0.5f + strip), cockpitGlow);
            // 옆벽(낮은 난간)과 콘솔 위 계기판 3개: 창 하나·민무늬 벽뿐이면 조종석처럼 안 보인다(11/13).
            var amber = CockpitMaterial("Cockpit_GlowAmber", new Color(0.06f, 0.04f, 0.02f), new Color(1f, 0.6f, 0.15f) * 1.8f, 0f, 0.6f);
            var railSize = new Vector3(FrameThickness, ConsoleSize.y * 0.9f, WindowZ + 0.5f);
            CreateBox(root.transform, "Side Rail L", railSize, new Vector3(-WindowHalfWidth, railSize.y * 0.5f, (WindowZ - 0.5f) * 0.5f));
            CreateBox(root.transform, "Side Rail R", railSize, new Vector3(WindowHalfWidth, railSize.y * 0.5f, (WindowZ - 0.5f) * 0.5f));
            CreateBox(root.transform, "Glow Rail L", new Vector3(strip, strip, railSize.z * 0.9f), new Vector3(-WindowHalfWidth + inset, railSize.y, (WindowZ - 0.5f) * 0.5f), cockpitGlow);
            CreateBox(root.transform, "Glow Rail R", new Vector3(strip, strip, railSize.z * 0.9f), new Vector3(WindowHalfWidth - inset, railSize.y, (WindowZ - 0.5f) * 0.5f), cockpitGlow);
            var panelY = ConsoleSize.y + 0.012f;
            var panelZ = ConsoleZ - ConsoleSize.z * 0.15f;
            CreateBox(root.transform, "Panel L", new Vector3(0.7f, 0.02f, 0.16f), new Vector3(-1.15f, panelY, panelZ), amber);
            CreateBox(root.transform, "Panel R", new Vector3(0.7f, 0.02f, 0.16f), new Vector3(1.15f, panelY, panelZ), amber);
            CreateCockpitDetail(root.transform, amber, strip);
            cockpitFrame = cockpitGlow = null;
        }

        /// <summary>
        /// 앉은 눈높이(1.2 m)에서 보이는 조종석 디테일(11/13 자동 점검: 민무늬 콘솔 하나뿐이라 싸 보임).
        /// 조종사 쪽으로 기운 계기판(자리마다 화면 3개 + 스위치 줄), 가운데 콘솔, 창 모서리 보강대, 머리 위 패널, 자리 옆 레버, 바닥 발판 줄.
        /// 창 가운데(전투가 보이는 곳)는 가리지 않는다: 계기판 윗변은 콘솔 위 0.2 m 이내.
        /// </summary>
        static void CreateCockpitDetail(Transform root, Material amber, float strip)
        {
            var panel = CockpitMaterial("Cockpit_Panel", new Color(0.16f, 0.18f, 0.21f), Color.black, 0.7f, 0.55f);
            var red = CockpitMaterial("Cockpit_GlowRed", new Color(0.06f, 0.02f, 0.02f), new Color(1f, 0.18f, 0.1f) * 1.8f, 0f, 0.6f);
            var screen = CockpitMaterial("Cockpit_Screen", new Color(0.01f, 0.03f, 0.04f), new Color(0.08f, 0.3f, 0.4f) * 1.2f, 0f, 0.9f);

            // 기운 계기판: 콘솔 뒤(조종사 쪽) 위 모서리에서 조종사 쪽으로 내려오는 판.
            const float dashTilt = 38f;
            var dashDepth = 0.42f;
            var dashCenter = new Vector3(0f, ConsoleSize.y - 0.06f, ConsoleZ - ConsoleSize.z * 0.5f - dashDepth * 0.38f);
            var dashRotation = Quaternion.Euler(-dashTilt, 0f, 0f);
            var dash = CreateBox(root, "Dash", new Vector3(ConsoleSize.x, 0.03f, dashDepth), dashCenter, panel);
            dash.localRotation = dashRotation;
            foreach (var side in new[] { -1f, 1f })
            {
                var seatX = side * SeatSpacing * 0.5f;
                var tag = side < 0f ? "L" : "R";
                // 화면 3개: 가운데 큰 것(청록), 양옆 작은 것(주황·청록).
                DashItem(root, $"Screen {tag} Main", dashCenter, dashRotation, new Vector3(seatX, 0.02f, 0.02f), new Vector3(0.46f, 0.012f, 0.26f), screen);
                DashItem(root, $"Screen {tag} Out", dashCenter, dashRotation, new Vector3(seatX + side * 0.42f, 0.02f, 0.04f), new Vector3(0.24f, 0.012f, 0.2f), screen);
                DashItem(root, $"Screen {tag} In", dashCenter, dashRotation, new Vector3(seatX - side * 0.42f, 0.02f, 0.04f), new Vector3(0.24f, 0.012f, 0.2f), screen);
                // 큰 화면 위 밝은 표시: 막대 그래프 5개 + 가로 기준선(어두운 화면만 있으면 꺼진 것처럼 보인다, 11/13 run10).
                for (var i = 0; i < 5; i++)
                {
                    var height = 0.05f + 0.035f * ((i * 3 + (side < 0f ? 1 : 2)) % 5);
                    DashItem(root, $"Bar {tag} {i}", dashCenter, dashRotation, new Vector3(seatX - 0.16f + i * 0.045f, 0.028f, -0.09f + height * 0.5f), new Vector3(0.028f, 0.006f, height), i == 4 ? amber : cockpitGlow);
                }
                DashItem(root, $"Line {tag}", dashCenter, dashRotation, new Vector3(seatX + 0.11f, 0.028f, 0.03f), new Vector3(0.16f, 0.006f, 0.012f), cockpitGlow);
                DashItem(root, $"Line {tag} 2", dashCenter, dashRotation, new Vector3(seatX + 0.11f, 0.028f, -0.03f), new Vector3(0.1f, 0.006f, 0.012f), amber);
                DashItem(root, $"Mark {tag} Out", dashCenter, dashRotation, new Vector3(seatX + side * 0.42f, 0.028f, 0.04f), new Vector3(0.14f, 0.006f, 0.014f), red);
                DashItem(root, $"Mark {tag} In", dashCenter, dashRotation, new Vector3(seatX - side * 0.42f, 0.028f, 0.04f), new Vector3(0.014f, 0.006f, 0.12f), cockpitGlow);
                // 화면 테두리(어두운 틀)와 아래 스위치 줄.
                DashItem(root, $"Bezel {tag}", dashCenter, dashRotation, new Vector3(seatX, 0.012f, 0.03f), new Vector3(1.22f, 0.012f, 0.32f), cockpitFrame);
                for (var i = 0; i < 7; i++)
                {
                    var material = i == 3 ? red : i % 2 == 0 ? cockpitGlow : amber;
                    DashItem(root, $"Switch {tag} {i}", dashCenter, dashRotation, new Vector3(seatX + (i - 3) * 0.12f, 0.025f, -0.165f), new Vector3(0.05f, 0.02f, 0.03f), material);
                }
                // 자리 바깥쪽 레버(받침 + 손잡이 + 빨간 머리).
                var leverX = seatX + side * 0.5f;
                CreateBox(root, $"Lever Base {tag}", new Vector3(0.16f, 0.3f, 0.3f), new Vector3(leverX, 0.15f, 0.35f), panel);
                var lever = CreateBox(root, $"Lever {tag}", new Vector3(0.03f, 0.3f, 0.03f), new Vector3(leverX, 0.42f, 0.4f));
                lever.localRotation = Quaternion.Euler(18f, 0f, 0f);
                CreateBox(root, $"Lever Knob {tag}", new Vector3(0.06f, 0.06f, 0.06f), new Vector3(leverX, 0.57f, 0.45f), red);
                // 창 위 모서리 보강대(45°)와 아래 모서리 보강판.
                var brace = CreateBox(root, $"Brace Top {tag}", new Vector3(0.5f, FrameThickness * 0.7f, FrameThickness * 0.7f),
                    new Vector3(side * (WindowHalfWidth - 0.17f), WindowTop - 0.17f, WindowZ));
                brace.localRotation = Quaternion.Euler(0f, 0f, side * 45f);
                var foot = CreateBox(root, $"Brace Low {tag}", new Vector3(0.36f, FrameThickness * 0.7f, FrameThickness * 0.7f),
                    new Vector3(side * (WindowHalfWidth - 0.12f), ConsoleSize.y + 0.12f, WindowZ));
                foot.localRotation = Quaternion.Euler(0f, 0f, side * -45f);
                // 옆 난간 위 작은 계기 상자.
                CreateBox(root, $"Side Box {tag}", new Vector3(0.1f, 0.14f, 0.5f), new Vector3(side * (WindowHalfWidth - 0.09f), ConsoleSize.y * 0.9f + 0.07f, 0.75f), panel);
                CreateBox(root, $"Side Light {tag}", new Vector3(0.012f, 0.05f, 0.36f), new Vector3(side * (WindowHalfWidth - 0.145f), ConsoleSize.y * 0.9f + 0.08f, 0.75f), side < 0f ? amber : cockpitGlow);
            }
            // 두 자리 사이 가운데 콘솔(낮게: 서로와 창을 가리지 않게).
            CreateBox(root, "Center Console", new Vector3(0.34f, 0.42f, 0.9f), new Vector3(0f, 0.21f, 0.55f), panel);
            CreateBox(root, "Center Top", new Vector3(0.26f, 0.012f, 0.6f), new Vector3(0f, 0.426f, 0.55f), screen);
            CreateBox(root, "Center Stop", new Vector3(0.08f, 0.05f, 0.08f), new Vector3(0f, 0.45f, 0.2f), red);
            // 머리 위 패널(창 윗변 바로 안쪽, 얇게) + 표시등 줄.
            CreateBox(root, "Overhead", new Vector3(WindowHalfWidth * 1.3f, 0.06f, 0.5f), new Vector3(0f, WindowTop + 0.02f, WindowZ - 0.3f), panel);
            for (var i = 0; i < 9; i++)
                CreateBox(root, $"Overhead Light {i}", new Vector3(0.09f, 0.012f, 0.05f), new Vector3((i - 4) * 0.24f, WindowTop - 0.016f, WindowZ - 0.42f),
                    i == 4 ? red : i % 3 == 0 ? amber : cockpitGlow);
            // 바닥 발판 줄(미끄럼 방지 철판 느낌).
            for (var i = 0; i < 6; i++)
                CreateBox(root, $"Floor Tread {i}", new Vector3(FloorSize.x * 0.94f, 0.008f, 0.05f), new Vector3(0f, 0.004f, -0.7f + i * 0.3f), panel);
        }

        /// <summary>기운 계기판 면 위에 놓는 조각. offset은 계기판 기준(x = 좌우, y = 면에서 뜨는 높이, z = 위·아래).</summary>
        static void DashItem(Transform root, string name, Vector3 dashCenter, Quaternion dashRotation, Vector3 offset, Vector3 size, Material material)
        {
            var item = CreateBox(root, name, size, dashCenter + dashRotation * offset, material);
            item.localRotation = dashRotation;
        }

        static Material cockpitFrame, cockpitGlow;
        const string CockpitMaterialFolder = "Assets/_Anseong/Art/Cockpit";

        /// <summary>조종석 도형 재질(URP Lit). 있으면 값만 다시 맞춘다.</summary>
        static Material CockpitMaterial(string name, Color baseColor, Color emission, float metallic, float smoothness)
        {
            if (!AssetDatabase.IsValidFolder(CockpitMaterialFolder)) AssetDatabase.CreateFolder(BossFolderParent, "Cockpit");
            var path = $"{CockpitMaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) return null;
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor(BaseColorId, baseColor);
            material.SetFloat(MetallicId, metallic);
            material.SetFloat(SmoothnessId, smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor(EmissionColorId, emission);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform CreateBox(Transform parent, string name, Vector3 size, Vector3 localPosition, Material material = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            if (material == null) material = cockpitFrame;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localScale = size;
            box.transform.localPosition = localPosition;
            return box.transform;
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
        /// 나머지는 모델 재질을 복사해 보스 색을 곱한 재질(Art/Boss/Materials, 텍스처·노멀 그대로)로 바꾼다.
        /// 단색으로 덮으면 장난감처럼 보여서(11/12 자동 점검) 무늬를 살린다. 복사할 수 없으면 Boss_Tint.
        /// </summary>
        public static void TintBoss(Transform boss)
        {
            var renderers = boss.GetComponentsInChildren<Renderer>(true);
            var slots = RobotArtSettings.Load().BossSlotMaterials;
            var tint = GetOrCreateBossTint(renderers);
            var replaced = ApplySlotMaterials(renderers, slots, source => GetOrCreateBossCopy(source) ?? tint);
            if (slots.Length > 0)
                Debug.Log($"[RobotSandboxUtility] 보스 재질 슬롯 적용: {replaced}칸, 나머지는 보스 색을 곱한 복사 재질", boss);
        }

        const string BossMaterialFolder = "Assets/_Anseong/Art/Boss/Materials";
        /// <summary>모델 재질 색에 곱하는 보스 색(검붉은 강철). 텍스처는 그대로라 판금 무늬가 남는다.</summary>
        static readonly Color BossMultiply = new Color(0.62f, 0.2f, 0.17f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>모델 재질 하나를 복사해 보스 색을 곱한 재질. 이미 있으면 색만 다시 맞춘다.</summary>
        static Material GetOrCreateBossCopy(Material source)
        {
            if (source == null || source.shader == null) return null;
            if (!AssetDatabase.IsValidFolder(BossMaterialFolder))
                AssetDatabase.CreateFolder($"{BossFolderParent}/{BossFolderName}", "Materials");
            var safe = string.Concat(source.name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));
            var path = $"{BossMaterialFolder}/Boss_{safe}.mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == source) return copy;
            if (copy == null)
            {
                copy = new Material(source) { name = $"Boss_{safe}" };
                AssetDatabase.CreateAsset(copy, path);
            }
            var baseColor = source.HasProperty(BaseColorId) ? source.GetColor(BaseColorId) : Color.white;
            var tinted = new Color(baseColor.r * BossMultiply.r, baseColor.g * BossMultiply.g, baseColor.b * BossMultiply.b, baseColor.a);
            if (copy.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, tinted);
            if (copy.HasProperty(MetallicId)) copy.SetFloat(MetallicId, Mathf.Max(source.GetFloat(MetallicId), 0.55f));
            if (copy.HasProperty(SmoothnessId)) copy.SetFloat(SmoothnessId, Mathf.Clamp(source.GetFloat(SmoothnessId), 0.35f, 0.6f));
            // 예고 빛(BossTelegraphTint)이 emission을 덮어쓰므로 키워드를 켜 둔다(평소엔 검정 = 안 빛남).
            copy.EnableKeyword("_EMISSION");
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (copy.HasProperty(EmissionColorId) && !source.IsKeywordEnabled("_EMISSION")) copy.SetColor(EmissionColorId, Color.black);
            EditorUtility.SetDirty(copy);
            return copy;
        }

        /// <summary>렌더러 재질 칸마다 이름이 맞는 슬롯 재질로 바꾸고, 맞지 않으면 fallback이 돌려준 재질(null이면 그대로). 슬롯으로 바꾼 칸 수를 돌려준다.</summary>
        static int ApplySlotMaterials(Renderer[] renderers, RobotArtSettings.SlotMaterial[] slots, System.Func<Material, Material> fallback)
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
                        // 이미 보스 복사 재질이면(다시 돌린 경우) 그대로.
                        if (materials[i] != null && AssetDatabase.GetAssetPath(materials[i]).StartsWith(BossMaterialFolder)) continue;
                        var swap = fallback(materials[i]);
                        if (swap != null) materials[i] = swap;
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
