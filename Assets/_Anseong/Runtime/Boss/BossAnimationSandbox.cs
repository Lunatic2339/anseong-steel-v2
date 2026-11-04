#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Anseong.Boss
{
    /// <summary>Local animation inspection only; no combat or network authority.</summary>
    public sealed class BossAnimationSandbox : MonoBehaviour
    {
        [Serializable] public class Motion { public string state; public float duration; public bool loop; public Vector3 viewCenter; public float viewDistance = 2.2f; }
        [Serializable] public class Catalogue { public Motion[] motions; }
        [SerializeField, Tooltip("The final textured boss animator.")] Animator animator;
        [SerializeField, Tooltip("Animation selection interface.")] UIDocument document;
        [SerializeField, Tooltip("Preview camera.")] Camera viewCamera;
        [SerializeField, Tooltip("Center of the model used for camera orbit.")] Transform focus;
        [SerializeField, Tooltip("Right wrist used by the hand close-up view.")] Transform rightWrist;
        [SerializeField, Tooltip("Middle fingertip used to center the hand view.")] Transform rightFinger;
        [SerializeField, Tooltip("Available individually selectable animations.")] Motion[] motions;
        [SerializeField, Tooltip("Playback speed multiplier."), Range(.1f, 2f)] float speed = 1;
        [SerializeField, Tooltip("Preview model height in metres.")] float modelHeight = 65;
        int[] stateHashes;
        int selected;
        bool paused, looping, refreshing;
        float yaw = 12, pitch = 6, distance = 2.2f;
        int cameraMode;
        Label nowPlaying;
        Slider timeline;
        Toggle loopToggle;
        Button pauseButton;
        public int MotionCount => motions?.Length ?? 0;
        public Animator Animator => animator;
        public string CurrentMotion => motions[selected].state;
        public bool IsPaused => paused;
        public void Configure(Animator actor, UIDocument ui, Camera camera, Transform center, Transform wrist, Transform finger, Motion[] list, float height)
        { animator = actor; document = ui; viewCamera = camera; focus = center; rightWrist = wrist; rightFinger = finger; motions = list; modelHeight = height; }
        void OnEnable()
        {
            if (!animator || !document || motions == null || motions.Length == 0) return;
            stateHashes = new int[motions.Length];
            for (int i = 0; i < motions.Length; i++) stateHashes[i] = UnityEngine.Animator.StringToHash(motions[i].state);
            BuildInterface();
            SelectMotion(0);
            SetCameraView(0);
        }
        public void SelectMotion(int index)
        {
            if (stateHashes == null || index < 0 || index >= motions.Length) return;
            selected = index; paused = false; looping = motions[index].loop;
            if (cameraMode == 0) distance = motions[index].viewDistance;
            animator.Play(stateHashes[index], 0, 0); animator.Update(0); animator.speed = speed;
            loopToggle?.SetValueWithoutNotify(looping);
            if (pauseButton != null) pauseButton.text = "Pause";
            if (nowPlaying != null) nowPlaying.text = LabelFor(motions[index].state);
        }
        public void SetPaused(bool value) { paused = value; animator.speed = paused ? 0 : speed; if (pauseButton != null) pauseButton.text = paused ? "Resume" : "Pause"; }
        public void Seek(float normalizedTime)
        {
            SetPaused(true); animator.Play(stateHashes[selected], 0, Mathf.Clamp(normalizedTime, 0, .99999f)); animator.Update(0);
        }
        public void SetCameraView(int mode)
        {
            cameraMode = mode;
            if (mode == 0) { yaw = 12; pitch = 6; distance = motions[selected].viewDistance; }
            else if (mode == 1) { yaw = 0; pitch = -5; distance = 1.4f; }
            else { yaw = 25; pitch = 0; distance = .4f; }
            PositionCamera();
        }
        void Update()
        {
            if (stateHashes == null) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!paused && state.normalizedTime >= 1 && !motions[selected].loop)
            {
                if (looping) { animator.Play(stateHashes[selected], 0, state.normalizedTime % 1); }
                else { animator.Play(stateHashes[selected], 0, .99999f); animator.Update(0); SetPaused(true); }
            }
            else if (!paused && !looping && state.normalizedTime >= 1) { animator.Play(stateHashes[selected], 0, .99999f); animator.Update(0); SetPaused(true); }
            refreshing = true;
            timeline?.SetValueWithoutNotify(looping ? state.normalizedTime % 1 : Mathf.Clamp01(state.normalizedTime));
            refreshing = false;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            { var delta = mouse.delta.ReadValue(); yaw += delta.x * .15f; pitch = Mathf.Clamp(pitch - delta.y * .15f, -65, 65); }
            PositionCamera();
        }
        void PositionCamera()
        {
            if (!viewCamera || !focus) return;
            var point = cameraMode == 2 && rightWrist ? Vector3.Lerp(rightWrist.position, rightFinger ? rightFinger.position : rightWrist.position, .6f) : focus.position;
            if (cameraMode == 0) point = motions[selected].viewCenter;
            if (cameraMode == 1) point += Vector3.up * modelHeight * .12f;
            viewCamera.transform.position = point + Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * modelHeight * distance;
            viewCamera.transform.LookAt(point);
        }
        static string LabelFor(string state)
        {
            return state.Replace("Combat_", "").Replace("SwordIdle", "Sword idle").Replace("DrawSword", "Draw sword")
                .Replace("SheatheSword", "Sheathe sword").Replace("WalkBackward", "Walk backward")
                .Replace("TurnLeft", "Turn left").Replace("TurnRight", "Turn right").Replace("OverheadSmash", "Overhead smash")
                .Replace("AlternatingCombo", "Punch combo").Replace("SwordSlash", "Sword slash").Replace("WeaponBreak", "Weapon break")
                .Replace("ArmLock", "Arm lock").Replace("SensorJam", "Sensor jam").Replace("VisionDisruption", "Vision disruption");
        }
        void BuildInterface()
        {
            var root = document.rootVisualElement; root.Clear(); root.pickingMode = PickingMode.Ignore;
            var panel = new VisualElement { name = "MotionPanel" };
            panel.style.position = Position.Absolute; panel.style.left = 18; panel.style.top = 18; panel.style.bottom = 18;
            panel.style.width = 275; panel.style.paddingLeft = 16; panel.style.paddingRight = 16;
            panel.style.paddingTop = 16; panel.style.paddingBottom = 16;
            panel.style.backgroundColor = new Color(.035f, .045f, .06f, .94f); panel.style.color = Color.white;
            root.Add(panel);
            var title = new Label("BOSS MOTION LAB"); title.style.fontSize = 20; title.style.unityFontStyleAndWeight = FontStyle.Bold; panel.Add(title);
            var subtitle = new Label("V13 final surface / revised hands"); subtitle.style.fontSize = 11; subtitle.style.color = new Color(.7f, .75f, .8f); panel.Add(subtitle);
            nowPlaying = new Label(); nowPlaying.style.fontSize = 17; nowPlaying.style.marginTop = 12; panel.Add(nowPlaying);
            var controls = new VisualElement(); controls.style.flexDirection = FlexDirection.Row; panel.Add(controls);
            pauseButton = MakeButton("Pause", () => SetPaused(!paused)); controls.Add(pauseButton); controls.Add(MakeButton("Restart", () => SelectMotion(selected)));
            timeline = new Slider("Pose", 0, 1); timeline.RegisterValueChangedCallback(e => { if (!refreshing) Seek(e.newValue); }); panel.Add(timeline);
            var rate = new Slider("Speed", .1f, 2f) { value = speed }; rate.RegisterValueChangedCallback(e => { speed = e.newValue; if (!paused) animator.speed = speed; }); panel.Add(rate);
            loopToggle = new Toggle("Loop motion"); loopToggle.RegisterValueChangedCallback(e => looping = e.newValue); panel.Add(loopToggle);
            var views = new VisualElement(); views.style.flexDirection = FlexDirection.Row; panel.Add(views);
            views.Add(MakeButton("Full", () => SetCameraView(0))); views.Add(MakeButton("Eye level", () => SetCameraView(1))); views.Add(MakeButton("Hand", () => SetCameraView(2)));
            var zoom = new VisualElement(); zoom.style.flexDirection = FlexDirection.Row; panel.Add(zoom);
            zoom.Add(MakeButton("Zoom +", () => distance = Mathf.Max(.15f, distance * .85f))); zoom.Add(MakeButton("Zoom -", () => distance = Mathf.Min(4, distance / .85f)));
            var hint = new Label("Right-drag to orbit  |  Select a motion"); hint.style.fontSize = 10; hint.style.marginTop = 8; hint.style.marginBottom = 8; panel.Add(hint);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; panel.Add(scroll);
            for (int i = 0; i < motions.Length; i++) { int index = i; var button = MakeButton(LabelFor(motions[i].state), () => SelectMotion(index)); button.name = "Motion_" + motions[i].state; scroll.Add(button); }
        }
        static Button MakeButton(string text, Action action)
        {
            var button = new Button(action) { text = text }; button.style.minHeight = 28; button.style.flexGrow = 1;
            button.style.backgroundColor = new Color(.12f, .16f, .21f); button.style.color = Color.white;
            button.style.marginTop = 3; button.style.marginBottom = 3; return button;
        }
    }
}
#endif
