using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>
    /// 정비고(장면 1): 조종석 앞을 가린 격납고 벽·출격 문 두 짝 + 공을 던지는 정비 로봇. 겉모습만(충돌 없음, 모든 PC가 서버 시각으로 같이 그린다).
    /// - 문: 둘이 같이 스틱을 밀면 무겁게 좌우로 열려 마당과 멀리 선 보스가 드러난다(<see cref="OpenDoors"/>).
    /// - 벽: 출격하면(<see cref="Leave"/>) 벽 전체가 땅속으로 내려가 싸움터를 비운다. 판을 다시 시작하면 <see cref="SetClosed"/>로 다시 선다.
    /// - 정비 로봇: 공 막기 연습 때 바닥에서 올라와, 던질 쪽으로 돌아 팔을 젖혔다가(예고) 강철 공을 던진다. 공은 닿는 시각에 정확히 방패 자리에 온다.
    /// <see cref="CheckupDirector"/>가 움직인다. 도형은 메뉴 'Build Stage Props'가 조립한다.
    /// </summary>
    public class CheckupBay : MonoBehaviour
    {
        [Header("정비고 (메뉴가 채운다)")]
        [SerializeField, Tooltip("벽·문·바닥 전체(출격 뒤 땅속으로 내려가는 묶음).")] Transform walls;
        [SerializeField, Tooltip("왼쪽 문(로컬 -X로 열린다).")] Transform leftDoor;
        [SerializeField, Tooltip("오른쪽 문(로컬 +X로 열린다).")] Transform rightDoor;
        [SerializeField, Tooltip("문 가운데(문이 열릴 때 사건 자리).")] Transform doorCenter;
        [SerializeField, Tooltip("문이 열릴 때 바닥 먼지.")] ParticleSystem[] doorFx = new ParticleSystem[0];
        [SerializeField, Tooltip("벽이 내려갈 때 바닥 먼지.")] ParticleSystem[] sinkFx = new ParticleSystem[0];
        [SerializeField, Min(1f), Tooltip("문 한 짝이 옆으로 가는 거리(m).")] float doorTravel = 47f;
        [SerializeField, Min(1f), Tooltip("벽이 땅속으로 내려가는 깊이(m).")] float sinkMeters = 100f;
        [SerializeField, Min(0.5f), Tooltip("벽이 내려가는 시간(초). 등장 장면(10초) 안에 끝나게.")] float sinkSeconds = 7f;

        [Header("정비 로봇 (메뉴가 채운다)")]
        [SerializeField, Tooltip("정비 로봇 전체(바닥에서 오르내린다).")] Transform lift;
        [SerializeField, Tooltip("포탑(세로축으로 돈다).")] Transform turret;
        [SerializeField, Tooltip("던지는 팔(로컬 X축으로 젖힌다).")] Transform arm;
        [SerializeField, Tooltip("팔 끝 공 받침(공이 얹히는 자리).")] Transform ballRest;
        [SerializeField, Tooltip("강철 공.")] Transform ball;
        [SerializeField, Tooltip("방패에 막혔을 때 불꽃.")] ParticleSystem blockFx;
        [SerializeField, Tooltip("못 막았을 때 먼지.")] ParticleSystem missFx;
        [SerializeField, Min(1f), Tooltip("정비 로봇이 숨어 있는 깊이(m).")] float liftDepth = 56f;
        [SerializeField, Min(0.3f), Tooltip("정비 로봇이 올라오는 시간(초).")] float liftSeconds = 2.5f;
        [SerializeField, Range(5f, 60f), Tooltip("던질 쪽으로 포탑이 도는 각도(°). 크게 돌아야 읽힌다.")] float aimYawDegrees = 24f;
        [SerializeField, Tooltip("팔 각도(°, 위로 든 쪽이 음수): 쉴 때.")] float armRest = -15f;
        [SerializeField, Tooltip("팔 각도: 젖혔을 때.")] float armWound = -80f;
        [SerializeField, Tooltip("팔 각도: 던진 뒤.")] float armThrown = 5f;
        [SerializeField, Min(0.05f), Tooltip("팔이 앞으로 휘둘러지는 시간(초).")] float throwSeconds = 0.3f;

        [Header("공")]
        [SerializeField, Tooltip("공이 닿는 자리(조종사 로봇 루트 기준 m: x = 옆(왼쪽 공은 부호를 뒤집는다), y = 높이, z = 앞). 방패를 든 자리.")]
        Vector3 targetOffset = new Vector3(6f, 50f, 13f);
        [SerializeField, Min(0f), Tooltip("포물선이 뜨는 높이(m).")] float arcHeight = 7f;
        [SerializeField, Min(0f), Tooltip("막혔을 때 튕겨 나가는 빠르기(m/s).")] float bounceSpeed = 18f;
        [SerializeField, Min(0.1f), Tooltip("튕긴 공에 걸리는 중력(m/s²).")] float gravity = 14f;
        [SerializeField, Min(0.5f), Tooltip("공 반지름(m).")] float ballRadius = 2.5f;
        [SerializeField, Min(0.5f), Tooltip("튕긴 공이 사라지기까지(초).")] float looseSeconds = 5f;

        enum BallState { Hidden, Held, Flying, Loose }

        Transform player;
        Vector3 leftHome, rightHome, wallsHome, liftHome;
        bool cached, gone, throwerWanted;
        double doorStart = -1d, leaveStart = -1d, aimAt = -1d, releaseAt, impactAt;
        float doorSeconds = 2.5f, liftAmount, looseUntil;
        PilotSide aimSide;
        BallState ballState;
        Vector3 flightFrom, flightTo, looseVelocity, looseSpin;
        float turretYaw, armAngle;

        public Vector3 DoorCenter => doorCenter != null ? doorCenter.position : transform.position;

        void Awake() => Cache();

        void Cache()
        {
            if (cached) return;
            cached = true;
            if (leftDoor != null) leftHome = leftDoor.localPosition;
            if (rightDoor != null) rightHome = rightDoor.localPosition;
            if (walls != null) wallsHome = walls.localPosition;
            if (lift != null) liftHome = lift.localPosition;
            armAngle = armRest;
        }

        /// <summary>조종사 로봇 루트(공이 닿는 자리의 기준).</summary>
        public void Bind(Transform playerRoot) => player = playerRoot;

        /// <summary>공이 닿는 자리(월드).</summary>
        public Vector3 BallTarget(PilotSide side)
        {
            var offset = targetOffset;
            if (side == PilotSide.Left) offset.x = -offset.x;
            var root = player != null ? player : transform;
            return root.position + root.rotation * offset;
        }

        /// <summary>문 닫힌 처음 상태로(대기·점검 시작, 판 재시작).</summary>
        public void SetClosed()
        {
            Cache();
            gone = false;
            doorStart = leaveStart = aimAt = -1d;
            throwerWanted = false;
            liftAmount = 0f;
            turretYaw = 0f;
            armAngle = armRest;
            ballState = BallState.Hidden;
            if (walls != null)
            {
                walls.gameObject.SetActive(true);
                walls.localPosition = wallsHome;
            }
            if (leftDoor != null) leftDoor.localPosition = leftHome;
            if (rightDoor != null) rightDoor.localPosition = rightHome;
            Stop(doorFx);
            Stop(sinkFx);
            ApplyThrower();
        }

        /// <summary>문을 연다(서버 시각 start부터 seconds 동안).</summary>
        public void OpenDoors(double start, float seconds)
        {
            if (doorStart >= 0d) return;
            doorStart = start;
            doorSeconds = Mathf.Max(0.2f, seconds);
            Play(doorFx);
        }

        /// <summary>출격: 벽 전체가 땅속으로 내려간다. 문이 아직 닫혀 있어도 같이 내려간다.</summary>
        public void Leave(double start)
        {
            if (leaveStart >= 0d || gone) return;
            leaveStart = start;
            throwerWanted = false;
            Play(sinkFx);
        }

        /// <summary>정비 로봇을 올리거나 내린다(공 막기 연습 동안만 올라와 있다).</summary>
        public void ShowThrower(bool up) => throwerWanted = up;

        /// <summary>공 하나: 조준 시작(aim) → 던짐(release) → 닿음(impact). 모두 서버 시각.</summary>
        public void Aim(PilotSide side, double aim, double release, double impact)
        {
            aimSide = side;
            aimAt = aim;
            releaseAt = release;
            impactAt = impact;
            ballState = BallState.Held;
        }

        /// <summary>닿는 순간의 결과(서버 판정). 막았으면 튕겨 나가고, 못 막았으면 기체에 맞고 떨어진다.</summary>
        public void BallResult(bool blocked)
        {
            if (ball == null || ballState == BallState.Hidden || ballState == BallState.Loose) return;
            var at = flightTo;
            if (ballState == BallState.Held) at = BallTarget(aimSide);   // 던지는 그림을 못 본 PC(막 들어옴)
            ball.position = at;
            var back = flightFrom - at;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) back = transform.forward;
            back.Normalize();
            var root = player != null ? player : transform;
            var outward = root.right * (aimSide == PilotSide.Left ? -1f : 1f);
            if (blocked)
            {
                looseVelocity = (back * 0.7f + outward * 0.55f).normalized * bounceSpeed + Vector3.up * (bounceSpeed * 0.55f);
                Burst(blockFx, at);
            }
            else
            {
                // 기체에 맞고 힘없이 아래로 흘러내린다.
                looseVelocity = back * (bounceSpeed * 0.2f) + outward * (bounceSpeed * 0.25f) + Vector3.down * 4f;
                Burst(missFx, at);
            }
            looseSpin = Random.insideUnitSphere * 200f;
            looseUntil = Time.time + looseSeconds;
            ballState = BallState.Loose;
            aimAt = -1d;
        }

        /// <summary>프레임마다(모든 PC). now = 서버 시각.</summary>
        public void Tick(double now)
        {
            Cache();
            TickWalls(now);
            TickThrower(now);
            TickBall(now);
        }

        void TickWalls(double now)
        {
            if (gone) return;
            var open = doorStart >= 0d ? PropMotion.Heavy((float)((now - doorStart) / doorSeconds)) : 0f;
            if (leftDoor != null) leftDoor.localPosition = leftHome + Vector3.left * (doorTravel * open);
            if (rightDoor != null) rightDoor.localPosition = rightHome + Vector3.right * (doorTravel * open);
            if (leaveStart < 0d || walls == null) return;
            var u = (float)((now - leaveStart) / sinkSeconds);
            walls.localPosition = wallsHome + Vector3.down * (sinkMeters * PropMotion.Heavy(u));
            if (u < 1f) return;
            gone = true;
            walls.gameObject.SetActive(false);
        }

        void TickThrower(double now)
        {
            liftAmount = Mathf.MoveTowards(liftAmount, throwerWanted ? 1f : 0f, Time.deltaTime / liftSeconds);
            float wantYaw = 0f, wantArm = armRest;
            if (aimAt >= 0d && lift != null)
            {
                var local = lift.InverseTransformPoint(BallTarget(aimSide));
                wantYaw = (local.x >= 0f ? 1f : -1f) * aimYawDegrees;
                if (now < releaseAt)
                {
                    // 예고: 그쪽으로 돌며 팔을 뒤로 젖힌다(묵직하게).
                    var u = PropMotion.Heavy((float)((now - aimAt) / System.Math.Max(0.01d, releaseAt - aimAt)) * 1.25f);
                    turretYaw = Mathf.Lerp(0f, wantYaw, u);
                    armAngle = Mathf.Lerp(armRest, armWound, u);
                }
                else
                {
                    // 던짐: 팔을 앞으로 휘두른다.
                    var u = Mathf.Clamp01((float)(now - releaseAt) / throwSeconds);
                    turretYaw = wantYaw;
                    armAngle = Mathf.Lerp(armWound, armThrown, u * u);
                }
            }
            else
            {
                turretYaw = Mathf.MoveTowards(turretYaw, wantYaw, 30f * Time.deltaTime);
                armAngle = Mathf.MoveTowards(armAngle, wantArm, 40f * Time.deltaTime);
            }
            ApplyThrower();
        }

        void ApplyThrower()
        {
            if (lift != null)
            {
                var shown = liftAmount > 0.001f;
                if (lift.gameObject.activeSelf != shown) lift.gameObject.SetActive(shown);
                lift.localPosition = liftHome + Vector3.down * (liftDepth * (1f - PropMotion.Heavy(liftAmount)));
            }
            if (turret != null) turret.localRotation = Quaternion.Euler(0f, turretYaw, 0f);
            if (arm != null) arm.localRotation = Quaternion.Euler(armAngle, 0f, 0f);
        }

        void TickBall(double now)
        {
            if (ball == null) return;
            if (ballState == BallState.Held && now >= releaseAt)
            {
                flightFrom = ballRest != null ? ballRest.position : ball.position;
                flightTo = BallTarget(aimSide);
                ballState = BallState.Flying;
            }
            var visible = ballState != BallState.Hidden && (ballState != BallState.Held || liftAmount > 0.5f);
            if (ball.gameObject.activeSelf != visible) ball.gameObject.SetActive(visible);
            switch (ballState)
            {
                case BallState.Held:
                    if (ballRest != null) ball.SetPositionAndRotation(ballRest.position, ballRest.rotation);
                    break;
                case BallState.Flying:
                    var u = (float)((now - releaseAt) / System.Math.Max(0.01d, impactAt - releaseAt));
                    // 닿는 자리는 날아오는 동안에도 로봇을 따라간다(로봇이 조금 움직여도 방패 자리에 닿게).
                    flightTo = BallTarget(aimSide);
                    ball.position = PropMotion.Arc(flightFrom, flightTo, arcHeight, u);
                    ball.Rotate(Vector3.right, 90f * Time.deltaTime, Space.Self);
                    // 결과 사건이 끝내 안 오면(끊김) 못 막은 것으로 흘려보낸다.
                    if (now > impactAt + 1d) BallResult(false);
                    break;
                case BallState.Loose:
                    looseVelocity += Vector3.down * (gravity * Time.deltaTime);
                    var p = ball.position + looseVelocity * Time.deltaTime;
                    var floor = transform.position.y + ballRadius;
                    if (p.y < floor && looseVelocity.y < 0f)
                    {
                        p.y = floor;
                        looseVelocity = new Vector3(looseVelocity.x * 0.6f, -looseVelocity.y * 0.35f, looseVelocity.z * 0.6f);
                    }
                    ball.position = p;
                    ball.Rotate(looseSpin * Time.deltaTime, Space.World);
                    if (Time.time >= looseUntil) ballState = BallState.Hidden;
                    break;
            }
        }

        static void Play(ParticleSystem[] list)
        {
            if (list == null) return;
            foreach (var fx in list)
                if (fx != null) fx.Play(true);
        }

        static void Stop(ParticleSystem[] list)
        {
            if (list == null) return;
            foreach (var fx in list)
                if (fx != null) fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        static void Burst(ParticleSystem fx, Vector3 at)
        {
            if (fx == null) return;
            fx.transform.position = at;
            fx.Play(true);
        }
    }
}
