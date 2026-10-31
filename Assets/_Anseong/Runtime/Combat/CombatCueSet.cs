using System;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 전투 이벤트 → 소리·진동 표(설정 에셋). 소리 파일이 오면 이 에셋의 칸에 넣기만 하면 된다(코드 수정 없음).
    /// 'Anseong → Setup → Rebuild Robot + Pilot Input'이 S-01 이름(SFX_PunchHit 등)으로 기본 줄을 만들고,
    /// Audio 폴더에서 같은 이름으로 시작하는 파일(SFX_PunchHit_01.wav…)을 찾아 채운다. 칸이 비면 그 소리는 무음.
    /// </summary>
    [CreateAssetMenu(menuName = "Anseong/Combat Cue Set", fileName = "CombatCues")]
    public class CombatCueSet : ScriptableObject
    {
        public enum KindFilter { Any, Jab, Hook, Uppercut }

        [Serializable]
        public class Entry
        {
            [Tooltip("어떤 사건에.")]
            public CombatCue cue;

            [Tooltip("펀치 종류가 맞을 때만(Any = 모두).")]
            public KindFilter kind;

            [Tooltip("S-01 소리 이름(예: SFX_PunchHit). 메뉴가 Audio 폴더에서 이 이름으로 시작하는 파일을 찾아 아래 칸을 채운다.")]
            public string soundName;

            [Tooltip("재생할 소리(여러 개면 번갈아). 비면 무음.")]
            public AudioClip[] clips = new AudioClip[0];

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("재생 높이. 06: 사람 크기 소리도 0.6~0.8로 낮춰 거대하게.")]
            [Range(0.3f, 2f)] public float pitch = 0.75f;

            [Tooltip("재생마다 높이를 이만큼(±) 흔들어 반복감을 줄인다.")]
            [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

            [Tooltip("연계 단계마다 높이를 이만큼 올린다(SFX_ComboStep 딩↗).")]
            [Range(0f, 0.5f)] public float pitchPerComboStep;

            [Tooltip("켜면 맞은 자리(보스 쪽)에서 3D로, 끄면 조종석 안에서 몸으로 울리듯 2D로.")]
            public bool atHitPoint;

            [Tooltip("컨트롤러 진동 세기(0 = 없음).")]
            [Range(0f, 1f)] public float hapticAmplitude;

            [Tooltip("컨트롤러 진동 길이(초).")]
            [Range(0f, 1f)] public float hapticSeconds;

            [Tooltip("켜면 사건 번호(Code: 보스 기술 등)가 아래 값일 때만.")]
            public bool matchCode;
            public int code;

            [Tooltip("켜면 사건 수치(Amount: 예고 등급 0 일반·1 강·2 가드 불가 등)를 반올림한 값이 아래 값일 때만.")]
            public bool matchAmount;
            public int amount;

            public bool Matches(CombatEvent e)
            {
                if (e.Cue != cue) return false;
                if (matchCode && e.Code != code) return false;
                if (matchAmount && Mathf.RoundToInt(e.Amount) != amount) return false;
                switch (kind)
                {
                    case KindFilter.Jab: return e.Kind == PunchKind.Jab;
                    case KindFilter.Hook: return e.Kind == PunchKind.Hook;
                    case KindFilter.Uppercut: return e.Kind == PunchKind.Uppercut;
                    default: return true;
                }
            }
        }

        [SerializeField] Entry[] entries = new Entry[0];

        public Entry[] Entries => entries ?? new Entry[0];

        /// <summary>06_feel_and_sound·S-01 표의 기본 줄(메뉴가 새 에셋을 만들 때 쓴다).</summary>
        public static Entry[] DefaultEntries() => new[]
        {
            New(CombatCue.GripLock, KindFilter.Any, "SFX_GripLock", haptic: 0.15f, seconds: 0.03f),
            New(CombatCue.GripRelease, KindFilter.Any, "SFX_GripRelease"),
            New(CombatCue.StrikeLaunch, KindFilter.Jab, "SFX_JabLaunch"),
            New(CombatCue.StrikeLaunch, KindFilter.Hook, "SFX_HookSwing"),
            New(CombatCue.StrikeLaunch, KindFilter.Uppercut, "SFX_HookSwing"),
            New(CombatCue.Hit, KindFilter.Jab, "SFX_PunchHit", atHitPoint: true, haptic: 0.5f, seconds: 0.08f),
            New(CombatCue.Hit, KindFilter.Hook, "SFX_HookHit", atHitPoint: true, haptic: 0.8f, seconds: 0.15f),
            New(CombatCue.Hit, KindFilter.Uppercut, "SFX_HookHit", atHitPoint: true, haptic: 0.7f, seconds: 0.12f),
            New(CombatCue.Hit, KindFilter.Hook, "SFX_BossStagger", atHitPoint: true),
            New(CombatCue.Miss, KindFilter.Any, "SFX_PunchWhoosh"),
            New(CombatCue.ComboStep, KindFilter.Any, "SFX_ComboStep", pitch: 1f, perStep: 0.15f),
            New(CombatCue.BoostReady, KindFilter.Any, "SFX_BoostReady", pitch: 1f),
            New(CombatCue.Boost, KindFilter.Any, "SFX_Boost", haptic: 1f, seconds: 0.25f),
            New(CombatCue.Boost, KindFilter.Any, "SFX_BoostHit", atHitPoint: true),
            // M2-02 보스 공격
            New(CombatCue.Blocked, KindFilter.Any, "SFX_Guard", atHitPoint: true, haptic: 0.3f, seconds: 0.05f),
            WithAmount(New(CombatCue.BossTelegraph, KindFilter.Any, "SFX_WarnNormal", pitch: 1f), 0),
            WithAmount(New(CombatCue.BossTelegraph, KindFilter.Any, "SFX_WarnHeavy", pitch: 1f), 1),
            WithAmount(New(CombatCue.BossTelegraph, KindFilter.Any, "SFX_Siren", pitch: 1f), 2),
            New(CombatCue.BossStrike, KindFilter.Any, "SFX_BossSwing", atHitPoint: true),
            // M2-03 막기·피하기·피격
            New(CombatCue.GuardBlock, KindFilter.Any, "SFX_Guard", haptic: 0.6f, seconds: 0.2f),
            New(CombatCue.GuardFail, KindFilter.Any, "SFX_GuardStrain"),
            New(CombatCue.DodgeStart, KindFilter.Any, "SFX_DodgeSlide"),
            New(CombatCue.Dodge, KindFilter.Any, "SFX_DodgeBurst", pitch: 1f),
            New(CombatCue.PilotHit, KindFilter.Any, "SFX_RobotHit", haptic: 1f, seconds: 0.3f),
            New(CombatCue.PilotDefeated, KindFilter.Any, "SFX_AlarmLowHP", pitch: 1f),
            // M2-04 페이즈·밀려남·사령관 대사(파일이 없으면 무음)
            New(CombatCue.PhaseChanged, KindFilter.Any, "SFX_BossRoar", atHitPoint: true),
            New(CombatCue.BossKnockback, KindFilter.Any, "SFX_BossKnockback", atHitPoint: true),
            Voice(CommanderLines.IncomingAttack), Voice(CommanderLines.CounterNow), Voice(CommanderLines.KneelFinisher),
            Voice(CommanderLines.Victory), Voice(CommanderLines.FirstSlam), Voice(CommanderLines.FirstCombo),
            Voice(CommanderLines.FirstCharge), Voice(CommanderLines.Phase2), Voice(CommanderLines.Phase3), Voice(CommanderLines.FinisherHint),
        };

        static Entry Voice(int line)
        {
            var entry = New(CombatCue.CommanderLine, KindFilter.Any, CommanderLines.SoundName(line), pitch: 1f);
            entry.pitchJitter = 0f;
            entry.matchCode = true;
            entry.code = line;
            return entry;
        }

        static Entry WithAmount(Entry entry, int value)
        {
            entry.matchAmount = true;
            entry.amount = value;
            return entry;
        }

        /// <summary>기본 줄 중 이 표에 없는 것(같은 사건·소리 이름이 없음). 메뉴가 기존 표에 새 줄을 더할 때 쓴다.</summary>
        public Entry[] MissingDefaults()
        {
            var have = Entries;
            var missing = new System.Collections.Generic.List<Entry>();
            foreach (var d in DefaultEntries())
            {
                var found = false;
                foreach (var e in have)
                    if (e != null && e.cue == d.cue && e.soundName == d.soundName && e.kind == d.kind) { found = true; break; }
                if (!found) missing.Add(d);
            }
            return missing.ToArray();
        }

        static Entry New(CombatCue cue, KindFilter kind, string sound, bool atHitPoint = false, float haptic = 0f, float seconds = 0f,
            float pitch = 0.75f, float perStep = 0f)
        {
            return new Entry
            {
                cue = cue, kind = kind, soundName = sound, atHitPoint = atHitPoint,
                hapticAmplitude = haptic, hapticSeconds = seconds, pitch = pitch, pitchPerComboStep = perStep,
            };
        }

        /// <summary>메뉴가 기본 줄을 넣을 때.</summary>
        public void SetEntries(Entry[] value) => entries = value ?? new Entry[0];
    }
}
