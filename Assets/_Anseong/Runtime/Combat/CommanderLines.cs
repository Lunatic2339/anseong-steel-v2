namespace Anseong.Combat
{
    /// <summary>
    /// 사령관 대사 번호와 글자(S-01 제안 1~10 + M2 추가). 소리 파일 이름은 VO_Commander_&lt;두 자리 번호&gt;.
    /// 문구는 11/05 전 팀 회의에서 확정(바꾸면 여기 글자만 고친다).
    /// </summary>
    public static class CommanderLines
    {
        public const int IncomingAttack = 7;
        public const int CounterNow = 8;
        public const int KneelFinisher = 9;
        public const int Victory = 10;
        public const int FirstSlam = 11;
        public const int FirstCombo = 12;
        public const int FirstCharge = 13;
        public const int Phase2 = 14;
        public const int Phase3 = 15;
        public const int FinisherHint = 16;
        // M4-01 판 흐름(10_game_scenario.md 번호). 번호표 전체 정리는 M4-06.
        public const int Sortie = 6;
        public const int Ambush = 18;
        public const int GunLost = 20;
        public const int AllWeaponsLost = 23;
        public const int Reboot = 25;
        public const int DronesCleared = 46;
        public const int TargetCannon = 47;
        public const int TargetArmor = 48;
        public const int TargetCore = 49;

        public static string Text(int line)
        {
            switch (line)
            {
                case 1: return "조종사, 담당 팔을 들어 연결을 확인하라.";
                case 2: return "편한 가드 자세에서 그립.";
                case 3: return "정면으로 끝까지 뻗고 그립.";
                case 4: return "표적을 쳐라.";
                case 5: return "양팔 가드. 출격 준비 완료.";
                case 6: return "출격한다!";
                case IncomingAttack: return "적의 공격이 온다, 막아라!";
                case CounterNow: return "지금이다, 반격하라!";
                case KneelFinisher: return "적이 무릎 꿇었다. 둘이 함께 팔을 뻗어라!";
                case Victory: return "작전 성공.";
                case FirstSlam: return "두 팔로 막아라!";
                case FirstCombo: return "들어오는 쪽을 번갈아 막아라!";
                case FirstCharge: return "막을 수 없다! 둘이 같은 쪽으로 피하라!";
                case Phase2: return "적이 분노했다. 새 공격에 대비하라!";
                case Phase3: return "마지막 발악이다. 버텨라!";
                case FinisherHint: return "두 팔을 정면으로 뻗고 버튼을 모두 눌러라!";
                case Ambush: return "피격! 어디선가 쏜다. 왼팔, 스캐너로 찾아라!";
                case GunLost: return "주포 파손! 칼과 방패뿐이다!";
                case AllWeaponsLost: return "무장 전부 상실. 처음 배운 주먹이 남았다. 쥐어라!";
                case Reboot: return "기체 정지…. 재기동한다!";
                case DronesCleared: return "드론 전멸. 놈이 직접 온다!";
                case TargetCannon: return "놈의 팔뚝 포를 부숴라!";
                case TargetArmor: return "가슴 장갑을 뜯어내라!";
                case TargetCore: return "핵이 드러났다! 주먹으로 부숴라!";
                default: return string.Empty;
            }
        }

        public static string SoundName(int line) => $"VO_Commander_{line:00}";
    }
}
