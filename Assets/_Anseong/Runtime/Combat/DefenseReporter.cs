using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 가드·회피 스틱 보고(M2-03). 이 PC가 맡은 팔의 가드(사람 손 판정, <see cref="GuardDriver"/>)와 회피 스틱을 바뀔 때만 전투 서버에 보낸다.
    /// 서버는 보고된 값만 읽고 보스 공격 명중 순간에 판정한다. 1인 양팔이면 두 팔·두 스틱 모두 이 PC가 보낸다.
    /// 스틱: 1인 양팔은 그 팔 손의 스틱, 2인(M3)은 그 조종사의 이동 손 스틱.
    /// </summary>
    [DefaultExecutionOrder(GuardDriver.ExecutionOrder + 10)]
    public class DefenseReporter : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정(보고 통로). 비어 있으면 같은 오브젝트에서 찾는다.")]
        CombatDirector director;
        [SerializeField, Tooltip("가드 판정. 비어 있으면 같은 오브젝트에서 찾는다.")]
        GuardDriver guard;
        [SerializeField, Tooltip("손 입력. 비어 있으면 시작할 때 한 번 찾는다.")]
        PilotHandInput input;

        [SerializeField, Range(0.01f, 0.3f), Tooltip("스틱이 이만큼 넘게 바뀌어야 다시 보낸다(보내는 수 줄이기).")]
        float stickStep = 0.08f;

        readonly bool[] sentGuard = new bool[2];
        readonly Vector2[] sentStick = new Vector2[2];
        ICombatLink sentTo;

        void Start()
        {
            if (director == null) director = GetComponent<CombatDirector>();
            if (guard == null) guard = GetComponent<GuardDriver>();
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (director == null || input == null) enabled = false;
        }

        void LateUpdate()
        {
            var link = director.Link;
            if (link == null)
            {
                sentTo = null;
                return;
            }
            var fresh = link != sentTo;
            sentTo = link;
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                if (!input.IsArmHand(side)) continue;
                var guarding = guard != null && guard.IsGuarding(side);
                if (fresh || guarding != sentGuard[h])
                {
                    link.SubmitGuard(side, guarding);
                    sentGuard[h] = guarding;
                }
                var stick = Stick(side);
                if (fresh || (stick - sentStick[h]).sqrMagnitude >= stickStep * stickStep || (stick == Vector2.zero && sentStick[h] != Vector2.zero))
                {
                    link.SubmitStick(side, stick);
                    sentStick[h] = stick;
                }
            }
        }

        Vector2 Stick(PilotSide side)
        {
            if (input.ControlsBothArms) return input.Get(side).Stick;
            for (var h = 0; h < 2; h++)
                if (input.IsMoveHand((PilotSide)h)) return input.Get((PilotSide)h).Stick;
            return input.Get(side).Stick;
        }
    }
}
