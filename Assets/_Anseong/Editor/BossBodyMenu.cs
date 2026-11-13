using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 보스 전용 몸(Boss_Body.fbx, 11/13 모델링 스레드)을 쓰게 한다. 보스가 우리 로봇과 같은 모델이라 색만 다른 쌍둥이로 보여서,
    /// 같은 모델에서 머리 뒤 고리를 떼고 뿔·등 날개·어깨 가시·무릎 가시를 붙였다(`tools/blender/build_boss_body.py`).
    /// 뼈 이름·계층·자세는 그대로라 보스 클립은 그대로 쓴다. 붙인 조각은 재질 `BossExtras` 하나(텍스처 `Art/Boss/Extras/`).
    /// 이 메뉴는 Robot Art Settings의 보스 모델 칸과 보스 재질 슬롯(BossExtras)을 채운다. 그 뒤 'Build Full Game'으로 다시 조립한다.
    /// 되돌리려면 'Use Shared Body For Boss'(보스 모델 칸을 플레이어 모델로).
    /// </summary>
    public static class BossBodyMenu
    {
        const string LogPrefix = "[BossBody]";
        public const string BossBodyPath = "Assets/_Anseong/Art/Robot/Source/Boss_Body.fbx";
        const string ExtrasFolder = "Assets/_Anseong/Art/Boss/Extras";
        const string ExtrasSlot = "BossExtras";

        /// <summary>보스 모델 칸이 보스 전용 몸인가.</summary>
        public static bool InUse => RobotArtSettings.Load().BossModelPath == BossBodyPath;

        [MenuItem("Anseong/Art/Use Boss Body Model")]
        public static void Use()
        {
            HandFixModelMenu.CopyImportSettings(RobotArtSettings.DefaultModelPath, BossBodyPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BossBodyPath);
            if (model == null)
            {
                Debug.LogError($"{LogPrefix} 모델이 없습니다: {BossBodyPath}");
                return;
            }
            var material = AiModelFit.BuildAuthoredMaterial(ExtrasFolder, ExtrasSlot, string.Empty);
            Apply(model, material);
            Debug.Log($"{LogPrefix} 보스 모델: {BossBodyPath}. 'Anseong → Build Full Game'으로 다시 조립하세요.");
        }

        [MenuItem("Anseong/Art/Use Shared Body For Boss")]
        public static void UseShared()
        {
            Apply(null, null);
            Debug.Log($"{LogPrefix} 보스 모델을 플레이어 모델과 같게 되돌렸습니다. 'Anseong → Build Full Game'으로 다시 조립하세요.");
        }

        static void Apply(GameObject model, Material extras)
        {
            var settings = AssetDatabase.LoadAssetAtPath<RobotArtSettings>(RobotArtSettings.AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RobotArtSettings>();
                AssetDatabase.CreateAsset(settings, RobotArtSettings.AssetPath);
            }
            var s = new SerializedObject(settings);
            // 되돌릴 때는 플레이어 모델 칸과 같은 것(비어 있으면 비움 = 플레이어 모델을 따라감).
            s.FindProperty("bossModel").objectReferenceValue = model != null ? model : s.FindProperty("playerModel").objectReferenceValue;
            var slots = s.FindProperty("bossSlotMaterials");
            var index = -1;
            for (var i = 0; i < slots.arraySize; i++)
                if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("slot").stringValue == ExtrasSlot) index = i;
            if (extras != null)
            {
                if (index < 0)
                {
                    index = slots.arraySize;
                    slots.arraySize++;
                }
                var e = slots.GetArrayElementAtIndex(index);
                e.FindPropertyRelative("slot").stringValue = ExtrasSlot;
                e.FindPropertyRelative("material").objectReferenceValue = extras;
            }
            else if (index >= 0) slots.DeleteArrayElementAtIndex(index);
            s.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
