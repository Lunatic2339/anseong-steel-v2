using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// Art/Robot/ 아래 FBX에 일관된 임포트 설정을 적용한다.
    /// 모델 FBX는 메시·재질만, 애니메이션 FBX(Animation/ 아래)는 클립만 쓴다.
    /// </summary>
    public class RobotModelPostprocessor : AssetPostprocessor
    {
        const string RobotRoot = "Assets/_Anseong/Art/Robot/";
        const string AnimationRoot = "Assets/_Anseong/Art/Robot/Animation/";
        const string ModelExtension = ".fbx";

        // 설정을 바꾸면 이 값을 올려서 재임포트시킨다.
        const uint Version = 1;

        // 공통
        const ModelImporterAnimationType AnimationType = ModelImporterAnimationType.Generic;
        const ModelImporterAvatarSetup AvatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        const float GlobalScale = 1f;      // 65m 스케일은 씬에서 적용한다.
        const bool UseFileScale = true;

        // 모델 FBX
        const ModelImporterMaterialImportMode ModelMaterialMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        const ModelImporterMaterialLocation ModelMaterialLocation = ModelImporterMaterialLocation.InPrefab;
        const ModelImporterMaterialSearch ModelMaterialSearch = ModelImporterMaterialSearch.RecursiveUp;

        // 애니메이션 FBX
        const ModelImporterMaterialImportMode AnimationMaterialMode = ModelImporterMaterialImportMode.None;
        // 처음 임포트할 때 반복 재생으로 둘 클립(파일 이름, 04_art_spec 애니메이션 목록).
        // 그 뒤에는 FBX Inspector의 Animation 탭에서 Loop Time을 바꾸면 그대로 남는다.
        static readonly string[] LoopClipNames =
        {
            "Idle", "Walk", "TurnLeft", "TurnRight",
            "Boss_WalkBack", "Pilot_StrafeLeft", "Pilot_StrafeRight",
        };

        public override uint GetVersion() => Version;

        void OnPreprocessModel()
        {
            if (!IsRobotModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;

            importer.animationType = AnimationType;
            importer.avatarSetup = AvatarSetup;
            importer.globalScale = GlobalScale;
            importer.useFileScale = UseFileScale;
            importer.importCameras = false;
            importer.importLights = false;

            if (IsAnimationFile(assetPath))
            {
                importer.importAnimation = true;
                importer.materialImportMode = AnimationMaterialMode;
                importer.importBlendShapes = false;
            }
            else
            {
                importer.importAnimation = false;
                importer.materialImportMode = ModelMaterialMode;
                importer.materialLocation = ModelMaterialLocation;
                importer.materialSearch = ModelMaterialSearch;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!IsRobotModel(assetPath) || !IsAnimationFile(assetPath)) return;
            var importer = (ModelImporter)assetImporter;

            // Mixamo 클립 이름("mixamo.com")을 파일 이름으로 바꾸고 루프 여부를 정한다.
            // 구간은 매번 파일에서 다시 읽고(클립 교체 대비), Loop Time은 이미 정해져 있으면(Inspector에서 바꾼 값) 그대로 둔다.
            var clipName = Path.GetFileNameWithoutExtension(assetPath);
            var loop = LoopClipNames.Contains(clipName);
            var existing = importer.clipAnimations;
            var clips = importer.defaultClipAnimations;
            for (var i = 0; i < clips.Length; i++)
            {
                clips[i].name = clips.Length == 1 ? clipName : $"{clipName}_{i}";
                var previous = existing.FirstOrDefault(c => c.name == clips[i].name);
                clips[i].loopTime = previous != null ? previous.loopTime : loop;
            }
            importer.clipAnimations = clips;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsRobotModel(assetPath) || !IsAnimationFile(assetPath)) return;

            // 애니메이션 FBX에 포함된 메시는 쓰지 않는다.
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh != null)
                    UnityEngine.Object.DestroyImmediate(skinned.sharedMesh, true);
                UnityEngine.Object.DestroyImmediate(skinned, true);
            }
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                    UnityEngine.Object.DestroyImmediate(filter.sharedMesh, true);
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null)
                    UnityEngine.Object.DestroyImmediate(renderer, true);
                UnityEngine.Object.DestroyImmediate(filter, true);
            }
        }

        static bool IsRobotModel(string path) =>
            path.StartsWith(RobotRoot, StringComparison.Ordinal) &&
            path.EndsWith(ModelExtension, StringComparison.OrdinalIgnoreCase);

        static bool IsAnimationFile(string path) =>
            path.StartsWith(AnimationRoot, StringComparison.Ordinal);
    }
}
