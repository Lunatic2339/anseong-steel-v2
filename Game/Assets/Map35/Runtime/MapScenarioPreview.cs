using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.Map35
{
    [Serializable] public sealed class ScenarioView
    {
        public Vector3 position,look;
        public float fov=65;
    }
    [Serializable] public sealed class ScenarioSpatialStep
    {
        public string id,title,vrTask,droneTask,controlTask;
        public GameObject root;
        public ScenarioView[] views;
        public Transform[] targets;
        public string[] targetNames;
    }

    // Authored spatial rehearsal. These states do not implement combat or network authority.
    public sealed class MapScenarioPreview : MonoBehaviour
    {
        public Map35Viewer viewer;
        public ScenarioSpatialStep[] steps;
        public int firstCamera=43;
        public GameObject[] hiddenLegacy;
        public int selectedStep,selectedRole;
        public bool showPanel=true,showTargetOverlay=true;
        public MapScenarioTransit transit;
        int selectedTarget;
        bool auditing;
        public bool IsAuditing=>auditing;
        Vector2 scroll;
        GUIStyle text,small;
        static readonly string[] Roles={"VR 메카","3안 드론","4안 관제","4안 근접"};
        public ScenarioSpatialStep Current=>steps[Mathf.Clamp(selectedStep,0,steps.Length-1)];

        void Start()
        {
            SelectStep(selectedStep);
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--scenario-art-audit");
            if(at>=0&&at+1<args.Length){auditing=true;StartCoroutine(AuditPlayer(args[at+1]));}
        }
        public void SelectStep(int index)
        {
            selectedStep=Mathf.Clamp(index,0,steps.Length-1);selectedTarget=0;
            for(int i=0;i<steps.Length;i++)if(steps[i].root!=null)steps[i].root.SetActive(i==selectedStep);
            foreach(var go in hiddenLegacy)if(go!=null)go.SetActive(false);
            viewer.labels.SetActive(false);viewer.routes.SetActive(false);viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);
            viewer.plate.localPosition=new Vector3(selectedStep==0?0:-22,24,8);
            var s=Current;
            for(int i=0;i<4;i++)
            {
                var c=viewer.views[firstCamera+i];var v=s.views[i];c.transform.position=v.position;
                c.transform.LookAt(v.look);c.fieldOfView=v.fov;c.aspect=1.6f;
            }
            if(transit!=null){transit.enabled=s.id=="transit";if(transit.enabled)transit.SetTime(60);}
            SelectRole(selectedRole);
        }
        public void SelectRole(int role)
        {selectedRole=Mathf.Clamp(role,0,3);viewer.Select(firstCamera+selectedRole);}
        void Update()
        {
            if(auditing)return;var k=Keyboard.current;if(k==null)return;
            if(k.rightBracketKey.wasPressedThisFrame)SelectStep((selectedStep+1)%steps.Length);
            if(k.leftBracketKey.wasPressedThisFrame)SelectStep((selectedStep+steps.Length-1)%steps.Length);
        }
        void OnGUI()
        {
            if(steps==null||steps.Length==0)return;
            if(text==null)
            {
                var font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",16);
                text=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};
                small=new GUIStyle(text){fontSize=13};
            }
            if(showTargetOverlay&&viewer.views[firstCamera+selectedRole].enabled)DrawTargetOverlay();
            if(!showPanel)return;
            float width=330,x=Screen.width-width-16;GUI.Box(new Rect(x,16,width,Screen.height-32),"");
            GUILayout.BeginArea(new Rect(x+12,26,width-24,Screen.height-50));
            GUILayout.Label("안성제철 · 장면별 공간 검토",text);
            GUILayout.Label("배치·자세·시야 시안 / 전투 판정·협동 플레이 아님",small);
            GUILayout.BeginHorizontal();for(int i=0;i<4;i++)if(GUILayout.Button(Roles[i],GUILayout.Height(30)))SelectRole(i);GUILayout.EndHorizontal();
            scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(Mathf.Min(330,Screen.height*.37f)));
            for(int i=0;i<steps.Length;i++)if(GUILayout.Button((i==selectedStep?"● ":"")+steps[i].title,GUILayout.Height(25)))SelectStep(i);
            GUILayout.EndScrollView();
            GUILayout.Label(Current.title,text);
            GUILayout.Label("VR: "+Current.vrTask,small);GUILayout.Label("3안: "+Current.droneTask,small);GUILayout.Label("4안: "+Current.controlTask,small);
            if(selectedRole>=2&&Current.targets!=null)
            {
                GUILayout.Label("관제 대상 선택 시안",text);
                for(int i=0;i<Current.targets.Length;i++)if(Current.targets[i]!=null&&GUILayout.Button((selectedTarget==i?"▶ ":"")+Current.targetNames[i]))selectedTarget=i;
                if(GUILayout.Button(selectedRole==2?"선택 상태 유지 · 근접 영상":"선택 상태 유지 · 전체 영상"))SelectRole(selectedRole==2?3:2);
            }
            if(transit!=null&&transit.enabled)
            {
                GUILayout.Label("이동 구간: "+transit.elapsed.ToString("F1")+"초 / 후보 속도 메카4·보스8m/s",small);
                float t=GUILayout.HorizontalSlider(transit.elapsed,0,95);if(Mathf.Abs(t-transit.elapsed)>.01f)transit.SetTime(t);
                if(GUILayout.Button(transit.playing?"이동 프리뷰 정지":"이동 프리뷰 재생"))transit.playing=!transit.playing;
                GUILayout.Label(transit.Status,small);
            }
            showTargetOverlay=GUILayout.Toggle(showTargetOverlay,"목표 선택 윤곽 표시");
            GUILayout.Label("[ / ] 장면 이동. 시점은 검토용 단안 카메라. 실제 HMD 시야/멀미 검증은 별도.",small);
            GUILayout.EndArea();
        }
        void DrawTargetOverlay()
        {
            if(Current.targets==null||selectedRole<2)return;var c=viewer.views[firstCamera+selectedRole];
            for(int i=0;i<Current.targets.Length;i++)
            {
                var t=Current.targets[i];if(t==null||!t.gameObject.activeInHierarchy)continue;
                var p=c.WorldToScreenPoint(t.position);if(p.z<=0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)continue;
                float size=i==selectedTarget?44:32;var rect=new Rect(p.x-size/2,Screen.height-p.y-size/2,size,size);
                var old=GUI.color;GUI.color=i==selectedTarget?new Color(.35f,1,.8f):new Color(1,.78f,.2f);
                GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.yMax-2,rect.width,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x,rect.y,2,rect.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.xMax-2,rect.y,2,rect.height),Texture2D.whiteTexture);GUI.color=old;
                GUI.Label(new Rect(rect.xMax+4,rect.y-2,170,44),Current.targetNames[i],small);
                if(GUI.Button(rect,GUIContent.none,GUIStyle.none))selectedTarget=i;
            }
        }
        IEnumerator AuditPlayer(string output)
        {
            Directory.CreateDirectory(output);yield return null;
            var manifest=new System.Text.StringBuilder("step,role,filename\n");
            for(int step=0;step<steps.Length;step++)for(int role=0;role<4;role++)
            {
                SelectStep(step);SelectRole(role);showPanel=false;
                for(int warm=0;warm<5;warm++)yield return null;
                yield return new WaitForEndOfFrame();
                string file=steps[step].id+"-role"+role+".png";
                if(!CaptureAuditFrame(Path.Combine(output,file))){Application.Quit(1);yield break;}
                manifest.AppendLine(steps[step].id+","+role+","+file);
            }
            SelectStep(steps.Length-1);selectedTarget=2;SelectRole(2);SelectRole(3);showPanel=true;
            if(selectedTarget!=2){Debug.LogError("SCENARIO_SELECTION_LOST");Application.Quit(1);yield break;}
            File.WriteAllText(Path.Combine(output,"control-selection.txt"),"Selected E02 retained through overview to detail API transition. Human mouse input and IMGUI pixels are not captured by this off-screen camera audit.\n");
            File.WriteAllText(Path.Combine(output,"capture-scope.txt"),"Native player cameras rendered explicitly to1600x1000 targets. Scene geometry/materials/post-processing included; IMGUI overlays and control panel excluded. Hidden-window display-buffer capture was rejected.\n");
            Debug.Log("SCENARIO_SELECTION_PRESERVED_OK");
            File.WriteAllText(Path.Combine(output,"captures.csv"),manifest.ToString());
            Debug.Log("SCENARIO_PLAYER_AUDIT_OK steps="+steps.Length+" captures="+(steps.Length*4));Application.Quit(0);
        }
        bool CaptureAuditFrame(string path)
        {
            // A hidden Windows player has no reliable presented back buffer. Render the same
            // active scene camera explicitly. IMGUI is outside this capture's verified scope.
            var camera=viewer.views[firstCamera+selectedRole];var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(1600,1000,24);var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.aspect=1.6f;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();Destroy(target);}
            var pixels=texture.GetPixels32();int minimum=255,maximum=0;
            for(int i=0;i<pixels.Length;i+=997){var p=pixels[i];int v=(p.r+p.g+p.b)/3;minimum=Mathf.Min(minimum,v);maximum=Mathf.Max(maximum,v);}
            File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
            if(maximum-minimum<12){Debug.LogError("SCENARIO_CAPTURE_UNIFORM "+path);return false;}
            return true;
        }
    }
}
