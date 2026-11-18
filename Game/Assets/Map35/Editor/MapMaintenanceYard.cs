using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // A small, repeatable art pass. This equipment is environmental storytelling, not a new objective.
        static void BuildMaintenanceYard()
        {
            var yard = ArchitectureGroup("B02 maintenance service yard", structures);
            var steel = ThemeMaterial(1, "metal", 0);
            var concrete = ThemeMaterial(1, "concrete", 0);
            var rust = ThemeMaterial(1, "rust", 0);
            var paint = Material("Maintenance_faded_ochre", new Color(.48f,.39f,.18f));
            paint.shader = agedYellow.shader; paint.CopyPropertiesFromMaterial(agedYellow);
            paint.color = new Color(.51f,.43f,.24f);
            var rubber = Material("Maintenance_cable_rubber", new Color(.055f,.06f,.055f));
            rubber.SetFloat("_Glossiness", .23f);
            var dark = Material("Maintenance_drain_recess", new Color(.065f,.071f,.069f));
            var timber = Material("Maintenance_old_timber", new Color(.24f,.20f,.15f));
            var trim = Group("Contact fittings and service markings", yard);

            // The floor of the reused warehouse is at y=1. The service apron outside is at -0.055.
            // A 2.2m personnel strip follows the open east bay, away from collapsed rafters and cables.
            for(int edge=0; edge<2; edge++) for(int segment=0; segment<13; segment++)
            {
                if((segment+edge*2)%7==5) continue;
                float length = segment%3==0 ? 1.0f : 1.35f;
                Box("worn personnel route paint", new Vector3(edge==0?-72.6f:-70.4f,1.012f,30+segment*1.46f),
                    new Vector3(.09f,.012f,length), paint, trim, false);
            }
            // Offset the six human-scale risers west of the existing corner column at (-71,29).
            for(int step=0;step<6;step++)
            {
                float height=(step+1)*(1.055f/6);
                Box("personnel access step "+step,new Vector3(-74.4f,-.055f+height/2,25.62f+step*.34f),
                    new Vector3(1.6f,height,.35f),concrete,yard,true);
                Box("worn step nosing",new Vector3(-74.4f,-.055f+height+.008f,25.48f+step*.34f),
                    new Vector3(1.4f,.012f,.065f),paint,trim,false);
            }
            // Use the placed renderer bound: source bounds/pivot normalization can offset this edge.
            float foundationEdge=Existing("B02").GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Foundation").bounds.min.z;
            float landingStart=27.49f;
            Box("personnel stair top landing",new Vector3(-74.4f,.4725f,(landingStart+foundationEdge)*.5f),new Vector3(1.6f,1.055f,foundationEdge-landingStart+.002f),concrete,yard,true);
            for(int side=0;side<2;side++)
            {
                float x=side==0?-75.23f:-73.57f;
                MaintenanceTube("stair rail "+side,new[]{new Vector3(x,.90f,25.43f),new Vector3(x,1.96f,27.49f),new Vector3(x,1.96f,28.2f)},.032f,steel,trim);
                foreach(float z in new[]{25.65f,27.5f})
                    MaintenanceTube("stair post "+side+" "+z,new[]{new Vector3(x,z<27?.21f:1.01f,z),new Vector3(x,z<27?1:1.96f,z)},.032f,steel,trim);
                Box("dogleg personnel approach",new Vector3(x,1.012f,29),new Vector3(.09f,.012f,2.7f),paint,trim,false);
            }
            Box("dogleg personnel outer turn",new Vector3(-73.8f,1.012f,31.65f),new Vector3(2.9f,.012f,.09f),paint,trim,false);
            // An intact edge rail makes the one-metre loading edge legible without enclosing the entire ruin.
            for(int i=0;i<7;i++)
            {
                float z=30+i*2.75f;
                MaintenanceTube("edge rail upright "+i,new[]{new Vector3(-70.13f,1.04f,z),new Vector3(-70.13f,2.08f,z)},.029f,steel,trim);
                Box("edge rail footplate",new Vector3(-70.13f,1.035f,z),new Vector3(.16f,.05f,.17f),steel,trim,false);
            }
            foreach(float y in new[]{1.56f,2.08f})
                MaintenanceTube("continuous edge rail "+y,new[]{new Vector3(-70.13f,y,30),new Vector3(-70.13f,y,46.5f)},.029f,steel,trim);

            // Utility island: raised cabinet bases, a bolted cable support and connected standby generator.
            foreach(float z in new[]{34f,36.5f})
            {
                Box("cabinet concrete plinth",new Vector3(-74,1.08f,z),new Vector3(.82f,.16f,.82f),concrete,yard,true);
                foreach(float dx in new[]{-.31f,.31f}) foreach(float dz in new[]{-.31f,.31f})
                    Cylinder("cabinet anchor",new Vector3(-74+dx,1.19f,z+dz),.025f,.06f,rust,trim,false);
            }
            Box("generator drip tray base",new Vector3(-78,1.055f,33),new Vector3(1.5f,.11f,1.15f),steel,yard,true);
            foreach(float x in new[]{-78.75f,-77.25f})
                Box("generator tray edge",new Vector3(x,1.14f,33),new Vector3(.035f,.14f,1.18f),steel,trim,false);
            foreach(float z in new[]{32.43f,33.57f})
                Box("generator tray edge",new Vector3(-78,1.14f,z),new Vector3(1.5f,.14f,.035f),steel,trim,false);
            foreach(float z in new[]{32.8f,37.7f})
            {
                Box("service rack upright",new Vector3(-74.7f,2.07f,z),new Vector3(.09f,2.1f,.09f),steel,trim,false);
                Box("service rack bolted foot",new Vector3(-74.7f,1.04f,z),new Vector3(.32f,.08f,.32f),steel,trim,false);
            }
            Box("service rack header",new Vector3(-74.7f,3.08f,35.25f),new Vector3(.09f,.09f,4.98f),steel,trim,false);
            foreach(float x in new[]{-74.86f,-74.54f})
                Box("open cable tray side",new Vector3(x,2.77f,35.25f),new Vector3(.035f,.11f,4.8f),steel,trim,false);
            for(int i=0;i<17;i++)
                Box("cable tray rung",new Vector3(-74.7f,2.73f,32.9f+i*.29f),new Vector3(.35f,.025f,.045f),steel,trim,false);
            MaintenanceTube("generator feeder",new[]{new Vector3(-77.65f,1.36f,33),new Vector3(-77.1f,1.06f,33.1f),new Vector3(-76.3f,1.06f,33.5f),new Vector3(-75.5f,1.06f,33.2f),new Vector3(-74.65f,1.06f,33.5f),new Vector3(-74.35f,1.31f,34)},.027f,rubber,trim,true);
            MaintenanceTube("cabinet branch feeder",new[]{new Vector3(-74.35f,1.31f,34),new Vector3(-74.65f,1.06f,34.45f),new Vector3(-75.05f,1.06f,35.35f),new Vector3(-74.65f,1.06f,36.1f),new Vector3(-74.35f,1.31f,36.5f)},.024f,rubber,trim,true);
            foreach(float z in new[]{34f,36.5f})
                MaintenanceTube("cabinet tray riser "+z,new[]{new Vector3(-74.35f,1.36f,z),new Vector3(-74.7f,1.6f,z),new Vector3(-74.7f,2.72f,z),new Vector3(-74.7f,2.78f,z+.25f)},.022f,rubber,trim,true);
            MaintenanceTube("tray surviving loom",new[]{new Vector3(-74.7f,2.79f,33),new Vector3(-74.7f,2.79f,37.4f)},.032f,rubber,trim);
            // Two rails and loose replacement stock indicate the abandoned maintenance job.
            foreach(float x in new[]{-83f,-80f})
                Box("repair stock trestle",new Vector3(x,1.22f,32),new Vector3(.22f,.44f,2.2f),steel,yard,true);
            for(int i=0;i<5;i++)
                MaintenanceTube("replacement pipe on trestle "+i,new[]{new Vector3(-83.5f,1.64f,31.2f+i*.38f),new Vector3(-79.5f,1.64f,31.2f+i*.38f)},.13f,steel,trim);
            foreach(float x in new[]{-82.7f,-80.3f})
                Box("stock separator timber",new Vector3(x,1.47f,32),new Vector3(.18f,.08f,2),timber,trim,false);

            // Surface-mounted slotted drainage covers lead toward the existing apron drainage network.
            for(int bay=0;bay<9;bay++)
            {
                float x=-100+bay*4;
                Box("drain recess",new Vector3(x,-.042f,24),new Vector3(3.94f,.018f,.38f),dark,trim,false);
                foreach(float z in new[]{23.78f,24.22f})
                    Box("drain cover frame",new Vector3(x,-.022f,z),new Vector3(3.96f,.04f,.035f),steel,trim,false);
                for(int grate=0;grate<20;grate++)
                    Box("drain grate",new Vector3(x-1.88f+grate*.197f,-.019f,24),new Vector3(.058f,.045f,.42f),steel,trim,false);
            }

            // Reuse the seven angular fragments, but place them as a connected fall fan under the failed rafter.
            var fragments=ruins.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("B02 angular concrete fracture")).OrderBy(t=>t.name).ToArray();
            var fallPoints=new[]{new Vector3(-93,0,38),new Vector3(-90.5f,0,39.5f),new Vector3(-87.9f,0,41),new Vector3(-85.7f,0,42),new Vector3(-83.3f,0,43.4f),new Vector3(-81.6f,0,45.1f),new Vector3(-78.9f,0,46.5f)};
            for(int i=0;i<fragments.Length;i++)
            {
                var t=fragments[i];t.position=fallPoints[i%fallPoints.Length];t.localScale=Vector3.one*(i%3==0?.8f:.55f);
                t.rotation=Quaternion.Euler(i*3,28+i*21,-8+i*2);
                var b=t.GetComponent<MeshRenderer>().bounds;t.position+=Vector3.up*(1.015f-b.min.y);
                t.SetParent(yard,true);
            }
            MaintenanceScorch("fire source soot on slab",new Vector3(-104,1.024f,47),new Vector2(12,9),.7f,yard);
            MaintenanceScorch("impact dust beneath failed rafter",new Vector3(-87,1.022f,42),new Vector2(18,8),.24f,yard);
            // These fire origins were previously below the raised foundation.
            foreach(Transform t in ruins)
                if(t.position.x>-110&&t.position.x<-100&&t.position.z>42&&t.position.z<53)
                {
                    if(t.GetComponent<ParticleSystem>()!=null&&t.position.y<.1f)t.position+=Vector3.up*1.03f;
                    if(t.name=="glowing debris"&&t.position.y<1)t.position+=Vector3.up*1.03f;
                    if(t.name=="fire bounce"&&t.position.y<4)t.position+=Vector3.up*1.03f;
                }

            // Fixed tiny fittings share a few meshes; preserve actual stair/plinth colliders separately.
            int fittings=trim.GetComponentsInChildren<MeshRenderer>().Length;
            CombineMaintenanceFittings(trim);
            foreach(var renderer in yard.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
            AddMaintenanceWorkLight(yard,steel,rubber);
            var bounds=LegacyEnvironmentAudit.BoundsOf(yard.gameObject);
            if(bounds.max.x>-59.5f)throw new InvalidOperationException("Maintenance detailing intrudes into Z1 gameplay apron.");
            var report=new StringBuilder();
            report.AppendLine("B02 maintenance yard: personnel access, connected standby power, repair stock, drainage, motivated roof fall.");
            report.AppendLine("Personnel strip: x[-72.6,-70.4], z[29.5,49], surface y=1; nominal width 2.2m.");
            report.AppendLine("Six 0.17583m risers centred x=-74.4 join external apron y=-0.055 to floor y=1; dogleg avoids existing column (-71,29).");
            report.AppendLine("Landing z: "+landingStart+" to placed foundation edge "+foundationEdge);
            report.AppendLine("Cabinets: (-74,34),(-74,36.5), generator (-78,33); final ground contact resolved against plinths.");
            report.AppendLine("Removed 36 random radius-16m slabs/girders and their circular burn footprint; retained seven scaled, grounded fall fragments.");
            report.AppendLine("Fittings combined: "+fittings+" -> "+trim.GetComponentsInChildren<MeshRenderer>().Length+" material meshes.");
            report.AppendLine("Yard renderer bounds: "+bounds.min+" to "+bounds.max);
            report.AppendLine("Existing 3/4-plan routes, objectives, review cameras and palette preserved. Static environmental art only.");
            File.WriteAllText(WideOutput+"/maintenance-yard-audit.txt",report.ToString());
            Debug.Log("B02_MAINTENANCE_YARD_READY");
        }

        static void AddMaintenanceWorkLight(Transform yard,Material steel,Material rubber)
        {
            var p=new Vector3(-73.15f,1.04f,32.2f);
            var support=Group("Generator supplied portable work light",yard);
            MaintenanceTube("work light upright",new[]{p,p+Vector3.up*2.8f},.035f,steel,support);
            for(int i=0;i<3;i++)
            {
                float angle=i*Mathf.PI*2/3;
                MaintenanceTube("work light tripod "+i,new[]{p+Vector3.up*.45f,p+new Vector3(Mathf.Cos(angle)*.48f,0,Mathf.Sin(angle)*.48f)},.025f,steel,support);
            }
            var lampPosition=p+Vector3.up*2.75f;
            var rotation=Quaternion.LookRotation(new Vector3(-78,1.35f,35)-lampPosition);
            var housing=Box("work lamp vented housing",lampPosition,new Vector3(.62f,.35f,.14f),steel,support,false);housing.transform.rotation=rotation;
            var lens=Material("Maintenance_work_lamp_emitter",new Color(.95f,.8f,.55f));lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(2.4f,1.7f,.9f));
            var face=Box("work lamp luminous diffuser",lampPosition+rotation*new Vector3(0,0,.078f),new Vector3(.52f,.25f,.015f),lens,support,false);face.transform.rotation=rotation;
            var lamp=new GameObject("B02 warm emergency task light").AddComponent<Light>();lamp.transform.SetParent(support,true);
            lamp.transform.SetPositionAndRotation(lampPosition+rotation*new Vector3(0,0,.14f),rotation);
            lamp.type=LightType.Spot;lamp.color=new Color(1,.79f,.52f);lamp.intensity=5.5f;lamp.range=15;lamp.spotAngle=84;lamp.innerSpotAngle=48;
            lamp.shadows=LightShadows.Soft;lamp.shadowStrength=.65f;lamp.shadowBias=.025f;lamp.shadowNormalBias=.12f;lamp.renderMode=LightRenderMode.ForcePixel;
            MaintenanceTube("work light generator lead",new[]{p+Vector3.up*.2f,new Vector3(-73.3f,1.06f,31.8f),new Vector3(-75,1.06f,31.3f),new Vector3(-77.3f,1.06f,32.3f),new Vector3(-77.65f,1.36f,33)},.018f,rubber,support,true);
        }

        static void RefineWorldStencilDepth()
        {
            int count=0;
            foreach(var text in Obj.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(text.transform.IsChildOf(labels))continue;
                var renderer=text.GetComponent<MeshRenderer>();if(renderer.sharedMaterial==null)continue;
                var atlas=renderer.sharedMaterial.mainTexture;
                var material=Material("World_stencil_depth_"+(atlas==null?"default":atlas.name),Color.white);
                material.shader=Shader.Find("Anseong/SurfaceStencil");material.mainTexture=atlas;
                renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;count++;
            }
            File.WriteAllText(WideOutput+"/world-stencil-depth.txt",count+" world stencils depth-tested; review overlay labels excluded.\n");
        }

        static void ValidateMaintenanceAccess()
        {
            var path=new[]{new Vector3(-74.4f,1,27.55f),new Vector3(-74.4f,1,30.65f),new Vector3(-71.5f,1,30.65f),new Vector3(-71.5f,1,48.4f)};
            Physics.SyncTransforms();int samples=0;var problems=new List<string>();
            for(int segment=0;segment<path.Length-1;segment++)
            {
                int steps=Mathf.CeilToInt(Vector3.Distance(path[segment],path[segment+1])/.25f);
                for(int i=0;i<=steps;i++)
                {
                    var p=Vector3.Lerp(path[segment],path[segment+1],i/(float)steps);samples++;
                    if(!Physics.Raycast(p+Vector3.up*.15f,Vector3.down,out var support,.3f)||Mathf.Abs(support.point.y-1)>.04f)
                        problems.Add("unsupported floor at "+p);
                    var blocked=Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.28f)
                        .Where(c=>!c.isTrigger&&c.transform.IsChildOf(structures)).Select(c=>c.name).ToArray();
                    if(blocked.Length>0)problems.Add(p+": "+string.Join(";",blocked));
                }
            }
            File.WriteAllText(WideOutput+"/maintenance-access-check.txt","Landing and dogleg/east walkway: "+samples+" floor/capsule samples.\n"+
                "Support gaps or static solid obstructions: "+problems.Count+"\n"+string.Join("\n",problems)+"\nEnvironmental access geometry check; no walking-player controller was added.\n");
            if(problems.Count>0)throw new InvalidOperationException("Maintenance access obstruction: "+problems[0]);
        }

        static void MaintenanceScorch(string name,Vector3 p,Vector2 size,float opacity,Transform parent)
        {
            var mat=Material("Maintenance_"+name.Replace(' ','_'),new Color(.14f,.13f,.11f,opacity));
            mat.shader=Shader.Find("Anseong/SurfaceSoot");
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"Textures/SootDecal_AI_v1.png");
            var go=Shape(name,PrimitiveType.Quad,p,new Vector3(size.x,size.y,1),mat,parent,false);
            go.transform.rotation=Quaternion.Euler(90,18,0);
            go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        static void MaintenanceTube(string name,Vector3[] controls,float radius,Material material,Transform parent,bool smooth=false)
        {
            var points=new List<Vector3>();
            for(int i=0;i<controls.Length-1;i++)
            {
                int samples=smooth?6:1;
                for(int s=0;s<samples;s++)
                {
                    float t=s/(float)samples;
                    var a=controls[Mathf.Max(0,i-1)];var b=controls[i];var c=controls[i+1];var d=controls[Mathf.Min(controls.Length-1,i+2)];
                    points.Add(smooth?.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t):b);
                }
            }
            points.Add(controls[controls.Length-1]);
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();float distance=0;
            const int sides=8;
            for(int i=0;i<points.Count;i++)
            {
                var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;
                var across=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized;
                var up=Vector3.Cross(tangent,across).normalized;
                if(i>0)distance+=Vector3.Distance(points[i],points[i-1]);
                for(int s=0;s<sides;s++)
                {float a=s*Mathf.PI*2/sides;vertices.Add(points[i]+radius*(Mathf.Cos(a)*across+Mathf.Sin(a)*up));uv.Add(new Vector2(s/(float)sides,distance));}
                if(i>0)for(int s=0;s<sides;s++)
                {int a=(i-1)*sides+s,b=(i-1)*sides+(s+1)%sides,c=i*sides+s,d=i*sides+(s+1)%sides;triangles.AddRange(new[]{a,b,c,b,d,c});}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            var go=new GameObject(name);go.transform.SetParent(parent,true);
            go.AddComponent<MeshFilter>().sharedMesh=SaveDetailMesh(mesh,"Maintenance_"+name.Replace(' ','_'));
            go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        static void CombineMaintenanceFittings(Transform root)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            foreach(var group in renderers.GroupBy(r=>r.sharedMaterial))
            {
                var combines=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines,true,true);
                var go=new GameObject("Maintenance fittings "+group.Key.name);go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=SaveDetailMesh(mesh,"Maintenance_combined_"+group.Key.name);
                go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var renderer in renderers)Obj.DestroyImmediate(renderer.gameObject);
        }
    }
}
