using System;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 보스의 팔과 대검을 싸움 상태에 맞춰 세우는 임시 표현. Mixamo 대검(Great Sword) 동작이 오면 이 파일을 대체한다.
    /// 뼈의 축이 모델마다 달라서 각도가 아니라 "팔이 향할 방향"으로 자세를 잡는다(위팔 → 아래팔 순서).
    /// 방향은 보스 기준(x 보스의 오른쪽, y 위, z 보스의 정면 = 로봇 쪽)이다. 대검은 보스의 오른손(로봇이 볼 때 왼쪽)에 든다. 방패는 없다(2026-11-27 선우 결정). shield 칸은 비워 둔다.
    /// </summary>
    public class BossPuppet : MonoBehaviour
    {
        [Serializable]
        public struct Arm
        {
            public Transform upper, fore, hand;
            [HideInInspector] public Quaternion upperRest, foreRest, handRest;
        }

        struct Pose
        {
            public Vector3 swordUpper, swordFore, blade, shieldUpper, shieldFore, shieldFace;
        }

        [SerializeField] Arm swordArm, shieldArm;
        [SerializeField, Tooltip("손에서 칼끝 쪽으로 뻗는 칼. 길이 방향이 z.")] Transform sword;
        [SerializeField, Tooltip("아래팔에 붙는 방패. 판이 향하는 쪽이 z.")] Transform shield;
        [SerializeField, Min(0f)] float swordLength = 20f;
        [SerializeField, Min(0f), Tooltip("방패를 아래팔에서 바깥으로 띄우는 거리(m).")] float shieldOffset = 2f;
        [SerializeField, Min(0f), Tooltip("자세가 바뀌는 빠르기(1초에 몇 번 따라잡는가). 0 = 바로.")] float follow = 14f;

        // 기본 자세: 대검을 앞으로 세워 든다. 왼팔은 비어 있다.
        static readonly Pose Stance = new Pose
        {
            swordUpper = new Vector3(0.55f, -0.75f, 0.35f), swordFore = new Vector3(0.1f, 0.15f, 1f), blade = new Vector3(-0.3f, 0.95f, 0.1f),
            shieldUpper = new Vector3(-0.7f, -0.7f, 0.15f), shieldFore = new Vector3(-0.15f, -0.2f, 1f), shieldFace = new Vector3(-0.25f, 0f, 1f),
        };
        // 왼쪽으로 오는 베기(내려베기) 예고: 대검이 머리 위로.
        static readonly Pose SwordUp = new Pose
        {
            swordUpper = new Vector3(0.35f, 0.9f, 0.1f), swordFore = new Vector3(-0.1f, 0.9f, -0.35f), blade = new Vector3(-0.1f, 0.75f, -0.65f),
            shieldUpper = Stance.shieldUpper, shieldFore = Stance.shieldFore, shieldFace = Stance.shieldFace,
        };
        // 대검이 내려온 끝.
        static readonly Pose SwordDown = new Pose
        {
            swordUpper = new Vector3(0.25f, -0.15f, 1f), swordFore = new Vector3(-0.05f, -0.45f, 1f), blade = new Vector3(-0.15f, -0.55f, 1f),
            shieldUpper = Stance.shieldUpper, shieldFore = Stance.shieldFore, shieldFace = Stance.shieldFace,
        };
        // 오른쪽으로 오는 베기 예고: 대검을 보스의 왼쪽(로봇이 볼 때 오른쪽)으로 넘겨 눕힌다.
        static readonly Pose SwordSide = new Pose
        {
            swordUpper = new Vector3(-0.2f, 0.15f, 0.95f), swordFore = new Vector3(-0.9f, 0.3f, 0.3f), blade = new Vector3(-0.9f, 0.35f, -0.25f),
            shieldUpper = new Vector3(-0.85f, -0.4f, -0.3f), shieldFore = new Vector3(-0.7f, -0.3f, 0.6f), shieldFace = Stance.shieldFace,
        };
        // 가로로 벤 끝: 대검이 로봇 앞을 지나 반대쪽으로.
        static readonly Pose SwordAcross = new Pose
        {
            swordUpper = new Vector3(0.45f, -0.1f, 0.9f), swordFore = new Vector3(0.6f, -0.15f, 0.8f), blade = new Vector3(0.85f, -0.1f, 0.5f),
            shieldUpper = Stance.shieldUpper, shieldFore = Stance.shieldFore, shieldFace = Stance.shieldFace,
        };
        // 튕겨남: 두 팔이 벌어져 가슴이 열린다.
        static readonly Pose Open = new Pose
        {
            swordUpper = new Vector3(0.95f, 0.3f, -0.25f), swordFore = new Vector3(0.85f, 0.5f, -0.2f), blade = new Vector3(0.75f, 0.6f, -0.3f),
            shieldUpper = new Vector3(-0.95f, 0.25f, -0.25f), shieldFore = new Vector3(-0.85f, 0.45f, -0.2f), shieldFace = new Vector3(-0.8f, 0.1f, 0.5f),
        };

        Pose target = Stance, shown = Stance;
        bool started;

        /// <summary>장면을 만드는 에디터 메뉴가 뼈와 칼·방패를 넣는다. 그때의 뼈 회전을 기준 자세로 적어 둔다.</summary>
        public void Bind(Arm swordSide, Arm shieldSide, Transform swordRoot, Transform shieldRoot, float bladeLength)
        {
            swordArm = Rest(swordSide); shieldArm = Rest(shieldSide);
            sword = swordRoot; shield = shieldRoot; swordLength = bladeLength;
        }

        static Arm Rest(Arm arm)
        {
            arm.upperRest = arm.upper.localRotation; arm.foreRest = arm.fore.localRotation; arm.handRest = arm.hand.localRotation;
            return arm;
        }

        /// <summary>싸움 상태를 자세로 옮긴다. 화면을 그리는 쪽이 매번 부른다.</summary>
        public void Show(GruntStep step, GruntAttack attack, float timer, GruntSettings s)
        {
            bool slam = attack == GruntAttack.Slam;
            var ready = slam ? SwordUp : SwordSide;
            switch (step)
            {
                case GruntStep.Rise:
                case GruntStep.Hold:
                    target = ready;
                    break;
                case GruntStep.Strike:
                    // 내려치는 구간은 남은 시간으로 직접 잇는다(끝나는 순간이 막기 판정이다).
                    float k = 1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, s.strikeSeconds));
                    target = shown = Blend(ready, slam ? SwordDown : SwordAcross, k * k);
                    break;
                case GruntStep.Recover:
                    target = slam ? SwordDown : SwordAcross;
                    break;
                case GruntStep.Stagger:
                    target = Open;
                    break;
                default:
                    target = Stance;
                    break;
            }
            if (!Application.isPlaying) { shown = target; Apply(); }
        }

        void LateUpdate()
        {
            float k = follow <= 0f || !started ? 1f : 1f - Mathf.Exp(-follow * Time.deltaTime);
            started = true;
            shown = Blend(shown, target, k);
            Apply();
        }

        static Pose Blend(Pose a, Pose b, float k) => new Pose
        {
            swordUpper = Vector3.Slerp(a.swordUpper, b.swordUpper, k), swordFore = Vector3.Slerp(a.swordFore, b.swordFore, k),
            blade = Vector3.Slerp(a.blade, b.blade, k),
            shieldUpper = Vector3.Slerp(a.shieldUpper, b.shieldUpper, k), shieldFore = Vector3.Slerp(a.shieldFore, b.shieldFore, k),
            shieldFace = Vector3.Slerp(a.shieldFace, b.shieldFace, k),
        };

        void Apply()
        {
            if (swordArm.upper == null || shieldArm.upper == null) return;
            var frame = transform.rotation;
            Aim(swordArm, frame * shown.swordUpper, frame * shown.swordFore);
            Aim(shieldArm, frame * shown.shieldUpper, frame * shown.shieldFore);
            if (sword != null)
            {
                var along = (frame * shown.blade).normalized;
                sword.SetPositionAndRotation(swordArm.hand.position + along * (swordLength * 0.5f), Quaternion.LookRotation(along, frame * Vector3.up));
            }
            if (shield != null)
            {
                var face = (frame * shown.shieldFace).normalized;
                var middle = (shieldArm.fore.position + shieldArm.hand.position) * 0.5f;
                shield.SetPositionAndRotation(middle + face * shieldOffset, Quaternion.LookRotation(face, frame * Vector3.up));
            }
        }

        /// <summary>기준 자세에서 시작해 위팔이 upper 쪽을, 아래팔이 fore 쪽을 향하게 돌린다. 매번 기준에서 다시 시작하므로 비틀림이 쌓이지 않는다.</summary>
        static void Aim(Arm arm, Vector3 upper, Vector3 fore)
        {
            arm.upper.localRotation = arm.upperRest;
            arm.fore.localRotation = arm.foreRest;
            arm.hand.localRotation = arm.handRest;
            Point(arm.upper, arm.fore, upper);
            Point(arm.fore, arm.hand, fore);
        }

        static void Point(Transform bone, Transform child, Vector3 direction)
        {
            var now = child.position - bone.position;
            if (now.sqrMagnitude < 1e-10f || direction.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(now, direction) * bone.rotation;
        }
    }
}
