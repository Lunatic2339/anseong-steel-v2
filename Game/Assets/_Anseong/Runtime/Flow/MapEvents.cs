using Anseong.Boss;
using Anseong.Combat;
using Anseong.Net;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>맵 장치(10_game_scenario.md, 한 판에 한 번씩). 숫자가 이벤트 Code로 가므로 끝에만 더한다.</summary>
    public enum MapEventKind
    {
        /// <summary>장면 4-2: 크레인 고리를 쏘면 코일이 떨어져 보스 휘청.</summary>
        CraneCoil,
        /// <summary>장면 6: 돌진을 피하면 보스가 컨베이어 다리에 박혀 4초 휘청.</summary>
        ConveyorStuck,
        /// <summary>장면 8: 맨손 첫 부스트로 보스를 사일로에 처박음.</summary>
        SiloSlam,
    }

    /// <summary>
    /// 맵 연출(M4-07 프로토타입). 서버: 전투 사건을 보고 맵 장치를 작동(보스 경직·피해·밀려남 + 사령관 대사 + <see cref="CombatCue.MapEvent"/>).
    /// 모든 PC: MapEvent를 듣고 맵 소품을 움직인다(위치 이동·흔들림만, 확정 사항 10). 윤주 맵 자리(크레인 코일·컨베이어 다리·사일로)를
    /// Inspector 칸에 넣으면 그것을 움직이고, 비면 도형을 만든다. 메뉴 'Add Match Flow'가 Combat Server에 붙인다.
    /// </summary>
    public class MapEvents : MonoBehaviour
    {
        [Header("윤주 맵 자리 (비우면 도형·흔들림만)")]
        [SerializeField, Tooltip("크레인 코일(떨어질 물체).")] Transform craneCoil;
        [SerializeField, Tooltip("컨베이어 다리(흔들림).")] Transform conveyor;
        [SerializeField, Tooltip("사일로(흔들림).")] Transform silo;

        [Header("크레인 코일")]
        [SerializeField, Tooltip("코일 자리가 비었을 때: 보스 위 이 높이(m)에 고리가 있다고 본다.")]
        float hookHeight = 80f;
        [SerializeField, Range(2f, 30f), Tooltip("포 조준이 고리에서 이 각도(°) 안이면 맞힌 것.")]
        float hookDegrees = 10f;
        [SerializeField, Min(0f), Tooltip("코일이 보스에 준 피해(보스 무기 내구도 %, 참고표 6).")]
        float coilDamage = 6f;
        [SerializeField, Min(0.5f)] float coilStunSeconds = 3f;

        [Header("컨베이어·사일로")]
        [SerializeField, Min(0.5f), Tooltip("컨베이어 다리에 박혀 휘청(초, 대본 4).")]
        float stuckSeconds = 4f;
        [SerializeField, Min(0f), Tooltip("사일로 처박기: 첫 부스트 밀려남에 더하는 거리(m).")]
        float siloExtraMeters = 20f;
        [SerializeField, Min(0f)] float shakeSeconds = 1.5f;
        [SerializeField] Material shapeMaterial;

        CombatServer combat;
        MatchFlow flow;
        BossBrain boss;
        Transform player;
        bool bound, coilDone, siloDone, conveyorDone, subscribed;
        Transform shaking;
        float shakeUntil;
        Vector3 shakeHome;

        void Start()
        {
            combat = GetComponent<CombatServer>();
            flow = GetComponent<MatchFlow>();
            boss = FindAnyObjectByType<BossBrain>();
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) player = seat.transform.root;
            if (combat != null && combat.Director != null)
            {
                combat.Director.Cue += OnClientCue;
                subscribed = true;
            }
        }

        void OnDestroy()
        {
            if (subscribed && combat != null && combat.Director != null) combat.Director.Cue -= OnClientCue;
            if (bound && combat != null) combat.ServerCue -= OnServerCue;
        }

        void Update()
        {
            if (combat != null && combat.IsServer && !bound && combat.Authority != null)
            {
                combat.ServerCue += OnServerCue;
                bound = true;
            }
            if (shaking != null)
            {
                if (Time.time < shakeUntil) shaking.position = shakeHome + Random.insideUnitSphere * 0.6f;
                else
                {
                    shaking.position = shakeHome;
                    shaking = null;
                }
            }
        }

        /// <summary>고리 위치(서버·모든 PC 같은 계산).</summary>
        public Vector3 HookPosition => craneCoil != null ? craneCoil.position + Vector3.up * 5f :
            boss != null ? boss.transform.position + Vector3.up * hookHeight : Vector3.zero;

        // ── 서버 ──

        void OnServerCue(CombatEvent e)
        {
            if (flow == null || combat.Authority == null) return;
            var scene = flow.Scene;
            switch (e.Cue)
            {
                case CombatCue.MatchScene:
                    if ((MatchScene)e.Code == MatchScene.Waiting || (MatchScene)e.Code == MatchScene.Checkup) coilDone = siloDone = conveyorDone = false;
                    break;
                case CombatCue.CannonFire:
                    if (scene == MatchScene.FightCannon && !coilDone && player != null && boss != null &&
                        DroneRules.AngleTo(player.position, e.Point - player.position, HookPosition) <= hookDegrees)
                    {
                        coilDone = true;
                        combat.Authority.Resolver.Boss.Apply(coilDamage);
                        boss.Stun(coilStunSeconds, 2f);
                        Fire(MapEventKind.CraneCoil, e.Time, CommanderLines.Stuck);
                    }
                    break;
                case CombatCue.Dodge:
                    if (scene == MatchScene.FightArmor && !conveyorDone && e.Code == (int)BossAttackKind.Charge && boss != null)
                    {
                        conveyorDone = true;
                        boss.Stun(stuckSeconds, 2f);
                        Fire(MapEventKind.ConveyorStuck, e.Time, CommanderLines.Stuck);
                    }
                    break;
                case CombatCue.BossKnockback:
                    if (scene == MatchScene.FightCore && !siloDone && boss != null)
                    {
                        siloDone = true;
                        boss.PushFurther(siloExtraMeters);
                        Fire(MapEventKind.SiloSlam, e.Time, CommanderLines.PushOn);
                    }
                    break;
            }
        }

        void Fire(MapEventKind kind, double now, int line)
        {
            var health = combat.Authority.Resolver.Boss.Current;
            combat.Events.Add(new CombatEvent { Cue = CombatCue.MapEvent, Kind = PunchKind.None, Code = (int)kind, Time = now, BossHealth = health, Point = boss.transform.position });
            combat.Events.Add(new CombatEvent { Cue = CombatCue.CommanderLine, Kind = PunchKind.None, Code = line, Time = now, BossHealth = health });
        }

        // ── 모든 PC: 소품 움직이기 ──

        void OnClientCue(CombatEvent e)
        {
            if (e.Cue != CombatCue.MapEvent) return;
            switch ((MapEventKind)e.Code)
            {
                case MapEventKind.CraneCoil:
                    DropCoil(e.Point);
                    break;
                case MapEventKind.ConveyorStuck:
                    Shake(conveyor);
                    break;
                case MapEventKind.SiloSlam:
                    Shake(silo);
                    break;
            }
        }

        void DropCoil(Vector3 bossPosition)
        {
            Transform coil = craneCoil;
            if (coil == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
                var r = go.GetComponent<Renderer>();
                if (shapeMaterial != null && r != null) r.sharedMaterial = shapeMaterial;
                go.name = "Crane Coil (임시)";
                go.transform.localScale = new Vector3(14f, 6f, 14f);
                go.transform.position = bossPosition + Vector3.up * hookHeight;
                coil = go.transform;
                Destroy(go, 8f);
            }
            var body = coil.gameObject.AddComponent<Rigidbody>();
            body.mass = 5000f;
        }

        void Shake(Transform target)
        {
            if (target == null) return;
            if (shaking != null) shaking.position = shakeHome;
            shaking = target;
            shakeHome = target.position;
            shakeUntil = Time.time + shakeSeconds;
        }
    }
}
