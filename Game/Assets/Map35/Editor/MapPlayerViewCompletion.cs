using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static void CompletePlayerFacingMap()
        {
            CompleteDeploymentInterlock();
            RepairLegacyVehicleGroundContact();
            // The settled roll rests on R1's 0.6m top; its wound-sheet edge extends 14mm below the nominal roll.
            var settled=viewer.lateCoil.transform.position;settled.y=2.62f;viewer.lateCoil.transform.position=settled;
            CompleteMotivatedWorkLighting();
            File.WriteAllText(WideOutput+"/player-facing-completion.txt",
                "I1 industrial sliding interlock: segmented pressure face, reinforcing ribs, open upper guard, rollers, supported overhead rail and continuous chain drive.\n"+
                "Preserved push/status/latch target positions. The PC3 latch has a physical side cradle and PC4 state has dual-face indicators.\n"+
                "Settled coil centre corrected to y2.62m above R1 top y0.6m; suspended height and horizontal settlement unchanged.\n"+
                "Six installed work lights and physical fixtures link departure, repair platform, pump service, valve maintenance, crane control and furnace entrance.\n"+
                "Visual map construction only; no new network interaction or HMD approval inferred.\n");
        }

        static void CompleteDeploymentInterlock()
        {
            var metal=ThemeMaterial(0,"metal",0);var paint=ThemeMaterial(0,"paint",0);
            var warning=ThemeMaterial(6,"warning",0);var trim=ThemeMaterial(7,"metal",0);
            var rubber=Material("Interlock_rubber_contact",new Color(.065f,.075f,.073f));
            var signal=Material("Interlock_cool_status",new Color(.18f,.66f,.58f));
            signal.EnableKeyword("_EMISSION");signal.SetColor("_EmissionColor",new Color(.12f,.6f,.42f)*1.6f);
            var waiting=Material("Interlock_amber_status",new Color(.9f,.42f,.06f));
            waiting.EnableKeyword("_EMISSION");waiting.SetColor("_EmissionColor",new Color(.9f,.32f,.04f)*1.5f);
            if(viewer.plate!=null)Obj.DestroyImmediate(viewer.plate.gameObject);
            var oldRail=details.Find("plate rail");if(oldRail!=null)Obj.DestroyImmediate(oldRail.gameObject);
            foreach(string name in new[]{"I1 push face","I1 control status","I1 drone latch"})
            {var old=details.Find(name);if(old!=null)Obj.DestroyImmediate(old.gameObject);}
            var fixedRoot=Group("I1 fixed rail and role equipment",details);
            var moving=Group("I1 sliding plate",details);moving.position=new Vector3(0,24,8);viewer.plate=moving;
            // Preserve the original moving pivot. Face panels are subordinate to the actual frame.
            foreach(float x in new[]{-4.8f,4.8f})Box("Interlock vertical edge",new Vector3(x,24,8),new Vector3(.42f,8,.7f),metal,moving,false);
            foreach(float y in new[]{20.2f,24.9f,27.8f})Box("Interlock transverse stiffener",new Vector3(0,y,8.1f),new Vector3(9.6f,.38f,.7f),metal,moving,false);
            for(int i=0;i<3;i++)
            {
                float x=-3.1f+i*3.1f;
                Box("Interlock replaceable impact panel "+i,new Vector3(x,22.8f,7.9f),new Vector3(2.96f,4.3f,.34f),paint,moving,false);
                Box("Interlock impact rib "+i,new Vector3(x,22.9f,7.56f),new Vector3(.22f,3.5f,.45f),metal,moving,false);
                foreach(float y in new[]{21.1f,24.3f})foreach(float dx in new[]{-.95f,.95f})
                    InterlockAxle("Captured panel bolt",new Vector3(x+dx,y,7.55f),.11f,.13f,trim,moving);
            }
            Box("I1 pressure shoe backing",new Vector3(0,24,7.48f),new Vector3(3.4f,1.5f,.25f),trim,moving,false);
            for(int i=0;i<5;i++)Box("I1 replaceable rubber contact",new Vector3(-1.25f+i*.625f,24,7.28f),new Vector3(.36f,1.15f,.18f),rubber,moving,false);
            foreach(float x in new[]{-4.2f,4.2f})
            {
                Box("Interlock edge safety stripe",new Vector3(x,22.8f,7.69f),new Vector3(.4f,4.1f,.035f),warning,moving,false);
                for(int i=0;i<6;i++)
                {var stripe=Box("Interlock diagonal edge marker",new Vector3(x,21.1f+i*.63f,7.655f),new Vector3(.47f,.18f,.018f),trim,moving,false);stripe.transform.rotation=Quaternion.Euler(0,0,-35);}
            }
            // Upper guard stays visually porous so the exit remains visible around the task.
            for(float x=-4.3f;x<4.4f;x+=.72f)Box("Interlock upper guard",new Vector3(x,26.3f,8.14f),new Vector3(.075f,2.2f,.11f),metal,moving,false);
            foreach(float x in new[]{-3.9f,-1.3f,1.3f,3.9f})
            {
                Box("Interlock suspended hanger",new Vector3(x,30.1f,8.1f),new Vector3(.24f,4.5f,.32f),metal,moving,false);
                Box("Interlock roller saddle",new Vector3(x,32.2f,8.1f),new Vector3(.7f,.9f,.8f),metal,moving,false);
                InterlockAxle("Interlock guide roller",new Vector3(x,32.7f,8.05f),.34f,.8f,trim,moving);
                InterlockAxle("Interlock roller hub",new Vector3(x,32.7f,7.57f),.13f,.16f,metal,moving);
            }
            foreach(float y in new[]{32.35f,33.05f})Box("Interlock rail flange",new Vector3(-9,y,8.05f),new Vector3(42,.2f,1.05f),metal,fixedRoot,false);
            Box("Interlock rail web",new Vector3(-9,32.7f,8.05f),new Vector3(42,.6f,.16f),metal,fixedRoot,false);
            foreach(float x in new[]{-27f,-16f,11.5f})
            {
                Box("Rail lintel mounting pad",new Vector3(x,39,-.05f),new Vector3(1.7f,2,.35f),metal,fixedRoot,false);
                Beam("Rail diagonal hanger",new Vector3(x,39,.1f),new Vector3(x,33.1f,8.05f),.38f,metal,fixedRoot);
                Beam("Rail side tie",new Vector3(x,33,.3f),new Vector3(x,33,8.05f),.3f,metal,fixedRoot);
            }
            // An overhead continuous chain can support the full 22m travel without a disconnected piston.
            Box("Interlock drive motor seat",new Vector3(-27,33.5f,8.1f),new Vector3(2,1.1f,1.35f),metal,fixedRoot,false);
            InterlockAxle("Interlock geared drive casing",new Vector3(-27,33.5f,7.5f),.6f,.75f,trim,fixedRoot);
            foreach(float x in new[]{-28.3f,10.5f})InterlockAxle("Interlock chain sprocket",new Vector3(x,33.6f,8.4f),.32f,.28f,metal,fixedRoot);
            MaintenanceTube("Interlock continuous drive chain",new[]{new Vector3(-28.3f,33.92f,8.4f),new Vector3(10.5f,33.92f,8.4f),new Vector3(10.82f,33.6f,8.4f),new Vector3(10.5f,33.28f,8.4f),new Vector3(-28.3f,33.28f,8.4f),new Vector3(-28.62f,33.6f,8.4f),new Vector3(-28.3f,33.92f,8.4f)},.07f,trim,fixedRoot);
            Beam("Interlock moving chain pickup",new Vector3(-3.9f,32.7f,8.1f),new Vector3(-3.9f,33.28f,8.4f),.2f,metal,moving);
            MaintenanceTube("Interlock drive conduit",new[]{new Vector3(-27,33.5f,8.4f),new Vector3(-27,34,1),new Vector3(-27,18,1)},.065f,rubber,fixedRoot);
            Box("I1 PC3 latch cradle",new Vector3(8,26,8),new Vector3(1.8f,2.3f,1.4f),metal,fixedRoot,false);
            Box("I1 PC3 protective shoulder",new Vector3(8,27.25f,8),new Vector3(2.2f,.18f,1.65f),warning,fixedRoot,false);
            foreach(float z in new[]{7.5f,8.5f})
                Box("I1 PC3 exposed locking jaw",new Vector3(9.02f,26,z),new Vector3(.22f,1.1f,.22f),warning,fixedRoot,false);
            var pinSteel=Material("Interlock_exposed_pin_satin_steel",new Color(.72f,.76f,.71f));
            pinSteel.SetFloat("_Metallic",.6f);pinSteel.SetFloat("_Glossiness",.35f);
            var pin=Cylinder("I1 PC3 mechanical release pin",new Vector3(9.05f,26,8),.28f,.35f,pinSteel,fixedRoot,false);pin.transform.rotation=Quaternion.Euler(0,0,90);
            Box("I1 PC3 pin approach witness",new Vector3(9.15f,26.65f,8),new Vector3(.04f,.14f,.6f),waiting,fixedRoot,false);
            Beam("I1 latch fixed attachment",new Vector3(8,26.8f,8),new Vector3(13,26.8f,.2f),.3f,metal,fixedRoot);
            Box("I1 PC4 load monitor housing",new Vector3(-5.4f,26,8),new Vector3(1.4f,1.8f,.6f),trim,fixedRoot,false);
            foreach(float z in new[]{7.67f,8.33f})
            {
                Box("I1 PC4 held load amber bar",new Vector3(-5.4f,26.35f,z),new Vector3(.9f,.25f,.04f),waiting,fixedRoot,false);
                Box("I1 PC4 support power bar",new Vector3(-5.4f,25.75f,z),new Vector3(.9f,.25f,.04f),signal,fixedRoot,false);
            }
            Beam("I1 monitor bracket",new Vector3(-5.4f,26.8f,8),new Vector3(-5.4f,32.7f,8),.18f,metal,fixedRoot);
            Sign("LOAD HOLD",new Vector3(-5.4f,26.05f,7.66f),Quaternion.identity,.019f,fixedRoot);
            Sign("LOAD HOLD",new Vector3(-5.4f,26.05f,8.34f),Quaternion.Euler(0,180,0),.019f,fixedRoot);
            foreach(var text in fixedRoot.GetComponentsInChildren<TextMesh>())
            {
                var bounds=text.GetComponent<MeshRenderer>().bounds;
                text.transform.localScale*=Mathf.Min(1,.95f/Mathf.Max(.001f,bounds.size.x));
            }
        }

        static void InterlockAxle(string name,Vector3 p,float radius,float depth,Material mat,Transform parent)
        {var go=Cylinder(name,p,radius,depth,mat,parent,false);go.transform.rotation=Quaternion.Euler(90,0,0);}

        static void RepairLegacyVehicleGroundContact()
        {
            Physics.SyncTransforms();
            var report=new StringBuilder("object,old_y,new_y,lowest_mesh_vertex,support_y\n");
            foreach(var t in ruins.GetComponentsInChildren<Transform>().Where(t=>t.name=="Z2A-2 abandoned steel convoy"||t.name.StartsWith("Z2A crushed container ")).ToArray())
            {
                // Rotated mesh bounds can include empty corners. Measure the actual transformed vertices.
                float minY=float.PositiveInfinity;
                foreach(var f in t.GetComponentsInChildren<MeshFilter>())
                    foreach(var v in f.sharedMesh.vertices)minY=Mathf.Min(minY,f.transform.TransformPoint(v).y);
                if(float.IsInfinity(minY))continue;
                float support=0;var hits=Physics.RaycastAll(t.position+Vector3.up*8,Vector3.down,20);
                foreach(var h in hits)if(h.normal.y>.9f&&h.collider.transform.IsChildOf(structures)&&h.point.y<2) support=Mathf.Max(support,h.point.y);
                float previous=t.position.y;t.position+=Vector3.up*(support+.018f-minY);
                report.AppendLine(t.name+","+previous+","+t.position.y+","+minY+","+support);
            }
            File.WriteAllText(WideOutput+"/legacy-vehicle-ground-repair.csv",report.ToString());
        }

        static void CompleteMotivatedWorkLighting()
        {
            var root=Group("18 Installed district work lighting",structures);
            var steel=ThemeMaterial(0,"metal",0);
            Beam("Departure light lintel cross support",new Vector3(-13,39,.4f),new Vector3(13,39,.4f),.3f,steel,root);
            Beam("Departure light overhang support",new Vector3(0,39,.4f),new Vector3(0,38,5.5f),.25f,steel,root);
            Beam("Repair bay wall arm",new Vector3(-29,26,-34),new Vector3(-25,26,-34),.16f,steel,root);
            Beam("Repair bay wall brace",new Vector3(-29,28,-34),new Vector3(-25,26,-34),.12f,steel,root);
            Beam("Pump wall light outrigger",new Vector3(83,11,34.2f),new Vector3(83,11,32),.14f,steel,root);
            Box("Valve flood mast concrete footing",new Vector3(-149,.3f,330),new Vector3(1.5f,.6f,1.5f),ThemeMaterial(5,"concrete",0),root,false);
            Beam("Valve anchored flood mast",new Vector3(-149,.6f,330),new Vector3(-149,26,330),.25f,steel,root);
            Beam("Crane deck flood mast",new Vector3(141,46,551),new Vector3(141,51.5f,551),.15f,steel,root);
            Beam("Furnace frontage light bracket",new Vector3(105,22,665),new Vector3(105,22,662),.2f,steel,root);
            InstalledMapSpot("Departure rail task light",new Vector3(0,38,5.5f),new Vector3(0,24,8),new Color(1,.77f,.46f),7.5f,24,62,true,root);
            InstalledMapSpot("Repair platform service light",new Vector3(-25,26,-34),new Vector3(-23,16,-35),new Color(1,.8f,.55f),5,22,75,false,root);
            InstalledMapSpot("Pump service entrance light",new Vector3(83,11,32),new Vector3(95,1,29),new Color(.69f,.83f,.9f),4.5f,24,74,false,root);
            InstalledMapSpot("Valve service safety light",new Vector3(-149,26,330),new Vector3(-143,19,332),new Color(1,.7f,.36f),4.8f,19,66,false,root);
            InstalledMapSpot("Crane latch inspection light",new Vector3(141,51.5f,551),new Vector3(143,46,555),new Color(1,.8f,.49f),5.5f,17,70,true,root);
            InstalledMapSpot("Furnace emergency access light",new Vector3(105,22,662),new Vector3(100,1,663),new Color(1,.65f,.32f),8,34,70,false,root);
        }

        static void InstalledMapSpot(string name,Vector3 origin,Vector3 target,Color color,float intensity,float range,float angle,bool shadow,Transform parent)
        {
            var root=Group(name,parent);var metal=ThemeMaterial(0,"metal",0);
            var emitter=Material("Luminaire_"+name.Replace(' ','_'),color);
            emitter.EnableKeyword("_EMISSION");emitter.SetColor("_EmissionColor",color*2.2f);
            var rotation=Quaternion.LookRotation(target-origin);
            var shell=Box("Weatherproof luminaire housing",origin,new Vector3(1.25f,.5f,.5f),metal,root,false);shell.transform.rotation=rotation;
            var lens=Box("Diffused luminaire face",origin+rotation*new Vector3(0,0,.27f),new Vector3(1.1f,.32f,.035f),emitter,root,false);lens.transform.rotation=rotation;
            Beam("Luminaire mounting bracket",origin,origin+new Vector3(0,.65f,-.7f),.11f,metal,root);
            var source=new GameObject(name+" spot").AddComponent<Light>();source.transform.SetParent(root);
            source.transform.SetPositionAndRotation(origin+rotation*new Vector3(0,0,.34f),rotation);
            source.type=LightType.Spot;source.color=color;source.intensity=intensity;source.range=range;source.spotAngle=angle;source.innerSpotAngle=angle*.57f;
            source.shadows=shadow?LightShadows.Soft:LightShadows.None;source.shadowStrength=.62f;source.shadowBias=.04f;source.shadowNormalBias=.15f;
            source.lightmapBakeType=LightmapBakeType.Mixed;source.bounceIntensity=.6f;
        }

        static void AddWholeMapReviewViews()
        {
            cameraList.Clear();cameraList.AddRange(viewer.views);
            MakeCamera("39 B03 southeast process connections",new Vector3(118,10,18),new Vector3(90,6,45),65);
            MakeCamera("40 B03 northwest envelope",new Vector3(62,10,70),new Vector3(90,6,45),65);
            MakeCamera("41 B03 control process layout",new Vector3(90,48,95),new Vector3(90,5,45),65);
            MakeCamera("42 Mech Z1 right side pump yard",new Vector3(0,29.75f,0),new Vector3(90,6,45),65);
            viewer.views=cameraList.ToArray();
        }
    }
}
