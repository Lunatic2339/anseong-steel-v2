using System;
using System.Collections;
using System.IO;
using Anseong.Pilot;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace Anseong.Z1
{
    public sealed class Z1Prototype : MonoBehaviour
    {
        PlateNetwork net; Z1World world;
        Seat seat; Variant mode=Variant.Drone;
        bool simulate=true, xrStarting, auto, readySent;
        string address="127.0.0.1", port="7777", message="";
        int cameraIndex=1, observedEpoch;
        float yaw=-50, pitch=20, timer, scanButton;
        bool pendingFire;
        double started, openAt=-1;
        string resultFile;
        GUIStyle label;
        Vector3 handLeft=new Vector3(-6,24,3),handRight=new Vector3(6,24,3);
        bool tracked;
        Vector3 neutralLeft=new Vector3(-.2f,1.1f,.3f),neutralRight=new Vector3(.2f,1.1f,.3f);
        readonly HandDeadzone leftFilter=new HandDeadzone(),rightFilter=new HandDeadzone();
        readonly HandDeadzoneSettings deadzone=new HandDeadzoneSettings();
        static string Arg(string name,string fallback="")
        { var args=Environment.GetCommandLineArgs(); var i=Array.IndexOf(args,name); return i>=0&&i+1<args.Length?args[i+1]:fallback; }
        IEnumerator Start()
        {
            Application.runInBackground=true; Application.targetFrameRate=90;
            net=gameObject.AddComponent<PlateNetwork>(); world=gameObject.AddComponent<Z1World>(); world.Build();
            started=Time.realtimeSinceStartupAsDouble;
            auto=Array.IndexOf(Environment.GetCommandLineArgs(),"--z1-auto")>=0;
            if(auto)
            {
                seat=Arg("--seat")=="support"?Seat.Support:Seat.Mech;
                mode=Arg("--variant","3")=="4"?Variant.Control:Variant.Drone;
                port=Arg("--port","7777"); address=Arg("--address","127.0.0.1"); resultFile=Arg("--result");
                yield return null; Connect(Arg("--host","false")=="true");
            }
        }
        void Connect(bool host)
        {
            if(!ushort.TryParse(port,out var p)||p==0){ message="유효한 포트를 입력하세요.";return; }
            net.Connect(host,seat,mode,address,p); readySent=false;
            if(seat==Seat.Mech&&!simulate&&!xrStarting) StartCoroutine(StartXR());
        }
        IEnumerator StartXR()
        {
            xrStarting=true;
            var manager=XRGeneralSettings.Instance?.Manager;
            if(manager!=null)
            {
                yield return manager.InitializeLoader();
                if(manager.activeLoader!=null) manager.StartSubsystems();
                else message="OpenXR 초기화 실패 · Link/활성 런타임을 확인하세요.";
            }
            else message="XR 설정 없음 · Build Setup 메뉴를 실행하세요.";
            xrStarting=false;
        }
        void StopXR()
        {
            var manager=XRGeneralSettings.Instance?.Manager;
            if(manager!=null&&manager.activeLoader!=null){ manager.StopSubsystems(); manager.DeinitializeLoader(); }
        }
        void OnDestroy(){ StopXR(); }
        bool ReadHand(XRNode node,out Vector3 position)
        {
            var device=InputDevices.GetDeviceAtXRNode(node); position=Vector3.zero;
            if(!device.TryGetFeatureValue(CommonUsages.trackingState,out InputTrackingState state)||!TrackingStateRules.IsPoseTracked((int)state)||
                !device.TryGetFeatureValue(CommonUsages.devicePosition,out var p)||!PlateSession.Finite(p))return false;
            var isLeft=node==XRNode.LeftHand; var shoulder=new Vector3(isLeft?-6:6,27,0);
            var filtered=(isLeft?leftFilter:rightFilter).Filter(p,Quaternion.identity,deadzone).position;
            var desired=new Vector3(isLeft?-4:4,24,3)+(filtered-(isLeft?neutralLeft:neutralRight))*12;
            position=shoulder+Vector3.ClampMagnitude(desired-shoulder,13);
            return true;
        }
        void Update()
        {
            if(net==null)return;
            var k=Keyboard.current;var mouse=Mouse.current;
            if(net.State.epoch!=observedEpoch){observedEpoch=net.State.epoch;readySent=false;}
            if(seat==Seat.Mech)
            {
                if(simulate)
                {
                    // Explicit desktop test substitute, never reported as physical tracking.
                    tracked=auto||k==null||!k.tKey.isPressed;
                    handLeft=new Vector3(-4,24,(auto||k!=null&&k.qKey.isPressed)?7.5f:3);
                    handRight=new Vector3(4,24,(auto||k!=null&&k.eKey.isPressed)?7.5f:3);
                    world.view.transform.SetPositionAndRotation(new Vector3(0,29.75f,0),Quaternion.Euler(25,0,0));
                }
                else
                {
                    tracked=ReadHand(XRNode.LeftHand,out handLeft)&ReadHand(XRNode.RightHand,out handRight);
                    var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                    if(head.TryGetFeatureValue(CommonUsages.devicePosition,out var hp)&&head.TryGetFeatureValue(CommonUsages.deviceRotation,out var hr))
                        world.view.transform.SetPositionAndRotation(new Vector3(0,28.05f,0)+hp,hr);
                    var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                    if(!readySent&&right.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger)&&trigger)Ready();
                }
            }
            else if(mode==Variant.Drone)
            {
                if(mouse!=null&&mouse.rightButton.isPressed&&!auto){var d=mouse.delta.ReadValue();yaw+=d.x*.15f;pitch=Mathf.Clamp(pitch-d.y*.15f,-75,75);}
                var rotation=auto?Quaternion.LookRotation(PlateNetwork.Target-net.State.drone):Quaternion.Euler(pitch,yaw,0);
                world.view.transform.SetPositionAndRotation(net.State.drone,rotation);
            }
            else world.view.transform.SetPositionAndRotation(PlateNetwork.ControlPosition(cameraIndex),Quaternion.LookRotation(PlateNetwork.Target-PlateNetwork.ControlPosition(cameraIndex)));
            if(k!=null&&k.enterKey.wasPressedThisFrame)Ready();
            if(k!=null&&k.rKey.wasPressedThisFrame&&seat==Seat.Mech)net.Send(new InputFrame{command=2});
            if(k!=null&&k.tabKey.wasPressedThisFrame)cameraIndex=1-cameraIndex;
            var overMenu=mouse!=null&&mouse.position.ReadValue().x<426&&mouse.position.ReadValue().y>Screen.height-560;
            if(mouse!=null && !overMenu && (mode==Variant.Drone?mouse.leftButton.wasPressedThisFrame:mouse.rightButton.wasPressedThisFrame))pendingFire=true;
            if(auto&&net.Connected&&!readySent)Ready();
            timer+=Time.unscaledDeltaTime;
            if(timer>=.05f)
            {
                timer=0;var f=new InputFrame{left=handLeft,right=handRight,tracked=tracked,camera=cameraIndex};
                if(seat==Seat.Support)
                {
                    f.ray=(auto||mode==Variant.Drone||mouse==null||scanButton>Time.unscaledTime)?world.view.transform.forward:world.view.ScreenPointToRay(mouse.position.ReadValue()).direction;
                    f.scan=auto||scanButton>Time.unscaledTime||mouse!=null&&mouse.rightButton.isPressed;
                    f.fire=auto||pendingFire;
                    if(!auto&&k!=null&&mode==Variant.Drone)
                    {
                        f.move=world.view.transform.forward*((k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0))+world.view.transform.right*((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0))+Vector3.up*((k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0));
                    }
                }
                net.Send(f);pendingFire=false;
            }
            world.Show(net.State,seat,mode);
            if(auto)
            {
                if(net.State.phase==PlatePhase.Open&&openAt<0)openAt=Time.realtimeSinceStartupAsDouble;
                if(openAt>=0&&Time.realtimeSinceStartupAsDouble-openAt>2)Finish(true);
                else if(Time.realtimeSinceStartupAsDouble-started>45)Finish(false);
            }
        }
        void Ready()
        {
            if(seat==Seat.Mech&&!simulate)
            {
                var l=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var r=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if(l.TryGetFeatureValue(CommonUsages.devicePosition,out var lp)&&r.TryGetFeatureValue(CommonUsages.devicePosition,out var rp)&&PlateSession.Finite(lp)&&PlateSession.Finite(rp))
                {neutralLeft=lp;neutralRight=rp;leftFilter.Reset();rightFilter.Reset();}
            }
            net.Send(new InputFrame{command=1});if(net.Connected)readySent=true;
        }
        void Finish(bool success)
        {
            var result=JsonUtility.ToJson(net.State,true);
            if(!string.IsNullOrEmpty(resultFile))File.WriteAllText(resultFile,result);
            Debug.Log("Z1_SMOKE "+(success?"PASS":"FAIL")+" "+seat+" "+mode+" releases="+net.State.releases);
            Application.Quit(success&&net.State.releases==1?0:2);auto=false;
        }
        void OnGUI()
        {
            if(net==null)return;
            if(label==null){GUI.skin.font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",17);label=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};label.normal.textColor=Color.white;}
            GUI.Box(new Rect(16,16,410,540),"");
            GUILayout.BeginArea(new Rect(30,26,380,520));
            GUILayout.Label("안성제철 · Z1 협동 검증",label);
            if(!net.Started)
            {
                seat=(Seat)GUILayout.SelectionGrid((int)seat,new[]{"메카 · 양팔","PC 지원"},2);
                mode=GUILayout.SelectionGrid(mode==Variant.Drone?0:1,new[]{"3안 · 드론","4안 · 관제"},2)==0?Variant.Drone:Variant.Control;
                if(seat==Seat.Mech)simulate=GUILayout.Toggle(simulate,"키보드 대체 입력 (실제 VR 검증 아님)");
                GUILayout.Label("호스트 IP / UDP 포트",label);address=GUILayout.TextField(address);port=GUILayout.TextField(port);
                if(GUILayout.Button("호스트 시작",GUILayout.Height(30)))Connect(true);
                if(GUILayout.Button("클라이언트 접속",GUILayout.Height(30)))Connect(false);
            }
            else
            {
                GUILayout.Label((seat==Seat.Mech?"양팔 메카":mode==Variant.Drone?"PC 드론":"PC 관제")+" · "+(simulate&&seat==Seat.Mech?"대체 입력":""),label);
                GUILayout.Label("메카 "+(net.State.mechReady?"준비":"대기")+" / 지원 "+(net.State.supportReady?"준비":"대기")+"\n판: "+net.State.phase+" / 스캔: "+Mathf.RoundToInt(net.State.scan*100)+"%",label);
                GUILayout.Label(net.State.phase==PlatePhase.Locked?"메카가 청록 판을 밀면 해제할 수 있습니다.":net.State.phase==PlatePhase.Supported?"받침 고정됨 · 팔을 내려도 됩니다. PC 해제 대기":"판을 서쪽으로 이동합니다.",label);
                if(GUILayout.Button("준비 [Enter / VR 오른손 트리거]"))Ready();
                GUILayout.Label(seat==Seat.Mech?(simulate?"Q: 왼손 밀기 / E: 오른손 밀기\nT 유지: 추적 중단 / R: 구역 재시작":"청록 판을 천천히 밀고 팔을 내려놓으세요."):
                    mode==Variant.Drone?"WASD 이동 / Q·E 고도 / 우클릭 드래그 시점\n화면 중앙 + 조준 · 왼클릭 사격":"Tab: K0/K1 · 고정부에 커서 조준\n우클릭 유지: 스캔 / 완료 후 다시 우클릭: 해제",label);
                if(seat==Seat.Support&&mode==Variant.Control)
                {
                    if(GUILayout.Button("K0 / K1 전환"))cameraIndex=1-cameraIndex;
                    if(GUILayout.Button(net.State.scan<1?"중앙 고정부 스캔":"중앙 고정부 해제")){scanButton=Time.unscaledTime+1.5f;pendingFire=true;}
                }
            }
            GUILayout.Label(net.Status+"\n"+message,label);
            if(GUILayout.Button("연결 종료 / 다시 선택")){net.Disconnect();StopXR();}
            GUILayout.EndArea();
            if(seat==Seat.Support&&mode==Variant.Drone)GUI.Label(new Rect(Screen.width/2-8,Screen.height/2-12,30,30),"+",label);
            if(net.State.phase==PlatePhase.Open)GUI.Label(new Rect(Screen.width/2-180,Screen.height-80,400,60),"출격로 확보 · 협동 검증 완료",label);
        }
    }
}
