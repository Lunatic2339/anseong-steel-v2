using System.Collections.Generic;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>
    /// Z2 교전 상태를 화면에 옮긴다(회색 상자 단계). 규칙은 <see cref="SkirmishSession"/>에만 있고 여기는 보여 주기만 한다.
    /// 씬 구성은 메뉴 'Anseong → Sandbox → Build Z2 Skirmish'가 한다.
    /// </summary>
    public class SkirmishView : MonoBehaviour
    {
        [SerializeField] Transform mech;
        [SerializeField, Tooltip("어깨에서 조준 방향으로 도는 오른팔(포).")] Transform mechRightArm;
        [SerializeField, Tooltip("가드 방향으로 도는 왼팔(방패).")] Transform mechLeftArm;
        [SerializeField, Tooltip("0 = G3, 1 = G4.")] Transform[] enemies = new Transform[2];
        [SerializeField, Tooltip("발사 예고 때 커지는 포구 빛.")] Renderer[] enemyGlow = new Renderer[2];
        [SerializeField] Transform bigShellTemplate, furnaceShellTemplate, missileTemplate;
        [SerializeField] LineRenderer cannonTracer, mechTracer;
        [SerializeField, Tooltip("고로 상태등(초록 → 주황 → 빨강).")] Renderer furnaceLight;
        [SerializeField] Transform gunnerRig;
        [SerializeField, Tooltip("공중포대 포신(사수의 오른손 방향으로 돈다).")] Transform gunnerBarrel;

        [SerializeField, Tooltip("돌격형(한 번에 한 명). 없으면 안 그린다.")] Transform grunt;
        [SerializeField, Tooltip("예고 표시: 공격이 오는 쪽에 뜨는 빛.")] Renderer gruntTelegraph;
        [SerializeField, Tooltip("가드 굳힘 표시: 가슴 앞을 막는 판.")] Renderer gruntGuard;
        [SerializeField, Tooltip("실제 로봇 모델의 팔(있으면 상자 팔 대신 쓴다).")] SkirmishMechArms mechArms;
        [SerializeField, Tooltip("뒤에서 기다리는 돌격형(둘이 같이 오는 장면에서만).")] Transform gruntBack;

        [SerializeField, Min(0.01f), Tooltip("사격 궤적이 보이는 시간(초).")] float tracerSeconds = 0.06f;
        [Header("돌격형 테두리 빛(전용 셰이더가 있는 재질에서만 보인다)")]
        [SerializeField, ColorUsage(false, true)] Color gruntRim = new Color(0.3f, 0.17f, 0.1f);
        [SerializeField, ColorUsage(false, true), Tooltip("공격을 예고하는 동안.")] Color gruntRimTelegraph = new Color(1.3f, 0.16f, 0.08f);
        [SerializeField, ColorUsage(false, true), Tooltip("멈칫해서 빈틈이 난 동안.")] Color gruntRimStagger = new Color(0.7f, 0.7f, 0.65f);
        [SerializeField, ColorUsage(false, true), Tooltip("가드를 굳힌 동안.")] Color gruntRimGuard = new Color(0.8f, 0.5f, 0.08f);
        [SerializeField] Color furnaceSafe = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] Color furnaceWarn = new Color(1f, 0.6f, 0.1f);
        [SerializeField] Color furnaceDanger = new Color(1f, 0.1f, 0.05f);

        [SerializeField, Tooltip("공격 예고를 큰 빛 덩어리로도 보여 준다. 테두리 빛이 되는 프로젝트에서는 끈다(표시가 겹친다).")] bool telegraphGlow = true;
        public bool TelegraphGlow { get => telegraphGlow; set => telegraphGlow = value; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        Renderer[] gruntRenderers;
        MaterialPropertyBlock rimBlock;

        readonly Dictionary<int, Transform> shells = new Dictionary<int, Transform>();
        readonly List<int> stale = new List<int>();
        MaterialPropertyBlock block;
        float cannonTracerUntil, mechTracerUntil, punchUntil;

        public Transform Mech => mech;
        /// <summary>규칙은 1초에 50번 돌고 화면은 더 자주 그리므로, 그 사이를 이은 위치를 서버가 넣는다. null이면 상태의 위치.</summary>
        public Vector3? MechPosition { get; set; }
        public Transform GunnerRig => gunnerRig;

        /// <summary>씬을 만드는 에디터 메뉴가 참조를 채운다.</summary>
        public void Bind(Transform mechRoot, Transform rightArm, Transform leftArm, Transform[] enemyRoots, Renderer[] glows,
            Transform bigShell, Transform furnaceShell, Transform missile, LineRenderer cannonLine, LineRenderer mechLine,
            Renderer furnace, Transform gunner, Transform barrel)
        {
            mech = mechRoot; mechRightArm = rightArm; mechLeftArm = leftArm;
            enemies = enemyRoots; enemyGlow = glows;
            bigShellTemplate = bigShell; furnaceShellTemplate = furnaceShell; missileTemplate = missile;
            cannonTracer = cannonLine; mechTracer = mechLine;
            furnaceLight = furnace; gunnerRig = gunner; gunnerBarrel = barrel;
        }

        public void BindMechArms(SkirmishMechArms arms) => mechArms = arms;

        public void BindGruntBack(Transform root) => gruntBack = root;
        Renderer[] backRenderers;

        /// <summary>뒤에서 기다리는 돌격형. 큰 탄을 겨누는 동안 붉게 빛난다.</summary>
        void ShowBack(SkirmishState st)
        {
            if (gruntBack == null) return;
            bool present = st.backIndex >= 0;
            if (gruntBack.gameObject.activeSelf != present) gruntBack.gameObject.SetActive(present);
            if (!present) return;
            gruntBack.position = st.backPosition;
            var facing = st.mech - st.backPosition;
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f) gruntBack.rotation = Quaternion.LookRotation(facing);
            backRenderers ??= gruntBack.GetComponentsInChildren<Renderer>(true);
            rimBlock ??= new MaterialPropertyBlock();
            rimBlock.SetColor(RimColorId, st.backShell > 0f ? gruntRimTelegraph : st.backStep == GruntStep.Frozen ? gruntRimStagger : gruntRim);
            foreach (var r in backRenderers) r.SetPropertyBlock(rimBlock);
        }

        public void BindGrunt(Transform root, Renderer telegraph, Renderer guardPlate)
        {
            grunt = root; gruntTelegraph = telegraph; gruntGuard = guardPlate;
        }

        /// <summary>서버 쪽(규칙을 직접 돌리는 PC)에서 쓰는 짧은 형태.</summary>
        public void Show(SkirmishSession session, Vector3 mechAim, float now) =>
            Show(session.State, session.Settings, session.Events, mechAim, Vector3.zero, now);

        /// <param name="events">이번에 새로 일어난 일(사격 궤적 표시).</param>
        /// <param name="mechAim">메카 오른팔이 향한 방향(화면 표시용).</param>
        /// <param name="gunnerAim">공중포대 포신 방향(0이면 그대로 둔다).</param>
        /// <param name="now">궤적을 끄는 데 쓰는 현재 시각(초).</param>
        public void Show(SkirmishState st, SkirmishSettings s, IReadOnlyList<SkirmishEvent> events, Vector3 mechAim, Vector3 gunnerAim, float now)
        {
            block ??= new MaterialPropertyBlock();
            if (gunnerBarrel != null && gunnerAim.sqrMagnitude > 1e-6f) gunnerBarrel.rotation = Quaternion.LookRotation(gunnerAim);

            mech.position = MechPosition ?? st.mech;
            if (mechRightArm != null && mechAim.sqrMagnitude > 1e-6f) mechRightArm.rotation = Quaternion.LookRotation(mechAim);
            for (int i = 0; events != null && i < events.Count; i++)
            {
                if (events[i].Kind == SkirmishEventKind.PunchHit || events[i].Kind == SkirmishEventKind.PunchBounced) punchUntil = now + 0.15f;
                // 타격 멈춤: 꽂힌 순간 적(규칙 쪽)과 함께 로봇 오른팔도 선다. 튕길 때는 서지 않는다.
                if (events[i].Kind == SkirmishEventKind.PunchHit && mechArms != null)
                    mechArms.HoldRight(st.gruntHp <= 0f ? s.grunt.finishStopSeconds : s.grunt.hitStopSeconds);
            }
            // 임시 표현: 주먹을 낸 순간 오른팔이 앞으로 늘어난다.
            if (mechRightArm != null) mechRightArm.localScale = new Vector3(1f, 1f, now < punchUntil ? 1.6f : 1f);
            if (mechLeftArm != null)
            {
                var guard = st.guarding && st.guardDirection.sqrMagnitude > 0f ? st.guardDirection + Vector3.up * 0.2f : new Vector3(0f, -0.6f, 0.8f);
                mechLeftArm.rotation = Quaternion.LookRotation(guard);
            }
            if (mechArms != null)
            {
                mechArms.SetPose(st.guarding, st.guardSide, now < punchUntil);
                mechArms.SetShield(st.shieldOpen, st.shieldBroken);
                // 에디터에서 화면을 찍을 때는 LateUpdate가 돌지 않으므로 여기서 바로 자세를 잡는다.
                if (!Application.isPlaying) mechArms.Apply(0f, true);
            }
            if (gunnerRig != null) gunnerRig.position = s.gunnerPost;

            for (int i = 0; i < enemies.Length; i++)
            {
                var e = st.enemies[i];
                var t = enemies[i];
                if (t.gameObject.activeSelf != s.rangedEnemies) t.gameObject.SetActive(s.rangedEnemies);
                if (!s.rangedEnemies)
                {
                    Off(enemyGlow[i]);
                    continue;
                }
                t.position = e.position;
                var facing = i == SkirmishSession.G3 ? st.mech - e.position
                    : e.step == EnemyStep.Advance ? s.g4Post - e.position : s.furnacePoint - e.position;
                facing.y = 0f;
                var rotation = facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : t.rotation;
                // 임시 표현: 쓰러지면 눕히고, 멈칫하면 뒤로 젖힌다. 동작 클립이 붙으면 대체한다.
                if (e.step == EnemyStep.Dead) rotation *= Quaternion.Euler(-88f, 0f, 0f);
                else if (e.step == EnemyStep.Stagger) rotation *= Quaternion.Euler(-14f, 0f, 0f);
                t.rotation = rotation;

                var glow = enemyGlow[i];
                if (glow == null) continue;
                bool winding = e.step == EnemyStep.Windup;
                glow.enabled = winding;
                if (!winding) continue;
                float total = i == SkirmishSession.G4 ? s.g4Charge : s.g3Windup;
                glow.transform.position = SkirmishSession.Muzzle(st, s, i) + rotation * Vector3.forward * 5f;
                glow.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 7f, 1f - Mathf.Clamp01(e.timer / total));
            }

            ShowGrunt(st, s);
            ShowBack(st);
            SyncShells(st);
            ShowTracers(events, now);

            if (furnaceLight != null)
            {
                float left = (float)st.furnace / Mathf.Max(1, s.furnaceHits);
                block.SetColor(BaseColorId, left > 0.67f ? furnaceSafe : left > 0.34f ? furnaceWarn : furnaceDanger);
                furnaceLight.SetPropertyBlock(block);
            }
        }

        static void Off(Renderer r) { if (r != null) r.enabled = false; }

        /// <summary>돌격형을 임시로 표현한다: 예고는 공격이 오는 쪽의 빛, 가드는 가슴 앞의 판. 동작 클립이 붙으면 대체한다.</summary>
        void ShowGrunt(SkirmishState st, SkirmishSettings s)
        {
            if (grunt == null) return;
            bool present = st.gruntIndex >= 0;
            if (grunt.gameObject.activeSelf != present) grunt.gameObject.SetActive(present);
            if (!present) { Off(gruntTelegraph); Off(gruntGuard); return; }

            bool dead = st.gruntStep == GruntStep.Dead;
            // 마무리 한 방에 밀려나는 것은 순간 이동하지 않고 미끄러지게 한다.
            float glide = 60f * (Application.isPlaying ? Time.deltaTime : 1f);
            grunt.position = dead ? Vector3.MoveTowards(grunt.position, st.gruntPosition, glide) : st.gruntPosition;
            var facing = st.mech - st.gruntPosition;
            facing.y = 0f;
            var rotation = facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : grunt.rotation;
            var pose = rotation;
            if (dead) pose *= Quaternion.Euler(-88f, 0f, 0f);
            else if (st.gruntStep == GruntStep.Stagger) pose *= Quaternion.Euler(-16f, 0f, 0f);
            else if (st.gruntStep == GruntStep.Guard) pose *= Quaternion.Euler(10f, 0f, 0f);
            grunt.rotation = pose;

            bool telegraph = st.gruntStep == GruntStep.Rise || st.gruntStep == GruntStep.Hold || st.gruntStep == GruntStep.Strike || st.gruntShell > 0f;
            // 상태를 몸의 테두리 색으로도 알린다(예고 = 붉게, 빈틈 = 희게, 가드 = 노랗게, 쓰러지면 꺼짐).
            gruntRenderers ??= grunt.GetComponentsInChildren<Renderer>(true);
            rimBlock ??= new MaterialPropertyBlock();
            rimBlock.SetColor(RimColorId, dead ? Color.black : telegraph ? gruntRimTelegraph
                : st.gruntStep == GruntStep.Stagger || st.gruntStep == GruntStep.Frozen ? gruntRimStagger
                : st.gruntStep == GruntStep.Guard ? gruntRimGuard : gruntRim);
            foreach (var r in gruntRenderers) r.SetPropertyBlock(rimBlock);
            if (gruntTelegraph != null)
            {
                gruntTelegraph.enabled = telegraph && telegraphGlow;
                if (telegraph && telegraphGlow)
                {
                    // 내려치기는 적의 오른팔(로봇이 볼 때 왼쪽 위), 휘두르기는 적의 왼팔(로봇이 볼 때 오른쪽 옆)에서 온다.
                    bool slam = st.gruntAttack == GruntAttack.Slam;
                    var ready = st.gruntPosition + rotation * (slam ? new Vector3(9f, s.gruntHeight + 4f, 2f) : new Vector3(-15f, s.gruntHeight * 0.75f, 0f));
                    var target = SkirmishSession.Chest(st, s) + rotation * (slam ? new Vector3(4f, 5f, 0f) : new Vector3(-6f, 0f, 0f));
                    float k = st.gruntStep == GruntStep.Strike ? 1f - Mathf.Clamp01(st.gruntTimer / Mathf.Max(0.01f, s.grunt.strikeSeconds)) : 0f;
                    float grow = st.gruntStep == GruntStep.Rise ? 1f - Mathf.Clamp01(st.gruntTimer / Mathf.Max(0.01f, s.grunt.riseSeconds)) : 1f;
                    gruntTelegraph.transform.position = Vector3.Lerp(ready, target, k * k);
                    gruntTelegraph.transform.localScale = Vector3.one * Mathf.Lerp(2f, 7f, grow);
                }
            }
            if (gruntGuard != null)
            {
                bool guard = st.gruntStep == GruntStep.Guard;
                gruntGuard.enabled = guard;
                if (guard) gruntGuard.transform.SetPositionAndRotation(st.gruntPosition + rotation * new Vector3(0f, s.gruntHeight * 0.62f, 5.5f), rotation);
            }
        }

        void SyncShells(SkirmishState st)
        {
            stale.Clear();
            foreach (var id in shells.Keys) stale.Add(id);
            foreach (var shell in st.shells)
            {
                stale.Remove(shell.id);
                if (!shells.TryGetValue(shell.id, out var t))
                {
                    var template = shell.kind == ShellKind.BigShell ? bigShellTemplate
                        : shell.kind == ShellKind.FurnaceShell ? furnaceShellTemplate : missileTemplate;
                    t = Instantiate(template, transform);
                    t.gameObject.SetActive(true);
                    shells.Add(shell.id, t);
                }
                t.position = shell.position;
                if (shell.velocity.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(shell.velocity);
            }
            foreach (var id in stale)
            {
                var go = shells[id].gameObject;
                shells.Remove(id);
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
        }

        void ShowTracers(IReadOnlyList<SkirmishEvent> events, float now)
        {
            for (int i = 0; events != null && i < events.Count; i++)
            {
                var e = events[i];
                if (e.Kind == SkirmishEventKind.CannonShot && cannonTracer != null)
                {
                    // 눈앞에서 나가는 선이 화면을 가리지 않게 조종석 아래 포구에서 시작한다.
                    cannonTracer.SetPosition(0, e.From + Vector3.down * 3f);
                    cannonTracer.SetPosition(1, e.To);
                    cannonTracerUntil = now + tracerSeconds;
                }
                else if (e.Kind == SkirmishEventKind.MechGunShot && mechTracer != null)
                {
                    mechTracer.SetPosition(0, e.From);
                    mechTracer.SetPosition(1, e.To);
                    mechTracerUntil = now + tracerSeconds;
                }
            }
            if (cannonTracer != null) cannonTracer.enabled = now < cannonTracerUntil;
            if (mechTracer != null) mechTracer.enabled = now < mechTracerUntil;
        }
    }
}
