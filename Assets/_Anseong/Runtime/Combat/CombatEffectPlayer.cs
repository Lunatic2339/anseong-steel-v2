using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 전투 사건을 듣고 <see cref="CombatEffectSet"/> 표대로 효과를 띄운다(M5-03, 모든 PC). 프리팹이 있으면 그것을, 없으면 색 구를 커졌다 사라지게.
    /// 소리(<see cref="CombatCuePlayer"/>)와 따로 돈다. 표가 없으면 아무것도 안 한다.
    /// </summary>
    public class CombatEffectPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정. 비면 시작할 때 한 번 찾는다.")] CombatDirector director;
        [SerializeField, Tooltip("효과 표(Art/VFX/CombatEffects.asset). 메뉴가 넣는다.")] CombatEffectSet effects;
        [SerializeField, Tooltip("도형 재질(비면 기본 재질).")] Material shapeMaterial;
        [SerializeField, Tooltip("끄면 프리팹 없는 줄은 아무것도 안 띄운다(도형 대신 무).")] bool placeholderShapes = true;
        [SerializeField, Min(0f), Tooltip("조종석 기준 효과를 앞으로 띄울 거리(m, 로봇 크기).")] float cockpitForward = 8f;

        const int MaxShapes = 24;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform boss, cockpit;
        MaterialPropertyBlock block;
        readonly Transform[] shapes = new Transform[MaxShapes];
        readonly Renderer[] shapeRenderers = new Renderer[MaxShapes];
        readonly float[] shapeStart = new float[MaxShapes];
        readonly float[] shapeEnd = new float[MaxShapes];
        readonly float[] shapeSize = new float[MaxShapes];
        int nextShape;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (director == null)
            {
                enabled = false;
                return;
            }
            var brain = FindAnyObjectByType<Anseong.Boss.BossBrain>();
            if (brain != null) boss = brain.transform;
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) cockpit = seat.transform;
            block = new MaterialPropertyBlock();
            director.Cue += OnCue;
        }

        void OnDestroy()
        {
            if (director != null) director.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            if (effects == null) return;
            foreach (var entry in effects.Entries)
            {
                if (entry == null || !entry.Matches(e)) continue;
                var at = Where(entry.anchor, e.Point) + Vector3.up * entry.lift;
                if (entry.prefab != null)
                {
                    var go = Instantiate(entry.prefab, at, Quaternion.identity);
                    go.transform.localScale *= entry.scale;
                    Destroy(go, entry.seconds);
                }
                else if (placeholderShapes) Shape(at, entry);
            }
        }

        Vector3 Where(CombatEffectSet.Anchor anchor, Vector3 point)
        {
            switch (anchor)
            {
                case CombatEffectSet.Anchor.Boss: return boss != null ? boss.position : point;
                case CombatEffectSet.Anchor.Cockpit: return cockpit != null ? cockpit.position + cockpit.forward * cockpitForward : point;
                default: return point;
            }
        }

        /// <summary>도형은 미리 만든 구 몇 개를 돌려 쓴다(매번 만들고 지우지 않음).</summary>
        void Shape(Vector3 at, CombatEffectSet.Entry entry)
        {
            var i = nextShape;
            nextShape = (nextShape + 1) % MaxShapes;
            if (shapes[i] == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Effect (임시)";
                var c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
                go.transform.SetParent(transform, false);
                shapeRenderers[i] = go.GetComponent<Renderer>();
                if (shapeMaterial != null) shapeRenderers[i].sharedMaterial = shapeMaterial;
                shapeRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shapes[i] = go.transform;
            }
            shapes[i].position = at;
            shapes[i].gameObject.SetActive(true);
            shapeStart[i] = Time.time;
            shapeEnd[i] = Time.time + entry.seconds;
            shapeSize[i] = entry.scale;
            Tint(i, entry.color);
        }

        void Tint(int i, Color color)
        {
            shapeRenderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            shapeRenderers[i].SetPropertyBlock(block);
        }

        void Update()
        {
            var now = Time.time;
            for (var i = 0; i < MaxShapes; i++)
            {
                if (shapes[i] == null || !shapes[i].gameObject.activeSelf) continue;
                if (now >= shapeEnd[i])
                {
                    shapes[i].gameObject.SetActive(false);
                    continue;
                }
                var t = Mathf.InverseLerp(shapeStart[i], shapeEnd[i], now);
                // 빨리 커지고 천천히 줄어듦(묵직한 폭발 느낌).
                var size = shapeSize[i] * (t < 0.2f ? Mathf.Lerp(0.3f, 1f, t / 0.2f) : Mathf.Lerp(1f, 0.1f, (t - 0.2f) / 0.8f));
                shapes[i].localScale = Vector3.one * size;
            }
        }
    }
}
