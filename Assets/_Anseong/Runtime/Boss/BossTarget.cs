using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>보스 하나의 맞는 부위 목록. 전투 판정이 시작할 때 한 번 찾는다. 메뉴가 보스 프리팹 루트에 붙이고 채운다.</summary>
    public class BossTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("맞는 부위들. 메뉴가 채운다. 비어 있으면 시작할 때 자식에서 한 번 찾는다.")]
        BossHurtZone[] zones = new BossHurtZone[0];

        public BossHurtZone[] Zones
        {
            get
            {
                if (zones == null || zones.Length == 0) zones = GetComponentsInChildren<BossHurtZone>(true);
                return zones;
            }
        }

        /// <summary>point에 가장 가까운 활성 부위와 표면 점. 없으면 false.</summary>
        public bool TryClosest(Vector3 point, out BossHurtZone zone, out Vector3 surface)
        {
            zone = null;
            surface = point;
            var best = float.PositiveInfinity;
            foreach (var z in Zones)
            {
                if (z == null || !z.IsActive) continue;
                var p = z.ClosestPoint(point);
                var d = (p - point).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                zone = z;
                surface = p;
            }
            return zone != null;
        }
    }
}
