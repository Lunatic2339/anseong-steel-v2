using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Net;
using Anseong.Pilot;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 헤드셋 없이 한 판을 끝까지 돌려 보는 자동 점검(선우 11/12 "자체 테스트해서 스샷이든 영상이든").
    /// Main을 열고 Play → 1인 양팔로 시작 → 장면마다 몇 초 머물며 세 시점(조종사·뒤에서·옆에서)으로 PNG를 찍고
    /// 운영자 "다음 장면"으로 넘긴다(싸움 장면은 보스 무기가 부서짐) → 필살기 준비를 대신 보내 결과판까지.
    /// 결과: 프로젝트 폴더 Logs/Playtest/ 아래 PNG + report.txt(장면 지나간 시각, 오류·예외 로그).
    /// 손 입력은 봇이 대신 넣는다(PilotHandInput.DevOverride): 보정 2단계 → 보스가 예고하면 가드, 아니면 좌우 번갈아 잽,
    /// 접근 장면은 스틱 앞으로, 드론 장면은 오른손 방아쇠. 판 흐름·연출·화면 구성을 보는 용도이고 조작감·VR 동작은 사람이 헤드셋으로 확인한다.
    /// 메뉴 'Anseong → Test → Auto Playtest (스크린샷)' 또는 명령줄
    /// Unity.exe -projectPath ... -executeMethod Anseong.EditorTools.AutoPlaytest.RunBatch (끝나면 스스로 종료).
    /// </summary>
    public static class AutoPlaytest
    {
        const string MainScenePath = "Assets/_Anseong/Scenes/Main.unity";
        const float StartDelay = 3f;
        const float Dwell = 6f;          // 장면마다 머무는 시간
        const float FightDwell = 26f;    // 싸움 장면(봇이 치고 막는 것을 본다)
        const float CheckupDwell = 16f;  // 점검(보정 + 철판 치기)
        const float ShotEvery = 3f;      // 머무는 동안 찍는 간격
        const float Timeout = 600f;
        // 실전 모드(명령줄 -anseongReal, 선우 11/13 "1분 55초 승리는 볼륨이 작다는 증거"): 장면을 강제로 넘기지 않고 봇이 실제로 끝까지 싸운다.
        // 진짜 판 길이·장면별 시간을 잰다. 한 장면이 RealStuck초 넘게 안 넘어가면 "막힘"으로 적고 그때만 넘긴다.
        const float RealTimeout = 1500f;
        const float RealStuck = 300f;
        static bool real;
        const int Width = 1920, Height = 1080;

        // 영상용 연속 장면(명령줄에 -anseongRecord를 붙이면): 두 로봇을 도는 카메라로 초당 12장 JPG → Logs/Playtest/video/. ffmpeg로 묶는다.
        const float RecordFps = 12f;
        const int RecordWidth = 1280, RecordHeight = 720;
        static bool recording;
        static double lastFrame;
        static int frameIndex;
        static RenderTexture recordTarget;
        static Texture2D recordReadback;
        static string videoDir;

        static bool running, exitWhenDone, begun, finishing;
        static double playStart, sceneEnter, lastShot, lastNext;
        static MatchScene lastScene;
        static bool sawScene;
        static int shotIndex;
        static string outDir;
        static StringBuilder report;
        static int errorCount;
        static EnterPlayModeOptions savedOptions;
        static bool savedOptionsEnabled;
        static PilotCalibrationFlow calibration;
        static BossServer bossServer;
        static Transform trackingSpace, headTransform;
        static double calibratedAt;
        static CalibrationStage lastStage;
        static MatchFlow flow;
        static SessionBootstrap session;
        static CombatServer combat;
        static Camera shotCamera;
        static RenderTexture target;
        static Texture2D readback;
        static readonly HashSet<MatchScene> visited = new HashSet<MatchScene>();

        [MenuItem("Anseong/Test/Auto Playtest (스크린샷)")]
        static void RunFromMenu() => Run(false);

        /// <summary>명령줄용. 끝나면 에디터를 닫는다(오류가 있었으면 종료 코드 3).</summary>
        public static void RunBatch() => Run(true);

        static void Run(bool exit)
        {
            if (running || EditorApplication.isPlaying) return;
            exitWhenDone = exit;
            outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Playtest");
            if (Directory.Exists(outDir))
                foreach (var f in Directory.GetFiles(outDir)) File.Delete(f);
            Directory.CreateDirectory(outDir);
            recording = Array.IndexOf(Environment.GetCommandLineArgs(), "-anseongRecord") >= 0;
            real = Array.IndexOf(Environment.GetCommandLineArgs(), "-anseongReal") >= 0;
            frameIndex = 0;
            videoDir = Path.Combine(outDir, "video");
            if (Directory.Exists(videoDir)) Directory.Delete(videoDir, true);
            if (recording) Directory.CreateDirectory(videoDir);
            report = new StringBuilder();
            report.AppendLine("자동 점검 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " / 그래픽: " + SystemInfo.graphicsDeviceName);
            errorCount = 0;
            shotIndex = 0;
            begun = finishing = sawScene = false;
            visited.Clear();
            if (EditorSceneManager.GetActiveScene().path != MainScenePath)
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            // Play에 들어가도 이 스크립트의 상태가 남도록 도메인 다시 읽기만 잠깐 끈다(끝나면 되돌림).
            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            running = true;
            playStart = -1;
            EditorApplication.isPlaying = true;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errorCount++;
            if (errorCount > 60) return;
            report.AppendLine($"[{type}] {message}");
            if (type == LogType.Exception && !string.IsNullOrEmpty(stack))
                report.AppendLine("    " + stack.Split('\n')[0]);
        }

        static void Tick()
        {
            if (!running) return;
            try
            {
                Step();
            }
            catch (Exception e)
            {
                report.AppendLine("[점검 스크립트 예외] " + e);
                errorCount++;
                Finish();
            }
        }

        static void Step()
        {
            var now = EditorApplication.timeSinceStartup;
            if (finishing)
            {
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                Close();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (playStart < 0) playStart = now;
            var t = now - playStart;
            var limit = real ? RealTimeout : Timeout;
            if (t > limit)
            {
                report.AppendLine($"시간 초과({limit}초). 마지막 장면: {(sawScene ? lastScene.ToString() : "없음")}");
                errorCount++;
                Finish();
                return;
            }
            if (t < StartDelay) return;

            if (!begun)
            {
                begun = true;
                session = UnityEngine.Object.FindAnyObjectByType<SessionBootstrap>();
                if (session == null)
                {
                    report.AppendLine("SessionBootstrap이 씬에 없음(Build Full Game을 먼저).");
                    errorCount++;
                    Finish();
                    return;
                }
                if (!session.Running)
                {
                    var ok = session.Begin(SessionMode.SoloBothArms, string.Empty);
                    report.AppendLine($"{t:F1}s 1인 양팔 시작: {(ok ? "됨" : "실패 " + session.Problem)}");
                }
                LiftHeadIfNoHeadset();
                StartBot();
                Shot("00_start", t);
                return;
            }

            if (flow == null) flow = UnityEngine.Object.FindAnyObjectByType<MatchFlow>();
            if (combat == null) combat = UnityEngine.Object.FindAnyObjectByType<CombatServer>();
            if (flow == null || !flow.Running) return;

            var scene = flow.Scene;
            if (!sawScene || scene != lastScene)
            {
                if (sawScene) report.AppendLine($"    {lastScene} 머문 시간 {now - sceneEnter:F1}초{Situation()}");
                sawScene = true;
                lastScene = scene;
                sceneEnter = now;
                lastShot = 0;
                visited.Add(scene);
                report.AppendLine($"{t:F1}s 장면 → {scene}{Situation()}");
            }
            if (recording && now - lastFrame >= 1.0 / RecordFps)
            {
                lastFrame = now;
                RecordFrame(t);
            }
            var inScene = now - sceneEnter;
            if (now - lastShot >= ShotEvery && inScene >= 1.0)
            {
                lastShot = now;
                Shot(scene.ToString(), t);
            }

            if (scene == MatchScene.Finisher && combat != null)
            {
                // 손이 없으니 필살기 준비를 대신 보낸다.
                combat.SubmitFinisherReady(PilotSide.Left, true);
                combat.SubmitFinisherReady(PilotSide.Right, true);
            }
            if (calibration != null && calibration.Stage != lastStage)
            {
                lastStage = calibration.Stage;
                report.AppendLine($"{t:F1}s 보정 단계 → {lastStage}");
            }
            if (scene == MatchScene.Result)
            {
                if (inScene >= Dwell)
                {
                    report.AppendLine($"{t:F1}s 결과판: {(flow.Result.Won ? "승리" : "패배")}");
                    Finish();
                }
                return;
            }
            // 시간으로 넘어가는 장면은 기다리고, 나머지는 머문 뒤 "다음 장면".
            var timed = flow.Remaining >= 0f;
            var dwell = scene == MatchScene.FightCannon || scene == MatchScene.FightArmor || scene == MatchScene.FightCore ? FightDwell
                : scene == MatchScene.Checkup ? CheckupDwell : Dwell;
            if (real && scene != MatchScene.Waiting)
            {
                if (inScene < RealStuck) return;
                if (now - lastNext >= 2.0) report.AppendLine($"{t:F1}s 막힘: {scene}이(가) {RealStuck}초 동안 스스로 넘어가지 않아 강제로 넘김{Situation()}");
                dwell = RealStuck;
            }
            if (!timed && scene != MatchScene.Finisher && inScene >= dwell && now - lastNext >= 2.0)
            {
                lastNext = now;
                Next();
            }
        }

        /// <summary>보고서용: 보스 거리·HP·상태.</summary>
        static string Situation()
        {
            var brain = UnityEngine.Object.FindAnyObjectByType<BossBrain>();
            var seat = UnityEngine.Object.FindAnyObjectByType<CockpitSeat>();
            if (brain == null || seat == null) return string.Empty;
            var text = $" (보스 거리 {Vector3.Distance(brain.transform.position, seat.transform.root.position):F0} m";
            if (combat != null && combat.Authority != null)
            {
                var hp = combat.Authority.Resolver.Boss;
                text += $", 보스 HP {hp.Current:F0}/{hp.Max:F0}";
            }
            if (bossServer != null) text += $", 보스 상태 {bossServer.Action.BossState}";
            return text + ")";
        }

        // ── 손 입력 봇 ──────────────────────────────────────────────
        static void StartBot()
        {
            calibration = UnityEngine.Object.FindAnyObjectByType<PilotCalibrationFlow>();
            bossServer = UnityEngine.Object.FindAnyObjectByType<BossServer>();
            var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null || calibration == null)
            {
                report.AppendLine("손 입력 봇: XR Origin 또는 보정 컴포넌트가 없어 손 입력 없이 돈다.");
                return;
            }
            headTransform = origin.Camera.transform;
            trackingSpace = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : headTransform.parent;
            calibratedAt = -1;
            lastStage = calibration.Stage;
            PilotHandInput.DevOverride = BotHand;
            report.AppendLine("손 입력 봇 켬(보정 → 가드·잽).");
        }

        static void StopBot()
        {
            PilotHandInput.DevOverride = null;
            calibration = null;
            bossServer = null;
            trackingSpace = headTransform = null;
            drones = null;
            openingStart = -1;
        }

        static Vector3 HeadLocal()
        {
            if (headTransform == null) return new Vector3(0f, 1.25f, 0f);
            return trackingSpace != null ? trackingSpace.InverseTransformPoint(headTransform.position) : headTransform.localPosition;
        }

        static readonly Quaternion GuardRotation = Quaternion.Euler(-78f, 0f, 0f);
        const double PunchPeriod = 2.6;   // 두 손이 한 번씩 치는 주기(손마다 1.3초 간격)

        static PilotHandInput.DevHand? BotHand(PilotSide side)
        {
            if (calibration == null) return null;
            var time = Time.timeAsDouble;
            var head = HeadLocal();
            var sx = side == PilotSide.Left ? -1f : 1f;
            var reach = head + new Vector3(sx * 0.20f, -0.25f, 0.62f);
            var guard = head + new Vector3(sx * 0.14f, -0.12f, 0.28f);
            var hand = new PilotHandInput.DevHand { Position = guard, Rotation = GuardRotation };
            // 0.5초 쥐고 0.5초 놓기(보정 저장용 박자).
            var pulse = time % 1.0 < 0.5;

            switch (calibration.Stage)
            {
                case CalibrationStage.NotStarted:
                    return hand;
                case CalibrationStage.Shoulder:
                    hand.Position = head + new Vector3(sx * 0.2f, -0.75f, 0.05f);
                    hand.Rotation = Quaternion.Euler(80f, 0f, 0f);
                    hand.Grip = pulse ? 1f : 0f;
                    return hand;
                case CalibrationStage.Reach:
                    hand.Position = reach;
                    hand.Rotation = Quaternion.identity;
                    hand.Grip = pulse ? 1f : 0f;
                    calibratedAt = -1;
                    return hand;
                case CalibrationStage.Guard:
                    hand.Grip = pulse ? 1f : 0f;
                    calibratedAt = -1;
                    return hand;
            }

            // 보정 끝: 한 번 놓았다가 계속 쥔다.
            if (calibratedAt < 0) calibratedAt = time;
            var since = time - calibratedAt;
            if (since < 0.8) return hand;
            hand.Grip = 1f;

            var scene = flow != null && flow.Running ? flow.Scene : MatchScene.Waiting;
            // 걷지 않는다: 보스가 근접 거리(30 m)까지 걸어온다. 우리가 걸어 들어가면 26 m까지 붙어 두 로봇이 겹쳐 보인다.
            if (scene == MatchScene.DroneHunt)
            {
                // 드론 사냥(11/13 run12): 왼손 스캐너로 숨은 드론을 비춰 드러내고, 오른손 포로 드러난 드론을 겨눠 쏜다.
                // 팔(어깨→손) 방향이 무기 방향이므로 손 자리를 드론 쪽으로 뻗고, 손목도 같은 쪽으로 돌린다.
                var shoulder = head + new Vector3(sx * 0.2f, -0.25f, 0f);
                var aim = AimAtDrone(side);
                hand.Position = shoulder + aim * 0.65f;
                hand.Rotation = Quaternion.FromToRotation(Vector3.forward, aim);
                if (side == PilotSide.Right) hand.Trigger = time % 0.6 < 0.3 ? 1f : 0f;
                return hand;
            }
            aimDirection[(int)side] = Vector3.forward;
            var state = bossServer != null ? bossServer.Action.BossState : BossState.Rest;
            var fighting = scene == MatchScene.Checkup || scene == MatchScene.FightCannon || scene == MatchScene.FightArmor || scene == MatchScene.FightCore;
            if (state == BossState.Telegraph || state == BossState.Strike)
            {
                Defend(ref hand, side, head, time);
                return hand;
            }
            // 평소에는 주먹을 가슴 아래 준비 자세로 내린다(가드를 계속 들고 있으면 거대한 주먹이 조종석 시야 위쪽을 가린다, 11/13). 가드는 예고·명중 때만.
            var ready = head + new Vector3(sx * 0.2f, -0.42f, 0.3f);
            var readyRotation = Quaternion.Euler(-35f, 0f, 0f);
            hand.Position = ready;
            hand.Rotation = readyRotation;
            if (!fighting) return hand;

            // 빈틈·경직: 좌우 번갈아 0.5초마다 한 번씩 빠르게 친다(연계 제한 2.4초·최소 간격 0.15초 안, 11/13 run12 "최고 연계 1").
            // 헛친 주먹은 연계를 끊으므로 두 손 다 몸통 가운데로 뻗는다(왼손이 바깥으로 빗나가 매번 끊겼다).
            if (state == BossState.Opening || state == BossState.Staggered)
            {
                reach = head + new Vector3(sx * 0.05f, -0.22f, 0.66f);
                if (openingStart < 0) openingStart = time;
                var t = time - openingStart;
                var slot = (int)(t / ComboBeat);
                if ((slot % 2 == 0) == (side == PilotSide.Left))
                {
                    var p = t - slot * ComboBeat;
                    var kk = p < 0.12 ? (float)(p / 0.12) : p < 0.32 ? 1f : p < 0.45 ? 1f - (float)((p - 0.32) / 0.13) : 0f;
                    kk = Mathf.SmoothStep(0f, 1f, kk);
                    hand.Position = Vector3.Lerp(ready, reach, kk);
                    hand.Rotation = Quaternion.Slerp(readyRotation, Quaternion.identity, Mathf.Clamp01(kk * 2.5f));
                }
                return hand;
            }
            openingStart = -1;

            // 잽: 0.18초 뻗기 → 0.5초 버티기(로봇 팔이 따라올 시간) → 0.25초 거두기. 왼손 먼저, 오른손은 반 주기 뒤.
            var phase = (since + (side == PilotSide.Right ? PunchPeriod * 0.5 : 0.0)) % PunchPeriod;
            float k;
            if (phase < 0.18) k = (float)(phase / 0.18);
            else if (phase < 0.68) k = 1f;
            else if (phase < 0.93) k = 1f - (float)((phase - 0.68) / 0.25);
            else k = 0f;
            k = Mathf.SmoothStep(0f, 1f, k);
            hand.Position = Vector3.Lerp(ready, reach, k);
            hand.Rotation = Quaternion.Slerp(readyRotation, Quaternion.identity, Mathf.Clamp01(k * 2.5f));
            return hand;
        }

        const double ComboBeat = 0.5;
        static double openingStart = -1;
        static readonly Vector3[] aimDirection = { Vector3.forward, Vector3.forward };
        static DroneDirector drones;
        static System.Reflection.FieldInfo droneNet;

        /// <summary>
        /// 목표 드론 방향(추적 공간)을 돌려준다. 손을 어깨에서 이 방향으로 뻗으면 팔뚝 무기(스캐너·포)가 드론을 향한다.
        /// 왼손 = 숨은 드론(스캐너로 드러냄), 오른손 = 드러난 드론(포로 격추).
        /// </summary>
        static Vector3 AimAtDrone(PilotSide side)
        {
            var i = (int)side;
            if (drones == null) drones = UnityEngine.Object.FindAnyObjectByType<DroneDirector>();
            var weapons = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponVisuals>();
            if (drones == null || weapons == null || trackingSpace == null) return aimDirection[i];
            var tip = side == PilotSide.Left ? weapons.ScannerTip : weapons.CannonTip;
            if (tip == null || !tip.gameObject.activeInHierarchy) return aimDirection[i];
            if (droneNet == null) droneNet = typeof(DroneDirector).GetField("net", BindingFlags.Instance | BindingFlags.NonPublic);
            if (droneNet == null || !(droneNet.GetValue(drones) is Unity.Netcode.NetworkVariable<DroneNetState> net)) return aimDirection[i];
            var s = net.Value;
            var want = side == PilotSide.Left ? DroneState.Hidden : DroneState.Revealed;
            var best = -1;
            var bestAngle = float.MaxValue;
            for (var d = 0; d < DroneDirector.Slots; d++)
            {
                if (s.State(d) != want) continue;
                var angle = Vector3.Angle(tip.up, s.Position(d) - tip.position);
                if (angle < bestAngle) { bestAngle = angle; best = d; }
            }
            if (best < 0) return aimDirection[i];
            // 목표 방향(추적 공간)으로 천천히 돌린다(로봇 팔이 묵직하게 따라오므로 손을 홱 돌리지 않는다).
            var want3 = Quaternion.Inverse(trackingSpace.rotation) * (s.Position(best) - tip.position).normalized;
            aimDirection[i] = Vector3.Slerp(aimDirection[i], want3, 0.1f).normalized;
            return aimDirection[i];
        }

        /// <summary>
        /// 예고·명중 동안의 대응: 가드 기술은 가드(왼팔 방패는 들어오는 쪽 얼굴 앞으로 옮김), 회피 기술은 두 스틱을 같이 옆으로 짧게 반복해 민다
        /// (회피는 명중 0.6초 안에 새로 민 것만 인정되므로 0.4초마다 다시 민다).
        /// </summary>
        static void Defend(ref PilotHandInput.DevHand hand, PilotSide side, Vector3 head, double time)
        {
            var action = bossServer.Action;
            if (!action.HasAttack) return;
            var spec = bossServer.Machine != null && bossServer.Machine.Attack != null ? bossServer.Machine.Attack : BossAttackSpec.Default(action.AttackKind);
            if (spec == null) return;
            if (spec.counter == BossCounter.Dodge)
            {
                hand.Stick = time % 0.4 < 0.25 ? new Vector2(1f, 0f) : Vector2.zero;
                return;
            }
            if (side != PilotSide.Left || spec.counter == BossCounter.BothGuard) return;
            var elapsed = bossServer.Now - action.Start + action.Lead;
            var upcoming = elapsed < spec.telegraphSeconds ? 0
                : Mathf.Clamp((int)((elapsed - spec.telegraphSeconds) / Mathf.Max(0.05f, spec.hitIntervalSeconds)) + 1, 0, spec.HitCount - 1);
            if (spec.SideOf(upcoming) == PilotSide.Right) hand.Position = head + new Vector3(0.07f, -0.12f, 0.28f);
        }

        static void Next()
        {
            var send = typeof(MatchFlow).GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic);
            var command = typeof(MatchFlow).GetNestedType("Command", BindingFlags.NonPublic);
            if (send == null || command == null)
            {
                report.AppendLine("MatchFlow.Send를 찾지 못함");
                errorCount++;
                Finish();
                return;
            }
            // Waiting이면 시작, 아니면 다음 장면.
            var value = flow.Scene == MatchScene.Waiting ? 0 : 1;
            send.Invoke(flow, new[] { Enum.ToObject(command, value) });
        }

        const float SeatedEyeHeight = 1.2f;

        /// <summary>
        /// 헤드셋이 없으면 XR 카메라가 추적 원점(0)에 있다 → 앉은 눈높이로 맞춰 조종사 시점을 흉내 낸다.
        /// Camera Offset에 높이가 들어 있으면(기기 기준 원점일 때 XR Origin이 넣는 값) 그만큼 빼서, XR Origin 바닥에서 눈까지가 앉은 키가 되게 한다
        /// (안 빼면 눈이 창틀 높이까지 올라가 창틀·팔이 화면을 가린다, 11/13).
        /// </summary>
        static void LiftHeadIfNoHeadset()
        {
            var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) return;
            var cam = origin.Camera.transform;
            if (cam.localPosition.sqrMagnitude >= 1e-4f) return;
            var offset = origin.CameraFloorOffsetObject != null ? origin.transform.InverseTransformPoint(origin.CameraFloorOffsetObject.transform.position).y : 0f;
            cam.localPosition = new Vector3(0f, SeatedEyeHeight - offset, 0f);
            report.AppendLine($"헤드셋 없음: 눈높이 {SeatedEyeHeight:F2} m로 맞춤(Camera Offset 높이 {offset:F2} m)");
        }

        static void Shot(string label, double t)
        {
            var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            var main = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            if (main == null) return;
            EnsureCamera(main);
            var seat = UnityEngine.Object.FindAnyObjectByType<CockpitSeat>();
            var player = seat != null ? seat.transform.root : main.transform.root;
            var bossBrain = UnityEngine.Object.FindAnyObjectByType<BossBrain>();
            var boss = bossBrain != null ? bossBrain.transform : null;
            shotIndex++;
            var prefix = $"{shotIndex:000}_{label}";

            // 1) 조종사 시점
            Capture(main.transform.position, main.transform.rotation, main.fieldOfView > 1f ? Mathf.Max(main.fieldOfView, 75f) : 75f, prefix + "_pilot");

            var focus = boss != null ? Vector3.Lerp(player.position, boss.position, 0.5f) + Vector3.up * 35f : player.position + player.forward * 60f + Vector3.up * 35f;
            // 2) 뒤에서(우리 로봇 어깨 너머)
            var back = player.position - player.forward * 55f + player.right * 28f + Vector3.up * 62f;
            Capture(back, Quaternion.LookRotation(focus - back), 50f, prefix + "_chase");
            // 3) 옆에서(두 로봇이 한 화면에). 두 번에 한 번만.
            if (shotIndex % 2 == 0) return;
            var distance = boss != null ? Vector3.Distance(player.position, boss.position) : 80f;
            var side = focus + player.right * Mathf.Max(110f, distance * 0.95f) + Vector3.up * 8f;
            Capture(side, Quaternion.LookRotation(focus - side), 50f, prefix + "_side");
        }

        /// <summary>영상 한 장: 두 로봇 가운데를 보며 천천히 도는 카메라(우리 로봇 뒤 오른쪽에서 옆까지 왔다 갔다).</summary>
        static void RecordFrame(double t)
        {
            var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            var main = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            if (main == null) return;
            EnsureCamera(main);
            if (recordTarget == null)
            {
                recordTarget = new RenderTexture(RecordWidth, RecordHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
                recordReadback = new Texture2D(RecordWidth, RecordHeight, TextureFormat.RGB24, false);
            }
            var seat = UnityEngine.Object.FindAnyObjectByType<CockpitSeat>();
            var player = seat != null ? seat.transform.root : main.transform.root;
            var bossBrain = UnityEngine.Object.FindAnyObjectByType<BossBrain>();
            var boss = bossBrain != null ? bossBrain.transform : null;
            var distance = boss != null ? Vector3.Distance(player.position, boss.position) : 60f;
            var focus = (boss != null ? Vector3.Lerp(player.position, boss.position, 0.5f) : player.position + player.forward * 30f) + Vector3.up * 36f;
            // 뒤쪽 오른편(150°)에서 옆(80°) 사이를 느리게 오간다. 0° = 우리 로봇 정면 방향.
            var angle = Mathf.Lerp(80f, 150f, 0.5f + 0.5f * Mathf.Sin((float)t * 0.12f));
            var radius = Mathf.Max(85f, distance * 0.5f + 70f);
            var direction = Quaternion.AngleAxis(angle, Vector3.up) * player.forward;
            var position = focus + direction * radius + Vector3.up * 14f;
            shotCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
            shotCamera.fieldOfView = 48f;
            shotCamera.targetTexture = recordTarget;
            shotCamera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = recordTarget;
            recordReadback.ReadPixels(new Rect(0, 0, RecordWidth, RecordHeight), 0, 0);
            recordReadback.Apply(false);
            RenderTexture.active = previous;
            shotCamera.targetTexture = null;
            frameIndex++;
            File.WriteAllBytes(Path.Combine(videoDir, $"f{frameIndex:00000}.jpg"), recordReadback.EncodeToJPG(82));
        }

        static void EnsureCamera(Camera main)
        {
            if (shotCamera != null) return;
            var go = new GameObject("AutoPlaytest Camera") { hideFlags = HideFlags.HideAndDontSave };
            shotCamera = go.AddComponent<Camera>();
            shotCamera.CopyFrom(main);
            shotCamera.stereoTargetEye = StereoTargetEyeMask.None;
            shotCamera.enabled = false;
            shotCamera.nearClipPlane = 0.05f;
            // 후처리(블룸·톤)도 같이 찍는다.
            shotCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        }

        static void Capture(Vector3 position, Quaternion rotation, float fov, string name)
        {
            shotCamera.transform.SetPositionAndRotation(position, rotation);
            shotCamera.fieldOfView = fov;
            shotCamera.targetTexture = target;
            shotCamera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = previous;
            shotCamera.targetTexture = null;
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), readback.EncodeToPNG());
        }

        static void Finish()
        {
            if (finishing) return;
            finishing = true;
            var all = (MatchScene[])Enum.GetValues(typeof(MatchScene));
            var missed = new List<string>();
            foreach (var s in all)
                if (!visited.Contains(s) && s != MatchScene.Rebooting && s != MatchScene.Opening && s != MatchScene.Ending) missed.Add(s.ToString());
            report.AppendLine("지나간 장면 " + visited.Count + "개. 안 지나간 장면: " + (missed.Count > 0 ? string.Join(", ", missed) : "없음"));
            report.AppendLine("오류·예외 로그 " + errorCount + "개, 스크린샷 " + Directory.GetFiles(outDir, "*.png").Length + "장");
            File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString(), Encoding.UTF8);
            if (shotCamera != null) UnityEngine.Object.DestroyImmediate(shotCamera.gameObject);
            if (target != null) target.Release();
            if (recordTarget != null) recordTarget.Release();
            recordTarget = null;
            if (recording) report.AppendLine($"영상 장면 {frameIndex}장({RecordFps}fps) → {videoDir}. 묶기: ffmpeg -framerate {RecordFps} -i f%05d.jpg -c:v libx264 -pix_fmt yuv420p out.mp4");
            shotCamera = null;
            StopBot();
            flow = null;
            combat = null;
            session = null;
            EditorApplication.isPlaying = false;
        }

        static void Close()
        {
            running = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorSettings.enterPlayModeOptions = savedOptions;
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            Debug.Log($"[AutoPlaytest] 끝. 결과: {outDir} (오류 {errorCount}개)");
            if (exitWhenDone) EditorApplication.Exit(errorCount > 0 ? 3 : 0);
        }
    }
}
