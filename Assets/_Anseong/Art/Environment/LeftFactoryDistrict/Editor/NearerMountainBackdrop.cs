using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace CoastalFactory.Editor {public static partial class LeftFactoryDistrictBuilder {
public static void BringMountainsCloser(){
const string folder="Assets/_Anseong/Art/Environment/DistantMountainTerrain";
var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/ConnectedCoastalHinterland.asset");
var vertices=mesh.vertices;float nearest=99999,maxHeight=0,peakZ=0;
float[] cx={-10400,-7600,-5950,-3100,-1650,-600,2700,6200,8400,12300};
float[] cz={7800,8500,6200,8100,6400,9000,11200,11900,10800,12100};
float[] wx={1800,1300,2100,1700,1300,1450,1700,2100,1300,2400};
float[] wz={2700,2300,2600,3100,2100,2800,1700,1500,1600,1900};
float[] heights={280,350,240,290,180,110,240,330,210,360};
for(int i=0;i<vertices.Length;i++){
var p=vertices[i];float x=p.x/.5f,z=1100+(p.z-1100)/.28f,y=0;
for(int k=0;k<cx.Length;k++){float dx=(x-cx[k])/wx[k],dz=(z-cz[k])/wz[k];float q=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dz*dz));y=Mathf.Max(y,q*q*(3-2*q)*heights[k]);}
float inland=Mathf.Lerp(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-2500,250,x)),1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(9000,10500,z)));
y*=inland*Mathf.SmoothStep(0,1,Mathf.InverseLerp(3200,4700,z))*(.95f+.05f*Mathf.PerlinNoise(x*.002f+17,z*.002f))*.65f;
if(p.z<=1200)y=0;
p.y=y-.08f;vertices[i]=p;
if(y>5)nearest=Mathf.Min(nearest,p.z);if(y>maxHeight){maxHeight=y;peakZ=p.z;}
if(p.z<=1100&&Mathf.Abs(y)>.001f)throw new Exception("Factory ground disturbed");
}
mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
File.WriteAllText(folder+"/NearerMountains.md","# Closer mountain background\n\nRepositioned height profile toward the industrial site: rear distance compressed to28% aroundZ1100; lateral mountain footprint50%; peak heights65% to preserve low rounded hills. Mesh XZ coordinates/coastal outline unchanged. Z<=1200 stays flat, keeping the existing factory and roads clear. Same terrain mesh, triangles, material and memory allocation; no extra textures or runtime scripts. This closes the visible rear gap rather than changing robot movement bounds or the ocean mesh.\n\nFirst vertices higher than5m at Z="+nearest+"m; maximum height="+maxHeight+"m at Z="+peakZ+"m.\n");AssetDatabase.Refresh();Debug.Log("NEARER_MOUNTAINS_PASS firstSlopeZ="+nearest+" peakHeight="+maxHeight+" peakZ="+peakZ+" factoryGroundPreserved=true coastXZUnchanged=true");
}
}}
