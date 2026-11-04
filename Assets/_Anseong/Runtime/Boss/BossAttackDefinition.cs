using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 기술 하나 = 에셋 하나(M2-02). 시간·대응·피해와 클립 칸. 'Anseong → Boss → Create Attack Assets'가 05 제안값으로 5개를 만든다.
    /// 클립 칸이 비면 컨트롤러 메뉴가 임시 클립(Punch·Walk 등)을 쓴다. 진짜 클립이 오면 칸만 바꾸고 Rebuild를 다시 돌린다.
    /// </summary>
    [CreateAssetMenu(menuName = "Anseong/Boss Attack", fileName = "BossAttack")]
    public class BossAttackDefinition : ScriptableObject
    {
        [Tooltip("이 기술의 보스 클립(A-01). 비우면 임시 클립. 같은 Mixamo 뼈 이름이어야 한다.")]
        public AnimationClip clip;

        [Tooltip("클립 재생 속도(1 = 원래). 임시 클립을 기술 시간에 맞출 때.")]
        [Min(0.1f)] public float clipSpeed = 1f;

        public BossAttackSpec spec = new BossAttackSpec();

        public BossAttackKind Kind => spec.kind;

        /// <summary>보스 Animator 컨트롤러의 상태 이름(메뉴가 이 이름으로 만든다).</summary>
        public static string StateName(BossAttackKind kind) => "Attack_" + kind;
    }
}
