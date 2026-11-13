namespace Anseong.Combat
{
    /// <summary>
    /// 사령관 대사 번호와 글자(M4-06, 10_game_scenario.md 번호표). 목소리는 사령관 하나(확정 사항 12).
    /// 소리 파일 이름은 VO_Commander_&lt;두 자리 번호&gt;(윤주 녹음). 문구를 바꾸면 여기 글자만 고친다.
    /// 대본에서 안 쓰는 옛 번호(12·14·15)는 글자를 비워 둔다(코드가 내도 아무것도 안 뜸).
    /// </summary>
    public static class CommanderLines
    {
        // 장면 1 기체 점검
        public const int Reach = 1;
        public const int GuardPose = 2;
        public const int PushTogether = 3;
        public const int JointTest = 4;
        public const int CallOut = 26;
        public const int TriggerNow = 39;
        public const int WeaponsBrief = 42;
        // 장면 2~3
        public const int Sortie = 6;
        public const int Ambush = 18;
        public const int Found = 44;
        public const int FindAndShoot = 45;
        public const int ArmorBounces = 43;
        public const int DronesCleared = 46;
        // 장면 4~
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
        public const int GunLost = 20;
        public const int AllWeaponsLost = 23;
        public const int Reboot = 25;
        public const int Hacked = 30;
        public const int Restored = 31;
        public const int ShootCrane = 33;
        public const int Stuck = 34;
        public const int PushOn = 35;
        public const int DronesAgain = 36;
        public const int MeleeArms = 37;
        public const int NoGun = 38;
        public const int TargetCannon = 47;
        public const int TargetArmor = 48;
        public const int TargetCore = 49;
        // 잡고 베기(11/13)
        public const int GrabHint = 50;
        public const int GrabHeld = 51;
        public const int GrabLost = 52;
        // 검 맞부딪힘 힘겨루기(11/13)
        public const int ClashHold = 53;
        public const int ClashWon = 54;
        public const int ClashLost = 55;

        public static string Text(int line)
        {
            switch (line)
            {
                case Reach: return "정면을 보고 두 팔을 끝까지 뻗어라.";
                case GuardPose: return "편한 가드 자세에서 그립.";
                case PushTogether: return "둘이 같이 스틱을 앞으로!";
                case JointTest: return "무장 달기 전 관절 시험. 번갈아 쳐라!";
                case Sortie: return "목표 확인. 전투 개시!";
                case IncomingAttack: return "포탄이 온다! 불러 줘라!";
                case CounterNow: return "지금이다!";
                case KneelFinisher: return "둘이 함께 팔을 뻗어라!";
                case Victory: return "작전 성공. 안성제철소를 지켜냈다.";
                case FirstSlam: return "두 팔로 막아라!";
                case FirstCharge: return "막을 수 없다! 불러 주고 같이 피하라!";
                case FinisherHint: return "두 팔을 정면으로 쭉 뻗어라!";
                case Ambush: return "피격! 어디선가 쏜다. 왼팔, 스캐너로 찾아라!";
                case GunLost: return "주포 파손! 칼과 방패뿐이다!";
                case AllWeaponsLost: return "무장 전부 상실. 처음 배운 주먹이 남았다. 쥐어라!";
                case Reboot: return "기체 정지…. 재기동한다!";
                case CallOut: return "네 쪽에 보이는 건 상대에게 안 보인다. 불러 줘라!";
                case Hacked: return "팔이 해킹됐다! 반대 팔로 뜯어내라!";
                case Restored: return "제어 복구.";
                case ShootCrane: return "크레인 고리를 찾아 쏴라!";
                case Stuck: return "박혔다! 몰아쳐라!";
                case PushOn: return "그대로 밀어붙여!";
                case DronesAgain: return "또 숨었다! 사격 무장!";
                case MeleeArms: return "근접 무장!";
                case NoGun: return "쏠 무기가 없다! 달려들면 쳐라!";
                case TriggerNow: return "트리거!";
                case WeaponsBrief: return "왼팔이 찾고 오른팔이 쏜다. 주포는 드론용이다. 붙으면 칼과 방패.";
                case ArmorBounces: return "장갑에 안 통한다! 저놈은 붙어서 부숴야 한다!";
                case Found: return "찾았다! 오른팔, 쏴라!";
                case FindAndShoot: return "그거다. 왼쪽이 찾고, 오른쪽이 쏜다.";
                case DronesCleared: return "드론 전멸. 놈이 직접 온다!";
                case TargetCannon: return "놈의 팔뚝 포를 부숴라!";
                case TargetArmor: return "놈의 방패를 깨라!";
                case TargetCore: return "놈도 맨손이다! 가슴 핵을 부숴라!";
                case GrabHint: return "휘청인다! 왼팔, 뻗어서 붙잡아라!";
                case GrabHeld: return "붙잡았다! 오른팔, 베어라!";
                case GrabLost: return "놓쳤다!";
                case ClashHold: return "검이 맞물렸다! 둘 다 팔을 들어 버텨라!";
                case ClashWon: return "밀어냈다!";
                case ClashLost: return "밀렸다! 버텨!";
                default: return string.Empty;
            }
        }

        public static string SoundName(int line) => $"VO_Commander_{line:00}";

        /// <summary>대본에서 쓰는 번호 전부(소리 표 기본 줄, 녹음 목록).</summary>
        public static readonly int[] All =
        {
            1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 13, 16, 18, 20, 23, 25, 26, 30, 31, 33, 34, 35, 36, 37, 38, 39, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55,
        };
    }
}
