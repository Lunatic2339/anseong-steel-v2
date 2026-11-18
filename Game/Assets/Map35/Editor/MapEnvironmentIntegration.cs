using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static Transform environmentIntegration;
        static Material integrationSlope,integrationRetaining,integrationRepair,integrationTidal;
        static readonly StringBuilder environmentConnections=new StringBuilder();
        static int integrationCoastEdges,integrationSupportCount;

        public static void DiagnoseCoastSurfaceRays()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            var c=new GameObject("Temporary coast diagnostic camera").AddComponent<Camera>();
            c.transform.position=new Vector3(450,3,776);c.transform.LookAt(new Vector3(448,0,812));c.fieldOfView=65;c.aspect=1.6f;c.nearClipPlane=.1f;
            var ignored=new HashSet<Renderer>(Obj.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).SelectMany(g=>g.GetLODs().Skip(1)).SelectMany(l=>l.renderers));
            var meshes=Obj.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.GetComponent<Renderer>()!=null&&!ignored.Contains(f.GetComponent<Renderer>())&&
                (f.name.StartsWith("Integration coastal rock volume",StringComparison.Ordinal)||f.name=="Integration coastal saddle closure"||f.transform.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("coast_rocks_01"))))
                .Select(f=>(name:f.name,vertices:f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray(),triangles:f.sharedMesh.triangles)).ToArray();
            var report=new StringBuilder("px,py,water_distance,rock_distance,rock_name,triangle,world_x,world_y,world_z,normal_y,facing_dot,front_distance\n");
            for(int py=525;py<=595;py+=5)for(int px=460;px<=560;px+=5)
            {
                var ray=c.ViewportPointToRay(new Vector3(px/1600f,1-py/1000f,0));float nearest=9999,front=9999;string name="";int face=-1;Vector3 normal=Vector3.zero;
                foreach(var m in meshes)for(int t=0;t<m.triangles.Length;t+=3)
                {
                    Vector3 a=m.vertices[m.triangles[t]],b=m.vertices[m.triangles[t+1]],d=m.vertices[m.triangles[t+2]];
                    float distance=RayTriangle(ray,a,b,d);if(distance<=0)continue;var n=Vector3.Cross(b-a,d-a).normalized;
                    if(Vector3.Dot(n,ray.direction)<0&&distance<front)front=distance;
                    if(distance>=nearest)continue;nearest=distance;name=m.name;face=t/3;normal=n;
                }
                var point=ray.GetPoint(nearest);float water=(-.6f-ray.origin.y)/ray.direction.y;
                report.AppendLine(px+","+py+","+water.ToString(Inv)+","+nearest.ToString(Inv)+","+name+","+face+","+point.x.ToString(Inv)+","+point.y.ToString(Inv)+","+point.z.ToString(Inv)+","+normal.y.ToString(Inv)+","+Vector3.Dot(normal,ray.direction).ToString(Inv)+","+front.ToString(Inv));
            }
            Directory.CreateDirectory(WideOutput);File.WriteAllText(WideOutput+"/coast-ray-diagnosis.csv",report.ToString());Obj.DestroyImmediate(c.gameObject);
            Debug.Log("COAST_RAY_DIAGNOSIS_OK candidates="+meshes.Length);
        }
        static void SealCoastSaddle()
        {
            var side=GameObject.Find("Integration coastal rock volume 4 LOD0 part0");if(side==null)return;
            var parent=GameObject.Find("18 Connected landscape and steelworks").transform;
            var old=parent.Find("Integration coastal saddle closure");if(old!=null)Obj.DestroyImmediate(old.gameObject);
            // Ray diagnosis located two back-facing folds, not holes in the
            // scanned source: x442.15..443.04 / z790.74..795.15. This small
            // embedded solid saddle closes only that concave junction.
            const int nx=12,nz=32;const float cx=442.55f,cz=792.9f,rx=1.45f,rz=4;
            var data=new LandscapeMesh();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float u=2f*x/nx-1,v=2f*z/nz-1;
                float profile=Mathf.Pow(Mathf.Max(0,1-u*u-v*v),.22f);
                float h=-.8f+(1.48f+.085f*v*rz)*profile;
                data.Add(new Vector3(cx+u*rx,h,cz+v*rz),new Color(.7f,Mathf.Clamp01((.55f-h)*.65f),0,1));
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;data.Tri(a,c,b);data.Tri(b,c,d);}
            // The perimeter and bottom are below the water and adjoining rock.
            data.Quad(new Vector3(cx-rx,-.8f,cz-rz),new Vector3(cx-rx,-1.35f,cz-rz),new Vector3(cx+rx,-1.35f,cz-rz),new Vector3(cx+rx,-.8f,cz-rz));
            data.Quad(new Vector3(cx+rx,-.8f,cz+rz),new Vector3(cx+rx,-1.35f,cz+rz),new Vector3(cx-rx,-1.35f,cz+rz),new Vector3(cx-rx,-.8f,cz+rz));
            data.Quad(new Vector3(cx-rx,-.8f,cz+rz),new Vector3(cx-rx,-1.35f,cz+rz),new Vector3(cx-rx,-1.35f,cz-rz),new Vector3(cx-rx,-.8f,cz-rz));
            data.Quad(new Vector3(cx+rx,-.8f,cz-rz),new Vector3(cx+rx,-1.35f,cz-rz),new Vector3(cx+rx,-1.35f,cz+rz),new Vector3(cx+rx,-.8f,cz+rz));
            data.Quad(new Vector3(cx-rx,-1.35f,cz-rz),new Vector3(cx+rx,-1.35f,cz-rz),new Vector3(cx+rx,-1.35f,cz+rz),new Vector3(cx-rx,-1.35f,cz+rz));
            var r=LandscapeObject("Integration coastal saddle closure",data,side.GetComponent<Renderer>().sharedMaterial,parent).GetComponent<MeshRenderer>();
            r.receiveGI=ReceiveGI.LightProbes;r.lightProbeUsage=LightProbeUsage.BlendProbes;
        }
        public static void PatchCoastSaddleAndInspect()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();SealCoastSaddle();ValidateFiniteMapGeometry();
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            DiagnoseCoastSurfaceRays();CaptureWholeMapQualityAudit();
            Debug.Log("COAST_SADDLE_LOCAL_PATCH_OK");
        }

        static Material IntegrationVariant(string name,Material source,Color tint)
        {
            var m=Material("Integration_"+name,tint);m.shader=source.shader;m.CopyPropertiesFromMaterial(source);m.color=tint;
            EditorUtility.SetDirty(m);return m;
        }
        static float GroundSegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {
            p.y=a.y=b.y=0;Vector3 ab=b-a;
            return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(ab.sqrMagnitude,.01f)));
        }
        static bool IntegrationClear(Vector3 p,float radius)
        {
            for(int i=1;i<WideMech.Length;i++)if(GroundSegmentDistance(p,WideMech[i-1],WideMech[i])<20+radius)return false;
            for(int i=1;i<WideBoss.Length;i++)if(GroundSegmentDistance(p,WideBoss[i-1],WideBoss[i])<44+radius)return false;
            return true;
        }
        static void IntegrationGrid(string name,float x0,float x1,float z0,float z1,int nx,int nz,Func<float,float,float> elevation,Material mat)
        {
            var data=new LandscapeMesh();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float wx=Mathf.Lerp(x0,x1,(float)x/nx),wz=Mathf.Lerp(z0,z1,(float)z/nz),h=elevation(wx,wz);
                if(float.IsNaN(h)||float.IsInfinity(h))throw new InvalidOperationException("Non-finite landscape elevation in "+name+" at x="+wx.ToString(Inv)+", z="+wz.ToString(Inv));
                data.Add(new Vector3(wx,h,wz),new Color(.12f,Mathf.Clamp01((.25f-h)*.25f),0,1));
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;data.Tri(a,c,b);data.Tri(b,c,d);}
            LandscapeObject("Integration "+name,data,mat,environmentIntegration);
        }
        static void IntegrationRoad(string name,Vector3[] points,float width)
        {
            var surface=new LandscapeMesh();var paint=new LandscapeMesh();var shoulder=new LandscapeMesh();
            var leftEdges=new Vector3[points.Length];var rightEdges=new Vector3[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                var before=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                var side=new Vector3(-before.z,0,before.x)*width*.5f;var p=points[i];p.y=.025f;
                leftEdges[i]=p-side;rightEdges[i]=p+side;
            }
            for(int i=1;i<points.Length;i++)
            {
                // Subdivide the original two edge lines, preserving the exact
                // silhouette while avoiding kilometre-long collider triangles.
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[i-1],points[i])/80));
                int start=surface.v.Count;
                for(int step=0;step<=steps;step++)
                {
                    float t=(float)step/steps;
                    surface.Add(Vector3.Lerp(leftEdges[i-1],leftEdges[i],t),Color.black);
                    surface.Add(Vector3.Lerp(rightEdges[i-1],rightEdges[i],t),Color.black);
                    if(step>0){int q=start+(step-1)*2;surface.Tri(q,q+1,q+3);surface.Tri(q,q+3,q+2);}
                }
            }
            for(int i=1;i<points.Length;i++)
            {
                Vector3 a=points[i-1],b=points[i];a.y=b.y=.025f;
                Vector3 d=(b-a).normalized,s=new Vector3(-d.z,0,d.x)*width*.5f;
                float length=Vector3.Distance(a,b);
                for(float t=3;t<length-3;t+=10)
                {
                    var c=a+d*t+Vector3.up*.006f;
                    paint.Quad(c-d*1.6f-s*.015f,c-d*1.6f+s*.015f,c+d*1.6f+s*.015f,c+d*1.6f-s*.015f);
                }
                foreach(int sign in new[]{-1,1})
                {
                    Vector3 pa=a+s*sign,pb=b+s*sign,oa=pa+s.normalized*sign*2.5f,ob=pb+s.normalized*sign*2.5f;
                    pa.y=pb.y=.026f;oa.y=ob.y=-.185f;
                    if(sign==1)shoulder.Quad(pa,oa,ob,pb);else shoulder.Quad(pa,pb,ob,oa);
                }
            }
            LandscapeObject("Integration "+name+" pavement",surface,groundTransport,environmentIntegration,true);
            LandscapeObject("Integration "+name+" compacted side fill",shoulder,groundSlag,environmentIntegration);
            if(paint.v.Count>0)LandscapeObject("Integration "+name+" faded service centre",paint,agedYellow,environmentIntegration).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            environmentConnections.AppendLine(name+",service vehicle and material approach,"+width+","+string.Join(" | ",points.Select(p=>p.x.ToString(Inv)+"/"+p.z.ToString(Inv))));
        }
        static void LoadingBays(string name,Vector3 origin,int count,Vector2 size,bool crosswise)
        {
            var marks=new LandscapeMesh();var axis=crosswise?Vector3.forward:Vector3.right;var depth=crosswise?Vector3.right:Vector3.forward;
            for(int i=0;i<count;i++)
            {
                Vector3 p=origin+axis*(size.x+1.2f)*i;p.y=-.052f;
                foreach(float side in new[]{-.5f,.5f})
                {
                    Vector3 a=p+axis*(size.x*side),b=a+depth*size.y;
                    if(crosswise)marks.Quad(a-axis*.045f,a+axis*.045f,b+axis*.045f,b-axis*.045f);
                    else marks.Quad(b-axis*.045f,b+axis*.045f,a+axis*.045f,a-axis*.045f);
                }
                Vector3 back=p+depth*size.y;
                if(crosswise)marks.Quad(back+axis*size.x*.5f-depth*.045f,back+axis*size.x*.5f+depth*.045f,back-axis*size.x*.5f+depth*.045f,back-axis*size.x*.5f-depth*.045f);
                else marks.Quad(back-axis*size.x*.5f-depth*.045f,back-axis*size.x*.5f+depth*.045f,back+axis*size.x*.5f+depth*.045f,back+axis*size.x*.5f-depth*.045f);
            }
            var g=LandscapeObject("Integration "+name+" loading markings",marks,agedYellow,environmentIntegration);
            g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static Transform IntegrationStock(string asset,string name,Vector3 ground,float yaw,float scale=1)
        {
            if(!IntegrationClear(ground,12*scale))throw new InvalidOperationException("Peripheral cargo enters protected gameplay space: "+name);
            return Legacy(asset,"Integration "+name,ground,yaw,environmentIntegration,scale);
        }
        static void IntegrationHatching(string name,float x0,float x1,float z0,float z1,float y,float spacing=4)
        {
            var paint=new LandscapeMesh();
            paint.Top(x0,x1,z0,z0+.11f,y);paint.Top(x0,x1,z1-.11f,z1,y);
            paint.Top(x0,x0+.11f,z0,z1,y);paint.Top(x1-.11f,x1,z0,z1,y);
            for(float z=z0+spacing;z<z1-1;z+=spacing)
                paint.Quad(new Vector3(x0,y,z),new Vector3(x0,y,z+.16f),new Vector3(Mathf.Min(x0+3,x1),y,z-1.34f),new Vector3(Mathf.Min(x0+3,x1),y,z-1.5f));
            var g=LandscapeObject("Integration "+name+" restricted edge paint",paint,agedYellow,environmentIntegration);
            g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static void IntegrationBearingRing(string name,Vector3 centre,float outer,float inner,float height,float fromDegrees,float toDegrees)
        {
            if(!IntegrationClear(centre,outer))throw new InvalidOperationException("Bearing component enters protected gameplay space.");
            var data=new LandscapeMesh();int segments=Mathf.CeilToInt((toDegrees-fromDegrees)/7);
            Vector3 radial(float angle,float r,float y)=>centre+new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
            for(int i=0;i<segments;i++)
            {
                float a=Mathf.Lerp(fromDegrees,toDegrees,(float)i/segments)*Mathf.Deg2Rad,b=Mathf.Lerp(fromDegrees,toDegrees,(float)(i+1)/segments)*Mathf.Deg2Rad;
                Vector3 ot0=radial(a,outer,height*.5f),ot1=radial(b,outer,height*.5f),it0=radial(a,inner,height*.5f),it1=radial(b,inner,height*.5f);
                Vector3 ob0=radial(a,outer,-height*.5f),ob1=radial(b,outer,-height*.5f),ib0=radial(a,inner,-height*.5f),ib1=radial(b,inner,-height*.5f);
                data.Quad(ot0,it0,it1,ot1);data.Quad(ob0,ot0,ot1,ob1);data.Quad(ib1,it1,it0,ib0);
                if(i==0)data.Quad(ob0,ib0,it0,ot0);
                if(i==segments-1)data.Quad(ob1,ot1,it1,ib1);
            }
            LandscapeObject("Integration "+name,data,agedSteel,environmentIntegration,true);
        }
        static void ConnectOuterWestFacilities()
        {
            IntegrationRoad("west outer facilities service spine",new[]{new Vector3(-355,0,-28),new Vector3(-355,0,793)},10);
            IntegrationRoad("west outer south loop connection",new[]{new Vector3(-355,0,-28),new Vector3(-235,0,-28)},10);
            IntegrationRoad("west outer north loop connection",new[]{new Vector3(-355,0,793),new Vector3(-235,0,793)},10);
            foreach(Transform district in districts)
            {
                if(!district.name.StartsWith("WEST district",StringComparison.Ordinal))continue;
                var bounds=LegacyEnvironmentAudit.BoundsOf(district.gameObject);if(bounds.center.x>=-350)continue;
                float z=bounds.min.z+Mathf.Min(15,bounds.size.z*.35f);
                IntegrationRoad(district.name+" outer service approach",new[]{new Vector3(-355,0,z),new Vector3(bounds.max.x-1,0,z)},8);
            }
            DrainageRun("Integration western service spine runoff",new Vector3(-363,-.025f,-24),new Vector3(-363,-.025f,785),false);
        }
        static void MakeSoutheastReceivingYard()
        {
            // Deliberately peripheral: this explains the former blank south-east
            // sheet without adding cover or obstacles to the combat yards.
            Apron("Integration heavy component receiving work apron",112,208,-112,-28,-.055f,groundProcess,16);
            Apron("Integration receiving carrier standby apron",208,236,-112,-28,-.055f,groundMaintenance,14);
            IntegrationRoad("southeast receiving approach from coast",new[]{new Vector3(347,0,-70),new Vector3(236,0,-70),new Vector3(206,0,-70)},11);
            LoadingBays("Receiving carrier standby",new Vector3(216,0,-103),3,new Vector2(6,15),true);
            LoadingBays("Heavy bearing and beam incoming lots",new Vector3(126,0,-100),3,new Vector2(19,27),false);
            IntegrationHatching("receiving quarantine component",119,177,-104,-66,-.052f,5);
            // The interrupted annular casting is a damaged delivered part,
            // accompanied by its broken sector on a separate inspection mat.
            IntegrationBearingRing("fractured incoming bearing housing",new Vector3(139,1.16f,-85),6.2f,4.0f,2.4f,15,305);
            IntegrationBearingRing("detached bearing fracture sector",new Vector3(156,.71f,-82),6.2f,4,1.5f,316,365);
            IntegrationStock("SteelCargo/SteelBeamBundle","receiving long members",new Vector3(184,.275f,-89),90,1.5f);
            IntegrationStock("SteelCargo/SteelSlabStack","receiving inspection plinth plates",new Vector3(129,-.055f,-47),0,1.2f);
            IntegrationStock("SteelTruck/SteelTruck","receiving immobilised flatbed carrier",new Vector3(222,-.055f,-96),0);
            // Low dunnage makes the small amount of stock read as intentional
            // storage. The open central turning/work strip stays unoccupied.
            var dunnage=new LandscapeMesh();
            foreach(float z in new[]{-93f,-85f})dunnage.Block(new Vector3(184,.11f,z),new Vector3(10,.33f,.45f));
            LandscapeObject("Integration receiving cargo dunnage",dunnage,agedRoof,environmentIntegration);
            var wheelStops=new LandscapeMesh();
            for(int i=0;i<3;i++)wheelStops.Block(new Vector3(229,.095f,-103+i*7.2f),new Vector3(.36f,.3f,3.6f));
            LandscapeObject("Integration receiving parking wheel stops",wheelStops,groundMaintenance,environmentIntegration,true);
            foreach(float z in new[]{-108f,-32f})
            {
                var barrier=IntegrationStock("ContainerYardEquipment/ConcreteSafetyBarrier","receiving edge safety barrier",new Vector3(116,-.055f,z),90);
                barrier.name+=" "+z;
            }
            DrainageRun("Integration receiving eastern collection drain",new Vector3(239,-.025f,-112),new Vector3(239,-.025f,-28),true);
            environmentConnections.AppendLine("southeast receiving yard,damaged bearing inspection plus carrier standby and empty manoeuvring space,124,112/-112 | 236/-28");
        }
        static void DifferentiateHarborWorkStates()
        {
            IntegrationRoad("harbor operating berth delivery crossing",new[]{new Vector3(347,0,565),new Vector3(398,0,565)},8);
            IntegrationHatching("operating berth crane loading clearance",389,422,576,613,-.052f,5);
            LoadingBays("Operating berth dispatched steel",new Vector3(394,0,582),2,new Vector2(9,23),false);
            IntegrationStock("SteelCargo/SteelSlabStack","operating berth steel on crane landing",new Vector3(435,-.055f,590),0,1.3f);
            IntegrationStock("SteelCargo/SteelBeamBundle","operating berth prepared export bundle",new Vector3(400,-.055f,593),90,1.25f);
            IntegrationStock("SteelTruck/SteelTruck","operating berth delivery flatbed",new Vector3(407,-.055f,578),90);
            // Existing berth 2 gets a real broken outer corner, rather than
            // rubble placed over a completely intact rectangular pier.
            var oldPier=landscapeRoot.Find("Crane loading pier 2");if(oldPier!=null)Obj.DestroyImmediate(oldPier.gameObject);
            var outline=new[]{new Vector2(427,403),new Vector2(427,437),new Vector2(445,437),new Vector2(445,435),new Vector2(440,433),new Vector2(438,429),new Vector2(443,425),new Vector2(445,423),new Vector2(445,403)};
            var top=new LandscapeMesh();var cut=new LandscapeMesh();
            // Ear-clip the concave fracture outline. A centre fan would bridge
            // the missing corner and accidentally restore an intact top face.
            var remaining=Enumerable.Range(0,outline.Length).ToList();
            float cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
            bool inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>cross(a,b,p)<=.0001f&&cross(b,c,p)<=.0001f&&cross(c,a,p)<=.0001f;
            while(remaining.Count>=3)
            {
                bool clipped=false;
                for(int n=0;n<remaining.Count;n++)
                {
                    int a=remaining[(n+remaining.Count-1)%remaining.Count],b=remaining[n],c=remaining[(n+1)%remaining.Count];
                    if(cross(outline[a],outline[b],outline[c])>=-.0001f)continue;
                    if(remaining.Any(k=>k!=a&&k!=b&&k!=c&&inside(outline[k],outline[a],outline[b],outline[c])))continue;
                    int q=top.v.Count;foreach(int k in new[]{a,b,c})top.Add(new Vector3(outline[k].x,-.055f,outline[k].y),Color.black);
                    top.Tri(q,q+1,q+2);remaining.RemoveAt(n);clipped=true;break;
                }
                if(!clipped)throw new InvalidOperationException("Damaged berth fracture outline could not be triangulated.");
            }
            for(int i=0;i<outline.Length;i++)
            {
                Vector2 a=outline[i],b=outline[(i+1)%outline.Length];Vector3 pa=new Vector3(a.x,-.055f,a.y),pb=new Vector3(b.x,-.055f,b.y);
                cut.Quad(pa,pa+Vector3.down*.7f,pb+Vector3.down*.7f,pb);
            }
            LandscapeObject("Integration damaged berth cut-corner deck",top,groundHarbor,environmentIntegration,true);
            LandscapeObject("Integration damaged berth exposed slab section",cut,integrationRetaining,environmentIntegration,true);
            IntegrationHatching("damaged berth exclusion strip",401,427,402,437,-.052f,3.5f);
            var railing=new LandscapeMesh();
            foreach(float z in new[]{405f,410f,415f,420f})railing.Block(new Vector3(444.7f,.6f,z),new Vector3(.12f,1.3f,.12f));
            railing.Block(new Vector3(444.7f,1.21f,412.5f),new Vector3(.12f,.12f,15.2f));
            railing.Block(new Vector3(444.7f,.57f,412.5f),new Vector3(.1f,.1f,15.2f));
            LandscapeObject("Integration damaged berth surviving guard",railing,agedYellow,environmentIntegration);
            Beam("Integration bent rail follows damaged edge",new Vector3(444.7f,1.21f,420),new Vector3(446.5f,.18f,423),.12f,agedYellow,environmentIntegration);
            Beam("Integration bent rail lower connected end",new Vector3(444.7f,.57f,420),new Vector3(446.1f,-.22f,423),.1f,agedYellow,environmentIntegration);
            for(int i=0;i<4;i++)Beam("Integration exposed reinforcing bar",new Vector3(439.1f+i*.55f,-.22f,431.5f),new Vector3(441.4f+i*.4f,-.32f,432.6f),.045f,agedRust,environmentIntegration);
            IntegrationStock("SteelCargo/SteelBeamBundle","damaged berth fallen steel load",new Vector3(435,-.055f,419),26,1.05f);
            foreach(var ends in new[]{new[]{new Vector3(434.7f,.18f,425),new Vector3(443.6f,-.48f,429)},new[]{new Vector3(435.3f,.12f,423),new Vector3(446.1f,-.67f,425.5f)}})
                Beam("Integration dropped member bridges the broken deck",ends[0],ends[1],.32f,agedRust,environmentIntegration);
            foreach(float z in new[]{406f,414f,422f,430f})
                IntegrationStock("ContainerYardEquipment/ConcreteSafetyBarrier","damaged berth access closure "+z,new Vector3(402,-.055f,z),90);
            environmentConnections.AppendLine("harbor berth 3,operational loading and vehicle approach,0,389/576 | 445/613");
            environmentConnections.AppendLine("harbor berth 2,isolated impact damage with broken deck guard and displaced load,0,401/402 | 447/437");
        }
        static void ConnectIndustrialApproaches()
        {
            IntegrationRoad("B01 rear workshop access",new[]{new Vector3(-235,0,-76),new Vector3(-63,0,-76),new Vector3(-41,0,-63)},9);
            IntegrationRoad("B02 western repair delivery",new[]{new Vector3(-235,0,44),new Vector3(-131,0,44)},10);
            IntegrationRoad("B03 north pump service spur",new[]{new Vector3(112,0,90),new Vector3(112,0,72)},8);
            IntegrationRoad("B04 casting entry",new[]{new Vector3(108,0,90),new Vector3(108,0,112)},9);
            IntegrationRoad("B05 mill incoming stock",new[]{new Vector3(-235,0,210),new Vector3(-198,0,210)},14);
            IntegrationRoad("B06 pressure service entry",new[]{new Vector3(-235,0,341),new Vector3(-176,0,341)},8);
            IntegrationRoad("B09 process maintenance entry",new[]{new Vector3(-235,0,555),new Vector3(-143,0,555)},10);
            IntegrationRoad("B08 furnace supply from coast",new[]{new Vector3(347,0,745),new Vector3(160,0,745)},12);
            IntegrationRoad("B08 south charging access",new[]{new Vector3(105,0,630),new Vector3(105,0,660)},12);
            // Background facilities use their service side, without adding anything in the combat reservation.
            foreach(Transform district in districts)
            {
                if(!district.name.StartsWith("WEST district",StringComparison.Ordinal))continue;
                var bounds=LegacyEnvironmentAudit.BoundsOf(district.gameObject);
                if(bounds.center.x<-350)continue;
                float z=bounds.min.z+Mathf.Min(15,bounds.size.z*.35f);
                IntegrationRoad(district.name+" loading apron tie",new[]{new Vector3(-235,0,z),new Vector3(bounds.max.x-1,0,z)},8);
            }
            Apron("Integration B02 incoming parts laydown",-154,-132,17,40,-.055f,groundMaintenance,11);
            Apron("Integration mill finished stock laydown",-221,-201,247,275,-.055f,groundProcess,10);
            LoadingBays("Repair incoming parts",new Vector3(-143,0,22),3,new Vector2(4,9),true);
            LoadingBays("Rolling mill steel dispatch",new Vector3(-215,0,252),3,new Vector2(4.5f,11),true);
            LoadingBays("Harbor north operational cargo",new Vector3(389,0,622),3,new Vector2(7,18),false);
            LoadingBays("Harbor south operational cargo",new Vector3(389,0,130),3,new Vector2(7,18),false);
            // Only selected loading bays contain stock, so operational / damaged / unused strips remain legible.
            Legacy("SteelCargo/SteelBeamBundle","Integration dispatch stock awaiting mill pickup",new Vector3(-207,0,258),90,environmentIntegration);
            Legacy("SteelCargo/SteelSlabStack","Integration harbor landed slab load",new Vector3(400,0,632),0,environmentIntegration);
            Legacy("SteelCargo/SteelBeamBundle","Integration harbor incoming beam load",new Vector3(401,0,138),0,environmentIntegration);
            var repairs=new LandscapeMesh();repairs.Top(-224,-214,36,52,.038f);repairs.Top(108,116,74,86,.04f);repairs.Top(307,329,739,751,.04f);
            LandscapeObject("Integration repaired heavy vehicle thresholds",repairs,integrationRepair,environmentIntegration);
            DrainageRun("Integration B02 delivery outlet",new Vector3(-139,.03f,8),new Vector3(-139,.03f,35),true);
            DrainageRun("Integration mill apron outlet",new Vector3(-204,.03f,228),new Vector3(-204,.03f,292),true);
            ConnectOuterWestFacilities();MakeSoutheastReceivingYard();DifferentiateHarborWorkStates();
        }
        static void UtilityTrestle(Vector3 p,float width=5,bool alongX=false)
        {
            if(!IntegrationClear(p,width*.5f))throw new InvalidOperationException("Utility support would enter a protected route: "+p);
            var mesh=new LandscapeMesh();
            Vector3 axis=alongX?Vector3.forward:Vector3.right;
            foreach(int side in new[]{-1,1})
            {mesh.Block(p+axis*(side*width*.5f)+Vector3.up*3.05f,new Vector3(.3f,6.1f,.35f));mesh.Block(p+axis*(side*width*.5f)+Vector3.up*.18f,new Vector3(1.1f,.36f,1.1f));}
            mesh.Block(p+new Vector3(0,6.2f,0),alongX?new Vector3(.5f,.3f,width+.7f):new Vector3(width+.7f,.3f,.5f));
            for(int i=0;i<3;i++){float h=.15f+i*.8f;mesh.Block(p+axis*((i-1)*1.3f)+Vector3.up*(6.35f+h*.5f),new Vector3(.18f,h,.22f));}
            LandscapeObject("Integration utility trestle "+integrationSupportCount++,mesh,structuralSteel,environmentIntegration,true);
        }
        static void ConnectPeripheralProcess()
        {
            for(int i=0;i<3;i++)
            {
                float offset=(i-1)*1.3f,y=6.8f+i*.8f;
                ProcessPipe("Integration western process return "+i,new[]{new Vector3(-210+offset,y,-55),new Vector3(-210+offset,y,798+offset),new Vector3(-207+offset,y,801+offset),new Vector3(242+offset,y,801+offset),new Vector3(245+offset,y,798+offset),new Vector3(245+offset,y,522)},i==0?.46f:.30f,environmentIntegration);
                ProcessPipe("Integration eastern process supply "+i,new[]{new Vector3(245+offset,y,43),new Vector3(245+offset,y,430)},i==0?.46f:.30f,environmentIntegration);
            }
            for(float z=-40;z<790;z+=42)
            {
                if(Mathf.Abs(z-44)<12||Mathf.Abs(z-210)<14||Mathf.Abs(z-341)<12||Mathf.Abs(z-555)<12||(z>245&&z<279))continue;
                UtilityTrestle(new Vector3(-210,0,z));
            }
            foreach(float z in new[]{53f,110f,170f,230f,288f,345f,405f,538f,597f,655f,712f,774f})UtilityTrestle(new Vector3(245,0,z));
            for(float x=-177;x<240;x+=58)UtilityTrestle(new Vector3(x,0,801),5,true);
            var branches=new[]{
                new[]{new Vector3(-210,7,65),new Vector3(-122,7,65),new Vector3(-120,7,62)},
                new[]{new Vector3(-210,7,285),new Vector3(-192,7,285)},
                new[]{new Vector3(-210,7,335),new Vector3(-162,7,335)},
                new[]{new Vector3(-210,7,555),new Vector3(-137,7,555)},
                new[]{new Vector3(245,7,45),new Vector3(107,7,45)},
                new[]{new Vector3(245,7,145),new Vector3(124,7,145)},
                new[]{new Vector3(245,7,740),new Vector3(161,7,740)}};
            for(int i=0;i<branches.Length;i++)
            {
                ProcessPipe("Integration plant utility branch "+i,branches[i],.34f,environmentIntegration);
                Vector3 start=branches[i][0],end=branches[i][1],d=(end-start).normalized;
                for(float distance=20;distance<Vector3.Distance(start,end)-6;distance+=22)
                {
                    Vector3 p=start+d*distance;p.y=0;if(!IntegrationClear(p,1))throw new InvalidOperationException("Branch support enters reserved gameplay space.");
                    var support=new LandscapeMesh();support.Block(p+Vector3.up*3.15f,new Vector3(.3f,6.3f,.35f));support.Block(p+Vector3.up*6.47f,new Vector3(.7f,.3f,1.7f));
                    LandscapeObject("Integration branch saddle "+i+" "+distance,support,structuralSteel,environmentIntegration,true);
                }
                environmentConnections.AppendLine("process branch "+i+",supported round process pipe,0.68,"+string.Join(" | ",branches[i].Select(p=>p.x.ToString(Inv)+"/"+p.z.ToString(Inv))));
            }
            // A visible isolation end on either side of the existing damaged bridge explains the supply interruption.
            foreach(float z in new[]{430f,522f})for(int i=0;i<3;i++)
            {
                var cap=Shape("Integration isolated process pipe end",PrimitiveType.Cylinder,new Vector3(245+(i-1)*1.3f,6.8f+i*.8f,z),new Vector3(.9f,.075f,.9f),agedRust,environmentIntegration,false);
                cap.transform.rotation=Quaternion.Euler(90,0,0);
            }
        }

        static void CalmGroundAndLayerMountains()
        {
            foreach(var m in new[]{landscapeRock,landscapeGravel,landscapeWet,landscapeDistant,groundMaintenance,groundProcess,groundHarbor,groundTransport,groundSlag,groundTidal})
            {
                if(m==null)continue;m.SetFloat("_MacroContrast",m==landscapeDistant?.11f:.035f);
                if(m.GetFloat("_GroundSurface")>.5f)m.SetFloat("_SurfaceMix",Mathf.Min(.1f,m.GetFloat("_SurfaceMix")));
                EditorUtility.SetDirty(m);
            }
            groundSlag.SetFloat("_Wetness",.035f);groundSlag.SetFloat("_SurfaceMix",.065f);
            // Move the tall formations back. A separate low foothill then occupies the gap,
            // so three ranges differ in silhouette and depth, rather than only colour.
            var west=nature.Find("Geological ridge western cut slopes");if(west!=null)west.position+=new Vector3(-180,0,0);
            var north=nature.Find("Geological ridge northern eroded foothills");if(north!=null)north.position+=new Vector3(0,0,120);
            var nearMat=IntegrationVariant("near fractured bedrock",landscapeDistant,new Color(.58f,.60f,.56f));
            var middleMat=IntegrationVariant("middle muted massif",landscapeDistant,new Color(.69f,.73f,.72f));
            var farMat=IntegrationVariant("far atmospheric massif",landscapeDistant,new Color(.79f,.82f,.83f));
            foreach(Transform t in nature)
                if(t.name.StartsWith("Geological ridge",StringComparison.Ordinal))
                    t.GetComponent<MeshRenderer>().sharedMaterial=t.name.Contains("far storm")?farMat:t.name.Contains("middle overlapping")?middleMat:nearMat;
            foreach(string id in new[]{"Western reclaimed land cut and fill","Northern graded industrial boundary"})
            {var old=landscapeRoot.Find(id);if(old!=null)Obj.DestroyImmediate(old.gameObject);}
            IntegrationGrid("western low quarried foothill",-735,-496,-235,920,46,125,(x,z)=>
            {
                float u=Mathf.Clamp01((-x-500)/235),end=Mathf.SmoothStep(0,1,Mathf.Min((z+235)/80,(920-z)/80));
                float crest=20+14*Mathf.PerlinNoise(z*.006f,37.6f);
                float low=2.5f+crest*Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.Clamp01(u)*Mathf.PI)),1.6f)*end;
                float shoulder=Mathf.SmoothStep(0,1,Mathf.Clamp01((-x-500)/9));
                // This is a separate low landform, not a second sampled copy of
                // the tall ridge. Its rear toe lies well below the existing
                // massif, so the overlap is buried rather than near-coplanar.
                return Mathf.Lerp(-.18f,low,shoulder);
            },integrationSlope);
            IntegrationGrid("northern low spoil and eroded foothill",-560,660,895,1140,132,44,(x,z)=>
            {
                float u=Mathf.Clamp01((z-900)/240),coast=445+Mathf.Max(0,z-900)*.38f;
                float crest=16+12*Mathf.PerlinNoise(x*.007f,17.4f);
                float low=2.4f+crest*Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),1.5f);
                float y=Mathf.Lerp(-.18f,low,Mathf.SmoothStep(0,1,(z-900)/10));
                return Mathf.Lerp(-5,y,Mathf.SmoothStep(0,1,(coast-x)/45));
            },integrationSlope);
            // The retaining toe is low industrial civil engineering, not a new enclosing wall.
            var retain=new LandscapeMesh();
            for(float z=-160;z<850;z+=22)
            {
                retain.Block(new Vector3(-500,1.05f,z+10.8f),new Vector3(.85f,2.5f,21.6f));
                retain.Block(new Vector3(-500,2.36f,z+10.8f),new Vector3(1.1f,.18f,21.6f));
            }
            for(float x=-475;x<415;x+=26)
            {retain.Block(new Vector3(x+12.8f,1.05f,900),new Vector3(25.6f,2.5f,.85f));retain.Block(new Vector3(x+12.8f,2.36f,900),new Vector3(25.6f,.18f,1.1f));}
            LandscapeObject("Integration retained cut slope toe",retain,integrationRetaining,environmentIntegration,true);
            DrainageRun("Integration west cut slope interception",new Vector3(-497,-.02f,-150),new Vector3(-497,-.02f,845),false);
            DrainageRun("Integration north cut slope interception",new Vector3(-475,-.02f,897),new Vector3(415,-.02f,897),false);
        }

        struct IntegrationEdge { public int a,b,count; }
        static void AddIntegrationEdge(Dictionary<ulong,IntegrationEdge> edges,int a,int b)
        {
            if(a==b)return;ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
            if(edges.TryGetValue(key,out var edge)){edge.count++;edges[key]=edge;}
            else edges.Add(key,new IntegrationEdge{a=a,b=b,count=1});
        }
        static Vector3 CoastOutward(Vector3 p,Vector3 centre)
        {var d=p-centre;d.y=0;return d.sqrMagnitude>.001f?d.normalized:Vector3.right;}
        static void CoastShoulderProfile(Vector3 top,Vector3 outward,out Vector3 shoulder,out Vector3 tidal,out Vector3 toe)
        {
            // The earlier 1.1m drop made a sheer cookie-cutter wall. A broad,
            // uneven wave-cut shoulder remains visible from the inland drone
            // camera and then descends through the waterline into solid rock.
            float grain=Mathf.PerlinNoise(top.x*.073f+13.7f,top.z*.073f+5.3f);
            float erosion=Mathf.PerlinNoise(top.x*.21f+3.1f,top.z*.21f+19.8f);
            float width=4.2f+3.8f*grain;
            shoulder=top+outward*(width*.55f);shoulder.y=top.y-Mathf.Lerp(.06f,.19f,erosion);
            tidal=top+outward*width;tidal.y=-.82f-.22f*grain;
            toe=top+outward*(width+4.4f+1.8f*erosion);toe.y=-5.2f;
        }
        static Vector3 CoastWaterCrossing(Vector3 top,Vector3 shoulder,Vector3 tidal)
        {
            const float waterline=-.585f;
            return shoulder.y>waterline?Vector3.Lerp(shoulder,tidal,Mathf.InverseLerp(shoulder.y,tidal.y,waterline)):
                Vector3.Lerp(top,shoulder,Mathf.InverseLerp(top.y,shoulder.y,waterline));
        }
        static void CoastUpTriangle(LandscapeMesh data,int a,int b,int c)
        {
            // All generated shoulder/tidal faces are height surfaces. Radial
            // normals are unreliable at a concave scan outline, and checking
            // a whole quad can miss a differently oriented second triangle.
            if(Vector3.Cross(data.v[b]-data.v[a],data.v[c]-data.v[a]).y<0)data.Tri(a,c,b);
            else data.Tri(a,b,c);
        }
        static void CoastQuad(LandscapeMesh data,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int start=data.v.Count;
            foreach(var p in new[]{a,b,c,d})data.Add(p,new Color(.7f,Mathf.Clamp01((.55f-p.y)*.65f),0,1));
            CoastUpTriangle(data,start,start+1,start+2);CoastUpTriangle(data,start,start+2,start+3);
        }
        static Renderer CloseCoastScanBoundary(MeshRenderer renderer,string id,LandscapeMesh foam)
        {
            var filter=renderer.GetComponent<MeshFilter>();var source=filter.sharedMesh;
            var canonical=new List<Vector3>();var weld=new Dictionary<Vector3Int,int>();var remap=new int[source.vertexCount];var sourceVertices=source.vertices;
            // Weld by position so UV seams are not mistaken for open shoreline boundaries.
            for(int i=0;i<sourceVertices.Length;i++)
            {
                Vector3 world=filter.transform.TransformPoint(sourceVertices[i]);
                var q=new Vector3Int(Mathf.RoundToInt(world.x*1000),Mathf.RoundToInt(world.y*1000),Mathf.RoundToInt(world.z*1000));
                if(!weld.TryGetValue(q,out int index)){index=canonical.Count;weld.Add(q,index);canonical.Add(world);}remap[i]=index;
            }
            var edges=new Dictionary<ulong,IntegrationEdge>();var triangles=source.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {int a=remap[triangles[i]],b=remap[triangles[i+1]],c=remap[triangles[i+2]];AddIntegrationEdge(edges,a,b);AddIntegrationEdge(edges,b,c);AddIntegrationEdge(edges,c,a);}
            var sides=new LandscapeMesh();Vector3 centre=renderer.bounds.center;
            foreach(var edge in edges.Values)
            {
                if(edge.count!=1)continue;
                Vector3 a=canonical[edge.a],b=canonical[edge.b];if(Vector3.Distance(a,b)<.001f)continue;
                Vector3 da=CoastOutward(a,centre),db=CoastOutward(b,centre);
                CoastShoulderProfile(a,da,out var sa,out var ma,out var ba);
                CoastShoulderProfile(b,db,out var sb,out var mb,out var bb);
                CoastQuad(sides,a,b,sb,sa);
                CoastQuad(sides,sa,sb,mb,ma);
                CoastQuad(sides,ma,mb,bb,ba);
                integrationCoastEdges++;
                // A small water-coloured ribbon follows the real submerged scan perimeter.
                // It uses the same ocean material, so the zero-weight edge blends exactly into water.
                if(foam!=null&&a.y>-.58f&&b.y>-.58f&&(a.x+b.x)*.5f>431)
                {
                    Vector3 fa=CoastWaterCrossing(a,sa,ma);
                    Vector3 fb=CoastWaterCrossing(b,sb,mb);
                    fa.y=fb.y=-.583f;int first=foam.v.Count;
                    foreach(var pair in new[]{(fa,da),(fb,db)})
                    {
                        foam.Add(pair.Item1,Color.black);foam.Add(pair.Item1+pair.Item2*.65f,new Color(.40f,0,0,1));
                        foam.Add(pair.Item1+pair.Item2*1.65f,Color.black);
                    }
                    for(int j=0;j<2;j++)
                    {
                        int q=first+j;
                        CoastUpTriangle(foam,q,q+1,q+3);CoastUpTriangle(foam,q+1,q+4,q+3);
                    }
                }
            }
            if(sides.v.Count==0)return null;
            return LandscapeObject("Integration coastal rock volume "+id,sides,integrationTidal,environmentIntegration).GetComponent<Renderer>();
        }
        static void TieScansIntoCoast()
        {
            var acquired=GameObject.Find("15 CC0 scanned and authored equipment");if(acquired==null)return;
            var scans=acquired.transform.Cast<Transform>().Where(t=>t.name.Contains("coast_rocks_01")).ToArray();
            var foam=new LandscapeMesh();int index=0;
            foreach(var scan in scans)
            {
                // Retain the authored shoreline silhouette and review-camera
                // coverage. The new submerged side volume supplies thickness;
                // shifting the scan inland would erase the foreground instead.
                Vector3 old=scan.position;var scanBounds=LegacyEnvironmentAudit.BoundsOf(scan.gameObject);
                // The complete scan base must sit above the sea (-0.6m).
                // Previously its cut boundary was submerged, hiding every new
                // side face and letting water cut holes across the scanned top.
                scan.position=new Vector3(old.x,old.y+(-.5f-scanBounds.min.y),old.z);
                var lodGroup=scan.GetComponent<LODGroup>();
                if(lodGroup!=null)
                {
                    var lods=lodGroup.GetLODs();
                    for(int level=0;level<lods.Length;level++)
                    {
                        var additions=new List<Renderer>();int part=0;
                        foreach(var r in lods[level].renderers.OfType<MeshRenderer>())
                        {
                            var volume=CloseCoastScanBoundary(r,index+" LOD"+level+" part"+part++,level==0?foam:null);
                            if(volume!=null){volume.transform.SetParent(scan,true);additions.Add(volume);}
                        }
                        lods[level].renderers=lods[level].renderers.Concat(additions).ToArray();
                    }
                    lodGroup.SetLODs(lods);lodGroup.RecalculateBounds();
                }
                else
                {
                    int part=0;foreach(var r in scan.GetComponentsInChildren<MeshRenderer>())CloseCoastScanBoundary(r,index+" part"+part++,foam);
                }
                environmentConnections.AppendLine(scan.name+",closed rock volume joined to coast,0,"+old.x+"/"+old.z+" -> "+scan.position.x+"/"+scan.position.z);index++;
            }
            foreach(string oldName in new[]{"South reclaimed shore rock toe","North coastal geology transition"})
            {var old=landscapeRoot.Find(oldName);if(old!=null)Obj.DestroyImmediate(old.gameObject);}
            foreach(var band in new[]{new Vector2(-240,-48),new Vector2(755,925)})
                IntegrationGrid("shore attached rock embankment "+band.x,401,465,band.x,band.y,24,45,(x,z)=>
                {
                    float edge=430+3*Mathf.PerlinNoise(z*.043f,5.8f);
                    float u=Mathf.Clamp01((x-edge+9)/39);
                    float h=Mathf.Lerp(-.12f,-5.7f,Mathf.Pow(u,1.13f));
                    return h+Mathf.Sin(u*Mathf.PI)*(Mathf.PerlinNoise(x*.13f,z*.11f)-.5f)*.5f;
                },integrationTidal);
            var ocean=nature.Find("open sea to horizon");
            if(ocean!=null)
            {
                var filter=ocean.GetComponent<MeshFilter>();var mesh=Obj.Instantiate(filter.sharedMesh);mesh.name="Integration sea contact colour base";
                mesh.colors=Enumerable.Repeat(Color.black,mesh.vertexCount).ToArray();
                filter.sharedMesh=SaveDetailMesh(mesh,"Integration_SeaContactBase");
                var material=ocean.GetComponent<Renderer>().sharedMaterial;material.SetFloat("_UseContactColour",1);EditorUtility.SetDirty(material);
                if(foam.v.Count>0)
                {
                    var r=LandscapeObject("Integration curved shoreline contact wash",foam,material,environmentIntegration).GetComponent<MeshRenderer>();
                    r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                }
            }
        }
        static void CompleteLandscapeIntegration()
        {
            var previous=GameObject.Find("18 Connected landscape and steelworks");if(previous!=null)Obj.DestroyImmediate(previous);
            environmentIntegration=Group("18 Connected landscape and steelworks");integrationCoastEdges=integrationSupportCount=0;
            environmentConnections.Clear();environmentConnections.AppendLine("connection,purpose,width_m,points_x_z");
            integrationSlope=IntegrationVariant("eroded ash soil foothills",landscapeGravel,new Color(.62f,.60f,.52f));
            integrationSlope.SetFloat("_RockBias",.08f);integrationSlope.SetFloat("_RockMetres",11);integrationSlope.SetFloat("_GravelMetres",2.3f);integrationSlope.SetFloat("_MacroContrast",.07f);
            integrationRetaining=IntegrationVariant("cut slope retaining concrete",groundProcess,new Color(.67f,.70f,.68f));
            integrationRepair=IntegrationVariant("dense threshold asphalt repair",groundTransport,new Color(.47f,.49f,.50f));
            integrationTidal=IntegrationVariant("shore rock wet cut faces",landscapeRock,new Color(.65f,.64f,.55f));
            integrationTidal.SetFloat("_Wetness",.18f);integrationTidal.SetFloat("_ShoreWeathering",1);integrationTidal.SetFloat("_RockMetres",4.5f);integrationTidal.SetFloat("_MacroContrast",.04f);
            CalmGroundAndLayerMountains();ConnectIndustrialApproaches();ConnectPeripheralProcess();TieScansIntoCoast();SealCoastSaddle();
            AddApronContactSurfaces();Physics.SyncTransforms();
            File.WriteAllText(WideOutput+"/environment-connections.csv",environmentConnections.ToString());
            var meshes=environmentIntegration.GetComponentsInChildren<MeshFilter>().Concat(
                Obj.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.name.StartsWith("Integration coastal rock volume",StringComparison.Ordinal))).Distinct().ToArray();
            File.WriteAllText(WideOutput+"/environment-integration-audit.txt",
                "Connections are peripheral service approaches and supported process pipes; the 650m mech route, 32m connectors and 80m boss reservation are unchanged.\n"+
                "Support placement checks: distance from mech centreline >=20m plus support radius; boss centreline >=44m plus support radius.\n"+
                "Tall west ridge moved 180m west, front north ridge 120m north; low 16-34m foothills, 2.5m retaining toe and intercept drainage fill the transition.\n"+
                "Closed welded coast boundary edges across all LODs: "+integrationCoastEdges+"\n"+
                "New integration meshes: "+meshes.Length+"; triangles: "+meshes.Sum(f=>f.sharedMesh.triangles.Length/3)+"\n"+
                "Coast scan source FBXs are preserved; new volume meshes follow source boundary vertices and extend to y=-5.2m.\n"+
                "Scan minimum height is -0.5m, 0.1m above the ocean; added side faces meet the actual waterline. All generated terrain heights are checked for finite values.\n"+
                "Coastal scan edges include a 4.2-8m irregular exposed shoulder before the tidal and submerged slopes; contact wash follows the resulting water intersection.\n"+
                "Visual review targets: 00/01/03/09/12/14/15/34/36; actual dynamic traversal and HMD performance remain unmeasured.\n");
        }
    }
}
