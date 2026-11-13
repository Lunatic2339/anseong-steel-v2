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
    /// 맵 연출(M4-07). 서버: 전투 사건을 보고 맵 장치를 작동(보스 경직·피해·밀려남 + 사령관 대사 + <see cref="CombatCue.MapEvent"/>).
    /// 모든 PC: 사건을 듣고 실제 구조물을 움직인다.
    /// - 크레인 코일: <see cref="CraneRig"/>가 팔뚝 포 구간 동안 보스 위로 따라오고, 고리를 쏘면 코일이 떨어진다. 피해·휘청·대사는
    ///   코일이 보스에 닿는 순간(<see cref="CombatCue.MapImpact"/>)에 들어간다.
    /// - 컨베이어 다리·사일로: 보스에 가장 가까운 칸/통이 찌그러지고 기울며 조각이 떨어진다(<see cref="BreakableProp"/>).
    /// 판을 다시 시작하면(대기·점검 장면) 모두 원래대로. 소품은 메뉴 'Build Stage Props'가 만들어 칸에 넣는다(비면 사건·대사만).
    /// 메뉴 'Add Match Flow'가 Combat Server에 붙인다.
    /// </summary>
    public class MapEvents : MonoBehaviour
    {
        [Header("맵 소품 (메뉴 'Build Stage Props'가 만들어 넣는다)")]
        [SerializeField, Tooltip("메뉴가 만든 소품 묶음(다시 만들 때 지우고 새로 만든다).")] GameObject propsRoot;
        [SerializeField, Tooltip("크레인(들보·대차·코일).")] CraneRig crane;
        [SerializeField, Tooltip("컨베이어 다리 칸들(싸움터 옆으로 늘어선 순서).")] BreakableProp[] conveyorBays = new BreakableProp[0];
        [SerializeField, Tooltip("사일로들(보스가 처음 서는 자리 뒤).")] BreakableProp[] silos = new BreakableProp[0];

        [Header("크레인 코일")]
        [SerializeField, Tooltip("크레인이 없을 때: 보스 위 이 높이(m)에 고리가 있다고 본다.")]
        float hookHeight = 100f;
        [SerializeField, Range(2f, 30f), Tooltip("포 조준이 고리에서 이 각도(°) 안이면 맞힌 것.")]
        float hookDegrees = 12f;
        [SerializeField, Min(0f), Tooltip("조준 각도를 재는 높이(m, 조종사 로봇 발에서 어깨쯤).")]
        float aimOriginHeight = 45f;
        [SerializeField, Min(1f), Tooltip("코일이 맞는 보스 머리 높이(m).")]
        float bossHeadHeight = 62f;
        [SerializeField, Min(0f), Tooltip("코일이 보스에 준 피해(보스 무기 내구도 %, 참고표 6).")]
        float coilDamage = 6f;
        [SerializeField, Min(0.5f)] float coilStunSeconds = 3f;

        [Header("컨베이어·사일로")]
        [SerializeField, Min(0.5f), Tooltip("컨베이어 다리에 박혀 휘청(초, 대본 4).")]
        float stuckSeconds = 4f;
        [SerializeField, Min(0f), Tooltip("돌진을 피한 뒤 컨베이어가 무너지기까지(초). 보스가 지나가 처박히는 시간.")]
        float conveyorDelay = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("처박힌 칸의 양옆 칸이 같이 흔들리는 세기(0 = 그 칸만).")]
        float neighbourStrength = 0.45f;
        [SerializeField, Min(0f), Tooltip("사일로 처박기: 첫 부스트 밀려남에 더하는 거리(m).")]
        float siloExtraMeters = 20f;
        [SerializeField, Min(0f), Tooltip("부스트 뒤 사일로가 찌그러지기까지(초). 보스가 밀려가 닿는 시간.")]
        float siloDelay = 0.45f;

        CombatServer combat;
        MatchFlow flow;
        BossBrain boss;
        Transform player;
        CombatDirector cueSource;
        bool bound, coilDone, siloDone, conveyorDone, subscribed, coilFalling;
        double coilImpactAt;
        float conveyorAt = -1f, siloAt = -1f;
        Vector3 conveyorFrom, siloFrom;
        Vector3[] conveyorPoints, siloPoints;

        void Start()
        {
            combat = GetComponent<CombatServer>();
            flow = GetComponent<MatchFlow>();
            boss = FindAnyObjectByType<BossBrain>();
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) player = seat.transform.root;
            conveyorPoints = Points(conveyorBays);
            siloPoints = Points(silos);
        }

        void OnDestroy()
        {
            if (subscribed && cueSource != null) cueSource.Cue -= OnClientCue;
            if (bound && combat != null) combat.ServerCue -= OnServerCue;
        }

        void Update()
        {
            if (combat == null) return;
            if (!subscribed && combat.Director != null)
            {
                cueSource = combat.Director;
                cueSource.Cue += OnClientCue;
                subscribed = true;
            }
            if (combat.IsServer && !bound && combat.Authority != null)
            {
                combat.ServerCue += OnServerCue;
                bound = true;
            }
            if (combat.IsServer && coilFalling && combat.Authority != null && combat.Now >= coilImpactAt) CoilLands(coilImpactAt);

            // 모든 PC: 크레인은 팔뚝 포 구간 동안 보스 머리 위로 따라온다. 사격 무장(드론이 뜬 때)이면 고리 경고등이 깜빡인다.
            if (crane != null)
            {
                var scene = flow != null && flow.Running ? flow.Scene : MatchScene.Waiting;
                var follow = scene == MatchScene.FightCannon && boss != null;
                crane.Tick(combat.Now, follow ? boss.transform.position : (Vector3?)null,
                    follow && flow.Loadout == Loadout.Ranged && !crane.Dropped);
            }
            if (conveyorAt >= 0f && Time.time >= conveyorAt)
            {
                conveyorAt = -1f;
                BreakNearest(conveyorBays, conveyorPoints, conveyorFrom, conveyorFrom, neighbourStrength);
            }
            if (siloAt >= 0f && Time.time >= siloAt)
            {
                siloAt = -1f;
                // 보스가 밀려가는 쪽(조종사 반대쪽)에 있는 사일로.
                var away = player != null ? siloFrom - player.position : Vector3.forward;
                away.y = 0f;
                var toward = siloFrom + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward) * 60f;
                BreakNearest(silos, siloPoints, toward, siloFrom, 0f);
            }
        }

        /// <summary>고리 위치(서버·모든 PC 같은 계산).</summary>
        public Vector3 HookPosition => crane != null ? crane.HookPosition :
            boss != null ? boss.transform.position + Vector3.up * hookHeight : Vector3.zero;

        /// <summary>from에서 가장 가까운 컨베이어 다리 칸의 자리(보스 돌진이 향할 곳을 정할 때 쓸 수 있다). 없으면 from.</summary>
        public Vector3 ConveyorPoint(Vector3 from) => NearestPoint(conveyorBays, from);

        /// <summary>from에서 가장 가까운 사일로 자리. 없으면 from.</summary>
        public Vector3 SiloPoint(Vector3 from) => NearestPoint(silos, from);

        // ── 서버 ──

        void OnServerCue(CombatEvent e)
        {
            if (flow == null || combat.Authority == null) return;
            var scene = flow.Scene;
            switch (e.Cue)
            {
                case CombatCue.MatchScene:
                    if ((MatchScene)e.Code == MatchScene.Waiting || (MatchScene)e.Code == MatchScene.Checkup)
                        coilDone = siloDone = conveyorDone = coilFalling = false;
                    break;
                case CombatCue.CannonFire:
                    if (scene == MatchScene.FightCannon && !coilDone && player != null && boss != null)
                    {
                        var origin = player.position + Vector3.up * aimOriginHeight;
                        if (DroneRules.AngleTo(origin, e.Point - origin, HookPosition) > hookDegrees) break;
                        coilDone = true;
                        // 줄이 끊겨 떨어지기 시작. 피해·휘청은 보스에 닿는 순간(CoilLands).
                        var head = boss.transform.position + Vector3.up * bossHeadHeight;
                        var fall = crane != null ? crane.FallSeconds(head.y) : 1.2f;
                        coilFalling = true;
                        coilImpactAt = e.Time + fall;
                        Fire(MapEventKind.CraneCoil, e.Time, head, fall);
                    }
                    break;
                case CombatCue.Dodge:
                    if (scene == MatchScene.FightArmor && !conveyorDone && e.Code == (int)BossAttackKind.Charge && boss != null)
                    {
                        conveyorDone = true;
                        boss.Stun(stuckSeconds, 2f);
                        Fire(MapEventKind.ConveyorStuck, e.Time, boss.transform.position, conveyorDelay);
                        Line(CommanderLines.Stuck, e.Time);
                    }
                    break;
                case CombatCue.BossKnockback:
                    if (scene == MatchScene.FightCore && !siloDone && boss != null)
                    {
                        siloDone = true;
                        boss.PushFurther(siloExtraMeters);
                        Fire(MapEventKind.SiloSlam, e.Time, boss.transform.position, siloDelay);
                        Line(CommanderLines.PushOn, e.Time);
                    }
                    break;
            }
        }

        /// <summary>서버: 코일이 보스 머리에 닿은 순간. 피해 + 휘청 + 대사.</summary>
        void CoilLands(double now)
        {
            coilFalling = false;
            if (boss == null) return;
            combat.Authority.Resolver.Boss.Apply(coilDamage);
            boss.Stun(coilStunSeconds, 2f);
            combat.Events.Add(new CombatEvent
            {
                Cue = CombatCue.MapImpact, Kind = PunchKind.None, Code = (int)MapEventKind.CraneCoil, Amount = 1f, Damage = coilDamage, Time = now,
                BossHealth = combat.Authority.Resolver.Boss.Current, Point = boss.transform.position + Vector3.up * bossHeadHeight,
            });
            Line(CommanderLines.Stuck, now);
            combat.Flush();
        }

        void Fire(MapEventKind kind, double now, Vector3 point, float amount)
        {
            combat.Events.Add(new CombatEvent
            {
                Cue = CombatCue.MapEvent, Kind = PunchKind.None, Code = (int)kind, Amount = amount, Time = now,
                BossHealth = combat.Authority.Resolver.Boss.Current, Point = point,
            });
        }

        void Line(int line, double now)
        {
            combat.Events.Add(new CombatEvent
            {
                Cue = CombatCue.CommanderLine, Kind = PunchKind.None, Code = line, Time = now, BossHealth = combat.Authority.Resolver.Boss.Current,
            });
        }

        // ── 모든 PC: 소품 움직이기 ──

        void OnClientCue(CombatEvent e)
        {
            switch (e.Cue)
            {
                case CombatCue.MatchScene:
                    // 판 재시작: 구조물을 원래대로.
                    if ((MatchScene)e.Code == MatchScene.Waiting || (MatchScene)e.Code == MatchScene.Checkup) ResetProps();
                    break;
                case CombatCue.MapEvent:
                    switch ((MapEventKind)e.Code)
                    {
                        case MapEventKind.CraneCoil:
                            if (crane != null) crane.Drop(e.Time, e.Point, RollDirection(e.Point));
                            break;
                        case MapEventKind.ConveyorStuck:
                            conveyorFrom = e.Point;
                            conveyorAt = Time.time + e.Amount;
                            break;
                        case MapEventKind.SiloSlam:
                            siloFrom = e.Point;
                            siloAt = Time.time + e.Amount;
                            break;
                    }
                    break;
            }
        }

        /// <summary>코일이 튕겨 굴러갈 쪽: 조종사–보스 선의 옆, 마당 가운데 쪽(구조물이 없는 빈 곳).</summary>
        Vector3 RollDirection(Vector3 bossPoint)
        {
            var forward = player != null ? bossPoint - player.position : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            var side = Vector3.Cross(Vector3.up, forward.normalized);
            var origin = crane != null ? crane.transform.position : Vector3.zero;
            var toCentre = origin - bossPoint;
            toCentre.y = 0f;
            return Vector3.Dot(side, toCentre) < 0f ? -side : side;
        }

        void ResetProps()
        {
            conveyorAt = siloAt = -1f;
            if (crane != null) crane.ResetRig();
            if (conveyorBays != null)
                foreach (var bay in conveyorBays)
                    if (bay != null) bay.ResetProp();
            if (silos != null)
                foreach (var silo in silos)
                    if (silo != null) silo.ResetProp();
        }

        static Vector3[] Points(BreakableProp[] props)
        {
            var n = props != null ? props.Length : 0;
            var points = new Vector3[n];
            // 빈 칸은 고르지 않게 아주 먼 자리로.
            for (var i = 0; i < n; i++) points[i] = props[i] != null ? props[i].BasePoint : new Vector3(1e6f, 0f, 1e6f);
            return points;
        }

        Vector3 NearestPoint(BreakableProp[] props, Vector3 from)
        {
            var i = PropMotion.Nearest(Points(props), from);
            return i >= 0 && props[i] != null ? props[i].BasePoint : from;
        }

        static void BreakNearest(BreakableProp[] props, Vector3[] points, Vector3 near, Vector3 from, float neighbours)
        {
            var i = PropMotion.Nearest(points, near);
            if (i < 0 || props[i] == null) return;
            props[i].Break(from);
            if (neighbours <= 0f) return;
            if (i > 0 && props[i - 1] != null) props[i - 1].Break(from, neighbours);
            if (i + 1 < props.Length && props[i + 1] != null) props[i + 1].Break(from, neighbours);
        }
    }
}
