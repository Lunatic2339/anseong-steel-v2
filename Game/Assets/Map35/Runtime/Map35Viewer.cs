using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.Map35
{
    public sealed class Map35Viewer : MonoBehaviour
    {
        public Camera[] views;
        public GameObject routes, labels, frusta, reservations;
        public Transform plate, coil, walker;
        public GameObject lateCoil;
        int selected; bool lateState, touring;
        float routeDistance; GUIStyle text;
        void Start(){Application.runInBackground=true;Select(0);}
        public void Select(int i)
        {
            selected=Mathf.Clamp(i,0,views.Length-1);
            for(int n=0;n<views.Length;n++)views[n].enabled=n==selected;
            FaceLabels();
        }
        public void FaceLabels()
        {
            if(labels==null)return;
            foreach(var t in labels.GetComponentsInChildren<TextMesh>(true))
            {
                t.transform.rotation=views[selected].transform.rotation;
                t.GetComponent<Renderer>().enabled=selected>=2 || (t.text.StartsWith("B")&&!t.text.StartsWith("Boss")) || t.text.StartsWith("Z") || t.text.StartsWith("K") || t.text.StartsWith("OPTIONAL");
            }
        }
        public void SetLateState(bool value)
        {
            lateState=value;plate.localPosition=new Vector3(value?-22:0,24,8);
            coil.gameObject.SetActive(!value);lateCoil.SetActive(value);
        }
        void Update()
        {
            walker.gameObject.SetActive(touring);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--map-smoke")>=0 && Time.timeSinceLevelLoad>3)
            {Debug.Log("MAP35_VIEWER_READY views="+views.Length+" route="+Map35Layout.Length(Map35Layout.Mech));Application.Quit(0);}
            var k=Keyboard.current;var m=Mouse.current;
            if(k==null)return;
            if(k.tabKey.wasPressedThisFrame)Select((selected+1)%views.Length);
            if(k.rKey.wasPressedThisFrame)routes.SetActive(!routes.activeSelf);
            if(k.lKey.wasPressedThisFrame)labels.SetActive(!labels.activeSelf);
            if(k.fKey.wasPressedThisFrame)frusta.SetActive(!frusta.activeSelf);
            if(k.vKey.wasPressedThisFrame)reservations.SetActive(!reservations.activeSelf);
            if(k.pKey.wasPressedThisFrame)SetLateState(!lateState);
            if(k.spaceKey.wasPressedThisFrame)touring=!touring;
            if(touring)
            {
                routeDistance=(routeDistance+Time.unscaledDeltaTime*18)%Map35Layout.Length(Map35Layout.Mech);
                float remaining=routeDistance;
                for(int i=1;i<Map35Layout.Mech.Length;i++)
                {
                    float length=Vector3.Distance(Map35Layout.Mech[i-1],Map35Layout.Mech[i]);
                    if(remaining<=length){walker.position=Vector3.Lerp(Map35Layout.Mech[i-1],Map35Layout.Mech[i],remaining/length);break;}
                    remaining-=length;
                }
            }
            // Free review is confined to the overview camera. This is a map viewer, not VR locomotion.
            if(selected==0 && m!=null && m.rightButton.isPressed)
            {
                var t=views[0].transform;var delta=m.delta.ReadValue();var angles=t.eulerAngles;
                t.rotation=Quaternion.Euler(angles.x-delta.y*.12f,angles.y+delta.x*.12f,0);
                var move=t.forward*((k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0))+t.right*((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0))+Vector3.up*((k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0));
                t.position+=move*Time.unscaledDeltaTime*(k.leftShiftKey.isPressed?160:55);FaceLabels();
            }
        }
        void OnGUI()
        {
            if(text==null){GUI.skin.font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",15);text=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true};}
            GUI.Box(new Rect(16,16,280,Mathf.Min(Screen.height-30,700)),"");
            GUILayout.BeginArea(new Rect(28,26,255,Screen.height-55));
            GUILayout.Label("안성제철 / 35m · 3구역 맵",text);
            GUILayout.Label("공간 검토용 · 대전/VR 실행 아님\n청록: 메카 / 주황: 드론 / 보라: 보스",text);
            for(int i=0;i<views.Length;i++)if(GUILayout.Button((i==selected?"● ":"")+views[i].name,GUILayout.Height(23)))Select(i);
            if(GUILayout.Button("R 동선 표시"))routes.SetActive(!routes.activeSelf);
            if(GUILayout.Button("L 표식 표시"))labels.SetActive(!labels.activeSelf);
            if(GUILayout.Button("F 관제 화각"))frusta.SetActive(!frusta.activeSelf);
            if(GUILayout.Button("V 공간 예약 범위"))reservations.SetActive(!reservations.activeSelf);
            if(GUILayout.Button("P 판 후퇴 / 코일 정착"))SetLateState(!lateState);
            if(GUILayout.Button("Space 경로 추적 표식"))touring=!touring;
            GUILayout.Label("전체 보기: 우클릭+WASD / Q·E 높이\nShift 빠르게 · Tab 시점 순환\n경로 추적은 충돌/이동 게임 구현이 아닙니다.",text);
            GUILayout.EndArea();
        }
    }
}
