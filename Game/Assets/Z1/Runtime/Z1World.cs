using UnityEngine;

namespace Anseong.Z1
{
    public sealed class Z1World : MonoBehaviour
    {
        public Transform plate, left, right, drone;
        public Renderer target, pad;
        public Camera view;
        public TextMesh hud;
        Transform[] arms = new Transform[4];
        static Material Mat(Color c)
        {
            var template=Resources.Load<Material>("Z1Base");
            var m=template!=null?new Material(template):new Material(Shader.Find("Standard"));
            m.color=c;m.SetFloat("_Glossiness",.25f);return m;
        }
        static GameObject Box(string name, Vector3 p, Vector3 size, Color color, bool collision = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.position = p; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = Mat(color);
            if (!collision) g.GetComponent<Collider>().enabled = false;
            return g;
        }
        public void Build()
        {
            var steel = new Color(.13f,.19f,.22f); var orange = new Color(1,.42f,.06f); var teal = new Color(.1f,.7f,.7f);
            Box("Ground", new Vector3(0,-.5f,0), new Vector3(100,1,110), steel);
            for (int i=-1;i<=1;i+=2)
            {
                Box("Maintenance column",new Vector3(i*20,21,-10),new Vector3(3,42,3),steel);
                Box("West east rail",new Vector3(i*13,.1f,5),new Vector3(.3f,.1f,65),orange,false);
                for (int z=-30;z<40;z+=12) Box("Route stripe",new Vector3(i*10,.15f,z),new Vector3(1,.1f,5),orange,false);
            }
            Box("Open hangar beam", new Vector3(0,43,-10), new Vector3(43,2,3),steel);
            Box("Side retraction rail",new Vector3(-11,29,8),new Vector3(33,1,1),steel);
            plate = Box("Safety plate", new Vector3(0,24,8),new Vector3(10,8,1),orange).transform;
            pad = Box("Push pad",new Vector3(0,24,7.4f),new Vector3(8,6,.15f),teal,false).GetComponent<Renderer>();
            pad.transform.SetParent(plate,true);
            target = Box("ReleaseTarget",PlateNetwork.Target,new Vector3(2,2,2),Color.red).GetComponent<Renderer>();
            Box("Latch bracket",new Vector3(7,27.5f,8),new Vector3(4,.5f,1),steel);
            Box("Mech torso 35m proxy",new Vector3(0,22,-2),new Vector3(10,12,4),steel,false);
            Box("Mech head 35m datum",new Vector3(0,33,-2),new Vector3(5,4,4),steel,false);
            for(int side=-1;side<=1;side+=2) Box("Leg",new Vector3(side*3,8,-2),new Vector3(3,16,4),steel,false);
            left = Box("Left hand",new Vector3(-6,24,3),Vector3.one*1.4f,teal,false).transform;
            right = Box("Right hand",new Vector3(6,24,3),Vector3.one*1.4f,teal,false).transform;
            for(int i=0;i<4;i++) arms[i]=Box("Arm proxy",Vector3.zero,Vector3.one,steel,false).transform;
            drone = Box("Support drone",new Vector3(14,28,3),new Vector3(1.4f,.4f,1.4f),teal,false).transform;
            for(int i=0;i<4;i++) Box("Background stack",new Vector3(35+i*9,18+i*4,45),new Vector3(5,36+i*8,5),steel);
            var sun = new GameObject("Work light").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.5f; sun.transform.rotation=Quaternion.Euler(40,-25,0);
            RenderSettings.ambientLight = new Color(.45f,.5f,.55f);
            view=new GameObject("Role camera").AddComponent<Camera>(); view.nearClipPlane=.1f; view.farClipPlane=400; view.fieldOfView=70;
            view.backgroundColor=new Color(.12f,.18f,.23f); view.clearFlags=CameraClearFlags.SolidColor; view.gameObject.AddComponent<AudioListener>();
            view.transform.position=new Vector3(0,29.75f,0); view.transform.rotation=Quaternion.Euler(25,0,0);
            hud=new GameObject("VR status").AddComponent<TextMesh>(); hud.transform.SetParent(view.transform,false);
            hud.transform.localPosition=new Vector3(-1.1f,.85f,2); hud.characterSize=.011f; hud.fontSize=56; hud.color=Color.white;
            hud.anchor=TextAnchor.UpperLeft; hud.text="Z1 CO-OP TEST";
        }
        static void Segment(Transform t, Vector3 a, Vector3 b)
        { t.position=(a+b)*.5f; t.rotation=Quaternion.LookRotation(b-a); t.localScale=new Vector3(1.1f,1.1f,Vector3.Distance(a,b)); }
        public void Show(PlateState s, Seat seat, Variant mode)
        {
            plate.position=new Vector3(-22*Mathf.SmoothStep(0,1,s.travel),24,8);
            target.material.color=s.phase==PlatePhase.Locked?Color.red:s.phase==PlatePhase.Supported?Color.yellow:Color.green;
            pad.material.color=s.phase==PlatePhase.Locked?new Color(.1f,.7f,.7f):Color.green;
            left.position=s.left; right.position=s.right; drone.position=s.drone;
            drone.gameObject.SetActive(mode==Variant.Drone && seat!=Seat.Support);
            for(int i=0;i<2;i++)
            {
                var shoulder=new Vector3(i==0?-6:6,27,0); var hand=i==0?s.left:s.right;
                var elbow=(shoulder+hand)*.5f+new Vector3(i==0?-2:2,-1,0);
                Segment(arms[i*2],shoulder,elbow); Segment(arms[i*2+1],elbow,hand);
            }
            hud.gameObject.SetActive(seat==Seat.Mech);
            hud.text="ANSEONG / Z1\n"+(s.Running?"CREW READY":"WAIT FOR BOTH PLAYERS")+"\n"+
                (s.phase==PlatePhase.Locked?"PUSH THE TEAL PLATE":s.phase==PlatePhase.Supported?"BRACE SET / PC RELEASE":s.phase==PlatePhase.Opening?"PLATE RETRACTING":"EXIT CLEAR")+
                "\n"+(s.tracking?"TRACKING OK":"TRACKING LOST / NO PUSH");
        }
    }
}
