using System.Linq;
using Anseong.Combat;
using Anseong.Haptics;
using Anseong.Pilot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M3-04: 지금 열린 씬에 외골격 연결(왼팔·오른팔 <see cref="ExoArmLink"/>)을 둔다. 여러 번 실행해도 된다(있으면 연결만 다시).
    /// 포트 이름은 Inspector에서 사람이 넣는다(비우면 그 팔은 꺼짐). 씬 저장은 사람이 한다.
    /// </summary>
    public static class ExoSetupMenu
    {
        const string LogPrefix = "[ExoSetupMenu]";
        const string RootName = "Exoskeleton";

        [MenuItem("Anseong/Setup/Add Exoskeleton Links")]
        static void AddExoskeletonLinks()
        {
            var scene = SceneManager.GetActiveScene();
            var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatDirector>(true)).FirstOrDefault();
            if (director == null)
            {
                EditorUtility.DisplayDialog("Add Exoskeleton Links",
                    $"'{scene.name}' 씬에 전투 판정(CombatDirector)이 없습니다. Rebuild Robot + Pilot Input을 먼저 실행하세요.", "확인");
                return;
            }

            var links = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExoArmLink>(true)).ToList();
            GameObject host = links.Count > 0 ? links[0].gameObject : null;
            if (host == null)
            {
                host = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(host, "Add Exoskeleton Links");
                SceneManager.MoveGameObjectToScene(host, scene);
            }

            var added = 0;
            foreach (var side in new[] { PilotSide.Left, PilotSide.Right })
            {
                var link = links.FirstOrDefault(l => l.Arm == side);
                if (link == null)
                {
                    link = Undo.AddComponent<ExoArmLink>(host);
                    var s = new SerializedObject(link);
                    s.FindProperty("arm").enumValueIndex = (int)side;
                    s.ApplyModifiedProperties();
                    added++;
                }
                var serialized = new SerializedObject(link);
                serialized.FindProperty("director").objectReferenceValue = director;
                serialized.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LogPrefix} 외골격 연결 {added}개 추가(있던 것은 연결만 다시). '{host.name}'의 Port Name에 COM 번호를 넣으세요" +
                      "(장치 관리자 → 포트). 비우면 그 팔은 꺼짐. 씬을 저장하세요.", host);
        }
    }
}
