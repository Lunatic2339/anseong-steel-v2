using System.Collections.Generic;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 무기·머리 빛·주먹 포탄 겉모습(M4-02 프로토타입, 모든 PC). 규칙은 모르고 복제된 보스 체력만 보고 그린다.
    /// - 무기 3개를 <see cref="BossWeaponSet"/> 목록대로 보스 뼈에 붙인다(11/11 확정안: 팔뚝 포 → 왼손 방패·오른손 검 → 가슴 핵). 모델 칸이 비면 도형.
    ///   금이 가면 색이 바뀌고, 부서지면 떼어 떨어뜨린다. 핵은 처음부터 붉게 빛난다.
    /// - 머리 빛: 기체 점검이 끝나면 켜지고, 장면이 바뀔 때(걸어오기·보복) 깜빡인다(보스는 소리치지 않고 빛으로 반응).
    /// - 주먹 포탄: 멀리서 좌/우 펀치를 시작하면 그 손에서 탄이 날아와 명중 순간에 닿는다.
    /// 아트가 오면 무기 목록 에셋(Data/Boss/BossWeaponSet.asset)의 부품 모델 칸에 넣는다. 보스 프리팹 메뉴가 붙이고 뼈를 채운다.
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

        [Header("무기 목록 (비우면 페이즈 표의 것, 그것도 없으면 확정안)")]
        [SerializeField, Tooltip("Data/Boss/BossWeaponSet.asset. 부품 모델 칸이 아트 자리.")]
        BossWeaponSet weaponSet;

        [SerializeField, Tooltip("우리 무기 모델을 빌려 쓸 때 덮을 보스 재질(비면 색만 칠함).")]
        Material borrowedMaterial;

        [Header("아트 자리 (비우면 도형)")]
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
                var baseColor = Set().At((int)weapon) != null ? Set().At((int)weapon).color : armorColor;
                var color = cracks <= 0 ? baseColor : cracks == 1 ? crack1 : crack2;
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
            var set = Set();
            for (var w = 0; w < parts.Length; w++)
            {
                var weapon = set.At(w);
                if (weapon == null || weapon.parts == null) continue;
                foreach (var part in weapon.parts)
                {
                    var go = Attach(part);
                    if (go == null) continue;
                    parts[w].Add(go);
                    Tint(go, weapon.color);
                }
            }
        }

        BossWeaponSet fallback;

        BossWeaponSet Set()
        {
            if (weaponSet != null) return weaponSet;
            if (brain != null && brain.Weapons != null) return brain.Weapons;
            if (fallback == null) fallback = ScriptableObject.CreateInstance<BossWeaponSet>();
            return fallback;
        }

        PlayerWeaponVisuals player;

        GameObject Borrowed(BossWeaponSet.PlayerModel model)
        {
            if (model == BossWeaponSet.PlayerModel.None) return null;
            if (player == null) player = FindAnyObjectByType<PlayerWeaponVisuals>();
            if (player == null) return null;
            switch (model)
            {
                case BossWeaponSet.PlayerModel.Cannon: return player.CannonPrefab;
                case BossWeaponSet.PlayerModel.Sword: return player.SwordPrefab;
                default: return player.ShieldPrefab;
            }
        }

        Transform BoneOf(BossBone bone)
        {
            switch (bone)
            {
                case BossBone.LeftForeArm: return leftForeArm;
                case BossBone.RightForeArm: return rightForeArm;
                case BossBone.LeftHand: return leftHand;
                case BossBone.RightHand: return rightHand;
                default: return chest;
            }
        }

        /// <summary>부품 하나를 뼈에 붙인다. 크기 배수: 팔뚝을 따라 붙이면 팔뚝 길이, 그 밖은 가슴~머리 거리.</summary>
        GameObject Attach(BossWeaponSet.Part part)
        {
            var bone = BoneOf(part.bone);
            if (bone == null) return null;
            var prefab = part.prefab != null ? part.prefab : Borrowed(part.borrow);
            var go = Make(prefab, part.shape, "Boss Weapon · " + part.label);
            if (part.prefab == null && prefab != null && borrowedMaterial != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (var m = 0; m < mats.Length; m++) mats[m] = borrowedMaterial;
                    r.sharedMaterials = mats;
                }
            switch (part.placement)
            {
                case BossWeaponSet.Placement.AlongForeArm:
                {
                    var hand = part.bone == BossBone.LeftForeArm ? leftHand : part.bone == BossBone.RightForeArm ? rightHand : null;
                    var along = hand != null ? hand.position - bone.position : bone.up * size * 0.5f;
                    var length = Mathf.Max(0.1f, along.magnitude);
                    go.transform.SetPositionAndRotation(bone.position + along * 0.5f, Quaternion.FromToRotation(Vector3.up, along.normalized));
                    go.transform.SetParent(bone, true);
                    if (prefab == null) SetWorldSize(go.transform, part.size * length);
                    else go.transform.localScale = prefab.transform.localScale; // 우리 로봇과 같은 뼈 크기라 손 뼈 기준 그대로
                    break;
                }
                case BossWeaponSet.Placement.InHand:
                {
                    var unit = bone.parent != null ? Mathf.Max(0.1f, Vector3.Distance(bone.position, bone.parent.position)) : size * 0.3f;
                    go.transform.SetParent(bone, false);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.position = bone.position + bone.up * unit * part.push;
                    if (prefab == null) SetWorldSize(go.transform, part.size * unit);
                    break;
                }
                default:
                    go.transform.SetPositionAndRotation(bone.position + transform.forward * size * part.push, transform.rotation);
                    go.transform.SetParent(bone, true);
                    if (prefab == null) SetWorldSize(go.transform, part.size * size);
                    else go.transform.localScale = prefab.transform.localScale;
                    break;
            }
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
