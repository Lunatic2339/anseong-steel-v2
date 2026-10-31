using System.Linq;
using Anseong.Combat;
using Anseong.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M2-01: 열린 씬에 네트워크 세션(NetworkManager + UnityTransport + SessionBootstrap)과 전투 서버(NetworkObject + CombatServer)를 붙인다.
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

            var server = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatServer>(true)).FirstOrDefault();
            if (server == null)
            {
                var go = new GameObject(ServerName);
                Undo.RegisterCreatedObjectUndo(go, "Add Network Session");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<NetworkObject>();
                server = go.AddComponent<CombatServer>();
            }
            var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatDirector>(true)).FirstOrDefault();
            var serialized = new SerializedObject(server);
            serialized.FindProperty("director").objectReferenceValue = director;
            serialized.ApplyModifiedProperties();
            if (director == null) Debug.LogWarning($"{LogPrefix} 씬에 CombatDirector가 없습니다. Rebuild Robot + Pilot Input을 먼저 실행하세요.");

            EditorSceneManager.MarkSceneDirty(scene);
            return $"네트워크 세션({(made ? "새로 만듦" : "있던 것 다시 연결")}, 1인 양팔 호스트 127.0.0.1:7777) + 전투 서버";
        }
    }
}
