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
    public class SkirmishSettings
    {
        [Header("메카 (P1)")]
        public Vector3 mechStart = new Vector3(0f, 0f, 205f);
        [Tooltip("메카가 움직일 수 있는 범위(x 최소, z 최소, x 최대, z 최대).")]
        public Vector4 mechArea = new Vector4(-25f, 180f, 25f, 215f);
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
        public float gruntHeight = 30f;
        [Tooltip("포 조준 판정 반지름(m).")]
        public float gruntRadius = 6.5f;

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
            grunts = new[]
            {
                // 돌격형 1: 내려치기만, 가드를 굳히지 않음, 첫 적이라 예고가 길다.
                new GruntWave { start = new Vector3(0f, 0f, 130f), spec = new GruntSpec { holdSeconds = 1.2f, guardEveryDamage = 0f } },
                // 돌격형 2: 한 대 맞으면 가드를 굳힌다. P2가 열어 줘야 한다.
                new GruntWave { start = new Vector3(15f, 0f, 135f), spec = new GruntSpec { firstGuardDamage = 25f, guardEveryDamage = 60f } },
            },
        };
    }

    [Serializable]
    public class GruntWave
    {
        public Vector3 start;
        public GruntSpec spec = new GruntSpec();
    }
}
