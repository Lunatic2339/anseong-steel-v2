using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Anseong.Map35;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string Base="Assets/Map35/", ScenePath=Base+"Scenes/Steelworks35m.unity";
        const string Output="../docs/validation/map35";
        static Transform structures, details, routes, labels, volumes, proxies, nature, cameras;
        static Material steel,rust,concrete,orange,teal,purple,cream,green,water,dark,red;
        static Material routeMat; static Mesh coilMesh;
        static Map35Viewer viewer;
        static readonly List<Camera> cameraList=new List<Camera>();
        static readonly Dictionary<string,Vector3> targets=new Dictionary<string,Vector3>();
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;

        static Material Material(string name,Color color,bool unlit=false)
        {
            var path=Base+"Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(unlit?"Sprites/Default":"Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;if(!unlit)m.SetFloat("_Glossiness",.18f);EditorUtility.SetDirty(m);return m;
        }
        static Transform Group(string name,Transform parent=null)
        {var t=new GameObject(name).transform;if(parent!=null)t.SetParent(parent);return t;}
        static GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 scale,Material mat,Transform parent,bool collide=true)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent);g.transform.position=p;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collide)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            else if(type==PrimitiveType.Sphere)
            {
                // Nonuniform SphereCollider expands to the largest scale and overstates sightline occlusion.
                UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
                g.AddComponent<MeshCollider>().sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;
            }
            return g;
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,Transform parent,bool collision=true)=>Shape(name,PrimitiveType.Cube,p,size,mat,parent,collision);
        static GameObject Cylinder(string name,Vector3 p,float radius,float height,Material mat,Transform parent,bool collision=true)=>Shape(name,PrimitiveType.Cylinder,p,new Vector3(radius*2,height*.5f,radius*2),mat,parent,collision);
        static void Beam(string name,Vector3 a,Vector3 b,float width,Material mat,Transform parent,bool collision=false)
        {var g=Box(name,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),mat,parent,collision);g.transform.rotation=Quaternion.LookRotation(b-a);}
        static void Line(string name,IEnumerable<Vector3> points,Color color,float width,Transform parent,bool loop=false)
        {
            var lr=new GameObject(name).AddComponent<LineRenderer>();lr.transform.SetParent(parent);lr.sharedMaterial=routeMat;lr.useWorldSpace=true;
            lr.startColor=lr.endColor=color;lr.startWidth=lr.endWidth=width;var p=points.ToArray();lr.positionCount=p.Length;lr.SetPositions(p);lr.loop=loop;
        }
        static void Label(string name,Vector3 position,float size=1.1f,Color? color=null)
        {
            var t=new GameObject("Label "+name).AddComponent<TextMesh>();t.transform.SetParent(labels);t.transform.position=position;
            t.text=name;t.characterSize=size;t.fontSize=60;t.anchor=TextAnchor.MiddleCenter;t.color=color??Color.white;
        }
        static void Rect(string name,float minX,float maxX,float minZ,float maxZ,float y,Color color,Transform group)
        {Line(name,new[]{new Vector3(minX,y,minZ),new Vector3(maxX,y,minZ),new Vector3(maxX,y,maxZ),new Vector3(minX,y,maxZ)},color,.55f,group,true);}
        static void Circle(string name,Vector3 center,float radius,Color color,Transform parent)
        {var p=new List<Vector3>();for(int i=0;i<64;i++){float a=i*Mathf.PI/32;p.Add(center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}Line(name,p,color,.5f,parent,true);}
        static void Floor(string name,float x0,float x1,float z0,float z1,Material mat)
        {
            float top=name=="site ground"?-.2f:name.Contains("connector")?.06f:name.Contains("transport")||name.Contains("apron")?.02f:0;
            Box(name,new Vector3((x0+x1)/2,top-.15f,(z0+z1)/2),new Vector3(x1-x0,.3f,z1-z0),mat,structures);
        }
        static void Warehouse(string id,float x0,float x1,float z0,float z1,float h,bool damaged=false)
        {
            var root=Group(id,structures);float cx=(x0+x1)*.5f,cz=(z0+z1)*.5f;
            Box("building shell",new Vector3(cx,h*.5f,cz),new Vector3(x1-x0,h,z1-z0),steel,root);
            Box("roof",new Vector3(cx,h+.4f,cz),new Vector3(x1-x0+1,.8f,z1-z0+1),dark,root);
            for(float x=x0+2;x<x1;x+=8)Box("facade rib",new Vector3(x,h*.5f,z0-.15f),new Vector3(.4f,h,.4f),rust,root,false);
            for(float z=z0+2;z<z1;z+=8)Box("side rib",new Vector3(x0-.15f,h*.5f,z),new Vector3(.4f,h,.4f),rust,root,false);
            for(int i=0;i<3;i++)Box("service shutter",new Vector3(x0+(i+1)*(x1-x0)/4,4,z0-.3f),new Vector3(5,8,.3f),dark,root,false);
            Box("yellow building trim",new Vector3(cx,h*.7f,z0-.35f),new Vector3(x1-x0,.7f,.3f),orange,root,false);
            if(damaged)
            {
                var hazard=Group("existing damage - no dynamic destruction",details);
                for(int i=0;i<7;i++)
                {
                    var p=new Vector3(x0+3+i*4,1.5f,z0+5+(i%3)*9);
                    var g=Box("collapsed panel",p,new Vector3(7,2,6),rust,hazard,false);g.transform.rotation=Quaternion.Euler(i*13, i*31, 22);
                    Shape("fire placeholder",PrimitiveType.Sphere,p+Vector3.up*4,new Vector3(3,7,3),orange,hazard,false);
                }
            }
            Label(id,new Vector3(cx,h+5,cz));
        }
        static void Hangar()
        {
            var root=Group("B01 Mech maintenance hangar",structures);
            Box("west wall",new Vector3(-29.5f,24,-27.5f),new Vector3(1,48,55),steel,root);
            Box("east wall",new Vector3(29.5f,24,-27.5f),new Vector3(1,48,55),steel,root);
            Box("back wall",new Vector3(0,24,-54.5f),new Vector3(60,48,1),steel,root);
            Box("roof",new Vector3(0,47,-27.5f),new Vector3(60,2,55),dark,root);
            Box("left front pier",new Vector3(-21,21,-.5f),new Vector3(18,42,1),steel,root);
            Box("right front pier",new Vector3(21,21,-.5f),new Vector3(18,42,1),steel,root);
            Box("door lintel 42m clear",new Vector3(0,45,-.5f),new Vector3(60,6,1),rust,root);
            for(int i=0;i<5;i++)Beam("ceiling frame",new Vector3(-29,45,-4-i*10),new Vector3(29,45,-4-i*10),1.2f,orange,root);
            Label("B01 / 42m door",new Vector3(0,52,-25));
        }
        static Mesh MakeCoil()
        {
            string path=Base+"Meshes/SteelCoil.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            const int rings=32,sides=12;
            for(int r=0;r<rings;r++)for(int s=0;s<sides;s++)
            {float a=r*2*Mathf.PI/rings,b=s*2*Mathf.PI/sides;float radial=4+2*Mathf.Cos(b);vertices.Add(new Vector3(radial*Mathf.Cos(a),2*Mathf.Sin(b),radial*Mathf.Sin(a)*5/6));}
            for(int r=0;r<rings;r++)for(int s=0;s<sides;s++)
            {int a=r*sides+s,b=((r+1)%rings)*sides+s,c=((r+1)%rings)*sides+(s+1)%sides,d=r*sides+(s+1)%sides;triangles.AddRange(new[]{a,c,b,a,d,c});}
            var m=existing!=null?existing:new Mesh{name="12x4x10 steel coil"};m.Clear();m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();if(existing==null)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);return m;
        }
        static Transform Coil(string name,Vector3 position,Transform parent)
        {var g=new GameObject(name);g.transform.SetParent(parent);g.transform.position=position;g.AddComponent<MeshFilter>().sharedMesh=coilMesh;g.AddComponent<MeshRenderer>().sharedMaterial=steel;var c=g.AddComponent<BoxCollider>();c.size=new Vector3(12,4,10);return g.transform;}
        static void Crane()
        {
            var root=Group("B07 Crane 68m / underside 62m",structures);
            foreach(float x in new[]{10f,110f})foreach(float z in new[]{375f,435f})
            {
                Box("6x6 footing",new Vector3(x,1,z),new Vector3(6,2,6),concrete,root);
                Box("crane column",new Vector3(x,32,z),new Vector3(3,64,3),rust,root);
                for(int i=0;i<6;i++)Beam("brace",new Vector3(x-1.4f,4+i*9,z-1.4f),new Vector3(x+1.4f,13+i*9,z-1.4f),.35f,orange,root);
            }
            foreach(float z in new[]{375f,435f})Box("cross beam",new Vector3(60,65,z),new Vector3(106,6,4),orange,root);
            foreach(float x in new[]{10f,110f})Box("runway beam",new Vector3(x,65,405),new Vector3(4,6,60),rust,root);
            Box("travelling bridge",new Vector3(60,65,405),new Vector3(100,6,3),orange,root);
            Box("trolley",new Vector3(60,64,405),new Vector3(12,4,8),steel,root);
            Beam("hoist cable",new Vector3(60,62,405),new Vector3(60,54,405),.25f,dark,root);
            viewer.coil=Coil("coil suspended Y50-54",new Vector3(60,52,405),root);
            viewer.lateCoil=Coil("coil settled after hit",new Vector3(27,2,405),root).gameObject;viewer.lateCoil.SetActive(false);
            Box("R1 receiving slab",new Vector3(27,.3f,405),new Vector3(18,.6f,20),concrete,root);
            Label("B07 / 68m crane",new Vector3(60,76,405),1.8f);
        }
        static void Furnace()
        {
            var root=Group("B08 Protected furnace / 90m landmark",structures);
            Box("process hall",new Vector3(70,14,510),new Vector3(90,28,60),steel,root);
            Cylinder("blast vessel",new Vector3(52,44,510),15,40,rust,root);
            Cylinder("north stack 90m",new Vector3(96,45,520),6,90,steel,root);
            Cylinder("hot blast stove",new Vector3(85,29,490),7,58,rust,root);
            for(int i=0;i<5;i++)
            {
                Box("vessel platform",new Vector3(52,30+i*7,510),new Vector3(33,.7f,33),dark,root,false);
                Beam("pipe",new Vector3(27,15+i*2,482),new Vector3(110,15+i*2,482),1.2f,orange,root);
            }
            Label("B08 / protected furnace 90m",new Vector3(70,99,510),1.7f);
        }
        static void Mech(string name,Vector3 position)
        {
            var root=Group(name,proxies);root.position=position;
            Box("torso",position+new Vector3(0,23,0),new Vector3(9,12,5),teal,root);
            Box("head / 35m datum",position+new Vector3(0,32,0),new Vector3(5,6,5),steel,root);
            foreach(int s in new[]{-1,1})
            {
                Box("leg",position+new Vector3(s*3,8,0),new Vector3(3,16,4),steel,root);
                Box("foot",position+new Vector3(s*3,1,1),new Vector3(4,2,6),dark,root);
                Box("folded arm",position+new Vector3(s*5,22,1),new Vector3(2,11,3),teal,root);
            }
            Label(name+" / 35m",position+Vector3.up*39);
            int layer=position.z<100?8:position.z<300?9:10;
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=layer;
        }
        static void Boss(string name,Vector3 p)
        {
            var root=Group(name,proxies);
            Shape("body proxy",PrimitiveType.Sphere,p+new Vector3(0,30,5),new Vector3(34,30,54),purple,root);
            Shape("head proxy",PrimitiveType.Sphere,p+new Vector3(0,25,-29),new Vector3(24,20,12),rust,root);
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
                Box("leg proxy",p+new Vector3(sx*15,9,sz*22),new Vector3(10,18,12),purple,root);
            Label(name+" / same boss, phase pose",p+Vector3.up*49,1.2f);
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=p.z<300?11:12;
        }
        static void Target(string id,Vector3 p)
        {targets[id]=p;Shape(id,PrimitiveType.Sphere,p,Vector3.one*2,orange,details,false);Label(id,p+Vector3.up*3,.7f);}
        static Camera MakeCamera(string name,Vector3 position,Vector3 look,float vfov=60,bool ortho=false,float size=350)
        {
            var c=new GameObject(name).AddComponent<Camera>();c.transform.SetParent(cameras);c.transform.position=position;c.transform.LookAt(look);
            c.fieldOfView=vfov;c.nearClipPlane=.3f;c.farClipPlane=2500;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.15f,.23f,.3f);
            c.orthographic=ortho;c.orthographicSize=size;c.enabled=false;cameraList.Add(c);return c;
        }
        static void Route(string name,Vector3[] p,string[] names,Color color,bool ground)
        {
            var drawn=p.Select(v=>v+(ground?Vector3.up*.5f:Vector3.zero)).ToArray();Line(name,drawn,color,ground?1.2f:.55f,routes);
            for(int i=0;i<p.Length;i++)
            {
                if(i==p.Length-1&&ground)continue;
                Shape(name+" point "+i,PrimitiveType.Sphere,drawn[i],Vector3.one*(ground?2:1.2f),ground?teal:orange,routes,false);
                Label(names[i],drawn[i]+new Vector3(0,ground?3:2,0),.65f,color);
                if(!ground)Line("altitude "+names[i],new[]{new Vector3(p[i].x,.4f,p[i].z),p[i]},color*.65f,.12f,routes);
            }
        }
        static void Hills()
        {
            Ridge("West ridge",-520,-180,-170,850,true);
            Ridge("North ridge",-520,420,620,1040,false);
            Floor("river surface",190,255,-110,760,water);
            Box("west levee",new Vector3(185,1,320),new Vector3(10,2,860),green,nature);
            Box("east levee",new Vector3(260,1,320),new Vector3(10,2,860),green,nature);
            Box("distant service bridge",new Vector3(222,8,-75),new Vector3(95,2,10),concrete,nature);
            for(int i=0;i<4;i++)Box("bridge pier",new Vector3(190+i*22,4,-75),new Vector3(2,8,8),steel,nature);
        }
        static void Ridge(string name,float x0,float x1,float z0,float z1,bool west)
        {
            var points=new List<Vector3>();var indices=new List<int>();const int n=32;
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)
            {
                float u=x/(float)n,v=z/(float)n,px=Mathf.Lerp(x0,x1,u),pz=Mathf.Lerp(z0,z1,v);
                float band=Mathf.Sin(Mathf.PI*(west?u:v));float end=Mathf.Clamp01((west?Mathf.Min(v,1-v):Mathf.Min(u,1-u))*7);
                float h=band*end*(65+100*Mathf.PerlinNoise(px*.013f+20,pz*.011f+20));points.Add(new Vector3(px,h-.5f,pz));
            }
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){int a=z*(n+1)+x,b=a+1,c=a+n+1,d=c+1;indices.AddRange(new[]{a,c,b,b,c,d});}
            var path=Base+"Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool fresh=mesh==null;
            if(fresh)mesh=new Mesh{name=name};else mesh.Clear();mesh.SetVertices(points);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(fresh)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            var g=new GameObject(name);g.transform.SetParent(nature);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=green;
        }
        [MenuItem("Anseong/Map35/Generate Whole Map")]
        public static void Generate()
        {
            Anseong.Z1.Editor.Z1Build.PrepareXR();
            foreach(string d in new[]{"Scenes","Materials","Meshes"})Directory.CreateDirectory(Base+d);
            Directory.CreateDirectory(Output);cameraList.Clear();targets.Clear();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            viewer=new GameObject("Map35 review controls").AddComponent<Map35Viewer>();
            structures=Group("01 Buildings and traversable ground");details=Group("02 Interaction markers and scale details");nature=Group("03 Landscape and river");
            proxies=Group("04 Scale proxies - phase snapshots");routes=Group("05 Routes - mech cyan drone amber boss violet");labels=Group("06 Labels");volumes=Group("07 Reserved volumes");cameras=Group("08 Review cameras");
            steel=Material("Steel",new Color(.24f,.31f,.35f));rust=Material("Oxide",new Color(.43f,.23f,.14f));concrete=Material("Concrete",new Color(.35f,.37f,.35f));
            dark=Material("Roof",new Color(.1f,.14f,.17f));orange=Material("SafetyAmber",new Color(.95f,.5f,.08f));teal=Material("MechCyan",new Color(.1f,.68f,.69f));purple=Material("BossProxy",new Color(.32f,.22f,.38f));
            green=Material("Landscape",new Color(.18f,.29f,.23f));water=Material("River",new Color(.13f,.36f,.46f));cream=Material("Yard",new Color(.3f,.29f,.25f));red=Material("Danger",new Color(.85f,.16f,.1f));
            routeMat=Material("RouteLine",Color.white,true);coilMesh=MakeCoil();
            Floor("site ground",-180,180,-70,570,cream);
            Floor("Z1 departure 80x70",-40,40,0,70,concrete);Floor("Z2 storage 150x120",-75,75,130,250,concrete);Floor("Z3 crane yard 160x140",-20,140,320,460,concrete);
            Floor("24m connector 1",-12,12,70,130,dark);Floor("connector 2 south",-12,12,250,285,dark);Floor("connector 2 east",-12,72,273,297,dark);Floor("connector 2 north",48,72,285,340,dark);
            Floor("boss east transport 60m",85,145,200,355,concrete);Floor("boss south alignment apron",15,160,270,360,concrete);
            Hangar();Warehouse("B02 damaged maintenance",-80,-45,15,55,18,true);Warehouse("B03 control room",45,65,20,45,12);
            Warehouse("B04 transport plant",20,70,80,120,48);Warehouse("B05 material warehouse",-120,-80,160,190,18);
            Cylinder("B06 optional tank",new Vector3(-100,12.5f,230),9,25,rust,structures);Label("B06 optional tank",new Vector3(-100,30,230));
            Crane();Furnace();Warehouse("B09 cooling / pipe hall",-85,-30,365,425,25);
            for(int i=0;i<4;i++)Beam("B09 pipe",new Vector3(-82,27+i*1.5f,370),new Vector3(-32,27+i*1.5f,370),1,orange,details);
            viewer.plate=Box("I1 sliding plate",new Vector3(0,24,8),new Vector3(10,8,1),orange,details).transform;
            Box("plate rail",new Vector3(-11,29,8),new Vector3(33,1,1),steel,details);
            Target("I1 push face",new Vector3(0,24,7.3f));Target("I1 control status",new Vector3(-5.4f,26,8));Target("I1 drone latch",new Vector3(8,26,8));
            Target("T1 front shell",new Vector3(-12,32,185));Target("T2 west nerve",new Vector3(-20,34,198));Target("T3 low neck",new Vector3(0,24,178));
            Target("E01 control emitter",new Vector3(-60,35,200));Target("P02 crane latch",new Vector3(110,48,405));Target("Coil east edge",new Vector3(66.4f,52,405));Target("I6 final weakpoint",new Vector3(60,24,362));
            Mech("Mech Z1",new Vector3(0,0,-20));Mech("Mech Z2",new Vector3(0,0,170));Mech("Mech Z3",new Vector3(60,0,354));
            Boss("Boss Z2",new Vector3(0,0,215));Boss("Boss Z3",new Vector3(60,0,405));
            for(int i=0;i<6;i++)
            {
                var c=Coil("stored coil",new Vector3(-58+(i%2)*9,1,145+(i/2)*10),details);c.localScale=Vector3.one*.5f;
                Box("stacked slab",new Vector3(50,2+i*.6f,165),new Vector3(18,.5f,8),rust,details);
            }
            for(int i=0;i<3;i++)
            {
                Shape("1.8m person scale",PrimitiveType.Capsule,new Vector3(42+i*2,.9f,23),new Vector3(.5f,.9f,.5f),orange,details,false);
                Box("service vehicle 6m",new Vector3(48+i*5,1.2f,55),new Vector3(2.4f,2.4f,6),steel,details);
            }
            Hills();
            Route("Mech route",Map35Layout.Mech,Map35Layout.MechNames,new Color(.1f,1,.9f),true);
            Route("Drone route",Map35Layout.Drone,Map35Layout.DroneNames,new Color(1,.65f,.1f),false);
            Line("Optional D6 ascent",Map35Layout.DroneOptional,new Color(1,.85f,.4f),.35f,routes);
            Line("Boss route - staged movement",Map35Layout.BossRoute.Select(p=>p+Vector3.up*.8f),new Color(.8f,.35f,1),1.3f,routes);
            Rect("mech connector 1",-12,12,70,130,.6f,Color.cyan,volumes);
            Circle("turning pad M7",new Vector3(0,.7f,285),20,Color.cyan,volumes);Circle("turning pad M8",new Vector3(60,.7f,285),20,Color.cyan,volumes);
            Rect("boss road",85,145,200,355,.6f,new Color(.8f,.35f,1),volumes);Rect("boss alignment",15,160,270,360,.6f,new Color(.8f,.35f,1),volumes);
            Circle("Q1 fall radius24",new Vector3(60,.8f,405),24,Color.red,volumes);Rect("coil sweep reservation",18,68,395,415,.8f,Color.red,volumes);
            Rect("boss front strike",50,70,348,380,.9f,Color.red,volumes);Rect("M11a folded mech footprint",36,48,346,362,.9f,Color.cyan,volumes);
            Circle("optional tank radius25",new Vector3(-100,.6f,230),25,Color.red,volumes);Label("OPTIONAL / G01",new Vector3(-100,3,255));
            Label("Z1 / DEPARTURE",new Vector3(0,3,50),2);Label("Z2 / STORAGE YARD",new Vector3(30,3,145),2);Label("Z3 / CRANE YARD",new Vector3(80,3,335),2);
            MakeCamera("00 Overview / free camera",new Vector3(-360,400,-215),new Vector3(15,12,250),55);
            MakeCamera("01 Top plan",new Vector3(20,850,255),new Vector3(20,0,255.01f),60,true,350);
            MakeCamera("02 Mech Z1 / M1",new Vector3(0,29.75f,0),new Vector3(0,24,8),70);
            MakeCamera("03 Mech Z2 / M5",new Vector3(0,29.75f,170),new Vector3(-7,32,205),70);
            MakeCamera("04 Mech Z3 / M10",new Vector3(60,29.75f,354),new Vector3(60,42,405),70);
            MakeCamera("05 Drone Z1 / D1",Map35Layout.Drone[3],new Vector3(8,26,8),65);
            MakeCamera("06 Drone Z2 / D5",Map35Layout.Drone[7],targets["T2 west nerve"],65);
            MakeCamera("07 Drone Z3 / D10",Map35Layout.Drone[11],targets["P02 crane latch"],65);
            var frustumRoot=Group("09 Control view frusta");
            for(int i=0;i<6;i++)
            {
                var c=MakeCamera("K "+Map35Layout.ControlNames[i],Map35Layout.Control[i],Map35Layout.Look[i],Map35Layout.VerticalFov[i]);
                Label(Map35Layout.ControlNames[i],c.transform.position+Vector3.up*4,1,Color.yellow);
                var corners=new Vector3[4];c.aspect=16f/10;c.CalculateFrustumCorners(new Rect(0,0,1,1),Vector3.Distance(c.transform.position,Map35Layout.Look[i])+10,Camera.MonoOrStereoscopicEye.Mono,corners);
                var wp=corners.Select(p=>c.transform.TransformPoint(p)).ToArray();Line("frustum perimeter "+i,wp,Color.yellow,.22f,frustumRoot,true);
                foreach(var p in wp)Line("frustum ray "+i,new[]{c.transform.position,p},Color.yellow,.18f,frustumRoot);
                if(i!=0&&i!=3&&i!=5)Cylinder("observation mast "+i,new Vector3(c.transform.position.x,c.transform.position.y*.5f,c.transform.position.z),.7f,c.transform.position.y,steel,details,false);
            }
            MakeCamera("14 Mech Z2 arrival / M4",new Vector3(0,29.75f,130),new Vector3(0,28,215),65);
            MakeCamera("15 Mech Z3 approach / M9",new Vector3(60,29.75f,340),new Vector3(60,35,405),65);
            // Overview compares snapshots; role views show only their phase, never a second mech/boss ahead.
            int mechLayers=(1<<8)|(1<<9)|(1<<10), bossLayers=(1<<11)|(1<<12);
            foreach(int i in new[]{2,3,4,14,15})cameraList[i].cullingMask&=~mechLayers;
            cameraList[2].cullingMask&=~bossLayers;
            foreach(int i in new[]{3,14})cameraList[i].cullingMask&=~(1<<12);
            foreach(int i in new[]{4,15})cameraList[i].cullingMask&=~(1<<11);
            foreach(int i in new[]{5,8,9})cameraList[i].cullingMask&=~((1<<9)|(1<<10)|bossLayers);
            foreach(int i in new[]{6,10,11})cameraList[i].cullingMask&=~((1<<8)|(1<<10)|(1<<12));
            foreach(int i in new[]{7,12,13})cameraList[i].cullingMask&=~((1<<8)|(1<<9)|(1<<11));
            viewer.views=cameraList.ToArray();viewer.routes=routes.gameObject;viewer.labels=labels.gameObject;viewer.frusta=frustumRoot.gameObject;viewer.reservations=volumes.gameObject;
            viewer.walker=Box("route tracing marker - not controller",Vector3.zero,new Vector3(12,.5f,16),teal,details,false).transform;viewer.walker.gameObject.SetActive(false);
            frustumRoot.gameObject.SetActive(false);volumes.gameObject.SetActive(false);
            var sun=new GameObject("Late afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(35,-35,0);
            RenderSettings.ambientLight=new Color(.52f,.58f,.64f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=700;RenderSettings.fogEndDistance=1700;RenderSettings.fogColor=new Color(.15f,.23f,.3f);
            viewer.Select(0);viewer.views[0].gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Physics.SyncTransforms();Validate();Capture();
            viewer.Select(0);labels.gameObject.SetActive(true);routes.gameObject.SetActive(true);viewer.SetLateState(false);EditorSceneManager.SaveScene(scene,ScenePath);
            Debug.Log("MAP35_GENERATED "+ScenePath);
        }
        static void Capture(string output=Output)
        {
            WarmSceneParticles();
            for(int i=0;i<viewer.views.Length;i++)
            {
                viewer.Select(i);labels.gameObject.SetActive(i<2);routes.gameObject.SetActive(i<2);var c=viewer.views[i];
                var rt=new RenderTexture(1600,1000,24);c.targetTexture=rt;c.aspect=1.6f;c.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
                var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
                File.WriteAllBytes(output+"/view-"+i.ToString("00")+".png",tex.EncodeToPNG());RenderTexture.active=prev;c.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        static void Validate()
        {
            var csv=new StringBuilder("camera,target,in_frustum,line_clear,blocker\n");
            var checks=new Dictionary<int,string[]>{[8]=new[]{"I1 push face"},[9]=new[]{"I1 control status","I1 push face"},[10]=new[]{"T1 front shell","T2 west nerve"},[11]=new[]{"T2 west nerve"},[12]=new[]{"I6 final weakpoint","Coil east edge"},[13]=new[]{"P02 crane latch","Coil east edge"},[5]=new[]{"I1 drone latch"},[6]=new[]{"T2 west nerve"},[7]=new[]{"P02 crane latch"}};
            foreach(var pair in checks)foreach(var id in pair.Value)
            {
                var c=viewer.views[pair.Key];c.aspect=1.6f;var p=targets[id];var viewport=c.WorldToViewportPoint(p);
                bool inside=viewport.z>0&&viewport.x>=0&&viewport.x<=1&&viewport.y>=0&&viewport.y<=1;
                var delta=p-c.transform.position;bool blocked=Physics.Raycast(c.transform.position,delta.normalized,out var hit,delta.magnitude-.2f);
                csv.AppendLine(c.name+","+id+","+inside+","+!blocked+","+(blocked?hit.collider.name:""));
            }
            var oldK1=new Vector3(-65,65,-20);var oldDelta=targets["I1 control status"]-oldK1;
            bool oldBlocked=Physics.Raycast(oldK1,oldDelta.normalized,out var oldHit,oldDelta.magnitude-.2f);
            csv.AppendLine("K1 original z=-20,I1 control status,not_checked,"+!oldBlocked+","+(oldBlocked?oldHit.collider.name:""));
            File.WriteAllText(Output+"/sightlines.csv",csv.ToString());
            var conflicts=new List<string>();int samples=0;
            for(int segment=1;segment<Map35Layout.Mech.Length;segment++)
            {
                Vector3 a=Map35Layout.Mech[segment-1],b=Map35Layout.Mech[segment];int n=Mathf.CeilToInt(Vector3.Distance(a,b)/2);
                for(int j=0;j<=n;j++)
                {
                    samples++;var p=Vector3.Lerp(a,b,(float)j/n)+Vector3.up*17.6f;
                    foreach(var c in Physics.OverlapBox(p,new Vector3(6,17.4f,8),Quaternion.LookRotation(b-a)))
                        if(c.transform.IsChildOf(structures)&&c.bounds.size.y>3)conflicts.Add("M segment "+segment+": "+c.name);
                }
            }
            var droneConflicts=new HashSet<string>();
            for(int i=1;i<Map35Layout.Drone.Length;i++)
            {
                var a=Map35Layout.Drone[i-1];var d=Map35Layout.Drone[i]-a;
                foreach(var h in Physics.SphereCastAll(a,1.5f,d.normalized,d.magnitude))
                    if(h.collider.transform.IsChildOf(structures))droneConflicts.Add("D segment "+i+": "+h.collider.name);
            }
            var summary="Mech polyline length m: "+Map35Layout.Length(Map35Layout.Mech).ToString("F2",Inv)+"\nMech structural samples: "+samples+"\nMech structure contacts: "+string.Join(";",conflicts.Distinct().DefaultIfEmpty("none"))+"\nDrone radius1.5 structure contacts: "+string.Join(";",droneConflicts.DefaultIfEmpty("none"))+"\n";
            summary+="Scope: blockout static geometry only. Mech tests ignore boss phase proxies, plate, debris and props. Drone checks include suspended coil. Turn rotation, real rigs, moving boss and gameplay are not certified. Camera tests are point rays; consult rendered images.\n";
            File.WriteAllText(Output+"/geometry-check.txt",summary);Debug.Log("MAP35_GEOMETRY "+summary);
            File.WriteAllText(Output+"/view-index.csv","index,name,x,y,z,vertical_fov,horizontal_fov_16_10\n"+string.Join("\n",viewer.views.Select((c,i)=>i+","+c.name+","+c.transform.position.x.ToString(Inv)+","+c.transform.position.y.ToString(Inv)+","+c.transform.position.z.ToString(Inv)+","+c.fieldOfView.ToString(Inv)+","+Camera.VerticalToHorizontalFieldOfView(c.fieldOfView,1.6f).ToString("F2",Inv))));
        }
        [MenuItem("Anseong/Map35/Generate and Build Viewer")]
        public static void Build()
        {
            Generate();Directory.CreateDirectory("../Builds/Map35");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="../Builds/Map35/AnseongMap35.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Map35 build failed");Debug.Log("MAP35_BUILD_OK");
        }
    }
}
