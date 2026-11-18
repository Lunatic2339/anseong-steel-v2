using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Map art and spatial preview only. All additions are owned, repeatable,
        // and outside the moving actor reservations except flush route paint.
        static void ApplyScenarioEnvironmentCorrections()
        {
            if(structures==null)structures=GameObject.Find("01 Buildings and traversable ground").transform;
            if(details==null)details=GameObject.Find("02 Interaction markers and scale details").transform;
            if(structuralSteel==null)structuralSteel=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/Wide_structural_steel.mat");
            if(agedRoof==null)agedRoof=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/Wide_charred_roof.mat");
            if(structuralSteel==null||agedRoof==null)throw new InvalidOperationException("Existing inner-pipe materials must be present.");
            int obsolete=0;
            foreach(var item in details.Cast<Transform>().Where(t=>t.name=="B09 pipe").ToArray())
            {
                // Exact legacy object identity, not an arbitrary yellow pipe search.
                Obj.DestroyImmediate(item.gameObject);obsolete++;
            }
            var root=ArchitectureGroup("Scenario environment corrections",structures);
            var metal=ScenarioEnvironmentMaterial("aged service green",ScenarioEnvironmentTheme(1,"metal",0),new Color(.35f,.42f,.37f));
            var iron=ScenarioEnvironmentMaterial("dark oxide steel",ScenarioEnvironmentTheme(1,"metal",0),new Color(.29f,.29f,.26f));
            var zinc=ScenarioEnvironmentTheme(1,"galvanized",0);
            var warn=ScenarioEnvironmentMaterial("faded transit amber",ScenarioEnvironmentTheme(1,"warning",0),new Color(.73f,.61f,.31f));
            var concreteMat=ScenarioEnvironmentTheme(1,"concrete",0);
            BuildScenarioConnectorCues(root,iron,warn,concreteMat);
            BuildScenarioServiceStock(root,metal,iron,zinc,warn,concreteMat);
            ReplaceScenarioServiceCarrier(root,metal,iron,zinc,warn);
            BuildScenarioDamageContinuation(root,iron,warn);
            ScenarioEnvironmentPhysicalUv(root);
            int colliders=root.GetComponentsInChildren<Collider>(true).Length;
            if(colliders!=0)throw new InvalidOperationException("Scenario environment art must not introduce route blockers.");
            var report=new StringBuilder();
            report.AppendLine("Legacy B09 pipe objects removed this application="+obsolete);
            report.AppendLine("Remaining legacy B09 pipe objects="+details.Cast<Transform>().Count(t=>t.name=="B09 pipe"));
            report.AppendLine("Owned art mesh renderers="+root.GetComponentsInChildren<MeshRenderer>(true).Length);
            report.AppendLine("Owned art colliders="+colliders);
            report.AppendLine("Connector: flush arrows at M6/M7/M8; 3 mounted landmarks remain outside32m route and24m turn pads / boss alignment apron.");
            report.AppendLine("Z1 stock: west rack x[-60,-50],z[77.5,90.5]; B03 service reel x[118,124],z[50,56]. No centerline stock.");
            report.AppendLine("Carrier: inactive source Z1B abandoned repair carrier retained; detailed replacement uses same(-64,-.055,58),22deg yaw, approx3.1x16m footprint. No new collider.");
            report.AppendLine("Z2: impact scrape/drag trail and three torn edges continue existing pipe/leak/fire narrative; no independent random rubble field.");
            report.AppendLine("Review: K1=9, closeB02=17/37, B03=39/42; newM6/M7/M8 scenario route views. Geometry not gameplay/AI or final vehicle asset validation.");
            string output=Path.GetFullPath("../docs/validation/map-scenario-upgrade");Directory.CreateDirectory(output);
            File.WriteAllText(output+"/environment-corrections.txt",report.ToString());
        }

        static Material ScenarioEnvironmentTheme(int theme,string role,int variant)
        {
            string name="Wide_Palette_B"+(theme+1).ToString("00")+"_"+role+(role=="paint"?"_"+variant:"");
            var existing=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/"+name+".mat");
            return existing!=null?existing:ThemeMaterial(theme,role,variant);
        }

        static Material ScenarioEnvironmentMaterial(string id,Material source,Color tint)
        {
            string path=Base+"Materials/ScenarioEnv_"+id.Replace(' ','_')+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(source);
            m.name="ScenarioEnv "+id;m.color=tint;EditorUtility.SetDirty(m);return m;
        }

        static void ScenarioEnvironmentPhysicalUv(Transform root)
        {
            Directory.CreateDirectory(Base+"Meshes/PhysicalUV");uvMeshes.Clear();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null||filter.sharedMesh==null||!renderer.sharedMaterials.Any(m=>m!=null&&m.shader.name=="Anseong/PhysicalUVSurface"))continue;
                filter.sharedMesh=MakePhysicalUv(filter.sharedMesh,filter.transform.lossyScale);
            }
        }

        static void ScenarioTransitArrow(IndustrialGeometry g,Vector3 p,Vector3 direction,Material paint)
        {
            direction.y=0;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);
            g.Bar(p-direction*3.6f,p+direction*2.5f,.48f,.012f,paint);
            g.Bar(p+direction*2.5f,p+direction*.2f+side*2,.48f,.012f,paint);
            g.Bar(p+direction*2.5f,p+direction*.2f-side*2,.48f,.012f,paint);
        }

        static void BuildScenarioConnectorCues(Transform root,Material metal,Material paint,Material concreteMat)
        {
            var g=new IndustrialGeometry(root,"Scenario connector physical guidance");
            // Actual pavement tops: connector .06, storage0. Flush paint avoids occlusion and collision.
            ScenarioTransitArrow(g,new Vector3(0,.081f,378),Vector3.forward,paint);
            ScenarioTransitArrow(g,new Vector3(14,.081f,405),Vector3.right,paint);
            ScenarioTransitArrow(g,new Vector3(67,.081f,405),Vector3.right,paint);
            ScenarioTransitArrow(g,new Vector3(90,.081f,432),Vector3.forward,paint);
            foreach(var p in new[]{new Vector3(-29,0,405),new Vector3(90,0,375),new Vector3(35,0,465)})
            {
                g.Box(p+new Vector3(0,.18f,0),new Vector3(1.6f,.36f,1.6f),concreteMat,.08f);
                g.IBeam(p+Vector3.up*.36f,p+Vector3.up*8,.24f,.36f,metal);
                g.Box(p+Vector3.up*7,new Vector3(4.4f,2.2f,.15f),metal,.07f);
                if(p.x<0)
                {
                    g.Bar(p+new Vector3(-1.2f,7.65f,-.09f),p+new Vector3(.4f,7,-.09f),.24f,.04f,paint);
                    g.Bar(p+new Vector3(.4f,7,-.09f),p+new Vector3(-1.2f,6.35f,-.09f),.24f,.04f,paint);
                    g.Bar(p+new Vector3(-.2f,7.65f,-.09f),p+new Vector3(1.4f,7,-.09f),.24f,.04f,paint);
                    g.Bar(p+new Vector3(1.4f,7,-.09f),p+new Vector3(-.2f,6.35f,-.09f),.24f,.04f,paint);
                }
                else foreach(float y in new[]{6.5f,7f,7.5f})
                {
                    g.Bar(p+new Vector3(-1.25f,y,-.09f),p+new Vector3(.1f,y+.35f,-.09f),.16f,.04f,paint);
                    g.Bar(p+new Vector3(.1f,y+.35f,-.09f),p+new Vector3(1.3f,y,-.09f),.16f,.04f,paint);
                }
                g.Box(p+new Vector3(0,8.1f,0),new Vector3(.7f,.18f,.4f),metal,.03f);
            }
            g.Finish();
        }

        static void BuildScenarioServiceStock(Transform root,Material greenMat,Material iron,Material zinc,Material paint,Material concreteMat)
        {
            var g=new IndustrialGeometry(root,"Scenario Z1 service islands");
            // West station stores replacement lengths beside the existing abandoned carrier.
            g.Box(new Vector3(-55,.08f,84),new Vector3(10,.16f,13),concreteMat,.06f);
            foreach(float x in new[]{-59f,-51f})foreach(float z in new[]{80f,87f})
            {
                g.Box(new Vector3(x,.27f,z),new Vector3(.8f,.22f,.9f),concreteMat,.04f);
                g.IBeam(new Vector3(x,.38f,z),new Vector3(x,2.7f,z),.15f,.22f,iron);
                g.Bar(new Vector3(x,1.05f,z),new Vector3(x+(x<-55?1.2f:-1.2f),1.05f,z),.16f,.18f,iron);
                g.Bar(new Vector3(x,2.02f,z),new Vector3(x+(x<-55?1.2f:-1.2f),2.02f,z),.16f,.18f,iron);
            }
            foreach(float x in new[]{-58.6f,-57.9f,-51.4f,-52.1f})
            {
                g.Tube(new[]{new Vector3(x,1.43f,78.8f),new Vector3(x,1.43f,88.1f)},.28f,zinc,24,true);
                if(x<-55)g.Tube(new[]{new Vector3(x,2.35f,79.2f),new Vector3(x,2.35f,87.4f)},.22f,greenMat,20,true);
            }
            foreach(float z in new[]{78.2f,89.8f})g.Bar(new Vector3(-59.3f,.173f,z),new Vector3(-50.7f,.173f,z),.11f,.014f,paint);
            // East station: two hose reels stand next to the pump maintenance approach,
            // not on the main yard or the existing cabinet/cable access strip.
            g.Box(new Vector3(121,.02f,53),new Vector3(6,.15f,6),concreteMat,.07f);
            foreach(float z in new[]{51.7f,54.3f})
            {
                foreach(float x in new[]{120.2f,121.8f})
                {
                    g.Bar(new Vector3(x,.1f,z-.6f),new Vector3(x,1.2f,z),.12f,.12f,iron);
                    g.Bar(new Vector3(x,.1f,z+.6f),new Vector3(x,1.2f,z),.12f,.12f,iron);
                }
                g.Rod(new Vector3(120.1f,1.2f,z),new Vector3(121.9f,1.2f,z),.1f,zinc);
                foreach(float x in new[]{120.35f,121.65f})g.Ring(new Vector3(x,1.2f,z),Vector3.right,.85f,.12f,.07f,greenMat,32);
                var hose=Material("ScenarioEnv_dark_service_hose",new Color(.055f,.065f,.057f));hose.SetFloat("_Glossiness",.28f);
                for(int i=0;i<9;i++)g.Ring(new Vector3(120.5f+i*.125f,1.2f,z),Vector3.right,.65f,.49f,.11f,hose,32);
                g.Tube(new[]{new Vector3(121.1f,.59f,z-.2f),new Vector3(121.6f,.16f,z-.8f),new Vector3(123,.16f,z-1),new Vector3(123.5f,.16f,z-.6f)},.07f,hose,12);
            }
            g.Finish();
        }

        static void ReplaceScenarioServiceCarrier(Transform root,Material paint,Material iron,Material zinc,Material warning)
        {
            var old=Obj.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Z1B abandoned repair carrier");
            if(old!=null)
            {
                if(old.GetComponentsInChildren<Collider>(true).Length>0)throw new InvalidOperationException("Carrier replacement requires preserving any existing physics: unexpected collider.");
                old.gameObject.SetActive(false);
            }
            var frame=Matrix4x4.TRS(new Vector3(-64,-.055f,58),Quaternion.Euler(0,22,0),Vector3.one);
            var g=new IndustrialGeometry(root,"Scenario repair carrier close silhouette",frame);
            var rubber=Material("ScenarioEnv_vehicle_rubber",new Color(.035f,.043f,.039f));rubber.SetFloat("_Glossiness",.19f);
            var glass=Material("ScenarioEnv_vehicle_cold_glass",new Color(.13f,.22f,.24f));glass.SetFloat("_Glossiness",.82f);glass.SetFloat("_Metallic",.4f);
            var light=Material("ScenarioEnv_vehicle_unlit_lens",new Color(.62f,.67f,.58f));light.SetFloat("_Glossiness",.7f);
            foreach(float x in new[]{-.85f,.85f})g.IBeam(new Vector3(x,.88f,-7.65f),new Vector3(x,.88f,7.8f),.18f,.28f,iron);
            foreach(float z in new[]{-5.55f,1.8f,3.35f,5.9f})
            {
                g.Rod(new Vector3(-1.35f,.62f,z),new Vector3(1.35f,.62f,z),.13f,iron,12);
                foreach(float side in new[]{-1f,1f})
                {
                    var c=new Vector3(side*1.28f,.62f,z);
                    g.Ring(c,Vector3.right,.62f,.29f,.43f,rubber,36);
                    g.Ring(c+Vector3.right*side*.225f,Vector3.right,.3f,.09f,.07f,zinc,24);
                    for(int t=0;t<24;t++)
                    {
                        float a=t*Mathf.PI/12;var v=new Vector3(0,Mathf.Sin(a),Mathf.Cos(a));
                        g.Bar(c+v*.625f+Vector3.left*.19f,c+v*.625f+Vector3.right*.19f,.022f,.03f,iron);
                    }
                }
            }
            // Split cab: chamfered bonnet, narrowed roof, inset glazing and visible sills.
            g.Box(new Vector3(0,1.32f,-5.8f),new Vector3(2.9f,.75f,3.5f),paint,.16f);
            g.Box(new Vector3(0,2.51f,-5.78f),new Vector3(2.78f,1.6f,2.83f),paint,.18f);
            g.Box(new Vector3(0,3.32f,-5.64f),new Vector3(2.87f,.18f,2.98f),iron,.07f);
            g.Box(new Vector3(0,2.76f,-7.215f),new Vector3(2.35f,.86f,.026f),glass,.045f);
            g.Bar(new Vector3(0,2.3f,-7.24f),new Vector3(0,3.21f,-7.24f),.045f,.04f,iron);
            foreach(float x in new[]{-.58f,.58f})g.Bar(new Vector3(x-.32f,2.36f,-7.26f),new Vector3(x+.18f,2.9f,-7.26f),.028f,.025f,iron);
            foreach(float side in new[]{-1f,1f})
            {
                g.Box(new Vector3(side*1.407f,2.73f,-5.98f),new Vector3(.024f,.91f,1.65f),glass,.02f);
                g.Box(new Vector3(side*1.416f,1.83f,-5.67f),new Vector3(.028f,.72f,1.95f),paint,.025f);
                g.Box(new Vector3(side*1.44f,2.23f,-4.87f),new Vector3(.08f,.06f,.21f),zinc,.02f);
                foreach(float y in new[]{.72f,.98f})g.Box(new Vector3(side*1.43f,y,-4.72f),new Vector3(.35f,.07f,.62f),zinc,.025f);
                g.Bar(new Vector3(side*1.37f,2.85f,-6.68f),new Vector3(side*1.75f,2.85f,-6.79f),.04f,.05f,iron);
                g.Box(new Vector3(side*1.75f,2.69f,-6.79f),new Vector3(.15f,.4f,.24f),iron,.04f);
                g.Box(new Vector3(side*1.75f,2.69f,-6.93f),new Vector3(.12f,.31f,.025f),glass,.02f);
                g.Box(new Vector3(side*1.08f,1.3f,-7.58f),new Vector3(.42f,.27f,.07f),light,.04f);
            }
            for(int i=0;i<7;i++)g.Box(new Vector3(0,1.23f+i*.075f,-7.57f),new Vector3(1.45f,.027f,.045f),iron,.005f);
            g.Box(new Vector3(0,.87f,-7.7f),new Vector3(3.03f,.23f,.25f),zinc,.05f);
            g.Box(new Vector3(0,1.53f,1.82f),new Vector3(2.95f,.27f,11.65f),iron,.04f);
            foreach(float side in new[]{-1f,1f})
            {
                g.Box(new Vector3(side*1.49f,1.61f,1.8f),new Vector3(.08f,.32f,11.7f),paint,.025f);
                for(int i=0;i<8;i++)g.Box(new Vector3(side*1.535f,1.61f,-3.2f+i*1.42f),new Vector3(.02f,.11f,.37f),i%2==0?warning:zinc,.01f);
            }
            foreach(float z in new[]{-.3f,2.35f,5f})
            {
                foreach(float x in new[]{-.83f,.83f})g.Box(new Vector3(x,1.73f,z),new Vector3(.3f,.13f,1.5f),paint,.025f);
                g.Ring(new Vector3(0,2.91f,z),Vector3.right,1.12f,.43f,1.86f,zinc,48);
                foreach(float x in new[]{-.93f,.93f})g.Ring(new Vector3(x,2.91f,z),Vector3.right,1.12f,1.085f,.022f,iron,48);
                foreach(float x in new[]{-.68f,.68f})g.Ring(new Vector3(x,2.91f,z),Vector3.right,1.135f,1.115f,.08f,iron,40);
            }
            g.Finish();
        }

        static void BuildScenarioDamageContinuation(Transform root,Material iron,Material warning)
        {
            var g=new IndustrialGeometry(root,"Scenario Z2 impact trail continuation");
            var scar=Material("ScenarioEnv_scraped_ground",new Color(.25f,.23f,.18f));
            // Northward drag terminates at the existing failed pipe/fire, away from the boss transport footprint.
            foreach(float x in new[]{82f,84.2f,87.1f})
                for(int j=0;j<6;j++)g.Bar(new Vector3(x+j*.1f,.029f,232+j*4.3f),new Vector3(x+.4f+j*.1f,.029f,234.1f+j*4.3f),.13f,.012f,scar);
            foreach(var p in new[]{new Vector3(79,.2f,239),new Vector3(82,.2f,250),new Vector3(84,.2f,258)})
            {
                g.Box(p,new Vector3(1.6f,.18f,2.4f),iron,.035f,Quaternion.Euler(4,22,-3));
                g.Box(p+new Vector3(.3f,.12f,.8f),new Vector3(.09f,.08f,1.4f),warning,.012f,Quaternion.Euler(0,22,0));
            }
            g.Finish();
        }

        static Transform BuildScenarioFirstEncounter(Transform parent)
        {
            var root=Group("Z1 first encounter spatial preview",parent);
            var shell=ScenarioEnvironmentMaterial("intruder scavenged shell",ScenarioEnvironmentTheme(1,"rust",0),new Color(.36f,.29f,.22f));
            var skin=Material("ScenarioEnv_intruder_membrane",new Color(.11f,.17f,.14f));skin.SetFloat("_Glossiness",.34f);
            var nerve=Material("ScenarioEnv_intruder_exposed_tissue",new Color(.72f,.3f,.1f));
            nerve.EnableKeyword("_EMISSION");nerve.SetColor("_EmissionColor",new Color(1,.31f,.08f)*1.4f);
            ScenarioRayCreature(root,"E1",new Vector3(0,30,85),shell,skin,nerve);
            ScenarioRayCreature(root,"E2",new Vector3(18,30,80),shell,skin,nerve);
            var cue=Group("E2 approach cue anchor",root);cue.position=new Vector3(25,32,85);
            ScenarioEnvironmentPhysicalUv(root);
            return root;
        }

        static void ScenarioRayCreature(Transform parent,string id,Vector3 target,Material shell,Material skin,Material nerve)
        {
            var root=Group("Intruder "+id+" - static attack preview",parent);
            Shape("segmented ray torso",PrimitiveType.Sphere,target+new Vector3(0,.08f,1.6f),new Vector3(1.8f,1.25f,3.8f),skin,root,false);
            var g=new IndustrialGeometry(root,"Scenario "+id+" wing spars and shell");
            foreach(float side in new[]{-1f,1f})
            {
                var p=target+new Vector3(side*.6f,.2f,1.2f);
                var tip=target+new Vector3(side*3.2f,.0f,3.4f);
                g.Tube(new[]{p,target+new Vector3(side*1.6f,.45f,2),tip},.14f,shell,12);
                g.Tube(new[]{target+new Vector3(side*.75f,-.2f,2.5f),target+new Vector3(side*1.7f,-1.0f,2.8f),target+new Vector3(side*1.3f,-1.65f,1.7f)},.11f,shell,12);
                var a=target+new Vector3(side*.7f,.12f,.7f);var b=tip;var c=target+new Vector3(side*.8f,-.1f,3.6f);
                if(Vector3.Cross(b-a,c-a).y<0){var swap=b;b=c;c=swap;}
                var mesh=new Mesh{name="Scenario "+id+" membrane "+side};
                mesh.vertices=new[]{a,b,c,a+Vector3.down*.06f,c+Vector3.down*.06f,b+Vector3.down*.06f};mesh.triangles=new[]{0,1,2,3,4,5};
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=SaveDetailMesh(mesh,"Scenario_"+id+"_wing_"+(side<0?"L":"R"));
                var wing=new GameObject("ray membrane");wing.transform.SetParent(root);wing.AddComponent<MeshFilter>().sharedMesh=mesh;wing.AddComponent<MeshRenderer>().sharedMaterial=skin;
            }
            for(int i=0;i<4;i++)g.Box(target+new Vector3(0,.52f,1+i*.6f),new Vector3(1.55f-i*.12f,.25f,.62f),shell,.12f,Quaternion.Euler(-9,0,0));
            g.Tube(new[]{target+new Vector3(0,0,3.4f),target+new Vector3(.1f,-.1f,4.8f),target+new Vector3(.45f,.35f,6.1f)},.08f,skin,12);
            g.Finish();
            Shape("exposed frontal tissue",PrimitiveType.Sphere,target+new Vector3(0,0,.45f),new Vector3(1.5f,1.1f,.9f),nerve,root,false);
            var anchor=Group("Target "+id,root);anchor.position=target;
        }
    }
}

