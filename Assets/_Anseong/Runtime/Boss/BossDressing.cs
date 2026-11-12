using System;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 전용 장식(머리 뿔·어깨 장갑·등 가시·가슴 핵 테두리). 보스가 우리 로봇과 같은 모델이라 실루엣이 겹쳐 보여서(11/13 자동 점검)
    /// 뼈에 모델을 덧붙여 구분한다. 겉모습만 바꾸고 판정·무기(<see cref="BossWeaponVisuals"/>)와는 무관하다.
    /// 조각 표는 메뉴(Fit AI Models)가 채운다. 모델 칸이 빈 조각은 건너뛴다. 크기·위치 단위 = 가슴~머리 거리.
    /// </summary>
    public class BossDressing : MonoBehaviour
    {
        [Serializable]
        public class Piece
        {
            [Tooltip("이름(로그·오브젝트 이름).")] public string label = "장식";
            [Tooltip("붙일 뼈.")] public Transform bone;
            [Tooltip("모델(가장 긴 쪽이 1 m가 되게 맞춘 프리팹).")] public GameObject prefab;
            [Tooltip("뼈에서 떨어진 자리(보스 기준 x 오른쪽, y 위, z 앞. 가슴~머리 거리 배수).")] public Vector3 offset;
            [Tooltip("보스 기준 회전(도).")] public Vector3 euler;
            [Min(0.01f), Tooltip("가장 긴 쪽 크기(가슴~머리 거리 배수).")] public float size = 1f;
            [Tooltip("좌우를 뒤집는다(왼쪽 어깨용).")] public bool mirror;
        }

        [SerializeField, Tooltip("가슴 뼈(mixamorig:Spine2).")] Transform chest;
        [SerializeField] Transform head;
        [SerializeField, Tooltip("장식 조각. 메뉴 'Fit AI Models'가 채운다.")] Piece[] pieces = new Piece[0];

        void Start()
        {
            if (pieces == null || pieces.Length == 0) return;
            var unit = chest != null && head != null ? Mathf.Max(1f, Vector3.Distance(chest.position, head.position)) : 10f;
            foreach (var piece in pieces)
            {
                if (piece == null || piece.bone == null || piece.prefab == null) continue;
                var go = Instantiate(piece.prefab);
                go.name = "Boss Dressing · " + piece.label;
                var t = go.transform;
                var s = piece.size * unit;
                t.localScale = new Vector3(piece.mirror ? -s : s, s, s);
                t.SetPositionAndRotation(piece.bone.position + transform.rotation * (piece.offset * unit), transform.rotation * Quaternion.Euler(piece.euler));
                t.SetParent(piece.bone, true);
            }
        }
    }
}
