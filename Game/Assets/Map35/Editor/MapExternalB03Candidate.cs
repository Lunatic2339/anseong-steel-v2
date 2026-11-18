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
        const string ExtB03="Assets/Map35/ExternalReferenceCandidate";
        const string ExtB03Scene=ExtB03+"/Scenes/B03_ReferenceCandidate.unity";
        const string ExtB03Root="B03 external reference candidate";
        static string ExtB03Output=>Path.GetFullPath("../docs/validation/b03-reference-candidate-2026-11-15");
        static readonly string[] ExtB03Kits={"modular_factory_facade","modular_airduct_circular_01","modular_fire_escape","modular_metal_gutter"};
        static readonly Dictionary<string,GameObject> extB03Models=new Dictionary<string,GameObject>();
        static readonly Dictionary<string,Material> extB03Materials=new Dictionary<string,Material>();
        static readonly Dictionary<string,Mesh> extB03Meshes=new Dictionary<string,Mesh>();
        static readonly StringBuilder extB03Placement=new StringBuilder();
        static float extB03Unit=1;
        static Quaternion extB03FacadeFlip=Quaternion.identity;

        static string ExtB03CameraSignature()=>string.Join("\n",viewer.views.Select((c,i)=>i+"|"+c.name+"|"+c.transform.position.ToString("R")+"|"+c.transform.rotation.ToString("R")+"|"+c.fieldOfView.ToString("R",Inv)));

        // Imports copies only. FBX and photographic texture originals remain outside Assets.
        public static void ImportExternalB03CandidateAssets()
        {
            Directory.CreateDirectory(ExtB03Output);
            foreach(string folder in new[]{"Sources","Materials","Meshes","Textures","Scenes"})PurposeFolder(ExtB03+"/"+folder);
            extB03Models.Clear();extB03Materials.Clear();extB03Meshes.Clear();
            foreach(string kit in ExtB03Kits)
            {
                string source=kit==ExtB03Kits[0]?Path.GetFullPath("../SourceAssets/ExternalBuildings20261115/"+kit):Path.GetFullPath("../SourceAssets/ExternalYard20261115/"+kit+"/source-4k");
                string folder=ExtB03+"/Sources/"+kit;Directory.CreateDirectory(folder);Directory.CreateDirectory(folder+"/textures");
                if(!Directory.Exists(source))throw new DirectoryNotFoundException(source);
                string fbx=Directory.GetFiles(source,"*.fbx").Single();
                string target=folder+"/"+Path.GetFileName(fbx);if(!File.Exists(target))File.Copy(fbx,target);
                foreach(var file in Directory.GetFiles(source+"/textures"))
                {
                    if(!new[]{".jpg",".jpeg",".png",".exr"}.Contains(Path.GetExtension(file).ToLowerInvariant()))continue;
                    string dest=folder+"/textures/"+Path.GetFileName(file);if(!File.Exists(dest))File.Copy(file,dest);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string kit in ExtB03Kits)
            {
                string folder=ExtB03+"/Sources/"+kit;
                foreach(string path in Directory.GetFiles(folder+"/textures").Where(p=>!p.EndsWith(".meta")))
                {
                    var ti=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;if(ti==null)continue;
                    string n=Path.GetFileName(path);bool normal=n.Contains("_nor_gl_");
                    ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                    ti.sRGBTexture=n.Contains("_diff_");ti.convertToNormalmap=false;ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Repeat;
                    ti.isReadable=!normal&&(n.Contains("_rough_")||n.Contains("_metal_")||n.Contains("_arm_"));
                    ti.anisoLevel=8;ti.maxTextureSize=4096;ti.SaveAndReimport();
                }
                string fbx=Directory.GetFiles(folder,"*.fbx").Single().Replace('\\','/');var mi=(ModelImporter)AssetImporter.GetAtPath(fbx);
                mi.isReadable=true;mi.importAnimation=false;mi.importCameras=false;mi.importLights=false;mi.addCollider=false;
                mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;mi.generateSecondaryUV=true;mi.SaveAndReimport();
                extB03Models[kit]=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if(extB03Models[kit]==null)throw new InvalidOperationException("No FBX prefab "+fbx);
            }
            var standard=ExtB03Find(ExtB03Kits[0],"wall_standard_standard_01");
            Bounds raw=ExtB03WorldBounds(standard);extB03Unit=3/Mathf.Max(.00001f,raw.size.x);
            if(Mathf.Abs(raw.size.y*extB03Unit-3)>.06f)throw new InvalidOperationException("FBX factory axes are not Y-up: "+raw);
            var m=standard.sharedMesh;var normals=m.normals;Vector3 sum=Vector3.zero;
            foreach(var n in normals)sum+=standard.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(n).normalized;
            extB03FacadeFlip=sum.z>0?Quaternion.Euler(0,180,0):Quaternion.identity;
            var report=new StringBuilder("kit,node,width,height,depth,submeshes,materials\n");
            foreach(var kit in ExtB03Kits)foreach(var f in extB03Models[kit].GetComponentsInChildren<MeshFilter>(true))
            {
                var b=ExtB03WorldBounds(f);var r=f.GetComponent<MeshRenderer>();
                report.AppendLine(kit+","+FacadeCsv(f.name)+","+RepVec(b.size)+","+f.sharedMesh.subMeshCount+","+FacadeCsv(string.Join(";",r.sharedMaterials.Select(x=>x==null?"NULL":x.name))));
            }
            File.WriteAllText(ExtB03Output+"/imported-modules.csv",report.ToString());
            File.WriteAllText(ExtB03Output+"/import-units.txt","Factory native imported width="+raw.size.x+"; normalization="+extB03Unit+"; Y-up verified. Canonical facade front=-Z. Original UV0 retained.\n");
            AssetDatabase.SaveAssets();Debug.Log("EXTERNAL_B03_IMPORT_OK");
        }

        static MeshFilter ExtB03Find(string kit,string name)
        {
            var f=extB03Models[kit].GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x=>x.name==name);
            if(f==null)throw new InvalidOperationException("Missing external module "+kit+" / "+name);return f;
        }
        static Bounds ExtB03WorldBounds(MeshFilter f)
        {
            var a=f.sharedMesh.vertices;Bounds b=new Bounds(f.transform.TransformPoint(a[0]),Vector3.zero);
            for(int i=1;i<a.Length;i++)b.Encapsulate(f.transform.TransformPoint(a[i]));return b;
        }
        static Texture2D ExtB03Texture(string kit,string prefix,string channel)
        {
            string folder=ExtB03+"/Sources/"+kit+"/textures";
            string p=Directory.GetFiles(folder).Where(x=>!x.EndsWith(".meta")&&Path.GetFileName(x).StartsWith(prefix+"_"+channel+"_",StringComparison.Ordinal)).OrderBy(x=>Path.GetExtension(x)==".png"?0:Path.GetExtension(x)==".exr"?1:2).FirstOrDefault();
            return p==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'));
        }
        static Material ExtB03Material(string kit,string sourceName)
        {
            string key=kit+"__"+sourceName;if(extB03Materials.TryGetValue(key,out var mat))return mat;
            string prefix=sourceName.Replace(" (Instance)","");if(prefix.Length>4&&prefix[prefix.Length-4]=='.'&&prefix.Substring(prefix.Length-3).All(char.IsDigit))prefix=prefix.Substring(0,prefix.Length-4);bool glass=prefix.Contains("glass");
            if(kit==ExtB03Kits[0])
            {
                if(prefix.Contains("glass"))prefix="modular_factory_facade_windows";
                if(prefix=="modular_factory_facade_brick_doors")prefix="modular_factory_facade_doors";
            }
            var albedo=ExtB03Texture(kit,prefix,"diff");
            if(albedo==null)throw new InvalidOperationException("No source albedo for "+kit+" / "+sourceName);
            string path=ExtB03+"/Materials/SOURCE_"+key+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}mat.shader=Shader.Find("Standard");
            mat.SetTexture("_MainTex",albedo);mat.SetColor("_Color",Color.white);
            var normal=ExtB03Texture(kit,prefix,"nor_gl");if(normal!=null){mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.65f);}
            var rough=ExtB03Texture(kit,prefix,"rough");var metal=ExtB03Texture(kit,prefix,"metal");
            if(rough!=null)
            {
                int side=Mathf.Max(rough.width,rough.height);var packed=new Texture2D(side,side,TextureFormat.RGBA32,false,true);var pixels=new Color32[side*side];
                for(int y=0;y<side;y++)for(int x=0;x<side;x++)
                {
                    float u=(x+.5f)/side,v=(y+.5f)/side;byte me=(byte)Mathf.RoundToInt(255*(metal==null?0:metal.GetPixelBilinear(u,v).r));
                    byte sm=(byte)Mathf.RoundToInt(255*Mathf.Clamp01(1-rough.GetPixelBilinear(u,v).r));pixels[y*side+x]=new Color32(me,0,0,sm);
                }
                packed.SetPixels32(pixels);packed.Apply();string p=ExtB03+"/Textures/packed_"+key+".png";File.WriteAllBytes(p,packed.EncodeToPNG());Obj.DestroyImmediate(packed);
                AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);var ti=(TextureImporter)AssetImporter.GetAtPath(p);ti.sRGBTexture=false;ti.mipmapEnabled=true;ti.maxTextureSize=4096;ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.SaveAndReimport();
                mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(p));mat.EnableKeyword("_METALLICGLOSSMAP");mat.SetFloat("_GlossMapScale",1);mat.SetFloat("_SmoothnessTextureChannel",0);
            }
            else {mat.SetFloat("_Metallic",0);mat.SetFloat("_Glossiness",.25f);}
            if(glass)
            {
                mat.SetOverrideTag("RenderType","Transparent");mat.SetFloat("_Mode",3);mat.SetInt("_SrcBlend",(int)BlendMode.One);mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);mat.SetInt("_ZWrite",0);
                mat.DisableKeyword("_ALPHATEST_ON");mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");mat.renderQueue=3000;mat.SetColor("_Color",new Color(.62f,.68f,.66f,.24f));mat.SetFloat("_Glossiness",.7f);
            }
            mat.SetOverrideTag("ExternalOriginalMaterial",sourceName);EditorUtility.SetDirty(mat);extB03Materials[key]=mat;return mat;
        }
        static Material ExtB03Role(string role,Material source,float tile=1)
        {
            string key="ROLE_"+role;if(extB03Materials.TryGetValue(key,out var m))return m;
            string path=ExtB03+"/Materials/B03_"+role+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(source);
            m.name="B03 "+role;m.SetOverrideTag("ExternalSurfaceRole",role);m.mainTextureScale=Vector2.one/tile;
            foreach(var prop in new[]{"_BumpMap","_MetallicGlossMap"})if(m.HasProperty(prop))m.SetTextureScale(prop,Vector2.one/tile);
            extB03Materials[key]=m;EditorUtility.SetDirty(m);return m;
        }
        static Mesh ExtB03Mesh(string kit,MeshFilter source,Vector3 anchor,bool facade,bool back=false)
        {
            string key=kit+"__"+source.name+"_"+Hash128.Compute(anchor.ToString("R")+facade+back);if(extB03Meshes.TryGetValue(key,out var saved))return saved;
            var mesh=Obj.Instantiate(source.sharedMesh);mesh.name=source.name+" canonical";
            float unit=facade?extB03Unit:1;Quaternion rotate=facade?extB03FacadeFlip:Quaternion.identity;
            Matrix4x4 matrix=Matrix4x4.TRS(Vector3.zero,rotate,Vector3.one*unit)*source.transform.localToWorldMatrix;
            var v=mesh.vertices;var n=mesh.normals;var tang=mesh.tangents;Matrix4x4 nm=matrix.inverse.transpose;
            for(int i=0;i<v.Length;i++){v[i]=matrix.MultiplyPoint3x4(v[i])-anchor;if(back)v[i].z=.27f;if(n.Length==v.Length)n[i]=(back?-1:1)*nm.MultiplyVector(n[i]).normalized;if(tang.Length==v.Length){var t=matrix.MultiplyVector(new Vector3(tang[i].x,tang[i].y,tang[i].z)).normalized;tang[i]=new Vector4(t.x,t.y,t.z,tang[i].w*(matrix.determinant<0?-1:1));}}
            mesh.vertices=v;if(n.Length==v.Length)mesh.normals=n;if(tang.Length==v.Length)mesh.tangents=tang;
            if((matrix.determinant<0)^back)for(int s=0;s<mesh.subMeshCount;s++){var ix=mesh.GetTriangles(s);for(int i=0;i<ix.Length;i+=3){int t=ix[i+1];ix[i+1]=ix[i+2];ix[i+2]=t;}mesh.SetTriangles(ix,s);}
            mesh.RecalculateBounds();if(tang.Length!=v.Length)mesh.RecalculateTangents();
            string path=ExtB03+"/Meshes/"+key+".asset";saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Obj.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}extB03Meshes[key]=saved;return saved;
        }
        static GameObject ExtB03Render(string name,Mesh mesh,Material[] mats,Transform parent,Vector3 p,Quaternion q,Vector3 scale)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(p,q);go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=mats;r.receiveGI=ReceiveGI.LightProbes;
            if(mats.Any(m=>m.renderQueue>=3000))r.shadowCastingMode=ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
            extB03Placement.AppendLine(FacadeCsv(name)+","+RepVec(r.bounds.min)+","+RepVec(r.bounds.max)+","+mesh.triangles.Length/3);return go;
        }
        static Vector3 ExtB03FacadePoint(Vector3 p)=>extB03FacadeFlip*(p*extB03Unit);
        static void ExtB03Tile(Transform parent,string wall,string detail,Vector3 position,Quaternion rotation,Vector2 size,Material brick,string label)
        {
            string kit=ExtB03Kits[0];var f=ExtB03Find(kit,wall);var pts=f.sharedMesh.vertices.Select(v=>ExtB03FacadePoint(f.transform.TransformPoint(v))).ToArray();
            Bounds b=new Bounds(pts[0],Vector3.zero);foreach(var p in pts)b.Encapsulate(p);
            // The outer plane is canonical z=0; paired authored objects retain one origin.
            Vector3 anchor=new Vector3(b.min.x,b.min.y,b.min.z);Vector3 scale=new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),1);
            foreach(bool back in new[]{false,true})ExtB03Render(label+(back?" inside":" wall"),ExtB03Mesh(kit,f,anchor,true,back),new[]{brick},parent,position,rotation,scale);
            if(!string.IsNullOrEmpty(detail))
            {
                var d=ExtB03Find(kit,detail);var mats=d.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>ExtB03Material(kit,m.name)).ToArray();
                ExtB03Render(label+" "+detail,ExtB03Mesh(kit,d,anchor,true),mats,parent,position,rotation,scale);
            }
        }
        static GameObject ExtB03Part(Transform root,string kit,string part,Vector3 center,Quaternion rotation,Vector3 scale)
        {
            var f=ExtB03Find(kit,part);var b=ExtB03WorldBounds(f);bool facade=kit==ExtB03Kits[0];var m=ExtB03Mesh(kit,f,facade?ExtB03FacadePoint(b.center):b.center,facade);
            var mats=f.GetComponent<MeshRenderer>().sharedMaterials.Select(x=>ExtB03Material(kit,x.name)).ToArray();
            return ExtB03Render(part,m,mats,root,center,rotation,scale);
        }
        static Material ExtB03Coating(string role,Color tint)
        {
            string key="COATING_"+role;if(extB03Materials.TryGetValue(key,out var found))return found;
            string prefix="Assets/ThirdParty/FreePBR/PhotorealFacades/corrugated_iron_02/corrugated_iron_02_";
            var m=new Material(Shader.Find("Standard"));m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"diff_2k.jpg"));m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"nor_gl_2k.png"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.55f);m.SetFloat("_Glossiness",.24f);m.SetFloat("_Metallic",0);m.color=tint;
            var result=ExtB03Role(role,m,2.7f);Obj.DestroyImmediate(m);extB03Materials[key]=result;return result;
        }
        static GameObject ExtB03Box(Transform parent,string name,Vector3 center,Vector3 size,Material mat,Quaternion? rotation=null)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            Vector3[] ns={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(Vector3 n in ns)
            {
                Vector3 u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,n);Vector3 v=Vector3.Cross(n,u);
                Vector3 c=Vector3.Scale(n,size)*.5f;float w=Vector3.Scale(u,size).magnitude,h=Vector3.Scale(v,size).magnitude;int a=vertices.Count;
                vertices.Add(c-u*w*.5f-v*h*.5f);vertices.Add(c+u*w*.5f-v*h*.5f);vertices.Add(c+u*w*.5f+v*h*.5f);vertices.Add(c-u*w*.5f+v*h*.5f);
                uv.Add(Vector2.zero);uv.Add(new Vector2(w,0));uv.Add(new Vector2(w,h));uv.Add(new Vector2(0,h));tris.AddRange(new[]{a,a+1,a+2,a,a+2,a+3});
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=ExtB03+"/Meshes/box_"+Hash128.Compute(name+size.ToString("R"))+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else Obj.DestroyImmediate(mesh);
            return ExtB03Render(name,saved,new[]{mat},parent,center,rotation??Quaternion.identity,Vector3.one);
        }

        // Candidate scene only. Original B03 colliders and lights remain active.
        public static void BuildB03ExternalCandidate()
        {
            ImportExternalB03CandidateAssets();EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            string originalHash=AuditHash(WideScene),look=FacadeLookSignature(),anchors=PhotoAnchorSignature(),camera=ExtB03CameraSignature();
            var colliders=RepColliderStates();var productionMats=RepMaterialHashes();
            var b03=Existing("B03");if(b03==null)throw new InvalidOperationException("Production B03 missing");
            if(GameObject.Find(ExtB03Root)!=null)throw new InvalidOperationException("Start candidate from production scene");
            var root=new GameObject(ExtB03Root).transform;extB03Placement.Clear();extB03Placement.AppendLine("name,min_x,min_y,min_z,max_x,max_y,max_z,triangles");
            int hidden=0;var oldReport=new StringBuilder("path,renderer_was_enabled,operation\n");
            var lod=b03.GetComponent<LODGroup>();if(lod!=null)lod.enabled=false;
            foreach(var r in b03.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n=r.name.ToLowerInvariant();bool retain=n.Contains("lamp")||n.Contains("light")||n.Contains("luminaire")||n.Contains("cable")||n.Contains("cabinet")||n.Contains("foundation")||n.Contains("footing");
                if(retain&&!FacadeIsFarLod(r.transform))continue;
                oldReport.AppendLine(FacadeCsv(ScenarioValidationPath(r.transform))+","+r.enabled+",visual disabled only");r.enabled=false;hidden++;
            }
            var brickBase=ExtB03Material(ExtB03Kits[0],"modular_factory_facade_brick");
            var brickWest=ExtB03Role("brick_west",brickBase);var brickSouth=ExtB03Role("brick_south",brickBase);var brickOther=ExtB03Role("brick_other",brickBase);
            var metalA=ExtB03Coating("annex_south",new Color(.70f,.76f,.66f));var metalB=ExtB03Coating("annex_south_b",new Color(.63f,.70f,.61f));var metalC=ExtB03Coating("annex_south_c",new Color(.76f,.77f,.67f));
            var metalOther=ExtB03Coating("annex_other",new Color(.64f,.70f,.62f));var roofMat=ExtB03Coating("roof",new Color(.54f,.57f,.53f));
            var trim=ExtB03Material(ExtB03Kits[0],"modular_factory_facade_trim_01");var plinth=ExtB03Role("plinth",trim);
            const float west=73.9f,seam=91.4f,east=100f,south=42.8f,north=56.5f,lowSouth=36f,eave=12.9f,ridge=14.85f;
            // Full-height brick wing follows the reference within the existing overall footprint.
            // West: exactly three broad industrial windows on each of two floors.
            Quaternion westQ=Quaternion.Euler(0,90,0),southQ=Quaternion.identity,northQ=Quaternion.Euler(0,180,0),eastQ=Quaternion.Euler(0,270,0);
            float wingDepth=north-south,windowBay=3.6f,pier=(wingDepth-windowBay*3)/4;
            for(int floor=0;floor<2;floor++)
            {
                float y=1+floor*5.5f;float cursor=0;
                for(int bay=0;bay<3;bay++)
                {
                    ExtB03Tile(root,"wall_standard_standard_01",null,new Vector3(west,y,north-cursor),westQ,new Vector2(pier,4.7f),brickWest,"West pier "+floor+" "+bay);cursor+=pier;
                    ExtB03Tile(root,"wall_window_tall_large_03","window_tall_large_03",new Vector3(west,y,north-cursor),westQ,new Vector2(windowBay,4.7f),brickWest,"West window "+floor+" "+bay);cursor+=windowBay;
                }
                ExtB03Tile(root,"wall_standard_standard_01",null,new Vector3(west,y,north-cursor),westQ,new Vector2(pier,4.7f),brickWest,"West final pier "+floor);
            }
            // Continuous brick bands close the ground, floor transition and eave.
            foreach(var band in new[]{new Vector2(0,1),new Vector2(5.7f,.8f),new Vector2(11.2f,eave-11.2f)})
                for(int k=0;k<5;k++)ExtB03Tile(root,"wall_standard_standard_01",null,new Vector3(west,band.x,north-k*wingDepth/5),westQ,new Vector2(wingDepth/5,band.y),brickWest,"West brick band "+band.x+" "+k);
            for(int row=0;row<4;row++)for(int col=0;col<4;col++)
            {
                float w=(seam-west)/4;string label="South brick "+row+" "+col;
                ExtB03Tile(root,"wall_standard_standard_01",null,new Vector3(west+col*w,row*eave/4,south),southQ,new Vector2(w,eave/4),brickSouth,label);
                ExtB03Tile(root,"wall_standard_standard_01",null,new Vector3(seam-col*w,row*eave/4,north),northQ,new Vector2(w,eave/4),brickOther,"North brick "+row+" "+col);
            }
            for(int col=0;col<5;col++)ExtB03Tile(root,"wall_standard_standard_01",null,new Vector3(seam,8.2f,south+col*wingDepth/5),eastQ,new Vector2(wingDepth/5,eave-8.2f),brickOther,"East upper brick "+col);
            // A low forward wing wraps in FRONT of the high brick gable, as in the reference.
            // The west return is brick with the pedestrian door; the long south face is metal.
            float returnLength=south-lowSouth;
            for(int row=0;row<3;row++)for(int col=0;col<2;col++)
            {
                bool door=row==0&&col==1;
                ExtB03Tile(root,door?"wall_door_centered_large_01":"wall_standard_standard_01",door?"door_centered_large_01":null,new Vector3(west,row*8.2f/3,south-col*returnLength/2),westQ,new Vector2(returnLength/2,8.2f/3),brickSouth,"Low west brick entry "+row+" "+col);
            }
            const float shutterL=89.7f,shutterR=93.3f;
            foreach(var range in new[]{new Vector2(west,shutterL),new Vector2(shutterR,east)})
            {
                int bays=Mathf.CeilToInt((range.y-range.x)/2.7f);float width=(range.y-range.x)/bays;
                for(int bay=0;bay<bays;bay++)for(int band=0;band<3;band++)
                    ExtB03Box(root,"Annex south "+range.x+" bay "+bay+" band "+band,new Vector3(range.x+(bay+.5f)*width,.5f+(band+.5f)*2.55f,lowSouth),new Vector3(width,2.55f,.24f),new[]{metalA,metalB,metalC}[(bay+band*2)%3]);
            }
            ExtB03Box(root,"Annex south shutter head",new Vector3((shutterL+shutterR)/2,7.325f,lowSouth),new Vector3(shutterR-shutterL,1.65f,.24f),metalA);
            for(int k=0;k<7;k++)for(int band=0;band<3;band++)
            {
                ExtB03Box(root,"Annex east bay "+k+" band "+band,new Vector3(east,1.5f+band*2.5f,lowSouth+(k+.5f)*(north-lowSouth)/7),new Vector3(.24f,2.5f,(north-lowSouth)/7),metalOther);
                ExtB03Box(root,"Annex north bay "+k+" band "+band,new Vector3(seam+(k+.5f)*(east-seam)/7,1.5f+band*2.5f,north),new Vector3((east-seam)/7,2.5f,.24f),metalOther);
            }
            ExtB03Box(root,"B03 continuous plinth",new Vector3((west+east)/2,.25f,(lowSouth+north)/2),new Vector3(east-west,.5f,north-lowSouth),plinth);
            ExtB03Box(root,"Annex forward low roof",new Vector3((west+east)/2,8.10f,(lowSouth+south)/2),new Vector3(east-west+.65f,.20f,south-lowSouth+.40f),roofMat);
            ExtB03Box(root,"Annex east low roof",new Vector3((seam+east)/2,8.10f,(south+north)/2),new Vector3(east-seam+.65f,.20f,north-south+.40f),roofMat);
            float half=(seam-west)/2,rise=ridge-eave,slope=Mathf.Atan2(rise,half)*Mathf.Rad2Deg,len=Mathf.Sqrt(half*half+rise*rise);
            ExtB03Box(root,"Brick wing west pitched roof",new Vector3(west+half*.5f,(eave+ridge)/2,(south+north)/2),new Vector3(len+.4f,.16f,north-south+.75f),roofMat,Quaternion.Euler(0,0,slope));
            ExtB03Box(root,"Brick wing east pitched roof",new Vector3(seam-half*.5f,(eave+ridge)/2,(south+north)/2),new Vector3(len+.4f,.16f,north-south+.75f),roofMat,Quaternion.Euler(0,0,-slope));
            // Gable triangles have real front/back faces; the roof is not a floating pair of planes.
            foreach(float z in new[]{south,north})ExtB03Gable(root,"Gable "+z,new Vector3(west,eave,z),new Vector3((west+seam)/2,ridge,z),new Vector3(seam,eave,z),roofMat);
            // Original framed rolling door module, scaled only as an industrial door assembly.
            ExtB03Part(root,ExtB03Kits[0],"door_garage_door_01",new Vector3((shutterL+shutterR)/2,3.5f,lowSouth+.10f),Quaternion.identity,new Vector3((shutterR-shutterL)/5.32f,6f/2.74f,1));
            for(int vent=0;vent<2;vent++)ExtB03Part(root,ExtB03Kits[1],"modular_airduct_rectangular_vent_fan",new Vector3(84+vent*12f,6.2f,lowSouth-.16f),Quaternion.identity,new Vector3(2.1f,2.4f,1));
            // Rooftop trunk is repeated photographed duct segments, with real flanges.
            for(int i=0;i<7;i++)ExtB03Part(root,ExtB03Kits[1],"modular_airduct_circular_triple",new Vector3(81+i*1.75f,9.3f,lowSouth+3.2f),Quaternion.Euler(0,90,0),new Vector3(3.2f,3.2f,1.23f));
            ExtB03Part(root,ExtB03Kits[1],"modular_airduct_circular_triple",new Vector3(92.5f,10.2f,lowSouth+3.2f),Quaternion.Euler(90,0,0),new Vector3(3.2f,3.2f,2.1f));
            // Measured source:15 treads, rise .2578m,going .1549m. Three adapted flights:
            // 45 rises to8.2m =>.18222m rise;going .240m. This is longer than the AI stair.
            float stairY=(8.2f/45)/.2578f,stairX=1.55f,firstCenterY=8.2f/45+1.72045f*stairY;
            for(int flight=0;flight<3;flight++)ExtB03Part(root,ExtB03Kits[2],"modular_fire_escape_stairs",new Vector3(east+1f,firstCenterY+flight*(8.2f/3),lowSouth+4.02f+flight*4.275f),Quaternion.Euler(0,-90,0),new Vector3(stairX,stairY,1));
            ExtB03Part(root,ExtB03Kits[2],"modular_fire_escape_platform_middle",new Vector3(east+.9f,8.2f-.772419f/2,lowSouth+15.2f),Quaternion.Euler(0,90,0),Vector3.one);
            ExtB03Part(root,ExtB03Kits[2],"modular_fire_escape_railing_middle",new Vector3(east+.9f,8.2f+.9841f/2,lowSouth+15.2f),Quaternion.Euler(0,90,0),Vector3.one);
            PrepareB03WallReferenceUVs();
            ApplyB03ReferenceTextureOverrides();
            File.WriteAllText(ExtB03Output+"/placements.csv",extB03Placement.ToString());File.WriteAllText(ExtB03Output+"/preserved-original-renderers.csv",oldReport.ToString());
            if(look!=FacadeLookSignature()||anchors!=PhotoAnchorSignature()||camera!=ExtB03CameraSignature())throw new InvalidOperationException("Candidate changed protected look/anchors/cameras");
            foreach(var p in colliders)if(p.Key==null||RepColliderState(p.Key)!=p.Value)throw new InvalidOperationException("Candidate changed original collider");
            foreach(var p in productionMats)if(AuditHash(p.Key)!=p.Value)throw new InvalidOperationException("Candidate changed original material "+p.Key);
            if(originalHash!=AuditHash(WideScene))throw new InvalidOperationException("Candidate changed production scene file");
            Bounds combined=root.GetComponentInChildren<MeshRenderer>().bounds;foreach(var r in root.GetComponentsInChildren<MeshRenderer>())combined.Encapsulate(r.bounds);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ExtB03Scene);
            File.WriteAllText(ExtB03Output+"/candidate-build.txt","Candidate scene="+ExtB03Scene+"\nProduction SHA256="+originalHash+" unchanged\nDisabled original visual renderers="+hidden+"; original colliders="+colliders.Count+" preserved\nCameras="+viewer.views.Length+" unchanged; global look/anchors unchanged\nCandidate world bounds="+combined+"\nMorphology stage02: high brick x[73.9,91.4],z[42.8,56.5],eave12.9/ridge14.85; forward low wing x[73.9,100],z[36,42.8] plus east annex,roof8.2. Candidate height+1.35m vs production aligns reference ridge; current production unchanged. Six west windows. Native stairs require visual contact review. No production promotion; GI/LOD/performance not accepted.\n");
            Debug.Log("EXTERNAL_B03_CANDIDATE_READY");
        }
        static void ExtB03Gable(Transform root,string name,Vector3 a,Vector3 b,Vector3 c,Material mat)
        {
            var mesh=new Mesh{name=name};mesh.vertices=new[]{a,b,c,a+Vector3.forward*.16f,b+Vector3.forward*.16f,c+Vector3.forward*.16f};
            mesh.triangles=new[]{0,1,2,5,4,3,0,3,4,0,4,1,1,4,5,1,5,2,2,5,3,2,3,0};mesh.uv=new[]{Vector2.zero,new Vector2((c.x-a.x)/2,b.y-a.y),new Vector2(c.x-a.x,0),Vector2.zero,new Vector2((c.x-a.x)/2,b.y-a.y),new Vector2(c.x-a.x,0)};mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=ExtB03+"/Meshes/"+Hash128.Compute(name)+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);old=mesh;}else{EditorUtility.CopySerialized(mesh,old);Obj.DestroyImmediate(mesh);}
            ExtB03Render(name,old,new[]{mat},root,Vector3.zero,Quaternion.identity,Vector3.one);
        }
        // UV0 imported PBR atlas,UV2 lightmap retained. UV3 orthographic facade and
        // UV4 original fixed camera projection are separate fair-comparison modes.
        public static void PrepareB03WallReferenceUVs()
        {
            var obj=GameObject.Find(ExtB03Root);if(obj==null)throw new InvalidOperationException("Candidate geometry missing");
            PurposeFolder(ExtB03+"/Meshes/ReferenceUV");var csv=new StringBuilder("renderer,face,uv3_rule,uv4_reference_camera\n");
            Camera c=viewer.views[52];float aspect=c.aspect;c.aspect=1586f/992f;
            try
            {
                foreach(var f in obj.GetComponentsInChildren<MeshFilter>())
                {
                    string n=f.name,face;
                    if(n.StartsWith("West "))face="brick_west";
                    else if(n.StartsWith("Low west brick"))face="brick_entry";
                    else if(n.StartsWith("South brick"))face="brick_south";
                    else if(n.StartsWith("North brick"))face="brick_north";
                    else if(n.StartsWith("East upper"))face="brick_east";
                    else if(n.StartsWith("Annex south"))face="annex_south";
                    else if(n.StartsWith("Annex east bay"))face="annex_east";
                    else if(n.StartsWith("Annex north"))face="annex_north";
                    else if(n.Contains("roof")||n.StartsWith("Gable"))face="roof";
                    else if(n.Contains("plinth"))face="plinth";
                    else face="accessory";
                    var m=Obj.Instantiate(f.sharedMesh);var a=m.vertices;var ortho=new List<Vector2>(a.Length);var projected=new List<Vector3>(a.Length);
                    for(int i=0;i<a.Length;i++)
                    {
                        Vector3 p=f.transform.TransformPoint(a[i]);Vector2 uv;
                        switch(face)
                        {
                            case "brick_west":uv=new Vector2((56.5f-p.z)/13.7f,p.y/12.9f);break;
                            case "brick_entry":uv=new Vector2((42.8f-p.z)/6.8f,p.y/8.2f);break;
                            case "brick_south":uv=new Vector2((p.x-73.9f)/17.5f,p.y/12.9f);break;
                            case "brick_north":uv=new Vector2((91.4f-p.x)/17.5f,p.y/12.9f);break;
                            case "brick_east":uv=new Vector2((p.z-42.8f)/13.7f,(p.y-8.2f)/4.7f);break;
                            case "annex_south":uv=new Vector2((p.x-73.9f)/26.1f,(p.y-.5f)/7.65f);break;
                            case "annex_east":uv=new Vector2((p.z-36)/20.5f,(p.y-.25f)/7.5f);break;
                            case "annex_north":uv=new Vector2((100-p.x)/8.6f,(p.y-.25f)/7.5f);break;
                            default:uv=new Vector2((p.x-73.9f)/26.1f,(p.z-36)/20.5f);break;
                        }
                        ortho.Add(uv);var q=c.WorldToViewportPoint(p);projected.Add(new Vector3(q.x*q.z,q.y*q.z,q.z));
                    }
                    m.SetUVs(2,ortho);m.SetUVs(3,projected);
                    string key=Hash128.Compute(n+f.transform.position.ToString("R")+f.transform.rotation.ToString("R")+f.transform.localScale.ToString("R")).ToString();
                    string path=ExtB03+"/Meshes/ReferenceUV/"+key+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(saved==null){AssetDatabase.CreateAsset(m,path);saved=m;}else{EditorUtility.CopySerialized(m,saved);Obj.DestroyImmediate(m);EditorUtility.SetDirty(saved);}f.sharedMesh=saved;
                    csv.AppendLine(FacadeCsv(n)+","+face+",wall_specific_normalized,52_1586x992");
                }
            }
            finally{c.aspect=aspect;}
            File.WriteAllText(ExtB03Output+"/reference-uv.csv",csv.ToString());
        }
        public static void ApplyB03ReferenceTextureOverrides()
        {
            foreach(string path in AssetDatabase.FindAssets("t:Material",new[]{ExtB03+"/Materials"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);string role=m.GetTag("ExternalSurfaceRole",false,"");if(role.Length==0)continue;
                string texturePath=ExtB03+"/Textures/b03_"+role+"_albedo.png";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(t==null)continue;
                var ti=(TextureImporter)AssetImporter.GetAtPath(texturePath);ti.sRGBTexture=true;ti.textureType=TextureImporterType.Default;ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Repeat;ti.anisoLevel=8;ti.SaveAndReimport();
                m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));m.SetColor("_Color",Color.white);EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }
        public static void CaptureB03ExternalCandidate()
        {
            EditorSceneManager.OpenScene(ExtB03Scene);ReconnectWideScene();Directory.CreateDirectory(ExtB03Output);RepHideOverlays();
            foreach(int i in new[]{0,3,9,52})SaveWearCamera(viewer.views[i],ExtB03Output+"/candidate-"+i.ToString("00")+".png");
            Debug.Log("EXTERNAL_B03_CAPTURE_OK");
        }
    }
}
