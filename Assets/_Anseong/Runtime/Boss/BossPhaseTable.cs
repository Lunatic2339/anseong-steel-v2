using System;
using UnityEngine;

namespace Anseong.Boss
{
    [Serializable]
    public struct AttackWeight
    {
        public BossAttackKind kind;
        [Min(0f)] public float weight;

        public AttackWeight(BossAttackKind kind, float weight)
        {
            this.kind = kind;
            this.weight = weight;
        }
    }

    [Serializable]
    public class BossPhaseEntry
    {
        [Tooltip("페이즈 첫 공격(새 기술을 먼저 보여준다).")]
        public BossAttackKind firstAttack;
        [Tooltip("이후 고르는 비율. 단발 펀치는 왼손·오른손에 반씩 나눠 적는다.")]
        public AttackWeight[] weights = new AttackWeight[0];
        [Tooltip("공격 사이 쉬는 시간 최소(초).")]
        [Min(0f)] public float restMin = 2.5f;
        [Tooltip("공격 사이 쉬는 시간 최대(초).")]
        [Min(0f)] public float restMax = 3.5f;
        [Tooltip("이 페이즈 시작 뒤 이 횟수 안에 좌우 연타를 한 번 보여준다(0 = 규칙 없음). 05: 2페이즈 3.")]
        [Min(0)] public int showComboWithin;
    }

    /// <summary>
    /// 페이즈별 공격 순서표(05 "페이즈별 순서", "페이즈 전환"). 에셋 하나. 기본값 = 05 제안값.
    /// 'Anseong → Boss → Create Attack Assets'가 Data/Boss/BossPhaseTable.asset으로 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "Anseong/Boss Phase Table", fileName = "BossPhaseTable")]
    public class BossPhaseTable : ScriptableObject
    {
        [Tooltip("페이즈 1·2·3.")]
        public BossPhaseEntry[] phases = DefaultPhases();

        [Tooltip("페이즈가 바뀌는 보스 HP(%): 1→2, 2→3, 3→무릎. 그 아래로는 한 번에 내려가지 않는다.")]
        public float[] thresholds = { 60f, 25f, 10f };

        [Tooltip("페이즈 전환 포효 길이(초, 무적).")]
        [Min(0f)] public float roarSeconds = 2f;

        [Tooltip("페이즈 시작 때 조종사 HP가 이것(%) 미만이면 여기까지 회복(05: 60, 50과 비교 예정). 0이면 회복 없음.")]
        [Range(0f, 100f)] public float pilotRestorePercent = 60f;

        [Tooltip("무작위 시드(0 = 판마다 다름). 같은 판을 다시 보려면 숫자를 적는다.")]
        public int seed;

        public static BossPhaseEntry[] DefaultPhases() => new[]
        {
            new BossPhaseEntry
            {
                firstAttack = BossAttackKind.PunchRight, restMin = 2.5f, restMax = 3.5f,
                weights = new[] { new AttackWeight(BossAttackKind.PunchLeft, 50f), new AttackWeight(BossAttackKind.PunchRight, 50f) },
            },
            new BossPhaseEntry
            {
                firstAttack = BossAttackKind.SlamBoth, restMin = 2f, restMax = 3f, showComboWithin = 3,
                weights = new[]
                {
                    new AttackWeight(BossAttackKind.PunchLeft, 25f), new AttackWeight(BossAttackKind.PunchRight, 25f),
                    new AttackWeight(BossAttackKind.AlternatingCombo, 25f), new AttackWeight(BossAttackKind.SlamBoth, 25f),
                },
            },
            new BossPhaseEntry
            {
                firstAttack = BossAttackKind.Charge, restMin = 1.5f, restMax = 2.5f,
                weights = new[]
                {
                    new AttackWeight(BossAttackKind.PunchLeft, 17.5f), new AttackWeight(BossAttackKind.PunchRight, 17.5f),
                    new AttackWeight(BossAttackKind.AlternatingCombo, 20f), new AttackWeight(BossAttackKind.SlamBoth, 20f),
                    new AttackWeight(BossAttackKind.Charge, 25f),
                },
            },
        };
    }
}
