using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string WideScene="Assets/Map35/Scenes/Steelworks35m_WideRuins.unity";
        const string WideOutput="../docs/validation/map-wide";
        static Material agedSteel,agedConcrete,agedRust,agedYellow,agedRoof,asphalt,soot,fire;
        static Transform districts,nearDetails,ruins;
        static readonly Dictionary<Material,Material> wornMaterials=new Dictionary<Material,Material>();
        static readonly List<Transform> assetRoots=new List<Transform>();
        static readonly Vector3[] WideMech={new Vector3(0,0,-20),new Vector3(0,0,0),new Vector3(0,0,65),new Vector3(0,0,90),new Vector3(0,0,180),new Vector3(0,0,255),new Vector3(0,0,360),new Vector3(0,0,405),new Vector3(90,0,405),new Vector3(90,0,490),new Vector3(90,0,504),new Vector3(72,0,504),new Vector3(90,0,504)};
        static readonly Vector3[] WideDrone={new Vector3(12,30,-15),new Vector3(8,30,-8),new Vector3(8,30,8),new Vector3(16,30,8),new Vector3(35,32,65),new Vector3(-35,38,145),new Vector3(-45,38,245),new Vector3(-50,42,300),new Vector3(-30,42,405),new Vector3(90,48,435),new Vector3(165,54,510),new Vector3(165,54,555),new Vector3(165,54,615)};
        static readonly Vector3[] WideBoss=MapScenarioTransit.BossPoints;
        static Material Textured(string name,Texture image,Color tint,float gain,float metallic,float tile=4)
        {
            var m=Material("Wide_"+name,tint);m.shader=Shader.Find("Anseong/WorldWeathered");m.mainTexture=image;
            m.SetFloat("_Exposure",gain);m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",.23f);m.SetFloat("_TileMetres",tile);m.SetFloat("_Relief",.12f);m.enableInstancing=true;
            EditorUtility.SetDirty(m);return m;
        }
        static void WideMaterials()
        {
            FreePbrMaterials();
        }
        static Material Worn(Material source)
        {
            if(source==null)return agedSteel;if(wornMaterials.TryGetValue(source,out var value))return value;
            string n=source.name.ToLowerInvariant();Material chosen;
            if(n.Contains("glass"))return source;
            if(n.Contains("structural")||n.Contains("dark_steel")||n.Contains("frameandwindows")||n.Contains("windows_cables")||n.Contains("galvanizedsteel")||n=="steel"||n=="straps"||n.Contains("scrap_steel"))chosen=structuralSteel;
            else if(n.Contains("concrete")||n.Contains("cement")||n.Contains("plaster")||n=="weatheredgray"||n=="redfoundation")chosen=agedConcrete;
            else if(n=="openings"||n=="joints"||n=="sootinterior")chosen=agedRoof;
            else if(n=="weatheredseams"||n=="oxideedges")chosen=agedRust;
            else if(n.Contains("roof")||n.Contains("rubber")||n.Contains("black")||n.Contains("coal"))chosen=agedRoof;
            else if(n.Contains("yellow")||n.Contains("safety")||n.Contains("orange"))chosen=agedYellow;
            else if(n.Contains("rust")||n=="red"||n.StartsWith("red_")||n.EndsWith("_red")||n.Contains("_red_")||n.Contains("brick"))chosen=agedRust;
            else chosen=agedSteel;
            wornMaterials[source]=chosen;return chosen;
        }
        static void TextureHierarchy(Transform root)
        {
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var source=r.sharedMaterials;var filter=r.GetComponent<MeshFilter>();
                if(filter!=null&&filter.sharedMesh!=null&&source.Length>filter.sharedMesh.subMeshCount)source=source.Take(filter.sharedMesh.subMeshCount).ToArray();
                originalMaterialSlots[r]=source.Select(m=>m==null?"":m.name).ToArray();
                r.sharedMaterials=source.Select(Worn).ToArray();
            }
        }
        static Transform Existing(string name)=>structures.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith(name));
        static void RemoveChildren(Transform t){foreach(var c in t.Cast<Transform>().ToArray())Obj.DestroyImmediate(c.gameObject);}
        static void ResizeFloor(string name,float x0,float x1,float z0,float z1,Material m)
        {var old=structures.Find(name);if(old!=null)Obj.DestroyImmediate(old.gameObject);Floor(name,x0,x1,z0,z1,m);}
        static Transform Legacy(string relative,string id,Vector3 ground,float yaw,Transform parent,float scale=1,bool collider=false)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LegacyEnvironmentAudit.Root+"/Assets/"+relative+".prefab");
            if(prefab==null)throw new Exception("Missing reuse asset "+relative);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            go.name=id;var t=go.transform;t.SetParent(parent,true);t.localScale=Vector3.one*scale;t.rotation=Quaternion.Euler(0,yaw,0);
            var b=LegacyEnvironmentAudit.BoundsOf(go);t.position+=ground-new Vector3(b.center.x,b.min.y,b.center.z);
            LegacyEnvironmentAudit.StripSimulation(go);foreach(var c in go.GetComponentsInChildren<Collider>(true))Obj.DestroyImmediate(c);
            TextureHierarchy(t);
            if(collider){b=LegacyEnvironmentAudit.BoundsOf(go);var c=go.AddComponent<BoxCollider>();c.center=t.InverseTransformPoint(b.center);c.size=new Vector3(b.size.x/scale,b.size.y/scale,b.size.z/scale);}
            return t;
        }
        static Transform Replace(string prefix,string prefab,Vector3 ground,float scale=1)
        {var old=Existing(prefix);string name=old.name;Obj.DestroyImmediate(old.gameObject);var root=Legacy(prefab,name,ground,0,structures,scale,true);assetRoots.Add(root);return root;}
        static void Sign(string textValue,Vector3 p,Quaternion rotation,float size,Transform parent)
        {
            var textMesh=new GameObject("Stencil "+textValue.Replace('\n',' ')).AddComponent<TextMesh>();textMesh.transform.SetParent(parent);textMesh.transform.SetPositionAndRotation(p,rotation);
            textMesh.text=textValue;textMesh.characterSize=size;textMesh.fontSize=70;textMesh.anchor=TextAnchor.MiddleCenter;textMesh.alignment=TextAlignment.Center;textMesh.color=new Color(.82f,.76f,.58f);
        }
        static void Ladder(Vector3 bottom,float height,Transform parent)
        {
            Beam("ladder side",bottom+Vector3.left*.45f,bottom+Vector3.left*.45f+Vector3.up*height,.06f,agedSteel,parent);
            Beam("ladder side",bottom+Vector3.right*.45f,bottom+Vector3.right*.45f+Vector3.up*height,.06f,agedSteel,parent);
            for(float y=.3f;y<height;y+=.32f)Beam("ladder rung",bottom+new Vector3(-.45f,y,0),bottom+new Vector3(.45f,y,0),.045f,agedYellow,parent);
        }
        static void DetailPanel(Vector3 p,float width,float height,Transform parent,string stencil)
        {
            Box("service backing",p,new Vector3(width,height,.12f),agedSteel,parent,false);
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
                Shape("rivet",PrimitiveType.Sphere,p+new Vector3(x*(width/2-.12f),y*(height/2-.12f),-.09f),Vector3.one*.1f,agedRust,parent,false);
            for(int i=0;i<6;i++)Box("vent slit",p+new Vector3(0,-height*.2f+i*.11f,-.08f),new Vector3(width*.65f,.035f,.025f),agedRoof,parent,false);
            Sign(stencil,p+new Vector3(0,height*.28f,-.09f),Quaternion.identity,.045f,parent);
        }
        static void Rubble(Vector3 center,float radius,int count,int seed,Transform parent)
        {
            var rng=new System.Random(seed);
            for(int i=0;i<count;i++)
            {
                float a=(float)rng.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)rng.NextDouble())*radius;
                var p=center+new Vector3(Mathf.Cos(a)*r,.35f+(float)rng.NextDouble(),Mathf.Sin(a)*r);
                bool girder=i%4==0;var g=Box(girder?"torn girder":"fractured slab",p,girder?new Vector3(.5f,.6f,4+(float)rng.NextDouble()*6):new Vector3(1+(float)rng.NextDouble()*4,.4f,1+(float)rng.NextDouble()*3),girder?agedRust:agedConcrete,parent,false);
                g.transform.rotation=Quaternion.Euler((float)rng.NextDouble()*25,(float)rng.NextDouble()*360,(float)rng.NextDouble()*22);
            }
            Cylinder("burn footprint",center+Vector3.up*.12f,radius*.9f,.04f,soot,parent,false);
        }
        static void Smoke(Vector3 p,Transform parent,int seed)
        {
            var g=new GameObject("Fire aftermath smoke - visual only");g.transform.SetParent(parent);g.transform.position=p;
            var ps=g.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=18;main.loop=true;main.startLifetime=18;main.startSpeed=3;main.startSize=new ParticleSystem.MinMaxCurve(9,15);main.startColor=new Color(.22f,.23f,.24f,.6f);main.maxParticles=40;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.rateOverTime=1.4f;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=2;shape.rotation=new Vector3(-90,0,0);
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,2.2f));
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.x=1.2f;velocity.y=0;velocity.z=.4f;
            var colour=ps.colorOverLifetime;colour.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.65f,.7f,.75f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.12f),new GradientAlphaKey(0,1)});colour.color=gradient;
            var m=Material("Wide_soft_smoke",new Color(.25f,.26f,.27f,.5f));m.shader=Shader.Find("Anseong/SoftIndustrialSmoke");m.color=new Color(.7f,.72f,.74f,.65f);
            var renderer=g.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=m;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(seed+1);ps.Simulate(15,true,true);ps.Pause(true);
            var flames=new GameObject("Rising industrial flames");flames.transform.SetParent(parent);flames.transform.position=p;
            var fps=flames.AddComponent<ParticleSystem>();fps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var fm=fps.main;fm.duration=4;fm.loop=true;fm.startLifetime=1.6f;fm.startSpeed=1.5f;fm.startSize=new ParticleSystem.MinMaxCurve(4,7);fm.maxParticles=32;fm.startColor=Color.white;
            var fe=fps.emission;fe.rateOverTime=12;var fs=fps.shape;fs.shapeType=ParticleSystemShapeType.Cone;fs.angle=10;fs.radius=3;fs.rotation=new Vector3(-90,0,0);
            var flameMat=Material("Wide_flame",Color.white);flameMat.shader=Shader.Find("Anseong/IndustrialFlame");flameMat.SetFloat("_Intensity",1.7f);flames.GetComponent<ParticleSystemRenderer>().sharedMaterial=flameMat;
            fps.useAutoRandomSeed=false;fps.randomSeed=(uint)(seed+100);fps.Simulate(3,true,true);fps.Pause(true);
            if(seed==3 || seed==9){var lamp=new GameObject("fire bounce").AddComponent<Light>();lamp.transform.SetParent(parent);lamp.transform.position=p+Vector3.up*3;lamp.color=new Color(1,.3f,.06f);lamp.intensity=2.5f;lamp.range=18;lamp.shadows=LightShadows.None;}
            for(int i=0;i<4;i++)Box("glowing debris",p+new Vector3(i*1.3f-2,.6f,Mathf.Sin(i)*2),new Vector3(1.8f,.7f,1),fire,parent,false);
        }
        static void DamageWarehouse(Transform root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>().ToArray())
            {
                if(t==root)continue;
                bool breach=(t.name.Contains("EndWall") && t.position.z<root.position.z-8 && t.position.x>root.position.x-5) || (t.name.Contains("SideWall") && t.position.x>root.position.x+15 && t.position.z<root.position.z+5);
                if(breach || (t.name.Contains("RoofPanel") && t.position.x>root.position.x-4))
                    t.gameObject.SetActive(false);
            }
            // B02 debris is placed beneath its failed roof by BuildMaintenanceYard.
            for(int i=0;i<4;i++)Beam("fallen roof truss",new Vector3(-115,2+i,37+i*6),new Vector3(-80,5+i,44+i*6),.65f,agedRust,ruins);
            Smoke(new Vector3(-105,0,47),ruins,3);
        }
        static void RepositionMap()
        {
            var soil=Textured("ash_soil",agedConcrete.mainTexture,new Color(.42f,.37f,.3f),1,0,7);
            ResizeFloor("site ground",-500,430,-200,900,soil);
            var oldRiver=structures.Find("river surface");if(oldRiver!=null)Obj.DestroyImmediate(oldRiver.gameObject);
            ResizeFloor("Z1 departure 80x70",-60,60,0,100,agedConcrete);structures.Find("Z1 departure 80x70").name="Z1 departure 120x100";
            ResizeFloor("Z2 storage 150x120",-110,110,180,360,agedConcrete);structures.Find("Z2 storage 150x120").name="Z2 storage 220x180";
            ResizeFloor("Z3 crane yard 160x140",-30,210,450,650,agedConcrete);structures.Find("Z3 crane yard 160x140").name="Z3 crane yard 240x200";
            ResizeFloor("24m connector 1",-16,16,100,180,asphalt);
            ResizeFloor("connector 2 south",-16,16,360,405,asphalt);ResizeFloor("connector 2 east",-16,106,389,421,asphalt);ResizeFloor("connector 2 north",74,106,405,490,asphalt);
            ResizeFloor("boss east transport 60m",135,215,280,510,asphalt);structures.Find("boss east transport 60m").name="boss east transport 80m";
            ResizeFloor("boss south alignment apron",40,225,390,510,agedConcrete);
            foreach(var t in details.Cast<Transform>().ToArray())
            {
                if(t.name.Contains("existing damage")){Obj.DestroyImmediate(t.gameObject);continue;}
                if(t.name=="route tracing marker - not controller")continue;
                if(t.position.z>=320)t.position+=new Vector3(30,0,150);
                else if(t.position.z>=130)t.position+=new Vector3(0,0,85);
            }
            foreach(var t in proxies.Cast<Transform>())
                if(t.name.Contains("Z2"))t.position+=new Vector3(0,0,85);else if(t.name.Contains("Z3"))t.position+=new Vector3(30,0,150);
            Existing("B07").position+=new Vector3(30,0,150);
            var b02=Replace("B02","Warehouse/Warehouse",new Vector3(-95,0,45));DamageWarehouse(b02);
            Replace("B03","IndustrialBuildings/UtilityPumpStation",new Vector3(90,0,45),.75f);
            Replace("B04","IndustrialBuildings/SteelmakingHall",new Vector3(85,0,145));
            Replace("B05","IndustrialBuildings/RollingMill",new Vector3(-155,0,235));
            Replace("B06","StorageSilo/StorageSilo",new Vector3(-155,0,340));
            Replace("B08","IndustrialBuildings/BlastFurnaceUnit",new Vector3(100,0,715),90f/70);
            Replace("B09","IndustrialBuildings/GasCleaningStation",new Vector3(-110,0,555));
            assetRoots.Add(Existing("B01"));assetRoots.Add(Existing("B07"));
            TextureHierarchy(Existing("B01"));TextureHierarchy(Existing("B07"));
        }
        static void LandscapeAndDistricts()
        {
            RemoveChildren(nature);
            var ocean=Material("Wide_storm_sea",new Color(.045f,.12f,.15f));ocean.shader=Shader.Find("Anseong/StormCoast");ocean.SetFloat("_ShoreX",430);
            Box("open sea to horizon",new Vector3(5430,-1.1f,1000),new Vector3(10000,1,12000),ocean,nature,false);
            Box("coastal embankment",new Vector3(426,-.2f,350),new Vector3(9,3,1100),agedConcrete,nature);
            for(int i=0;i<4;i++)Legacy("HarborCrane/HarborCrane","HARBOR crane "+i,new Vector3(378,0,80+i*170),90,districts);
            string[] terrainNames={"MountainLayer0","MountainLayer1","MountainLayer2"};
            for(int i=0;i<3;i++)
            {
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(LegacyEnvironmentAudit.Root+"/DistantMountainTerrain/"+terrainNames[i]+".asset");
                if(source==null)continue;
                var g=new GameObject("Reused mountain band "+i);g.transform.SetParent(nature);g.AddComponent<MeshFilter>().sharedMesh=source;g.AddComponent<MeshRenderer>().sharedMaterial=Material("Wide_ash_hinterland",new Color(.24f,.25f,.23f));
                var b=source.bounds;var desired=new Vector3(2500,90+i*25,550);g.transform.localScale=new Vector3(desired.x/b.size.x,desired.y/b.size.y,desired.z/b.size.z);
                g.transform.position=new Vector3(0,0,1050+i*220)-Vector3.Scale(new Vector3(b.center.x,b.min.y,b.center.z),g.transform.localScale);
            }
            // West mountain uses the same reusable mesh; no new terrain generation is required.
            var west=Obj.Instantiate(nature.Find("Reused mountain band 0").gameObject,nature);west.name="Reused west mountain band";west.transform.RotateAround(Vector3.zero,Vector3.up,90);west.transform.position+=new Vector3(-1800,0,500);
            string[] facilities={"IndustrialBuildings/SteelmakingHall","IndustrialBuildings/RollingMill","IndustrialBuildings/GasCleaningStation","IndustrialBuildings/BlastFurnaceUnit","IndustrialBuildings/UtilityPumpStation"};
            for(int row=0;row<5;row++)for(int col=0;col<2;col++)
            {
                int index=row*2+col;var p=new Vector3(-290-col*140,0,80+row*155);
                Legacy(facilities[index%facilities.Length],"WEST district "+index,p,index%2==0?0:90,districts);
                if(index%3==1){Rubble(p+new Vector3(30,0,-35),15,14,index+80,ruins);Smoke(p+new Vector3(10,5,10),ruins,index);}
            }
            for(int i=0;i<5;i++)Legacy(facilities[(i+1)%5],"EAST district "+i,new Vector3(295,0,40+i*155),90,districts,.8f);
            for(int i=0;i<3;i++)Legacy("Smokestack/Smokestack","West damaged chimney "+i,new Vector3(-220,0,160+i*210),0,districts,.8f);
            Floor("west district service road",-245,-225,-100,825,asphalt);Floor("coastal service road",335,359,-100,825,asphalt);
            for(float z=-70;z<825;z+=14)
            {
                Box("district lane dash",new Vector3(-235,.09f,z),new Vector3(.3f,.025f,5),agedYellow,details,false);
                Box("coastal lane dash",new Vector3(347,.09f,z),new Vector3(.3f,.025f,5),agedYellow,details,false);
            }
        }
        static void NineSectors()
        {
            // A: maintenance gate / scorched service bay / evacuation loading edge.
            var gate=Group("Z1A gate workshop details",Existing("B01"));
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<7;i++)Box("door recessed rib",new Vector3(side*15,4+i*5,-1.12f),new Vector3(3,.15f,.28f),agedYellow,gate,false);
                Ladder(new Vector3(side*18,.5f,-1.25f),32,gate);
                DetailPanel(new Vector3(side*22,25,-1.2f),3,4,gate,"A-01\nSERVICE");
                for(int j=0;j<6;j++)DetailPanel(new Vector3(side*22,5+j*3.2f,-1.2f),2,2,gate,"35 / REPAIR");
            }
            Sign("ANSEONG STEEL\nEMERGENCY DEPLOYMENT",new Vector3(0,44, .15f),Quaternion.Euler(0,180,0),.42f,gate);
            var bay=Group("Z1B ruptured wall service details",Existing("B02"));DetailPanel(new Vector3(-73,11,27),3,4,bay,"B-02\nPOWER OFF");Ladder(new Vector3(-72,0,27),17,bay);
            Legacy("SteelTruck/SteelTruck","Z1B abandoned repair carrier",new Vector3(-64,0,58),22,ruins);
            for(int i=0;i<5;i++)Legacy("ShippingContainer/ShippingContainer","Z1C evacuation container "+i,new Vector3(63+i*8,0,82),0,districts);
            Legacy("ContainerYardEquipment/ReachStacker","Z1C abandoned reach stacker",new Vector3(86,0,99),-35,ruins);
            for(int i=0;i<7;i++)Legacy("ContainerYardEquipment/ConcreteSafetyBarrier","evacuation barrier "+i,new Vector3(62+i*4,0,70),0,nearDetails);
            Sign("EVACUATION →",new Vector3(88,4,72),Quaternion.identity,.5f,nearDetails);
            // B: cargo ruins / boss breach / gas and valve alley.
            for(int i=0;i<6;i++)
            {
                var cargo=Legacy("ShippingContainer/ShippingContainer","Z2A crushed container "+i,new Vector3(-65+(i%2)*10,0,205+(i/2)*12),20+i*17,ruins);
                if(i%2==0)cargo.Rotate(Vector3.forward,13);Legacy("SteelCargo/SteelBeamBundle","Z2A displaced beams "+i,new Vector3(-90+i*5,0,220),30,ruins);
            }
            Rubble(new Vector3(85,0,220),18,26,42,ruins);Smoke(new Vector3(90,0,245),ruins,9);
            var pipeBay=Group("Z2C maintenance valve platform",nearDetails);
            Box("maintenance deck",new Vector3(-142,21,332),new Vector3(12,.3f,4),agedSteel,pipeBay,false);
            for(int i=0;i<3;i++)
            {
                Beam("exposed process pipe",new Vector3(-165,10+i*3,323),new Vector3(-130,10+i*3,323),1.1f,agedRust,pipeBay);
                DetailPanel(new Vector3(-143+i*3,23,329),2.2f,2.5f,pipeBay,"V-0"+(i+1));
            }
            Ladder(new Vector3(-137,0,329),21,pipeBay);
            // C: failed pipe bridge / crane service gantry / defended furnace edge.
            var broken=Group("Z3A broken pipe bridge",ruins);
            Beam("standing bridge leg",new Vector3(137,0,459),new Vector3(137,23,459),1.1f,agedSteel,broken);
            Beam("snapped bridge truss",new Vector3(137,23,459),new Vector3(160,3,474),1,agedRust,broken);
            for(int i=0;i<3;i++)Beam("ruptured pipe",new Vector3(137+i*2,22,459),new Vector3(160+i*2,2,480),.8f,agedSteel,broken);
            Rubble(new Vector3(155,0,475),12,18,91,broken);
            broken.position+=Vector3.right*100; // Keep the entire broken bridge outside the boss turning apron.
            var crane=Existing("B07");var service=Group("Z3B close inspection service deck",crane);
            Box("maintenance walkway",new Vector3(144,46,555),new Vector3(6,.25f,13),agedSteel,service,false);
            foreach(float z in new[]{549f,561f})Beam("platform handrail",new Vector3(141,47.1f,z),new Vector3(147,47.1f,z),.07f,agedYellow,service);
            Ladder(new Vector3(145,35,549),11,service);DetailPanel(new Vector3(143,48,549),2,2.4f,service,"C-07\nRELEASE");
            for(int i=0;i<12;i++)Box("walkway grate",new Vector3(144,46.15f,549+i),new Vector3(6,.03f,.08f),agedRoof,service,false);
            DetailPanel(new Vector3(143,48,554),2.3f,2.6f,service,"LOCK / 07");
            for(int i=0;i<8;i++)Legacy("ContainerYardEquipment/ConcreteSafetyBarrier","Z3C last defensive line "+i,new Vector3(25+i*4,0,647),0,nearDetails);
            Legacy("ScrapDumpTruck/ScrapDumpTruck","Z3C burned supply truck",new Vector3(12,0,635),-30,ruins);
            for(int i=0;i<4;i++)Legacy("ContainerYardEquipment/YardPowerCabinet","Z3C reserve power "+i,new Vector3(155+i*4,0,647),0,nearDetails);
            Sign("FURNACE 08\nKEEP RUNNING",new Vector3(100,12,662),Quaternion.identity,.35f,nearDetails);
            foreach(var building in assetRoots)
            {
                var b=LegacyEnvironmentAudit.BoundsOf(building.gameObject);float z=b.min.z-.2f;
                for(int i=0;i<3;i++)DetailPanel(new Vector3(b.center.x-3+i*3,Mathf.Min(8,b.size.y*.5f),z),2,3,building,"SERVICE "+(i+1));
            }
        }
        static void RoutesAndViews()
        {
            RemoveChildren(routes);RemoveChildren(labels);RemoveChildren(volumes);targets.Clear();
            Route("Mech route - 650m",WideMech,Map35Layout.MechNames,new Color(.1f,1,.9f),true);
            Route("Drone main route",WideDrone,Map35Layout.DroneNames,new Color(1,.65f,.1f),false);
            Line("Boss route",WideBoss.Select(p=>p+Vector3.up*.8f),new Color(.8f,.35f,1),1.3f,routes);
            Line("Drone upper optional bypass",new[]{WideDrone[7],new Vector3(-50,57,300),new Vector3(0,57,330),WideDrone[8]},new Color(1,.85f,.4f),.35f,routes);
            Rect("32m connector",-16,16,100,180,.65f,Color.cyan,volumes);Circle("M7 rotation pad",new Vector3(0,.7f,405),24,Color.cyan,volumes);Circle("M8 rotation pad",new Vector3(90,.7f,405),24,Color.cyan,volumes);
            Rect("boss road",135,215,280,510,.6f,Color.magenta,volumes);Rect("boss alignment",40,225,390,510,.6f,Color.magenta,volumes);
            Circle("Q1 fall radius",new Vector3(90,.8f,555),24,Color.red,volumes);Rect("coil settlement sweep",48,98,545,565,.8f,Color.red,volumes);
            Rect("boss front strike",80,100,498,530,.9f,Color.red,volumes);Rect("M11a safe folded footprint",66,78,496,512,.9f,Color.cyan,volumes);
            Circle("optional silo blast",new Vector3(-155,.8f,340),25,Color.red,volumes);
            // Existing target meshes were translated with the site. The exact points are listed explicitly for repeatable checks.
            targets["I1 status"]=new Vector3(-5.4f,26,8);targets["I1 push"]=new Vector3(0,24,7.3f);targets["I1 latch"]=new Vector3(8,26,8);
            targets["T1 shell"]=new Vector3(-12,32,270);targets["T2 nerve"]=new Vector3(-20,34,283);targets["P02 latch"]=new Vector3(140,48,555);targets["Coil edge"]=new Vector3(96.4f,52,555);targets["I6 neck"]=new Vector3(90,24,512);
            Label("Z1 / EMERGENCY DEPLOYMENT",new Vector3(0,5,75),2.3f);Label("Z2 / BREACHED STORAGE",new Vector3(25,5,200),2.3f);Label("Z3 / LAST FURNACE",new Vector3(110,5,475),2.3f);
            foreach(var root in assetRoots){var b=LegacyEnvironmentAudit.BoundsOf(root.gameObject);Label(root.name.Split(' ')[0],new Vector3(b.center.x,b.max.y+6,b.center.z),1.3f);}
            SetCamera(0,new Vector3(-640,640,-410),new Vector3(10,20,360),57);SetCamera(1,new Vector3(-20,1200,360),new Vector3(-20,0,360.01f),60);viewer.views[1].orthographicSize=600;
            SetCamera(2,new Vector3(0,29.75f,0),new Vector3(0,24,8),70);SetCamera(3,new Vector3(0,29.75f,255),new Vector3(-7,32,290),70);
            SetCamera(4,new Vector3(90,29.75f,504),new Vector3(90,42,555),70);
            SetCamera(5,WideDrone[3],targets["I1 latch"],65);SetCamera(6,WideDrone[7],targets["T2 nerve"],65);SetCamera(7,WideDrone[11],targets["P02 latch"],65);
            SetCamera(8,new Vector3(-18,32,-35),new Vector3(0,24,-5),60);SetCamera(9,new Vector3(-95,80,95),new Vector3(0,18,40),60);
            SetCamera(10,new Vector3(-180,125,165),new Vector3(0,25,285),60);SetCamera(11,new Vector3(-90,55,300),targets["T2 nerve"],50);
            SetCamera(12,new Vector3(-130,145,410),new Vector3(90,25,545),60);SetCamera(13,new Vector3(175,60,555),new Vector3(90,30,555),55);
            SetCamera(14,new Vector3(0,29.75f,180),new Vector3(0,28,300),65);SetCamera(15,new Vector3(90,29.75f,490),new Vector3(90,35,555),65);
            foreach(var m in details.Cast<Transform>().Where(t=>t.name.StartsWith("observation mast")).ToArray())Obj.DestroyImmediate(m.gameObject);
            RemoveChildren(viewer.frusta.transform);
            for(int i=8;i<14;i++)
            {
                var c=viewer.views[i];var corners=new Vector3[4];c.aspect=1.6f;c.CalculateFrustumCorners(new Rect(0,0,1,1),150,Camera.MonoOrStereoscopicEye.Mono,corners);
                var wp=corners.Select(v=>c.transform.TransformPoint(v)).ToArray();Line("view cone",wp,Color.yellow,.25f,viewer.frusta.transform,true);
                foreach(var p in wp)Line("camera edge",new[]{c.transform.position,p},Color.yellow,.2f,viewer.frusta.transform);
            }
            Vector3[] closePositions={new Vector3(10,27,3),new Vector3(-66,16,16),new Vector3(88,7,65),new Vector3(-55,6,195),new Vector3(-110,15,215),new Vector3(-126,25,319),new Vector3(228,18,442),new Vector3(151,49,544),new Vector3(170,28,665)};
            Vector3[] closeTargets={new Vector3(0,24,8),new Vector3(-95,17,45),new Vector3(95,3,82),new Vector3(-65,3,216),new Vector3(-145,14,235),new Vector3(-144,23,330),new Vector3(250,15,468),new Vector3(143,48,552),new Vector3(100,44,715)};
            for(int i=0;i<9;i++)
            {var camera=MakeCamera("DETAIL Z"+(i/3+1)+(char)('A'+i%3)+"-1",closePositions[i],closeTargets[i],60);camera.cullingMask&=~((1<<8)|(1<<9)|(1<<10)|(1<<11)|(1<<12));}
            Vector3[] secondPositions={new Vector3(-10,12,-13),new Vector3(-70,37,60),new Vector3(101,7,102),new Vector3(-90,8,250),new Vector3(70,12,197),new Vector3(-169,16,321),new Vector3(267,8,487),new Vector3(146,48,565),new Vector3(171,7,635)};
            Vector3[] secondTargets={new Vector3(-23,12,-35),new Vector3(-95,12,45),new Vector3(88,4,99),new Vector3(-78,3,231),new Vector3(87,4,220),new Vector3(-155,14,334),new Vector3(248,6,470),new Vector3(142,48,555),new Vector3(163,3,647)};
            for(int i=0;i<9;i++){var c=MakeCamera("DETAIL Z"+(i/3+1)+(char)('A'+i%3)+"-2",secondPositions[i],secondTargets[i],60);c.cullingMask&=~((1<<8)|(1<<9)|(1<<10)|(1<<11)|(1<<12));}
            MakeCamera("COAST / open sea",new Vector3(310,55,480),new Vector3(630,10,650),65);
            MakeCamera("DETAIL PC drone / maintenance equipment",new Vector3(-69,2.2f,30),new Vector3(-74,.9f,35),60);
            MakeCamera("DETAIL PC drone / natural shoreline",new Vector3(430,6,801),new Vector3(454,0,812),60);
            MakeCamera("DETAIL PC drone / connected maintenance bay",new Vector3(-66,4.6f,33),new Vector3(-78,1.8f,36),65);
            MakeCamera("DETAIL PC drone / B02 painted and corroded cladding",new Vector3(-125,9,40.5f),new Vector3(-119.3f,8.5f,42),55);
            viewer.views=cameraList.ToArray();viewer.mechRoute=WideMech;viewer.sceneTitle="안성제철 / 넓은 폐허 맵\n35m · 3구역 · 18 디테일셀";
        }
        static void SetCamera(int index,Vector3 position,Vector3 look,float fov)
        {var c=viewer.views[index];c.transform.SetPositionAndRotation(position,Quaternion.LookRotation(look-position));c.fieldOfView=fov;c.farClipPlane=4000;}
        static void SaveBuildingAssets()
        {
            Directory.CreateDirectory(Base+"Prefabs/WideBuildings");
            var report=new StringBuilder("id,prefab,width,height,depth,renderers,material_count\n");
            foreach(var root in assetRoots)
            {
                // Imported combined buildings store their mesh on the root itself. Recentring only
                // children would otherwise shift the original envelope away from its new details.
                var rootFilter=root.GetComponent<MeshFilter>();var rootRenderer=root.GetComponent<MeshRenderer>();
                if(rootFilter!=null&&rootRenderer!=null)
                {
                    var envelope=new GameObject("Source authored envelope");envelope.transform.SetParent(root,false);envelope.layer=root.gameObject.layer;
                    envelope.AddComponent<MeshFilter>().sharedMesh=rootFilter.sharedMesh;
                    var copiedRenderer=envelope.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(rootRenderer,copiedRenderer);
                    foreach(var collider in root.GetComponents<Collider>())
                    {var copy=envelope.AddComponent(collider.GetType());EditorUtility.CopySerialized(collider,copy);Obj.DestroyImmediate(collider);}
                    Obj.DestroyImmediate(rootRenderer);Obj.DestroyImmediate(rootFilter);
                }
                string id=root.name.Split(' ')[0];var children=root.Cast<Transform>().ToArray();var b=LegacyEnvironmentAudit.BoundsOf(root.gameObject);
                foreach(var child in children)child.SetParent(null,true);root.position=new Vector3(b.center.x,0,b.center.z);foreach(var child in children)child.SetParent(root,true);
                BuildBuildingLod(root,id);
                Vector3 position=root.position;root.position=Vector3.zero;string path=Base+"Prefabs/WideBuildings/"+id+"_Textured.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,path,InteractionMode.AutomatedAction);root.position=position;
                var renderers=root.GetComponentsInChildren<MeshRenderer>(true);report.AppendLine(id+","+path+","+b.size.x+","+b.size.y+","+b.size.z+","+renderers.Length+","+renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count());
            }
            File.WriteAllText(WideOutput+"/building-assets.csv",report.ToString());
        }
        static void BuildBuildingLod(Transform root,string id)
        {
            bool moving(Transform t)=>(viewer.coil!=null&&t.IsChildOf(viewer.coil))||(viewer.lateCoil!=null&&t.IsChildOf(viewer.lateCoil.transform))||(viewer.plate!=null&&t.IsChildOf(viewer.plate));
            var close=root.GetComponentsInChildren<Renderer>().Where(r=>r.gameObject.activeInHierarchy&&r.enabled&&!moving(r.transform)).ToArray();
            var buckets=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null || !filter.gameObject.activeInHierarchy || moving(filter.transform))continue;
                string n=filter.name.ToLowerInvariant();if(n.Contains("rivet")||n.Contains("vent slit")||n.Contains("ladder rung")||n.Contains("walkway grate"))continue;
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled)continue;
                for(int i=0;i<filter.sharedMesh.subMeshCount && i<renderer.sharedMaterials.Length;i++)
                {var m=renderer.sharedMaterials[i];if(m==null)continue;if(!buckets.ContainsKey(m))buckets[m]=new List<CombineInstance>();buckets[m].Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=i,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});}
            }
            var far=Group("LOD1 combined structure",root);
            // Meshes were combined in root-local space; do not preserve the new
            // group's world origin or compensate for the root's scale a second time.
            far.localPosition=Vector3.zero;far.localRotation=Quaternion.identity;far.localScale=Vector3.one;
            var farRenderers=new List<Renderer>();int index=0;
            foreach(var pair in buckets)
            {
                var temp=new Mesh{name=id+" coarse material "+index,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};temp.CombineMeshes(pair.Value.ToArray(),true,true);temp.RecalculateBounds();
                string path=Base+"Meshes/Wide_"+id+"_LOD1_"+index+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(temp,path);saved=temp;}else{EditorUtility.CopySerialized(temp,saved);Obj.DestroyImmediate(temp);EditorUtility.SetDirty(saved);}
                var g=new GameObject("merged structure "+index++);g.transform.SetParent(far,false);g.AddComponent<MeshFilter>().sharedMesh=saved;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=pair.Key;farRenderers.Add(r);
            }
            var lod=root.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.18f,close),new LOD(.012f,farRenderers.ToArray())});lod.RecalculateBounds();
        }
        static void MoreDetailCells()
        {
            var interior=Group("Z1A-2 field repair bay",Existing("B01"));
            for(int i=0;i<3;i++){Box("repair platform",new Vector3(-23,9+i*4,-35),new Vector3(5,.25f,10),agedSteel,interior,false);DetailPanel(new Vector3(-22,11+i*4,-40),2.5f,2.5f,interior,"PWR / "+i);}
            Ladder(new Vector3(-21,0,-39),21,interior);Sign("FIELD REPAIR",new Vector3(-23,23,-40),Quaternion.identity,.15f,interior);
            var trailer=Legacy("SteelTruck/SteelTruck","Z2A-2 abandoned steel convoy",new Vector3(-78,0,231),72,ruins);trailer.Rotate(Vector3.forward,22);
            for(int i=0;i<6;i++)Legacy("SteelCargo/SteelSlabStack","Z2A-2 spilled plate load "+i,new Vector3(-90+i*4,0,243),i*12,ruins);
            Rubble(new Vector3(88,0,216),7,20,57,ruins);
            for(int i=0;i<3;i++)DetailPanel(new Vector3(-155,11+i*2.8f,332),2,2.3f,nearDetails,"PRESSURE "+i);
            for(int i=0;i<9;i++)Legacy("StreetLight/StreetLight","broken service lamp "+i,new Vector3(333,0,20+i*80),90,districts);
            for(int i=0;i<4;i++)
            {
                float z=90+i*180;Floor("industrial cross road "+i,-225,335,z-5,z+5,asphalt);
                for(float x=-220;x<335;x+=18)Box("road centre dash",new Vector3(x,.09f,z),new Vector3(6,.02f,.3f),agedYellow,details,false);
            }
            // The exposed quay and broken breakwater make the water read as a sea rather than a canal.
            for(int i=0;i<14;i++)Legacy("Quay/Quay_20m","coastal quay module "+i,new Vector3(422,-3,20+i*40),0,districts);
            for(int i=0;i<8;i++)Rubble(new Vector3(414,0,60+i*80),5,5,130+i,ruins);
        }
        static void ValidateWide()
        {
            foreach(var camera in viewer.views)camera.aspect=1.6f;
            Physics.SyncTransforms();var issues=new HashSet<string>();int count=0;
            for(int i=1;i<WideMech.Length;i++)
            {
                var a=WideMech[i-1];var b=WideMech[i];int n=Mathf.CeilToInt(Vector3.Distance(a,b)/2);
                for(int j=0;j<=n;j++){count++;foreach(var c in Physics.OverlapBox(Vector3.Lerp(a,b,(float)j/n)+Vector3.up*17.6f,new Vector3(6,17.4f,8),Quaternion.LookRotation(b-a)))
                    if(c.transform.IsChildOf(structures)&&c.bounds.size.y>3)issues.Add(c.name);}
            }
            var droneIssues=new HashSet<string>();
            for(int i=1;i<WideDrone.Length;i++){var d=WideDrone[i]-WideDrone[i-1];foreach(var h in Physics.SphereCastAll(WideDrone[i-1],1.5f,d.normalized,d.magnitude))
                if(h.collider.transform.IsChildOf(structures))droneIssues.Add(h.collider.name);}
            var csv=new StringBuilder("camera,target,in_frustum,line_clear,blocker\n");
            var pairs=new Dictionary<int,string[]>{{8,new[]{"I1 push"}},{9,new[]{"I1 status"}},{10,new[]{"T1 shell","T2 nerve"}},{11,new[]{"T2 nerve"}},{12,new[]{"I6 neck","Coil edge"}},{13,new[]{"P02 latch","Coil edge"}},{5,new[]{"I1 latch"}},{6,new[]{"T2 nerve"}},{7,new[]{"P02 latch"}}};
            foreach(var pair in pairs)foreach(var id in pair.Value)
            {var c=viewer.views[pair.Key];var p=targets[id];var v=c.WorldToViewportPoint(p);var d=p-c.transform.position;bool hit=Physics.Raycast(c.transform.position,d.normalized,out var h,d.magnitude-.2f);csv.AppendLine(c.name+","+id+","+(v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1)+","+!hit+","+(hit?h.collider.name:""));}
            File.WriteAllText(WideOutput+"/sightlines.csv",csv.ToString());
            File.WriteAllText(WideOutput+"/geometry-check.txt","Mech route metres: "+Map35Layout.Length(WideMech)+"\nSamples: "+count+"\nMech building contacts: "+string.Join(";",issues.DefaultIfEmpty("none"))+"\nDrone building contacts: "+string.Join(";",droneIssues.DefaultIfEmpty("none"))+"\nStatic blockout only; excludes moving boss, unfold/turn, and decorative non-collision rubble.\n");
            File.WriteAllText(WideOutput+"/view-index.csv","index,name,x,y,z\n"+string.Join("\n",viewer.views.Select((c,i)=>i+","+c.name+","+c.transform.position.x+","+c.transform.position.y+","+c.transform.position.z)));
            File.WriteAllText(WideOutput+"/routes.csv","role,index,x,y,z\n"+string.Join("\n",new[]{("mech",WideMech),("drone",WideDrone),("boss",WideBoss)}.SelectMany(a=>a.Item2.Select((p,i)=>a.Item1+","+i+","+p.x+","+p.y+","+p.z))));
        }
        [MenuItem("Anseong/Map35/Generate Wide Ruins With Existing Assets")]
        public static void GenerateWide()
        {
            Generate();Directory.CreateDirectory(WideOutput);LegacyEnvironmentAudit.AdaptCopiedMaterials();wornMaterials.Clear();originalMaterialSlots.Clear();assetRoots.Clear();WideMaterials();
            districts=Group("10 Reused industrial districts");nearDetails=Group("11 Nine-sector close details");ruins=Group("12 Invasion damage and wreckage");
            RepositionMap();TextureHierarchy(details);LandscapeAndDistricts();NineSectors();MoreDetailCells();RealismDetails();
            UpgradeArchitecture();UpgradeLandscapeAndGround();PlaceAcquiredModels();RefineSurfaceScale();ApplyIndustrialPalette();BuildMaintenanceYard();
            CompleteLandscapeIntegration();CompleteIndustrialStructures();CompletePlayerFacingMap();CompleteSurfaceAndDamage();
            RefineWorldStencilDepth();ApplyPhysicalUv();
            ApplyGroundSurfaceUpgrade();ApplyGroundEdgeUpgrade();RefineGroundPassTaskLighting();
            RoutesAndViews();AddWholeMapReviewViews();SaveBuildingAssets();
            var sun=Obj.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);sun.transform.rotation=Quaternion.Euler(38,-48,0);sun.color=new Color(.82f,.88f,.95f);sun.intensity=.72f;
            var sky=Material("Wide_overcast",Color.white);sky.shader=Shader.Find("Anseong/OvercastSky");RenderSettings.skybox=sky;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.43f,.47f,.51f);RenderSettings.ambientEquatorColor=new Color(.34f,.36f,.38f);RenderSettings.ambientGroundColor=new Color(.2f,.19f,.17f);
            RenderSettings.fogStartDistance=400;RenderSettings.fogEndDistance=2000;RenderSettings.fogColor=new Color(.34f,.37f,.39f);
            SetPhotographicSky();ConfigureProductionLook();
            ApplyScenarioEnvironmentCorrections();BuildScenarioSpatialRehearsal();
            foreach(var c in viewer.views){c.backgroundColor=RenderSettings.fogColor;c.clearFlags=CameraClearFlags.Skybox;}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);ValidateWide();Capture(WideOutput);
            viewer.Select(0);viewer.labels.SetActive(true);viewer.routes.SetActive(true);viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);Debug.Log("WIDE_MAP_READY cameras="+viewer.views.Length+" prefabs="+assetRoots.Count);
        }
        [MenuItem("Anseong/Map35/Build Wide Ruins Viewer")]
        public static void BuildWide()
        {
            GenerateWide();Directory.CreateDirectory("../Builds/MapWide");BakeVisualUpgradeAndBuild();Debug.Log("WIDE_BUILD_OK");
        }
    }
}
