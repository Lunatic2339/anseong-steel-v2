using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 필살기 임시 연출(M2-05): 충전 동안 플레이어 로봇 몸이 점점 밝아지고, 발사하면 두 주먹 가운데에서 보스까지 광선(LineRenderer)을 3초 쏜다.
    /// 전투 이벤트만 듣는다(모든 PC가 같은 이벤트를 받는다). 진짜 효과(VFX·셰이더)는 M5에서 이 자리를 바꾼다.
    /// 재질은 바꾸지 않고 MaterialPropertyBlock으로 색만 덮는다(URP Lit _BaseColor·_EmissionColor). 플레이어 로봇 루트에 메뉴가 붙인다.
    /// </summary>
    public class FinisherVisual : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;
        [SerializeField, Tooltip("밝아질 로봇 렌더러. 비어 있으면 자식의 SkinnedMeshRenderer 전부.")]
        Renderer[] renderers = new Renderer[0];
        [SerializeField, Tooltip("광선 시작점(두 주먹). 비어 있으면 자식의 주먹 명중 구.")]
        Transform[] beamFrom = new Transform[0];
        [SerializeField, Tooltip("광선. 비어 있으면 광선 없이 발광만.")]
        LineRenderer beam;

        [SerializeField, Tooltip("발광 색.")]
        Color glow = new Color(0.3f, 0.8f, 1f);
        [SerializeField, Range(0f, 1f), Tooltip("충전이 다 찼을 때 섞는 정도.")]
        float strength = 0.8f;
        [SerializeField, Min(0f), Tooltip("광선 끝 높이(보스 발 위치에서 위로, m).")]
        float targetHeight = 40f;
        [SerializeField, Min(0.1f), Tooltip("광선 굵기(m).")]
        float beamWidth = 6f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        float chargeStart = -1f, chargeSeconds = 1.5f, beamUntil = -1f;
        Vector3 target;
        bool tinted;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (beamFrom == null || beamFrom.Length == 0)
            {
                var probes = GetComponentsInChildren<FistHitProbe>(true);
                beamFrom = new Transform[probes.Length];
                for (var i = 0; i < probes.Length; i++) beamFrom[i] = probes[i].transform;
            }
            if (director == null)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            if (beam != null) beam.enabled = false;
            director.Cue += OnCue;
        }

        void OnDestroy()
        {
            if (director != null) director.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            switch (e.Cue)
            {
                case CombatCue.FinisherCharge:
                    chargeStart = Time.time;
                    chargeSeconds = Mathf.Max(0.1f, e.Amount);
                    break;
                case CombatCue.FinisherReset:
                    chargeStart = -1f;
                    Clear();
                    break;
                case CombatCue.FinisherFire:
                    chargeStart = -1f;
                    target = e.Point + Vector3.up * targetHeight;
                    beamUntil = Time.time + Mathf.Max(0.1f, e.Amount);
                    Tint(1f);
                    break;
                case CombatCue.EncounterWon:
                case CombatCue.BossDefeated:
                    // 광선 시간이 끝나면 저절로 꺼진다. 재시작은 아래 Update가 정리.
                    break;
            }
        }

        void Update()
        {
            // 재시작(R)하면 페이즈가 1로 돌아간다: 남은 발광·광선을 끈다.
            if ((chargeStart >= 0f || beamUntil >= 0f) && director.State.BossPhase < 4)
            {
                chargeStart = beamUntil = -1f;
                if (beam != null) beam.enabled = false;
                Clear();
                return;
            }
            if (chargeStart >= 0f) Tint(Mathf.Clamp01((Time.time - chargeStart) / chargeSeconds));
            if (beamUntil < 0f) return;
            if (Time.time >= beamUntil)
            {
                beamUntil = -1f;
                if (beam != null) beam.enabled = false;
                Clear();
                return;
            }
            if (beam == null) return;
            var from = Vector3.zero;
            var count = 0;
            foreach (var t in beamFrom)
            {
                if (t == null) continue;
                from += t.position;
                count++;
            }
            from = count > 0 ? from / count : transform.position + Vector3.up * targetHeight;
            beam.enabled = true;
            beam.positionCount = 2;
            beam.SetPosition(0, from);
            beam.SetPosition(1, target);
            // 살짝 굵기가 떨린다.
            var w = beamWidth * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f));
            beam.startWidth = w;
            beam.endWidth = w * 0.6f;
        }

        void Tint(float k)
        {
            k *= strength;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(Color.white, glow, k));
                block.SetColor(EmissionId, glow * (k * 2f));
                r.SetPropertyBlock(block);
            }
            tinted = true;
        }

        void Clear()
        {
            if (!tinted) return;
            foreach (var r in renderers)
                if (r != null) r.SetPropertyBlock(null);
            tinted = false;
        }
    }
}
