using System;
using System.Collections.Generic;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>Z2 교전의 진행 상태.</summary>
    public enum SkirmishPhase : byte
    {
        /// <summary>두 사람이 준비하기 전.</summary>
        Waiting,
        Fighting,
        /// <summary>일반병 둘을 잡고 날아가던 탄도 다 정리됨.</summary>
        Cleared,
        /// <summary>고로 내구도 또는 메카 내구도가 0.</summary>
        Failed,
        /// <summary>내구도 0에서 한 판에 한 번 "기체 재기동". 끝나면 그 장면의 처음부터 다시 싸운다.</summary>
        Rebooting,
    }

    public enum EnemyStep : byte
    {
        /// <summary>자기 자리로 걸어가는 중.</summary>
        Advance,
        /// <summary>발사 예고(G3는 큰 탄, G4는 고로 포격 충전).</summary>
        Windup,
        Recover,
        /// <summary>미사일이나 누적 피해로 멈칫함. 예고가 취소된다.</summary>
        Stagger,
        Dead,
    }

    public enum ShellKind : byte
    {
        /// <summary>G3가 메카에게 쏘는 느린 큰 탄. 공중포대가 요격하거나 메카가 가드로 막는다.</summary>
        BigShell,
        /// <summary>G4가 고로에 쏘는 포격탄. 발사 뒤에는 못 막는다.</summary>
        FurnaceShell,
        /// <summary>공중포대의 유도 미사일.</summary>
        Missile,
    }

    public enum SkirmishEventKind : byte
    {
        WindupStarted,
        ShellFired,
        ShellIntercepted,
        ShellBlocked,
        ShellMissed,
        MechHit,
        CannonShot,
        MechGunShot,
        MissileFired,
        MissileHit,
        MissileLost,
        EnemyStaggered,
        EnemyDown,
        FurnaceHit,
        Cleared,
        Failed,
        // 돌격형. Id는 공격 종류(0 = 내려치기, 1 = 휘두르기) 또는 돌격형 번호.
        GruntArrived,
        GruntTelegraph,
        GruntBlocked,
        GruntPerfectBlock,
        PunchHit,
        PunchBounced,
        GuardClosed,
        GuardBroken,
        GruntDown,
        /// <summary>차폐판이 부서짐. 이후로는 맨팔로 막는다(판정은 같다).</summary>
        ShieldBroken,
        /// <summary>돌격형이 큰 탄을 겨누기 시작함. Id는 돌격형 번호.</summary>
        GruntShellWindup,
        /// <summary>내구도 0. 재기동이 시작됨.</summary>
        Rebooting,
        /// <summary>재기동이 끝나 그 장면의 처음으로 돌아감.</summary>
        SceneRestarted,
        /// <summary>P2의 스캔이 끝나 보스의 약점이 표시됨. To가 약점 자리.</summary>
        WeakPointShown,
    }

    /// <summary>한 틱에 일어난 일. 화면·소리·진동이 이것을 보고 반응한다.</summary>
    public struct SkirmishEvent
    {
        public SkirmishEventKind Kind;
        /// <summary>적 번호(0 = G3, 1 = G4) 또는 탄 번호.</summary>
        public int Id;
        public Vector3 From, To;
    }

    [Serializable]
    public class SkirmishEnemy
    {
        public int id;
        public Vector3 position;
        public float hp;
        public EnemyStep step;
        /// <summary>지금 단계가 끝날 때까지 남은 초.</summary>
        public float timer;
        /// <summary>이번 예고 동안 받은 피해. 문턱을 넘으면 예고가 끊긴다.</summary>
        public float windupDamage;
        /// <summary>0보다 크면 다시 멈칫하지 않는다(계속 멈춰 세우는 것 방지).</summary>
        public float staggerImmunity;
        public bool atPost;
        public bool Alive => step != EnemyStep.Dead;
    }

    [Serializable]
    public class SkirmishShell
    {
        public int id;
        public ShellKind kind;
        public Vector3 position, velocity;
        /// <summary>미사일이 쫓는 적 번호.</summary>
        public int target;
        /// <summary>요격까지 남은 명중 수(큰 탄만).</summary>
        public int hitsLeft;
        public float life;
    }

    [Serializable]
    public class SkirmishState
    {
        /// <summary>재시작마다 1씩 오른다. 이전 판의 입력·탄을 구별하는 데 쓴다.</summary>
        public int epoch = 1;
        public SkirmishPhase phase;
        public bool mechReady, gunnerReady;
        public float elapsed;

        public Vector3 mech;
        public float hull;
        public bool guarding;
        public Vector3 guardDirection;
        /// <summary>막는 팔이 덮고 있는 쪽(돌격형의 공격을 막을 때 본다).</summary>
        public PilotSide guardSide;
        /// <summary>차폐판이 펴져 있다(P2 화면의 로봇도 같이 펴고 접는다). 부서지면 false.</summary>
        public bool shieldOpen = true;
        /// <summary>차폐판이 부서졌다(장면 6). 막기 판정은 그대로다.</summary>
        public bool shieldBroken;
        /// <summary>딱 맞춰 막은 뒤 느린 시간이 끝날 때까지 남은 실제 초. 0이면 평소 속도.</summary>
        public float slowMotion;

        /// <summary>지금 나와 있는 돌격형 번호. 아직 없으면 -1.</summary>
        public int gruntIndex = -1;
        public Vector3 gruntPosition;
        public float gruntHp, gruntTimer;
        public GruntStep gruntStep;
        public GruntAttack gruntAttack;
        public int gruntsDown;
        public bool GruntAlive => gruntIndex >= 0 && gruntStep != GruntStep.Dead;
        /// <summary>맞붙은 돌격형이 큰 탄을 겨누는 중이면 발사까지 남은 초(서 있는 적).</summary>
        public float gruntShell;

        /// <summary>뒤에서 기다리는 돌격형 번호. 없으면 -1.</summary>
        public int backIndex = -1;
        public Vector3 backPosition;
        public float backHp;
        public GruntStep backStep;
        /// <summary>기다리는 돌격형이 큰 탄을 겨누는 중이면 발사까지 남은 초.</summary>
        public float backShell;

        /// <summary>남은 재기동 횟수(한 판에 한 번).</summary>
        public int rebootsLeft;
        /// <summary>재기동이 끝날 때까지 남은 초.</summary>
        public float rebootTimer;

        public SkirmishEnemy[] enemies;
        public List<SkirmishShell> shells = new List<SkirmishShell>();

        public int missiles;
        /// <summary>미사일이 잠그는 중인 적 번호. 없으면 -1.</summary>
        public int lockTarget = -1;
        /// <summary>잠금 진행(0~1). 1이면 놓을 때 발사된다.</summary>
        public float lockProgress;
        /// <summary>기관포 열(0~1). 1에 닿으면 식을 때까지 못 쏜다.</summary>
        public float cannonHeat;
        public bool cannonOverheated;

        /// <summary>고로가 더 버틸 수 있는 피격 수.</summary>
        public int furnace;

        /// <summary>보스의 약점 표시가 꺼질 때까지 남은 초. 0이면 표시가 없다. 스캔 진행은 lockProgress(표적은 GruntTarget)를 쓴다.</summary>
        public float weakTimer;
        /// <summary>표시된 약점의 자리.</summary>
        public Vector3 weakPoint;
    }

    /// <summary>P1 메카 입력. 방향은 월드 기준.</summary>
    public struct MechInput
    {
        /// <summary>이동(x 동서, z 남북, 길이 0~1).</summary>
        public Vector3 move;
        public bool guard;
        /// <summary>가드하는 팔이 향한 수평 방향.</summary>
        public Vector3 guardDirection;
        public bool fire;
        /// <summary>오른팔 포가 향한 방향.</summary>
        public Vector3 aim;
        /// <summary>막는 팔이 덮고 있는 쪽. 왼손이 얼굴 가운데보다 왼쪽이면 Left.</summary>
        public PilotSide guardSide;
        /// <summary>주먹을 낼 때마다 1씩 올린다. 값이 바뀐 틱에 한 번만 판정한다.</summary>
        public int punchSeq;
        /// <summary>그 주먹의 피해(잽 25, 훅 35).</summary>
        public float punchDamage;
        /// <summary>조종사가 차폐판을 접어 두었다(버튼으로 펴고 접는 방식일 때만 true가 된다).</summary>
        public bool shieldFolded;
    }

    /// <summary>P2 공중포대 입력. 방향은 월드 기준.</summary>
    public struct GunnerInput
    {
        /// <summary>오른손 기관포 방향.</summary>
        public Vector3 aim;
        public bool fire;
        /// <summary>왼손 미사일 지시기 방향.</summary>
        public Vector3 pointer;
        /// <summary>누르고 있는 동안 잠그고, 놓으면 발사.</summary>
        public bool lockHeld;
        /// <summary>잠금 취소. 미사일을 쓰지 않는다.</summary>
        public bool cancel;
    }
}
