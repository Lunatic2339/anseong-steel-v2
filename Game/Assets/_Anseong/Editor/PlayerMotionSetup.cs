using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// M3-03: 플레이어 로봇 전용 Animator(대기 + 다리 층 걷기). 협동 이동 중 RobotMotionSync가 "Walking"을 켠다.
    /// 다리 층은 다리 뼈만 움직인다(골반·몸통·머리는 대기 그대로): 조종석이 머리 뼈를 따라가므로 걷기 흔들림이 카메라로 오지 않게.
    /// 옆걸음(회피) 클립은 M4에서 바꾼다. Build Player Robot Prefab이 부른다.
    /// </summary>
    public static class PlayerMotionSetup
    {
        const string LogPrefix = "[PlayerMotionSetup]";
        public const string PlayerControllerPath = "Assets/_Anseong/Art/Robot/Animation/Robot_Player.controller";
        const string LegsMaskPath = "Assets/_Anseong/Art/Robot/Animation/Robot_Player_Legs.mask";
        const string WalkClipPath = "Assets/_Anseong/Art/Robot/Animation/Source/Walk.fbx";
        const string WalkingParameter = "Walking";
        const float BlendSeconds = 0.15f;
        // 다리 층에 넣을 뼈 이름(Mixamo). 골반(Hips)은 넣지 않는다.
        static readonly string[] LegBoneKeys = { "UpLeg", "Leg", "Foot", "ToeBase", "Toe_End" };

        /// <summary>플레이어 로봇 Animator를 전용 컨트롤러로 바꾼다. 걷기 클립이 없으면 대기 컨트롤러 그대로 두고 이유를 돌려준다.</summary>
        public static string Apply(Transform player, AnimationClip idleClip)
        {
            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null) return "Animator 없음(걷기 없이)";
            var walk = RobotArtSettings.ClipIn(WalkClipPath);
            if (walk == null || idleClip == null) return $"걷기 클립 없음({WalkClipPath}), 대기만";

            var mask = BuildLegsMask(animator.transform, out var legCount);
            if (legCount == 0) return "다리 뼈를 못 찾음(걷기 없이)";

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath) != null) AssetDatabase.DeleteAsset(PlayerControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
            controller.AddParameter(WalkingParameter, AnimatorControllerParameterType.Bool);
            var baseMachine = controller.layers[0].stateMachine;
            var baseIdle = baseMachine.AddState("Idle");
            baseIdle.motion = idleClip;
            baseMachine.defaultState = baseIdle;

            controller.AddLayer("Legs");
            var layers = controller.layers;
            var legs = layers[1];
            legs.avatarMask = mask;
            legs.defaultWeight = 1f;
            legs.blendingMode = AnimatorLayerBlendingMode.Override;
            var machine = legs.stateMachine;
            var idle = machine.AddState("Idle");
            idle.motion = idleClip;
            machine.defaultState = idle;
            var walking = machine.AddState("Walk");
            walking.motion = walk;
            var toWalk = idle.AddTransition(walking);
            toWalk.hasExitTime = false;
            toWalk.duration = BlendSeconds;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, WalkingParameter);
            var toIdle = walking.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = BlendSeconds;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, WalkingParameter);
            controller.layers = layers;   // 층 설정(마스크·가중치)은 배열을 다시 넣어야 저장된다.
            AssetDatabase.SaveAssets();

            animator.runtimeAnimatorController = controller;
            return $"전용 컨트롤러(대기 + 다리 걷기, 다리 뼈 {legCount}개)";
        }

        static AvatarMask BuildLegsMask(Transform animatorRoot, out int legCount)
        {
            var paths = new List<string>();
            var active = new List<bool>();
            legCount = 0;
            paths.Add(string.Empty);   // Animator 루트 자신
            active.Add(false);
            Walk(animatorRoot, string.Empty, paths, active, ref legCount);
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(LegsMaskPath) != null) AssetDatabase.DeleteAsset(LegsMaskPath);
            var mask = new AvatarMask();
            for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.transformCount = paths.Count;
            for (var i = 0; i < paths.Count; i++)
            {
                mask.SetTransformPath(i, paths[i]);
                mask.SetTransformActive(i, active[i]);
            }
            AssetDatabase.CreateAsset(mask, LegsMaskPath);
            return mask;
        }

        static void Walk(Transform t, string path, List<string> paths, List<bool> active, ref int legCount)
        {
            foreach (Transform child in t)
            {
                var childPath = string.IsNullOrEmpty(path) ? child.name : path + "/" + child.name;
                var isLeg = IsLegBone(child.name);
                if (isLeg) legCount++;
                paths.Add(childPath);
                active.Add(isLeg);
                Walk(child, childPath, paths, active, ref legCount);
            }
        }

        static bool IsLegBone(string name)
        {
            var colon = name.LastIndexOf(':');
            var bare = colon >= 0 ? name.Substring(colon + 1) : name;
            foreach (var key in LegBoneKeys)
                if (bare.StartsWith("Left" + key) || bare.StartsWith("Right" + key)) return true;
            return false;
        }
    }
}
