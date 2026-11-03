using Anseong.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anseong.Pilot
{
    /// <summary>
    /// 조종석 상자 안으로 들어온 자기 로봇 몸 렌더러를 숨긴다(그림자는 남김). 선우 10/28: 어깨가 조종석과 너무 겹친다.
    /// 아래팔·손 렌더러는 목록에 넣지 않으므로 주먹은 늘 보인다. 목록은 Build Player Robot Prefab 메뉴가 채운다.
    /// </summary>
    [DefaultExecutionOrder(CockpitSeat.ExecutionOrder + 1)]
    public class CockpitClearZone : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("켜면 조종석 상자와 겹치는 로봇 몸 렌더러를 숨긴다(기본). 끄면 전부 보인다(이전 동작).")]
        bool clearZone = true;

        [SerializeField, Tooltip("비울 상자 중심(m, 조종석 로컬: 바닥 중심 원점, X 오른쪽, Y 위, Z 앞).")]
        Vector3 boxCenter = new Vector3(0f, 1.25f, 0.25f);

        [SerializeField, Tooltip("비울 상자 크기(m). 기본: 폭 6(좌석 두 개 + 양옆 여유), 높이 3.5(바닥 0.5 m 아래까지), 앞뒤 4.5.")]
        Vector3 boxSize = new Vector3(6f, 3.5f, 4.5f);

        [SerializeField, Tooltip("숨길 수 있는 로봇 렌더러(아래팔·손·머리 좌석 숨김 목록 제외). 에디터 메뉴가 채운다.")]
        Renderer[] candidates = new Renderer[0];

        bool[] hidden;
        ShadowCastingMode[] original;

        /// <summary>지금 숨긴 렌더러 수.</summary>
        public int HiddenCount { get; private set; }

        void Awake()
        {
            hidden = new bool[candidates.Length];
            original = new ShadowCastingMode[candidates.Length];
        }

        void OnDisable() => RestoreAll();

        void LateUpdate()
        {
            if (!clearZone)
            {
                if (HiddenCount > 0) RestoreAll();
                return;
            }
            transform.GetPositionAndRotation(out var position, out var rotation);
            var count = 0;
            for (var i = 0; i < candidates.Length; i++)
            {
                var r = candidates[i];
                if (r == null) continue;
                var bounds = r.bounds;
                var inside = CockpitClearZoneMath.Overlaps(bounds.center, bounds.extents, position, rotation, boxCenter, boxSize);
                if (inside && !hidden[i])
                {
                    original[i] = r.shadowCastingMode;
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    hidden[i] = true;
                }
                else if (!inside && hidden[i])
                {
                    r.shadowCastingMode = original[i];
                    hidden[i] = false;
                }
                if (hidden[i]) count++;
            }
            HiddenCount = count;
        }

        void RestoreAll()
        {
            if (hidden == null) return;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (!hidden[i]) continue;
                if (candidates[i] != null) candidates[i].shadowCastingMode = original[i];
                hidden[i] = false;
            }
            HiddenCount = 0;
        }

        public void AddTuning(TuningList list)
        {
            const string g = "조종석 비우기";
            list.Describe(g, "할 것: 가드·잽·훅 하면서 주변 보기\n볼 것: 어깨·몸 모델이 조종석 안에 안 보임, 주먹·아래팔은 계속 보임");
            list.AddToggle(g, "cockpit.clear", "조종석 안 로봇 몸 숨김", () => clearZone, v => clearZone = v);
            list.AddFloat(g, "cockpit.clearWidth", "비울 폭(m)", () => boxSize.x, v => boxSize.x = v, 2f, 20f, 0.5f, 1);
            list.AddFloat(g, "cockpit.clearHeight", "비울 높이(m)", () => boxSize.y, v => { boxSize.y = v; boxCenter.y = v * 0.5f - 0.5f; }, 1f, 10f, 0.5f, 1);
            list.AddFloat(g, "cockpit.clearDepth", "비울 앞뒤 길이(m)", () => boxSize.z, v => boxSize.z = v, 1f, 15f, 0.5f, 1);
        }
    }
}
