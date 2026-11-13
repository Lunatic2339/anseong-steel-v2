using System;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 전투 사건 → 시각 효과 표(M5-03, 유진 VFX 자리). <see cref="CombatCueSet"/>(소리)와 같은 방식: 사건마다 줄 하나, 프리팹 칸에 VFX를 넣기만 하면 된다.
    /// 프리팹 칸이 비면 색 구(커졌다 사라짐)로 대신 보여 준다(확정 사항 10: 단순 도형·기본 파티클). 메뉴 'Add Match Flow'가 Art/VFX/CombatEffects.asset을 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "Anseong/Combat Effect Set", fileName = "CombatEffects")]
    public class CombatEffectSet : ScriptableObject
    {
        public enum Anchor
        {
            /// <summary>사건 자리(맞은 곳·드론·조준점).</summary>
            EventPoint,
            /// <summary>보스 몸 가운데.</summary>
            Boss,
            /// <summary>우리 조종석 앞.</summary>
            Cockpit,
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("이 효과의 이름(유진 VFX 목록 이름, 예: VFX_HookImpact).")]
            public string effectName;
            [Tooltip("어떤 사건에.")]
            public CombatCue cue;
            [Tooltip("켜면 사건 번호(Code)가 아래 값일 때만.")]
            public bool matchCode;
            public int code;
            [Tooltip("훅만 등 펀치 종류(Any = 모두).")]
            public CombatCueSet.KindFilter kind;
            [Tooltip("유진 VFX 프리팹. 비면 아래 색 구.")]
            public GameObject prefab;
            [Tooltip("어디에 띄울지.")]
            public Anchor anchor;
            [Tooltip("자리에서 위로 올릴 거리(m).")]
            public float lift;
            [Min(0.05f), Tooltip("몇 초 뒤 지울지.")]
            public float seconds = 1f;
            [Min(0.01f), Tooltip("크기 배수(도형일 땐 지름 m).")]
            public float scale = 1f;
            [Tooltip("도형 색(프리팹이 있으면 안 씀).")]
            public Color color = Color.white;

            public bool Matches(CombatEvent e)
            {
                if (e.Cue != cue) return false;
                if (matchCode && e.Code != code) return false;
                switch (kind)
                {
                    case CombatCueSet.KindFilter.Jab: return e.Kind == PunchKind.Jab;
                    case CombatCueSet.KindFilter.Hook: return e.Kind == PunchKind.Hook;
                    case CombatCueSet.KindFilter.Uppercut: return e.Kind == PunchKind.Uppercut;
                    default: return true;
                }
            }
        }

        [SerializeField] Entry[] entries = new Entry[0];

        public Entry[] Entries => entries ?? new Entry[0];

        public void SetEntries(Entry[] value) => entries = value ?? new Entry[0];

        /// <summary>06·12 문서의 VFX 목록 기본 줄(유진 결과물 이름).</summary>
        public static Entry[] DefaultEntries() => new[]
        {
            New("VFX_PunchSpark", CombatCue.Hit, Anchor.EventPoint, 0.4f, 3f, new Color(1f, 0.8f, 0.4f), CombatCueSet.KindFilter.Jab),
            New("VFX_HookImpact", CombatCue.Hit, Anchor.EventPoint, 0.5f, 6f, new Color(1f, 0.55f, 0.2f), CombatCueSet.KindFilter.Hook),
            New("VFX_HookImpact", CombatCue.Hit, Anchor.EventPoint, 0.5f, 6f, new Color(1f, 0.55f, 0.2f), CombatCueSet.KindFilter.Uppercut),
            New("VFX_BoostShock", CombatCue.Boost, Anchor.EventPoint, 0.8f, 14f, new Color(0.5f, 0.8f, 1f)),
            New("VFX_GuardSpark", CombatCue.GrabStart, Anchor.EventPoint, 0.5f, 6f, new Color(1f, 0.85f, 0.5f)),
            New("VFX_BoostShock", CombatCue.GrabSlash, Anchor.EventPoint, 1f, 18f, new Color(1f, 0.6f, 0.25f)),
            New("VFX_GuardSpark", CombatCue.ClashStart, Anchor.Cockpit, 0.8f, 10f, new Color(1f, 0.9f, 0.6f), lift: 0f),
            New("VFX_GuardSpark", CombatCue.ClashTick, Anchor.Cockpit, 0.4f, 6f, new Color(1f, 0.8f, 0.4f), lift: 0f),
            New("VFX_WeaponBreak", CombatCue.ClashWin, Anchor.Boss, 1.2f, 25f, Color.white, lift: 30f),
            New("VFX_WeaponBreak", CombatCue.ClashLose, Anchor.Cockpit, 1f, 12f, Color.white, lift: 0f),
            New("VFX_GuardSpark", CombatCue.GuardBlock, Anchor.Cockpit, 0.3f, 4f, new Color(0.4f, 0.6f, 1f), lift: 0f),
            New("VFX_WeaponBreak", CombatCue.PhaseChanged, Anchor.Boss, 1.2f, 25f, Color.white, lift: 30f),
            New("VFX_DroneExplode", CombatCue.DroneDown, Anchor.EventPoint, 0.8f, 8f, new Color(1f, 0.45f, 0.1f)),
            New("VFX_MuzzleFlash", CombatCue.CannonFire, Anchor.EventPoint, 0.2f, 3f, new Color(1f, 0.95f, 0.5f)),
            New("VFX_ScanPing", CombatCue.DroneRevealed, Anchor.EventPoint, 0.5f, 5f, new Color(0.2f, 1f, 0.6f)),
            New("VFX_HackSparks", CombatCue.HackStart, Anchor.Cockpit, 1f, 2f, new Color(0.8f, 0.2f, 1f)),
            New("VFX_MapDust", CombatCue.MapEvent, Anchor.EventPoint, 2f, 40f, new Color(0.6f, 0.55f, 0.5f)),
            New("VFX_BossExplode", CombatCue.BossDefeated, Anchor.Boss, 2.5f, 60f, new Color(1f, 0.5f, 0.15f), lift: 30f),
        };

        static Entry New(string name, CombatCue cue, Anchor anchor, float seconds, float scale, Color color,
            CombatCueSet.KindFilter kind = CombatCueSet.KindFilter.Any, float lift = 0f)
        {
            return new Entry { effectName = name, cue = cue, anchor = anchor, seconds = seconds, scale = scale, color = color, kind = kind, lift = lift };
        }

        /// <summary>기본 줄 중 이 표에 없는 것(사건·이름·종류가 같은 줄이 없음). 메뉴가 기존 표에 새 줄을 더할 때.</summary>
        public Entry[] MissingDefaults()
        {
            var missing = new System.Collections.Generic.List<Entry>();
            foreach (var d in DefaultEntries())
            {
                var found = false;
                foreach (var e in Entries)
                    if (e != null && e.cue == d.cue && e.effectName == d.effectName && e.kind == d.kind) { found = true; break; }
                if (!found) missing.Add(d);
            }
            return missing.ToArray();
        }
    }
}
