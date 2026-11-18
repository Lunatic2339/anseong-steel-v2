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
        const string ModelsRoot="Assets/ThirdParty/AcquiredModels/";
        static readonly Dictionary<string,Material> acquiredMaterials=new Dictionary<string,Material>();
        static readonly StringBuilder modelPlacements=new StringBuilder();
        static int modelInstanceCount;
        static Texture2D ModelChannel(string id,string stem,string channel)
        {
            string folder=ModelsRoot+id+"/textures";
            string path=Directory.GetFiles(folder,stem+"_"+channel+"_2k.*").FirstOrDefault(p=>!p.EndsWith(".meta"));
            if(path==null)return null;path=path.Replace('\\','/');
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=channel=="nor_gl"?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.convertToNormalmap=false;importer.sRGBTexture=channel=="diff";
            importer.mipmapEnabled=true;importer.anisoLevel=8;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.wrapMode=TextureWrapMode.Repeat;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material AcquiredMaterial(string id,string stem)
        {
            if(acquiredMaterials.TryGetValue(stem,out var material))return material;
            material=Material("Acquired_"+stem,Color.white);material.shader=Shader.Find("Anseong/ImportedPbr");
            material.SetTexture("_MainTex",ModelChannel(id,stem,"diff"));material.SetTexture("_BumpMap",ModelChannel(id,stem,"nor_gl"));
            material.SetTexture("_RoughnessMap",ModelChannel(id,stem,"rough"));material.SetTexture("_MetallicMap",ModelChannel(id,stem,"metal"));
            material.SetTexture("_AOMap",ModelChannel(id,stem,"ao"));material.SetFloat("_Metallic",1);material.SetFloat("_NormalStrength",.8f);
            material.enableInstancing=true;EditorUtility.SetDirty(material);acquiredMaterials[stem]=material;return material;
        }
        static GameObject ModelPart(string path,string id,string stem,Transform parent)
        {
            if(!File.Exists(path))throw new FileNotFoundException("Acquired game mesh missing",path);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.isReadable=true;importer.importAnimation=false;importer.generateSecondaryUV=false;
            importer.SaveAndReimport();var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            go.transform.SetParent(parent,false);var material=AcquiredMaterial(id,stem);
            foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                int count=renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                var slots=Enumerable.Repeat(material,count).ToArray();
                if(id=="portable_generator"&&count>1)
                {
                    var glass=Material("Acquired_generator_gauge_glass",new Color(.15f,.22f,.22f,.28f));
                    glass.SetFloat("_Glossiness",.8f);glass.SetFloat("_Metallic",.1f);glass.SetFloat("_Mode",3);
                    glass.SetInt("_SrcBlend",(int)BlendMode.One);glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);glass.SetInt("_ZWrite",0);
                    glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");glass.renderQueue=3000;slots[1]=glass;
                }
                renderer.sharedMaterials=slots;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            }
            return go;
        }
        static Transform PlaceAcquired(string id,string file,Vector3 p,float yaw,Vector3 scale,Transform parent,string stem=null,string farFile=null)
        {
            if(p.y<1&&id!="coast_rocks_01")
            {
                Physics.SyncTransforms();
                var ground=Physics.RaycastAll(p+Vector3.up*5,Vector3.down,8).Where(h=>h.normal.y>.65f&&(h.collider.transform.IsChildOf(structures)||h.collider.name.EndsWith("pavement"))).OrderBy(h=>h.distance).ToArray();
                if(ground.Length>0)p.y=ground[0].point.y+.015f;
            }
            stem=stem??id;var root=Group("Acquired "+id+" #"+modelInstanceCount++,parent);
            root.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));root.localScale=scale;
            var close=ModelPart(ModelsRoot+id+"/"+file+".fbx",id,stem,root);
            if(farFile!=null)
            {
                var far=ModelPart(ModelsRoot+id+"/"+farFile+".fbx",id,stem,root);
                var lod=root.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.10f,close.GetComponentsInChildren<Renderer>()),new LOD(.008f,far.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
            }
            var b=LegacyEnvironmentAudit.BoundsOf(close);
            modelPlacements.AppendLine(id+","+file+","+p.x+","+p.y+","+p.z+","+b.size.x+","+b.size.y+","+b.size.z);
            return root;
        }
        static void PlaceAcquiredModels()
        {
            AddApronContactSurfaces();
            acquiredMaterials.Clear();modelInstanceCount=0;modelPlacements.Clear();modelPlacements.AppendLine("asset,file,x,y,z,width,height,depth");
            var root=Group("15 CC0 scanned and authored equipment");
            // Human-scale equipment, grouped around service edges; the 35m mech and air corridors stay clear.
            foreach(var p in new[]{new Vector3(-74,0,34),new Vector3(-74,0,36.5f),new Vector3(-24,9.15f,-38),new Vector3(105,0,35),new Vector3(103,0,83),new Vector3(-138,0,219),new Vector3(145,0,608)})
                PlaceAcquired("utility_box_01","utility_box_01_Game",p,p.x<0?95:0,Vector3.one,root);
            foreach(var p in new[]{new Vector3(-78,0,33),new Vector3(-23,9.15f,-37),new Vector3(103,0,39),new Vector3(-135,0,225)})
                PlaceAcquired("portable_generator","portable_generator_Game",p,25,Vector3.one,root);
            foreach(var p in new[]{new Vector3(-63,0,22),new Vector3(-63,0,27),new Vector3(-63,0,32),new Vector3(111,0,73),new Vector3(111,0,78),new Vector3(-118,0,218),new Vector3(-118,0,223),new Vector3(315,0,495),new Vector3(315,0,500),new Vector3(315,0,505)})
                PlaceAcquired("concrete_road_barrier_02","concrete_road_barrier_02_Game",p,90,Vector3.one,root,null,"concrete_road_barrier_02_Far");
            for(int i=0;i<8;i++)
            {
                string stem="modular_industrial_pipes_01_group0"+(i<4?1:2);
                var p=i<4?new Vector3(-112+i*3,0,20):new Vector3(119,0,72+(i-4)*4);
                PlaceAcquired("modular_industrial_pipes_01","modular_industrial_pipes_01_pipe"+(i+1).ToString("00")+"_Game",p,i*23,Vector3.one,root,stem);
            }
            for(int i=0;i<6;i++)
            {
                var p=new Vector3(440+i%2*12,-1.55f,i<3?-155+i*40:775+(i-3)*38);
                PlaceAcquired("coast_rocks_01","coast_rocks_01_Game",p,20+i*23,Vector3.one*(.5f+i%3*.08f),root,null,"coast_rocks_01_Far");
            }
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.BatchingStatic);
            File.WriteAllText(WideOutput+"/acquired-model-placement.csv",modelPlacements.ToString());
            Debug.Log("ACQUIRED_MODELS_PLACED "+modelInstanceCount);
        }
        static void AddApronContactSurfaces()
        {
            foreach(var filter in Obj.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if(filter.name.EndsWith("pavement")&&filter.GetComponent<Collider>()==null)
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            Physics.SyncTransforms();
        }
        static void AlignAcquiredGroundContact()
        {
            AddApronContactSurfaces();var root=GameObject.Find("15 CC0 scanned and authored equipment");if(root==null)return;
            var report=new StringBuilder("object,x,y,z,support\n");
            foreach(Transform item in root.transform)
            {
                if(item.name.Contains("coast_rocks")||item.position.y>3)continue;
                var p=item.position;var hits=Physics.RaycastAll(new Vector3(p.x,5,p.z),Vector3.down,8).Where(h=>h.normal.y>.65f&&(h.collider.transform.IsChildOf(structures)||h.collider.name.EndsWith("pavement"))).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0)continue;p.y=hits[0].point.y+.015f;item.position=p;
                report.AppendLine(item.name+","+p.x+","+p.y+","+p.z+","+hits[0].collider.name);
            }
            File.WriteAllText(WideOutput+"/acquired-ground-contact.csv",report.ToString());
        }
    }
}
