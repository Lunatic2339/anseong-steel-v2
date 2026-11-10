namespace Anseong.Flow
{
    /// <summary>지금 두 팔에 든 무기(10_game_scenario.md "역할": 왼팔 = 방패/스캐너, 오른팔 = 검/포).</summary>
    public enum Loadout : byte
    {
        /// <summary>맨손(관절 시험, 장면 7 뒤).</summary>
        Bare,
        /// <summary>근접 무장: 왼 방패, 오른 검.</summary>
        Melee,
        /// <summary>사격 무장: 왼 스캐너, 오른 포.</summary>
        Ranged,
    }

    /// <summary>
    /// 무기 고르기(M4-03, 확정 사항 7·9: 상황이 무기를 고른다). 순수 함수.
    /// 멀거나 드론이 오면 포, 붙어 있으면 검·방패, 다 잃으면 주먹. 장면 5에서 포, 장면 7에서 검·방패를 잃는다.
    /// </summary>
    public static class WeaponRules
    {
        /// <summary>그 장면에 아직 포(스캐너·포)가 있는지.</summary>
        public static bool HasGun(MatchScene scene) => scene <= MatchScene.FightCannon || scene == MatchScene.Rebooting;

        /// <summary>그 장면에 아직 검·방패가 있는지.</summary>
        public static bool HasSword(MatchScene scene) => scene <= MatchScene.FightArmor || scene == MatchScene.Rebooting;

        /// <param name="checkupStep">기체 점검 단계(<see cref="CheckupStep"/>): 1-3에서 검·방패, 1-4부터 스캐너·포를 단다.</param>
        public static Loadout For(MatchScene scene, bool dronesActive, MatchScene resumeScene = MatchScene.Waiting, int checkupStep = 0)
        {
            if (scene == MatchScene.Rebooting) scene = resumeScene;
            switch (scene)
            {
                case MatchScene.Waiting:
                    return Loadout.Bare;
                case MatchScene.Checkup:
                    if (checkupStep >= (int)CheckupStep.GunBrief) return Loadout.Ranged;
                    return checkupStep == (int)CheckupStep.ShieldDrill ? Loadout.Melee : Loadout.Bare;
                case MatchScene.Entrance:
                case MatchScene.DroneHunt:
                case MatchScene.Approach:
                    return Loadout.Ranged;
                case MatchScene.FightCannon:
                    return dronesActive ? Loadout.Ranged : Loadout.Melee;
                case MatchScene.LostGun:
                case MatchScene.FightArmor:
                    return Loadout.Melee;
                default:
                    return Loadout.Bare;
            }
        }
    }
}
