using System;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>
    /// 한 판의 장면(10_game_scenario.md 장면 0~10). 숫자가 네트워크로 복제되므로 순서를 바꾸지 말고 끝에만 더한다.
    /// </summary>
    public enum MatchScene : byte
    {
        /// <summary>장면 0: 조종석 대기, 접속 기다림.</summary>
        Waiting,
        /// <summary>장면 1: 기체 점검(정비고에서 무장 달기). 내용은 M4-05.</summary>
        Checkup,
        /// <summary>장면 2: 마당 등장(보스 멀리, 10초).</summary>
        Entrance,
        /// <summary>장면 3: 숨은 드론 사냥. 드론은 M4-04, 그 전엔 시간으로 넘어간다.</summary>
        DroneHunt,
        /// <summary>장면 4: 접근. 보스가 근접 거리에 닿으면 근접전.</summary>
        Approach,
        /// <summary>장면 4-1·4-2: 근접전, 목표 = 보스 팔뚝 포.</summary>
        FightCannon,
        /// <summary>장면 5: 첫 상실(우리 포), 8초.</summary>
        LostGun,
        /// <summary>장면 6: 검·방패만, 목표 = 보스 방패(11/11 확정, 보스 검도 같이 부서짐).</summary>
        FightArmor,
        /// <summary>장면 7: 두 번째 상실(검·방패), 8초.</summary>
        LostSword,
        /// <summary>장면 8: 맨손, 목표 = 드러난 핵.</summary>
        FightCore,
        /// <summary>장면 9: 필살기(보스 무릎).</summary>
        Finisher,
        /// <summary>장면 10: 결과판.</summary>
        Result,
        /// <summary>패배 → 기체 재기동(그 구간 처음부터 1회).</summary>
        Rebooting,
    }

    /// <summary>판 흐름을 움직이는 신호. 서버의 <see cref="MatchFlow"/>가 전투 사건·운영자 조작을 이것으로 바꿔 넣는다.</summary>
    public enum MatchSignal
    {
        /// <summary>운영자 시작(또는 조종사가 다 들어옴).</summary>
        Start,
        /// <summary>기체 점검 끝(지금은 둘이 같이 스틱 앞으로 = 출격).</summary>
        CheckupDone,
        /// <summary>드론 전멸(M4-04).</summary>
        DronesCleared,
        /// <summary>보스가 근접 거리에 닿음.</summary>
        ReachedMelee,
        /// <summary>지금 차례 보스 무기가 부서짐(M4-02 전에는 보스 HP 페이즈 전환이 대신 보냄).</summary>
        BossWeaponBroken,
        /// <summary>보스 무릎(핵 부서짐) → 필살기.</summary>
        BossKneel,
        /// <summary>보스 쓰러짐 → 승리 결과.</summary>
        BossDefeated,
        /// <summary>기체 HP 0.</summary>
        PilotDefeated,
        /// <summary>운영자 "다음 장면"(시험용). 싸움 장면에서는 흐름이 직접 넘기지 않는다(<see cref="MatchFlowRules.IsFight"/>).</summary>
        Skip,
    }

    [Serializable]
    public class MatchFlowSettings
    {
        [Tooltip("장면 2 마당 등장 길이(초, 대본 10초).")]
        [Min(0f)] public float entranceSeconds = 10f;

        [Tooltip("드론(M4-04)이 생기기 전 장면 3을 이 시간(초) 뒤 넘긴다. 0이면 드론 전멸 신호만 기다린다.")]
        [Min(0f)] public float droneHuntPlaceholderSeconds = 8f;

        [Tooltip("상실 장면(5·7) 길이(초, 대본 8초).")]
        [Min(0f)] public float lossSeconds = 8f;

        [Tooltip("패배 뒤 기체 재기동 연출 길이(초).")]
        [Min(0f)] public float rebootSeconds = 4f;

        [Tooltip("결과판 보여 주는 시간(초, 대본 15초). 지나면 '자동 재시작'이 켜져 있을 때만 대기로.")]
        [Min(1f)] public float resultSeconds = 15f;

        [Tooltip("한 판에 재기동 허용 횟수(대본: 1회, 두 번째 패배면 결과판).")]
        [Min(0)] public int rebootsAllowed = 1;
    }

    /// <summary>
    /// 한 판 흐름(M4-01, 10_game_scenario.md). 대기 → 기체 점검 → 등장 → 드론 → 접근 → 근접전 3구간(사이에 상실 2번) → 필살기 → 결과.
    /// 패배하면 재기동 후 그 구간 처음부터 1회, 두 번째면 결과판. 순수 클래스: 시각은 서버 시각을 받는다.
    /// 장면이 바뀌면 true를 돌려주고, 들어갈 때 할 일(보스 멈춤, 대사, HP 회복)은 <see cref="MatchFlow"/>가 한다.
    /// </summary>
    public sealed class MatchFlowRules
    {
        readonly MatchFlowSettings settings;

        public MatchFlowRules(MatchFlowSettings settings = null)
        {
            this.settings = settings ?? new MatchFlowSettings();
        }

        public MatchFlowSettings Settings => settings;
        public MatchScene Scene { get; private set; } = MatchScene.Waiting;
        public MatchScene Previous { get; private set; } = MatchScene.Waiting;
        public double SceneStart { get; private set; }
        /// <summary>재기동이 끝나면 돌아갈 구간.</summary>
        public MatchScene ResumeScene { get; private set; } = MatchScene.Waiting;
        public int RebootsUsed { get; private set; }
        /// <summary>결과판일 때: 이겼는지.</summary>
        public bool Won { get; private set; }

        /// <summary>근접전 3구간(보스 무기를 부숴야 넘어가는 장면).</summary>
        public static bool IsFight(MatchScene scene) =>
            scene == MatchScene.FightCannon || scene == MatchScene.FightArmor || scene == MatchScene.FightCore;

        /// <summary>보스가 걸어와도 되는 장면.</summary>
        public static bool BossMayMove(MatchScene scene) => scene == MatchScene.Approach || IsFight(scene);

        /// <summary>보스가 공격해도 되는 장면.</summary>
        public static bool BossMayAttack(MatchScene scene) => IsFight(scene);

        /// <summary>시간으로 끝나는 장면의 길이(초). 시간으로 안 끝나면 0.</summary>
        public float SceneSeconds(MatchScene scene)
        {
            switch (scene)
            {
                case MatchScene.Entrance: return settings.entranceSeconds;
                case MatchScene.DroneHunt: return settings.droneHuntPlaceholderSeconds;
                case MatchScene.LostGun:
                case MatchScene.LostSword: return settings.lossSeconds;
                case MatchScene.Rebooting: return settings.rebootSeconds;
                case MatchScene.Result: return settings.resultSeconds;
                default: return 0f;
            }
        }

        /// <summary>처음(대기)으로. 이전 판 상태를 모두 지운다.</summary>
        public void Reset(double now)
        {
            Scene = Previous = ResumeScene = MatchScene.Waiting;
            SceneStart = now;
            RebootsUsed = 0;
            Won = false;
        }

        /// <summary>서버 프레임마다: 시간으로 넘어가는 장면. 결과판은 시간이 지나도 머문다(<see cref="ResultExpired"/>).</summary>
        public bool Update(double now)
        {
            var seconds = SceneSeconds(Scene);
            if (seconds <= 0f || Scene == MatchScene.Result || now - SceneStart < seconds) return false;
            switch (Scene)
            {
                case MatchScene.Entrance: return Go(MatchScene.DroneHunt, now);
                case MatchScene.DroneHunt: return Go(MatchScene.Approach, now);
                case MatchScene.LostGun: return Go(MatchScene.FightArmor, now);
                case MatchScene.LostSword: return Go(MatchScene.FightCore, now);
                case MatchScene.Rebooting: return Go(ResumeScene, now);
                default: return false;
            }
        }

        /// <summary>결과판 시간이 다 지났는지(자동 재시작용).</summary>
        public bool ResultExpired(double now) => Scene == MatchScene.Result && now - SceneStart >= settings.resultSeconds;

        public bool Signal(MatchSignal signal, double now)
        {
            switch (signal)
            {
                case MatchSignal.Start:
                    return Scene == MatchScene.Waiting && Go(MatchScene.Checkup, now);
                case MatchSignal.CheckupDone:
                    return Scene == MatchScene.Checkup && Go(MatchScene.Entrance, now);
                case MatchSignal.DronesCleared:
                    return Scene == MatchScene.DroneHunt && Go(MatchScene.Approach, now);
                case MatchSignal.ReachedMelee:
                    return Scene == MatchScene.Approach && Go(MatchScene.FightCannon, now);
                case MatchSignal.BossWeaponBroken:
                    if (Scene == MatchScene.FightCannon) return Go(MatchScene.LostGun, now);
                    if (Scene == MatchScene.FightArmor) return Go(MatchScene.LostSword, now);
                    return false;
                case MatchSignal.BossKneel:
                    return Scene == MatchScene.FightCore && Go(MatchScene.Finisher, now);
                case MatchSignal.BossDefeated:
                    if (Scene == MatchScene.Result) return false;
                    Won = true;
                    return Go(MatchScene.Result, now);
                case MatchSignal.PilotDefeated:
                    return Defeated(now);
                case MatchSignal.Skip:
                    return Skip(now);
                default:
                    return false;
            }
        }

        bool Defeated(double now)
        {
            if (Scene == MatchScene.Result || Scene == MatchScene.Rebooting || Scene == MatchScene.Waiting || Scene == MatchScene.Checkup) return false;
            if (RebootsUsed < settings.rebootsAllowed)
            {
                RebootsUsed++;
                ResumeScene = Scene;
                return Go(MatchScene.Rebooting, now);
            }
            Won = false;
            return Go(MatchScene.Result, now);
        }

        /// <summary>시험용 "다음 장면". 싸움 장면은 보스 무기를 부숴야 넘어가므로 여기서는 안 넘긴다(<see cref="MatchFlow"/>가 무기를 부순다).</summary>
        bool Skip(double now)
        {
            switch (Scene)
            {
                case MatchScene.Waiting: return Go(MatchScene.Checkup, now);
                case MatchScene.Checkup: return Go(MatchScene.Entrance, now);
                case MatchScene.Entrance: return Go(MatchScene.DroneHunt, now);
                case MatchScene.DroneHunt: return Go(MatchScene.Approach, now);
                case MatchScene.Approach: return Go(MatchScene.FightCannon, now);
                case MatchScene.LostGun: return Go(MatchScene.FightArmor, now);
                case MatchScene.LostSword: return Go(MatchScene.FightCore, now);
                case MatchScene.Rebooting: return Go(ResumeScene, now);
                default: return false;
            }
        }

        bool Go(MatchScene next, double now)
        {
            Previous = Scene;
            Scene = next;
            SceneStart = now;
            return true;
        }
    }
}
