using System.Linq;
using Anseong.Combat;
using Anseong.Net;
using Anseong.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M2-01·M3-01: 열린 씬에 네트워크 세션(NetworkManager + UnityTransport + SessionBootstrap)과 전투 서버(NetworkObject + CombatServer)를 붙인다.
    /// 여러 번 실행해도 된다(있으면 연결만 다시). Rebuild Robot + Pilot Input, Build M1 Main도 이것을 부른다. 씬 저장은 사람이 한다.
    /// NGO 씬 관리는 끈다: 두 PC가 같은 씬을 각자 열고 접속한다(맵 씬을 추가로 불러오는 EnvironmentLoader와 부딪히지 않게).
    /// </summary>
    public static class NetworkSessionMenu
    {
        const string LogPrefix = "[NetworkSessionMenu]";
        const string SessionName = "Network Session";
        const string ServerName = "Combat Server";

        [MenuItem("Anseong/Setup/Add Network Session", priority = 2)]
        static void AddToActiveScene()
        {
            var result = AddToScene(SceneManager.GetActiveScene());
            Debug.Log($"{LogPrefix} {result}. 씬을 저장하세요.");
        }

        /// <summary>씬에 세션과 전투 서버를 붙인다. 무엇을 했는지 한 줄로 돌려준다.</summary>
        public static string AddToScene(Scene scene)
        {
            var network = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).FirstOrDefault();
            var made = network == null;
            if (made)
            {
                var go = new GameObject(SessionName);
                Undo.RegisterCreatedObjectUndo(go, "Add Network Session");
                SceneManager.MoveGameObjectToScene(go, scene);
                network = go.AddComponent<NetworkManager>();
            }
            var host = network.gameObject;
            var transport = host.GetComponent<UnityTransport>();
            if (transport == null) transport = Undo.AddComponent<UnityTransport>(host);
            Undo.RecordObject(network, "Add Network Session");
            if (network.NetworkConfig == null) network.NetworkConfig = new NetworkConfig();
            network.NetworkConfig.NetworkTransport = transport;
            network.NetworkConfig.EnableSceneManagement = false;
            EditorUtility.SetDirty(network);
            if (host.GetComponent<SessionBootstrap>() == null) Undo.AddComponent<SessionBootstrap>(host);
            // M3-01: PC 모니터용 운영자 시작 화면(1인 양팔 / 호스트 / 클라이언트 + IP).
            if (host.GetComponent<OperatorStartScreen>() == null) Undo.AddComponent<OperatorStartScreen>(host);

            var server = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatServer>(true)).FirstOrDefault();
            if (server == null)
            {
                var go = new GameObject(ServerName);
                Undo.RegisterCreatedObjectUndo(go, "Add Network Session");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<NetworkObject>();
                server = go.AddComponent<CombatServer>();
            }
            // M3-03: 협동 이동(서버가 정하고 모든 PC가 같은 곡선으로 재생). 같은 NetworkObject에 붙인다.
            if (server.GetComponent<RobotMotionSync>() == null) Undo.AddComponent<RobotMotionSync>(server.gameObject);
            var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatDirector>(true)).FirstOrDefault();
            var serialized = new SerializedObject(server);
            serialized.FindProperty("director").objectReferenceValue = director;
            serialized.ApplyModifiedProperties();
            if (director == null) Debug.LogWarning($"{LogPrefix} 씬에 CombatDirector가 없습니다. Rebuild Robot + Pilot Input을 먼저 실행하세요.");

            // M3-02: 상대 조종사 팔·머리·양손 동기화. 같은 NetworkObject에 붙인다(두 PC 빌드가 같으면 순서도 같다).
            var sync = server.GetComponent<PilotPoseSync>();
            if (sync == null) sync = Undo.AddComponent<PilotPoseSync>(server.gameObject);
            var remote = AddRemotePilotPlaceholders(scene);
            var syncSerialized = new SerializedObject(sync);
            if (remote != null)
            {
                syncSerialized.FindProperty("cockpit").objectReferenceValue = remote.Value.cockpit;
                syncSerialized.FindProperty("remoteHead").objectReferenceValue = remote.Value.head;
                syncSerialized.FindProperty("remoteLeftHand").objectReferenceValue = remote.Value.left;
                syncSerialized.FindProperty("remoteRightHand").objectReferenceValue = remote.Value.right;
            }
            syncSerialized.ApplyModifiedProperties();

            // 선우 11/04: 테스트는 앞으로 제철소 맵 위에서(샌드박스도).
            var map = MainSceneMenu.AddMap(scene);
            // M4-01: 한 판 흐름 + 글자판.
            var match = MatchFlowMenu.AddToScene(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            return $"네트워크 세션({(made ? "새로 만듦" : "있던 것 다시 연결")}, 포트 7777, 운영자 시작 화면) + 전투 서버 + 상대 자세 동기화 + " + map + " + " + match +
                   (remote != null ? "" : "(조종석이 없어 상대 조종사 표시는 안 만듦)");
        }

        const string RemotePilotName = "Remote Pilot (임시)";
        const float RemoteHeadSize = 0.22f;   // 사람 머리(m)
        const float RemoteHandSize = 0.09f;   // 사람 손(m)

        /// <summary>조종석 안에 상대 조종사 임시 표시(머리 구·손 상자 2개). 조종사 아트가 오면 이 칸만 바꾼다. 있으면 그대로 쓴다.</summary>
        static (Anseong.Pilot.CockpitSeat cockpit, Transform head, Transform left, Transform right)? AddRemotePilotPlaceholders(Scene scene)
        {
            var cockpit = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Anseong.Pilot.CockpitSeat>(true)).FirstOrDefault();
            if (cockpit == null) return null;
            var root = cockpit.transform.Find(RemotePilotName);
            if (root == null)
            {
                var go = new GameObject(RemotePilotName);
                Undo.RegisterCreatedObjectUndo(go, "Add Network Session");
                go.transform.SetParent(cockpit.transform, false);
                root = go.transform;
            }
            var head = Part(root, "Head", PrimitiveType.Sphere, RemoteHeadSize);
            var left = Part(root, "Left Hand", PrimitiveType.Cube, RemoteHandSize);
            var right = Part(root, "Right Hand", PrimitiveType.Cube, RemoteHandSize);
            return (cockpit, head, left, right);
        }

        static Transform Part(Transform parent, string name, PrimitiveType shape, float size)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(go, "Add Network Session");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.SetActive(false);   // 받는 동안만 켠다
            return go.transform;
        }
    }
}
