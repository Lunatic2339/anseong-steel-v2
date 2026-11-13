using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>
    /// 보스가 처박히면 찌그러지고 기울며 조각이 떨어지는 맵 구조물(컨베이어 다리 한 칸, 사일로 하나). 겉모습만(충돌 없음, 각 PC가 따로 그린다).
    /// <see cref="MapEvents"/>가 맵 사건을 듣고 <see cref="Break"/>를 부르고, 판을 다시 시작하면 <see cref="ResetProp"/>으로 원래대로 세운다.
    /// 도형은 메뉴 'Build Stage Props'가 조립해 칸을 채운다. 윤주 모델이 오면 같은 칸(몸통·조각·먼지)에 넣으면 된다.
    /// </summary>
    public class BreakableProp : MonoBehaviour
    {
        [SerializeField, Tooltip("기울고 찌그러지는 몸통. 원점 = 바닥 가운데(그 점을 축으로 기운다).")]
        Transform body;
        [SerializeField, Tooltip("부서질 때 떨어져 나가는 조각들(몸통의 자식).")]
        Transform[] pieces = new Transform[0];
        [SerializeField, Tooltip("부서질 때 터뜨릴 먼지·불꽃 파티클.")]
        ParticleSystem[] dust = new ParticleSystem[0];

        [Header("찌그러짐")]
        [SerializeField, Range(0f, 45f), Tooltip("부딪힌 반대쪽으로 기우는 각도(°).")]
        float tiltDegrees = 10f;
        [SerializeField, Tooltip("찌그러진 뒤 크기 배수(가로·세로·가로).")]
        Vector3 squash = new Vector3(1.06f, 0.92f, 1.06f);
        [SerializeField, Min(0f), Tooltip("주저앉는 깊이(m). 다리가 꺾이는 구조물용.")]
        float sagMeters;
        [SerializeField, Min(0.2f), Tooltip("기울어 출렁이다 멈추기까지(초). 묵직하게 길게.")]
        float settleSeconds = 1.6f;

        [Header("조각")]
        [SerializeField, Min(0f), Tooltip("조각이 튀어 나가는 빠르기(m/s).")]
        float pieceSpeed = 16f;
        [SerializeField, Min(0f), Tooltip("조각이 위로 뜨는 빠르기(m/s).")]
        float pieceUp = 8f;
        [SerializeField, Tooltip("켜면 조각이 부딪혀 온 쪽(보스 쪽)으로 튀어나온다(사일로 앞면). 끄면 밀려난 쪽으로 날아간다(컨베이어).")]
        bool piecesFlyBack;
        [SerializeField, Min(0.1f), Tooltip("조각에 걸리는 중력(m/s²).")]
        float gravity = 14f;
        [SerializeField, Min(0f), Tooltip("조각이 멈추는 바닥 높이(이 오브젝트 기준 m).")]
        float groundHeight = 1f;

        Vector3 homePosition, homeScale;
        Quaternion homeRotation;
        Vector3[] pieceHomePosition, pieceVelocity, pieceSpin;
        Quaternion[] pieceHomeRotation;
        bool[] pieceResting;
        Vector3 tiltAxis;
        float breakStart = -1f, strength = 1f;
        bool cached;

        /// <summary>몸통 가운데 바닥(가장 가까운 구조물 고르기용).</summary>
        public Vector3 BasePoint => transform.position;
        public bool Broken => breakStart >= 0f;

        void Awake() => Cache();

        void Cache()
        {
            if (cached) return;
            cached = true;
            if (body == null) body = transform;
            homePosition = body.localPosition;
            homeRotation = body.localRotation;
            homeScale = body.localScale;
            var n = pieces != null ? pieces.Length : 0;
            pieceHomePosition = new Vector3[n];
            pieceHomeRotation = new Quaternion[n];
            pieceVelocity = new Vector3[n];
            pieceSpin = new Vector3[n];
            pieceResting = new bool[n];
            for (var i = 0; i < n; i++)
            {
                if (pieces[i] == null) continue;
                pieceHomePosition[i] = pieces[i].localPosition;
                pieceHomeRotation[i] = pieces[i].localRotation;
            }
        }

        /// <summary>부순다. from = 부딪혀 오는 쪽(보스 자리), amount = 세기(이웃 칸은 약하게).</summary>
        public void Break(Vector3 from, float amount = 1f)
        {
            Cache();
            if (Broken) return;
            strength = Mathf.Clamp(amount, 0.1f, 1.5f);
            var away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = transform.forward;
            away.Normalize();
            // 꼭대기가 away 쪽으로 넘어가게: 축 = 위 × away.
            tiltAxis = Vector3.Cross(Vector3.up, away);
            breakStart = Time.time;
            for (var i = 0; i < pieceVelocity.Length; i++)
            {
                if (pieces[i] == null) continue;
                var spread = Random.insideUnitSphere;
                spread.y = Mathf.Abs(spread.y);
                var push = piecesFlyBack ? -away : away;
                pieceVelocity[i] = (push * Random.Range(0.4f, 1f) + spread * 0.6f) * (pieceSpeed * strength) + Vector3.up * (pieceUp * Random.Range(0.3f, 1f));
                pieceSpin[i] = Random.insideUnitSphere * 140f;
                pieceResting[i] = false;
            }
            if (dust != null)
                foreach (var d in dust)
                    if (d != null) d.Play(true);
        }

        /// <summary>원래대로 세운다(판 재시작).</summary>
        public void ResetProp()
        {
            Cache();
            breakStart = -1f;
            body.localPosition = homePosition;
            body.localRotation = homeRotation;
            body.localScale = homeScale;
            for (var i = 0; i < pieceHomePosition.Length; i++)
            {
                if (pieces[i] == null) continue;
                pieces[i].localPosition = pieceHomePosition[i];
                pieces[i].localRotation = pieceHomeRotation[i];
            }
            if (dust != null)
                foreach (var d in dust)
                    if (d != null) d.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Update()
        {
            if (breakStart < 0f) return;
            var elapsed = Time.time - breakStart;
            var u = elapsed / settleSeconds;
            var parent = body.parent;
            var s = PropMotion.Settle(u);
            // 기울기(월드 축) → 부모 기준 회전으로.
            var tilt = Quaternion.AngleAxis(tiltDegrees * strength * s, tiltAxis);
            var parentRotation = parent != null ? parent.rotation : Quaternion.identity;
            body.rotation = tilt * (parentRotation * homeRotation);
            var crush = Mathf.Clamp01(u * 4f) * Mathf.Min(1f, strength);
            body.localScale = Vector3.Scale(homeScale, Vector3.Lerp(Vector3.one, squash, crush));
            body.localPosition = homePosition + Vector3.down * (sagMeters * strength * PropMotion.Heavy(u * 1.5f));

            var ground = transform.position.y + groundHeight;
            var dt = Time.deltaTime;
            for (var i = 0; i < pieceVelocity.Length; i++)
            {
                var piece = pieces[i];
                if (piece == null || pieceResting[i]) continue;
                pieceVelocity[i] += Vector3.down * (gravity * dt);
                var p = piece.position + pieceVelocity[i] * dt;
                if (p.y <= ground && pieceVelocity[i].y < 0f)
                {
                    p.y = ground;
                    // 한 번 낮게 튀고 멈춘다.
                    if (pieceVelocity[i].y < -6f) pieceVelocity[i] = new Vector3(pieceVelocity[i].x * 0.4f, -pieceVelocity[i].y * 0.25f, pieceVelocity[i].z * 0.4f);
                    else pieceResting[i] = true;
                }
                piece.position = p;
                if (!pieceResting[i]) piece.Rotate(pieceSpin[i] * dt, Space.World);
            }
        }
    }
}
