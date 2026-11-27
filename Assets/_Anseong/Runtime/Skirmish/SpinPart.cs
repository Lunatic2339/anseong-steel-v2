using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>헬기의 로터처럼 제자리에서 도는 부품. 축은 기준 틀(헬기 몸) 기준으로 준다.</summary>
    public class SpinPart : MonoBehaviour
    {
        [SerializeField, Tooltip("축의 기준이 되는 틀(헬기 몸).")] Transform frame;
        [SerializeField, Tooltip("기준 틀에서 본 회전축.")] Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("도는 빠르기(°/초). 음수면 반대로 돈다.")] float degreesPerSecond = 1400f;

        public void Bind(Transform frameRoot, Vector3 axisInFrame, float speed)
        {
            frame = frameRoot; axis = axisInFrame; degreesPerSecond = speed;
        }

        void Update()
        {
            var world = frame != null ? frame.TransformDirection(axis) : axis;
            transform.Rotate(world, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}
