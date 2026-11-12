using System;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>
    /// 전투장 둘레의 큰 제철소 구조물(고로·크레인·가스 탱크·굴뚝). 맵 건물이 단색 상자뿐이라(11/13 자동 점검) 텍스처 있는 큰 모델을 둘레에 세운다.
    /// 겉모습만 있다(충돌 없음). 표는 메뉴(Fit AI Models)가 채운다. 자리는 이 오브젝트 기준(m), 크기는 가장 긴 쪽(m).
    /// </summary>
    public class SceneryDressing : MonoBehaviour
    {
        [Serializable]
        public class Piece
        {
            public string label = "구조물";
            [Tooltip("모델(가장 긴 쪽 1 m, 원점 = 바닥 가운데).")] public GameObject prefab;
            [Tooltip("자리(이 오브젝트 기준 m, y = 바닥 높이).")] public Vector3 position;
            [Tooltip("세로축 회전(도).")] public float yaw;
            [Min(1f), Tooltip("가장 긴 쪽 크기(m).")] public float size = 80f;
        }

        [SerializeField, Tooltip("구조물 표. 메뉴 'Fit AI Models'가 채운다.")] Piece[] pieces = new Piece[0];

        void Start()
        {
            if (pieces == null) return;
            foreach (var piece in pieces)
            {
                if (piece == null || piece.prefab == null) continue;
                var go = Instantiate(piece.prefab, transform);
                go.name = "Scenery · " + piece.label;
                go.transform.localPosition = piece.position;
                go.transform.localRotation = Quaternion.Euler(0f, piece.yaw, 0f);
                go.transform.localScale = Vector3.one * piece.size;
            }
        }
    }
}
