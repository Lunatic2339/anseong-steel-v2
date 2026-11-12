using System.Collections.Generic;
using System.Linq;
using Anseong.Boss;
using Anseong.Pilot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M2-06 유진 보스 클립 연결. 유진 샌드박스(BossV13FinalAnimation 프리팹)용 클립은 뼈 경로가 "Visual/Mecha/Armature/..."라서
    /// 우리 보스 모델("Armature/...")에서 그대로 재생되지 않는다. 이 메뉴가 원본(Art/Boss/Animation/Source)을 읽어
    /// 뼈 이름으로 경로를 다시 잇고(손 리깅용 HF_ 뼈·검은 버림), 오른손 펀치를 좌우 대칭으로 구워 왼손 펀치를 만들고,
    /// 기술 에셋의 재생 속도를 "주먹이 가장 빠른 순간 = 예고 끝(첫 명중)"에 맞춘다. 결과는 Art/Boss/Animation/Boss_*.anim.
    /// 기술 에셋 클립 칸·Robot Art Settings 칸이 비어 있으면 이 클립을 쓴다(칸에 넣은 것이 먼저).
    /// </summary>
    public static class BossClipImport
    {
        const string LogPrefix = "[BossClipImport]";
        public const string Parent = "Assets/_Anseong/Art/Boss";
        public const string Folder = Parent + "/Animation";
        public const string SourceFolder = Folder + "/Source";
        /// <summary>Cascadeur로 다듬어 돌려받은 클립 자리(11/13). 결과 이름과 같은 FBX(예: Boss_SlamBoth.fbx)가 있으면 원본보다 먼저 쓴다.</summary>
        public const string CascadeurFolder = Folder + "/Cascadeur";
        const float BakeRate = 30f;
        const int MeasureRate = 60;
        const float StrongPeak = 0.7f;
        const float MinSpeed = 0.5f, MaxSpeed = 2.5f;

        /// <summary>유진 원본 이름 → 우리 이름, 반복 여부. Charge(돌진)·포효·무릎·밀려남은 맞는 클립이 없어 그대로(임시) 둔다.</summary>
        /// <summary>
        /// hipTravel = 골반이 처음 자리에서 수평으로 움직이는 정도(1 = 원본). 공격 클립은 몸이 앞으로 크게 쏠려
        /// 30 m 앞에 선 우리 로봇 몸·조종석 카메라 안으로 파고든다(11/13 자동 점검) → 줄인다. 위아래 움직임은 그대로.
        /// </summary>
        static readonly (string source, string output, bool loop, float hipTravel)[] Table =
        {
            ("Idle", "Boss_Idle", true, 1f),
            ("Walk", "Boss_Walk", true, 1f),
            ("Stagger", "Boss_Stagger", false, 1f),
            ("Death", "Boss_Death", false, 1f),
            ("Punch", "Boss_PunchRight", false, AttackHipTravel),
            ("Combat_AlternatingCombo", "Boss_AlternatingCombo", false, AttackHipTravel),
            ("Combat_OverheadSmash", "Boss_SlamBoth", false, AttackHipTravel),
            // 11/13 유진 브랜치(origin/Eugene, AnimationSandbox/Clips)에서 가져온 클립
            ("Combat_SwordSlash", "Boss_SwordSlash", false, AttackHipTravel),
            ("Combat_Shockwave", "Boss_Shockwave", false, AttackHipTravel),
            ("Combat_Grab", "Boss_Grab", false, AttackHipTravel),
            ("Combat_Feint", "Boss_Feint", false, AttackHipTravel),
            ("Combat_ArmLock", "Boss_ArmLock", false, AttackHipTravel),
        };

        const float AttackHipTravel = 0.4f;

        const string MirrorFrom = "Boss_PunchRight";
        const string MirrorTo = "Boss_PunchLeft";

        public static string OutputPath(string name) => $"{Folder}/{name}.anim";

        /// <summary>이 메뉴가 만든 클립(없으면 null).</summary>
        /// <summary>
        /// M4-08: 원본 클립 찾기. 같은 이름의 .anim이 먼저, 없으면 같은 이름의 .fbx(Cascadeur 내보내기) 안 첫 클립.
        /// 그래서 유진이 다듬은 FBX를 같은 이름으로 넣기만 하면 메뉴 한 번에 바뀐다.
        /// </summary>
        public static AnimationClip LoadSource(string source)
        {
            var anim = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{SourceFolder}/{source}.anim");
            if (anim != null) return anim;
            foreach (var ext in new[] { ".fbx", ".FBX" })
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{SourceFolder}/{source}{ext}"))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        /// <summary>Cascadeur 폴더의 같은 이름 FBX 안 첫 클립(없으면 null).</summary>
        public static AnimationClip LoadCascadeur(string output)
        {
            foreach (var ext in new[] { ".fbx", ".FBX" })
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{CascadeurFolder}/{output}{ext}"))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        public static AnimationClip Imported(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputPath(name));

        /// <summary>기술 하나의 가져온 클립(Boss_&lt;기술&gt;). 돌진은 없다.</summary>
        public static AnimationClip AttackClip(BossAttackKind kind) => Imported("Boss_" + kind);

        /// <summary>Cascadeur에서 내보낸 FBX 폴더를 골라 Boss_*.fbx만 Cascadeur 폴더로 복사한 뒤 클립을 다시 가져온다.</summary>
        [MenuItem("Anseong/Boss/Bring Cascadeur Clips...")]
        static void BringCascadeurMenu()
        {
            var folder = EditorUtility.OpenFolderPanel("Cascadeur에서 내보낸 FBX 폴더", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            Debug.Log($"{LogPrefix} Cascadeur 판 {CopyCascadeur(folder)}개 복사");
            ImportMenu();
        }

        /// <summary>명령줄용: -cascadeurFolder &lt;폴더&gt; 의 Boss_*.fbx를 복사하고 클립을 다시 가져온다.</summary>
        public static void BringCascadeurBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(args, "-cascadeurFolder");
            if (i >= 0 && i + 1 < args.Length) Debug.Log($"{LogPrefix} Cascadeur 판 {CopyCascadeur(args[i + 1])}개 복사");
            ImportMenu();
        }

        static int CopyCascadeur(string folder)
        {
            if (!System.IO.Directory.Exists(folder)) return 0;
            if (!AssetDatabase.IsValidFolder(CascadeurFolder)) AssetDatabase.CreateFolder(Folder, "Cascadeur");
            var count = 0;
            foreach (var file in System.IO.Directory.GetFiles(folder, "Boss_*.fbx"))
            {
                System.IO.File.Copy(file, $"{CascadeurFolder}/{System.IO.Path.GetFileName(file)}", true);
                count++;
            }
            AssetDatabase.Refresh();
            return count;
        }

        [MenuItem("Anseong/Boss/Import Boss Clips (Yujin)")]
        static void ImportMenu()
        {
            if (!Import(out var report))
            {
                EditorUtility.DisplayDialog("Import Boss Clips", report, "확인");
                return;
            }
            // 보스 컨트롤러가 새 클립·속도를 쓰도록 프리팹을 바로 다시 만든다(순서를 바꿔 실행해 옛 클립이 남던 문제, 11/04).
            var rebuilt = RobotPrefabMenu.Build();
            Debug.Log($"{LogPrefix} {report}\n보스 컨트롤러·프리팹 다시 만들기: {(rebuilt ? "완료" : "실패(Console 확인)")}. 씬에 안내 글자가 없으면 'Rebuild Robot + Pilot Input'도 실행하고 씬을 저장하세요.");
        }

        internal static bool Import(out string report)
        {
            var model = RobotArtSettings.Load().BossModel;
            if (model == null)
            {
                report = "보스 모델을 찾지 못했습니다. 'Anseong → Art → Robot Art Settings'를 확인하세요.";
                return false;
            }
            if (!AssetDatabase.IsValidFolder(SourceFolder))
            {
                report = $"원본 폴더가 없습니다: {SourceFolder}";
                return false;
            }
            var lines = new List<string>();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var all = root.GetComponentsInChildren<Transform>(true);
                var bones = new Dictionary<string, Transform>();
                foreach (var t in all)
                    if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);
                // 처음 자세(T 자세) = 대칭 굽기의 기준. 샘플링하기 전에 잡는다.
                var restRot = all.ToDictionary(t => t, t => t.rotation);
                var restPos = all.ToDictionary(t => t, t => t.position);

                foreach (var (source, output, loop, hipTravel) in Table)
                {
                    // Cascadeur 판(이미 우리 모델 뼈·골반 이동 줄임이 들어간 Boss_* 클립을 다듬은 것)이 있으면 그것을 쓴다. 골반 이동은 다시 줄이지 않는다.
                    var cascadeur = LoadCascadeur(output);
                    var src = cascadeur != null ? cascadeur : LoadSource(source);
                    if (src == null)
                    {
                        lines.Add($"{source}: 원본 없음, 건너뜀");
                        continue;
                    }
                    var clip = Remap(src, root.transform, bones, out var kept, out var dropped, out var hipScale);
                    var travel = cascadeur == null && hipTravel < 1f ? LimitHipTravel(clip, root.transform, bones, hipTravel) : -1f;
                    Save(clip, output, loop);
                    lines.Add($"{(cascadeur != null ? "Cascadeur/" + output : source)} → {output} ({src.length:F2}초, 뼈 곡선 {kept}개 연결·{dropped}개 버림, 골반 이동 배율 {hipScale:F3}" +
                              (travel >= 0f ? $", 골반 수평 이동 최대 {travel:F1} m → {travel * hipTravel:F1} m)" : ")"));
                }

                var right = Imported(MirrorFrom);
                if (right != null)
                {
                    var left = Mirror(right, root.transform, all, bones, restRot, restPos);
                    Save(left, MirrorTo, false);
                    lines.Add($"{MirrorFrom} → {MirrorTo} (좌우 대칭으로 구움)");
                }

                lines.AddRange(FitSpeeds(root, bones));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            AssetDatabase.SaveAssets();
            report = "보스 클립 가져오기:\n- " + string.Join("\n- ", lines);
            return true;
        }

        // 뼈 이름(경로 마지막 칸)으로 우리 모델의 뼈를 찾아 경로를 다시 쓴다. 못 찾은 곡선(HF_ 손 뼈, 검)은 버린다.
        static AnimationClip Remap(AnimationClip src, Transform root, Dictionary<string, Transform> bones, out int kept, out int dropped, out float hipScale)
        {
            kept = dropped = 0;
            var bindings = AnimationUtility.GetCurveBindings(src);
            hipScale = HipScale(src, bindings, bones);
            var newBindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            foreach (var b in bindings)
            {
                var name = b.path.Substring(b.path.LastIndexOf('/') + 1);
                if (b.type != typeof(Transform) || !bones.TryGetValue(name, out var bone) || bone == root)
                {
                    dropped++;
                    continue;
                }
                var curve = AnimationUtility.GetEditorCurve(src, b);
                if (curve == null) continue;
                if (b.propertyName.StartsWith("m_LocalPosition")) curve = Scaled(curve, hipScale);
                newBindings.Add(EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bone, root), typeof(Transform), b.propertyName));
                curves.Add(curve);
                kept++;
            }
            var clip = new AnimationClip { frameRate = src.frameRate };
            AnimationUtility.SetEditorCurves(clip, newBindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        /// <summary>
        /// 골반 위치 곡선을 다시 구워 수평 이동(첫 프레임 기준, 모델 루트 공간의 X·Z)만 factor배로 줄인다. 원래 최대 수평 이동(모델 단위 m)을 돌려준다.
        /// 골반 뼈의 부모(Armature)가 돌아가 있을 수 있어 부모 공간 → 루트 공간으로 바꿔 계산한다.
        /// </summary>
        static float LimitHipTravel(AnimationClip clip, Transform root, Dictionary<string, Transform> bones, float factor)
        {
            if (!bones.TryGetValue(RobotSandboxUtility.HipsBone, out var hips) || hips.parent == null) return -1f;
            var path = AnimationUtility.CalculateTransformPath(hips, root);
            var bx = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x");
            var by = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y");
            var bz = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z");
            var cx = AnimationUtility.GetEditorCurve(clip, bx);
            var cy = AnimationUtility.GetEditorCurve(clip, by);
            var cz = AnimationUtility.GetEditorCurve(clip, bz);
            if (cx == null || cy == null || cz == null) return -1f;

            // 부모 공간 벡터 → 루트 공간 벡터(회전·크기 포함).
            var toRoot = root.worldToLocalMatrix * hips.parent.localToWorldMatrix;
            var fromRoot = toRoot.inverse;
            var first = new Vector3(cx.Evaluate(0f), cy.Evaluate(0f), cz.Evaluate(0f));
            var frames = Mathf.Max(1, Mathf.CeilToInt(clip.length * BakeRate));
            var kx = new Keyframe[frames + 1];
            var ky = new Keyframe[frames + 1];
            var kz = new Keyframe[frames + 1];
            var most = 0f;
            for (var f = 0; f <= frames; f++)
            {
                var t = Mathf.Min(clip.length, f / BakeRate);
                var delta = toRoot.MultiplyVector(new Vector3(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t)) - first);
                most = Mathf.Max(most, new Vector2(delta.x, delta.z).magnitude);
                delta.x *= factor;
                delta.z *= factor;
                var p = first + fromRoot.MultiplyVector(delta);
                kx[f] = new Keyframe(t, p.x);
                ky[f] = new Keyframe(t, p.y);
                kz[f] = new Keyframe(t, p.z);
            }
            AnimationUtility.SetEditorCurve(clip, bx, Smooth(kx));
            AnimationUtility.SetEditorCurve(clip, by, Smooth(ky));
            AnimationUtility.SetEditorCurve(clip, bz, Smooth(kz));
            return most * root.lossyScale.x;
        }

        static AnimationCurve Smooth(Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (var i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        // 원본 골반 높이(첫 프레임)와 우리 모델 골반 높이의 비. 모델 단위가 달라도 골반이 떠 있거나 박히지 않게.
        static float HipScale(AnimationClip src, EditorCurveBinding[] bindings, Dictionary<string, Transform> bones)
        {
            if (!bones.TryGetValue(RobotSandboxUtility.HipsBone, out var hips)) return 1f;
            var p = Vector3.zero;
            var found = false;
            foreach (var b in bindings)
            {
                if (!b.path.EndsWith(RobotSandboxUtility.HipsBone) || !b.propertyName.StartsWith("m_LocalPosition")) continue;
                var v = AnimationUtility.GetEditorCurve(src, b).Evaluate(0f);
                if (b.propertyName.EndsWith(".x")) p.x = v;
                else if (b.propertyName.EndsWith(".y")) p.y = v;
                else p.z = v;
                found = true;
            }
            var ours = hips.localPosition.magnitude;
            return found && p.magnitude > 1e-5f && ours > 1e-5f ? ours / p.magnitude : 1f;
        }

        static AnimationCurve Scaled(AnimationCurve curve, float k)
        {
            if (Mathf.Approximately(k, 1f)) return curve;
            var keys = curve.keys;
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i].value *= k;
                keys[i].inTangent *= k;
                keys[i].outTangent *= k;
            }
            return new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
        }

        // 왼쪽 ↔ 오른쪽을 바꾸고 좌우(X) 대칭으로 굽는다. 각 뼈의 "처음 자세에서 바뀐 만큼"을 짝 뼈에서 가져와 거울에 비춘다.
        // 뼈 축 방향이 좌우로 달라도 맞게 나온다(처음 자세가 좌우 대칭이면).
        static AnimationClip Mirror(AnimationClip src, Transform root, Transform[] all, Dictionary<string, Transform> bones,
            Dictionary<Transform, Quaternion> restRot, Dictionary<Transform, Vector3> restPos)
        {
            var bindings = AnimationUtility.GetCurveBindings(src);
            var rotated = new HashSet<Transform>();
            var moved = new HashSet<Transform>();
            foreach (var b in bindings)
            {
                var t = root.Find(b.path);
                if (t == null) continue;
                if (b.propertyName.StartsWith("m_LocalRotation")) rotated.Add(t);
                else if (b.propertyName.StartsWith("m_LocalPosition")) moved.Add(t);
            }
            var partner = all.ToDictionary(t => t, t => bones.TryGetValue(Swap(t.name), out var p) ? p : t);
            var rotCurves = rotated.ToDictionary(t => t, t => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
            var posCurves = moved.ToDictionary(t => t, t => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
            var previous = new Dictionary<Transform, Quaternion>();
            var frames = Mathf.Max(1, Mathf.CeilToInt(src.length * BakeRate));
            var rot = new Dictionary<Transform, Quaternion>();
            var pos = new Dictionary<Transform, Vector3>();
            for (var f = 0; f <= frames; f++)
            {
                var time = Mathf.Min(src.length, f / BakeRate);
                src.SampleAnimation(root.gameObject, time);
                foreach (var t in all)
                {
                    rot[t] = t.rotation;
                    pos[t] = t.position;
                }
                foreach (var t in rotated)
                {
                    var local = Quaternion.Inverse(Mirrored(t.parent, root, partner, rot, restRot)) * Mirrored(t, root, partner, rot, restRot);
                    if (previous.TryGetValue(t, out var last) && Quaternion.Dot(last, local) < 0f) local = new Quaternion(-local.x, -local.y, -local.z, -local.w);
                    previous[t] = local;
                    var c = rotCurves[t];
                    c[0].AddKey(time, local.x);
                    c[1].AddKey(time, local.y);
                    c[2].AddKey(time, local.z);
                    c[3].AddKey(time, local.w);
                }
                foreach (var t in moved)
                {
                    // 짝 뼈가 처음 자리에서 움직인 만큼을 X 대칭으로.
                    var p = partner[t];
                    var delta = pos[p] - restPos[p];
                    var world = restPos[t] + new Vector3(-delta.x, delta.y, delta.z);
                    var parentRot = Mirrored(t.parent, root, partner, rot, restRot);
                    var parentPos = t.parent == root ? root.position : MirroredPosition(t.parent, partner, pos, restPos);
                    var scale = t.parent.lossyScale.x;
                    var local = Quaternion.Inverse(parentRot) * (world - parentPos) / (Mathf.Abs(scale) > 1e-6f ? scale : 1f);
                    var c = posCurves[t];
                    c[0].AddKey(time, local.x);
                    c[1].AddKey(time, local.y);
                    c[2].AddKey(time, local.z);
                }
            }
            var newBindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            string[] rotNames = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            string[] posNames = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            foreach (var pair in rotCurves)
                for (var i = 0; i < 4; i++)
                {
                    newBindings.Add(EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(pair.Key, root), typeof(Transform), rotNames[i]));
                    curves.Add(pair.Value[i]);
                }
            foreach (var pair in posCurves)
                for (var i = 0; i < 3; i++)
                {
                    newBindings.Add(EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(pair.Key, root), typeof(Transform), posNames[i]));
                    curves.Add(pair.Value[i]);
                }
            var clip = new AnimationClip { frameRate = BakeRate };
            AnimationUtility.SetEditorCurves(clip, newBindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();
            // 다음 샘플링(속도 맞추기)이 처음 자세에서 시작하게 돌려 둔다.
            foreach (var t in all) t.SetPositionAndRotation(restPos[t], restRot[t]);
            return clip;
        }

        // 거울에 비친 뼈의 월드 회전: 짝 뼈가 처음 자세에서 돈 만큼(X 대칭) × 이 뼈의 처음 자세.
        static Quaternion Mirrored(Transform t, Transform root, Dictionary<Transform, Transform> partner, Dictionary<Transform, Quaternion> rot, Dictionary<Transform, Quaternion> restRot)
        {
            if (t == root) return root.rotation;
            var p = partner[t];
            var delta = rot[p] * Quaternion.Inverse(restRot[p]);
            return MirrorX(delta) * restRot[t];
        }

        static Vector3 MirroredPosition(Transform t, Dictionary<Transform, Transform> partner, Dictionary<Transform, Vector3> pos, Dictionary<Transform, Vector3> restPos)
        {
            var p = partner[t];
            var delta = pos[p] - restPos[p];
            return restPos[t] + new Vector3(-delta.x, delta.y, delta.z);
        }

        /// <summary>X 대칭(좌우 거울)에 비친 회전.</summary>
        public static Quaternion MirrorX(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        /// <summary>뼈 이름의 Left ↔ Right.</summary>
        public static string Swap(string name)
        {
            if (name.Contains("Left")) return name.Replace("Left", "Right");
            if (name.Contains("Right")) return name.Replace("Right", "Left");
            return name;
        }

        static void Save(AnimationClip clip, string name, bool loop)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Parent, "Animation");
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.name = name;
            var path = OutputPath(name);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(clip, path);
                return;
            }
            // 같은 파일(같은 GUID)을 덮어써서 기술 에셋·컨트롤러 연결이 끊기지 않게.
            EditorUtility.CopySerialized(clip, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(clip);
        }

        // 기술 에셋의 재생 속도: 클립에서 주먹이 처음 크게 빨라지는 순간이 예고 끝(첫 명중)에 오도록. 클립 칸을 직접 채운 기술은 건드리지 않는다.
        static IEnumerable<string> FitSpeeds(GameObject root, Dictionary<string, Transform> bones)
        {
            bones.TryGetValue(RobotSandboxUtility.LeftPrefix + RobotSandboxUtility.HandSuffix, out var leftHand);
            bones.TryGetValue(RobotSandboxUtility.RightPrefix + RobotSandboxUtility.HandSuffix, out var rightHand);
            foreach (var def in BossAttackMenu.EnsureAttackAssets(out _))
            {
                if (def.clip != null) continue;
                var clip = AttackClip(def.Kind);
                if (clip == null) continue;
                Transform[] hands;
                switch (def.Kind)
                {
                    case BossAttackKind.PunchLeft: hands = new[] { leftHand }; break;
                    case BossAttackKind.PunchRight: hands = new[] { rightHand }; break;
                    case BossAttackKind.SwordSlash: hands = new[] { rightHand }; break;
                    case BossAttackKind.AlternatingCombo: hands = new[] { def.spec.firstSide == PilotSide.Left ? leftHand : rightHand }; break;
                    default: hands = new[] { leftHand, rightHand }; break;
                }
                var peak = FirstStrongPeak(root, clip, hands);
                if (peak <= 0f)
                {
                    yield return $"{def.Kind}: 주먹 순간을 찾지 못해 속도 {def.clipSpeed:F2} 그대로";
                    continue;
                }
                var speed = Mathf.Clamp(peak / def.spec.telegraphSeconds, MinSpeed, MaxSpeed);
                Undo.RecordObject(def, "Fit Boss Clip Speed");
                def.clipSpeed = speed;
                EditorUtility.SetDirty(def);
                yield return $"{def.Kind}: 클립 속 주먹 순간 {peak:F2}초 → 속도 {speed:F2} (예고 {def.spec.telegraphSeconds:F2}초에 맞춤" +
                             (Mathf.Approximately(speed, peak / def.spec.telegraphSeconds) ? ")" : $", {MinSpeed}~{MaxSpeed}배로 자름)");
            }
        }

        // 손 속도가 그 손 최고 속도의 70%를 처음 넘은 뒤 다시 줄기 시작하는 순간(클립 시간, 초). 여러 손이면 가장 이른 것.
        static float FirstStrongPeak(GameObject root, AnimationClip clip, Transform[] hands)
        {
            var frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * MeasureRate));
            var best = -1f;
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                var speeds = new float[frames + 1];
                var previous = Vector3.zero;
                for (var f = 0; f <= frames; f++)
                {
                    clip.SampleAnimation(root, Mathf.Min(clip.length, f / (float)MeasureRate));
                    var p = hand.position;
                    if (f > 0) speeds[f] = Vector3.Distance(p, previous) * MeasureRate;
                    previous = p;
                }
                var max = speeds.Max();
                if (max <= 0f) continue;
                for (var f = 1; f < frames; f++)
                {
                    if (speeds[f] < max * StrongPeak || speeds[f] < speeds[f + 1]) continue;
                    var t = f / (float)MeasureRate;
                    if (best < 0f || t < best) best = t;
                    break;
                }
            }
            return best;
        }
    }
}
