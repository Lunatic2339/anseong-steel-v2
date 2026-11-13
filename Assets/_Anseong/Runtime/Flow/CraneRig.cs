using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>
    /// 싸움터 위를 가로지르는 크레인(장면 4-2): 들보(레일을 따라 앞뒤로 주행) + 대차(들보를 따라 좌우) + 매달린 강철 코일.
    /// 팔뚝 포 구간에는 보스 머리 위로 천천히 따라가 "보스가 크레인 아래"가 되게 하고, 고리를 쏘면 코일이 떨어져 보스에 맞고 튕겨 굴러간다.
    /// 겉모습만(충돌 없음). 코일 길은 서버 시각의 함수(<see cref="CoilDrop"/>)라 모든 PC가 같은 순간에 맞는다.
    /// <see cref="MapEvents"/>가 움직인다. 도형은 메뉴 'Build Stage Props'가 조립한다.
    /// </summary>
    public class CraneRig : MonoBehaviour
    {
        [Header("부품 (메뉴가 채운다)")]
        [SerializeField, Tooltip("들보 + 다리(레일을 따라 이 오브젝트의 Z로 움직인다).")] Transform bridge;
        [SerializeField, Tooltip("대차(들보의 자식, X로 움직인다).")] Transform trolley;
        [SerializeField, Tooltip("코일이 매달리는 자리(대차의 자식, 코일 가운데).")] Transform hangPoint;
        [SerializeField, Tooltip("고리(대차의 자식). 쏘는 표적.")] Transform hook;
        [SerializeField, Tooltip("강철 코일(이 오브젝트의 자식, 원통 축 = 로컬 X).")] Transform coil;
        [SerializeField, Tooltip("코일을 건 줄. 떨어지면 숨긴다.")] GameObject slings;
        [SerializeField, Tooltip("고리 경고등. 쏠 수 있을 때 깜빡인다.")] GameObject beacon;
        [SerializeField, Tooltip("보스에 맞을 때 먼지·불꽃.")] ParticleSystem impactFx;
        [SerializeField, Tooltip("땅에 떨어질 때 먼지.")] ParticleSystem landFx;

        [Header("주행")]
        [SerializeField, Tooltip("들보가 쉬는 자리(이 오브젝트 기준 Z, m).")] float homeZ = 250f;
        [SerializeField, Tooltip("대차가 쉬는 자리(X, m).")] float homeX = 70f;
        [SerializeField, Tooltip("들보 주행 범위(Z 최소, 최대).")] Vector2 bridgeRange = new Vector2(-20f, 262f);
        [SerializeField, Tooltip("대차 주행 범위(X 최소, 최대).")] Vector2 trolleyRange = new Vector2(-130f, 130f);
        [SerializeField, Min(0f), Tooltip("들보 주행 빠르기(m/s). 묵직하게.")] float bridgeSpeed = 11f;
        [SerializeField, Min(0f), Tooltip("대차 빠르기(m/s).")] float trolleySpeed = 8f;

        [Header("코일 낙하")]
        [SerializeField, Min(1f), Tooltip("코일 반지름(m).")] float coilRadius = 7f;
        [SerializeField, Min(1f), Tooltip("떨어지는 중력(m/s²).")] float gravity = 16f;
        [SerializeField, Min(0f), Tooltip("보스에 맞고 옆으로 튕기는 빠르기(m/s).")] float bounceSpeed = 14f;
        [SerializeField, Min(0f), Tooltip("튕겨 오르는 빠르기(m/s).")] float bounceUp = 9f;
        [SerializeField, Min(0f), Tooltip("땅에 닿은 뒤 굴러가는 거리(m).")] float rollMeters = 34f;
        [SerializeField, Min(0.1f), Tooltip("굴러가다 멈추기까지(초).")] float rollSeconds = 4f;
        [SerializeField, Min(0.1f), Tooltip("경고등 깜빡임 한 번(초).")] float blinkSeconds = 0.35f;

        CoilDrop drop;
        double dropStart = -1d;
        bool impacted, landed;
        Quaternion hangRotation, rollBasis;

        /// <summary>쏘는 표적(고리).</summary>
        public Vector3 HookPosition => hook != null ? hook.position : transform.position;
        public bool Dropped => dropStart >= 0d;

        /// <summary>지금 매달린 자리에서 height 높이(월드 y)의 보스 머리에 닿기까지(초).</summary>
        public float FallSeconds(float impactY)
        {
            var from = hangPoint != null ? hangPoint.position.y : transform.position.y;
            return PropMotion.FallSeconds(from - (impactY + coilRadius), gravity);
        }

        /// <summary>처음 자리로(판 재시작). 코일을 다시 건다.</summary>
        public void ResetRig()
        {
            dropStart = -1d;
            impacted = landed = false;
            if (bridge != null) bridge.localPosition = new Vector3(bridge.localPosition.x, bridge.localPosition.y, homeZ);
            if (trolley != null) trolley.localPosition = new Vector3(homeX, trolley.localPosition.y, trolley.localPosition.z);
            if (slings != null) slings.SetActive(true);
            if (beacon != null) beacon.SetActive(false);
            if (impactFx != null) impactFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (landFx != null) landFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            HangCoil();
        }

        /// <summary>코일을 떨어뜨린다. startTime = 서버 시각, target = 보스 머리 꼭대기(월드), rollDirection = 튕겨 굴러갈 쪽.</summary>
        public void Drop(double startTime, Vector3 target, Vector3 rollDirection)
        {
            if (Dropped || coil == null || hangPoint == null) return;
            rollDirection.y = 0f;
            if (rollDirection.sqrMagnitude < 1e-4f) rollDirection = transform.right;
            rollDirection.Normalize();
            var from = hangPoint.position;
            var impact = target + Vector3.up * coilRadius;
            if (impact.y > from.y - 1f) impact.y = from.y - 1f;
            drop = new CoilDrop
            {
                From = from, Impact = impact, RollDirection = rollDirection, Gravity = gravity, BounceSpeed = bounceSpeed, BounceUp = bounceUp,
                GroundY = transform.position.y + coilRadius, RollMeters = rollMeters, RollSeconds = rollSeconds,
            };
            hangRotation = coil.rotation;
            rollBasis = Quaternion.LookRotation(rollDirection, Vector3.up);
            dropStart = startTime;
            impacted = landed = false;
            if (slings != null) slings.SetActive(false);
            if (beacon != null) beacon.SetActive(false);
        }

        /// <summary>
        /// 프레임마다(모든 PC). now = 서버 시각. track = 따라갈 자리(보스), 없으면 쉬는 자리로. armed = 쏠 수 있는 때(경고등).
        /// </summary>
        public void Tick(double now, Vector3? track, bool armed)
        {
            if (!Dropped)
            {
                var targetZ = homeZ;
                var targetX = homeX;
                if (track.HasValue)
                {
                    var local = transform.InverseTransformPoint(track.Value);
                    targetZ = local.z;
                    targetX = local.x;
                }
                if (bridge != null)
                {
                    var p = bridge.localPosition;
                    p.z = PropMotion.Travel(p.z, targetZ, bridgeRange.x, bridgeRange.y, bridgeSpeed, Time.deltaTime);
                    bridge.localPosition = p;
                }
                if (trolley != null)
                {
                    var p = trolley.localPosition;
                    p.x = PropMotion.Travel(p.x, targetX, trolleyRange.x, trolleyRange.y, trolleySpeed, Time.deltaTime);
                    trolley.localPosition = p;
                }
                HangCoil();
                if (beacon != null)
                {
                    var on = armed && Mathf.Repeat(Time.time, blinkSeconds * 2f) < blinkSeconds;
                    if (beacon.activeSelf != on) beacon.SetActive(on);
                }
                return;
            }

            if (coil == null) return;
            var t = (float)(now - dropStart);
            coil.position = drop.Position(t);
            var hit = drop.ImpactTime;
            if (t < hit)
            {
                // 떨어지며 굴러갈 방향으로 천천히 돌아눕는다.
                coil.rotation = Quaternion.Slerp(hangRotation, rollBasis, hit > 0f ? Mathf.Clamp01(t / hit) : 1f);
                return;
            }
            if (!impacted)
            {
                impacted = true;
                Burst(impactFx, drop.Impact + Vector3.down * coilRadius);
            }
            var spin = Mathf.Min(t, drop.LandTime) - hit;
            var degrees = spin * 120f + drop.Rolled(t) / coilRadius * Mathf.Rad2Deg;
            coil.rotation = rollBasis * Quaternion.AngleAxis(degrees, Vector3.right);
            if (!landed && t >= drop.LandTime)
            {
                landed = true;
                Burst(landFx, drop.LandPoint + Vector3.down * coilRadius);
            }
        }

        void HangCoil()
        {
            if (coil == null || hangPoint == null) return;
            coil.SetPositionAndRotation(hangPoint.position, hangPoint.rotation);
        }

        static void Burst(ParticleSystem fx, Vector3 at)
        {
            if (fx == null) return;
            fx.transform.position = at;
            fx.Play(true);
        }
    }
}
