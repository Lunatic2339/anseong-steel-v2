using Anseong.Core;
using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>
    /// 로봇 팔과 비교용 팔 모델(성재 임시 팔 등) 중 어느 쪽을 보일지 바꾼다(샌드박스 비교용, 선우 10/28).
    /// 두 쪽 팔 드라이버는 늘 같이 돌고 렌더러만 켜고 끈다. 끄면(로봇 팔) 이 컴포넌트가 없을 때와 같다.
    /// 펀치 분류·명중은 계속 로봇 팔 드라이버로 판정한다. 'Anseong → Sandbox → Add Comparison Arms' 메뉴가 붙이고 채운다.
    /// </summary>
    public class ArmModelSwap : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("켜면 비교용 팔을 보이고 로봇 팔을 숨긴다. 끄면 로봇 팔(기본).")]
        bool showComparisonArms;

        [SerializeField, Tooltip("로봇 팔 렌더러(위팔 아래 뼈에만 묶인 것). 메뉴가 채운다.")]
        Renderer[] robotArmRenderers = new Renderer[0];

        [SerializeField, Tooltip("비교용 팔 모델의 렌더러. 메뉴가 채운다.")]
        Renderer[] comparisonRenderers = new Renderer[0];

        public bool ShowComparisonArms
        {
            get => showComparisonArms;
            set
            {
                showComparisonArms = value;
                Apply();
            }
        }

        void Start()
        {
            Apply();
        }

        void Apply()
        {
            SetEnabled(robotArmRenderers, !showComparisonArms);
            SetEnabled(comparisonRenderers, showComparisonArms);
        }

        static void SetEnabled(Renderer[] renderers, bool on)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
                if (r != null) r.enabled = on;
        }

        public void AddTuning(TuningList list)
        {
            list.Describe("팔 모델", "할 것: 팔 모델을 바꿔 가며 가드·잽·훅\n볼 것: 크기·어깨 위치·주먹 방향, 어느 쪽이 육중한 로봇 같은지");
            list.AddChoice("팔 모델", "arm.model", "보이는 팔", () => showComparisonArms ? 1 : 0, v => ShowComparisonArms = v == 1, new[] { "로봇 팔", "비교용 팔" });
            list.AddCheck("팔 모델", "arm.modelFollows", "비교용 팔이 손을 따라 움직임");
            list.AddCheck("팔 모델", "arm.modelFist", "비교용 팔 주먹이 앞을 향함");
        }
    }
}
