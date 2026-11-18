using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Obj=UnityEngine.Object;
namespace Anseong.Map35.Editor {
 public static partial class Map35Build {
 const string GtAssets="Assets/Map35/GroundTransferCandidate";
 static string GtOutput=>Path.GetFullPath("../docs/validation/ground-transfer-2026-11-15");
 public static void ReviewGroundTransfer(){
 string phase=RepArg("-groundPhase","baseline");
 string[] valid={"baseline","concrete-reference","concrete-meshy","concrete-hybrid","asphalt-reference","asphalt-meshy","asphalt-hybrid","concrete-puddles","asphalt-puddles"};
 if(!valid.Contains(phase))throw new ArgumentException("Unknown phase");
 string production=AuditHash(WideScene);
 string source=ExtB03+"/Scenes/B03_MeshyGenerated.unity";
 string sourceHash=AuditHash(source);
 EditorSceneManager.OpenScene(source);ReconnectWideScene();
 var matHashes=RepMaterialHashes();var colliders=RepColliderStates();
 string camerasHash=ExtB03CameraSignature(),anchorsHash=PhotoAnchorSignature(),lookHash=FacadeLookSignature();
 RepHideOverlays();
 string output=GtOutput+"/"+phase;Directory.CreateDirectory(output);
 PurposeFolder(GtAssets+"/Materials");PurposeFolder(GtAssets+"/Scenes");PurposeFolder(GtAssets+"/Meshes");
 var log=new StringBuilder("phase="+phase+"\nsource="+source+"\nProduction="+production+"\nFresh process="+System.Diagnostics.Process.GetCurrentProcess().Id+"\n");
 // Interior patch intentionally avoids existing I1 and scenario anchors.
 float x0=20,z0=35,size=40;
 var ground=Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
 .Where(r=>r.enabled&&r.bounds.size.y<1.5f&&r.bounds.max.y<1.2f&&r.bounds.size.x>5&&r.bounds.size.z>5&&r.bounds.min.x<x0+size&&r.bounds.max.x>x0&&r.bounds.min.z<z0+size&&r.bounds.max.z>z0).ToArray();
 // Combined renderer bounds include distant raised foundations; use the actual
 // local floor collider, not their maximum Y, to avoid floating the whole patch.
 var floorHits=Physics.RaycastAll(new Vector3(40,2,55),Vector3.down,4)
 .Where(h=>h.normal.y>.9f&&h.collider.bounds.size.y<1.5f&&h.point.y<.1f).ToArray();
 float top=floorHits.Length>0?floorHits.Max(h=>h.point.y):0;
 float y=top+.012f;
 log.AppendLine("Patch X20..60 Z35..75; highest underlying low renderer="+top.ToString("R",Inv)+"; overlayY="+y.ToString("R",Inv)+"; no collision changes; not a final terrain replacement.");
 File.WriteAllText(output+"/underlying-surfaces.csv","name,min,max\n"+string.Join("\n",ground.Select(r=>FacadeCsv(r.name)+","+FacadeCsv(RepVec(r.bounds.min))+","+FacadeCsv(RepVec(r.bounds.max)))));
 if(phase!="baseline"){
 string kind=phase.StartsWith("asphalt")?"asphalt":"concrete";
 bool meshy=phase.EndsWith("meshy"),puddles=phase.EndsWith("puddles"),hybrid=phase.EndsWith("hybrid")||puddles;
 string folder=GtAssets+"/"+kind;PurposeFolder(folder+"/Textures");
 Texture2D color,normal=null,rough=null;
 if(meshy){
 string src=Path.GetFullPath("../SourceAssets/GroundTransferTest/"+kind);
 foreach(var p in Directory.GetFiles(src,"texture_0_*.png")){string dest=folder+"/Textures/"+Path.GetFileName(p);if(!File.Exists(dest))File.Copy(p,dest);}
 AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
 var records=new List<MrTextureRecord>();
 color=MrTexture(folder+"/Textures/texture_0_","base_color",true,false,records);
 normal=MrTexture(folder+"/Textures/texture_0_","normal",false,true,records);
 rough=MrTexture(folder+"/Textures/texture_0_","roughness",false,false,records);
 foreach(var r in records)log.AppendLine(r.role+"="+r.width+"x"+r.height+" sha="+r.sha256);
 }else{
 string p=GtAssets+"/References/"+kind+".png";MrConfigureTexture(p,true,false,false);color=AssetDatabase.LoadAssetAtPath<Texture2D>(p);
 log.AppendLine("reference native="+color.width+"x"+color.height);
 }
 var shader=Shader.Find("Anseong/GroundTransferTest");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Ground shader unavailable");
 var mat=new Material(shader){name="Ground transfer "+phase};mat.SetTexture("_MainTex",color);
 mat.SetVector("_UVRect",meshy?new Vector4(.02f,.12f,.96f,.86f):new Vector4(0,0,1,1));
 if(meshy){mat.SetTexture("_NormalMap",normal);mat.SetTexture("_RoughMap",rough);mat.SetFloat("_PBR",1);}
 if(hybrid){
 string code=kind=="asphalt"?"Asphalt030":"Concrete040";
 var dn=GroundChannel(code,"NormalGL");var dr=GroundChannel(code,"Roughness");
 string dp=folder+"/Textures/detail-normal"+Path.GetExtension(AssetDatabase.GetAssetPath(dn));
 if(!File.Exists(dp))File.Copy(AssetDatabase.GetAssetPath(dn),dp);
 AssetDatabase.ImportAsset(dp,ImportAssetOptions.ForceSynchronousImport);MrConfigureTexture(dp,false,true,false);
 mat.SetTexture("_DetailNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(dp));mat.SetTexture("_DetailRough",dr);mat.SetTexture("_DetailColor",GroundChannel(code,"Color"));mat.SetFloat("_Detail",1);mat.SetFloat("_DetailMetres",kind=="asphalt"?2.2f:1.4f);
 log.AppendLine("Hybrid keeps reference broad RGB; scanned fine albedo luminance +/-12.25%, normal and roughness added. Source maps unchanged.");
 }
 if(puddles){
 var random=new System.Random(37);var centers=new List<Vector2>();var poolLog=new StringBuilder("id,x,z,radius_x,radius_z\n");double area=0;
 for(int i=0;i<8;i++){
 Vector2 center=Vector2.zero;bool accepted=false;
 for(int attempt=0;attempt<1000;attempt++){
 center=new Vector2(24+(float)random.NextDouble()*32,39+(float)random.NextDouble()*32);
 if(centers.All(p=>(p-center).sqrMagnitude>36)){accepted=true;break;}
 }
 if(!accepted)throw new Exception("Puddle spacing failed");centers.Add(center);
 float rx=1.1f+(float)random.NextDouble()*1.4f,rz=.8f+(float)random.NextDouble()*1.1f;
 mat.SetVector("_Puddle"+i,new Vector4(center.x,center.y,rx,rz));area+=Math.PI*rx*rz;
 poolLog.AppendLine(i+","+center.x.ToString("R",Inv)+","+center.y.ToString("R",Inv)+","+rx.ToString("R",Inv)+","+rz.ToString("R",Inv));
 }
 mat.SetFloat("_Puddles",1);File.WriteAllText(output+"/puddles.csv",poolLog.ToString());
 log.AppendLine("Puddles: seed37;8 fixed world-XZ centers separated >=6m; nominal ellipse area="+area.ToString("F2",Inv)+"m2 /1600m2 (not actual noise-mask coverage). Irregular feathered edges; darkening <=12%; roughness core0.12; fine normal flattened94%; metallic0. Existing reflection probes only, no planar reflection/new lights/physics/depth.");
 }
 string matPath=GtAssets+"/Materials/"+phase+".mat";var existingMat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
 if(existingMat==null)AssetDatabase.CreateAsset(mat,matPath);else{EditorUtility.CopySerialized(mat,existingMat);Obj.DestroyImmediate(mat);mat=existingMat;EditorUtility.SetDirty(mat);}
 var mesh=new Mesh{name="Ground transfer 40m"};
 mesh.vertices=new[]{new Vector3(x0,y,z0),new Vector3(x0,y,z0+size),new Vector3(x0+size,y,z0+size),new Vector3(x0+size,y,z0)};
 mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
 mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
 string meshPath=GtAssets+"/Meshes/"+phase+".asset";var existingMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
 if(existingMesh==null)AssetDatabase.CreateAsset(mesh,meshPath);else{EditorUtility.CopySerialized(mesh,existingMesh);Obj.DestroyImmediate(mesh);mesh=existingMesh;EditorUtility.SetDirty(mesh);}
 var go=new GameObject("GROUND TRANSFER TEST 40m - "+phase);go.AddComponent<MeshFilter>().sharedMesh=mesh;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveGI=ReceiveGI.LightProbes;mr.lightProbeUsage=LightProbeUsage.Off;mr.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
 log.AppendLine("Candidate uses global ambient SH (probeOff), same for all overlay variants; baseline retains original baked surface. This difference prevents attributing baseline change to RGB alone. Metallic deliberately0 for nonmetal pavement.");
 }
 // All copied cameras share source grading, sky, fog and renderer settings.
 var review=new List<Camera>();
 Vector3[] positions={new Vector3(40,70,55),new Vector3(30,29.75f,14),new Vector3(29,2.5f,32),new Vector3(58,2.5f,56),new Vector3(-30,80,10)};
 Vector3[] targets={new Vector3(40,0,55),new Vector3(40,0,55),new Vector3(43,0,50),new Vector3(68,0,55),new Vector3(40,0,55)};
 string[] names={"top","mech","drone","edge","control"};
 for(int i=0;i<names.Length;i++){
 var c=Obj.Instantiate(viewer.views[52]);c.name="GROUND TEST "+names[i];c.enabled=false;c.transform.position=positions[i];c.transform.rotation=Quaternion.LookRotation((targets[i]-positions[i]).normalized,i==0?Vector3.forward:Vector3.up);c.fieldOfView=i==0?43:60;c.clearFlags=CameraClearFlags.Skybox;review.Add(c);
 log.AppendLine("camera "+names[i]+" position="+RepVec(positions[i])+" target="+RepVec(targets[i])+" fov="+c.fieldOfView);
 SaveWearCamera(c,output+"/"+names[i]+".png");
 }
 SaveWearCamera(viewer.views[9],output+"/existing-control09.png");
 SaveWearCamera(viewer.views[0],output+"/overview.png");
 AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),GtAssets+"/Scenes/"+phase+".unity");
 bool unchanged=production==AuditHash(WideScene)&&sourceHash==AuditHash(source)&&matHashes.All(p=>AuditHash(p.Key)==p.Value)&&colliders.All(p=>p.Key!=null&&RepColliderState(p.Key)==p.Value)&&colliders.Count==RepColliderStates().Count&&camerasHash==ExtB03CameraSignature()&&anchorsHash==PhotoAnchorSignature()&&lookHash==FacadeLookSignature();
 if(!unchanged)throw new Exception("Protected scene/material/anchor/camera/look changed");
 log.AppendLine("Protected production/source scene/materials/colliders/anchors/original camera array/global look preserved. Seven mono Editor captures, not HMD verification; candidate saved separately. No GI bake or lighting adjustment.");
 File.WriteAllText(output+"/report.txt",log.ToString());Debug.Log("GROUND_TRANSFER_OK "+phase);
 }
 }
}
