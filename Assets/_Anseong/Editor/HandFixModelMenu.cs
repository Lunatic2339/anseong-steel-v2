using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 손가락 관절을 고친 로봇 모델(BossUpdatedCompatible_HandFix.fbx, 11/13)을 쓰게 한다.
    /// 원본 모델은 손가락 뼈 회전 중심이 실제 마디 이음매에서 벗어나 있고 뼈 축이 손가락마다 달라, 굽히면 조각이 떨어지거나 파고들었다(선우 11/13).
    /// 고친 모델은 Blender로 손가락 뼈만 옮기고 축을 맞춘 것이다(뼈 이름·계층·나머지 뼈·메시·재질은 원본과 같음).
    /// 이 메뉴는 원본의 가져오기 설정을 복사하고, Robot Art Settings의 플레이어·보스 모델 칸을 새 모델로 채운다. 되돌리려면 'Use Original Robot Model'.
    /// </summary>
    public static class HandFixModelMenu
    {
        const string LogPrefix = "[HandFixModel]";
        public const string HandFixPath = "Assets/_Anseong/Art/Robot/Source/BossUpdatedCompatible_HandFix.fbx";

        [MenuItem("Anseong/Art/Use Hand-Fixed Robot Model")]
        public static void UseHandFix() => Apply(HandFixPath);

        [MenuItem("Anseong/Art/Use Original Robot Model")]
        public static void UseOriginal() => Apply(null);

        static void Apply(string path)
        {
            GameObject model = null;
            if (path != null)
            {
                CopyImportSettings(RobotArtSettings.DefaultModelPath, path);
                model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    Debug.LogError($"{LogPrefix} 모델이 없습니다: {path}");
                    return;
                }
            }
            // 설정 에셋이 없으면(지금까지는 기본값만 썼다) 만든다. 임시 인스턴스에 넣으면 저장되지 않는다.
            var settings = AssetDatabase.LoadAssetAtPath<RobotArtSettings>(RobotArtSettings.AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RobotArtSettings>();
                AssetDatabase.CreateAsset(settings, RobotArtSettings.AssetPath);
            }
            var s = new SerializedObject(settings);
            s.FindProperty("playerModel").objectReferenceValue = model;
            s.FindProperty("bossModel").objectReferenceValue = model;
            s.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} 로봇 모델: {(path ?? RobotArtSettings.DefaultModelPath)}. 'Anseong → Build Full Game'으로 다시 조립하세요.");
        }

        /// <summary>원본 FBX의 가져오기 설정(뼈대 종류·재질·크기)을 새 FBX에 맞춘다.</summary>
        internal static void CopyImportSettings(string from, string to)
        {
            var src = AssetImporter.GetAtPath(from) as ModelImporter;
            var dst = AssetImporter.GetAtPath(to) as ModelImporter;
            if (src == null || dst == null) return;
            dst.animationType = src.animationType;
            dst.avatarSetup = src.avatarSetup;
            dst.importAnimation = src.importAnimation;
            dst.materialImportMode = src.materialImportMode;
            dst.materialLocation = src.materialLocation;
            dst.globalScale = src.globalScale;
            dst.useFileScale = src.useFileScale;
            dst.importBlendShapes = src.importBlendShapes;
            dst.importCameras = false;
            dst.importLights = false;
            dst.SaveAndReimport();
        }
    }
}
