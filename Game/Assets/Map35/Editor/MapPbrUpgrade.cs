using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string PbrRoot="Assets/ThirdParty/FreePBR/";
        static Material damagedConcrete,structuralSteel;
        static readonly Dictionary<string,Mesh> uvMeshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,Texture2D> pbrTextures=new Dictionary<string,Texture2D>();
        static Texture2D PbrChannel(string id,string channel)
        {
            string path=PbrRoot+id+"/"+id+"_2K-PNG_"+channel+".png";
            if(!File.Exists(path))path="Assets/ThirdParty/QuietSurfaces/"+id+"/"+id+"_2K-PNG_"+channel+".png";
            if(!File.Exists(path))path="Assets/ThirdParty/SurfaceLibrary/"+id+"/"+id+"_2K-PNG_"+channel+".png";
            if(!File.Exists(path))return null;
            if(pbrTextures.TryGetValue(path,out var cached))return cached;
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);
            bool normal=channel=="NormalGL";
            ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            ti.sRGBTexture=channel=="Color";ti.convertToNormalmap=false;ti.wrapMode=TextureWrapMode.Repeat;
            ti.mipmapEnabled=true;ti.anisoLevel=8;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();var result=AssetDatabase.LoadAssetAtPath<Texture2D>(path);pbrTextures[path]=result;return result;
        }
        static Material PbrMaterial(string name,string id,Color tint,float metres,float metal=1,float normal=.75f)
        {
            var m=Material("Wide_"+name,tint);m.shader=Shader.Find("Anseong/PhysicalUVSurface");
            m.SetTexture("_MainTex",PbrChannel(id,"Color"));m.SetTexture("_BumpMap",PbrChannel(id,"NormalGL"));
            m.SetTexture("_RoughnessMap",PbrChannel(id,"Roughness"));m.SetTexture("_MetallicMap",PbrChannel(id,"Metalness"));
            m.SetTexture("_AOMap",PbrChannel(id,"AmbientOcclusion"));m.SetFloat("_TileMetres",metres);
            m.SetFloat("_Metallic",metal);m.SetFloat("_NormalStrength",normal);m.SetFloat("_RoughnessScale",1);
            m.SetFloat("_OcclusionStrength",.85f);m.mainTextureScale=Vector2.one;m.enableInstancing=true;
            m.SetFloat("_Saturation",1);m.SetFloat("_AlbedoGain",1);
            m.SetVector("_TileAspect",Vector4.one);m.SetFloat("_RoughnessFloor",.12f);
            m.SetFloat("_WearAmount",0);
            EditorUtility.SetDirty(m);return m;
        }
        static void FreePbrMaterials()
        {
            pbrTextures.Clear();
            agedSteel=PbrMaterial("painted_steel","Metal049A",new Color(.68f,.72f,.74f),6,.08f,.2f);
            agedSteel.SetFloat("_Saturation",.3f);agedSteel.SetFloat("_RoughnessFloor",.6f);
            structuralSteel=PbrMaterial("structural_steel","Metal020",new Color(.64f,.67f,.67f),3.5f,.28f,.65f);
            structuralSteel.SetFloat("_Saturation",.35f);
            agedRust=PbrMaterial("oxidised_steel","Metal020",new Color(.7f,.6f,.46f),3,.22f,.8f);
            agedConcrete=PbrMaterial("concrete","Concrete034",new Color(.75f,.76f,.73f),4,0,.3f);
            agedConcrete.SetVector("_TileAspect",new Vector4(1,.5f,1,1));
            damagedConcrete=PbrMaterial("broken_concrete","Concrete042B",new Color(.8f,.77f,.7f),2,1,.85f);
            agedYellow=PbrMaterial("warning_paint","Metal020",new Color(.95f,.7f,.16f),1.5f,.05f,.5f);
            agedRoof=PbrMaterial("charred_roof","Metal020",new Color(.24f,.27f,.29f),4,.15f,.65f);
            asphalt=PbrMaterial("asphalt","Road015C",new Color(.73f,.74f,.74f),4,0,.85f);
            soot=PbrMaterial("soot_concrete","Concrete030",new Color(.18f,.16f,.14f),4,0,.5f);
            fire=Material("Wide_embers",new Color(1,.3f,.035f));fire.EnableKeyword("_EMISSION");fire.SetColor("_EmissionColor",new Color(3.5f,.55f,.04f));
        }
        static string MeshKey(Mesh source,Vector3 scale)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
            string key=guid+"/"+localId+"/"+source.name+"/"+source.vertexCount+"/"+scale.x.ToString("F4",Inv)+"/"+scale.y.ToString("F4",Inv)+"/"+scale.z.ToString("F4",Inv);
            using(var hash=MD5.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-","").ToLowerInvariant();
        }
        static Mesh MakePhysicalUv(Mesh source,Vector3 scale)
        {
            string key=MeshKey(source,scale);if(uvMeshes.TryGetValue(key,out var cached))return cached;
            var v=source.vertices;var n=source.normals;var oldUV=source.uv;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var panelData=new List<Vector4>();
            var size=Vector3.Scale(source.bounds.size,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            var indices=new List<int[]>();bool cylinder=source.name=="Cylinder"&&oldUV.Length==v.Length;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var faces=source.GetTriangles(sub);var target=new int[faces.Length];
                for(int j=0;j<faces.Length;j+=3)
                {
                    var face=Vector3.Cross(Vector3.Scale(v[faces[j+1]]-v[faces[j]],scale),Vector3.Scale(v[faces[j+2]]-v[faces[j]],scale)).normalized;
                    int axis=Mathf.Abs(face.x)>Mathf.Abs(face.y)?0:1;if(Mathf.Abs(face.z)>Mathf.Abs(face[axis]))axis=2;
                    for(int k=0;k<3;k++)
                    {
                        int s=faces[j+k];var p=Vector3.Scale(v[s],scale);target[j+k]=vertices.Count;vertices.Add(v[s]);normals.Add(n.Length==v.Length?n[s]:face);
                        var normalized=v[s]-source.bounds.min;var extent=source.bounds.size;
                        normalized=new Vector3(normalized.x/Mathf.Max(.001f,extent.x),normalized.y/Mathf.Max(.001f,extent.y),normalized.z/Mathf.Max(.001f,extent.z));
                        panelData.Add(axis==0?new Vector4(normalized.z,normalized.y,size.z,size.y):axis==1?new Vector4(normalized.x,normalized.z,size.x,size.z):new Vector4(normalized.x,normalized.y,size.x,size.y));
                        if(cylinder && Mathf.Abs(face.y)<.6f)uv.Add(new Vector2(oldUV[s].x*Mathf.PI*Mathf.Max(scale.x,scale.z),oldUV[s].y*2*scale.y));
                        else if(axis==0)uv.Add(new Vector2(p.z*(face.x<0?1:-1),p.y));
                        else if(axis==1)uv.Add(new Vector2(p.x,p.z*(face.y<0?1:-1)));
                        else uv.Add(new Vector2(p.x*(face.z<0?-1:1),p.y));
                    }
                }
                indices.Add(target);
            }
            var mesh=new Mesh{name=source.name+" physical UV",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(2,panelData);mesh.subMeshCount=indices.Count;
            for(int i=0;i<indices.Count;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=Base+"Meshes/PhysicalUV/"+key+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Obj.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            uvMeshes[key]=saved;return saved;
        }
        static void ApplyPhysicalUv()
        {
            Directory.CreateDirectory(Base+"Meshes/PhysicalUV");AssetDatabase.Refresh();uvMeshes.Clear();int count=0;
            foreach(var filter in Obj.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var r=filter.GetComponent<MeshRenderer>();if(r==null || filter.sharedMesh==null)continue;
                if(!r.sharedMaterials.Any(m=>m!=null&&m.shader.name=="Anseong/PhysicalUVSurface"))continue;
                filter.sharedMesh=MakePhysicalUv(filter.sharedMesh,filter.transform.lossyScale);count++;
            }
            var b02=Existing("B02");int lightmaps=0;
            foreach(var mesh in b02.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Distinct())
                if(mesh!=null&&AssetDatabase.GetAssetPath(mesh).Contains("/PhysicalUV/")){Unwrapping.GenerateSecondaryUVSet(mesh);EditorUtility.SetDirty(mesh);lightmaps++;}
            if(lightmaps==0)throw new InvalidOperationException("B02 secondary UV preparation unexpectedly processed no meshes.");
            File.WriteAllText(WideOutput+"/uv-audit.txt","Physical UV0 render meshes: "+count+"\nUnique generated mesh assets: "+uvMeshes.Count+"\nB02 meshes with secondary lightmap UV: "+lightmaps+"\nNormals preserved and tangent frames recalculated. Secondary UV preparation is not a baked-lighting claim.\n");
        }
        static Mesh SaveDetailMesh(Mesh mesh,string name)
        {
            mesh.name=name;mesh.RecalculateNormals();mesh.RecalculateBounds();string path=Base+"Meshes/"+name+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);Obj.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }
        static void TornSheet(string name,Vector3 p,Vector2 size,Vector3 rotation,int seed,Transform parent)
        {
            const int cols=20,rows=8;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int side=0;side<2;side++)for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {
                float u=(float)x/cols,w=(float)y/rows;
                float ragged=(Mathf.PerlinNoise(u*7,seed+.3f)-.5f)*2.4f*(1-w)*(1-w);
                float curl=(1-w)*(1-w)*(.8f+Mathf.Sin(u*7+seed)*1.2f);
                vertices.Add(new Vector3((u-.5f)*size.x,w*size.y+ragged,Mathf.Sin(u*size.x*19)*.08f+curl+side*.055f));
            }
            int layer=(cols+1)*(rows+1);
            for(int side=0;side<2;side++)for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
            {int a=side*layer+y*(cols+1)+x,b=a+1,c=a+cols+1,d=c+1;
                if(side==0)triangles.AddRange(new[]{a,c,b,b,c,d});else triangles.AddRange(new[]{a,b,c,b,d,c});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            var g=new GameObject(name);g.transform.SetParent(parent);g.transform.SetPositionAndRotation(p,Quaternion.Euler(rotation));
            g.AddComponent<MeshFilter>().sharedMesh=SaveDetailMesh(mesh,name);g.AddComponent<MeshRenderer>().sharedMaterial=agedRust;
        }
        static void RealismDetails()
        {
            var b02=Existing("B02");
            // Remove the opaque blue source glazing from the damaged bay; it read as untouched solid cladding.
            foreach(var r in b02.GetComponentsInChildren<MeshRenderer>(true))
                if(r.sharedMaterials.Any(m=>m!=null&&m.name.ToLowerInvariant().Contains("glass")))r.gameObject.SetActive(false);
            TornSheet("B02 torn cladding south",new Vector3(-103,20,27.35f),new Vector2(6,10),Vector3.zero,2,b02);
            TornSheet("B02 curled cladding edge",new Vector3(-76,18,36),new Vector2(5,12),new Vector3(0,90,-6),8,b02);
            TornSheet("B02 fallen roof sheet",new Vector3(-89,.7f,40),new Vector2(7,11),new Vector3(82,35,0),13,b02);
            for(int i=0;i<7;i++)
            {
                var rng=new System.Random(180+i);int sides=7;var v=new List<Vector3>();var t=new List<int>();
                for(int layer=0;layer<3;layer++)for(int s=0;s<sides;s++)
                {float a=s*Mathf.PI*2/sides,r=(.8f+(float)rng.NextDouble()*.5f)*(layer==1?1:.85f);v.Add(new Vector3(Mathf.Cos(a)*r,layer*.22f+((float)rng.NextDouble()-.5f)*.13f,Mathf.Sin(a)*r));}
                for(int l=0;l<2;l++)for(int s=0;s<sides;s++){int a=l*sides+s,b=l*sides+(s+1)%sides,c=a+sides,d=b+sides;t.AddRange(new[]{a,b,c,b,d,c});}
                for(int s=1;s<sides-1;s++){t.AddRange(new[]{14,14+s+1,14+s});t.AddRange(new[]{0,s,s+1});}
                var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);
                var g=new GameObject("B02 angular concrete fracture "+i);g.transform.SetParent(ruins);g.transform.position=new Vector3(-88+i*2.3f,.4f,27+i%3*2);g.transform.rotation=Quaternion.Euler(i*3,47*i,i*7);g.transform.localScale=new Vector3(1.7f,1.1f,1.2f);
                g.AddComponent<MeshFilter>().sharedMesh=SaveDetailMesh(mesh,"B02_ConcreteFracture_"+i);g.AddComponent<MeshRenderer>().sharedMaterial=damagedConcrete;
            }
            string sootPath=Base+"Textures/SootDecal_AI_v1.png";var importer=(TextureImporter)AssetImporter.GetAtPath(sootPath);
            importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var sootMat=Material("local_scorch",new Color(.1f,.085f,.07f,.72f));sootMat.shader=Shader.Find("Anseong/SurfaceSoot");sootMat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(sootPath);
            var scorch=Shape("local burn on remaining south cladding",PrimitiveType.Quad,new Vector3(-112,10,27.36f),new Vector3(9,18,1),sootMat,b02,false);
            scorch.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static void SetPhotographicSky()
        {
            string path=PbrRoot+"OvercastSoilPureSky/overcast_soil_puresky_4k.exr";var ti=(TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture=false;ti.maxTextureSize=4096;ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Clamp;ti.SaveAndReimport();
            var sky=Material("Wide_photographic_sky",Color.white);sky.shader=Shader.Find("Skybox/Panoramic");sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(path));sky.SetFloat("_Exposure",.62f);sky.SetFloat("_Rotation",120);sky.SetColor("_Tint",new Color(.47f,.49f,.51f));RenderSettings.skybox=sky;
            var sun=Obj.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);sun.intensity=.48f;sun.color=new Color(.9f,.94f,1);sun.shadowBias=.025f;sun.shadowNormalBias=.12f;sun.shadowStrength=.8f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.49f,.53f,.58f);RenderSettings.ambientEquatorColor=new Color(.33f,.36f,.39f);RenderSettings.ambientGroundColor=new Color(.16f,.15f,.14f);
            RenderSettings.fogColor=new Color(.43f,.46f,.49f);RenderSettings.fogStartDistance=450;RenderSettings.fogEndDistance=2300;
            QualitySettings.shadowDistance=300;QualitySettings.shadowCascades=4;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.antiAliasing=4;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
            // Cache a low-resolution HDR sky reflection; no runtime real-time cubemap capture required.
            var capture=new GameObject("temporary sky reflection capture").AddComponent<Camera>();capture.clearFlags=CameraClearFlags.Skybox;capture.cullingMask=0;capture.allowHDR=true;capture.enabled=false;
            var target=new RenderTexture(128,128,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){dimension=TextureDimension.Cube,useMipMap=true,autoGenerateMips=false};target.Create();
            if(!capture.RenderToCubemap(target))throw new InvalidOperationException("Sky reflection render failed.");
            var cube=new Cubemap(128,TextureFormat.RGBAHalf,true);cube.name="Overcast sky reflection";
            var previous=RenderTexture.active;double brightness=0;
            for(int face=0;face<6;face++)
            {
                Graphics.SetRenderTarget(target,0,(CubemapFace)face);var pixels=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);
                pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();var values=pixels.GetPixels();
                cube.SetPixels(values,(CubemapFace)face);foreach(var value in values)brightness+=(value.r+value.g+value.b)/3;Obj.DestroyImmediate(pixels);
            }
            RenderTexture.active=previous;cube.Apply(true,false);target.Release();Obj.DestroyImmediate(target);Obj.DestroyImmediate(capture.gameObject);
            double mean=brightness/(128*128*6);if(mean<.001)throw new InvalidOperationException("Sky reflection readback is black.");
            File.WriteAllText(WideOutput+"/reflection-audit.txt","HDR cubemap face readback mean RGB: "+mean.ToString("F6",Inv)+"\nSix faces copied to CPU pixels before saving the cubemap asset.\n");
            string cubePath=Base+"Textures/OvercastReflection.asset";var saved=AssetDatabase.LoadAssetAtPath<Cubemap>(cubePath);
            if(saved==null){AssetDatabase.CreateAsset(cube,cubePath);saved=cube;}else{EditorUtility.CopySerialized(cube,saved);Obj.DestroyImmediate(cube);EditorUtility.SetDirty(saved);}
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=saved;RenderSettings.reflectionIntensity=.6f;
        }
        [MenuItem("Anseong/Map35/Refresh PBR Look And Build Existing Wide Map")]
        public static void RefreshPbrLookAndBuild()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(WideScene);
            viewer=Obj.FindFirstObjectByType<Map35Viewer>();labels=viewer.labels.transform;routes=viewer.routes.transform;
            FreePbrMaterials();SetPhotographicSky();AssetDatabase.SaveAssets();Capture(WideOutput);
            viewer.Select(0);viewer.labels.SetActive(true);viewer.routes.SetActive(true);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(),WideScene);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Wide PBR refresh build failed");Debug.Log("WIDE_PBR_REFRESH_BUILD_OK");
        }
    }
}
