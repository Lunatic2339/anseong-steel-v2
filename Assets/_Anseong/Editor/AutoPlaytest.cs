using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Anseong.Boss;
using Anseong.Flow;
using Anseong.Net;
using Anseong.Pilot;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 헤드셋 없이 한 판을 끝까지 돌려 보는 자동 점검(선우 11/12 "자체 테스트해서 스샷이든 영상이든").
    /// Main을 열고 Play → 1인 양팔로 시작 → 장면마다 몇 초 머물며 세 시점(조종사·뒤에서·옆에서)으로 PNG를 찍고
    /// 운영자 "다음 장면"으로 넘긴다(싸움 장면은 보스 무기가 부서짐) → 필살기 준비를 대신 보내 결과판까지.
    /// 결과: 프로젝트 폴더 Logs/Playtest/ 아래 PNG + report.txt(장면 지나간 시각, 오류·예외 로그).
    /// **손 입력은 없다**: 판 흐름·연출·화면 구성이 끝까지 도는지만 본다. 조작감·VR 동작은 사람이 헤드셋으로 확인한다.
    /// 메뉴 'Anseong → Test → Auto Playtest (스크린샷)' 또는 명령줄
    /// Unity.exe -projectPath ... -executeMethod Anseong.EditorTools.AutoPlaytest.RunBatch (끝나면 스스로 종료).
    /// </summary>
    public static class AutoPlaytest
    {
        const string MainScenePath = "Assets/_Anseong/Scenes/Main.unity";
        const float StartDelay = 3f;
        const float Dwell = 5f;          // 장면마다 머무는 시간
        const float ShotEvery = 2.5f;    // 머무는 동안 찍는 간격
        const float Timeout = 420f;
        const int Width = 1920, Height = 1080;

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
            if (t > Timeout)
            {
                report.AppendLine($"시간 초과({Timeout}초). 마지막 장면: {(sawScene ? lastScene.ToString() : "없음")}");
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
                Shot("00_start", t);
                return;
            }

            if (flow == null) flow = UnityEngine.Object.FindAnyObjectByType<MatchFlow>();
            if (combat == null) combat = UnityEngine.Object.FindAnyObjectByType<CombatServer>();
            if (flow == null || !flow.Running) return;

            var scene = flow.Scene;
            if (!sawScene || scene != lastScene)
            {
                sawScene = true;
                lastScene = scene;
                sceneEnter = now;
                lastShot = 0;
                visited.Add(scene);
                report.AppendLine($"{t:F1}s 장면 → {scene}");
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
            if (!timed && scene != MatchScene.Finisher && inScene >= Dwell && now - lastNext >= 2.0)
            {
                lastNext = now;
                Next();
            }
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

        /// <summary>헤드셋이 없으면 XR 카메라가 바닥(0)에 있다 → 앉은 눈높이로 올려 조종사 시점을 흉내 낸다.</summary>
        static void LiftHeadIfNoHeadset()
        {
            var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) return;
            var cam = origin.Camera.transform;
            if (cam.localPosition.sqrMagnitude < 1e-4f) cam.localPosition = new Vector3(0f, 1.25f, 0f);
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
            // 3) 옆에서(두 로봇이 한 화면에)
            var distance = boss != null ? Vector3.Distance(player.position, boss.position) : 80f;
            var side = focus + player.right * Mathf.Max(110f, distance * 0.95f) + Vector3.up * 8f;
            Capture(side, Quaternion.LookRotation(focus - side), 50f, prefix + "_side");
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
            report.AppendLine("오류·예외 로그 " + errorCount + "개, 스크린샷 " + shotIndex * 3 + "장");
            File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString(), Encoding.UTF8);
            if (shotCamera != null) UnityEngine.Object.DestroyImmediate(shotCamera.gameObject);
            if (target != null) target.Release();
            shotCamera = null;
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
