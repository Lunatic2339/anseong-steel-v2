using System;
using Anseong.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Anseong.Pilot
{
    /// <summary>
    /// 조종석(자기 Transform)을 로봇 루트 기준 고정 오프셋으로 매 프레임 옮기고, XR Origin을 그 안의 자기 좌석에 둔다.
    /// <para>
    /// 애니메이션되는 본을 따라가지 않는 이유: 로봇이 약 61배라 클립에서 머리 본이 1cm 움직이면 카메라가 약 60cm 움직인다.
    /// 그래서 위치 = 로봇 루트 + 고정 오프셋(m), 회전 = 로봇 루트의 yaw만 쓴다. 확대된 모델 아래에 자식으로 두지 않으므로
    /// XR Origin도 확대되지 않는다. 로봇 이동 코드가 끝난 뒤(LateUpdate, 늦은 실행 순서)에 위치를 맞추고,
    /// 카메라 자세는 TrackedPoseDriver가 렌더 직전에 그대로 갱신하므로 지연이 생기지 않는다.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class CockpitSeat : MonoBehaviour, ITunable
    {
        /// <summary>Animator와 일반 스크립트가 로봇을 움직인 뒤에 실행한다.</summary>
        public const int ExecutionOrder = 10000;

        [Serializable]
        public class SeatAnchor
        {
            [Tooltip("표시용 이름 (예: Head, ChestTop).")]
            public string label;

            [Tooltip("로봇 루트에서 조종석 바닥 중심까지의 오프셋. 로봇 정면 기준(X 오른쪽, Y 위, Z 앞), 단위 m(로봇 배율과 무관).")]
            public Vector3 offsetMeters;

            [Tooltip("이 좌석에 앉아 있는 동안 숨길 렌더러(그림자는 남김). 예: 머리 좌석에서 시야를 막는 자기 로봇 머리. 에디터 메뉴가 채운다.")]
            public Renderer[] hideWhileSeated;
        }

        [SerializeField, Tooltip("조종석 위치·정면의 기준. 로봇 모델을 감싼 배율 1 루트(정면 = +Z). 위치와 yaw만 쓴다.")]
        Transform robotRoot;

        // 기본값: M0-04 측정(키 1.6 m 가정) 눈 높이 Head 57.5 m, ChestTop 52.4 m에서 1.6 m를 뺀 바닥 높이.
        // 앞뒤·좌우 값은 Build Player Robot Prefab 메뉴가 본 위치에서 다시 계산해 넣는다.
        [SerializeField, Tooltip("좌석 후보. 바닥 = 루트 + 오프셋, 눈 높이 = 바닥 + 조종사 실제 키.")]
        SeatAnchor[] seats =
        {
            new SeatAnchor { label = "Head", offsetMeters = new Vector3(0f, 55.9f, 0f) },
            new SeatAnchor { label = "ChestTop", offsetMeters = new Vector3(0f, 50.8f, 0f) },
        };

        [SerializeField, Tooltip("시작할 때 쓸 좌석 번호(seats 배열 인덱스). 0 = Head(M0-04 결정).")]
        int activeSeat;

        [SerializeField, Tooltip("피격 흔들림 등을 위한 추가 오프셋(m, 조종석 로컬). 기본 0. 실제 흔들림은 M2에서 코드가 넣는다.")]
        Vector3 additionalOffset;

        [SerializeField, Tooltip("조종석이 따라 흔들릴 본(머리). 비우면 따라가지 않는다. 에디터 메뉴가 채운다.")]
        Transform followBone;

        [SerializeField, Tooltip("followBone의 기준 위치(m). 루트 원점, 로봇 정면 기준. 이 위치에서 벗어난 만큼만 따라간다. 메뉴가 대기 클립 평균 자세로 채운다.")]
        Vector3 followReferenceLocal;

        [SerializeField, Range(0f, 1f), Tooltip("본 흔들림을 따라가는 비율. 0 = 루트에 고정, 1 = 본과 똑같이(기본, M1-01 헤드셋 확인). 위치만 따라가고 회전은 따라가지 않는다.")]
        float followWeight = 1f;

        [SerializeField, Tooltip("본을 따라 움직이는 최대 거리(m). 큰 동작 클립에서 카메라가 크게 튀지 않게 한다.")]
        float maxFollowOffset = 1f;

        [SerializeField, Tooltip("이 조종석 안에 둘 XR Origin. 이 Transform의 자식이어야 한다.")]
        XROrigin xrOrigin;

        [SerializeField, Tooltip("이 PC 조종사가 서는 좌석 쪽.")]
        PilotSide side = PilotSide.Left;

        [SerializeField, Tooltip("두 조종사 좌석 중심 사이 거리(m). 실제 바닥 테이프 간격과 같게.")]
        float seatSpacing = 2.1f;

        // 직렬화 레이아웃을 빌드 종류와 무관하게 맞추려고 #if 밖에 둔다. 사용은 개발 빌드에서만.
#pragma warning disable 0414
        [SerializeField, Tooltip("다음 좌석으로 전환하는 버튼 액션(에디터·개발 빌드 전용).")]
        InputActionReference switchSeatAction;
#pragma warning restore 0414

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool enabledSwitchSeat;
#endif

        public int ActiveSeat => activeSeat;
        public int SeatCount => seats != null ? seats.Length : 0;
        public string ActiveSeatLabel => IsValidSeat(activeSeat) ? seats[activeSeat].label : string.Empty;

        /// <summary>좌석 오프셋에 더할 추가 오프셋(m, 조종석 로컬: X 오른쪽, Y 위, Z 앞). 피격 흔들림용.</summary>
        public Vector3 AdditionalOffset
        {
            get => additionalOffset;
            set => additionalOffset = value;
        }

        void OnEnable()
        {
            PlaceOrigin();
            ApplySeatPose();
            ApplySeatVisibility();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (switchSeatAction != null && switchSeatAction.action != null)
            {
                switchSeatAction.action.performed += OnSwitchSeat;
                if (!switchSeatAction.action.enabled)
                {
                    switchSeatAction.action.Enable();
                    enabledSwitchSeat = true;
                }
                LogSwitchSeatControls();
            }
            else
            {
                Debug.LogWarning("[CockpitSeat] 좌석 전환 액션이 연결되지 않았습니다.", this);
            }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (switchSeatAction != null && switchSeatAction.action != null)
            {
                switchSeatAction.action.performed -= OnSwitchSeat;
                if (enabledSwitchSeat) switchSeatAction.action.Disable();
            }
            enabledSwitchSeat = false;
#endif
        }

        void LateUpdate()
        {
            ApplySeatPose();
        }

        /// <summary>현재 좌석 자세로 조종석 Transform을 옮긴다. 에디터 메뉴에서도 호출한다.</summary>
        public void ApplySeatPose()
        {
            if (!IsValidSeat(activeSeat) || robotRoot == null) return;
            var follow = followBone != null
                ? CockpitSeatMath.FollowOffset(robotRoot.position, robotRoot.rotation, followBone.position, followReferenceLocal, followWeight, maxFollowOffset)
                : Vector3.zero;
            var (position, rotation) = CockpitSeatMath.RigPose(robotRoot.position, robotRoot.rotation, seats[activeSeat].offsetMeters + follow + additionalOffset);
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>인게임 조절 메뉴: 조종석이 로봇 머리 흔들림을 따라가는 정도(선우 10/17, 계속 시험할 항목).</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "조종석 흔들림";
            list.Describe(g, "할 것: 펀치 치며 시점 흔들림 보기\n       따라가는 비율 1 → 0.5 → 0 비교\n볼 것: 거대 로봇에 탄 느낌, 멀미 없음");
            list.AddCheck(g, "seat.sway", "흔들림이 로봇에 탄 느낌");
            list.AddCheck(g, "seat.sick", "멀미 없음");
            list.AddFloat(g, "seat.followWeight", "머리 흔들림 따라가는 비율", () => followWeight, v => followWeight = v, 0f, 1f, 0.1f, 1);
        }

        /// <summary>XR Origin을 조종석 안 자기 좌석(좌/우)에 둔다.</summary>
        public void PlaceOrigin()
        {
            if (xrOrigin == null) return;
            xrOrigin.transform.SetLocalPositionAndRotation(CockpitSeatMath.SideOffset(side, seatSpacing), Quaternion.identity);
        }

        public void SetActiveSeat(int index)
        {
            if (!IsValidSeat(index)) return;
            activeSeat = index;
            ApplySeatPose();
            ApplySeatVisibility();
        }

        /// <summary>다른 좌석의 숨김 렌더러는 다시 보이게 하고, 현재 좌석의 것만 숨긴다.</summary>
        void ApplySeatVisibility()
        {
            if (seats == null) return;
            for (var i = 0; i < seats.Length; i++)
                if (i != activeSeat) SetHidden(seats[i], false);
            if (activeSeat >= 0 && activeSeat < seats.Length) SetHidden(seats[activeSeat], true);
        }

        static void SetHidden(SeatAnchor seat, bool hidden)
        {
            if (seat == null || seat.hideWhileSeated == null) return;
            var mode = hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var r in seat.hideWhileSeated)
                if (r != null) r.shadowCastingMode = mode;
        }

        bool IsValidSeat(int index)
        {
            return seats != null && index >= 0 && index < seats.Length && seats[index] != null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnSwitchSeat(InputAction.CallbackContext context)
        {
            if (SeatCount == 0) return;
            SetActiveSeat((activeSeat + 1) % SeatCount);
            Debug.Log($"[CockpitSeat] 좌석 전환: {ActiveSeatLabel} ({context.control?.path})", this);
        }

        // 버튼이 반응하지 않을 때 원인을 가르기 위한 로그: 바인딩이 실제 장치 컨트롤에 연결됐는지.
        void LogSwitchSeatControls()
        {
            var controls = switchSeatAction.action.controls;
            var paths = new string[controls.Count];
            for (var i = 0; i < controls.Count; i++) paths[i] = controls[i].path;
            Debug.Log($"[CockpitSeat] 좌석 전환 액션 연결 컨트롤 {controls.Count}개: {string.Join(", ", paths)}", this);
        }
#endif

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (seats == null || robotRoot == null) return;
            for (var i = 0; i < seats.Length; i++)
            {
                if (seats[i] == null) continue;
                var (position, rotation) = CockpitSeatMath.RigPose(robotRoot.position, robotRoot.rotation, seats[i].offsetMeters);
                Gizmos.color = i == activeSeat ? Color.green : Color.yellow;
                Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
                Gizmos.DrawWireCube(new Vector3(0f, 0.9f, 0f), new Vector3(seatSpacing + 1f, 1.8f, 1.5f));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
