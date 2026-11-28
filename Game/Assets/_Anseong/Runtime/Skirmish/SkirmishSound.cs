using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 전투 소리. 규칙에는 관여하지 않고, 화면과 같이 상태와 그 틱의 사건만 보고 낸다(두 PC에서 따로 돈다).
    /// 자기 탈것의 소리(헬기 로터, 자기 포, 경고음)는 머리 안에서, 나머지는 일어난 자리에서 들린다.
    /// 소리 파일은 Audio/Clips에 있고 장면을 만드는 메뉴가 꽂는다. 비어 있는 칸은 그냥 안 난다.
    /// </summary>
    public class SkirmishSound : MonoBehaviour
    {
        [Serializable]
        public class Cue
        {
            public AudioClip[] clips = new AudioClip[0];
            [Range(0f, 2f)] public float volume = 1f;
            [Range(0.3f, 2f)] public float pitch = 1f;
            public AudioClip Pick(int n) => clips == null || clips.Length == 0 ? null : clips[Mathf.Abs(n) % clips.Length];
        }

        [Header("전체")]
        [SerializeField, Range(0f, 1f)] float master = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("음악 크기. 0 = 끔.")] float musicVolume = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("공장 배경음 크기.")] float ambienceVolume = 0.3f;
        [SerializeField, Min(1f), Tooltip("자리에서 나는 소리가 이 거리(m)까지는 줄지 않는다. 로봇이 35m라 크게 잡는다.")] float nearDistance = 30f;
        [SerializeField, Tooltip("보스전에서만 음악을 튼다.")] bool playMusic = true;

        [Header("계속 나는 소리")]
        public Cue music = new Cue();
        public Cue ambience = new Cue();
        public Cue heliRotor = new Cue();
        public Cue cannonLoop = new Cue();
        public Cue droneLoop = new Cue();
        public Cue jamAlarm = new Cue();

        [Header("로봇")]
        public Cue mechStep = new Cue();
        public Cue mechServo = new Cue();
        public Cue block = new Cue();
        public Cue swordHit = new Cue();
        public Cue fistHit = new Cue();
        public Cue bounce = new Cue();
        public Cue gearBreak = new Cue();
        public Cue mechHit = new Cue();
        public Cue reboot = new Cue();

        [Header("보스")]
        public Cue bossStep = new Cue();
        public Cue bossSwing = new Cue();
        public Cue bossSlamSwing = new Cue();
        public Cue bossThrust = new Cue();
        public Cue bossStagger = new Cue();
        public Cue bossFall = new Cue();
        public Cue heliShot = new Cue();

        [Header("드론·미사일")]
        public Cue explodeSmall = new Cue();
        public Cue explodeLarge = new Cue();
        public Cue missile = new Cue();

        [Header("신호")]
        public Cue telegraph = new Cue();
        public Cue telegraphSpecial = new Cue();
        public Cue actChange = new Cue();
        public Cue weakShown = new Cue();
        public Cue radio = new Cue();

        [Header("간격")]
        [SerializeField, Min(0.1f), Tooltip("보스가 걸을 때 발소리 간격(초).")] float bossStepEvery = 0.9f;
        [SerializeField, Min(0.5f), Tooltip("로봇이 이만큼(m) 움직일 때마다 발소리.")] float mechStepEvery = 7f;
        [SerializeField, Min(0.05f), Tooltip("포 소리가 마지막 발사 뒤 이 시간(초) 동안 이어진다.")] float cannonTail = 0.15f;

        const int Voices = 14;
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource musicSource, ambienceSource, rotorSource, cannonSource, droneSource, alarmSource;
        int next, count, epoch = -1;
        float cannonUntil, bossStepIn, mechWalked;
        Vector3 lastMech, lastBoss;
        GruntStep lastStep;
        bool started;

        public void SetMusic(bool on) => playMusic = on;

        void Build()
        {
            if (started) return;
            started = true;
            for (int i = 0; i < Voices; i++) voices.Add(Source("Voice " + i, false));
            musicSource = Source("Music", true);
            ambienceSource = Source("Ambience", true);
            rotorSource = Source("Heli rotor", true);
            cannonSource = Source("Cannon", true);
            droneSource = Source("Drone", true);
            alarmSource = Source("Jam alarm", true);
        }

        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject("Sound " + name);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = loop;
            a.dopplerLevel = 0f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = nearDistance;
            a.maxDistance = 1500f;
            return a;
        }

        /// <summary>한 번 나는 소리. inHead면 자리 없이 머리 안에서 난다.</summary>
        void Play(Cue cue, Vector3 at, bool inHead, float gain = 1f, float pitch = 1f)
        {
            var clip = cue?.Pick(count++);
            if (clip == null) return;
            var a = voices[next];
            next = (next + 1) % voices.Count;
            a.Stop();
            a.transform.position = at;
            a.spatialBlend = inHead ? 0f : 1f;
            a.clip = clip;
            a.volume = Mathf.Clamp01(cue.volume * gain * master);
            a.pitch = cue.pitch * pitch * UnityEngine.Random.Range(0.96f, 1.04f);
            a.Play();
        }

        /// <summary>계속 나는 소리를 켜고 끈다. 크기는 부드럽게 따라간다.</summary>
        void Loop(AudioSource a, Cue cue, bool on, Vector3 at, bool inHead, float gain, float dt, float fade = 4f)
        {
            var clip = cue?.Pick(0);
            if (clip == null) { if (a.isPlaying) a.Stop(); return; }
            float target = on ? Mathf.Clamp01(cue.volume * gain * master) : 0f;
            if (on && !a.isPlaying) { a.clip = clip; a.volume = 0f; a.pitch = cue.pitch; a.Play(); }
            a.transform.position = at;
            a.spatialBlend = inHead ? 0f : 1f;
            a.volume = Mathf.MoveTowards(a.volume, target, fade * dt);
            if (!on && a.isPlaying && a.volume <= 0.001f) a.Stop();
        }

        /// <summary>이번 프레임의 상태와 그 틱의 사건으로 소리를 낸다. 화면을 그릴 때 같이 부른다.</summary>
        public void Show(SkirmishState st, SkirmishSettings s, IReadOnlyList<SkirmishEvent> events, SkirmishSeat seat, float dt)
        {
            if (st == null || s == null) return;
            Build();
            bool gunner = seat == SkirmishSeat.Gunner;
            bool fighting = st.phase == SkirmishPhase.Fighting;
            if (st.epoch != epoch)
            {
                epoch = st.epoch; lastMech = st.mech; lastBoss = st.gruntPosition; lastStep = st.gruntStep;
                mechWalked = 0f; bossStepIn = 0f;
            }

            // 계속 나는 소리.
            bool boss = SkirmishSettings.IsBoss(s, Mathf.Max(0, st.gruntIndex));
            Loop(ambienceSource, ambience, true, st.mech, true, ambienceVolume, dt, 0.5f);
            // 음악은 보스가 나온 뒤부터(앞 장면에서는 배경음만).
            bool fightStage = st.stage == SceneStage.None || st.stage == SceneStage.Fight;
            Loop(musicSource, music, playMusic && boss && fighting && fightStage, st.mech, true, musicVolume, dt, 0.5f);
            Loop(rotorSource, heliRotor, true, SkirmishSession.GunnerAt(st, s), gunner, gunner ? 0.55f : 1f, dt, 1f);
            Loop(cannonSource, cannonLoop, Time.time < cannonUntil, s.gunnerPost, gunner, gunner ? 0.7f : 1f, dt, 12f);
            Loop(alarmSource, jamAlarm, gunner && st.cannonJam > 0f, s.gunnerPost, true, 1f, dt, 8f);
            // 가장 가까운 드론 하나의 소리만 낸다.
            bool anyDrone = false; var drone = st.mech; float best = float.MaxValue;
            var ear = gunner ? s.gunnerPost : st.mech;
            foreach (var shell in st.shells)
            {
                if (shell.kind != ShellKind.BigShell) continue;
                float d = (shell.position - ear).sqrMagnitude;
                if (d < best) { best = d; drone = shell.position; anyDrone = true; }
            }
            Loop(droneSource, droneLoop, anyDrone, drone, false, 1f, dt, 3f);

            // 걸음: 로봇은 움직인 거리로, 보스는 다가오거나 물러서는 동안 일정한 간격으로.
            mechWalked += Vector3.Distance(st.mech, lastMech);
            lastMech = st.mech;
            if (mechWalked >= mechStepEvery) { mechWalked = 0f; Play(mechStep, st.mech, !gunner, gunner ? 1f : 0.8f); }
            bool bossAlive = st.gruntIndex >= 0 && st.gruntStep != GruntStep.Dead;
            bool bossMoving = bossAlive && Vector3.Distance(st.gruntPosition, lastBoss) > 0.5f * dt;
            lastBoss = st.gruntPosition;
            if (bossMoving)
            {
                bossStepIn -= dt;
                if (bossStepIn <= 0f) { bossStepIn = bossStepEvery; Play(bossStep, st.gruntPosition, false); }
            }
            else bossStepIn = 0f;

            // 보스가 칼을 내리는 순간(예고가 끝나고 치기 시작할 때)의 바람 소리.
            if (bossAlive && st.gruntStep == GruntStep.Strike && lastStep != GruntStep.Strike)
            {
                var at = st.gruntPosition + Vector3.up * 30f;
                switch (st.gruntAttack)
                {
                    case GruntAttack.Thrust: Play(bossThrust, at, false); break;
                    case GruntAttack.Slam:
                    case GruntAttack.BigSlam: Play(bossSlamSwing, at, false, st.gruntAttack == GruntAttack.BigSlam ? 1.3f : 1f); break;
                    case GruntAttack.CallSwarm: break;
                    default: Play(bossSwing, at, false); break;
                }
            }
            lastStep = st.gruntStep;

            if (events == null) return;
            bool brokeNow = false;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Kind)
                {
                    case SkirmishEventKind.CannonShot: cannonUntil = Time.time + cannonTail; break;
                    case SkirmishEventKind.MechGunShot: Play(missile, e.From, !gunner, 0.5f, 1.4f); break;
                    case SkirmishEventKind.GruntTelegraph:
                        Play(e.Id > (int)GruntAttack.Swing ? telegraphSpecial : telegraph, e.From, true);
                        break;
                    case SkirmishEventKind.GruntBlocked: Play(block, e.To, !gunner); break;
                    case SkirmishEventKind.GruntPerfectBlock: Play(block, e.To, !gunner, 1.2f, 1.25f); break;
                    case SkirmishEventKind.ShellBlocked: Play(block, e.To, !gunner, 0.8f); Play(explodeSmall, e.From, false); break;
                    case SkirmishEventKind.MechHit: Play(mechHit, e.To, !gunner); break;
                    case SkirmishEventKind.PunchHit: Play(st.swordBroken ? fistHit : swordHit, e.To, false); break;
                    case SkirmishEventKind.PunchBounced: Play(bounce, e.To, false, 0.7f); break;
                    case SkirmishEventKind.GuardClosed: Play(bounce, e.To, false, 0.6f, 0.8f); break;
                    case SkirmishEventKind.GuardBroken:
                    case SkirmishEventKind.EnemyStaggered: Play(bossStagger, e.To, false); break;
                    case SkirmishEventKind.GruntDown:
                    case SkirmishEventKind.EnemyDown: Play(bossFall, e.To, false); break;
                    case SkirmishEventKind.ShieldBroken:
                    case SkirmishEventKind.SwordBroken:
                        // 방패와 검이 같이 나가면 한 번만 낸다.
                        if (!brokeNow) Play(gearBreak, e.To, !gunner);
                        brokeNow = true;
                        break;
                    case SkirmishEventKind.ActChanged: Play(actChange, e.From, true); break;
                    case SkirmishEventKind.HeliShot:
                        if (e.Id == 1) Play(heliShot, e.To, gunner);
                        else Play(bossSlamSwing, e.To, false, 0.8f);
                        break;
                    case SkirmishEventKind.SwarmCalled: Play(radio, e.From, true); break;
                    case SkirmishEventKind.ThrustPassed: Play(bossSwing, e.To, false, 1f, 0.8f); break;
                    case SkirmishEventKind.SideStep: Play(mechServo, st.mech, !gunner); break;
                    case SkirmishEventKind.ShellIntercepted: Play(explodeSmall, e.To, false); break;
                    case SkirmishEventKind.ShellMissed: Play(explodeLarge, e.To, false, 0.7f); break;
                    case SkirmishEventKind.FurnaceHit: Play(explodeLarge, e.To, false); break;
                    case SkirmishEventKind.MissileFired: Play(missile, e.From, gunner); break;
                    case SkirmishEventKind.MissileHit: Play(explodeLarge, e.To, false); break;
                    case SkirmishEventKind.WeakPointShown: Play(weakShown, e.To, true); break;
                    case SkirmishEventKind.WreckHit: Play(explodeSmall, e.To, false); break;
                    case SkirmishEventKind.StageChanged:
                        Play(e.Id == (int)SceneStage.Entrance ? actChange : radio, e.From, true, e.Id == (int)SceneStage.Entrance ? 0.8f : 1f);
                        break;
                    case SkirmishEventKind.FinaleStarted: Play(actChange, e.From, true); break;
                    case SkirmishEventKind.FinaleCaught: Play(block, e.To, !gunner, 1.3f, 0.8f); break;
                    case SkirmishEventKind.FinaleOpened: Play(explodeLarge, e.From, false, 1.2f); Play(bossStagger, e.From, false); break;
                    case SkirmishEventKind.FinaleBlow:
                        // 주먹과 같이 P2의 미사일이 꽂힌다.
                        Play(missile, s.gunnerPost, gunner);
                        Play(fistHit, e.From, false, 1.3f);
                        Play(explodeLarge, e.From, false, 1.3f, 0.8f);
                        Play(actChange, e.From, true, 0.8f);
                        break;
                    case SkirmishEventKind.Rebooting:
                    case SkirmishEventKind.Failed: Play(reboot, st.mech, true); break;
                }
            }
        }
    }
}
