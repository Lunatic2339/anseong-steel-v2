using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Authored spatial-review creature, not a final rigged/AI-generated character.
        // All geometry is boss-local metres. Root may move; no geometry is baked into world coordinates.
        static Transform BuildScenarioBossPose(string id,Vector3 origin,string pose,bool z3,Transform parent)
        {
            string[] supported={"arrival","defend","shell_open","neck","windup","recovery","hit_follow","miss_follow"};
            if(!supported.Contains(pose))throw new ArgumentException("Unknown spatial boss pose: "+pose);
            var old=parent.Find(id);if(old!=null)Obj.DestroyImmediate(old.gameObject);
            var root=new GameObject(id).transform;root.SetParent(parent,false);root.position=origin;
            bool damaged=pose=="shell_open"||pose=="neck"||pose=="recovery"||pose=="hit_follow"||pose=="miss_follow";
            bool neckOpen=pose=="neck"||pose=="recovery"||pose=="hit_follow"||pose=="miss_follow";
            bool hit=pose=="hit_follow",miss=pose=="miss_follow",windup=pose=="windup",recovery=pose=="recovery";
            bool lifted=pose=="defend"||windup;
            float bodyY=hit?24:miss?27:neckOpen?25:26;
            var shell=ScenarioBossMaterial("chitin",new Color(.30f,.38f,.34f),"Metal063",.04f,.23f,3.7f);
            var shellPale=ScenarioBossMaterial("worn_carapace",new Color(.53f,.56f,.45f),"Metal049A",.05f,.2f,5f);
            var skin=ScenarioBossMaterial("joint_tissue",new Color(.24f,.19f,.145f),"Concrete006",0,.3f,2.5f);
            var iron=ScenarioBossMaterial("ingested_iron",new Color(.49f,.38f,.25f),"rust_coarse_01",.13f,.4f,2.8f);
            var tendon=ScenarioBossMaterial("ribbed_tendon",new Color(.39f,.35f,.27f),"Metal027",0,.18f,2.8f);
            var horn=ScenarioBossMaterial("ivory_claw",new Color(.55f,.53f,.40f),"Concrete006",0,.24f,3f);
            var nerve=Material("ScenarioBoss_exposed_nerve",new Color(.85f,.24f,.075f));
            nerve.SetFloat("_Metallic",0);nerve.SetFloat("_Glossiness",.3f);nerve.EnableKeyword("_EMISSION");nerve.SetColor("_EmissionColor",new Color(1.2f,.22f,.035f));
            var wound=Material("ScenarioBoss_nerve_recess",new Color(.075f,.035f,.025f));wound.SetFloat("_Glossiness",.4f);
            var body=ScenarioBossPart(root,"Body and separated dorsal plates",id+"_body");
            var flesh=ScenarioBossPart(root,"Joint membranes and belly clearance",id+"_flesh");
            var limbs=ScenarioBossPart(root,"Four articulated support limbs",id+"_limbs");
            var head=ScenarioBossPart(root,"Small forward sensory head",id+"_head");
            var broken=ScenarioBossPart(root,"Displaced shell flaps",id+"_flaps");
            // The central belly stays above 11m (15m for miss-follow), leaving the 4m coil clear.
            // Smaller skull replaces the old 24x20m sphere; view-critical throat protrudes separately.
            var bodyCenter=new Vector3(hit?-1.2f:0,bodyY,3);
            flesh.Loft(bodyCenter,new[]{-24f,-18f,-6f,8f,22f,31f},new[]{5f,11.5f,13.5f,13.2f,10f,3f},
                new[]{4f,9f,12f,11f,8f,3f},skin,24,hit?-5f:0);
            for(int section=0;section<5;section++)
            {
                float z=-15+section*9.2f;float width=section==0?11.7f:section==4?8.6f:13.9f;
                float y=bodyY+(section==0?8.8f:section==4?6.6f:11.2f);
                float tilt=hit?-8f:0;
                if(hit&&section==2)
                {
                    // Hit damage is retained as a broken central arch, never a restored whole shell.
                    body.Wedge(new Vector3(-8.6f,y-2,z),new Vector3(8.5f,3,8.4f),shellPale,new Vector3(0,0,-19));
                    body.Wedge(new Vector3(8.6f,y+1,z),new Vector3(8.5f,3,8.4f),iron,new Vector3(0,0,23));
                    flesh.Ellipsoid(new Vector3(0,y-1,z),new Vector3(7,2.1f,6),wound,20,10);
                }
                else body.Wedge(new Vector3(bodyCenter.x,y,z),new Vector3(width*2,3.7f,8.4f),section%2==0?shell:shellPale,new Vector3(-4+section,0,tilt));
                if(section<4)
                {
                    body.Wedge(new Vector3(-width*.44f,y+2.9f,z+1),new Vector3(4.4f,2.2f,5),iron,new Vector3(12,17,-8));
                    body.Wedge(new Vector3(width*.34f,y+3.1f,z-1),new Vector3(4.2f,2,4.2f),shellPale,new Vector3(8,-19,9));
                }
            }
            // Side ribs remain below the exposed flank targets, preserving a real shooting surface.
            for(int side=-1;side<=1;side+=2)
            {
                for(int rib=0;rib<5;rib++)
                {
                    float z=-11+rib*8.2f;
                    flesh.Segment(new Vector3(side*9,bodyY-1,z),new Vector3(side*14.6f,bodyY+5,z+1),1.15f,.8f,tendon,12);
                    body.Wedge(new Vector3(side*12.6f,bodyY+2,z),new Vector3(4.3f,8,6.5f),shell,new Vector3(0,side*10,side*18));
                }
                for(int fore=0;fore<2;fore++)
                {
                    bool front=fore==0;float z=front?-20:23;
                    var hip=new Vector3(side*11.6f,bodyY-1,z);
                    var elbow=new Vector3(side*16.1f,front?16.5f:14.5f,z+(front?-3:2));
                    var foot=new Vector3(side*16,1.1f,z+(front?-5:2));
                    if(front&&lifted&&(windup||side<0))
                    {elbow=new Vector3(side*15,35,-22);foot=new Vector3(side*8.4f,windup?33:25,-33);}
                    else if(front&&recovery)
                    {elbow=new Vector3(side*10.8f,13.5f,-29);foot=new Vector3(side*6.5f,1.1f,-38.5f);}
                    else if(front&&hit)
                    {elbow=new Vector3(side*15.6f,14.3f,-22);foot=new Vector3(side*16,1.1f,-25);}
                    flesh.Ellipsoid(hip,new Vector3(5.6f,5.1f,5.6f),skin,16,9);
                    limbs.Segment(hip,elbow,3.6f,2.7f,shell,12);
                    flesh.Ellipsoid(elbow,new Vector3(5,5.2f,5),tendon,16,9);
                    limbs.Segment(elbow,foot+Vector3.up*1.4f,2.8f,1.8f,shellPale,12);
                    limbs.Wedge(foot,new Vector3(5.6f,2.1f,6.2f),shell,new Vector3(0,side*8,0));
                    for(int claw=0;claw<3;claw++)
                    {
                        var basePoint=foot+new Vector3((claw-1)*1.35f,-.25f,-2.2f);
                        limbs.Segment(basePoint,basePoint+new Vector3(0,-.5f,-1.9f),.58f,.10f,horn,10);
                    }
                }
            }
            float headY=neckOpen?31:windup?34:33;
            var skull=new Vector3(0,headY,-26.8f);
            flesh.Segment(new Vector3(0,bodyY+1,-19),skull+new Vector3(0,0,2),4.7f,4.2f,skin,20);
            head.Loft(skull,new[]{-6.2f,-3.6f,2f,6f},new[]{2.3f,5.8f,5.4f,3.8f},new[]{2.2f,3.7f,4.3f,3.6f},shellPale,24,0);
            head.Wedge(skull+new Vector3(0,3.6f,0),new Vector3(9,2.3f,9),shell,new Vector3(-5,0,0));
            for(int side=-1;side<=1;side+=2)
            {
                head.Segment(skull+new Vector3(side*3.4f,-1,-4.4f),skull+new Vector3(side*4.7f,-2.5f,-7.1f),.7f,.16f,horn,12);
                // Sensory slits are small anatomy accents, not a second objective-colour marker.
                head.Ellipsoid(skull+new Vector3(side*4.8f,1.3f,-3.8f),new Vector3(.6f,.5f,1.8f),wound,12,6);
            }
            // Connect shoulder to the exact front plate anchor, rather than float a target sphere.
            var t1=new Vector3(-12,32,-30);
            flesh.Segment(new Vector3(-12,bodyY,-20),t1+new Vector3(0,0,2.1f),3.6f,3.2f,skin,16);
            var t1root=ScenarioBossTarget(root,"Target T1",t1,Vector3.back,id+"_T1",iron,shell,3.8f,3.4f,.75f);
            if(damaged||z3)
            {
                // Existing damage survives every later pose. Exposed nerve becomes the support target.
                t1root.gameObject.SetActive(false);
                broken.Wedge(t1+new Vector3(-3,1.6f,2.8f),new Vector3(3.2f,5.2f,1.3f),iron,new Vector3(12,-28,-14));
                broken.Wedge(t1+new Vector3(3,-1.6f,2.3f),new Vector3(3.1f,3.3f,1),shell,new Vector3(-18,27,17));
            }
            int nerveSide=z3?1:-1;
            var t2=new Vector3(nerveSide*20,34,-17);
            var nerveSeat=new Vector3(nerveSide*16.6f,34,-17);
            flesh.Segment(new Vector3(nerveSide*12.4f,bodyY+2,-13),nerveSeat,3.8f,2.5f,skin,20);
            flesh.Segment(nerveSeat,t2-new Vector3(nerveSide*1.1f,0,0),2.5f,2.8f,skin,20);
            var t2root=ScenarioBossTarget(root,"Target T2",t2,Vector3.right*nerveSide,id+"_T2",nerve,wound,3,3.7f,1.1f);
            t2root.gameObject.SetActive(damaged);
            if(damaged)
            {
                // Two opened plates frame the recess without covering its normal-facing centre.
                broken.Wedge(t2+new Vector3(-nerveSide*2.3f,5.4f,-1.7f),new Vector3(2.2f,4.1f,7.8f),shellPale,new Vector3(0,0,nerveSide*28));
                broken.Wedge(t2+new Vector3(-nerveSide*1.5f,-4.8f,1.2f),new Vector3(1.9f,3,7),iron,new Vector3(0,0,-nerveSide*23));
            }
            else
            {
                body.Wedge(t2-new Vector3(nerveSide*1.2f,0,0),new Vector3(1.6f,8.6f,8.8f),shell,new Vector3(0,0,nerveSide*4));
            }
            var throat=new Vector3(0,28,z3?-43:-37);
            var throatBase=skull+new Vector3(0,-2,-5);
            if(neckOpen)
            {
                flesh.Segment(throatBase,throat+new Vector3(0,0,1.2f),2.6f,2.7f,skin,24);
                // Short rings across a taper communicate stretched tissue, not an additional pipe.
                for(int ring=1;ring<5;ring++)
                {
                    var p=Vector3.Lerp(throatBase,throat+new Vector3(0,0,1.4f),ring/5f);
                    flesh.Segment(p+new Vector3(-2.5f,0,.18f),p+new Vector3(2.5f,0,.18f),.25f,.25f,tendon,8);
                }
            }
            var neckRoot=ScenarioBossTarget(root,"Target Neck",throat,Vector3.back,id+"_neck",nerve,wound,3.5f,2.7f,.9f);
            neckRoot.gameObject.SetActive(neckOpen);
            body.Finish(shell);flesh.Finish(skin);limbs.Finish(shellPale);head.Finish(shell);broken.Finish(iron);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer=z3?12:11;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            }
            return root;
        }

        static Material ScenarioBossMaterial(string name,Color tint,string texture,float metal,float bump,float tile)
        {
            var result=PbrMaterial("ScenarioBoss_"+name,texture,tint,tile,metal,bump);
            result.SetFloat("_RoughnessFloor",.55f);result.SetFloat("_SurfaceCalm",.12f);
            result.SetFloat("_GrimeStrength",.12f);result.SetFloat("_Saturation",.8f);
            return result;
        }

        static ScenarioBossGeometry ScenarioBossPart(Transform parent,string name,string asset)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);
            return new ScenarioBossGeometry(t,asset);
        }

        static Transform ScenarioBossTarget(Transform parent,string name,Vector3 point,Vector3 outward,string asset,Material exposed,Material recess,float radiusX,float radiusY,float depth)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=point;
            t.localRotation=Quaternion.FromToRotation(Vector3.back,outward);
            var g=new ScenarioBossGeometry(t,asset);
            // Frontmost face passes through the exact anchor; the whole lump extends into its anatomy.
            g.Loft(new Vector3(0,0,0),new[]{0f,.22f,depth,depth*1.5f},new[]{radiusX*.76f,radiusX,radiusX*.9f,radiusX*.65f},new[]{radiusY*.76f,radiusY,radiusY*.9f,radiusY*.65f},exposed,24,0);
            for(int i=0;i<5;i++)
            {
                float x=(i-2)*radiusX*.32f;
                g.Segment(new Vector3(x,-radiusY*.61f,.1f),new Vector3(x,radiusY*.61f,.1f),.08f,.08f,recess,8);
            }
            g.Finish(exposed);
            return t;
        }

        // A tiny local-space authored mesh builder with physical-metre UVs and per-material batches.
        sealed class ScenarioBossGeometry
        {
            sealed class Part { public readonly List<Vector3> vertices=new List<Vector3>(); public readonly List<Vector2> uv=new List<Vector2>(); public readonly List<int> indices=new List<int>(); }
            readonly Dictionary<Material,Part> parts=new Dictionary<Material,Part>();
            readonly Transform root;readonly string asset;
            public ScenarioBossGeometry(Transform root,string asset){this.root=root;this.asset=asset;}
            Part Data(Material material){if(!parts.TryGetValue(material,out var p)){p=new Part();parts.Add(material,p);}return p;}
            void Face(Material mat,Vector3 normal,params Vector3[] points)
            {
                // A cap may receive a Loft/Segment ring. Reverse a copy so the next side
                // faces keep the matching vertex order of both rings.
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)points=points.Reverse().ToArray();
                var p=Data(mat);int first=p.vertices.Count;
                var tangent=Vector3.Cross(normal,Mathf.Abs(Vector3.Dot(normal.normalized,Vector3.up))>.95f?Vector3.forward:Vector3.up).normalized;
                var bitangent=Vector3.Cross(normal.normalized,tangent);
                foreach(var v in points){p.vertices.Add(v);p.uv.Add(new Vector2(Vector3.Dot(v,tangent),Vector3.Dot(v,bitangent)));}
                for(int i=1;i<points.Length-1;i++)p.indices.AddRange(new[]{first,first+i,first+i+1});
            }
            public void Loft(Vector3 center,float[] z,float[] xradius,float[] yradius,Material mat,int sides,float roll)
            {
                var q=Quaternion.Euler(0,0,roll);var rings=new Vector3[z.Length][];
                for(int i=0;i<z.Length;i++)
                {
                    rings[i]=new Vector3[sides];
                    for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;rings[i][j]=center+q*new Vector3(Mathf.Cos(a)*xradius[i],Mathf.Sin(a)*yradius[i],z[i]);}
                }
                Face(mat,q*Vector3.back,rings[0]);Face(mat,q*Vector3.forward,rings[rings.Length-1]);
                for(int i=0;i<rings.Length-1;i++)for(int j=0;j<sides;j++)
                {
                    int k=(j+1)%sides;float a=(j+.5f)*Mathf.PI*2/sides;
                    Face(mat,q*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]);
                }
            }
            public void Ellipsoid(Vector3 center,Vector3 size,Material mat,int sides,int rings)
            {
                var z=new float[rings+1];var x=new float[rings+1];var y=new float[rings+1];
                for(int i=0;i<=rings;i++){float t=-Mathf.PI*.5f+i*Mathf.PI/rings;z[i]=Mathf.Sin(t)*size.z*.5f;float s=Mathf.Max(.035f,Mathf.Cos(t));x[i]=s*size.x*.5f;y[i]=s*size.y*.5f;}
                Loft(center,z,x,y,mat,sides,0);
            }
            public void Segment(Vector3 from,Vector3 to,float lower,float upper,Material mat,int sides)
            {
                var d=to-from;if(d.sqrMagnitude<.0001f)return;var q=Quaternion.LookRotation(d);
                var a=new Vector3[sides];var b=new Vector3[sides];
                for(int i=0;i<sides;i++){float t=i*Mathf.PI*2/sides;var v=new Vector3(Mathf.Cos(t),Mathf.Sin(t),0);a[i]=from+q*v*lower;b[i]=to+q*v*upper;}
                Face(mat,-d,a);Face(mat,d,b);
                for(int i=0;i<sides;i++){int j=(i+1)%sides;Face(mat,(a[i]+a[j])*.5f-from,a[i],a[j],b[j],b[i]);}
            }
            public void Wedge(Vector3 center,Vector3 size,Material mat,Vector3 rotation)
            {
                var q=Quaternion.Euler(rotation.x,rotation.y,rotation.z);var h=size*.5f;
                var local=new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x*.68f,h.y,-h.z*.74f),new Vector3(h.x*.68f,h.y,-h.z*.74f),new Vector3(h.x*.68f,h.y,h.z*.84f),new Vector3(-h.x*.68f,h.y,h.z*.84f)};
                var p=local.Select(v=>center+q*v).ToArray();
                Face(mat,q*Vector3.down,p[0],p[1],p[2],p[3]);Face(mat,q*Vector3.up,p[4],p[5],p[6],p[7]);
                Face(mat,q*Vector3.back,p[0],p[1],p[5],p[4]);Face(mat,q*Vector3.forward,p[3],p[2],p[6],p[7]);
                Face(mat,q*Vector3.left,p[0],p[3],p[7],p[4]);Face(mat,q*Vector3.right,p[1],p[2],p[6],p[5]);
            }
            public void Finish(Material fallback)
            {
                int index=0;
                foreach(var item in parts)
                {
                    if(item.Value.vertices.Count==0)continue;
                    var mesh=new Mesh{indexFormat=item.Value.vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                    mesh.SetVertices(item.Value.vertices);mesh.SetUVs(0,item.Value.uv);mesh.SetTriangles(item.Value.indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                    string key=("ScenarioBoss_"+asset+"_"+index).Replace(' ','_').Replace('/','_');
                    mesh=SaveDetailMesh(mesh,key);
                    var go=new GameObject("Surface "+index++);go.transform.SetParent(root,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=item.Key??fallback;
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
                    // Precise static-review collider, not a final moving-creature physics rig.
                    go.AddComponent<MeshCollider>().sharedMesh=mesh;
                    GameObjectUtility.SetStaticEditorFlags(go,0);
                }
            }
        }
    }
}
