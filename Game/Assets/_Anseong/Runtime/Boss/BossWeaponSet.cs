using System;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>보스 무기 부품을 붙일 뼈(메뉴가 이름으로 찾아 <see cref="BossWeaponVisuals"/>에 채운다).</summary>
    public enum BossBone : byte
    {
        LeftForeArm,
        RightForeArm,
        LeftHand,
        RightHand,
        Chest,
    }

    /// <summary>
    /// 보스 무기 목록(데이터, M4-02). 차례대로 부수는 무기 3개. 무기마다 이름·내구도·부서질 때 보복 공격·부품(붙일 뼈, 모델 또는 도형).
    /// 무기 구성을 바꾸려면 코드 말고 이 에셋만 고친다. Inspector 오른쪽 위 ⋮ 메뉴에 "확정안"·"옛 안" 채우기가 있다.
    /// 'Anseong → Boss → Create Attack Assets'가 Data/Boss/BossWeaponSet.asset으로 만들고 페이즈 표·보스에 넣는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Anseong/Boss Weapon Set", fileName = "BossWeaponSet")]
    public class BossWeaponSet : ScriptableObject
    {
        public enum Placement : byte
        {
            /// <summary>팔뚝을 따라(뼈 → 손 방향 가운데). 팔뚝 포.</summary>
            AlongForeArm,
            /// <summary>뼈 앞쪽(보스 정면)으로 띄움. 가슴 장갑판·핵.</summary>
            InFront,
            /// <summary>손에 쥠(손 뼈 원점, 손가락 쪽으로). 검·방패.</summary>
            InHand,
        }

        /// <summary>우리 무기 모델을 빌려 쓰기(성재 Weapon_Cannon·Sword·Shield, 재질만 보스 색).</summary>
        public enum PlayerModel : byte
        {
            None,
            Cannon,
            Sword,
            Shield,
        }

        [Serializable]
        public class Part
        {
            [Tooltip("이름(디버그·로그).")] public string label = "부품";
            [Tooltip("붙일 뼈.")] public BossBone bone;
            [Tooltip("붙이는 방식.")] public Placement placement;
            [Tooltip("아트 모델. 비면 아래 '우리 무기 빌리기', 그것도 비면 도형.")] public GameObject prefab;
            [Tooltip("모델 칸이 비면 우리 로봇 무기(PlayerWeaponVisuals) 같은 모델을 보스 재질로 쓴다.")] public PlayerModel borrow;
            public PrimitiveType shape = PrimitiveType.Cube;
            [Tooltip("도형 크기(보스 가슴~머리 거리 배수, 팔뚝이면 팔뚝 길이 배수).")] public Vector3 size = Vector3.one * 0.5f;
            [Tooltip("앞으로(InFront)·손가락 쪽으로(InHand) 밀어낸 거리(같은 배수).")] public float push = 0.3f;
        }

        [Serializable]
        public class Weapon
        {
            [Tooltip("HUD·글자판에 나오는 이름.")] public string name = "무기";
            [Range(5f, 33f), Tooltip("내구도(보스 최대 체력 대비 %, 대본 30). 금 3개 = 부서짐.")] public float durability = 30f;
            [Tooltip("부서질 때 보복을 하나.")] public bool retaliate = true;
            [Tooltip("보복 공격(대본: 포 → 돌진, 둘째 → 두 손 내려찍기). 셋째(핵)는 보복 대신 무릎.")] public BossAttackKind retaliation = BossAttackKind.Charge;
            [Tooltip("금 가기 전 색(도형일 때).")] public Color color = new Color(0.35f, 0.35f, 0.38f);
            public Part[] parts = new Part[0];
        }

        [Tooltip("부수는 차례. 3개(마지막 = 핵, 부서지면 무릎 → 필살기).")]
        public Weapon[] weapons = DefaultWeapons();

        /// <summary>무기 i(0~2). 없으면 null.</summary>
        public Weapon At(int i) => weapons != null && i >= 0 && i < weapons.Length ? weapons[i] : null;

        /// <summary>페이즈 경계(%): 무기마다 내구도를 차례로 뺀다(30·30·30 → 70·40·10).</summary>
        public float[] Thresholds()
        {
            var d = new float[BossWeaponRules.WeaponCount];
            for (var i = 0; i < d.Length; i++) d[i] = At(i) != null ? At(i).durability : 30f;
            return BossWeaponRules.Thresholds(d);
        }

        static readonly Color Armor = new Color(0.35f, 0.35f, 0.38f);
        static readonly Color CoreRed = new Color(1f, 0.1f, 0.05f);

        /// <summary>
        /// 11/11 확정(시나리오 스레드): 보스도 우리와 같은 무장(팔뚝 포 + 오른손 검 + 왼손 방패), 가슴에 핵. 부수는 차례 팔뚝 포 → 방패 → 핵.
        /// 방패가 부서지면 검 내려치기 보복, 보스 검도 같이 부서진다(그래서 검은 둘째 무기 부품). 3페이즈는 맨주먹, 목표는 핵. 클립은 그대로.
        /// </summary>
        public static Weapon[] DefaultWeapons() => new[]
        {
            Cannons(),
            new Weapon
            {
                name = "방패", retaliation = BossAttackKind.SlamBoth, color = new Color(0.3f, 0.32f, 0.45f),
                parts = new[]
                {
                    new Part { label = "Shield", borrow = PlayerModel.Shield, bone = BossBone.LeftHand, placement = Placement.InHand, shape = PrimitiveType.Cube, size = new Vector3(1.2f, 1.5f, 0.15f), push = 0.25f },
                    new Part { label = "Sword", borrow = PlayerModel.Sword, bone = BossBone.RightHand, placement = Placement.InHand, shape = PrimitiveType.Cube, size = new Vector3(0.12f, 2.2f, 0.05f), push = 1.1f },
                },
            },
            Core(),
        };

        /// <summary>11/09 옛 안: 팔뚝 포 → 가슴 장갑판 → 핵(되돌릴 때).</summary>
        public static Weapon[] ArmorWeapons() => new[]
        {
            Cannons(),
            new Weapon
            {
                name = "가슴 장갑판", retaliation = BossAttackKind.SlamBoth, color = Armor,
                parts = new[] { new Part { label = "Chest Armor", bone = BossBone.Chest, placement = Placement.InFront, shape = PrimitiveType.Cube, size = new Vector3(1.3f, 1.1f, 0.2f), push = 0.35f } },
            },
            Core(),
        };

        static Weapon Cannons() => new Weapon
        {
            name = "팔뚝 포", retaliation = BossAttackKind.Charge, color = Armor,
            parts = new[]
            {
                new Part { label = "Arm Cannon L", borrow = PlayerModel.Cannon, bone = BossBone.LeftForeArm, placement = Placement.AlongForeArm, shape = PrimitiveType.Cylinder, size = new Vector3(0.35f, 0.45f, 0.35f) },
                new Part { label = "Arm Cannon R", borrow = PlayerModel.Cannon, bone = BossBone.RightForeArm, placement = Placement.AlongForeArm, shape = PrimitiveType.Cylinder, size = new Vector3(0.35f, 0.45f, 0.35f) },
            },
        };

        static Weapon Core() => new Weapon
        {
            name = "핵", retaliate = false, retaliation = BossAttackKind.SlamBoth, color = CoreRed,
            parts = new[] { new Part { label = "Core", bone = BossBone.Chest, placement = Placement.InFront, shape = PrimitiveType.Sphere, size = Vector3.one * 0.45f, push = 0.2f } },
        };

        [ContextMenu("확정안 채우기 (팔뚝 포 → 방패·검 → 핵)")]
        void FillDefault() => weapons = DefaultWeapons();

        [ContextMenu("옛 안 채우기 (팔뚝 포 → 가슴 장갑판 → 핵)")]
        void FillArmor() => weapons = ArmorWeapons();
    }
}
