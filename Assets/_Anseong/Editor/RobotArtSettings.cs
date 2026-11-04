using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 로봇·조종석 아트를 바꿔 끼우는 곳. 프리팹·샌드박스 메뉴가 읽는다.
    /// 칸이 비어 있으면 지금 쓰는 기본값(BossUpdatedCompatible.fbx, 상자 조종석, 보스 단색)으로 만든다.
    /// 바꾼 뒤에는 'Anseong → Setup → Build Player Robot Prefab'을 다시 실행한다.
    /// </summary>
    public class RobotArtSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Anseong/Editor/RobotArtSettings.asset";
        public const string DefaultModelPath = "Assets/_Anseong/Art/Robot/Source/BossUpdatedCompatible.fbx";
        public const string DefaultStaggerPath = "Assets/_Anseong/Art/Robot/Animation/Source/Stagger.fbx";

        [Serializable]
        public struct SlotMaterial
        {
            [Tooltip("재질 슬롯 이름(04_art_spec 공용 재질 슬롯, 예: ArmorMain). 모델 재질 이름이 이것과 같으면 바꾼다. Blender의 .001 접미사와 대소문자는 무시한다.")]
            public string slot;
            [Tooltip("그 슬롯에 넣을 재질.")]
            public Material material;
        }

        [SerializeField, Tooltip("플레이어 로봇 모델 FBX. 비우면 BossUpdatedCompatible.fbx. Mixamo 뼈 이름·계층·개수, 단위 m, Armature 스케일 1, 정면 +Z를 유지해야 한다.")]
        GameObject playerModel;

        [SerializeField, Tooltip("보스 로봇 모델 FBX. 비우면 플레이어 모델과 같은 파일을 쓴다.")]
        GameObject bossModel;

        [SerializeField, Tooltip("조종석 모델 프리팹. 비우면 회색 상자로 만든다. 원점 = 두 좌석 사이 바닥 중심, +Z = 앞, +Y = 위, 단위 m(사람 크기). " +
                                 "좌석은 원점에서 좌우 ±1.05 m(SeatSpacing 2.1 m의 절반).")]
        GameObject cockpitPrefab;

        [SerializeField, Tooltip("보스가 맞았을 때 휘청 클립(M1-05). 비우면 Art/Robot/Animation/Source/Stagger.fbx. 같은 Mixamo 뼈 이름이어야 한다.")]
        AnimationClip bossStaggerClip;

        [SerializeField, Tooltip("보스 쓰러짐 클립(M2, 옛 레포 CombatSource/Death). 비우면 쓰러질 때 대기 자세로 멈춘다.")]
        AnimationClip bossDeathClip;

        [SerializeField, Tooltip("보스 걷기 클립(M2-04). 비우면 Art/Robot/Animation/Source/Walk.fbx.")]
        AnimationClip bossWalkClip;

        [SerializeField, Tooltip("보스 페이즈 전환 포효 클립(M2-04). 비우면 대기 자세.")]
        AnimationClip bossRoarClip;

        [SerializeField, Tooltip("보스 크게 밀려남 클립(A-01 Boss_Knockback). 비우면 휘청(Stagger) 전체.")]
        AnimationClip bossKnockbackClip;

        [SerializeField, Tooltip("보스 무릎 클립(A-01 Boss_Kneel). 비우면 대기 자세로 멈춤.")]
        AnimationClip bossKneelClip;

        [SerializeField, Tooltip("플레이어 재질 슬롯. 맞는 이름의 재질만 바꾸고 나머지는 모델 재질 그대로 둔다. 비우면 모델 재질 그대로.")]
        SlotMaterial[] playerSlotMaterials = new SlotMaterial[0];

        [SerializeField, Tooltip("보스 재질 슬롯. 맞는 이름의 재질만 바꾸고 나머지는 보스 단색(Boss_Tint)으로 칠한다. 비우면 전부 Boss_Tint.")]
        SlotMaterial[] bossSlotMaterials = new SlotMaterial[0];

        public GameObject PlayerModel => playerModel != null ? playerModel : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);
        public GameObject BossModel => bossModel != null ? bossModel : PlayerModel;
        public GameObject CockpitPrefab => cockpitPrefab;
        public AnimationClip BossDeathClip => bossDeathClip;
        public AnimationClip BossWalkClip => bossWalkClip != null ? bossWalkClip : ClipIn("Assets/_Anseong/Art/Robot/Animation/Source/Walk.fbx");
        public AnimationClip BossRoarClip => bossRoarClip;
        public AnimationClip BossKnockbackClip => bossKnockbackClip;
        public AnimationClip BossKneelClip => bossKneelClip;
        public AnimationClip BossStaggerClip => bossStaggerClip != null ? bossStaggerClip : ClipIn(DefaultStaggerPath);

        /// <summary>FBX 안의 첫 애니메이션 클립(미리보기 클립 제외).</summary>
        public static AnimationClip ClipIn(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }
        public SlotMaterial[] PlayerSlotMaterials => playerSlotMaterials ?? new SlotMaterial[0];
        public SlotMaterial[] BossSlotMaterials => bossSlotMaterials ?? new SlotMaterial[0];

        /// <summary>모델이 비어 있을 때 대화상자·로그에 보여줄 경로.</summary>
        public string PlayerModelPath => playerModel != null ? AssetDatabase.GetAssetPath(playerModel) : DefaultModelPath;
        public string BossModelPath => bossModel != null ? AssetDatabase.GetAssetPath(bossModel) : PlayerModelPath;

        /// <summary>설정 에셋. 없으면 기본값만 가진 임시 인스턴스(저장하지 않음).</summary>
        public static RobotArtSettings Load()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RobotArtSettings>(AssetPath);
            if (settings != null) return settings;
            settings = CreateInstance<RobotArtSettings>();
            settings.hideFlags = HideFlags.DontSave;
            return settings;
        }

        /// <summary>모델 FBX에서 만든 Avatar. Generic 임포트라 경로가 맞는지 확인용이다.</summary>
        public static Avatar AvatarOf(GameObject model)
        {
            return model == null ? null : AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Avatar>().FirstOrDefault();
        }

        [MenuItem("Anseong/Art/Robot Art Settings")]
        static void SelectOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RobotArtSettings>(AssetPath);
            if (settings == null)
            {
                settings = CreateInstance<RobotArtSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[RobotArtSettings] 설정 에셋 생성: {AssetPath}", settings);
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
