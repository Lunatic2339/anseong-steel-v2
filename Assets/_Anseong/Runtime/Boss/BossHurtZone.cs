using Anseong.Combat;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 맞는 부위 하나(몸통·머리). 같은 오브젝트의 트리거 콜라이더(박스·캡슐·구) 표면으로 주먹 접촉과 공격 보정 목표를 잰다.
    /// Build Player Robot Prefab 메뉴가 보스 뼈에 붙인다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossHurtZone : MonoBehaviour
    {
        [SerializeField, Tooltip("부위. 명중 보고에 실린다.")]
        BossZone zone = BossZone.Body;

        [SerializeField, Tooltip("이 부위 표면 콜라이더. 비어 있으면 같은 오브젝트에서 한 번 찾는다.")]
        Collider surface;

        public BossZone Zone => zone;

        public Collider Surface
        {
            get
            {
                if (surface == null) surface = GetComponent<Collider>();
                return surface;
            }
        }

        public bool IsActive => isActiveAndEnabled && Surface != null && Surface.enabled;

        /// <summary>표면에서 point에 가장 가까운 점(월드). 안에 있으면 point 그대로.</summary>
        public Vector3 ClosestPoint(Vector3 point) => Surface.ClosestPoint(point);

        public Bounds Bounds => Surface.bounds;
    }
}
