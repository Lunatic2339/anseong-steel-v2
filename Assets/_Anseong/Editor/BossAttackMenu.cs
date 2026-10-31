using System.Collections.Generic;
using System.Linq;
using Anseong.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M2-02 보스 기술 에셋과 클립. 기술 에셋 5개(05 제안값)를 만들고, 비어 있는 클립 칸에는 임시 클립을 고른다.
    /// 'Measure Strike Moments'는 클립에서 보스 주먹 뼈가 가장 빠른 순간을 찾아 "명중 순간" 후보를 Console에 쓴다(D-01 표가 비었을 때).
    /// </summary>
    public static class BossAttackMenu
    {
        const string LogPrefix = "[BossAttackMenu]";
        public const string DataParent = "Assets/_Anseong";
        public const string DataFolder = DataParent + "/Data";
        public const string BossDataFolder = DataFolder + "/Boss";
        const string ClipFolder = "Assets/_Anseong/Art/Robot/Animation/Source";
        const string PunchClipPath = ClipFolder + "/Punch.fbx";
        const string WalkClipPath = ClipFolder + "/Walk.fbx";
        const int SampleRate = 60;

        static readonly BossAttackKind[] Kinds =
        {
            BossAttackKind.PunchLeft, BossAttackKind.PunchRight, BossAttackKind.AlternatingCombo, BossAttackKind.SlamBoth, BossAttackKind.Charge,
        };

        public static string AssetPath(BossAttackKind kind) => $"{BossDataFolder}/BossAttack_{kind}.asset";

        [MenuItem("Anseong/Boss/Create Attack Assets")]
        static void CreateMenu()
        {
            var assets = EnsureAttackAssets(out var made);
            Debug.Log($"{LogPrefix} 기술 에셋 {assets.Length}개({BossDataFolder}), 새로 만든 것 {made}개. 값은 05 기술표 제안값. 바꾼 뒤 Rebuild를 다시 돌리면 클립이 바뀐다.");
            if (assets.Length > 0) Selection.activeObject = assets[0];
        }

        /// <summary>기술 에셋 5개. 없으면 05 제안값으로 만든다(있는 것은 그대로).</summary>
        public static BossAttackDefinition[] EnsureAttackAssets(out int made)
        {
            made = 0;
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder(DataParent, "Data");
            if (!AssetDatabase.IsValidFolder(BossDataFolder)) AssetDatabase.CreateFolder(DataFolder, "Boss");
            var list = new List<BossAttackDefinition>();
            foreach (var kind in Kinds)
            {
                var path = AssetPath(kind);
                var def = AssetDatabase.LoadAssetAtPath<BossAttackDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<BossAttackDefinition>();
                    def.spec = BossAttackSpec.Default(kind);
                    AssetDatabase.CreateAsset(def, path);
                    made++;
                }
                list.Add(def);
            }
            if (made > 0) AssetDatabase.SaveAssets();
            return list.ToArray();
        }

        /// <summary>진짜 클립이 없을 때 쓰는 임시 클립(M2-02 지시서 표). 옛 레포 AlternatingCombo·GroundCast를 가져오면 기술 에셋 칸에 넣는다.</summary>
        public static AnimationClip FallbackClip(BossAttackKind kind)
        {
            switch (kind)
            {
                case BossAttackKind.Charge: return RobotArtSettings.ClipIn(WalkClipPath);
                default: return RobotArtSettings.ClipIn(PunchClipPath);
            }
        }

        public static AnimationClip ClipOf(BossAttackDefinition def) => def != null && def.clip != null ? def.clip : FallbackClip(def != null ? def.Kind : BossAttackKind.PunchRight);

        /// <summary>보스 컨트롤러에 넣을 기술 상태(이름, 클립, 속도).</summary>
        public static IEnumerable<(string name, AnimationClip clip, float speed)> AttackStates(IEnumerable<BossAttackDefinition> defs)
        {
            foreach (var def in defs)
            {
                if (def == null) continue;
                var clip = ClipOf(def);
                if (clip == null) continue;
                yield return (BossAttackDefinition.StateName(def.Kind), clip, def.clipSpeed);
            }
        }

        /// <summary>보스 컨트롤러의 M2 상태 전부: 기술 5개 + 쓰러짐(클립이 있을 때).</summary>
        public static List<(string name, AnimationClip clip, float speed)> BossStates(RobotArtSettings art, BossAttackDefinition[] attacks)
        {
            var states = AttackStates(attacks).ToList();
            if (art.BossDeathClip != null) states.Add(("Death", art.BossDeathClip, 1f));
            else Debug.Log($"{LogPrefix} 쓰러짐(Death) 클립이 없어 보스가 쓰러질 때 대기 자세로 멈춥니다. Robot Art Settings의 보스 쓰러짐 칸에 넣으면 됩니다.");
            return states;
        }

        [MenuItem("Anseong/Boss/Measure Strike Moments")]
        static void MeasureMenu()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabMenu.BossPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Measure Strike Moments", $"보스 프리팹이 없습니다: {RobotPrefabMenu.BossPrefabPath}\nRebuild를 먼저 실행하세요.", "확인");
                return;
            }
            var defs = EnsureAttackAssets(out _);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var animator = boss.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"{LogPrefix} 보스 프리팹에 Animator가 없습니다.");
                    return;
                }
                var left = RobotSandboxUtility.FindBone(boss.transform, RobotSandboxUtility.LeftPrefix + RobotSandboxUtility.HandSuffix);
                var right = RobotSandboxUtility.FindBone(boss.transform, RobotSandboxUtility.RightPrefix + RobotSandboxUtility.HandSuffix);
                foreach (var def in defs)
                {
                    var clip = ClipOf(def);
                    if (clip == null) continue;
                    var report = Measure(animator.gameObject, clip, left, right, def.clipSpeed);
                    Debug.Log($"{LogPrefix} {def.Kind} ({clip.name}{(def.clip == null ? ", 임시" : "")}, 길이 {clip.length / def.clipSpeed:F2}초): {report}. " +
                              $"지금 기술 에셋: 예고(=첫 명중 순간) {def.spec.telegraphSeconds:F2}초, 끝 {def.spec.EndTime:F2}초", def);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // 손 뼈 속도가 가장 큰 순간(초, 재생 속도 반영)과 그 속도(m/s).
        static string Measure(GameObject root, AnimationClip clip, Transform left, Transform right, float speed)
        {
            var frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * SampleRate));
            var best = new[] { (time: 0f, speed: 0f), (time: 0f, speed: 0f) };
            var previous = new Vector3[2];
            var hands = new[] { left, right };
            for (var f = 0; f <= frames; f++)
            {
                var t = Mathf.Min(clip.length, f / (float)SampleRate);
                clip.SampleAnimation(root, t);
                for (var h = 0; h < 2; h++)
                {
                    if (hands[h] == null) continue;
                    var p = hands[h].position;
                    if (f > 0)
                    {
                        var v = Vector3.Distance(p, previous[h]) * SampleRate;
                        if (v > best[h].speed) best[h] = (t, v);
                    }
                    previous[h] = p;
                }
            }
            string Text(int h, string name) => hands[h] == null ? $"{name} 손 뼈 없음" : $"{name} 손 최고 속도 {best[h].time / speed:F2}초({best[h].speed:F0} m/s)";
            return Text(0, "왼") + ", " + Text(1, "오른");
        }
    }
}
