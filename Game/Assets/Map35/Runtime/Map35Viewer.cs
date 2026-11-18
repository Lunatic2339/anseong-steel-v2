using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public Vector3[] mechRoute;
        public MapScenarioPreview scenario;
        public string sceneTitle="안성제철 / 35m · 3구역 맵";
        Vector3[] Route=>mechRoute!=null && mechRoute.Length>1?mechRoute:Map35Layout.Mech;
        Vector2 cameraScroll;
        int selected; bool lateState, touring;
        float routeDistance; GUIStyle text;
        bool automatedArtAudit;
        void Start()
        {
            Application.runInBackground=true;if(scenario!=null)scenario.SelectStep(scenario.selectedStep);else Select(0);
            var args=Environment.GetCommandLineArgs();int audit=Array.IndexOf(args,"--map-art-audit");
            if(audit>=0&&audit+1<args.Length){automatedArtAudit=true;StartCoroutine(ArtAudit(args[audit+1]));}
        }
        IEnumerator ArtAudit(string output)
        {
            Directory.CreateDirectory(output);QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var report=new StringBuilder("view,samples,median_frame_ms,p95_frame_ms,screen_width,screen_height\n");
            foreach(int index in new[]{2,3,4,5,6,7,9,12,17,20,28,30,32,34,35,36,37,38,39,40,42,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63})
            {
                if(index>=views.Length)continue;Select(index);labels.SetActive(false);routes.SetActive(false);frusta.SetActive(false);reservations.SetActive(false);
                for(int warm=0;warm<45;warm++)yield return null;
                var samples=new List<float>();for(int sample=0;sample<120;sample++){yield return null;samples.Add(Time.unscaledDeltaTime*1000);}
                samples.Sort();report.AppendLine(index+","+samples.Count+","+samples[60].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+samples[114].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+Screen.width+","+Screen.height);
                var camera=views[index];var target=new RenderTexture(1600,1000,24);camera.targetTexture=target;camera.aspect=1.6f;camera.Render();
                var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,"runtime-"+index.ToString("00")+".png"),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;Destroy(image);Destroy(target);
            }
            File.WriteAllText(Path.Combine(output,"desktop-frame-samples.csv"),report.ToString());
            File.WriteAllText(Path.Combine(output,"device.txt"),SystemInfo.graphicsDeviceName+"\n"+SystemInfo.graphicsDeviceType+"\n"+SystemInfo.processorType+"\nUncapped desktop development player; not HMD, GPU-time, or multiplayer validation.\n");
            Debug.Log("MAP35_ART_AUDIT_OK views="+views.Length+" route="+Map35Layout.Length(Route));Application.Quit(0);
        }
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
            var interlock=GameObject.Find("I1 fixed rail and role equipment");
            if(interlock!=null)
            {
                foreach(var label in interlock.GetComponentsInChildren<TextMesh>())label.text=value?"RELEASED":"LOAD HOLD";
                foreach(var lamp in interlock.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="I1 PC4 held load amber bar"))
                {
                    var block=new MaterialPropertyBlock();lamp.GetPropertyBlock(block);
                    var color=value?new Color(.12f,.65f,.38f):new Color(.9f,.42f,.06f);
                    block.SetColor("_Color",color);block.SetColor("_EmissionColor",color*1.5f);lamp.SetPropertyBlock(block);
                }
            }
        }
        void Update()
        {
            walker.gameObject.SetActive(touring);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--map-smoke")>=0 && Time.timeSinceLevelLoad>3)
            {Debug.Log("MAP35_VIEWER_READY views="+views.Length+" route="+Map35Layout.Length(Route));Application.Quit(0);}
            var k=Keyboard.current;var m=Mouse.current;
            if(k==null)return;
            if(k.tabKey.wasPressedThisFrame)Select((selected+1)%views.Length);
            if(k.rKey.wasPressedThisFrame)routes.SetActive(!routes.activeSelf);
            if(k.lKey.wasPressedThisFrame)labels.SetActive(!labels.activeSelf);
            if(k.fKey.wasPressedThisFrame)frusta.SetActive(!frusta.activeSelf);
            if(k.vKey.wasPressedThisFrame)reservations.SetActive(!reservations.activeSelf);
            if(k.pKey.wasPressedThisFrame&&scenario==null)SetLateState(!lateState);
            if(k.spaceKey.wasPressedThisFrame)touring=!touring;
            if(touring)
            {
                routeDistance=(routeDistance+Time.unscaledDeltaTime*18)%Map35Layout.Length(Route);
                float remaining=routeDistance;
                for(int i=1;i<Route.Length;i++)
                {
                    float length=Vector3.Distance(Route[i-1],Route[i]);
                    if(remaining<=length){walker.position=Vector3.Lerp(Route[i-1],Route[i],remaining/length);break;}
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
            if(automatedArtAudit||(scenario!=null&&scenario.IsAuditing))return;
            if(text==null){GUI.skin.font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",15);text=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true};}
            GUI.Box(new Rect(16,16,280,Screen.height-30),"");
            GUILayout.BeginArea(new Rect(28,26,255,Screen.height-55));
            GUILayout.Label(sceneTitle,text);
            GUILayout.Label("공간 검토용 · 대전/VR 실행 아님\n청록: 메카 / 주황: 드론 / 보라: 보스",text);
            cameraScroll=GUILayout.BeginScrollView(cameraScroll,GUILayout.Height(Mathf.Clamp(Screen.height-350,160,460)));
            for(int i=0;i<views.Length;i++)if(GUILayout.Button((i==selected?"● ":"")+views[i].name,GUILayout.Height(23)))Select(i);
            GUILayout.EndScrollView();
            if(GUILayout.Button("R 동선 표시"))routes.SetActive(!routes.activeSelf);
            if(GUILayout.Button("L 표식 표시"))labels.SetActive(!labels.activeSelf);
            if(GUILayout.Button("F 관제 화각"))frusta.SetActive(!frusta.activeSelf);
            if(GUILayout.Button("V 공간 예약 범위"))reservations.SetActive(!reservations.activeSelf);
            if(scenario==null&&GUILayout.Button("P 판 후퇴 / 코일 정착"))SetLateState(!lateState);
            if(scenario!=null&&GUILayout.Button("장면별 검토 패널"))scenario.showPanel=!scenario.showPanel;
            if(GUILayout.Button("Space 경로 추적 표식"))touring=!touring;
            GUILayout.Label("전체 보기: 우클릭+WASD / Q·E 높이\nShift 빠르게 · Tab 시점 순환\n경로 추적은 충돌/이동 게임 구현이 아닙니다.",text);
            GUILayout.EndArea();
        }
    }
}
