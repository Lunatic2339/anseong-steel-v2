using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>맵 소품이 움직이는 수학(정비고 문·공·코일·무너지는 구조물). 순수 함수 → EditMode 테스트.</summary>
    public static class PropMotion
    {
        /// <summary>from → to 포물선. u = 0~1, 가운데에서 arcHeight만큼 뜬다.</summary>
        public static Vector3 Arc(Vector3 from, Vector3 to, float arcHeight, float u)
        {
            u = Mathf.Clamp01(u);
            return Vector3.Lerp(from, to, u) + Vector3.up * (arcHeight * 4f * u * (1f - u));
        }

        /// <summary>height(m)를 떨어지는 시간(초).</summary>
        public static float FallSeconds(float height, float gravity) =>
            height <= 0f || gravity <= 0f ? 0f : Mathf.Sqrt(2f * height / gravity);

        /// <summary>무거운 것이 움직이는 곡선: 천천히 출발해 천천히 멈춘다(0~1).</summary>
        public static float Heavy(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        /// <summary>부딪혀 기우는 곡선: 0에서 1로 가며 넘쳤다가 출렁이며 가라앉는다(u = 지난 시간 / 가라앉는 시간).</summary>
        public static float Settle(float u)
        {
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            return 1f - Mathf.Exp(-5f * u) * Mathf.Cos(11f * u) * (1f - u);
        }

        /// <summary>바닥(XZ) 거리로 가장 가까운 것의 번호. 없으면 -1.</summary>
        public static int Nearest(Vector3[] points, Vector3 to)
        {
            var best = -1;
            var bestSqr = float.MaxValue;
            if (points == null) return best;
            for (var i = 0; i < points.Length; i++)
            {
                var dx = points[i].x - to.x;
                var dz = points[i].z - to.z;
                var sqr = dx * dx + dz * dz;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        /// <summary>한 축 위에서 목표로 다가가되 범위 안에 머문다(크레인 주행).</summary>
        public static float Travel(float current, float target, float min, float max, float speed, float dt) =>
            Mathf.MoveTowards(current, Mathf.Clamp(target, min, max), Mathf.Max(0f, speed) * dt);
    }

    /// <summary>
    /// 크레인 코일이 떨어지는 길(시간의 함수라 모든 PC가 같은 그림): 떨어짐 → 보스에 맞음(<see cref="ImpactTime"/>) → 옆으로 튕김 →
    /// 땅에 닿음(<see cref="LandTime"/>) → 굴러가다 멈춤(<see cref="EndTime"/>).
    /// </summary>
    public struct CoilDrop
    {
        /// <summary>매달려 있던 자리(코일 가운데).</summary>
        public Vector3 From;
        /// <summary>보스에 맞는 자리(코일 가운데).</summary>
        public Vector3 Impact;
        /// <summary>튕겨 굴러가는 쪽(바닥 방향, 길이 1).</summary>
        public Vector3 RollDirection;
        public float Gravity, BounceSpeed, BounceUp, GroundY, RollMeters, RollSeconds;

        public float ImpactTime => PropMotion.FallSeconds(From.y - Impact.y, Gravity);

        public float LandTime
        {
            get
            {
                var g = Mathf.Max(0.01f, Gravity);
                var drop = Mathf.Max(0f, Impact.y - GroundY);
                return ImpactTime + (BounceUp + Mathf.Sqrt(BounceUp * BounceUp + 2f * g * drop)) / g;
            }
        }

        public float EndTime => LandTime + Mathf.Max(0f, RollSeconds);

        /// <summary>땅에 닿는 자리.</summary>
        public Vector3 LandPoint
        {
            get
            {
                var p = Impact + RollDirection * (BounceSpeed * (LandTime - ImpactTime));
                p.y = GroundY;
                return p;
            }
        }

        /// <summary>떨어지기 시작한 뒤 t초의 자리.</summary>
        public Vector3 Position(float t)
        {
            if (t <= 0f) return From;
            var hit = ImpactTime;
            if (t < hit)
            {
                // 옆으로는 뒤로 갈수록 빨리 붙는다(보스가 조금 비켜 있어도 맞게).
                var u = hit > 0f ? t / hit : 1f;
                var p = Vector3.Lerp(From, Impact, u * u);
                p.y = From.y - 0.5f * Gravity * t * t;
                return p;
            }
            var land = LandTime;
            if (t < land)
            {
                var b = t - hit;
                return Impact + RollDirection * (BounceSpeed * b) + Vector3.up * (BounceUp * b - 0.5f * Gravity * b * b);
            }
            var r = RollSeconds > 0f ? Mathf.Clamp01((t - land) / RollSeconds) : 1f;
            return LandPoint + RollDirection * (RollMeters * (1f - (1f - r) * (1f - r)));
        }

        /// <summary>땅에 닿은 뒤 굴러간 거리(m). 굴림 회전에 쓴다.</summary>
        public float Rolled(float t)
        {
            var land = LandTime;
            if (t <= land) return 0f;
            var r = RollSeconds > 0f ? Mathf.Clamp01((t - land) / RollSeconds) : 1f;
            return RollMeters * (1f - (1f - r) * (1f - r));
        }
    }
}
