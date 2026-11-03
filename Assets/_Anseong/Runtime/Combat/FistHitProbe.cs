using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>로봇 주먹 명중 구(손가락 뿌리 자리). Build Player Robot Prefab 메뉴가 손 뼈 아래에 붙인다. 위치만 쓰고 물리는 쓰지 않는다.</summary>
    public class FistHitProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("어느 팔 주먹인지.")]
        PilotSide side;

        [SerializeField, Min(0.01f), Tooltip("주먹 구 반지름(m, 로봇 크기). 메뉴가 로봇 팔 길이 × 0.06으로 넣는다.")]
        float radius = 1.2f;

        public PilotSide Side => side;
        public float Radius => radius;
        public Vector3 Position => transform.position;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
