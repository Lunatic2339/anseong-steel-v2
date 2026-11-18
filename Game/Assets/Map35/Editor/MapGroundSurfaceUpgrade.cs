using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string PurposeGroundFolder="Assets/Map35/GroundSurfacing";
        const string PurposeGroundShader="Anseong/PurposeGround";

        // RGBA has semantic meaning: replacement surfacing, vehicle/work wear,
        // drainage moisture, and steel-stock residue. No random full-site dirt.
        sealed class GroundPurposeCanvas
        {
            public readonly string id;
            public readonly Rect rect;
            public readonly int width,height;
            readonly Color32[] pixels;
            public GroundPurposeCanvas(string id,Rect rect,int width,int height)
            {this.id=id;this.rect=rect;this.width=width;this.height=height;pixels=new Color32[width*height];}
            void Paint(Rect area,Func<Vector2,float> shape,int channel,float intensity)
            {
                int x0=Mathf.Clamp(Mathf.FloorToInt((area.xMin-rect.xMin)/rect.width*width),0,width-1);
                int x1=Mathf.Clamp(Mathf.CeilToInt((area.xMax-rect.xMin)/rect.width*width),0,width-1);
                int z0=Mathf.Clamp(Mathf.FloorToInt((area.yMin-rect.yMin)/rect.height*height),0,height-1);
                int z1=Mathf.Clamp(Mathf.CeilToInt((area.yMax-rect.yMin)/rect.height*height),0,height-1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    var p=new Vector2(rect.xMin+(x+.5f)/width*rect.width,rect.yMin+(z+.5f)/height*rect.height);
                    float amount=Mathf.Clamp01(shape(p)*intensity);if(amount<=0)continue;
                    int i=z*width+x;var c=pixels[i];byte v=(byte)Mathf.RoundToInt(amount*255);
                    if(channel==0)c.r=(byte)Mathf.Max(c.r,v);else if(channel==1)c.g=(byte)Mathf.Max(c.g,v);
                    else if(channel==2)c.b=(byte)Mathf.Max(c.b,v);else c.a=(byte)Mathf.Max(c.a,v);
                    pixels[i]=c;
                }
            }
            public void RectPatch(float x0,float x1,float z0,float z1,int channel,float amount,float feather=.28f)
            {
                var area=UnityEngine.Rect.MinMaxRect(x0-1,z0-1,x1+1,z1+1);
                Paint(area,p=>
                {
                    float d=Mathf.Min(Mathf.Min(p.x-x0,x1-p.x),Mathf.Min(p.y-z0,z1-p.y));
                    // Sub-metre chipped boundaries, never a cloud over the yard.
                    float chip=(Mathf.PerlinNoise(p.x*1.7f+13.1f,p.y*1.9f+4.7f)-.5f)*.26f;
                    return Mathf.SmoothStep(0,1,Mathf.Clamp01((d+chip+feather)/(feather*2)));
                },channel,amount);
            }
            public void Oval(float x,float z,float rx,float rz,int channel,float amount,float yaw=0)
            {
                float radius=Mathf.Max(rx,rz)+1,c=Mathf.Cos(yaw*Mathf.Deg2Rad),s=Mathf.Sin(yaw*Mathf.Deg2Rad);
                Paint(new Rect(x-radius,z-radius,radius*2,radius*2),p=>
                {
                    var d=p-new Vector2(x,z);float a=(d.x*c+d.y*s)/rx,b=(-d.x*s+d.y*c)/rz;
                    float v=1-Mathf.Sqrt(a*a+b*b);
                    float grain=.82f+.18f*Mathf.PerlinNoise(p.x*.72f+3,p.y*.83f+17);
                    return Mathf.SmoothStep(0,1,Mathf.Clamp01(v/.34f))*grain;
                },channel,amount);
            }
            public void Line(Vector2 a,Vector2 b,float radius,int channel,float amount)
            {
                var area=UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x,b.x)-radius-1,Mathf.Min(a.y,b.y)-radius-1,Mathf.Max(a.x,b.x)+radius+1,Mathf.Max(a.y,b.y)+radius+1);
                Vector2 direction=b-a;float denominator=Mathf.Max(direction.sqrMagnitude,.001f);
                Paint(area,p=>
                {
                    float t=Mathf.Clamp01(Vector2.Dot(p-a,direction)/denominator);
                    float distance=Vector2.Distance(p,a+direction*t);
                    return Mathf.SmoothStep(0,1,Mathf.Clamp01((radius-distance)/Mathf.Max(.25f,radius*.35f)));
                },channel,amount);
            }
            public void WheelRoute(Vector2[] path,float halfTrack=1.35f,float amount=.76f)
            {
                if(path.Length<2)return;
                // Contact-patch width is independent of the broad compacted
                // traffic lane. Smooth continuous offsets prevent angular,
                // ruler-drawn double lines at each authored route waypoint.
                var smooth=new List<Vector2>();
                for(int i=0;i<path.Length-1;i++)
                {
                    Vector2 p0=i==0?path[i]*2-path[i+1]:path[i-1],p1=path[i],p2=path[i+1];
                    Vector2 p3=i+2<path.Length?path[i+2]:p2*2-p1;
                    int steps=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(p1,p2)/1.4f));
                    for(int j=0;j<steps;j++)
                    {
                        float t=(float)j/steps,t2=t*t,t3=t2*t;
                        smooth.Add(.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3));
                    }
                }
                smooth.Add(path[path.Length-1]);
                var distance=new float[smooth.Count];var offset=new Vector2[smooth.Count];
                for(int i=0;i<smooth.Count;i++)
                {
                    if(i>0)distance[i]=distance[i-1]+Vector2.Distance(smooth[i-1],smooth[i]);
                    var direction=(smooth[Mathf.Min(i+1,smooth.Count-1)]-smooth[Mathf.Max(i-1,0)]).normalized;
                    offset[i]=new Vector2(-direction.y,direction.x)*halfTrack;
                }
                for(int i=1;i<smooth.Count;i++)
                {
                    Line(smooth[i-1],smooth[i],halfTrack+1.1f,1,amount*.11f);
                    TyreSegment(smooth[i-1]-offset[i-1],smooth[i]-offset[i],.35f,distance[i-1],distance[i],distance[distance.Length-1],amount,7.3f);
                    TyreSegment(smooth[i-1]+offset[i-1],smooth[i]+offset[i],.32f,distance[i-1],distance[i],distance[distance.Length-1],amount*.84f,19.7f);
                }
            }
            void TyreSegment(Vector2 a,Vector2 b,float radius,float start,float end,float total,float amount,float phase)
            {
                var area=UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x,b.x)-radius-1,Mathf.Min(a.y,b.y)-radius-1,Mathf.Max(a.x,b.x)+radius+1,Mathf.Max(a.y,b.y)+radius+1);
                Vector2 delta=b-a;float denominator=Mathf.Max(delta.sqrMagnitude,.001f);
                Paint(area,p=>
                {
                    float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/denominator),along=Mathf.Lerp(start,end,t);
                    float d=Vector2.Distance(p,a+delta*t);
                    float contact=Mathf.SmoothStep(0,1,Mathf.Clamp01((radius-d)/.18f));
                    float worn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.76f,Mathf.PerlinNoise(along*.115f,phase)));
                    float shortWear=.48f+.52f*Mathf.PerlinNoise(along*.73f,phase+11);
                    float tip=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(along,total-along)/3.5f));
                    return contact*(.08f+.92f*worn)*shortWear*tip;
                },1,amount);
            }
            public void Bend(float x,float z,float radius,float start,float end,float amount=.74f)
            {
                int n=Mathf.CeilToInt(Mathf.Abs(end-start)/7);var points=new Vector2[n+1];
                for(int i=0;i<=n;i++){float a=Mathf.Lerp(start,end,(float)i/n)*Mathf.Deg2Rad;points[i]=new Vector2(x+Mathf.Cos(a)*radius,z+Mathf.Sin(a)*radius);}
                WheelRoute(points,1.35f,amount);
            }
            public Texture2D Save()
            {
                string path=PurposeGroundFolder+"/Masks/"+id+".asset";
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,true){name=id,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
                texture.SetPixels32(pixels);texture.Apply(true,false);
                var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(saved==null){AssetDatabase.CreateAsset(texture,path);saved=texture;}
                else{EditorUtility.CopySerialized(texture,saved);Obj.DestroyImmediate(texture);EditorUtility.SetDirty(saved);}
                return saved;
            }
        }

        static void AuthoredZ1Ground(GroundPurposeCanvas c)
        {
            // Z1 departure: selected replacement pours beside the old service
            // circulation, not a repeating stain on every concrete rectangle.
            c.RectPatch(-60,-44,16,32,0,.93f);c.RectPatch(-44,-28,64,80,0,.81f);
            c.RectPatch(20,36,0,16,0,.65f);c.RectPatch(38,59,80,96,0,.75f);
            c.RectPatch(-56,-32,2,10,1,.24f,1);
            c.WheelRoute(new[]{new Vector2(-48,86),new Vector2(-48,45),new Vector2(-39,21),new Vector2(-18,19)});
            c.Bend(37,60,13,-90,20,.55f);
            c.WheelRoute(new[]{new Vector2(-212,44),new Vector2(-146,44),new Vector2(-139,34)},1.35f,.8f);
            // B02 external apron: the east passage, personnel access at z25.6,
            // parts laydown west of the workshop, and the actual z24 trench.
            c.RectPatch(-81,-61,9,20,0,.88f);c.RectPatch(-130,-118,47,66,0,.79f);
            c.RectPatch(-100,-88,10,22,0,.66f);c.RectPatch(-150,-136,18,27,0,.55f);
            c.WheelRoute(new[]{new Vector2(-61,83),new Vector2(-66,64),new Vector2(-66,29),new Vector2(-71,15),new Vector2(-90,14)},1.2f,.68f);
            c.Line(new Vector2(-101,23),new Vector2(-63,23),1.35f,2,.57f);
            c.Line(new Vector2(-62,12),new Vector2(-62,80),1.45f,2,.42f);
            foreach(float z in new[]{24f,45f,70f})c.Oval(-63,z,2.6f,z==24?4.4f:6,2,.95f);
            c.Line(new Vector2(-127,70),new Vector2(-126,82),2.8f,2,.62f);
            c.RectPatch(-146,-138,22,29,3,.7f,.7f);c.RectPatch(-145,-139,33,37,3,.52f,.5f);
            // Interior foundation top is one metre higher; world masks preserve
            // height and never lay a floating decal across its step.
            c.RectPatch(-87,-77,29,34,0,.7f);c.Oval(-78,33,1.6f,1.1f,1,.86f);
            c.Oval(-81,32,3.0f,1.5f,3,.75f);c.Oval(-74,35,1.4f,3.3f,2,.28f);
            // Real drainage destinations coordinated with the edge-geometry pass.
            foreach(float x in new[]{-28f,28f})
            {c.Oval(x,98.8f,3.8f,1.8f,2,.85f);c.Line(new Vector2(x,91),new Vector2(x,98.8f),1.2f,2,.38f);}
            c.Line(new Vector2(17.5f,104),new Vector2(17.5f,176),1.8f,2,.62f);
            c.WheelRoute(new[]{new Vector2(-4,104),new Vector2(-4,176)},1.45f,.42f);
            c.RectPatch(-14,-7,124,137,0,.94f);c.RectPatch(5,14,158,170,0,.84f);
            // Pump frontage and local patches follow service doors/pipe runoff.
            c.RectPatch(97,113,18,27,0,.92f);c.RectPatch(64,78,65,77,0,.64f);
            c.WheelRoute(new[]{new Vector2(112,90),new Vector2(112,73),new Vector2(108,66)},1.2f,.81f);
            c.Line(new Vector2(120,18),new Vector2(120,78),1.9f,2,.62f);
            c.Oval(117,62,3,7,2,.85f);c.RectPatch(98,110,42,47,3,.32f,.6f);
            // Incoming damaged bearing is stock-related: a few rectangular
            // support shadows and a clear carrier turning aisle, not full dirt.
            c.RectPatch(124,153,-103,-73,0,.74f);c.RectPatch(177,191,-96,-78,3,.8f,.8f);
            c.RectPatch(125,137,-51,-44,3,.6f,.7f);
            c.WheelRoute(new[]{new Vector2(234,-70),new Vector2(207,-70),new Vector2(196,-58),new Vector2(175,-57)},1.8f,.8f);
            c.WheelRoute(new[]{new Vector2(221,-107),new Vector2(221,-74)},1.4f,.9f);
            c.Line(new Vector2(237,-110),new Vector2(237,-30),2.2f,2,.65f);
        }

        static void AuthoredZ2Ground(GroundPurposeCanvas c)
        {
            c.RectPatch(-108,-76,196,209,0,.9f);c.RectPatch(-42,-24,303,325,0,.88f);
            c.RectPatch(60,91,336,354,0,.8f);c.RectPatch(-80,-62,249,263,0,.72f);
            c.WheelRoute(new[]{new Vector2(-107,270),new Vector2(-78,270),new Vector2(-61,251),new Vector2(-61,211)},1.6f,.9f);
            c.WheelRoute(new[]{new Vector2(106,270),new Vector2(74,270),new Vector2(56,291),new Vector2(56,333)},1.6f,.7f);
            c.Bend(-18,238,19,0,110,.7f);
            // Storage support imprints remain peripheral to the boss arena.
            foreach(float z in new[]{208f,224f,241f})
            {c.RectPatch(-102,-83,z,z+8,1,.28f,.6f);c.RectPatch(-100,-86,z+1,z+2.2f,3,.78f);c.RectPatch(-100,-86,z+5,z+6.2f,3,.62f);}
            c.WheelRoute(new[]{new Vector2(-232,210),new Vector2(-200,210),new Vector2(-191,225)},1.5f,.85f);
            c.RectPatch(-198,-181,174,192,0,.86f);c.RectPatch(-220,-202,249,268,3,.7f,.7f);
            c.Line(new Vector2(-203,228),new Vector2(-203,292),2.1f,2,.71f);
            c.Oval(-105,335,5,10,2,.82f);
            c.RectPatch(99,117,110,126,0,.95f);c.RectPatch(35,49,156,174,0,.75f);
            c.WheelRoute(new[]{new Vector2(108,92),new Vector2(108,120),new Vector2(99,130)},1.3f,.8f);
            c.RectPatch(88,102,180,205,3,.5f,1);c.Oval(94,210,8,11,1,.52f);
            c.RectPatch(-173,-155,314,325,0,.76f);c.Line(new Vector2(-175,340),new Vector2(-158,340),2,1,.35f);
        }

        static void AuthoredZ3Ground(GroundPurposeCanvas c)
        {
            // Selected crane hardstand replacement pours and carrier service
            // routes leave the neck/lure and evacuation footprint unmarked.
            c.RectPatch(-28,-12,466,498,0,.87f);c.RectPatch(162,194,578,594,0,.84f);
            c.RectPatch(2,18,610,642,0,.72f);c.RectPatch(114,146,626,642,0,.9f);
            c.WheelRoute(new[]{new Vector2(203,632),new Vector2(177,632),new Vector2(159,616),new Vector2(159,580)},1.8f,.8f);
            c.WheelRoute(new[]{new Vector2(-22,468),new Vector2(-22,574),new Vector2(-7,591)},1.5f,.7f);
            c.Oval(-22,620,5,13,2,.91f);
            foreach(float x in new[]{53f,141f})
            {c.RectPatch(x-3,x+3,539,571,3,.5f,.7f);c.RectPatch(x-5,x+5,535,546,0,.8f);}
            c.RectPatch(-143,-127,540,557,0,.88f);c.Line(new Vector2(-139,555),new Vector2(-153,555),2,1,.5f);
            c.RectPatch(88,115,659,674,0,.91f);c.RectPatch(139,160,731,755,0,.78f);
            c.WheelRoute(new[]{new Vector2(243,745),new Vector2(164,745),new Vector2(153,733)},1.8f,.8f);
            // Operating and damaged berth have distinct traces. Drainage is
            // landward, with salt wetting at the sea edge rather than full wet.
            c.Line(new Vector2(364,-94),new Vector2(364,780),2.3f,2,.72f);
            c.Line(new Vector2(426,-92),new Vector2(426,778),2.3f,2,.48f);
            c.RectPatch(388,406,558,576,0,.8f);c.RectPatch(410,426,594,612,0,.72f);
            c.WheelRoute(new[]{new Vector2(350,565),new Vector2(401,565),new Vector2(407,578)},1.5f,.9f);
            foreach(float z in new[]{586f,599f,631f,139f})c.RectPatch(393,405,z-1,z+1.3f,3,.79f,.6f);
            c.RectPatch(403,427,404,410,0,.78f);c.Line(new Vector2(438,425),new Vector2(429,419),2.4f,3,.76f);
            c.Oval(431,426,4,6,2,.75f);
        }

        static void AuthoredServiceGround(GroundPurposeCanvas c)
        {
            foreach(float x in new[]{-235f,-355f,347f})
                c.WheelRoute(new[]{new Vector2(x,-75),new Vector2(x,790)},1.6f,.55f);
            foreach(float z in new[]{90f,270f,630f})
                c.WheelRoute(new[]{new Vector2(-232,z),new Vector2(325,z)},1.45f,.48f);
            c.Line(new Vector2(-222,-68),new Vector2(-222,798),1.7f,2,.45f);
            c.Line(new Vector2(-363,-22),new Vector2(-363,783),1.8f,2,.42f);
            foreach(float z in new[]{42f,209f,340f,555f})c.RectPatch(-241,-229,z-6,z+6,0,.88f);
            // Compaction on the existing aggregate shoulders has continuity
            // with the roads; no invented full-site craters or tyre networks.
            c.Line(new Vector2(-249,-70),new Vector2(-249,794),3.0f,1,.3f);
            c.Line(new Vector2(330,-90),new Vector2(330,782),3.0f,1,.27f);
        }

        static bool PurposeGroundCandidate(MeshRenderer r,Material m)
        {
            if(m==null||m.shader==null||!r.gameObject.scene.IsValid())return false;
            // Boundary detail owns its own small-scale materials and is rebuilt
            // after this pass. Do not derive extra materials from it on reruns.
            if(r.GetComponentsInParent<Transform>().Any(t=>t.name=="Ground edge completion Z1"))return false;
            if(m.shader.name==PurposeGroundShader)return true;
            string n=r.name.ToLowerInvariant();var b=r.bounds;
            if(n=="foundation"&&b.center.x>-125&&b.center.x<-70&&b.center.z>25&&b.center.z<65&&b.max.y<1.1f)return true;
            if(n.StartsWith("industrial cross road",StringComparison.Ordinal)&&b.max.y<.1f)return true;
            if(m.shader.name!="Anseong/GeologicalGround"||m.GetFloat("_GroundSurface")<.5f)return false;
            // Sea faces, drainage channels, raised wheel stops and retaining
            // structures keep their existing materials. This is ground only.
            if(b.max.y>.21f||b.size.y>1.1f||n.Contains("stormwater")||n.Contains("section")||n.Contains("sleeper")||n.Contains("wheel stop"))return false;
            return true;
        }
        static string PurposeGroundPath(Transform t)
        {return t.parent==null?t.name:PurposeGroundPath(t.parent)+"/"+t.name;}
        static void PurposeFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');PurposeFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static float PurposeBay(string name)
        {
            if(name.StartsWith("Z1 departure",StringComparison.Ordinal)||name.StartsWith("Z3 crane yard",StringComparison.Ordinal))return 16;
            if(name.Contains("Harbor reclaimed"))return 18;
            if(name.Contains("B02 repair")||name.Contains("B03 pump")||name.Contains("B06 pressure"))return 10;
            if(name.Contains("B04 casting")||name.Contains("B08 furnace")||name.Contains("standby apron"))return 14;
            if(name.Contains("heavy component receiving"))return 16;
            if(name.Contains("incoming parts"))return 11;
            return 12;
        }

        /// <summary>
        /// Call after landscape integration and building material assignment,
        /// before final bake/capture. Works on an already loaded saved scene;
        /// does not open/save the scene, run Unity, or change any collider.
        /// Source materials remain untouched; stable derived assets are updated.
        /// </summary>
        public static void ApplyGroundSurfaceUpgrade()
        {
            var shader=Shader.Find(PurposeGroundShader);if(shader==null)throw new InvalidOperationException("PurposeGround shader missing.");
            PurposeFolder(PurposeGroundFolder+"/Masks");PurposeFolder(PurposeGroundFolder+"/Materials");
            var global=new GroundPurposeCanvas("Plant_authored_surface_use",new Rect(-510,-210,980,1130),1536,2048);
            var z1=new GroundPurposeCanvas("Z1_authored_surface_use",new Rect(-260,-140,530,340),1536,1024);
            AuthoredZ1Ground(global);AuthoredZ2Ground(global);AuthoredZ3Ground(global);AuthoredServiceGround(global);
            AuthoredZ1Ground(z1);AuthoredZ2Ground(z1);AuthoredServiceGround(z1);
            Texture2D globalMask=global.Save(),z1Mask=z1.Save();int assignments=0;
            var report=new StringBuilder("renderer,source_material,derived_material,mask,slab_metres,physical_repeat_metres\n");
            foreach(var r in Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var materials=r.sharedMaterials;bool changed=false;
                for(int slot=0;slot<materials.Length;slot++)
                {
                    var previous=materials[slot];if(!PurposeGroundCandidate(r,previous))continue;
                    string sourcePath=previous.shader.name==PurposeGroundShader?previous.GetTag("PurposeGroundSource",false):AssetDatabase.GetAssetPath(previous);
                    var source=AssetDatabase.LoadAssetAtPath<Material>(sourcePath);if(source==null)throw new InvalidOperationException("Ground source material missing: "+sourcePath);
                    var bounds=r.bounds;bool close=z1.rect.Contains(new Vector2(bounds.min.x,bounds.min.z))&&z1.rect.Contains(new Vector2(bounds.max.x,bounds.max.z));
                    var canvas=close?z1:global;var mask=close?z1Mask:globalMask;
                    string id=Hash128.Compute(PurposeGroundPath(r.transform)+"/"+slot+"/"+sourcePath).ToString().Substring(0,16);
                    string path=PurposeGroundFolder+"/Materials/Ground_"+id+".mat";
                    var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
                    bool geological=source.shader.name=="Anseong/GeologicalGround";
                    string sourceName=source.name.ToLowerInvariant();bool asphalt=sourceName.Contains("asphalt"),slag=sourceName.Contains("slag"),harbor=sourceName.Contains("harbor");
                    foreach(string property in new[]{"_GravelTex","_GravelNormal","_GravelRoughness"})
                    {
                        string channel=property.EndsWith("Normal",StringComparison.Ordinal)?"NormalGL":property.EndsWith("Roughness",StringComparison.Ordinal)?"Roughness":"Color";
                        m.SetTexture(property,geological?source.GetTexture(property):GroundChannel(asphalt?"Road015C":"Concrete040",channel));
                    }
                    string replacement=asphalt?"Road015C":slag?"Ground014":"Concrete030";
                    m.SetTexture("_RockTex",GroundChannel(replacement,"Color"));m.SetTexture("_RockNormal",GroundChannel(replacement,"NormalGL"));m.SetTexture("_RockRoughness",GroundChannel(replacement,"Roughness"));
                    m.SetTexture("_PurposeMask",mask);m.SetVector("_MaskRect",new Vector4(canvas.rect.xMin,canvas.rect.yMin,1/canvas.rect.width,1/canvas.rect.height));
                    float metres=geological?source.GetFloat("_GravelMetres"):asphalt?4:1.4f;
                    m.SetFloat("_GravelMetres",metres);m.SetFloat("_RockMetres",asphalt?4:slag?1.7f:3.5f);
                    m.SetColor("_Color",asphalt?new Color(.57f,.59f,.61f):slag?new Color(.92f,.89f,.82f):harbor?new Color(.90f,.94f,.94f):new Color(.94f,.95f,.93f));
                    m.SetColor("_RepairColor",asphalt?new Color(.48f,.50f,.53f):slag?new Color(.77f,.72f,.63f):new Color(.99f,.96f,.88f));
                    m.SetColor("_ResidueColor",new Color(.25f,.15f,.075f));
                    m.SetFloat("_Saturation",slag?.88f:.94f);m.SetFloat("_SlabVariation",harbor?.16f:.20f);m.SetFloat("_SlabEnabled",asphalt||slag?0:1);
                    m.SetFloat("_SeamStrength",r.name.Equals("Foundation",StringComparison.Ordinal)?.3f:0);
                    float bay=PurposeBay(r.name);m.SetVector("_SlabOrigin",new Vector4(bounds.min.x,bounds.min.z,0,0));m.SetVector("_SlabMetres",new Vector4(bay,bay,0,0));
                    m.SetVector("_SurfaceRect",new Vector4(bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z));
                    m.SetFloat("_NormalStrength",slag?.64f:asphalt?.52f:.48f);m.SetFloat("_RoughnessScale",slag?1.18f:1.1f);
                    m.SetFloat("_WearDarkening",asphalt?.23f:slag?.28f:.35f);m.SetFloat("_Wetness",harbor?.08f:.025f);m.SetFloat("_WetGain",harbor?.77f:.72f);
                    m.SetFloat("_RepairGain",asphalt?.9f:1.10f);m.SetFloat("_EdgeDust",slag?0:asphalt?.14f:.06f);m.SetFloat("_GroundSurface",1);
                    m.SetFloat("_RepairColorBlend",asphalt||slag?1:.45f);
                    m.SetOverrideTag("PurposeGroundSource",sourcePath);m.enableInstancing=true;EditorUtility.SetDirty(m);
                    materials[slot]=m;changed=true;assignments++;
                    report.AppendLine(r.name.Replace(',','_')+","+source.name+","+path+","+canvas.id+","+bay.ToString(Inv)+","+metres.ToString(Inv));
                }
                if(changed){r.sharedMaterials=materials;EditorUtility.SetDirty(r);}
            }
            if(assignments==0)throw new InvalidOperationException("No ground renderers found. Load WideRuins scene before the ground pass.");
            Directory.CreateDirectory(WideOutput);File.WriteAllText(WideOutput+"/purpose-ground-assignments.csv",report.ToString());
            AssetDatabase.SaveAssets();
            Debug.Log("PURPOSE_GROUND_APPLIED renderer_slots="+assignments+" masks=2 physical_repeat_preserved=true colliders_unchanged=true");
        }
    }
}
