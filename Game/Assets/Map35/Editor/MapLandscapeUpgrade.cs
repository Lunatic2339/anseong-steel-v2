using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // The industrial site remains at y=0 and the ocean at y=-.6. This pass
        // connects those datums; it never relocates a route, target, or building.
        static Material landscapeRock,landscapeDistant,landscapeGravel,landscapeWet,landscapeJoint,landscapePuddle;
        static Material groundMaintenance,groundProcess,groundHarbor,groundTransport,groundSlag,groundTidal;
        static readonly Dictionary<string,Texture2D> groundTextureCache=new Dictionary<string,Texture2D>();
        static Transform landscapeRoot;
        static int landscapeMeshCount,landscapeVertices,landscapeTriangles;

        sealed class LandscapeMesh
        {
            public readonly List<Vector3> v=new List<Vector3>();
            public readonly List<int> t=new List<int>();
            public readonly List<Color> c=new List<Color>();
            public int Add(Vector3 point,Color color){int id=v.Count;v.Add(point);c.Add(color);return id;}
            public void Tri(int a,int b,int d){t.Add(a);t.Add(b);t.Add(d);}
            public void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e)
            {int i=v.Count;Add(a,Color.black);Add(b,Color.black);Add(d,Color.black);Add(e,Color.black);Tri(i,i+1,i+2);Tri(i,i+2,i+3);}
            public void Top(float x0,float x1,float z0,float z1,float y)
            {Quad(new Vector3(x0,y,z0),new Vector3(x0,y,z1),new Vector3(x1,y,z1),new Vector3(x1,y,z0));}
            public void Block(Vector3 p,Vector3 size)
            {
                Vector3 a=p-size*.5f,b=p+size*.5f;
                Top(a.x,b.x,a.z,b.z,b.y);
                Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z));
                Quad(new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,b.z));
                Quad(new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),new Vector3(a.x,a.y,a.z));
                Quad(new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,a.y,b.z));
            }
        }

        static GameObject LandscapeObject(string name,LandscapeMesh data,Material material,Transform parent,bool collision=false)
        {
            var mesh=new Mesh{name="Landscape_"+name,indexFormat=data.v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(data.v);mesh.SetTriangles(data.t,0);mesh.SetColors(data.c);
            mesh.SetUVs(0,data.v.Select(p=>new Vector2(p.x,p.z)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();
            var normals=mesh.normals;var tangents=new Vector4[normals.Length];
            for(int i=0;i<normals.Length;i++)
            {
                var tangent=Vector3.Cross(Vector3.forward,normals[i]);
                if(tangent.sqrMagnitude<.01f)tangent=Vector3.Cross(Vector3.up,normals[i]);
                tangent.Normalize();tangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,-1);
            }
            mesh.tangents=tangents;
            Directory.CreateDirectory(Base+"Meshes/Landscape");string path=Base+"Meshes/Landscape/"+name.Replace(' ','_')+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
            else{EditorUtility.CopySerialized(mesh,saved);Obj.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=saved;
            var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            if(collision)g.AddComponent<MeshCollider>().sharedMesh=saved;
            landscapeMeshCount++;landscapeVertices+=saved.vertexCount;landscapeTriangles+=data.t.Count/3;return g;
        }

        static Material GeologicalMaterial(string id,Color tint,float rock,float wet)
        {
            var m=Material("Landscape_"+id,tint);m.shader=Shader.Find("Anseong/GeologicalGround");
            if(m.shader==null)throw new InvalidOperationException("GeologicalGround shader is missing.");
            m.SetTexture("_RockTex",PbrChannel("Rock034","Color"));m.SetTexture("_RockNormal",PbrChannel("Rock034","NormalGL"));
            m.SetTexture("_GravelTex",PbrChannel("Ground014","Color"));m.SetTexture("_GravelNormal",PbrChannel("Ground014","NormalGL"));
            m.SetTexture("_RockRoughness",PbrChannel("Rock034","Roughness"));m.SetTexture("_GravelRoughness",PbrChannel("Ground014","Roughness"));
            m.SetFloat("_RockMetres",7);m.SetFloat("_GravelMetres",1.7f);m.SetFloat("_RockBias",rock);m.SetFloat("_Wetness",wet);
            m.SetFloat("_NormalStrength",.55f);m.SetFloat("_DistantTerrain",0);m.SetFloat("_GroundSurface",0);
            m.SetFloat("_RoughnessScale",1);m.SetFloat("_AlbedoGain",1);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }

        static Texture2D GroundChannel(string id,string channel)
        {
            string key=id+"/"+channel;
            if(groundTextureCache.TryGetValue(key,out var texture))return texture;
            texture=PbrChannel(id,channel);
            if(texture==null)throw new InvalidOperationException("Required industrial ground channel missing: "+key);
            groundTextureCache.Add(key,texture);return texture;
        }
        static Material IndustrialGroundMaterial(string name,string primary,string secondary,float metres,float secondaryMetres,
            Color tint,float wet,float roughness,float normal,float mix,Vector3 phase)
        {
            var m=Material("Landscape_surface_"+name,tint);m.shader=Shader.Find("Anseong/GeologicalGround");
            foreach(var pair in new[]{("_GravelTex",primary,"Color"),("_GravelNormal",primary,"NormalGL"),("_GravelRoughness",primary,"Roughness"),
                                      ("_RockTex",secondary,"Color"),("_RockNormal",secondary,"NormalGL"),("_RockRoughness",secondary,"Roughness")})
                m.SetTexture(pair.Item1,GroundChannel(pair.Item2,pair.Item3));
            m.SetFloat("_GravelMetres",metres);m.SetFloat("_RockMetres",secondaryMetres);
            m.SetFloat("_GroundSurface",1);m.SetFloat("_SurfaceMix",mix);m.SetFloat("_RoughnessScale",roughness);m.SetFloat("_AlbedoGain",1);
            m.SetFloat("_DistantTerrain",0);m.SetFloat("_RockBias",0);m.SetFloat("_Wetness",wet);m.SetFloat("_NormalStrength",normal);
            m.SetVector("_SurfacePhase",new Vector4(phase.x,phase.y,phase.z,0));m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void IndustrialGroundPalette()
        {
            groundTextureCache.Clear();
            // These are new source surfaces, not recoloured copies of the shared building concrete.
            // Tile sizes follow the approximate dimensions declared on the ambientCG source pages.
            groundMaintenance=IndustrialGroundMaterial("maintenance washed concrete","Concrete040","Concrete030",1.4f,3.5f,
                new Color(.91f,.93f,.95f),.08f,1.05f,.35f,.22f,new Vector3(7.13f,3.17f,12.71f));
            groundProcess=IndustrialGroundMaterial("process aggregate concrete","Concrete040","Gravel024",1.4f,1.5f,
                new Color(.78f,.8f,.81f),.15f,1.07f,.42f,.12f,new Vector3(29.17f,8.31f,17.53f));
            groundHarbor=IndustrialGroundMaterial("harbor damp salt concrete","Concrete040","Concrete030",1.4f,3.5f,
                new Color(.75f,.83f,.88f),.48f,.85f,.3f,.28f,new Vector3(41.73f,13.41f,31.19f));
            groundTransport=IndustrialGroundMaterial("weathered aggregate asphalt","Asphalt030","Road015C",2.2f,4,
                new Color(.62f,.64f,.66f),.12f,1,.4f,.24f,new Vector3(61.37f,27.71f,11.23f));
            groundSlag=IndustrialGroundMaterial("dark compacted slag gravel","Gravel024","Ground014",1.5f,1.7f,
                new Color(.98f,.94f,.88f),.1f,1.08f,.48f,.4f,new Vector3(83.61f,31.73f,41.47f));
            groundTidal=IndustrialGroundMaterial("tidal wall soaked concrete","Concrete040","Rock034",1.4f,7,
                new Color(.65f,.73f,.74f),.65f,.8f,.28f,.12f,new Vector3(107.13f,19.37f,53.81f));
        }

        static float TerrainNoise(float x,float z,int seed)
        {
            return Mathf.PerlinNoise(x*.0034f+seed*13.17f,z*.0034f+seed*5.97f)*.57f+
                   Mathf.PerlinNoise(x*.0103f+seed*4.31f,z*.0103f+seed*9.13f)*.28f+
                   Mathf.PerlinNoise(x*.028f+seed*3.17f,z*.028f+seed*11.91f)*.15f;
        }
        static float RidgeHeight(float x,float z,float x0,float x1,float z0,float z1,float height,int seed,bool west)
        {
            float along=west?z:x,cross=west?x:z;
            float lo=west?x0:z0,hi=west?x1:z1;
            float warp=(Mathf.PerlinNoise(along*.0019f+seed,2.37f)-.5f)*.28f;
            float u=(cross-lo)/(hi-lo),ridge=.48f+warp;
            float section=u<ridge?u/ridge:(1-u)/(1-ridge);
            section=Mathf.Max(0,section);
            // Irregular peaks and diagonal erosional spurs replace spherical mounds.
            float peaks=.42f+.58f*Mathf.PerlinNoise(along*.0041f+seed*8,3.6f);
            float fineRidge=1-Mathf.Abs(TerrainNoise(x+z*.24f,z,seed)*2-1);
            float rock=(Mathf.PerlinNoise(x*.035f+seed,z*.018f)-.5f)*9;
            float h=height*Mathf.Pow(section,1.28f)*peaks*(.62f+.48f*fineRidge)+rock*section;
            float end=west?Mathf.Min((z-z0)/180,(z1-z)/180):Mathf.Min((x-x0)/220,(x1-x)/220);
            h*=Mathf.SmoothStep(0,1,Mathf.Clamp01(end));
            if(!west)
            {
                float eastCoast=410+(z-z0)*.57f+110*Mathf.PerlinNoise(z*.0041f,seed*.73f);
                float coast=Mathf.Clamp01((eastCoast-x)/140);
                h*=Mathf.SmoothStep(0,1,coast);
                if(x>eastCoast-18)h-=Mathf.Min(14,(x-eastCoast+18)*.16f);
            }
            return h-3.8f;
        }
        static void GeologicalRidge(string name,float x0,float x1,float z0,float z1,float height,int seed,bool west,int nx,int nz)
        {
            var data=new LandscapeMesh();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float wx=Mathf.Lerp(x0,x1,(float)x/nx),wz=Mathf.Lerp(z0,z1,(float)z/nz);
                float h=RidgeHeight(wx,wz,x0,x1,z0,z1,height,seed,west);
                data.Add(new Vector3(wx,h,wz),new Color(.4f+TerrainNoise(wx,wz,seed)*.5f,Mathf.Clamp01((3-h)*.08f),0,1));
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;data.Tri(a,c,b);data.Tri(b,c,d);}
            var g=LandscapeObject(name,data,landscapeDistant,nature);
            g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        static void ReclamationBoundary(string name,float x0,float x1,float z0,float z1,int nx,int nz,Func<float,float,float> elevation)
        {
            var data=new LandscapeMesh();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float wx=Mathf.Lerp(x0,x1,(float)x/nx),wz=Mathf.Lerp(z0,z1,(float)z/nz),h=elevation(wx,wz);
                data.Add(new Vector3(wx,h,wz),new Color(.12f,Mathf.Clamp01((.1f-h)*.1f),0,1));
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;data.Tri(a,c,b);data.Tri(b,c,d);}
            LandscapeObject(name,data,landscapeGravel,landscapeRoot);
        }

        static void ConnectReclaimedSiteToHinterland()
        {
            ReclamationBoundary("Western reclaimed land cut and fill",-565,-495,-200,900,14,120,(x,z)=>
            {
                float u=Mathf.InverseLerp(-565,-495,x);
                float edge=RidgeHeight(-565,z,-1450,-475,-650,1680,195,71,true)+.035f;
                float rock=RidgeHeight(x,z,-1450,-475,-650,1680,195,71,true)+.03f;
                return Mathf.Max(Mathf.Lerp(edge,-.19f,Mathf.SmoothStep(0,1,u)),rock);
            });
            ReclamationBoundary("Northern graded industrial boundary",-500,430,895,960,120,16,(x,z)=>
            {
                float u=Mathf.InverseLerp(895,960,z);
                float edge=RidgeHeight(x,960,-1750,1200,875,1650,210,21,false)+.035f;
                float rock=RidgeHeight(x,z,-1750,1200,875,1650,210,21,false)+.03f;
                return Mathf.Max(Mathf.Lerp(-.19f,edge,Mathf.SmoothStep(0,1,u)),rock);
            });
            ReclamationBoundary("Southern ash hinterland and spoil swales",-800,430,-1000,-195,100,72,(x,z)=>
            {
                float fromSite=Mathf.Clamp01((-z-195)/130);
                float h=-.19f+TerrainNoise(x,z,89)*15*fromSite;
                float coast=Mathf.SmoothStep(0,1,Mathf.InverseLerp(430,380,x));
                return Mathf.Lerp(-1.4f,h,coast);
            });
        }

        static void GroundPatch(string name,Vector3 center,Vector2 radius,Material material,int seed,int edgeCount=28,float roughness=.18f)
        {
            var data=new LandscapeMesh();data.Add(center,new Color(0,.1f,0,1));
            for(int i=0;i<edgeCount;i++)
            {
                float a=i*Mathf.PI*2/edgeCount;
                float r=1+roughness*(Mathf.PerlinNoise(Mathf.Cos(a)*2+seed,Mathf.Sin(a)*2+seed)-.5f)*2;
                data.Add(center+new Vector3(Mathf.Cos(a)*radius.x*r,0,Mathf.Sin(a)*radius.y*r),Color.black);
            }
            for(int i=0;i<edgeCount;i++)data.Tri(0,1+(i+1)%edgeCount,1+i);
            var go=LandscapeObject(name,data,material,landscapeRoot);go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        static void GravelTransition(string name,Vector3 a,Vector3 b,float outerWidth,int seed)
        {
            var data=new LandscapeMesh();var direction=(b-a).normalized;var side=new Vector3(-direction.z,0,direction.x);
            int rows=Mathf.CeilToInt(Vector3.Distance(a,b)/4);
            for(int i=0;i<=rows;i++)
            {
                var p=Vector3.Lerp(a,b,(float)i/rows);
                float width=outerWidth*(.82f+Mathf.PerlinNoise(i*.41f,seed)*.4f);
                data.Add(p+Vector3.up*.015f,new Color(0,.14f,0,1));
                data.Add(p+side*width*.45f+Vector3.down*.025f,new Color(.08f,.04f,0,1));
                data.Add(p+side*width+Vector3.down*.12f,Color.black);
            }
            for(int i=0;i<rows;i++)for(int j=0;j<2;j++)
            {int q=i*3+j;data.Tri(q,q+1,q+3);data.Tri(q+1,q+4,q+3);}
            LandscapeObject(name,data,groundSlag,landscapeRoot);
        }

        static void DrainageRun(string name,Vector3 a,Vector3 b,bool frequentCovers)
        {
            var direction=(b-a).normalized;var right=new Vector3(direction.z,0,-direction.x);float length=Vector3.Distance(a,b);
            var concreteMesh=new LandscapeMesh();var channelMesh=new LandscapeMesh();var grateMesh=new LandscapeMesh();
            // Open, shallow channel with a dark bottom, sloping concrete sides, and inset covers.
            Vector3[] section={-right*.56f,-right*.4f-Vector3.up*.025f,-right*.23f-Vector3.up*.12f,
                               right*.23f-Vector3.up*.12f,right*.4f-Vector3.up*.025f,right*.56f};
            for(int j=0;j<5;j++)
            {
                var target=j==2?channelMesh:concreteMesh;
                target.Quad(a+section[j],b+section[j],b+section[j+1],a+section[j+1]);
            }
            int interval=frequentCovers?11:24;
            for(float distance=3;distance<length-2;distance+=interval)
            {
                var p=a+direction*distance;
                for(int bar=0;bar<8;bar++)
                {
                    var c=p+direction*(bar*.12f-.42f)+Vector3.up*.012f;
                    grateMesh.Quad(c-right*.37f-direction*.024f,c+right*.37f-direction*.024f,c+right*.37f+direction*.024f,c-right*.37f+direction*.024f);
                }
            }
            LandscapeObject(name+" concrete shoulders",concreteMesh,groundProcess,landscapeRoot);
            LandscapeObject(name+" stormwater bed",channelMesh,groundTidal,landscapeRoot);
            LandscapeObject(name+" inset grate covers",grateMesh,structuralSteel,landscapeRoot);
        }

        static void Apron(string name,float x0,float x1,float z0,float z1,float y,Material material,float bay=12)
        {
            var deck=new LandscapeMesh();
            // Preserve the plane while avoiding kilometre-scale physics triangles on harbor aprons.
            for(float x=x0;x<x1;x+=80)for(float z=z0;z<z1;z+=80)
                deck.Top(x,Mathf.Min(x+80,x1),z,Mathf.Min(z+80,z1),y);
            LandscapeObject(name+" pavement",deck,material,landscapeRoot);
            var seams=new LandscapeMesh();
            if(material!=groundTransport)
            {
                for(float x=x0+bay;x<x1-.1f;x+=bay)seams.Top(x-.022f,x+.022f,z0+.12f,z1-.12f,y+.008f);
                for(float z=z0+bay;z<z1-.1f;z+=bay)seams.Top(x0+.12f,x1-.12f,z-.022f,z+.022f,y+.008f);
            }
            if(seams.v.Count>0)LandscapeObject(name+" expansion joints",seams,landscapeJoint,landscapeRoot);
            // Irregular aggregate shoulders meet the lower site ground without an exposed vertical plate edge.
            GravelTransition(name+" south aggregate",new Vector3(x1,y,z0),new Vector3(x0,y,z0),3.6f,13);
            GravelTransition(name+" west aggregate",new Vector3(x0,y,z0),new Vector3(x0,y,z1),3.5f,17);
            GravelTransition(name+" north aggregate",new Vector3(x0,y,z1),new Vector3(x1,y,z1),3.7f,19);
            GravelTransition(name+" east aggregate",new Vector3(x1,y,z1),new Vector3(x1,y,z0),3.4f,23);
        }

        static void ProcessPipe(string name,Vector3[] path,float radius,Transform parent)
        {
            var data=new LandscapeMesh();const int sides=12;
            for(int i=0;i<path.Length;i++)
            {
                Vector3 forward=(i==path.Length-1?path[i]-path[i-1]:i==0?path[1]-path[0]:path[i+1]-path[i-1]).normalized;
                var right=Vector3.Cross(forward,Vector3.up).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;
                var up=Vector3.Cross(right,forward).normalized;
                for(int s=0;s<sides;s++){float a=s*Mathf.PI*2/sides;data.Add(path[i]+radius*(right*Mathf.Cos(a)+up*Mathf.Sin(a)),Color.black);}
            }
            for(int i=0;i<path.Length-1;i++)for(int s=0;s<sides;s++)
            {int a=i*sides+s,b=i*sides+(s+1)%sides,c=a+sides,d=b+sides;data.Tri(a,c,b);data.Tri(b,c,d);}
            LandscapeObject(name,data,agedRust,parent);
        }

        static void IndustrialGroundConnections()
        {
            var ground=structures.Find("site ground");if(ground!=null)ground.GetComponent<Renderer>().sharedMaterial=groundSlag;
            foreach(var surface in structures.Cast<Transform>())
            {
                var renderer=surface.GetComponent<Renderer>();if(renderer==null)continue;
                string id=surface.name;
                if(id.StartsWith("Z1 departure",StringComparison.Ordinal))renderer.sharedMaterial=groundMaintenance;
                else if(id.StartsWith("Z2 storage",StringComparison.Ordinal))renderer.sharedMaterial=groundTransport;
                else if(id.StartsWith("Z3 crane yard",StringComparison.Ordinal)||id=="boss south alignment apron")renderer.sharedMaterial=groundProcess;
                else if(id.Contains("connector")||id.Contains("district service road")||id=="coastal service road"||id.StartsWith("boss east transport",StringComparison.Ordinal))renderer.sharedMaterial=groundTransport;
            }
            foreach(var crossing in structures.Cast<Transform>().Where(t=>t.name.StartsWith("industrial cross road")))crossing.position+=Vector3.up*.018f;
            var bossApron=structures.Find("boss south alignment apron");if(bossApron!=null)bossApron.position+=Vector3.up*.02f;
            Apron("B02 repair hardstanding",-132,-60,8,86,-.055f,groundMaintenance,10);
            Apron("B01 rear maintenance apron",-45,45,-70,-3,-.055f,groundMaintenance,12);
            Apron("B03 pump and evacuation hardstanding",62,119,13,86,-.055f,groundProcess,10);
            Apron("B04 casting building apron",35,128,107,183,-.055f,groundProcess,14);
            Apron("B05 rolling mill hardstanding",-201,-111,170,301,-.055f,groundTransport,14);
            Apron("B06 pressure plant apron",-178,-132,309,365,-.055f,groundProcess,10);
            Apron("B09 purification apron",-145,-73,529,580,-.055f,groundProcess,12);
            Apron("B08 furnace support apron",38,163,655,777,-.055f,groundProcess,14);
            // Main combat floors stay exactly where they are. Only 4.4-cm-wide joints are added.
            var joints=new LandscapeMesh();
            foreach(var rect in new[]{new Vector4(-60,60,0,100),new Vector4(-30,210,450,650)})
            {
                for(float x=rect.x+16;x<rect.y;x+=16)joints.Top(x-.022f,x+.022f,rect.z,rect.w,.008f);
                for(float z=rect.z+16;z<rect.w;z+=16)joints.Top(rect.x,rect.y,z-.022f,z+.022f,.008f);
            }
            LandscapeObject("Combat yard expansion joints",joints,landscapeJoint,landscapeRoot);
            GravelTransition("Z1 east yard edge",new Vector3(60,0,80),new Vector3(60,0,10),4,7);
            GravelTransition("Z2 western yard edge",new Vector3(-110,0,180),new Vector3(-110,0,360),4,9);
            GravelTransition("Z2 northern yard edge",new Vector3(-110,0,360),new Vector3(-24,0,360),4,12);
            GravelTransition("Z3 western yard edge",new Vector3(-30,0,460),new Vector3(-30,0,640),4,15);
            GravelTransition("Departure road west verge",new Vector3(-16,.02f,103),new Vector3(-16,.02f,177),4,19);
            GravelTransition("Departure road east verge",new Vector3(16,.02f,177),new Vector3(16,.02f,103),4,22);
            DrainageRun("B02 east runoff",new Vector3(-62,-.025f,11),new Vector3(-62,-.025f,80),true);
            DrainageRun("B03 outer runoff",new Vector3(121,-.025f,17),new Vector3(121,-.025f,80),true);
            DrainageRun("Z1 east storm drain",new Vector3(18.5f,-.025f,104),new Vector3(18.5f,-.025f,176),true);
            DrainageRun("West district trunk drain",new Vector3(-222,-.025f,-70),new Vector3(-222,-.025f,800),false);
            DrainageRun("Harbor service runoff",new Vector3(362,-.025f,-80),new Vector3(362,-.025f,778),false);
            // Standing water collects beside drainage/failed cladding, away from required footprints.
            GroundPatch("B02 runoff shallow puddle",new Vector3(-64,-.032f,71),new Vector2(2.1f,6.8f),landscapePuddle,15,26);
            GroundPatch("B02 roof runoff damp aggregate",new Vector3(-126,-.13f,76),new Vector2(5.4f,11),landscapeWet,16,28);
            GroundPatch("B03 pipe leak puddle",new Vector3(117,-.031f,62),new Vector2(1.5f,5.2f),landscapePuddle,17,22);
            GroundPatch("Z2 drainage seep",new Vector3(-105,.013f,335),new Vector2(3.2f,8.5f),landscapePuddle,19,28);
            GroundPatch("Z3 service yard rain pool",new Vector3(-22,.013f,620),new Vector2(3.5f,11),landscapePuddle,23,30);
            var pipeGroup=Group("Connected process utilities outside combat lanes",landscapeRoot);
            for(int i=0;i<3;i++)
            {
                float x=125+i*1.4f;
                ProcessPipe("B03 to B04 process main "+i,new[]{new Vector3(108,4+i*.7f,45),new Vector3(x-1,4+i*.7f,45),new Vector3(x,5.2f+i*.7f,46),new Vector3(x,6.2f+i*.7f,48),new Vector3(x,6.2f+i*.7f,108),new Vector3(x-1,6.2f+i*.7f,110),new Vector3(116,6.2f+i*.7f,110)},.33f,pipeGroup);
                ProcessPipe("Rolling mill pressure utility "+i,new[]{new Vector3(-185,5+i*.8f,265),new Vector3(-185,5+i*.8f,318),new Vector3(-184,5+i*.8f,321),new Vector3(-178,5+i*.8f,323),new Vector3(-165,5+i*.8f,323)},.4f,pipeGroup);
            }
            var supports=new LandscapeMesh();
            foreach(float z in new[]{51f,69f,87f,105f})
            {supports.Block(new Vector3(127,2.8f,z),new Vector3(.35f,5.6f,.35f));supports.Block(new Vector3(127,5.7f,z),new Vector3(6,.3f,.45f));}
            foreach(float z in new[]{274f,291f,308f})
            {supports.Block(new Vector3(-185,2.2f,z),new Vector3(.4f,4.4f,.4f));supports.Block(new Vector3(-185,4.5f,z),new Vector3(2.4f,.3f,.45f));}
            LandscapeObject("Process pipe low trestles",supports,structuralSteel,pipeGroup,true);
        }

        static void ShoreTransition(string name,float z0,float z1,bool north)
        {
            var data=new LandscapeMesh();const int cols=10;int rows=Mathf.CeilToInt((z1-z0)/6);
            for(int z=0;z<=rows;z++)for(int x=0;x<=cols;x++)
            {
                float wz=Mathf.Lerp(z0,z1,(float)z/rows),u=(float)x/cols;
                float shore=429+(Mathf.PerlinNoise(wz*.008f,13.7f)-.5f)*18;
                if(north)shore+=Mathf.Max(0,wz-870)*.45f;
                float wx=shore-36+u*92;
                float y=u<.34f?-.16f:Mathf.Lerp(.1f,-10,Mathf.Pow((u-.34f)/.66f,.82f));
                float rough=Mathf.Sin(wz*.37f+u*17)*Mathf.Sin(u*33+wz*.11f)*.6f;
                y+=rough*Mathf.Sin(u*Mathf.PI);
                data.Add(new Vector3(wx,y,wz),new Color(.6f,Mathf.Clamp01((.3f-y)*.3f),0,1));
            }
            for(int z=0;z<rows;z++)for(int x=0;x<cols;x++)
            {int a=z*(cols+1)+x,b=a+1,c=a+cols+1,d=c+1;data.Tri(a,c,b);data.Tri(b,c,d);}
            LandscapeObject(name,data,landscapeRock,landscapeRoot);
        }

        static void HarborAndCoast()
        {
            var oldWall=nature.Find("coastal embankment");if(oldWall!=null)Obj.DestroyImmediate(oldWall.gameObject);
            foreach(var old in districts.Cast<Transform>().Where(t=>t.name.StartsWith("coastal quay module")).ToArray())Obj.DestroyImmediate(old.gameObject);
            Apron("Harbor reclaimed loading apron",363,428,-100,790,-.055f,groundHarbor,18);
            ShoreTransition("South reclaimed shore rock toe",-350,-85,false);
            ShoreTransition("North coastal geology transition",765,1140,true);
            var face=new LandscapeMesh();var cap=new LandscapeMesh();var fenders=new LandscapeMesh();var rails=new LandscapeMesh();
            const float z0=-100,z1=790;
            // True seawall cross-section: inland shoulder, bevelled coping, tide-stained face, toe below sea.
            for(float z=z0;z<z1;z+=18)
            {
                float end=Mathf.Min(z+17.94f,z1);
                face.Quad(new Vector3(430,-8,z),new Vector3(430,.12f,z),new Vector3(430,.12f,end),new Vector3(430,-8,end));
                cap.Quad(new Vector3(427.8f,-.055f,z),new Vector3(427.8f,-.055f,end),new Vector3(429.7f,.18f,end),new Vector3(429.7f,.18f,z));
                cap.Quad(new Vector3(429.7f,.18f,z),new Vector3(429.7f,.18f,end),new Vector3(430,.12f,end),new Vector3(430,.12f,z));
                // Rubber fenders sit on the water-facing wall; bollards occupy the solid apron edge.
                if(((int)(z-z0)/18)%2==0)
                {
                    fenders.Block(new Vector3(430.3f,-.85f,z+7),new Vector3(.55f,2,1.5f));
                    rails.Block(new Vector3(425.8f,.28f,z+7),new Vector3(.65f,.65f,.65f));
                    rails.Block(new Vector3(425.8f,.63f,z+7),new Vector3(1.1f,.2f,.4f));
                }
            }
            LandscapeObject("Quay continuous tidal retaining face",face,groundTidal,landscapeRoot,true);
            LandscapeObject("Quay bevelled coping and shoulder",cap,groundHarbor,landscapeRoot,true);
            LandscapeObject("Harbor wall rubber fenders",fenders,agedRoof,landscapeRoot);
            LandscapeObject("Harbor mooring bollards",rails,structuralSteel,landscapeRoot);
            for(int i=0;i<4;i++)
            {
                float z=80+i*170;
                var deck=new LandscapeMesh();deck.Block(new Vector3(436,-.35f,z),new Vector3(18,.58f,34));
                LandscapeObject("Crane loading pier "+i,deck,groundHarbor,landscapeRoot,true);
                for(int j=0;j<4;j++)for(int side=0;side<2;side++)
                    Cylinder("Quay support pile",new Vector3(431+side*11,-4.1f,z-12+j*8),.65f,8.1f,groundTidal,landscapeRoot,false);
                GroundPatch("Harbor apron drained rain pool "+i,new Vector3(408,-.035f,z+27),new Vector2(5,2.2f),landscapePuddle,42+i,24);
            }
            // Rails and sleepers establish the cranes' industrial connection without touching play lanes.
            var craneRails=new LandscapeMesh();var sleepers=new LandscapeMesh();
            for(int side=0;side<2;side++)
            {
                float x=375+side*8;
                craneRails.Block(new Vector3(x,.035f,345),new Vector3(.16f,.16f,870));
            }
            for(float z=-84;z<780;z+=3.1f)sleepers.Block(new Vector3(379,-.015f,z),new Vector3(10,.08f,.32f));
            LandscapeObject("Quayside crane rail pair",craneRails,structuralSteel,landscapeRoot);
            LandscapeObject("Quayside embedded concrete sleepers",sleepers,groundMaintenance,landscapeRoot);
            // A low broken sheltering arm reads as a port entrance, not another edge of a canal.
            var breakwater=new LandscapeMesh();
            for(int i=0;i<11;i++)
            {
                var a=new Vector3(439+i*11,-.45f,-118+i*3.8f);var b=a+new Vector3(11,0,3.8f);
                Vector3 d=(b-a).normalized,side=new Vector3(-d.z,0,d.x);
                breakwater.Quad(a-side*7+Vector3.down*3,a-side*2+Vector3.up*.35f,b-side*2+Vector3.up*.35f,b-side*7+Vector3.down*3);
                breakwater.Quad(a-side*2+Vector3.up*.35f,a+side*2+Vector3.up*.35f,b+side*2+Vector3.up*.35f,b-side*2+Vector3.up*.35f);
                breakwater.Quad(a+side*2+Vector3.up*.35f,a+side*7+Vector3.down*3,b+side*7+Vector3.down*3,b+side*2+Vector3.up*.35f);
            }
            LandscapeObject("South harbor rubble mound breakwater",breakwater,landscapeRock,landscapeRoot);
        }

        static void UpgradeLandscapeAndGround()
        {
            landscapeMeshCount=landscapeVertices=landscapeTriangles=0;
            var previous=GameObject.Find("13 Geological coast and industrial ground");if(previous!=null)Obj.DestroyImmediate(previous);
            landscapeRoot=Group("13 Geological coast and industrial ground");
            landscapeRock=GeologicalMaterial("layered coastal rock",new Color(.79f,.8f,.76f),.57f,.08f);
            RefreshLandscapeDistanceLook();
            IndustrialGroundPalette();
            landscapeGravel=GeologicalMaterial("compacted slag and gravel",new Color(.72f,.69f,.63f),0,.06f);
            landscapeWet=GeologicalMaterial("tidal and runoff ground",new Color(.61f,.64f,.61f),.35f,.48f);
            landscapeJoint=Material("Landscape_dry_expansion_seal",new Color(.24f,.25f,.24f));landscapeJoint.shader=Shader.Find("Standard");landscapeJoint.SetFloat("_Glossiness",.1f);
            landscapePuddle=Material("Landscape_shallow_rainwater",new Color(.15f,.18f,.19f));landscapePuddle.shader=Shader.Find("Standard");landscapePuddle.SetFloat("_Metallic",.06f);landscapePuddle.SetFloat("_Glossiness",.93f);
            foreach(var child in nature.Cast<Transform>().Where(t=>t.name.StartsWith("Reused mountain")||t.name.StartsWith("Reused west mountain")||t.name.StartsWith("Geological ridge")).ToArray())Obj.DestroyImmediate(child.gameObject);
            GeologicalRidge("Geological ridge northern eroded foothills",-1750,1200,875,1650,210,21,false,180,64);
            GeologicalRidge("Geological ridge middle overlapping massif",-2300,1600,1170,2300,300,37,false,170,58);
            GeologicalRidge("Geological ridge far storm silhouette",-2700,2000,1570,3100,390,53,false,140,48);
            GeologicalRidge("Geological ridge western cut slopes",-1450,-475,-650,1680,195,71,true,66,145);
            ConnectReclaimedSiteToHinterland();IndustrialGroundConnections();HarborAndCoast();
            Directory.CreateDirectory(WideOutput);
            File.WriteAllText(WideOutput+"/landscape-audit.txt",
                "Generated native landscape mesh assets: "+landscapeMeshCount+"\nVertices: "+landscapeVertices+"\nTriangles: "+landscapeTriangles+"\n"+
                "Ocean retained at x>=430, surface y=-0.6. Existing 650m mech, drone and boss routes are not moved.\n"+
                "No terrain tessellation, runtime noise generation, real-time reflection capture, or new runtime behaviour.\n"+
                "Four distant ridges: separate 32m rock / 12m gravel material, slow coordinate warp, broad albedo blend, normal detail fades 150-450m.\n"+
                "Ground palette: Concrete040 1.4m maintenance/process/harbor variants; Asphalt030 2.2m transport yards; Gravel024 1.5m aggregate shoulders.\n"+
                "Distant terrain has no collider; physical quay walls/piers and low process trestles have native mesh colliders.\n"+
                "Low-profile aprons, drainage, joints and puddles are environmental dressing. Full traversal/HMD performance remains to be measured.\n");
            var assignments=new List<string>{"object,material,primary_albedo,secondary_albedo,primary_tile_m,secondary_tile_m,wetness,roughness_multiplier"};
            foreach(var renderer in Obj.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                foreach(var material in renderer.sharedMaterials)
                    if(material!=null&&material.name.StartsWith("Landscape_surface_",StringComparison.Ordinal))
                        assignments.Add(renderer.name.Replace(',','_')+","+material.name+","+material.GetTexture("_GravelTex").name+","+material.GetTexture("_RockTex").name+","+
                            material.GetFloat("_GravelMetres").ToString(Inv)+","+material.GetFloat("_RockMetres").ToString(Inv)+","+material.GetFloat("_Wetness").ToString(Inv)+","+material.GetFloat("_RoughnessScale").ToString(Inv));
            File.WriteAllLines(WideOutput+"/ground-surface-assignment.csv",assignments);
        }

        // Also safe after opening an already generated/baked scene: changes only
        // the four distant-ridge material references, without recreating meshes.
        static void RefreshLandscapeDistanceLook()
        {
            landscapeDistant=GeologicalMaterial("distant eroded massif",new Color(.79f,.8f,.76f),.65f,.02f);
            landscapeDistant.SetFloat("_RockMetres",32);landscapeDistant.SetFloat("_GravelMetres",12);
            landscapeDistant.SetFloat("_NormalStrength",.15f);landscapeDistant.SetFloat("_DistantTerrain",1);
            EditorUtility.SetDirty(landscapeDistant);
            foreach(var renderer in Obj.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if(renderer.name.StartsWith("Geological ridge",StringComparison.Ordinal))renderer.sharedMaterial=landscapeDistant;
        }
    }
}
