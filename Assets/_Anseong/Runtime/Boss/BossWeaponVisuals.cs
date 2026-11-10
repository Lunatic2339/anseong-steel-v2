using System.Collections.Generic;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 무기·머리 빛·주먹 포탄 겉모습(M4-02 프로토타입, 모든 PC). 규칙은 모르고 복제된 보스 체력만 보고 그린다.
    /// - 무기 3개(양 팔뚝 포, 가슴 장갑판, 가슴 핵)를 보스 뼈에 단순 도형으로 붙인다. 금이 가면 색이 바뀌고, 부서지면 떼어 떨어뜨린다.
    /// - 머리 빛: 기체 점검이 끝나면 켜지고, 장면이 바뀔 때(걸어오기·보복) 깜빡인다(보스는 소리치지 않고 빛으로 반응).
    /// - 주먹 포탄: 멀리서 좌/우 펀치를 시작하면 그 손에서 탄이 날아와 명중 순간에 닿는다.
    /// 아트가 오면 도형 대신 프리팹 칸(<see cref="cannonPrefab"/> 등)에 넣는다. 메뉴 'Add Match Flow'가 보스에 붙이고 뼈를 채운다.
    /// </summary>
    public class BossWeaponVisuals : MonoBehaviour
    {
        [Header("뼈 (메뉴가 채운다)")]
        [SerializeField] Transform leftForeArm;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightForeArm;
        [SerializeField] Transform rightHand;
        [SerializeField, Tooltip("가슴 뼈(mixamorig:Spine2).")] Transform chest;
        [SerializeField] Transform head;

        [Header("아트 자리 (비우면 도형)")]
        [SerializeField, Tooltip("팔뚝 포 모델(원점 = 팔꿈치 쪽 끝, +Y = 손 쪽). 비우면 원통.")]
        GameObject cannonPrefab;
        [SerializeField, Tooltip("가슴 장갑판 모델. 비우면 판.")]
        GameObject armorPrefab;
        [SerializeField, Tooltip("가슴 핵 모델. 비우면 구.")]
        GameObject corePrefab;
        [SerializeField, Tooltip("주먹 포탄 모델. 비우면 구.")]
        GameObject shellPrefab;
        [SerializeField, Tooltip("도형에 쓸 재질(비우면 기본 재질에 색만).")]
        Material shapeMaterial;

        [Header("색")]
        [SerializeField] Color armorColor = new Color(0.35f, 0.35f, 0.38f);
        [SerializeField, Tooltip("금 1개, 2개.")] Color crack1 = new Color(0.9f, 0.75f, 0.2f);
        [SerializeField] Color crack2 = new Color(0.95f, 0.35f, 0.1f);
        [SerializeField] Color coreColor = new Color(1f, 0.1f, 0.05f);

        [Header("기타")]
        [SerializeField, Tooltip("보스 HP 막대를 숨긴다(대본: 목표는 HP가 아니라 무기).")]
        bool hideHealthBar = true;
        [SerializeField, Min(0f), Tooltip("부서진 무기가 떨어진 뒤 사라지기까지(초).")]
        float debrisSeconds = 6f;
        [SerializeField, Min(1f), Tooltip("조종사와 이 거리(m)보다 멀 때만 펀치를 주먹 포탄으로 보인다.")]
        float shellMinDistance = 40f;
        [SerializeField, Min(0f), Tooltip("주먹 포탄이 닿는 자리를 조종석 가운데에서 좌우로 벌리는 거리(m).")]
        float shellSideOffset = 6f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        BossBrain brain;
        BossServer server;
        CombatDirector director;
        MatchFlow flow;
        Transform cockpit;
        Light headLight;
        Renderer headGlow;
        MaterialPropertyBlock block;
        readonly List<GameObject>[] parts = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        readonly List<Shell> shells = new List<Shell>();
        BossWeapon shown = BossWeapon.ArmCannon;
        int shownCracks = -1;
        float size = 10f;
        float flashUntil;
        bool subscribed;

        struct Shell
        {
            public Transform Body;
            public Vector3 From;
            public Vector3 To;
            public float Start;
            public float Seconds;
        }

        void Start()
        {
            brain = GetComponent<BossBrain>();
            server = GetComponent<BossServer>();
            director = FindAnyObjectByType<CombatDirector>();
            flow = FindAnyObjectByType<MatchFlow>();
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) cockpit = seat.transform;
            block = new MaterialPropertyBlock();
            if (chest != null && head != null) size = Mathf.Max(1f, Vector3.Distance(chest.position, head.position));
            if (hideHealthBar)
            {
                var bar = GetComponentInChildren<BossHealthBar>(true);
                if (bar != null) bar.gameObject.SetActive(false);
            }
            BuildHeadLight();
            Build();
            if (director != null)
            {
                director.Cue += OnCue;
                subscribed = true;
            }
            if (flow != null) flow.SceneChanged += OnScene;
        }

        void OnDestroy()
        {
            if (subscribed && director != null) director.Cue -= OnCue;
            if (flow != null) flow.SceneChanged -= OnScene;
        }

        void Update()
        {
            UpdateWeapons();
            UpdateHeadLight();
            UpdateShells();
        }

        // ── 무기 ──

        void UpdateWeapons()
        {
            if (director == null || brain == null) return;
            var state = director.State;
            var hp = state.BossMax > 0f ? state.BossHealth / state.BossMax * 100f : 100f;
            var weapon = BossWeaponRules.Current(hp, brain.Thresholds);
            var cracks = BossWeaponRules.CrackCount(hp, brain.Thresholds);
            if (weapon < shown)
            {
                // 재시작: 다시 붙인다.
                Build();
                shownCracks = -1;
            }
            while (shown < weapon && shown != BossWeapon.None)
            {
                BreakOff(shown);
                shown++;
                shownCracks = -1;
            }
            if (cracks != shownCracks && weapon != BossWeapon.None)
            {
                shownCracks = cracks;
                var color = cracks <= 0 ? (weapon == BossWeapon.Core ? coreColor : armorColor) : cracks == 1 ? crack1 : crack2;
                foreach (var p in parts[(int)weapon]) Tint(p, color);
            }
        }

        void Build()
        {
            for (var i = 0; i < parts.Length; i++)
            {
                foreach (var p in parts[i]) if (p != null) Destroy(p);
                parts[i].Clear();
            }
            shown = BossWeapon.ArmCannon;
            if (leftForeArm != null && leftHand != null) parts[0].Add(Cannon(leftForeArm, leftHand));
            if (rightForeArm != null && rightHand != null) parts[0].Add(Cannon(rightForeArm, rightHand));
            if (chest != null)
            {
                var forward = transform.forward;
                var plate = Make(armorPrefab, PrimitiveType.Cube, "Boss Weapon · Chest Armor");
                plate.transform.SetPositionAndRotation(chest.position + forward * size * 0.35f, transform.rotation);
                plate.transform.SetParent(chest, true);
                SetWorldSize(plate.transform, new Vector3(size * 1.3f, size * 1.1f, size * 0.2f));
                parts[1].Add(plate);
                Tint(plate, armorColor);

                var core = Make(corePrefab, PrimitiveType.Sphere, "Boss Weapon · Core");
                core.transform.SetPositionAndRotation(chest.position + forward * size * 0.2f, transform.rotation);
                core.transform.SetParent(chest, true);
                SetWorldSize(core.transform, Vector3.one * size * 0.45f);
                parts[2].Add(core);
                Tint(core, coreColor);
            }
        }

        GameObject Cannon(Transform foreArm, Transform hand)
        {
            var go = Make(cannonPrefab, PrimitiveType.Cylinder, "Boss Weapon · Arm Cannon");
            var along = hand.position - foreArm.position;
            var length = Mathf.Max(0.1f, along.magnitude);
            go.transform.SetPositionAndRotation(foreArm.position + along * 0.5f, Quaternion.FromToRotation(Vector3.up, along.normalized));
            go.transform.SetParent(foreArm, true);
            // 원통은 높이 2라 절반으로.
            SetWorldSize(go.transform, new Vector3(length * 0.35f, length * 0.45f, length * 0.35f));
            Tint(go, armorColor);
            return go;
        }

        GameObject Make(GameObject prefab, PrimitiveType shape, string label)
        {
            GameObject go;
            if (prefab != null) go = Instantiate(prefab);
            else
            {
                go = GameObject.CreatePrimitive(shape);
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                var renderer = go.GetComponent<Renderer>();
                if (shapeMaterial != null && renderer != null) renderer.sharedMaterial = shapeMaterial;
            }
            go.name = label;
            return go;
        }

        static void SetWorldSize(Transform t, Vector3 world)
        {
            var parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(world.x / Mathf.Max(1e-6f, parent.x), world.y / Mathf.Max(1e-6f, parent.y), world.z / Mathf.Max(1e-6f, parent.z));
        }

        void Tint(GameObject go, Color color)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        void BreakOff(BossWeapon weapon)
        {
            foreach (var p in parts[(int)weapon])
            {
                if (p == null) continue;
                p.transform.SetParent(null, true);
                var body = p.AddComponent<Rigidbody>();
                body.mass = 1000f;
                body.linearVelocity = (transform.forward + Vector3.up * 0.5f) * size * 0.5f;
                body.angularVelocity = Random.insideUnitSphere * 2f;
                Destroy(p, debrisSeconds);
            }
            parts[(int)weapon].Clear();
            flashUntil = Time.time + 1.5f;
        }

        // ── 머리 빛 ──

        void BuildHeadLight()
        {
            if (head == null) return;
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Boss Head Light";
            var collider = glow.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            glow.transform.SetParent(head, false);
            glow.transform.position = head.position + transform.forward * size * 0.25f;
            SetWorldSize(glow.transform, Vector3.one * size * 0.15f);
            headGlow = glow.GetComponent<Renderer>();
            if (shapeMaterial != null && headGlow != null) headGlow.sharedMaterial = shapeMaterial;
            Tint(glow, coreColor);
            headLight = glow.AddComponent<Light>();
            headLight.type = LightType.Point;
            headLight.color = coreColor;
            headLight.range = size * 3f;
            headLight.intensity = 0f;
        }

        void OnScene(MatchScene scene, MatchScene previous)
        {
            if (scene == MatchScene.Entrance || scene == MatchScene.Approach || scene == MatchScene.LostGun || scene == MatchScene.LostSword)
                flashUntil = Time.time + 1.5f;
        }

        void UpdateHeadLight()
        {
            if (headGlow == null) return;
            var awake = flow == null || !flow.Running || (flow.Scene != MatchScene.Waiting && flow.Scene != MatchScene.Checkup);
            var flashing = Time.time < flashUntil && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
            headGlow.enabled = awake && !flashing;
            if (headLight != null) headLight.intensity = awake && !flashing ? 50f : 0f;
        }

        // ── 주먹 포탄 ──

        void OnCue(CombatEvent e)
        {
            if (e.Cue != CombatCue.BossTelegraph || cockpit == null || server == null) return;
            var kind = (BossAttackKind)e.Code;
            if (kind != BossAttackKind.PunchLeft && kind != BossAttackKind.PunchRight) return;
            var flat = cockpit.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude < shellMinDistance) return;
            var hand = kind == BossAttackKind.PunchLeft ? leftHand : rightHand;
            var from = hand != null ? hand.position : transform.position + Vector3.up * size * 3f;
            var side = cockpit.right * (e.Side == PilotSide.Left ? -shellSideOffset : shellSideOffset);
            var body = Make(shellPrefab, PrimitiveType.Sphere, "Boss Fist Shell");
            SetWorldSize(body.transform, Vector3.one * size * 0.25f);
            body.transform.position = from;
            Tint(body, crack2);
            shells.Add(new Shell
            {
                Body = body.transform, From = from, To = cockpit.position + side, Start = Time.time,
                Seconds = Mathf.Max(0.2f, server.SpecOf(kind).telegraphSeconds),
            });
        }

        void UpdateShells()
        {
            for (var i = shells.Count - 1; i >= 0; i--)
            {
                var s = shells[i];
                if (s.Body == null)
                {
                    shells.RemoveAt(i);
                    continue;
                }
                var t = Mathf.Clamp01((Time.time - s.Start) / s.Seconds);
                var p = Vector3.Lerp(s.From, s.To, t);
                p.y += Mathf.Sin(t * Mathf.PI) * size * 0.5f;
                s.Body.position = p;
                if (t >= 1f)
                {
                    Destroy(s.Body.gameObject);
                    shells.RemoveAt(i);
                }
            }
        }
    }
}
