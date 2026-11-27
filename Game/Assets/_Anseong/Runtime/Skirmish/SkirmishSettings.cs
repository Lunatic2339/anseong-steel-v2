using System;
using Anseong.Core;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>
    /// Z2 교전 수치. 전부 처음 시험용 후보값이고 헤드셋에서 해 보고 고친다.
    /// 좌표는 확대 맵 기준(m, x 동서 / y 높이 / z 남북).
    /// </summary>
    [Serializable]
    public partial class SkirmishSettings
    {
        [Header("메카 (P1)")]
        public Vector3 mechStart = new Vector3(0f, 0f, 205f);
        [Tooltip("메카가 움직일 수 있는 범위(x 최소, z 최소, x 최대, z 최대).")]
        public Vector4 mechArea = new Vector4(-25f, 180f, 25f, 215f);
        [Tooltip("범위가 여러 칸일 때(길 + 마당). 비어 있지 않으면 mechArea 대신 이것들을 합친 곳을 쓴다.")]
        public Vector4[] mechZones = new Vector4[0];
        [Tooltip("재기동 뒤에 로봇이 다시 서는 자리를 따로 정한다. 끄면 mechStart.")]
        public bool restartElsewhere;
        public Vector3 mechRestart;
        [Min(0), Tooltip("내구도가 0이 되었을 때 그 장면을 다시 하는 횟수(명세: 한 판에 한 번). 0이면 바로 실패.")]
        public int reboots;
        [Min(0f), Tooltip("\"기체 재기동\" 시간(초).")]
        public float rebootSeconds = 3f;
        [Min(0.5f)] public float mechSpeed = 4f;
        [Min(0f), Tooltip("서 있다가 전속력이 되는 시간(초). 0 = 바로. 헤드셋에서 갑자기 출발하면 멀미가 난다.")]
        public float mechAccelSeconds = 0.4f;
        [Min(0f), Tooltip("전속력에서 멈추는 시간(초). 0 = 바로.")]
        public float mechStopSeconds = 0.3f;
        [Min(1f)] public float mechHull = 100f;
        [Tooltip("메카 가슴 높이(m). 큰 탄이 여기로 온다.")]
        public float mechChestHeight = 24f;
        [Tooltip("오른팔 포 높이(m).")]
        public float mechGunHeight = 26f;
        [Min(0.05f)] public float mechGunInterval = 0.5f;
        [Min(0f)] public float mechGunDamage = 10f;
        [Tooltip("가드한 팔 방향과 탄이 오는 방향이 이 각도(°) 안이면 막는다.")]
        [Range(10f, 120f)] public float guardDegrees = 50f;
        [Min(0f), Tooltip("딱 맞춰 막으면 이 시간(실제 초) 동안 시간이 느려진다. 두 사람 화면에 같이 적용된다. 0 = 끔.")]
        public float perfectSlowSeconds = 0.15f;
        [Range(0.05f, 1f), Tooltip("느려지는 동안의 빠르기(1 = 평소). 로봇의 이동은 느려지지 않는다(멀미).")]
        public float perfectSlowScale = 0.3f;

        [Header("공중포대 (P2)")]
        [Tooltip("메카 오른쪽 뒤 위.")]
        public Vector3 gunnerPost = new Vector3(20f, 50f, 180f);
        [Min(0.02f)] public float cannonInterval = 0.125f;
        [Min(0f)] public float cannonDamage = 4f;
        [Tooltip("한 발에 오르는 열(1에서 과열).")]
        [Range(0f, 0.5f)] public float cannonHeatPerShot = 0.03f;
        [Tooltip("초당 식는 양.")]
        [Min(0f)] public float cannonCooling = 0.2f;
        [Tooltip("과열 뒤 이 값까지 식어야 다시 쏜다.")]
        [Range(0f, 0.9f)] public float cannonResumeHeat = 0.4f;
        [Min(0)] public int missiles = 2;
        [Tooltip("미사일 잠금에 걸리는 시간(초).")]
        [Min(0.1f)] public float lockSeconds = 1f;
        [Min(1f)] public float missileSpeed = 70f;
        [Min(0f)] public float missileDamage = 90f;

        [Header("일반병 공통 (24m 2족)")]
        public float enemyHeight = 24f;
        [Tooltip("조준 판정 반지름(m). 몸 전체를 표적으로 삼는다.")]
        public float enemyRadius = 5.5f;
        public float enemyMuzzleHeight = 18f;
        [Min(0.1f)] public float staggerSeconds = 2f;
        [Tooltip("멈칫한 뒤 이 시간(초) 동안은 다시 멈칫하지 않는다.")]
        [Min(0f)] public float staggerImmunity = 4f;

        [Header("G3 — 메카 정면 압박 (메카 몸에 가리지 않게 정면 오른쪽 43m)")]
        public Vector3 g3Start = new Vector3(17f, 0f, 256f);
        public Vector3 g3Post = new Vector3(15f, 0f, 245f);
        [Min(0.1f)] public float g3Speed = 3f;
        [Min(1f)] public float g3Hp = 260f;
        [Tooltip("큰 탄 발사 예고(초).")]
        [Min(0.1f)] public float g3Windup = 2f;
        [Min(0.1f)] public float g3Recover = 4f;
        [Tooltip("예고 중 이만큼 맞으면 발사가 끊긴다. 메카 혼자서는 못 넘고 둘이 같이 쏘면 넘는다.")]
        [Min(1f)] public float g3InterruptDamage = 60f;
        [Min(1f)] public float bigShellSpeed = 16f;
        [Tooltip("요격 판정 반지름(m).")]
        public float bigShellRadius = 3.5f;
        [Tooltip("요격에 필요한 기관포 명중 수.")]
        [Min(1)] public int bigShellHits = 2;
        [Min(0f)] public float bigShellDamage = 25f;
        [Tooltip("탄이 메카 가슴에서 이 거리(m) 안에 오면 맞거나 막힌다.")]
        public float bigShellContact = 4f;

        [Header("G4 — 고로 포격")]
        public Vector3 g4Start = new Vector3(110f, 0f, 300f);
        public Vector3 g4Post = new Vector3(90f, 0f, 250f);
        [Min(0.1f)] public float g4Speed = 4f;
        [Min(1f)] public float g4Hp = 160f;
        [Tooltip("고로 포격 충전(초).")]
        [Min(0.1f)] public float g4Charge = 5f;
        [Min(0.1f)] public float g4Recover = 4f;
        [Min(1f)] public float g4InterruptDamage = 50f;
        [Min(1f)] public float furnaceShellSpeed = 60f;

        [Header("돌격형 (장면 1부터)")]
        [Tooltip("멀리서 쏘는 적(G3, G4)을 내보낸다. 끄면 돌격형만 나온다.")]
        public bool rangedEnemies = true;
        [Tooltip("P1의 오른팔 포. 주먹으로 싸우는 장면에서는 끈다.")]
        public bool mechGun = true;
        public GruntSettings grunt = new GruntSettings();
        [Tooltip("차례로 나오는 돌격형. 비어 있으면 돌격형이 없다.")]
        public GruntWave[] grunts = new GruntWave[0];
        [Tooltip("앞의 돌격형이 쓰러지고 다음이 나올 때까지(초).")]
        [Min(0f)] public float gruntGap = 2f;
        [Tooltip("맞붙던 적이 쓰러지고 기다리던 적이 들어올 때까지(초).")]
        [Min(0f)] public float gruntSwapSeconds = 3f;
        public float gruntHeight = 30f;
        [Tooltip("포 조준 판정 반지름(m).")]
        public float gruntRadius = 6.5f;

        [Header("보스전 (기획안 2026-11-26)")]
        [Tooltip("로봇이 바라보는 방향(°, 0 = 북쪽 +Z, 90 = 동쪽 +X). 로봇 모델, 조종석, P1의 정면이 같이 돈다.")]
        public float mechYaw;
        [Tooltip("P2가 자리에서 바라보는 방향(°, 0 = 북쪽). 싸움을 옆에서 볼 때 쓴다.")]
        public float gunnerYaw;
        [Range(0f, 45f), Tooltip("P2가 탄 헬기가 기수를 숙인 각도(°). 싸움을 가파르게 내려다보는 자리에서 계기판이 싸움을 가리지 않게 한다. P2의 머리(화면)는 기울지 않는다.")]
        public float gunnerPitch;
        [Min(0f), Tooltip("P1이 오른팔 검을 들고 있을 때 한 번 베는 피해. 0이면 주먹(입력에 실린 잽 25, 훅 35)을 쓴다.")]
        public float swordDamage;
        [Min(0.1f), Tooltip("스캔: 버튼을 누른 채 조준이 보스 위에 이만큼 머물면 약점이 뜬다(초).")]
        public float scanSeconds = 2f;
        [Min(0.1f), Tooltip("약점 표시가 떠 있는 시간(초). 이 동안 버튼을 놓으면 강한 탄이 나간다.")]
        public float weakPointSeconds = 4f;
        [Min(0.1f), Tooltip("약점 판정 반지름(m). 놓는 순간 조준이 이 안이어야 맞는다.")]
        public float weakPointRadius = 3f;
        [Min(0.1f), Tooltip("약점에 강한 탄이 맞았을 때 보스가 튕겨나는 시간(초).")]
        public float weakPointStagger = 4f;
        [Min(0f), Tooltip("로봇 내구도가 이 값 아래면 적의 예고가 길어진다. 0 = 끔.")]
        public float lowHullBelow;
        [Min(1f), Tooltip("내구도가 낮을 때 예고에 곱하는 값.")]
        public float lowHullTelegraphScale = 1.3f;
        [Min(0f), Tooltip("맞붙은 동안 자폭 드론이 하나씩 오는 간격(초). 0 = 안 온다. 혼자 할 때는 두 배로 뜸하다. 큰 탄의 속도·요격·피해를 그대로 쓴다.")]
        public float droneEvery;
        [Tooltip("자폭 드론이 뜨는 곳.")]
        public Vector3 droneFrom;
        [Min(0f), Tooltip("P1이 스틱을 옆으로 튕기면 비키는 거리(m). 0 = 옆으로 한 걸음 없음.")]
        public float sideStepDistance;
        [Min(0.05f), Tooltip("비키는 데(돌아오는 데) 걸리는 시간(초).")]
        public float sideStepSeconds = 0.4f;
        [Min(0f), Tooltip("비킨 자리에 머무는 시간(초). 그 뒤 제자리로 돌아온다.")]
        public float sideStepHold = 1.5f;
        [Min(0f), Tooltip("보스가 헬기를 노린 일격에 맞으면 P2의 포가 멈추는 시간(초).")]
        public float heliJamSeconds = 5f;
        [Min(1), Tooltip("자폭 떼 부르기에 한꺼번에 오는 드론 수. 혼자 할 때는 절반.")]
        public int swarmCount = 4;
        [Min(0f), Tooltip("막이 바뀔 때 보스가 물러서서 자세를 다시 잡는 시간(초). 그동안 서로 피해가 없다.")]
        public float actBreakSeconds = 2f;
        [Min(0f), Tooltip("검과 방패가 부서지는 순간 시간이 느려지는 길이(실제 초). 빠르기는 perfectSlowScale. 0 = 끔.")]
        public float breakSlowSeconds = 0.4f;

        [Header("고로 (B08)")]
        public Vector3 furnacePoint = new Vector3(100f, 45f, 715f);
        [Tooltip("고로가 버티는 피격 수.")]
        [Min(1)] public int furnaceHits = 3;

        /// <summary>규칙 수치는 호스트(P1) PC에서 바꾼 것만 판정에 쓰인다. P2 PC에서는 화면 표시에만 쓰인다.</summary>
        public void AddTuning(TuningList list)
        {
            const string Group = "교전: 규칙·연출", Team = "교전: 공중포대·성능";
            var s = this;
            list.Describe(Group, "호스트(P1) PC에서 바꾼 값만 판정에 쓰인다. 적을 때리고, 딱 맞춰 막아 본다.\n팔이 잠깐 서는 것과 시간이 느려지는 것이 어색하거나 어지럽지 않은지 본다.");
            list.AddFloat(Group, "sk.mechSpeed", "로봇 속도(m/s)", () => s.mechSpeed, v => s.mechSpeed = v, 4f, 16f, 0.5f, 1);
            list.AddFloat(Group, "sk.mechAccel", "전속력까지 걸리는 시간(초)", () => s.mechAccelSeconds, v => s.mechAccelSeconds = v, 0f, 1.5f, 0.05f);
            list.AddFloat(Group, "sk.mechStop", "멈추는 데 걸리는 시간(초)", () => s.mechStopSeconds, v => s.mechStopSeconds = v, 0f, 1.5f, 0.05f);
            list.AddFloat(Group, "sk.hitStop", "타격 멈춤(초)", () => s.grunt.hitStopSeconds, v => s.grunt.hitStopSeconds = v, 0f, 0.15f, 0.01f);
            list.AddFloat(Group, "sk.finishStop", "마무리 한 방 멈춤(초)", () => s.grunt.finishStopSeconds, v => s.grunt.finishStopSeconds = v, 0f, 0.3f, 0.01f);
            list.AddFloat(Group, "sk.slowSeconds", "딱 맞춰 막기: 느린 시간(초)", () => s.perfectSlowSeconds, v => s.perfectSlowSeconds = v, 0f, 0.4f, 0.01f);
            list.AddFloat(Group, "sk.slowScale", "딱 맞춰 막기: 느린 동안 빠르기", () => s.perfectSlowScale, v => s.perfectSlowScale = v, 0.1f, 1f, 0.05f);
            list.AddFloat(Group, "sk.perfectWindow", "딱 맞춰 막기로 쳐 주는 시간(초)", () => s.grunt.perfectWindow, v => s.grunt.perfectWindow = v, 0.1f, 0.6f, 0.05f);
            list.AddFloat(Group, "sk.engage", "적이 서는 거리(m)", () => s.grunt.engageDistance, v => s.grunt.engageDistance = v, 15f, 30f, 0.5f, 1);
            list.AddCheck(Group, "sk.hitStopFeel", "타격 순간 팔이 서는 것이 어색하지 않다");
            list.AddCheck(Group, "sk.slowFeel", "딱 맞춰 막을 때 느려짐이 어지럽지 않다");
            list.AddCheck(Group, "sk.enemySize", "맞붙은 적의 크기와 거리가 알맞다");

            list.Describe(Team, "두 PC를 접속해 P2 자리에서 본다. 고개를 빠르게 돌려 화면이 끊기는지 본다.");
            list.AddCheck(Team, "sk.p2View", "P2 자리에서 로봇과 적이 한눈에 보인다");
            list.AddCheck(Team, "sk.p2Aim", "P2의 포가 손이 가리키는 곳으로 나간다");
            list.AddCheck(Team, "sk.p2Shield", "P2 화면의 로봇도 차폐판을 펴고 접는다");
            list.AddCheck(Team, "sk.smooth", "고개를 돌려도 화면이 끊기지 않는다");
        }

        /// <summary>장면 1 "첫 상대": 정비고 앞 마당에서 돌격형 둘과 차례로 싸운다(제작 명세).</summary>
        public static SkirmishSettings Scene1() => new SkirmishSettings
        {
            mechStart = new Vector3(0f, 0f, 65f),
            mechArea = new Vector4(-40f, 40f, 40f, 100f),
            mechSpeed = 10f,
            gunnerPost = new Vector3(20f, 50f, 40f),
            missiles = 3,
            rangedEnemies = false,
            mechGun = false,
            reboots = 1,
            restartElsewhere = true,
            mechRestart = new Vector3(0f, 0f, 40f),
            grunts = new[]
            {
                // 돌격형 1: 내려치기만, 가드를 굳히지 않음, 첫 적이라 예고가 길다.
                new GruntWave { start = new Vector3(0f, 0f, 130f), spec = new GruntSpec { holdSeconds = 1.2f, guardEveryDamage = 0f } },
                // 돌격형 2: 한 대 맞으면 가드를 굳힌다. P2가 열어 줘야 한다.
                new GruntWave { start = new Vector3(15f, 0f, 135f), spec = new GruntSpec { firstGuardDamage = 25f, guardEveryDamage = 60f } },
            },
        };
    }

    public partial class SkirmishSettings
    {
        /// <summary>
        /// 장면 2 "적치장"의 돌격형 쪽(제작 명세): 제강동 옆 길을 달려 서 있는 돌격형 3과 맞붙고, 이어서 돌격형 4와 5가 같이 온다.
        /// 포격형 B·C와 고로 포격, 작업대 옮기기, 드론은 여기 넣지 않았다(P2 담당 몫).
        /// </summary>
        public static SkirmishSettings Scene2() => new SkirmishSettings
        {
            mechStart = new Vector3(0f, 0f, 65f),
            // 길(폭 28)과 적치장 마당.
            mechZones = new[] { new Vector4(-14f, 40f, 14f, 185f), new Vector4(-60f, 185f, 60f, 260f) },
            mechSpeed = 10f,
            gunnerPost = new Vector3(20f, 50f, 180f),
            missiles = 3,
            rangedEnemies = false,
            mechGun = false,
            reboots = 1,
            restartElsewhere = true,
            mechRestart = new Vector3(0f, 0f, 70f),
            grunts = new[]
            {
                // 돌격형 3: 제자리에서 큰 탄을 한 번 쏘고 기다린다. 내려치기와 휘두르기를 번갈아.
                new GruntWave
                {
                    start = new Vector3(0f, 0f, 222f), shellWhenRobotPassesZ = 90f,
                    spec = new GruntSpec { standStill = true, attacks = new[] { GruntAttack.Slam, GruntAttack.Swing } },
                },
                // 돌격형 4: 먼저 맞붙는다.
                new GruntWave
                {
                    start = new Vector3(-25f, 0f, 275f),
                    spec = new GruntSpec { attacks = new[] { GruntAttack.Slam, GruntAttack.Slam, GruntAttack.Swing } },
                },
                // 돌격형 5: 4와 같이 출발해 35 거리에서 기다리며 큰 탄을 쏜다.
                new GruntWave
                {
                    start = new Vector3(25f, 0f, 280f), withPrevious = true,
                    spec = new GruntSpec { attacks = new[] { GruntAttack.Swing, GruntAttack.Slam } },
                },
            },
        };
    }

    public partial class SkirmishSettings
    {
        /// <summary>
        /// 보스전(기획안 2026-11-26): 보스가 22 앞에 서서 대검으로 왼쪽 베기(내려베기)와 오른쪽 베기를 한다. 방패는 없다. 튕겨난 동안에만 피해가 들어간다.
        /// 체력 800에서 2막(속임 베기, 헬기를 노림, 자폭 떼), 400에서 3막(검과 방패를 잃고 큰 내려베기와 찌르며 돌진이 더해진다), 60에서 끝난다. 마지막 일격은 아직 없다(이름의 Act1은 1막만 있던 때의 것).
        /// 회색 상자 장면용 자리다. 로봇이 북쪽(+Z)을 본다.
        /// </summary>
        public static SkirmishSettings BossAct1() => BossAct1At(new Vector3(-350f, 0f, 450f), 0f);

        /// <summary>
        /// 같은 1막을 맵 위 고로 앞에 놓는다(기획안 2절): 로봇 (100, 0, 632)이 동쪽을 보고, 보스는 (122, 0, 632), P2는 (111, 40, 595)에서 북쪽을 본다.
        /// </summary>
        public static SkirmishSettings BossAct1OnMap() => BossAct1At(new Vector3(100f, 0f, 632f), 90f);

        /// <summary>
        /// 로봇의 자리와 방향을 받아 나머지 자리를 로봇 기준으로 놓는다: 보스는 앞 22, P2는 오른쪽 37·앞 11·높이 40에서 싸움을 옆으로 본다.
        /// </summary>
        /// <summary>그 번호의 적이 보스인가(튕겨난 동안에만 피해가 들어가는 적).</summary>
        public static bool IsBoss(SkirmishSettings s, int index) =>
            s.grunts != null && index >= 0 && index < s.grunts.Length && s.grunts[index].spec.openOnlyWhenStaggered;

        public static SkirmishSettings BossAct1At(Vector3 mech, float yaw)
        {
            var facing = Quaternion.Euler(0f, yaw, 0f);
            return new SkirmishSettings
            {
                mechStart = mech,
                mechYaw = yaw,
                // 걷지 않는다. 스틱을 옆으로 튕기면 8 비켰다가 돌아온다.
                sideStepDistance = 8f,
                mechArea = new Vector4(mech.x, mech.z, mech.x, mech.z),
                gunnerPost = mech + facing * new Vector3(37f, 40f, 11f),
                gunnerYaw = yaw - 90f,
                // 2026-11-27 선우 확정: 높이 40, 기수 15° 숙임. 수평이면 계기판과 기수가 싸움을 가린다.
                gunnerPitch = 15f,
                // 우리 로봇은 왼팔 방패와 오른팔 검으로 시작한다. 3막에 들어갈 때 둘 다 잃는다.
                swordDamage = 40f,
                missiles = 3,
                rangedEnemies = false,
                mechGun = false,
                reboots = 1,
                gruntHeight = 35f,
                gruntRadius = 8f,
                lowHullBelow = 30f,
                droneEvery = 12f,
                droneFrom = mech + facing * new Vector3(-25f, 45f, 170f),
                grunts = new[]
                {
                    new GruntWave
                    {
                        start = mech + facing * new Vector3(0f, 0f, 22f), endAtHp = 60f,
                        acts = new[]
                        {
                            // 같은 특별한 공격이 두 번 이어지지 않게 기본 공격 사이에 끼운다. 헬기를 노림과 자폭 떼는 한 번씩만 나온다.
                            new GruntAct
                            {
                                belowHp = 800f, droneEvery = 10f,
                                attacks = new[]
                                {
                                    GruntAttack.Slam, GruntAttack.Feint, GruntAttack.Swing, GruntAttack.AimHeli, GruntAttack.Slam, GruntAttack.Feint,
                                    GruntAttack.CallSwarm, GruntAttack.Swing, GruntAttack.Feint, GruntAttack.Slam, GruntAttack.Swing, GruntAttack.Feint,
                                },
                            },
                            new GruntAct
                            {
                                belowHp = 400f, droneEvery = 6f, breakWeapons = true,
                                attacks = new[]
                                {
                                    GruntAttack.Slam, GruntAttack.BigSlam, GruntAttack.Swing, GruntAttack.Thrust, GruntAttack.Feint,
                                    GruntAttack.Slam, GruntAttack.BigSlam, GruntAttack.Swing, GruntAttack.Feint, GruntAttack.Thrust,
                                },
                            },
                        },
                        spec = new GruntSpec
                        {
                            hp = 1200f, soloHp = 900f,
                            // 예고 1.2초 = 올림 0.4 + 멈춤 0.5 + 내려침 0.3.
                            holdSeconds = 0.5f, guardEveryDamage = 0f,
                            attacks = new[] { GruntAttack.Slam, GruntAttack.Swing, GruntAttack.Swing, GruntAttack.Slam, GruntAttack.Slam, GruntAttack.Swing, GruntAttack.Slam },
                            openOnlyWhenStaggered = true, firstTimeScale = 1.5f, strongShotTarget = true,
                        },
                    },
                },
            };
        }
    }

    /// <summary>보스전의 둘째 막부터, 막 하나의 설정.</summary>
    [Serializable]
    public class GruntAct
    {
        [Min(1f), Tooltip("체력이 이 값까지 내려가면 이 막이 시작된다. 한 방에 넘겨도 체력은 여기서 멈춘다. 혼자 할 때는 체력 비율만큼 낮아진다.")]
        public float belowHp = 1f;
        [Min(0f), Tooltip("이 막에서 자폭 드론이 오는 간격(초). 0이면 droneEvery 그대로.")]
        public float droneEvery;
        [Tooltip("이 막의 공격 순서. 비어 있으면 앞의 것을 그대로 쓴다.")]
        public GruntAttack[] attacks = new GruntAttack[0];
        [Tooltip("이 막에 들어갈 때 물러서는 대신 한 번 크게 내려쳐 로봇의 검과 방패를 부순다(피해 없음).")]
        public bool breakWeapons;
    }

    [Serializable]
    public class GruntWave
    {
        [Tooltip("둘째 막부터 차례로. 비어 있으면 막이 하나다.")]
        public GruntAct[] acts = new GruntAct[0];
        [Min(0f), Tooltip("체력이 이 값까지 내려가면 이 적과의 싸움을 끝낸다(보스전의 한 막). 0이면 쓰러질 때까지. 혼자 할 때는 체력 비율만큼 낮아진다.")]
        public float endAtHp;
        public Vector3 start;
        public GruntSpec spec = new GruntSpec();
        [Tooltip("앞 번호의 돌격형과 동시에 출발해 35 거리에서 기다린다. 혼자 할 때는 앞의 적이 쓰러진 뒤에 출발한다.")]
        public bool withPrevious;
        [Tooltip("로봇이 이 남북 좌표를 지나면 큰 탄을 한 번 겨눈다. 0이면 안 쏜다.")]
        public float shellWhenRobotPassesZ;
    }
}
