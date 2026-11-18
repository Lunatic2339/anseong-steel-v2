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

        [SerializeField, Min(0.01f), Tooltip("사격 궤적이 보이는 시간(초).")] float tracerSeconds = 0.06f;
        [SerializeField] Color furnaceSafe = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] Color furnaceWarn = new Color(1f, 0.6f, 0.1f);
        [SerializeField] Color furnaceDanger = new Color(1f, 0.1f, 0.05f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly Dictionary<int, Transform> shells = new Dictionary<int, Transform>();
        readonly List<int> stale = new List<int>();
        MaterialPropertyBlock block;
        float cannonTracerUntil, mechTracerUntil;

        public Transform Mech => mech;
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

            mech.position = st.mech;
            if (mechRightArm != null && mechAim.sqrMagnitude > 1e-6f) mechRightArm.rotation = Quaternion.LookRotation(mechAim);
            if (mechLeftArm != null)
            {
                var guard = st.guarding && st.guardDirection.sqrMagnitude > 0f ? st.guardDirection + Vector3.up * 0.2f : new Vector3(0f, -0.6f, 0.8f);
                mechLeftArm.rotation = Quaternion.LookRotation(guard);
            }
            if (gunnerRig != null) gunnerRig.position = s.gunnerPost;

            for (int i = 0; i < enemies.Length; i++)
            {
                var e = st.enemies[i];
                var t = enemies[i];
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

            SyncShells(st);
            ShowTracers(events, now);

            if (furnaceLight != null)
            {
                float left = (float)st.furnace / Mathf.Max(1, s.furnaceHits);
                block.SetColor(BaseColorId, left > 0.67f ? furnaceSafe : left > 0.34f ? furnaceWarn : furnaceDanger);
                furnaceLight.SetPropertyBlock(block);
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
