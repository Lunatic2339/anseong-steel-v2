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

        [SerializeField, Tooltip("맵 씬에서 숨길 오브젝트 이름 조각. 맵 작업용 크기 기준 로봇(…ScaleOnly…)이 전투 중에 서 있지 않게 한다.")]
        string[] hideNameContains = { "ScaleOnly" };

        [SerializeField, Tooltip("맵 씬의 조명 설정(하늘·환경광·반사)을 가져와 쓴다. 맵 재질은 맵 씬의 조명에 맞춰 만든 것이라 Main 조명으로 그리면 바닥이 하얗게 날아간다(11/13 자동 점검). 안개는 Main 것을 유지.")]
        bool useEnvironmentLighting = true;

        [Header("맵 바닥 (11/13: 바닥이 하얗게 날아가 무늬가 안 보임)")]
        [SerializeField, Range(0.2f, 1.5f), Tooltip("맵 바닥 재질(밝기 값이 있는 맵 전용 셰이더)의 밝기에 곱한다. 재질 파일은 건드리지 않고 화면에서만 덮는다. 1 = 맵 그대로.")]
        float groundBrightness = 0.5f;
        [SerializeField, Range(0f, 0.3f), Tooltip("바닥 판 줄눈 세기 최소값(맵 값이 더 크면 그대로).")]
        float groundJointMin = 0.24f;
        [SerializeField, Range(0f, 0.6f), Tooltip("바닥 얼룩(넓은 먼지 무늬) 세기 최소값.")]
        float groundDustMin = 0.45f;
        [SerializeField, Range(0.2f, 1.5f), Tooltip("바닥 텍스처 대비 최소값(낮으면 무늬가 안 보인다).")]
        float groundContrastMin = 1.1f;

        [Header("맵 건물 외벽 (11/13: 건물이 단색 상자로 보임)")]
        [SerializeField, Tooltip("텍스처 없는 단색 건물 재질 대신 화면에서만 쓰는 외벽 재질(골강판·패널 이음매·창·때). 메뉴 Build M1 Main이 넣는다. 비우면 맵 그대로.")]
        Material facadeMaterial;
        [SerializeField, Min(1f), Tooltip("이 크기(m, 가장 긴 변)보다 큰 것만 외벽 재질로 바꾼다. 작은 소품은 그대로 둔다.")]
        float facadeMinSize = 8f;

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
            int cameras = 0, scripts = 0, hidden = 0;
            var mapHasSun = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                hidden += HideByName(root.transform);
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
            if (useEnvironmentLighting) CopyLighting(scene);
            var grounds = TuneGround(scene);
            var facades = ApplyFacade(scene);
            Loaded = true;
            Debug.Log($"[EnvironmentLoader] 환경 '{scene.name}' 불러옴: 카메라 {cameras}개 끔, 기준 오브젝트 {hidden}개 숨김, 바닥 재질 칸 {grounds}개 조정, 외벽 재질 칸 {facades}개 교체, 맵 스크립트 {scripts}개 멈춤, 해 = {(mapHasSun ? "맵" : "Main")}.", this);
        }

        /// <summary>
        /// 조명 설정(RenderSettings)은 활성 씬 것만 쓰인다. 맵 씬을 잠깐 활성으로 바꿔 값을 읽고, 되돌린 뒤 Main에 적용한다
        /// (활성 씬을 맵으로 두면 새로 만드는 오브젝트가 맵 씬에 들어가므로 되돌린다).
        /// </summary>
        void CopyLighting(Scene environment)
        {
            var main = SceneManager.GetActiveScene();
            if (main == environment || !SceneManager.SetActiveScene(environment)) return;
            var skybox = RenderSettings.skybox;
            var sun = RenderSettings.sun;
            var mode = RenderSettings.ambientMode;
            var light = RenderSettings.ambientLight;
            var sky = RenderSettings.ambientSkyColor;
            var equator = RenderSettings.ambientEquatorColor;
            var ground = RenderSettings.ambientGroundColor;
            var intensity = RenderSettings.ambientIntensity;
            var reflectionMode = RenderSettings.defaultReflectionMode;
            var reflection = RenderSettings.customReflectionTexture;
            var reflectionIntensity = RenderSettings.reflectionIntensity;
            var shadow = RenderSettings.subtractiveShadowColor;
            SceneManager.SetActiveScene(main);

            RenderSettings.skybox = skybox;
            if (sun != null) RenderSettings.sun = sun;
            RenderSettings.ambientMode = mode;
            RenderSettings.ambientLight = light;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = intensity;
            RenderSettings.defaultReflectionMode = reflectionMode;
            if (reflection != null) RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.subtractiveShadowColor = shadow;
            DynamicGI.UpdateEnvironment();
        }

        static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        static readonly int JointId = Shader.PropertyToID("_JointStrength");
        static readonly int DustId = Shader.PropertyToID("_DustStrength");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");

        /// <summary>맵 바닥 셰이더(밝기 값이 있는 재질)를 쓰는 렌더러에 MaterialPropertyBlock으로 밝기·줄눈·얼룩·대비를 덮는다. 조정한 재질 칸 수.</summary>
        int TuneGround(Scene scene)
        {
            var count = 0;
            var block = new MaterialPropertyBlock();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (m == null || !m.HasProperty(BrightnessId)) continue;
                    r.GetPropertyBlock(block, i);
                    block.SetFloat(BrightnessId, m.GetFloat(BrightnessId) * groundBrightness);
                    if (m.HasProperty(JointId)) block.SetFloat(JointId, Mathf.Max(m.GetFloat(JointId), groundJointMin));
                    if (m.HasProperty(DustId)) block.SetFloat(DustId, Mathf.Max(m.GetFloat(DustId), groundDustMin));
                    if (m.HasProperty(ContrastId)) block.SetFloat(ContrastId, Mathf.Max(m.GetFloat(ContrastId), groundContrastMin));
                    r.SetPropertyBlock(block, i);
                    count++;
                }
            }
            return count;
        }

        const string PlainLitShader = "Universal Render Pipeline/Lit";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        readonly System.Collections.Generic.Dictionary<Material, Material> facadeCopies = new System.Collections.Generic.Dictionary<Material, Material>();

        /// <summary>
        /// 큰 건물의 단색 재질(텍스처 없는 불투명 URP Lit, 발광 없음)을 외벽 재질 사본으로 바꾼다. 원래 색은 유지한다.
        /// 재질 파일은 건드리지 않는다(사본은 판이 끝나면 지운다). 바꾼 재질 칸 수.
        /// </summary>
        int ApplyFacade(Scene scene)
        {
            if (facadeMaterial == null) return 0;
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var size = r.bounds.size;
                if (Mathf.Max(size.x, size.y, size.z) < facadeMinSize) continue;
                var materials = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (m == null || m.shader == null || m.shader.name != PlainLitShader) continue;
                    if (m.renderQueue >= 2500 || m.IsKeywordEnabled("_EMISSION")) continue;
                    if (m.HasProperty(BaseMapId) && m.GetTexture(BaseMapId) != null) continue;
                    if (!facadeCopies.TryGetValue(m, out var copy))
                    {
                        copy = new Material(facadeMaterial) { name = m.name + " (외벽)" };
                        if (m.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, m.GetColor(BaseColorId));
                        facadeCopies[m] = copy;
                    }
                    materials[i] = copy;
                    changed = true;
                    count++;
                }
                if (changed) r.sharedMaterials = materials;
            }
            return count;
        }

        void OnDestroy()
        {
            foreach (var copy in facadeCopies.Values) if (copy != null) Destroy(copy);
            facadeCopies.Clear();
        }

        /// <summary>이름에 숨길 조각이 든 오브젝트를 끈다(자식까지 내려가며, 끈 것 아래는 더 보지 않는다).</summary>
        int HideByName(Transform t)
        {
            if (hideNameContains == null || hideNameContains.Length == 0) return 0;
            foreach (var piece in hideNameContains)
            {
                if (string.IsNullOrEmpty(piece) || t.name.IndexOf(piece, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!t.gameObject.activeSelf) return 0;
                t.gameObject.SetActive(false);
                return 1;
            }
            var count = 0;
            for (var i = 0; i < t.childCount; i++) count += HideByName(t.GetChild(i));
            return count;
        }
    }
}
