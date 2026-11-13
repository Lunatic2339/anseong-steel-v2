using System;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>드론 종류(10_game_scenario.md 장면 3·4-2·6·8). 드론 코드 하나로 세 가지.</summary>
    public enum DroneKind : byte
    {
        /// <summary>은폐 사격: 숨어서 쏜다. 스캐너가 훑으면 드러나고, 드러난 동안 포로 격추.</summary>
        Cloaked,
        /// <summary>달려드는 드론(총 잃은 뒤): 날아와 부딪힌다. 주먹·검으로 쳐서 떨군다.</summary>
        Rusher,
        /// <summary>해킹 드론(맨손): 날아와 팔뚝에 붙는다. 그 팔이 느려지고, 동료가 반대 손으로 쥐고 당겨 뜯어 준다.</summary>
        Hacker,
    }

    /// <summary>드론 상태. 숫자가 네트워크로 복제되므로 순서를 바꾸지 말고 끝에만 더한다.</summary>
    public enum DroneState : byte
    {
        None,
        /// <summary>숨음: 몸은 안 보이고 아지랑이만.</summary>
        Hidden,
        /// <summary>드러남(은폐 드론은 스캐너에 걸린 동안, 달려드는 드론은 늘).</summary>
        Revealed,
        Down,
        /// <summary>보스 등 뒤에서 자기 자리(해킹 드론은 팔)로 날아오는 중. 보이지만 아직 쏠 수 없다.</summary>
        Entering,
        /// <summary>제한 시간이 지나 보스에게 돌아가는 중.</summary>
        Leaving,
        /// <summary>해킹 드론이 팔뚝에 붙어 있음.</summary>
        Attached,
        /// <summary>동료 손이 해킹 드론을 붙잡음(당기면 뜯긴다).</summary>
        Grabbed,
    }

    [Serializable]
    public class DroneSettings
    {
        [Header("장면 3·4-2 숨은 드론")]
        [Tooltip("떠 있는 숨은 드론들이 번갈아 쏘는 간격(초, 대본 4). 드론이 여러 기여도 전체가 이 간격에 한 발.")]
        [Min(0.5f)] public float fireInterval = 4f;
        [Tooltip("드론 포탄 피해(기체 HP %, 막으면 0). 대본 3.")]
        [Min(0f)] public float fireDamage = 3f;
        [Tooltip("드론 포탄이 날아오는 시간(초). 빛나는 탄과 궤적이 이 시간 동안 보이고, 닿는 순간 피해가 들어간다.")]
        [Range(0.2f, 3f)] public float shellSeconds = 1.2f;
        [Tooltip("스캐너가 훑으면 드러나는 시간(초, 대본 3).")]
        [Min(0.5f)] public float revealSeconds = 3f;
        [Tooltip("스캐너 레이저가 드론을 맞혔다고 보는 각도(°).")]
        [Range(1f, 30f)] public float scanDegrees = 6f;
        [Tooltip("포 조준이 붙는 각도(°, 대본: 겨누면 조준이 붙는다).")]
        [Range(1f, 30f)] public float aimDegrees = 10f;
        [Tooltip("우리 포탄 속도(m/s). 포탄이 닿는 순간 드론이 터진다.")]
        [Min(50f)] public float cannonShellSpeed = 350f;
        [Tooltip("드론 장면을 이 시간(초) 안에 못 끝내면 남은 드론이 보스에게 돌아간다(대본 60).")]
        [Min(5f)] public float huntTimeout = 60f;
        [Tooltip("드론이 보스 등 뒤에서 자기 자리까지 날아오는 시간(초). 장면 3 셋째 물결, 4-2 끼어들기.")]
        [Min(0.5f)] public float enterSeconds = 2.5f;
        [Tooltip("움직이는 드론(장면 3 셋째 물결)이 자리에서 좌우로 오가는 폭(m).")]
        [Min(0f)] public float patrolAmplitude = 14f;
        [Tooltip("움직이는 드론이 한 번 오가는 시간(초). 길수록 느리다.")]
        [Min(1f)] public float patrolPeriod = 7f;
        [Tooltip("숨은 드론을 이 시간(초) 넘게 못 찾으면 아지랑이가 진해진다(대본: 막히지 않게).")]
        [Min(1f)] public float hintAfterSeconds = 10f;
        [Tooltip("드론을 다 잡은 뒤 보스를 쏴 볼 시간(초, 대본 3-4). 안 쏘면 그냥 다음 장면으로.")]
        [Min(0f)] public float bossShotWindow = 8f;
        [Tooltip("포탄이 보스 장갑에 튕긴 뒤 다음 장면으로 넘어가기까지(초).")]
        [Min(0f)] public float bossShotAfter = 3f;
        [Tooltip("장면 2에서 등장 뒤 이 시간(초)에 보스 등 뒤에서 드론이 흩어진다(복선).")]
        [Min(0f)] public float scatterDelay = 4f;
        [Tooltip("장면 2에서 흩어지는 드론 수(겉모습만).")]
        [Range(0, 6)] public int scatterCount = 5;

        [Header("장면 6 달려드는 드론")]
        [Tooltip("달려드는 드론이 닿기까지(초).")]
        [Min(1f)] public float rushSeconds = 5f;
        [Tooltip("달려드는 드론 사이(초).")]
        [Min(3f)] public float rushInterval = 18f;
        [Tooltip("주먹·검이 이 거리(m) 안에 오면 쳐서 떨군 것으로 본다.")]
        [Min(0.5f)] public float punchRadius = 10f;
        [Tooltip("달려드는 길이 위로 휘는 높이(m). 0이면 직선.")]
        [Min(0f)] public float rushArc = 12f;
        [Tooltip("경고음 간격(초): 멀 때. 다가올수록 아래 값으로 빨라진다.")]
        [Range(0.2f, 3f)] public float warnSlow = 0.9f;
        [Tooltip("경고음 간격(초): 닿기 직전.")]
        [Range(0.05f, 1f)] public float warnFast = 0.16f;

        [Header("장면 8 해킹 드론")]
        [Tooltip("해킹 드론이 뜨는 때(맨손 장면 시작 뒤 초). 대본: 두 번.")]
        public float[] hackTimes = { 20f, 60f };
        [Tooltip("해킹 드론이 보스 등 뒤에서 팔까지 날아오는 시간(초).")]
        [Min(0.5f)] public float hackApproachSeconds = 3f;
        [Tooltip("해킹된 팔의 반응 배율(1 = 평소, 0.35 = 약 세 배 느림).")]
        [Range(0.1f, 1f)] public float hackArmSlow = 0.35f;
        [Tooltip("동료 손이 드론에서 이 거리(m, 로봇 크기) 안에서 그립을 새로 쥐어야 붙잡는다.")]
        [Min(1f)] public float hackGrabRadius = 9f;
        [Tooltip("붙잡으려면 그립을 쥔 채 이 시간(초) 그 거리 안에 머문다(펀치·가드 중 우연히 잡히지 않게).")]
        [Min(0f)] public float hackGrabSeconds = 0.35f;
        [Tooltip("붙잡은 손이 드론이 붙은 자리에서 이 거리(m) 넘게 멀어지면 뜯긴다.")]
        [Min(2f)] public float hackPullDistance = 16f;
        [Tooltip("이 시간(초)이 지나도 못 떼면 드론이 타서 저절로 떨어진다(막히지 않게). 0 = 끝까지 붙어 있음.")]
        [Min(0f)] public float hackMaxSeconds = 30f;
    }

    /// <summary>
    /// 해킹 드론 뜯어내기 판정(대본 장면 8, 순수). 동료 손이 드론 가까이에서 그립을 새로 쥐고 잠깐 버티면 붙잡고,
    /// 쥔 채로 붙은 자리에서 멀어지면 뜯긴다. 가드·펀치 중 두 손이 우연히 가까워진 것으로는 풀리지 않는다.
    /// </summary>
    public sealed class HackPull
    {
        bool gripWas, armed;
        float dwell;

        /// <summary>붙잡았다(이제 당기면 뜯긴다).</summary>
        public bool Holding { get; private set; }

        public void Reset()
        {
            gripWas = armed = Holding = false;
            dwell = 0f;
        }

        /// <param name="gripHeld">동료 손 그립.</param>
        /// <param name="distance">동료 손에서 드론이 붙은 자리까지(m).</param>
        /// <returns>이번에 뜯어냈으면 true(한 번만).</returns>
        public bool Step(bool gripHeld, float distance, float dt, DroneSettings settings)
        {
            var pressed = gripHeld && !gripWas;
            gripWas = gripHeld;
            if (settings == null || !gripHeld)
            {
                armed = Holding = false;
                dwell = 0f;
                return false;
            }
            if (Holding)
            {
                if (distance < settings.hackPullDistance) return false;
                armed = Holding = false;
                dwell = 0f;
                return true;
            }
            if (pressed && distance <= settings.hackGrabRadius)
            {
                armed = true;
                dwell = 0f;
            }
            if (!armed) return false;
            if (distance > settings.hackGrabRadius)
            {
                // 붙잡기 전에 손이 벗어남: 다시 쥐어야 한다.
                armed = false;
                dwell = 0f;
                return false;
            }
            dwell += Mathf.Max(0f, dt);
            if (dwell >= settings.hackGrabSeconds) Holding = true;
            return false;
        }
    }

    /// <summary>드론 판정 수학(M4-04). 순수 함수.</summary>
    public static class DroneRules
    {
        /// <summary>origin에서 dir로 본 target의 각도(°).</summary>
        public static float AngleTo(Vector3 origin, Vector3 dir, Vector3 target)
        {
            var to = target - origin;
            if (to.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-8f) return 180f;
            return Vector3.Angle(dir, to);
        }

        /// <summary>각도 안의 것 중 가장 가까운 각도의 번호. 없으면 -1.</summary>
        public static int Pick(Vector3 origin, Vector3 dir, Vector3[] targets, bool[] eligible, float maxDegrees)
        {
            var best = -1;
            var bestAngle = maxDegrees;
            for (var i = 0; i < targets.Length; i++)
            {
                if (eligible != null && (i >= eligible.Length || !eligible[i])) continue;
                var a = AngleTo(origin, dir, targets[i]);
                if (a > bestAngle) continue;
                bestAngle = a;
                best = i;
            }
            return best;
        }

        /// <summary>달려드는 드론의 위치(시작 → 목표, t = 0~1). 직선·같은 빠르기.</summary>
        public static Vector3 RushPosition(Vector3 from, Vector3 to, float t) => Vector3.Lerp(from, to, Mathf.Clamp01(t));

        /// <summary>
        /// 달려드는 드론의 위치: 처음엔 천천히 다가오다 끝에 빨라지고(쳐낼 틈 → 마지막 돌진), 길이 위로 휜다.
        /// t = 0이면 시작, 1이면 목표.
        /// </summary>
        public static Vector3 RushPosition(Vector3 from, Vector3 to, float t, float arcHeight)
        {
            t = Mathf.Clamp01(t);
            var eased = Mathf.Lerp(t, t * t, 0.6f);
            return Vector3.Lerp(from, to, eased) + Vector3.up * (arcHeight * Mathf.Sin(eased * Mathf.PI));
        }

        /// <summary>달려드는 드론 경고음 간격(초). 다가올수록(t → 1) 빨라진다.</summary>
        public static float WarnInterval(float t, float slow, float fast) => Mathf.Lerp(slow, fast, Mathf.Clamp01(t));

        /// <summary>자리에서 좌우로 오가는 드론의 위치. seconds = 오가기 시작한 뒤 지난 시간.</summary>
        public static Vector3 Patrol(Vector3 post, Vector3 axis, float amplitude, float period, float seconds)
        {
            if (period <= 0f || amplitude <= 0f || axis.sqrMagnitude < 1e-8f) return post;
            return post + axis.normalized * (amplitude * Mathf.Sin(seconds / period * Mathf.PI * 2f));
        }

        /// <summary>자리 → 자리로 날아가는 드론(부드럽게 출발·도착). t = 0~1.</summary>
        public static Vector3 Fly(Vector3 from, Vector3 to, float t)
        {
            t = Mathf.Clamp01(t);
            return Vector3.Lerp(from, to, t * t * (3f - 2f * t));
        }

        /// <summary>번갈아 쏘기: last 다음부터 돌아가며 쏠 수 있는 첫 번호. 없으면 -1.</summary>
        public static int NextShooter(bool[] canFire, int last)
        {
            if (canFire == null || canFire.Length == 0) return -1;
            for (var n = 1; n <= canFire.Length; n++)
            {
                var i = ((last + n) % canFire.Length + canFire.Length) % canFire.Length;
                if (canFire[i]) return i;
            }
            return -1;
        }

        /// <summary>
        /// 포 조준선이 보스 몸통(발밑에서 minHeight~maxHeight, 반지름 radius인 원통)에 맞는지. 맞으면 point = 장갑 겉면의 명중점.
        /// 안 맞으면 point = 몸통 가운데(쏜 쪽 겉면).
        /// </summary>
        public static bool BossHit(Vector3 origin, Vector3 dir, Vector3 bossFeet, float minHeight, float maxHeight, float radius, out Vector3 point)
        {
            var to = new Vector3(bossFeet.x - origin.x, 0f, bossFeet.z - origin.z);
            var center = bossFeet + Vector3.up * ((minHeight + maxHeight) * 0.5f);
            point = to.sqrMagnitude > 1e-6f ? center - to.normalized * radius : center;
            var flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-8f) return false;
            var t = Vector3.Dot(to, flat) / flat.sqrMagnitude;
            if (t <= 0f) return false;
            var closest = origin + dir * t; // 보스 세로축에 (수평으로) 가장 가까운 점
            var offAxis = new Vector3(closest.x - bossFeet.x, 0f, closest.z - bossFeet.z);
            var height = closest.y - bossFeet.y;
            if (offAxis.sqrMagnitude > radius * radius || height < minHeight || height > maxHeight) return false;
            // 겉면: 원통에 들어가는 점까지 조준선을 따라 되돌아온다.
            var back = Mathf.Sqrt(Mathf.Max(0f, radius * radius - offAxis.sqrMagnitude)) / flat.magnitude;
            point = origin + dir * Mathf.Max(0f, t - back);
            return true;
        }

        /// <summary>장갑에 튕겨 나가는 방향(정규화). 위로 조금 들린다(하늘로 날아가는 게 보이게).</summary>
        public static Vector3 Ricochet(Vector3 incoming, Vector3 normal)
        {
            if (incoming.sqrMagnitude < 1e-8f) return Vector3.up;
            var n = normal.sqrMagnitude > 1e-8f ? normal.normalized : -incoming.normalized;
            var d = incoming.normalized;
            // 장갑 안쪽으로 들어오는 탄만 뒤집는다.
            var reflected = Vector3.Dot(d, n) < 0f ? Vector3.Reflect(d, n) : d;
            return (reflected + Vector3.up * 0.5f).normalized;
        }

        /// <summary>
        /// 보스 등 뒤에서 흩어지는 드론의 방향(정규화). index = 0..count-1을 뒤쪽 반구에 부채꼴로 편다.
        /// back = 보스 등 쪽, right = 보스 오른쪽.
        /// </summary>
        public static Vector3 ScatterDirection(int index, int count, Vector3 back, Vector3 right)
        {
            if (count <= 0) return Vector3.up;
            var b = back.sqrMagnitude > 1e-8f ? back.normalized : Vector3.back;
            var r = right.sqrMagnitude > 1e-8f ? right.normalized : Vector3.right;
            // -80°~+80° 좌우로 펴고, 번갈아 높낮이를 준다.
            var spread = count == 1 ? 0f : Mathf.Lerp(-80f, 80f, index / (float)(count - 1));
            var lift = index % 2 == 0 ? 0.55f : 0.15f;
            var rad = spread * Mathf.Deg2Rad;
            return (b * Mathf.Cos(rad) + r * Mathf.Sin(rad) + Vector3.up * lift).normalized;
        }
    }
}
