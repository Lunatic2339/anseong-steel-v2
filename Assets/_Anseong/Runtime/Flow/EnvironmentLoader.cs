using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.Flow
{
    /// <summary>
    /// 환경(맵) 씬을 Main 뒤에 같이 불러온다(additive, M1-07). 맵은 윤주가 자기 씬에서 계속 고치고 Main은 그 씬을 그대로 쓴다.
    /// 불러온 뒤 맵 씬의 카메라·오디오 리스너를 끄고(XR 카메라만 남김), 부서짐 같은 맵 스크립트를 멈춘다(부서짐은 M5).
    /// 맵에 해가 있으면 Main의 해를 끈다(두 개면 두 배로 밝아짐).
    /// </summary>
    public class EnvironmentLoader : MonoBehaviour
    {
        [SerializeField, Tooltip("불러올 환경 씬 이름(Build Settings에 있어야 함). 메뉴 Build M1 Main이 넣는다. 비우면 불러오지 않는다.")]
        string environmentScene;

        [SerializeField, Tooltip("맵 씬에 있는 카메라·오디오 리스너를 끈다(맵 확인용 카메라가 XR 화면을 덮지 않게).")]
        bool disableEnvironmentCameras = true;

        [SerializeField, Tooltip("맵 씬의 스크립트(부서지는 건물·굴뚝 등)를 멈춘다. 맵 조각 수천 개가 매 프레임 도는 비용을 없앤다. 부서짐은 M5에서 켠다.")]
        bool freezeEnvironmentScripts = true;

        [SerializeField, Tooltip("Main의 해. 맵 씬에 켜진 Directional Light가 있으면 이것을 끈다.")]
        Light mainLight;

        public bool Loaded { get; private set; }

        void Start()
        {
            if (string.IsNullOrEmpty(environmentScene)) return;
            var existing = SceneManager.GetSceneByName(environmentScene);
            if (existing.IsValid() && existing.isLoaded)
            {
                // 에디터에서 두 씬을 같이 열어 둔 경우.
                Prepare(existing);
                return;
            }
            var operation = SceneManager.LoadSceneAsync(environmentScene, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogWarning($"[EnvironmentLoader] 환경 씬 '{environmentScene}'을 불러오지 못했습니다. File → Build Profiles의 씬 목록에 있는지 확인하세요(메뉴 Build M1 Main이 넣음).", this);
                return;
            }
            operation.completed += _ => Prepare(SceneManager.GetSceneByName(environmentScene));
        }

        void Prepare(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            int cameras = 0, scripts = 0;
            var mapHasSun = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (disableEnvironmentCameras)
                {
                    foreach (var c in root.GetComponentsInChildren<Camera>(true)) { c.enabled = false; cameras++; }
                    foreach (var l in root.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
                }
                if (freezeEnvironmentScripts)
                {
                    foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (b == null || !b.enabled) continue;
                        b.enabled = false;
                        scripts++;
                    }
                }
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional && light.enabled && light.gameObject.activeInHierarchy) mapHasSun = true;
            }
            if (mapHasSun && mainLight != null) mainLight.enabled = false;
            Loaded = true;
            Debug.Log($"[EnvironmentLoader] 환경 '{scene.name}' 불러옴: 카메라 {cameras}개 끔, 맵 스크립트 {scripts}개 멈춤, 해 = {(mapHasSun ? "맵" : "Main")}.", this);
        }
    }
}
