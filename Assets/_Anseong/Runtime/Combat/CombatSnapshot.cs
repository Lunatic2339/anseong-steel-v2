using Anseong.Pilot;

namespace Anseong.Combat
{
    /// <summary>
    /// 서버가 계산한 전투 상태 한 장(M2-01). 보스 HP 막대·안내판처럼 "지금 상태"를 보는 쪽은 이것만 읽는다.
    /// 서버가 바뀔 때마다 모든 PC에 복제한다. 클라이언트(M3 PC-B)에는 규칙 클래스가 없으므로 규칙 객체를 직접 읽지 않는다.
    /// </summary>
    public struct CombatSnapshot
    {
        /// <summary>보스 HP(최대 대비 %가 아니라 실제 값, 기본 최대 100).</summary>
        public float BossHealth;
        public float BossMax;
        /// <summary>0 = 연계 없음, 1·2 = 잽 단계.</summary>
        public int ComboStep;
        /// <summary>다음 차례 팔(ComboStep &gt; 0일 때만 의미).</summary>
        public PilotSide ComboNextSide;
        public bool BoostOpen;
        public PilotSide BoostSide;
        /// <summary>조종사 로봇 HP(M2-03).</summary>
        public float PilotHealth;
        public float PilotMax;
        /// <summary>보스 페이즈 1·2·3, 무릎이면 4(M2-04).</summary>
        public int BossPhase;

        public float BossFraction => BossMax > 0f ? BossHealth / BossMax : 0f;
        public bool BossDefeated => BossHealth <= 0f;
        public float PilotFraction => PilotMax > 0f ? PilotHealth / PilotMax : 0f;

        public static CombatSnapshot Full(float bossMax, float pilotMax = 100f) => new CombatSnapshot
        {
            BossHealth = bossMax, BossMax = bossMax, PilotHealth = pilotMax, PilotMax = pilotMax, BossPhase = 1,
        };

        public static CombatSnapshot Of(CombatResolver resolver, PilotHealth pilot = null, int bossPhase = 1) => new CombatSnapshot
        {
            BossHealth = resolver.Boss.Current,
            BossMax = resolver.Boss.Max,
            ComboStep = resolver.Combo.Step,
            ComboNextSide = resolver.Combo.NextSide,
            BoostOpen = resolver.Boost.IsOpen,
            BoostSide = resolver.Boost.Side,
            PilotHealth = pilot != null ? pilot.Current : 100f,
            PilotMax = pilot != null ? pilot.Max : 100f,
            BossPhase = bossPhase,
        };
    }

    /// <summary>
    /// 때린 쪽 PC → 전투 서버 보고 통로(M2-01). 호스트에서는 같은 PC 안 호출, M3에서는 같은 메서드가 ServerRpc로 간다.
    /// <see cref="CombatDirector"/>는 이 통로만 알고, 네트워크 코드는 모른다.
    /// </summary>
    public interface ICombatLink
    {
        void SubmitHit(HitReport report);
        void SubmitMiss(PilotSide side, PunchKind kind, int strikeId);
        /// <summary>그립 상태가 바뀌었거나 트리거를 새로 눌렀을 때만(부스트 입력).</summary>
        void SubmitArmInput(PilotSide side, bool gripHeld, bool triggerPressed);
        /// <summary>운영자 재시작·조절 메뉴 "보스 HP 다시 채우기".</summary>
        void RequestReset();
        /// <summary>그 팔의 가드가 켜지거나 꺼짐(M2-03, 바뀐 때만).</summary>
        void SubmitGuard(PilotSide side, bool guarding);
        /// <summary>그 조종사의 회피 스틱(M2-03, 바뀐 때만, -1~1).</summary>
        void SubmitStick(PilotSide side, UnityEngine.Vector2 stick);
    }
}
