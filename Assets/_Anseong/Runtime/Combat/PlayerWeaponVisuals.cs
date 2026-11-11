using Anseong.Flow;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 우리 로봇 무기 겉모습(M4-03 프로토타입, 모든 PC). 왼팔 = 방패/스캐너, 오른팔 = 검/포.
    /// 판 흐름이 복제한 장면으로 무엇을 들지 정하고(<see cref="WeaponRules"/>), 상실 장면이 시작되면 그 무기를 떼어 떨어뜨린다.
    /// 성재 모델이 오면 프리팹 칸에 넣는다(원점 = 손 뼈, +Y = 손가락 쪽). 비우면 상자·원통. 메뉴 'Add Match Flow'가 플레이어 로봇에 붙인다.
    /// </summary>
    public class PlayerWeaponVisuals : MonoBehaviour
    {
        [Header("아트 자리 (비우면 도형)")]
        [SerializeField] GameObject shieldPrefab;
        [SerializeField] GameObject swordPrefab;
        [SerializeField] GameObject scannerPrefab;
        [SerializeField] GameObject cannonPrefab;
        [SerializeField, Tooltip("도형 재질(비우면 기본 재질에 색만).")]
        Material shapeMaterial;

        [Header("색")]
        [SerializeField] Color shieldColor = new Color(0.25f, 0.4f, 0.75f);
        [SerializeField] Color swordColor = new Color(0.85f, 0.85f, 0.9f);
        [SerializeField] Color scannerColor = new Color(0.2f, 0.8f, 0.6f);
        [SerializeField] Color cannonColor = new Color(0.4f, 0.4f, 0.42f);

        [SerializeField, Min(0f), Tooltip("떨어진 무기가 사라지기까지(초).")]
        float debrisSeconds = 6f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        MatchFlow flow;
        GameObject shield, sword, scanner, cannon;
        Loadout shown = (Loadout)255;
        MaterialPropertyBlock block;

        /// <summary>왼팔 스캐너 끝(드론 찾기 레이저가 나가는 곳, M4-04). 없으면 null.</summary>
        public Transform ScannerTip => scanner != null ? scanner.transform : null;
        /// <summary>오른팔 포 끝(M4-04).</summary>
        public Transform CannonTip => cannon != null ? cannon.transform : null;
        public Loadout Shown => shown;
        /// <summary>보스가 같은 모델을 빌려 쓴다(11/11 보스 같은 무장). 비면 null.</summary>
        public GameObject ShieldPrefab => shieldPrefab;
        public GameObject SwordPrefab => swordPrefab;
        public GameObject CannonPrefab => cannonPrefab;

        void Start()
        {
            flow = FindAnyObjectByType<MatchFlow>();
            block = new MaterialPropertyBlock();
            Transform leftHand = null, rightHand = null;
            foreach (var arm in GetComponentsInChildren<RobotArmDriver>(true))
            {
                if (arm.Side == PilotSide.Left) leftHand = arm.HandBone;
                else rightHand = arm.HandBone;
            }
            if (leftHand != null)
            {
                shield = Attach(shieldPrefab, PrimitiveType.Cube, leftHand, new Vector3(1.0f, 1.3f, 0.12f), 0.2f, shieldColor, "Weapon · Shield");
                scanner = Attach(scannerPrefab, PrimitiveType.Cube, leftHand, new Vector3(0.3f, 0.6f, 0.3f), 0.5f, scannerColor, "Weapon · Scanner");
            }
            if (rightHand != null)
            {
                sword = Attach(swordPrefab, PrimitiveType.Cube, rightHand, new Vector3(0.15f, 2.4f, 0.05f), 1.3f, swordColor, "Weapon · Sword");
                cannon = Attach(cannonPrefab, PrimitiveType.Cylinder, rightHand, new Vector3(0.35f, 0.5f, 0.35f), 0.5f, cannonColor, "Weapon · Cannon");
            }
            if (flow != null) flow.SceneChanged += OnScene;
        }

        void OnDestroy()
        {
            if (flow != null) flow.SceneChanged -= OnScene;
        }

        void Update()
        {
            var loadout = flow != null && flow.Running ? flow.Loadout : Loadout.Bare;
            if (loadout == shown) return;
            shown = loadout;
            Show(shield, loadout == Loadout.Melee);
            Show(sword, loadout == Loadout.Melee);
            Show(scanner, loadout == Loadout.Ranged);
            Show(cannon, loadout == Loadout.Ranged);
        }

        void OnScene(MatchScene scene, MatchScene previous)
        {
            // 보복으로 부서지는 순간: 그 무기 복사본을 떨어뜨린다(원본은 장면에 따라 숨김).
            if (scene == MatchScene.LostGun)
            {
                Drop(scanner);
                Drop(cannon);
            }
            else if (scene == MatchScene.LostSword)
            {
                Drop(shield);
                Drop(sword);
            }
        }

        static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        void Drop(GameObject original)
        {
            if (original == null) return;
            var copy = Instantiate(original, original.transform.position, original.transform.rotation);
            copy.transform.localScale = original.transform.lossyScale;
            copy.SetActive(true);
            var body = copy.AddComponent<Rigidbody>();
            body.mass = 100f;
            body.angularVelocity = Random.insideUnitSphere * 3f;
            Destroy(copy, debrisSeconds);
        }

        /// <summary>손 뼈에 붙인다. size·along은 팔뚝 길이 배수(손 뼈 → 부모 뼈 거리), along = 손가락 쪽으로 밀어낸 정도.</summary>
        GameObject Attach(GameObject prefab, PrimitiveType shape, Transform hand, Vector3 size, float along, Color color, string label)
        {
            var unit = hand.parent != null ? Vector3.Distance(hand.position, hand.parent.position) : 1f;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab);
            else
            {
                go = GameObject.CreatePrimitive(shape);
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                var r = go.GetComponent<Renderer>();
                if (shapeMaterial != null && r != null) r.sharedMaterial = shapeMaterial;
            }
            go.name = label;
            go.transform.SetParent(hand, false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.position = hand.position + hand.up * unit * along;
            if (prefab == null)
            {
                var parent = hand.lossyScale;
                var world = size * unit;
                go.transform.localScale = new Vector3(world.x / Mathf.Max(1e-6f, parent.x), world.y / Mathf.Max(1e-6f, parent.y), world.z / Mathf.Max(1e-6f, parent.z));
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, color);
                    block.SetColor(ColorId, color);
                    r.SetPropertyBlock(block);
                }
            }
            go.SetActive(false);
            return go;
        }
    }
}
