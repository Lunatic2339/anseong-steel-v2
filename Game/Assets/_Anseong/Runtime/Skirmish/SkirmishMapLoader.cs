using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 교전 장면 위에 맵 장면을 겹쳐 불러온다. 맵은 맵 장면 하나에만 있고 교전 장면에는 복사하지 않는다(맵을 고치면 바로 반영).
    /// 맵 장면의 검토용 물건(검토 카메라, 경로선, 이름표 등)은 끄고, 안개·하늘·주변광이 맵의 것이 되도록 맵을 활성 장면으로 둔다.
    /// </summary>
    public class SkirmishMapLoader : MonoBehaviour
    {
        [SerializeField, Tooltip("겹쳐 불러올 맵 장면(Assets/…​.unity).")] string scenePath;
        [SerializeField, Tooltip("이 글자로 시작하는 맵 장면의 최상위 물건은 끈다(검토용).")] string[] hideRoots = new string[0];

        public bool Loaded { get; private set; }

        public void Bind(string path, string[] hidden)
        {
            scenePath = path; hideRoots = hidden;
        }

        IEnumerator Start()
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
            {
#if UNITY_EDITOR
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
#endif
                scene = SceneManager.GetSceneByPath(scenePath);
            }
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"[SkirmishMapLoader] 맵 장면을 불러오지 못했습니다: {scenePath}", this);
                yield break;
            }
            Prepare(scene, hideRoots);
            SceneManager.SetActiveScene(scene);
            Loaded = true;
        }

        /// <summary>맵 장면에서 검토용 물건과 카메라·소리 듣는 귀를 끈다. 에디터에서 화면을 찍을 때도 쓴다.</summary>
        public static void Prepare(Scene scene, string[] hidden)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                bool hide = false;
                for (int i = 0; hidden != null && i < hidden.Length; i++)
                    if (root.name.StartsWith(hidden[i])) hide = true;
                if (hide) { root.SetActive(false); continue; }
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
        }
    }
}
