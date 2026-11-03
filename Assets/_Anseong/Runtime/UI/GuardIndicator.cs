using Anseong.Combat;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.UI
{
    /// <summary>
    /// 조종석 콘솔의 왼팔·오른팔 가드 표시등(M1-06). 가드면 밝게, 아니면 어둡게. 중간발표에서도 보이는 게임 UI(개발용 아님).
    /// 판정 이유는 개발용 안내판(<see cref="TestGuidePanel"/>)에만 띄운다. 메뉴가 조종석 아래에 만든다.
    /// </summary>
    [DefaultExecutionOrder(GuardDriver.ExecutionOrder + 10)]
    public class GuardIndicator : MonoBehaviour
    {
        [SerializeField, Tooltip("가드 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        GuardDriver guard;

        [SerializeField, Tooltip("왼팔·오른팔 표시등(순서대로). 메뉴가 만든다.")]
        Renderer[] lights = new Renderer[2];

        [SerializeField, Tooltip("가드 중 색.")]
        Color onColor = new Color(0.35f, 1f, 0.55f);

        [SerializeField, Tooltip("가드 아님 색.")]
        Color offColor = new Color(0.25f, 0.08f, 0.06f);

        [SerializeField, Tooltip("이 팔을 맡지 않거나 보정 전일 때 색.")]
        Color idleColor = new Color(0.12f, 0.12f, 0.12f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock block;
        readonly int[] shown = { -1, -1 };

        void Start()
        {
            if (guard == null) guard = FindAnyObjectByType<GuardDriver>();
            block = new MaterialPropertyBlock();
            if (guard == null)
            {
                for (var i = 0; i < lights.Length; i++) Set(i, idleColor);
                enabled = false;
            }
        }

        void LateUpdate()
        {
            for (var h = 0; h < 2 && h < lights.Length; h++)
            {
                var side = (PilotSide)h;
                var state = guard.State(side);
                var value = !guard.UsesHand(side) || state.Reason == GuardReason.NotCalibrated ? 0 : state.IsGuarding ? 2 : 1;
                if (value == shown[h]) continue;
                shown[h] = value;
                Set(h, value == 2 ? onColor : value == 1 ? offColor : idleColor);
            }
        }

        void Set(int index, Color color)
        {
            if (index >= lights.Length || lights[index] == null) return;
            lights[index].GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            lights[index].SetPropertyBlock(block);
        }
    }
}
