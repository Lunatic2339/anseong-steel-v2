using Anseong.Pilot;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 이 PC 한 사람의 입력을 교전 입력으로 바꾼다. 헤드셋이 켜져 있으면 컨트롤러, 아니면 키보드·마우스(개발 확인용, VR 검증 아님).
    /// 자리에 맞춰 XR Origin을 메카 눈높이 또는 공중포대 조종석에 놓고, 처음 추적될 때 "정면 = 북쪽"으로 맞춘다.
    /// <list type="bullet">
    /// <item>메카(P1): 왼 스틱 이동, 왼 트리거 + 왼팔 방향 = 가드(왼손이 얼굴 가운데보다 왼쪽이면 왼쪽을 막음),
    /// 오른 트리거를 쥔 채 오른손을 앞으로 뻗으면 주먹. 포가 켜진 장면에서는 오른 트리거 + 오른팔 방향 = 포.
    /// 키보드·마우스: 오른쪽 버튼 또는 Q = 왼쪽 막기, E = 오른쪽 막기, 왼쪽 버튼 = 주먹.
    /// 막기와 주먹은 임시 판정이다. 실제 로봇이 들어오면 기존 팔 조종의 판정으로 바꾼다.</item>
    /// <item>공중포대(P2): 오른손 방향 + 오른 트리거 = 기관포, 왼손으로 가리키고 왼 트리거 유지 → 놓기 = 미사일, 왼손 X 또는 오른손 B = 잠금 취소.</item>
    /// </list>
    /// 씬 구성은 메뉴 'Anseong → Sandbox → Build Z2 Skirmish'가 한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SkirmishLocalInput : MonoBehaviour
    {
        [SerializeField, Tooltip("XR Origin 루트. 자리에 맞춰 옮긴다.")] Transform rig;
        [SerializeField, Tooltip("컨트롤러 좌표의 기준(XR Origin의 Camera Offset).")] Transform trackingSpace;
        [SerializeField, Tooltip("머리(카메라).")] Transform head;
        [SerializeField, Tooltip("손 위치 표시(비면 안 그림).")] Transform leftHandMarker;
        [SerializeField] Transform rightHandMarker;
        [SerializeField, Tooltip("오른손 조준선.")] LineRenderer aimLine;
        [SerializeField, Tooltip("왼손 지시선(가드 방향 / 미사일 지시).")] LineRenderer pointerLine;

        [SerializeField, Tooltip("메카 발밑에서 조종사 눈까지(m).")] Vector3 mechEye = new Vector3(0f, 30f, 0f);
        [SerializeField, Tooltip("실제 로봇 모델의 팔(있으면 헤드셋의 두 손을 넘긴다).")] SkirmishMechArms mechArms;
        [SerializeField, Range(0.05f, 0.95f), Tooltip("트리거를 이만큼 당기면 누른 것으로 본다.")] float triggerPress = 0.5f;
        [SerializeField, Min(10f), Tooltip("조준선 길이(m).")] float lineLength = 250f;
        [SerializeField, Range(0.02f, 0.5f)] float mouseSensitivity = 0.12f;
        [SerializeField, Tooltip("이 자리를 자동 조작이 대신한다(혼자 구경·자동 시험용).")] bool useBot;
        [SerializeField, Min(0.1f), Tooltip("오른손이 앞으로 이 속도(m/s)보다 빠르게 나가면 주먹.")] float punchSpeed = 1.5f;
        [SerializeField, Min(0.05f), Tooltip("주먹과 주먹 사이 최소 간격(초).")] float punchInterval = 0.35f;
        [SerializeField, Min(0f), Tooltip("주먹 한 대의 피해(잽).")] float punchDamage = 25f;
        [SerializeField, Tooltip("P1 카메라가 걸음·충격에 반응하는 정도.")] CameraFeelSettings cameraFeel = new CameraFeelSettings();
        [SerializeField, Range(0f, 1f), Tooltip("발을 디딜 때 두 컨트롤러 진동 세기. 0 = 끔.")] float stepHaptic = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("맞았을 때 진동 세기.")] float hitHaptic = 0.9f;
        [SerializeField, Range(0f, 1f), Tooltip("막았을 때 진동 세기.")] float blockHaptic = 0.5f;

        InputAction rightRotation, rightPointer, rightPosition, rightTrigger, rightSecondary;
        InputAction leftRotation, leftPointer, leftPosition, leftTrigger, leftPrimary, leftStick, headPosition, headRotation;
        float yaw, pitch;
        bool recentered;
        Vector3 rigOffset;
        int punchSeq;
        float nextPunchAt;
        Vector3 lastRightLocal;
        bool hasLastRight;
        readonly MechCameraFeel feel = new MechCameraFeel();

        public MechCameraFeel Feel => feel;

        public SkirmishSeat Seat { get; private set; }
        public MechInput Mech { get; private set; }
        public GunnerInput Gunner { get; private set; }
        public bool UseBot { get => useBot; set => useBot = value; }
        /// <summary>헤드셋이 켜져 있어 컨트롤러 입력을 쓰는 중.</summary>
        public bool VrActive { get; private set; }

        public void Bind(Transform rigRoot, Transform tracking, Transform headTransform, Transform leftMarker, Transform rightMarker,
            LineRenderer aim, LineRenderer pointer)
        {
            rig = rigRoot; trackingSpace = tracking; head = headTransform;
            leftHandMarker = leftMarker; rightHandMarker = rightMarker; aimLine = aim; pointerLine = pointer;
        }

        public void BindMech(SkirmishMechArms arms, Vector3 eye)
        {
            mechArms = arms; mechEye = eye;
        }

        /// <summary>자리가 바뀌면 다음 프레임에 정면을 다시 맞춘다.</summary>
        public void SetSeat(SkirmishSeat seat)
        {
            if (seat == Seat && recentered) return;
            Seat = seat;
            recentered = false;
            pitch = seat == SkirmishSeat.Gunner ? 25f : 5f;
            yaw = 0f;
        }

        /// <summary>운영자가 착용자 정면을 다시 맞출 때.</summary>
        public void Recenter() => recentered = false;

        static InputAction Action(string binding)
        {
            var action = new InputAction(type: InputActionType.Value, binding: binding);
            action.Enable();
            return action;
        }

        void OnEnable()
        {
            const string R = "<XRController>{RightHand}/", L = "<XRController>{LeftHand}/";
            rightRotation = Action(R + "deviceRotation"); rightPointer = Action(R + "pointerRotation");
            rightPosition = Action(R + "devicePosition"); rightTrigger = Action(R + "trigger"); rightSecondary = Action(R + "secondaryButton");
            leftRotation = Action(L + "deviceRotation"); leftPointer = Action(L + "pointerRotation");
            leftPosition = Action(L + "devicePosition"); leftTrigger = Action(L + "trigger"); leftPrimary = Action(L + "primaryButton");
            leftStick = Action(L + "thumbstick");
            headPosition = Action("<XRHMD>/centerEyePosition"); headRotation = Action("<XRHMD>/centerEyeRotation");
        }

        void OnDisable()
        {
            foreach (var action in new[] { rightRotation, rightPointer, rightPosition, rightTrigger, rightSecondary,
                         leftRotation, leftPointer, leftPosition, leftTrigger, leftPrimary, leftStick, headPosition, headRotation })
                action?.Dispose();
        }

        /// <summary>조준 자세(pointer)가 있으면 그것을, 없으면 쥔 자세를 쓴다.</summary>
        static Quaternion Rotation(InputAction pointer, InputAction device)
        {
            var q = pointer.controls.Count > 0 ? pointer.ReadValue<Quaternion>() : device.ReadValue<Quaternion>();
            return q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f ? Quaternion.identity : q;
        }

        /// <param name="anchor">이 자리의 눈 위치(메카 눈 또는 공중포대 조종석).</param>
        public void Read(Vector3 mechPosition, Vector3 gunnerPost)
        {
            if (rig == null || head == null) return;
            var anchor = Seat == SkirmishSeat.Mech ? mechPosition + mechEye : gunnerPost;
            VrActive = XRSettings.isDeviceActive && headRotation.controls.Count > 0;

            Vector3 rightDirection, leftDirection, move;
            bool rightHeld, leftHeld, cancel, guardHeld, punched = false;
            var guardSide = PilotSide.Left;
            if (VrActive)
            {
                var space = trackingSpace != null ? trackingSpace : rig;
                if (!recentered)
                {
                    // 지금 보는 쪽을 북쪽(+Z)으로, 지금 머리 위치를 자리의 눈 위치로.
                    var headLocal = headPosition.ReadValue<Vector3>();
                    float headYaw = headRotation.ReadValue<Quaternion>().eulerAngles.y;
                    rig.rotation = Quaternion.Euler(0f, -headYaw, 0f);
                    rigOffset = -(rig.rotation * headLocal);
                    recentered = true;
                }
                rig.position = anchor + rigOffset;
                rightDirection = space.rotation * (Rotation(rightPointer, rightRotation) * Vector3.forward);
                leftDirection = space.rotation * (Rotation(leftPointer, leftRotation) * Vector3.forward);
                rightHeld = rightTrigger.ReadValue<float>() >= triggerPress;
                leftHeld = leftTrigger.ReadValue<float>() >= triggerPress;
                cancel = leftPrimary.ReadValue<float>() > 0.5f || rightSecondary.ReadValue<float>() > 0.5f;
                var stick = leftStick.ReadValue<Vector2>();
                move = Quaternion.Euler(0f, rig.eulerAngles.y, 0f) * new Vector3(stick.x, 0f, stick.y);
                var rightLocal = rightPosition.ReadValue<Vector3>();
                var leftWorld = space.TransformPoint(leftPosition.ReadValue<Vector3>());
                Place(rightHandMarker, space.TransformPoint(rightLocal), rightDirection);
                Place(leftHandMarker, leftWorld, leftDirection);
                guardHeld = leftHeld;
                // 왼손이 얼굴 가운데보다 왼쪽이면 왼쪽을, 오른쪽이면 오른쪽을 막는다.
                guardSide = Vector3.Dot(leftWorld - head.position, head.right) < 0f ? PilotSide.Left : PilotSide.Right;
                // 임시 주먹 판정: 오른 트리거를 쥔 채 손이 앞으로 빠르게 나감(자리 이동은 빼고 손의 움직임만 본다).
                if (hasLastRight && Time.deltaTime > 0f)
                {
                    var velocity = space.TransformDirection((rightLocal - lastRightLocal) / Time.deltaTime);
                    float forward = Vector3.Dot(velocity, SkirmishMath.Flat(head.forward));
                    punched = rightHeld && forward >= punchSpeed;
                }
                lastRightLocal = rightLocal;
                hasLastRight = true;
                if (mechArms != null && Seat == SkirmishSeat.Mech && !useBot)
                    mechArms.SetHands(head.position, rig.rotation, leftWorld, space.TransformPoint(rightLocal));
                else if (mechArms != null) mechArms.ClearHands();
            }
            else
            {
                var keyboard = Keyboard.current;
                var mouse = Mouse.current;
                rig.SetPositionAndRotation(anchor, Quaternion.identity);
                recentered = true;
                if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                {
                    var delta = mouse.delta.ReadValue() * mouseSensitivity;
                    yaw = Mathf.Clamp(yaw + delta.x, -110f, 110f);
                    pitch = Mathf.Clamp(pitch - delta.y, -40f, 75f);
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
                head.localPosition = Vector3.zero;
                head.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                rightDirection = leftDirection = head.forward;
                rightHeld = mouse != null && mouse.leftButton.isPressed;
                leftHeld = mouse != null && mouse.rightButton.isPressed;
                cancel = keyboard != null && keyboard.cKey.isPressed;
                bool right = keyboard != null && keyboard.eKey.isPressed;
                guardHeld = leftHeld || right || (keyboard != null && keyboard.qKey.isPressed);
                guardSide = right ? PilotSide.Right : PilotSide.Left;
                punched = mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked;
                hasLastRight = false;
                move = keyboard == null ? Vector3.zero : Quaternion.Euler(0f, yaw, 0f) * new Vector3(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0f,
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                Hide(rightHandMarker); Hide(leftHandMarker);
                if (mechArms != null) mechArms.ClearHands();
            }
            if (mechArms != null)
            {
                mechArms.SetLocalPilot(Seat == SkirmishSeat.Mech && !useBot);
                // 차폐판을 버튼으로 펴는 방식에서는 다 펴진 동안에만 막을 수 있다.
                guardHeld &= mechArms.ShieldReady;
            }

            if (punched && Time.unscaledTime >= nextPunchAt)
            {
                punchSeq++;
                nextPunchAt = Time.unscaledTime + punchInterval;
            }
            Mech = new MechInput
            {
                move = move, guard = guardHeld, guardDirection = leftDirection, fire = rightHeld, aim = rightDirection,
                guardSide = guardSide, punchSeq = punchSeq, punchDamage = punchDamage,
            };
            Gunner = new GunnerInput { aim = rightDirection, fire = rightHeld, pointer = leftDirection, lockHeld = leftHeld, cancel = cancel };

            Line(aimLine, rightHandMarker, rightDirection, VrActive);
            Line(pointerLine, leftHandMarker, leftDirection, VrActive);
        }

        /// <summary>
        /// 이번 프레임의 로봇 위치(틱 사이를 이은 값)로 P1의 눈을 다시 놓는다. 규칙을 돌린 뒤, 화면을 그리기 전에 부른다.
        /// 걸음·충격 반응은 여기서만 더한다(위치만, 회전 없음).
        /// </summary>
        public void FollowMech(Vector3 mechPosition, float dt)
        {
            if (rig == null || Seat != SkirmishSeat.Mech) { feel.Reset(); return; }
            feel.Tick(mechPosition, dt, cameraFeel);
            var anchor = mechPosition + mechEye + feel.Offset;
            rig.position = VrActive ? anchor + rigOffset : anchor;
            if (feel.Stepped) Rumble(stepHaptic * feel.StepWeight, 0.04f);
        }

        /// <summary>이번에 일어난 일 가운데 P1 몸에 닿은 것을 카메라와 진동으로 옮긴다.</summary>
        public void React(System.Collections.Generic.IReadOnlyList<SkirmishEvent> events)
        {
            if (Seat != SkirmishSeat.Mech || events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                bool hit = e.Kind == SkirmishEventKind.MechHit;
                bool blocked = e.Kind == SkirmishEventKind.GruntBlocked || e.Kind == SkirmishEventKind.GruntPerfectBlock || e.Kind == SkirmishEventKind.ShellBlocked;
                if (!hit && !blocked) continue;
                feel.Impact(e.To - e.From, hit ? cameraFeel.hitPush : cameraFeel.blockPush);
                Rumble(hit ? hitHaptic : blockHaptic, hit ? 0.15f : 0.08f);
            }
        }

        void Rumble(float amplitude, float seconds)
        {
            if (!VrActive || amplitude <= 0f) return;
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0, amplitude, seconds);
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0, amplitude, seconds);
        }

        static void Place(Transform marker, Vector3 position, Vector3 direction)
        {
            if (marker == null) return;
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            marker.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        }

        static void Hide(Transform marker)
        {
            if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
        }

        void Line(LineRenderer line, Transform from, Vector3 direction, bool on)
        {
            if (line == null) return;
            line.enabled = on && from != null;
            if (!line.enabled) return;
            line.SetPosition(0, from.position);
            line.SetPosition(1, from.position + direction * lineLength);
        }
    }
}
