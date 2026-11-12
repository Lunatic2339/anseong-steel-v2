using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 메뉴 하나로 한 판 전체를 조립한다(선우 11/12 "완성된 게임 한 번에"). 있는 메뉴를 순서대로 부를 뿐이라 따로 돌려도 결과가 같다.
    /// 보스 클립 읽기 → 보스 기술 에셋 → Main 조립(로봇·보스·XR·입력·전투·네트워크·판 흐름·드론·점검·맵 연출·HUD·효과·음악·영상) → 외골격 연결.
    /// 여러 번 실행해도 된다(있는 것은 그대로, 연결만 다시). 끝나면 Main을 Ctrl+S로 저장한다(Main은 선우만).
    /// </summary>
    public static class FullGameMenu
    {
        static readonly string[] Steps =
        {
            "Anseong/Boss/Import Boss Clips (Yujin)",
            "Anseong/Boss/Create Attack Assets",
            "Anseong/Setup/Build M1 Main",
            "Anseong/Setup/Add Exoskeleton Links",
        };

        [MenuItem("Anseong/Build Full Game (한 번에)", priority = -100)]
        static void BuildFullGame()
        {
            var log = new System.Text.StringBuilder("[FullGameMenu] ");
            foreach (var step in Steps)
            {
                var ok = EditorApplication.ExecuteMenuItem(step);
                log.Append(ok ? "✓ " : "✗ ").Append(step).Append(" / ");
                if (!ok) Debug.LogWarning($"[FullGameMenu] 메뉴를 찾지 못했거나 실패: {step}");
            }
            log.Append("끝. 콘솔의 각 메뉴 줄을 확인하고 Ctrl+S로 Main을 저장하세요.");
            Debug.Log(log.ToString());
        }
    }
}
