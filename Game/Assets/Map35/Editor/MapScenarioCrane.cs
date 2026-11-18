using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // These are authored spatial-review states, not autonomous crane physics.
        // The caller activates one state at a time and hides the two original viewer coils.
        static Transform BuildScenarioCraneState(string id,string phase,Transform parent)
        {
            var allowed=new[]{"prepare","ready","windup","evaded","success","miss","success_follow","miss_follow"};
            if(!allowed.Contains(phase))throw new ArgumentException("Unsupported crane review phase: "+phase);
            var root=Group(id,parent);
            bool success=phase=="success"||phase=="success_follow";
            bool miss=phase=="miss"||phase=="miss_follow";
            bool spent=success||miss;
            bool armed=phase!="prepare";
            bool release=phase=="evaded";

            var source=success?viewer.lateCoil.transform:viewer.coil;
            if(source==null)throw new InvalidOperationException("Current crane coil source is missing");
            var roll=Obj.Instantiate(source.gameObject,root).transform;
            roll.name="Scenario coil "+phase;roll.gameObject.SetActive(true);
            roll.rotation=Quaternion.identity;roll.localScale=Vector3.one;
            if(success)roll.position=new Vector3(57,2.62f,555);
            else if(miss)
            {
                roll.position=new Vector3(90,2,555);
                float support=ScenarioCraneYardTop();
                float bottom=roll.GetComponentsInChildren<MeshRenderer>(true).Min(r=>r.bounds.min.y);
                roll.position+=Vector3.up*(support+.006f-bottom);
            }
            else roll.position=new Vector3(90,52,555);
            ClearScenarioCraneBakeFlags(roll);

            // P02 retains the existing ring and conduit. A physical pin face sits inside it.
            var metal=ThemeMaterial(6,"metal",0);var zinc=ThemeMaterial(6,"galvanized",0);
            var amber=ScenarioCraneMaterial("Amber",new Color(.72f,.32f,.045f),.45f);
            var greenLamp=ScenarioCraneMaterial("Ready",new Color(.16f,.62f,.42f),1.5f);
            var redLamp=ScenarioCraneMaterial("Danger",new Color(.7f,.055f,.025f),1.1f);
            var cyanLamp=ScenarioCraneMaterial("Transit",new Color(.1f,.43f,.55f),.5f);
            var spentMat=ScenarioCraneMaterial("Spent",new Color(.16f,.18f,.19f),0);
            Material signal=spent?spentMat:release?greenLamp:phase=="windup"?redLamp:amber;
            var g=new IndustrialGeometry(root,"Scenario crane fittings "+id);
            var latch=new Vector3(140.22f,48,555);
            g.Rod(latch-Vector3.right*.3f,latch+Vector3.right*.2f,.30f,zinc,24);
            g.Ring(latch+Vector3.right*.21f,Vector3.right,.36f,.23f,.04f,armed?greenLamp:amber,24);
            if(!armed)g.Bar(latch+new Vector3(.25f,-.37f,-.42f),latch+new Vector3(.25f,.37f,.42f),.12f,.16f,amber);
            else g.Bar(latch+new Vector3(.25f,.48f,-.42f),latch+new Vector3(.25f,.48f,.42f),.12f,.16f,zinc);
            ScenarioCraneAnchor("Target CraneLatch",new Vector3(140.45f,48,555),Vector3.right,root);

            // The release face looks east toward D9/D10/K3b; it is a separate device from the pin.
            // The pedestal terminates at the existing y46 service deck and is braced to its rim.
            var panel=new Vector3(147.5f,48,559);
            g.Box(new Vector3(146.75f,46.12f,559),new Vector3(.75f,.24f,.9f),metal,.035f);
            g.Bar(new Vector3(146.75f,46.24f,559),new Vector3(146.75f,48,559),.18f,.22f,metal);
            g.Bar(new Vector3(146.75f,47.1f,559),panel+new Vector3(-.2f,0,0),.16f,.18f,metal);
            g.Box(panel,new Vector3(.32f,2.4f,2.1f),metal,.075f);
            g.Box(panel+new Vector3(.18f,.1f,0),new Vector3(.05f,1.62f,1.55f),spentMat,.015f);
            g.Ring(panel+new Vector3(.235f,.1f,0),Vector3.right,.58f,.43f,.06f,signal,32);
            g.Rod(panel+new Vector3(.22f,.1f,0),panel+new Vector3(.28f,.1f,0),.34f,release?greenLamp:spentMat,32);
            // Raised shutter when safe; the armed/unsafe states visibly keep the centre covered.
            if(!release&&!spent)
                g.Box(panel+new Vector3(.33f,.1f,0),new Vector3(.10f,1.18f,1.18f),phase=="windup"?redLamp:amber,.04f);
            else
                g.Box(panel+new Vector3(.24f,1.58f,0),new Vector3(.10f,.55f,1.18f),spent?spentMat:zinc,.04f);
            g.Pipe(new[]{new Vector3(146.75f,46.4f,559),new Vector3(146.75f,46.4f,557.4f),new Vector3(140,46.4f,557.4f),new Vector3(140,48.8f,557.4f)},.055f,zinc);
            ScenarioCraneAnchor("Target CraneRelease",panel+new Vector3(.35f,.1f,0),Vector3.right,root);

            // Three broad physical status bars can be read as shapes as well as colours.
            // Text/remote selection UI is added by the scenario viewer, not baked into these hit faces.
            for(int i=0;i<3;i++)
            {
                Material bar=i==0?(armed?greenLamp:amber):i==1?(release?greenLamp:redLamp):(spent?spentMat:release?greenLamp:amber);
                g.Box(panel+new Vector3(.2f,-.93f,(i-1)*.55f),new Vector3(.06f,.16f,.37f),bar,.015f);
            }
            g.Finish();
            ScenarioCraneText("Scenario crane status "+phase,
                spent?"SPENT":release?"DROP READY":phase=="windup"?"CLEAR AREA":armed?"PREPARED":"LOAD LOCKED",
                panel+new Vector3(.18f,1.01f,0),Quaternion.Euler(0,-90,0),.13f,root);

            BuildScenarioCraneGroundRead(id,phase,root,amber,greenLamp,redLamp,cyanLamp,metal);
            if(spent)BuildScenarioImpactRead(id,success,root,metal);
            ClearScenarioCraneBakeFlags(root);
            return root;
        }

        // Shared E02 fixture: call once outside stage roots. No damage/cooldown implementation here.
        static Transform BuildScenarioE02(Transform parent)
        {return BuildScenarioEmitter("E02",new Vector3(180,36,595),new Vector3(110,34,538),parent);}

        static Transform BuildScenarioE01(Transform parent)
        {return BuildScenarioEmitter("E01",new Vector3(-60,35,285),new Vector3(-20,34,283),parent);}

        static Transform BuildScenarioEmitter(string id,Vector3 centre,Vector3 aim,Transform parent)
        {
            var root=Group("Scenario "+id+" mounted support emitter",parent);
            var metal=ThemeMaterial(6,"metal",0);var zinc=ThemeMaterial(6,"galvanized",0);
            var cement=ThemeMaterial(6,"concrete",0);var paint=ThemeMaterial(6,"paint",0);
            var signal=ScenarioCraneMaterial("EmitterLens",new Color(.08f,.55f,.66f),1.8f);
            var g=new IndustrialGeometry(root,"Scenario "+id+" structural assembly");
            var basePoint=new Vector3(centre.x,0,centre.z);
            Vector3 At(float x,float y,float z)=>new Vector3(centre.x+x,y,centre.z+z);
            float mastTop=centre.y-2.8f,deck=centre.y-2.7f;
            g.Box(basePoint+new Vector3(0,.3f,0),new Vector3(5,.6f,5),cement,.08f);
            foreach(float x in new[]{-1.5f,1.5f})foreach(float z in new[]{-1.5f,1.5f})
            {
                g.Box(At(x,.65f,z),new Vector3(.65f,.1f,.65f),zinc,.025f);
                g.IBeam(At(x,.7f,z),At(x,mastTop,z),.25f,.4f,metal);
            }
            for(int band=0;band<8;band++)
            {
                float pitch=(mastTop-.8f)/8,low=.8f+band*pitch,high=low+pitch;
                foreach(float z in new[]{-1.5f,1.5f})
                {
                    g.Bar(At(-1.5f,low,z),At(1.5f,high,z),.13f,.17f,zinc);
                    g.Bar(At(-1.5f,high,z),At(1.5f,high,z),.13f,.17f,zinc);
                }
                foreach(float x in new[]{-1.5f,1.5f})
                    g.Bar(At(x,low,-1.5f),At(x,high,1.5f),.13f,.17f,zinc);
            }
            g.Box(At(0,deck,0),new Vector3(6,.3f,6),metal,.035f);
            IndustrialRail(g,At(-3,deck+.15f,3),At(3,deck+.15f,3),metal,paint);
            IndustrialRail(g,At(-3,deck+.15f,-3),At(-3,deck+.15f,3),metal,paint);
            IndustrialRail(g,At(3,deck+.15f,-3),At(3,deck+.15f,3),metal,paint);
            foreach(float x in new[]{-.4f,.4f})g.Rod(At(x,.7f,-2.3f),At(x,centre.y-1.5f,-2.3f),.045f,zinc,8);
            for(float y=1;y<centre.y-1.7f;y+=.3f)g.Rod(At(-.4f,y,-2.3f),At(.4f,y,-2.3f),.032f,zinc,8);
            g.Box(At(1.9f,centre.y-1.6f,1.7f),new Vector3(1.4f,1.8f,.7f),paint,.06f);
            g.Pipe(new[]{At(1.7f,.65f,1.6f),At(1.7f,centre.y-3.3f,1.6f),At(1.7f,centre.y-1.9f,1.6f),At(0,centre.y-1.9f,0)},.065f,zinc);
            var direction=(aim-centre).normalized;
            g.Rod(At(0,centre.y-2.5f,0),At(0,centre.y-1.3f,0),1.1f,metal,32);
            g.Ring(At(0,centre.y-1.35f,0),Vector3.up,1.5f,1.05f,.3f,zinc,32);
            g.Box(At(0,centre.y-.75f,0),new Vector3(2,.9f,2),metal,.08f);
            g.Frustum(centre-direction*1.25f,centre+direction*.75f,1.35f,1.0f,paint,32);
            for(int i=0;i<5;i++)g.Ring(centre+direction*(-.9f+i*.37f),direction,1.4f-i*.055f,1.12f-i*.055f,.12f,zinc,32);
            g.Ring(centre+direction*1.1f,direction,1.02f,.67f,.7f,metal,32);
            g.Ring(centre+direction*1.48f,direction,.94f,.71f,.07f,signal,32);
            g.Rod(centre+direction*1.50f,centre+direction*1.52f,.68f,signal,32);
            g.Finish();
            ScenarioCraneAnchor("Target "+id,centre+direction*1.54f,direction,root);
            ScenarioCraneText(id+" equipment identification",id+" / BIO PULSE",At(1.9f,centre.y-1.55f,1.32f),Quaternion.identity,.12f,root);
            ClearScenarioCraneBakeFlags(root);
            return root;
        }

        static void BuildScenarioCraneGroundRead(string id,string phase,Transform parent,Material amber,Material safe,Material danger,Material transit,Material metal)
        {
            float y=ScenarioCraneYardTop()+.018f;
            var g=new IndustrialGeometry(parent,"Scenario crane floor reading "+id);
            bool evaded=phase=="evaded",spent=phase.Contains("success")||phase.Contains("miss");
            Material hazard=phase=="windup"?danger:amber;
            // Physical paint strips, never collider walls. The 12 x 16m folded footprint stays open.
            foreach(float x in new[]{65.65f,78.35f})g.Box(new Vector3(x,y,504),new Vector3(.22f,.012f,16.7f),safe);
            foreach(float z in new[]{495.65f,512.35f})g.Box(new Vector3(72,y,z),new Vector3(12.7f,.012f,.22f),safe);
            foreach(float x in new[]{80f,100f})
                for(float z=499;z<530;z+=4)g.Box(new Vector3(x,y,z),new Vector3(.3f,.012f,2.25f),hazard);
            foreach(float z in new[]{498f,530f})
                for(float x=81;x<100;x+=4)g.Box(new Vector3(x,y,z),new Vector3(2.25f,.012f,.3f),hazard);
            // Lure corners and a lateral escape chevron are recognisable without debug overlays.
            foreach(float x in new[]{86f,94f})g.Box(new Vector3(x,y,504),new Vector3(.24f,.012f,8),amber);
            foreach(float z in new[]{500f,508f})g.Box(new Vector3(90,y,z),new Vector3(8,.012f,.24f),amber);
            foreach(float x in new[]{81f,76.5f})
            {
                g.Bar(new Vector3(x+1,y,501.8f),new Vector3(x-1,y,504),.34f,.012f,safe);
                g.Bar(new Vector3(x-1,y,504),new Vector3(x+1,y,506.2f),.34f,.012f,safe);
            }
            // Low fixtures sit beyond safe/attack footprints; no new collision geometry is added.
            foreach(float z in new[]{495f,513f})
            {
                g.Box(new Vector3(64.9f,.22f,z),new Vector3(.65f,.42f,.65f),metal,.045f);
                g.Box(new Vector3(64.9f,.445f,z),new Vector3(.45f,.045f,.45f),evaded||spent?safe:transit,.01f);
            }
            // A small physical board is readable from the southern approach, outside centre travel.
            g.Box(new Vector3(64.7f,1.25f,493.4f),new Vector3(2.8f,2.1f,.16f),metal,.05f);
            g.Box(new Vector3(64.7f,1.25f,493.3f),new Vector3(2.5f,1.8f,.03f),safe,.015f);
            foreach(float x in new[]{63.9f,65.5f})g.Bar(new Vector3(x,.1f,493.45f),new Vector3(x,1.2f,493.45f),.12f,.16f,metal);
            g.Finish();
            ScenarioCraneAnchor("Target Evacuation",new Vector3(72,y,504),Vector3.up,parent);
            ScenarioCraneAnchor("Target Lure",new Vector3(90,y,504),Vector3.up,parent);
        }

        static void BuildScenarioImpactRead(string id,bool success,Transform parent,Material metal)
        {
            var g=new IndustrialGeometry(parent,"Scenario settled impact traces "+id);
            var chipped=ScenarioCraneMaterial("ImpactConcrete",new Color(.28f,.27f,.25f),0);
            var centre=new Vector3(success?57:90,ScenarioCraneYardTop()+.015f,555);
            if(!success)
            {
                // The dent is visual, leaves the nominal supporting surface and coil collision intact.
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;
                    var p=centre+new Vector3(Mathf.Cos(a)*6.8f,0,Mathf.Sin(a)*5.9f);
                    g.Box(p,new Vector3(.7f,.025f,.15f),chipped,0,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
                }
            }
            else
            {
                // Success ends on the existing R1 receiving slab; restrained scuff fragments stay on it.
                foreach(float x in new[]{49f,65f})g.Box(new Vector3(x,.64f,555),new Vector3(.5f,.04f,5),metal);
            }
            g.Finish();
        }

        static float ScenarioCraneYardTop()
        {
            var floor=structures==null?null:structures.Find("Z3 crane yard 240x200");
            var c=floor==null?null:floor.GetComponent<Collider>();
            return c==null?0:c.bounds.max.y;
        }

        static Material ScenarioCraneMaterial(string suffix,Color color,float emission)
        {
            var mat=Material("ScenarioCrane_"+suffix,color);
            mat.SetFloat("_Metallic",suffix=="Spent"?.55f:0);mat.SetFloat("_Glossiness",.3f);
            if(emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*emission);}
            else {mat.DisableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.black);}
            return mat;
        }

        static Transform ScenarioCraneAnchor(string name,Vector3 position,Vector3 forward,Transform parent)
        {
            var t=Group(name,parent);t.position=position;t.rotation=Quaternion.LookRotation(forward,Mathf.Abs(Vector3.Dot(forward.normalized,Vector3.up))>.98f?Vector3.forward:Vector3.up);
            return t;
        }

        static void ScenarioCraneText(string name,string value,Vector3 position,Quaternion rotation,float size,Transform parent)
        {
            var text=new GameObject(name).AddComponent<TextMesh>();text.transform.SetParent(parent);
            text.transform.SetPositionAndRotation(position,rotation);text.text=value;text.characterSize=size;
            text.fontSize=72;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.color=new Color(.82f,.84f,.79f);
        }

        static void ClearScenarioCraneBakeFlags(Transform root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {r.lightmapIndex=-1;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;}
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))Obj.DestroyImmediate(lod);
        }
    }
}
