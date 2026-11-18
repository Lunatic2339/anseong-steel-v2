using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Called after placement, maintenance and the other completion passes, before UV/LOD/bake.
        // Source prefabs are not edited. All dimensions below are world metres.
        static void CompleteSurfaceAndDamage()
        {
            var report=new StringBuilder("Surface, signage and damage completion / authored static environment\n");
            var root=ArchitectureGroup("Surface completion - grounded damage and signs",nearDetails);
            CompleteCopiedSmoke(report);
            CompleteWarehouseSeams(report);
            CompleteMaintenancePaint(report);
            CompleteWorldSigns(root,report);
            CompleteRubbleContacts(root,report);
            CompleteWarehouseRoofFall(report);
            CompleteFireSources(root,report);
            CompleteZ2ProcessDamage(root,report);
            report.AppendLine("Runtime animation, headset image quality and performance still require the integrated build; these are not inferred from generation.");
            File.WriteAllText(WideOutput+"/surface-completion.txt",report.ToString());
        }

        static Material SurfaceCopy(string name,Material source)
        {
            var material=Material("Completion_"+name,Color.white);
            material.shader=source.shader;material.CopyPropertiesFromMaterial(source);
            material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }

        static void CompleteCopiedSmoke(StringBuilder report)
        {
            int count=0;
            foreach(var renderer in Obj.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var source=renderer.sharedMaterial;
                if(source==null||!AssetDatabase.GetAssetPath(source).StartsWith(LegacyEnvironmentAudit.Root,StringComparison.Ordinal))continue;
                if(source.name.IndexOf("smoke",StringComparison.OrdinalIgnoreCase)<0)continue;
                source.shader=Shader.Find("Anseong/SoftIndustrialSmoke");
                source.SetColor("_Color",new Color(.39f,.42f,.44f,.58f));
                source.renderQueue=(int)RenderQueue.Transparent;EditorUtility.SetDirty(source);
                renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                var ps=renderer.GetComponent<ParticleSystem>();
                if(ps!=null)
                {
                    ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=ps.main;main.startColor=new Color(.78f,.81f,.83f,.62f);
                    var color=ps.colorOverLifetime;color.enabled=true;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.82f,.86f,.9f),1)},
                        new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.68f,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
                    ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(900+count);ps.Simulate(main.duration*.85f,true,true);ps.Pause(true);
                }
                count++;
            }
            report.AppendLine("D01: recovered "+count+" copied chimney particle renderers; alpha envelope, transparent blending, no depth write, distance sorting. Legacy material adaptation now excludes particle materials.");
        }

        static void CompleteWarehouseSeams(StringBuilder report)
        {
            var b02=Existing("B02");var root=ArchitectureGroup("B02 closed cladding joints and fixings",b02);
            var metal=SurfaceCopy("B02_folded_joint",ThemeMaterial(1,"metal",0));
            var backing=SurfaceCopy("B02_joint_backing",structuralSteel);
            var fastener=SurfaceCopy("B02_joint_fasteners",ThemeMaterial(1,"galvanized",0));
            var panels=b02.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("SideWall_")||r.name.StartsWith("EndWall_")).ToArray();
            int horizontal=0,vertical=0;float largestGap=0;
            for(int i=0;i<panels.Length;i++)for(int j=i+1;j<panels.Length;j++)
            {
                var a=panels[i].bounds;var b=panels[j].bounds;
                bool side=a.size.x<a.size.z;
                if(side!=(b.size.x<b.size.z))continue;
                int thin=side?0:2,along=side?2:0;
                if(Mathf.Abs(a.center[thin]-b.center[thin])>.06f)continue;
                bool row=Mathf.Abs(a.center[along]-b.center[along])<.06f;
                bool column=Mathf.Abs(a.center.y-b.center.y)<.06f;
                if(!row&&!column)continue;
                int axis=row?1:along;
                Bounds low=a.center[axis]<b.center[axis]?a:b,high=a.center[axis]<b.center[axis]?b:a;
                float gap=high.min[axis]-low.max[axis];
                if(gap<-.005f||gap>.065f)continue;
                float span=row?Mathf.Min(a.size[along],b.size[along]):Mathf.Min(a.size.y,b.size.y);
                Vector3 center=(a.center+b.center)*.5f;center[axis]=(low.max[axis]+high.min[axis])*.5f;
                Vector3 normal=side?Vector3.right:Vector3.forward;
                if(center[thin]<b02.position[thin])normal=-normal;
                var rotation=Quaternion.LookRotation(normal);
                float thickness=Mathf.Max(a.size[thin],b.size[thin]);
                var sealSize=row?new Vector3(span+.035f,.085f,thickness*.94f):new Vector3(.085f,span+.035f,thickness*.94f);
                var seal=ArchitectureBox("continuous concealed seam backing",center,sealSize,backing,root);seal.transform.rotation=rotation;
                // Two shallow folded caps close the original 20mm gaps on both faces.
                // The old 7.98m plates are spaced at 8m; simply enlarging them would create coplanar overlap.
                foreach(float face in new[]{-1f,1f})
                {
                    var front=center+normal*face*(thickness*.5f+.022f);
                    var size=row?new Vector3(span+.035f,.14f,.042f):new Vector3(.14f,span+.035f,.042f);
                    var cap=ArchitectureBox("folded weather lap flashing",front,size,metal,root);cap.transform.rotation=rotation;
                    int bolts=Mathf.Max(2,Mathf.CeilToInt(span/1.6f));
                    for(int k=0;k<=bolts;k++)
                    {
                        float offset=Mathf.Lerp(-span*.47f,span*.47f,k/(float)bolts);
                        Vector3 p=front+(row?rotation*Vector3.right:Vector3.up)*offset+normal*face*.025f;
                        var bolt=Cylinder("sealed cladding fastener",p,.022f,.018f,fastener,root,false);
                        bolt.transform.rotation=Quaternion.FromToRotation(Vector3.up,normal);
                        if(face>0&&row&&k%2==1)
                            SurfaceMark("fastener runoff",p-Vector3.up*.26f+normal*.007f,normal,new Vector2(.2f,.55f),
                                new Color(.23f,.115f,.045f,.45f),1,k+i,root);
                    }
                }
                if(row)horizontal++;else vertical++;largestGap=Mathf.Max(largestGap,gap);
            }
            foreach(var m in b02.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.HasProperty("_WearAmount")).Distinct())
                if(m.GetFloat("_WearAmount")>0)
                {m.SetFloat("_WearEdgeWidth",.055f);m.SetFloat("_GrimeStrength",.2f);EditorUtility.SetDirty(m);}
            CombineSurfaceOpaque(root,"B02_seams");
            if(horizontal==0)throw new InvalidOperationException("B02 joint completion found no stacked cladding seams; source layout changed.");
            report.AppendLine("D06: "+horizontal+" horizontal and "+vertical+" vertical joints closed. Largest measured active panel gap="+largestGap.ToString("F4",Inv)+"m. Source uses 8m pitch with 7.98m plate dimensions. Separate backing and lap caps avoid coplanar overlap. A7: short stains originate at actual fasteners; shader broad islands reduced, seam wear narrowed.");
        }

        static void CompleteMaintenancePaint(StringBuilder report)
        {
            var yard=structures.Find("B02 maintenance service yard");if(yard==null)return;
            int count=0;
            foreach(var filter in yard.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null||!renderer.sharedMaterials.Any(m=>m!=null&&m.name=="Maintenance_faded_ochre"))continue;
                var mesh=Obj.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=filter.transform.TransformPoint(vertices[i]);
                    if(p.y>.995f&&p.y<1.04f)p.y=1.0018f;
                    else
                    {
                        // Stair nosings were 12mm cuboids too. Their nearest riser height is known.
                        float riser=Mathf.Round((p.y+.055f)/(1.055f/6));
                        p.y=-.055f+riser*(1.055f/6)+.0018f;
                    }
                    vertices[i]=filter.transform.InverseTransformPoint(p);
                }
                mesh.vertices=vertices;
                // Collapsed box side/bottom triangles are removed, leaving only upward paint faces.
                var triangles=mesh.triangles;var keep=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                    if(Vector3.Dot(Vector3.Cross(b-a,c-a),Vector3.up)>1e-7f)keep.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
                }
                mesh.SetTriangles(keep,0);filter.sharedMesh=SaveDetailMesh(mesh,"Completion_maintenance_paint_"+count);
                renderer.shadowCastingMode=ShadowCastingMode.Off;count++;
            }
            report.AppendLine("B02 route/nosing paint: "+count+" combined meshes reduced from 12mm solid strips to 1.8mm-offset upward faces, no cast shadow.");
        }

        static void CompleteWorldSigns(Transform completion,StringBuilder report)
        {
            var plate=SurfaceCopy("sign_enamel_back",agedRoof);var edge=SurfaceCopy("sign_mount",structuralSteel);
            int count=0;
            foreach(var text in Obj.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(text.transform.IsChildOf(labels)||text.transform.IsChildOf(proxies))continue;
                var renderer=text.GetComponent<MeshRenderer>();if(renderer==null)continue;
                string value=text.text;
                if(value=="FIELD REPAIR")
                    // Columns are at z=-38 and -27. Keep the complete plate inside that bay.
                    text.transform.SetPositionAndRotation(new Vector3(-28.92f,22.4f,-32.5f),Quaternion.Euler(0,-90,0));
                else if(value.StartsWith("PWR /"))
                {var p=text.transform.position;p.z=-39.89f;text.transform.SetPositionAndRotation(p,Quaternion.Euler(0,180,0));}
                if(value.StartsWith("EVACUATION"))
                {text.characterSize=.13f;text.transform.SetPositionAndRotation(new Vector3(88,3.15f,72),Quaternion.identity);}
                if(value.StartsWith("FURNACE 08"))
                {
                    text.characterSize=.16f;
                    // The structural completion provides the canopy fascia at z=662.03.
                    text.transform.SetPositionAndRotation(new Vector3(100,11.3f,662.015f),Quaternion.identity);
                }
                if(value=="FIELD REPAIR")FitSurfaceStencil(text,5.6f,.85f);
                else if(value.StartsWith("EVACUATION"))FitSurfaceStencil(text,5.5f,.8f);
                else if(value.StartsWith("FURNACE 08"))FitSurfaceStencil(text,8,.92f);
                // A backboard always lies on the glyph's opaque back side. TextMesh's readable
                // normal is -Z; backface culling prevents mirrored text from the reverse approach.
                var t=text.transform;Vector3 right=t.right,up=t.up,back=t.forward;
                var local=renderer.localBounds;Vector3 scale=t.lossyScale;
                float width=Mathf.Max(.32f,local.size.x*Mathf.Abs(scale.x))+.15f;
                float height=Mathf.Max(.15f,local.size.y*Mathf.Abs(scale.y))+.1f;
                Vector3 center=t.TransformPoint(local.center);
                if(value=="FIELD REPAIR"||value.StartsWith("EVACUATION")||value.StartsWith("FURNACE 08"))
                    report.AppendLine("Mounted sign "+value.Replace('\n',' ')+": backplate "+width.ToString("F3",Inv)+" x "+height.ToString("F3",Inv)+" m, centre "+center.ToString("F3"));
                var board=ArchitectureBox("mounted opaque sign backing "+count,center+back*.055f,new Vector3(width,height,.085f),plate,completion);
                board.transform.rotation=t.rotation;
                foreach(float side in new[]{-1f,1f})
                {
                    var p=center+right*(width*.45f*side)+back*.06f;
                    var bolt=ArchitectureBox("sign corner rivet",p-back*.052f,new Vector3(.035f,.035f,.022f),edge,completion);bolt.transform.rotation=t.rotation;
                    // Only free-standing evacuation boards get posts. Device labels stay on devices.
                    if(value.StartsWith("EVACUATION"))
                    {
                        var post=ArchitectureBox("evacuation board anchored post",new Vector3(p.x,(p.y+height*.43f)*.5f,p.z+.02f),new Vector3(.09f,p.y+height*.43f,.09f),edge,completion);
                        ArchitectureBox("evacuation post concrete socket",new Vector3(p.x,.13f,p.z+.02f),new Vector3(.42f,.26f,.42f),agedConcrete,completion);
                    }
                }
                // FIELD REPAIR is wall mounted; the front header is physically on the portal.
                // Existing service labels have their service backing, furnace sign its new canopy.
                renderer.shadowCastingMode=ShadowCastingMode.Off;count++;
            }
            report.AppendLine("D05: "+count+" world labels have opaque backs and mounting plates. FIELD REPAIR faces the bay from the west wall; PWR labels face the repair platforms. Evacuation lettering reduced and posts grounded. Debug overlays/proxies excluded.");
        }

        static void FitSurfaceStencil(TextMesh text,float maximumWidth,float maximumHeight)
        {
            var renderer=text.GetComponent<MeshRenderer>();var bounds=renderer.localBounds;var scale=text.transform.lossyScale;
            float width=bounds.size.x*Mathf.Abs(scale.x),height=bounds.size.y*Mathf.Abs(scale.y);
            if(width<.0001f||height<.0001f)return;
            float factor=Mathf.Min(1,Mathf.Min(maximumWidth/width,maximumHeight/height));
            text.transform.localScale*=factor;
        }

        static void SurfaceMark(string name,Vector3 position,Vector3 normal,Vector2 size,Color color,int kind,int seed,Transform parent)
        {
            string key="Completion_deposit_"+kind+"_"+ColorUtility.ToHtmlStringRGBA(color)+"_"+(seed%4);
            var material=Material(key,color);material.shader=Shader.Find("Anseong/CausalSurfaceMark");
            material.SetColor("_Color",color);material.SetFloat("_Kind",kind);material.SetFloat("_Seed",seed%4);
            var g=Shape(name,PrimitiveType.Quad,position,new Vector3(size.x,size.y,1),material,parent,false);
            g.transform.rotation=Quaternion.LookRotation(-normal,Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.99f?Vector3.forward:Vector3.up);
            var renderer=g.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
        }

        static Mesh SurfaceFracture(int seed)
        {
            var rng=new System.Random(seed);var v=new List<Vector3>();var tr=new List<int>();
            const int sides=7;var low=new Vector3[sides];var high=new Vector3[sides];
            for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides,r=.38f+(float)rng.NextDouble()*.16f;
                low[i]=new Vector3(Mathf.Cos(angle)*r,-.5f,Mathf.Sin(angle)*r);
                high[i]=new Vector3(low[i].x*.86f,.26f+(float)rng.NextDouble()*.24f,low[i].z*.86f);
            }
            for(int i=0;i<sides;i++)ArchitectureQuad(v,tr,low[i],high[i],high[(i+1)%sides],low[(i+1)%sides]);
            for(int i=1;i<sides-1;i++)
            {int n=v.Count;v.AddRange(new[]{high[0],high[i+1],high[i],low[0],low[i],low[i+1]});tr.AddRange(new[]{n,n+1,n+2,n+3,n+4,n+5});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(tr,0);return SaveDetailMesh(mesh,"Completion_fracture_"+seed);
        }

        static void CompleteRubbleContacts(Transform parent,StringBuilder report)
        {
            var old=ruins.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="burn footprint").ToArray();
            var footprints=old.Select(r=>new Vector4(r.bounds.center.x,r.bounds.center.y-.12f,r.bounds.center.z,r.bounds.extents.x)).ToArray();
            foreach(var r in old)Obj.DestroyImmediate(r.gameObject);
            int fixedFragments=0;
            foreach(var r in ruins.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="fractured slab"||r.name=="torn girder").ToArray())
            {
                var nearest=footprints.OrderBy(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(r.bounds.center.x,r.bounds.center.z))).FirstOrDefault();
                float ground=nearest.y;
                if(r.name=="fractured slab")r.GetComponent<MeshFilter>().sharedMesh=SurfaceFracture(300+fixedFragments%7);
                else
                {
                    var t=r.transform;var a=t.TransformPoint(new Vector3(0,0,-.5f));var b=t.TransformPoint(new Vector3(0,0,.5f));
                    var replacement=ArchitectureBeam("fallen buckled steel - old rubble placement",a,b,Mathf.Abs(t.lossyScale.x),Mathf.Abs(t.lossyScale.y),parent,agedRust);
                    Obj.DestroyImmediate(r.gameObject);var renderer=replacement.GetComponent<MeshRenderer>();
                    replacement.transform.position+=Vector3.up*(ground+.015f-renderer.bounds.min.y);fixedFragments++;continue;
                }
                r.transform.position+=Vector3.up*(ground+.015f-r.bounds.min.y);fixedFragments++;
            }
            for(int i=0;i<footprints.Length;i++)
            {
                var p=footprints[i];
                // Dust follows an elongated collapse footprint, not an opaque circular disc.
                SurfaceMark("fall dust at grounded debris",new Vector3(p.x,p.y+.012f,p.z),Vector3.up,new Vector2(p.w*1.55f,p.w*.75f),new Color(.28f,.25f,.2f,.34f),0,i,parent);
            }
            report.AppendLine("A8: removed "+old.Length+" circular opaque burn discs; grounded "+fixedFragments+" existing fragments, replaced cuboid slabs by angular fractures and rectangular girders by I sections. Dust remains inside existing debris footprints; no expansion into movement reservations.");
        }

        static void CompleteWarehouseRoofFall(StringBuilder report)
        {
            var root=ArchitectureGroup("B02 roof loss connected fall sequence",Existing("B02"));
            var roof=ThemeMaterial(1,"roof",0);
            Vector3[] points={new Vector3(-98,1.04f,43),new Vector3(-94.5f,1.04f,46),new Vector3(-89.5f,1.04f,46),new Vector3(-85,1.04f,44.5f)};
            for(int i=0;i<points.Length;i++)
            {
                ArchitectureFoldedPanel("collapsed roof skin layer "+i,points[i],i==0?5.2f:4.4f,5.7f+i*.45f,i%2==0?19:-23,
                    Quaternion.Euler(86,i*19-25,2),roof,root);
                var t=root.GetChild(root.childCount-1);var r=t.GetComponent<MeshRenderer>();
                t.position+=Vector3.up*(1.035f+i*.055f-r.bounds.min.y);
                SurfaceMark("impact dust below roof skin",new Vector3(points[i].x,1.015f,points[i].z-2),Vector3.up,new Vector2(6,8),new Color(.29f,.26f,.21f,.25f),0,i,root);
            }
            // Secondary thin shards sit below the failed rafter, west of the clear personnel strip.
            for(int i=0;i<12;i++)
            {
                var go=new GameObject("roof insulation and edge fragment "+i);go.transform.SetParent(root,true);
                go.transform.SetPositionAndRotation(new Vector3(-97+i*1.3f,1.1f,40+(i%3)*2.1f),Quaternion.Euler(6,i*31,4));
                go.transform.localScale=new Vector3(.3f+i%3*.13f,.12f,.4f+i%4*.2f);
                go.AddComponent<MeshFilter>().sharedMesh=SurfaceFracture(320+i%3);var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=damagedConcrete;
                go.transform.position+=Vector3.up*(1.02f-r.bounds.min.y);
            }
            CombineSurfaceOpaque(root,"B02_fall");
            report.AppendLine("A8 B02: four closed corrugated roof skins and twelve small insulation/edge fragments connect the missing roof to the existing bent rafter/fall fan. Their minimum bounds are grounded; no pieces enter the east maintenance walkway.");
        }

        static void CompleteFireSources(Transform parent,StringBuilder report)
        {
            foreach(var r in ruins.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="glowing debris").ToArray())Obj.DestroyImmediate(r.gameObject);
            var charred=SurfaceCopy("charred_packaging",agedRoof);charred.color=new Color(.16f,.14f,.11f);
            var ember=Material("Completion_small_embers",new Color(.12f,.055f,.018f));
            ember.EnableKeyword("_EMISSION");ember.SetColor("_EmissionColor",new Color(.65f,.09f,.008f));
            int count=0;Physics.SyncTransforms();
            foreach(var ps in ruins.GetComponentsInChildren<ParticleSystem>().Where(p=>p.name=="Rising industrial flames").ToArray())
            {
                Vector3 oldPosition=ps.transform.position,p=oldPosition;
                bool b02=p.x>-120&&p.x<-70&&p.z>27&&p.z<65;
                float floor=b02?1:0;
                if(!b02)
                {
                    var hits=Physics.RaycastAll(p+Vector3.up,Vector3.down,20).Where(h=>h.normal.y>.6f&&!h.collider.transform.IsChildOf(ruins)).OrderBy(h=>h.distance).ToArray();
                    if(hits.Length>0)floor=hits[0].point.y;
                }
                p.y=floor+.09f;ps.transform.position=p;
                foreach(var smoke in ruins.GetComponentsInChildren<ParticleSystem>().Where(s=>s.name.StartsWith("Fire aftermath")&&Vector3.Distance(s.transform.position,oldPosition)<1).ToArray())
                {
                    smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);smoke.transform.position=p;
                    var main=smoke.main;main.startSize=new ParticleSystem.MinMaxCurve(3.6f,5.7f);main.startSpeed=1.8f;main.startLifetime=15;
                    main.startColor=new Color(.33f,.35f,.37f,.65f);
                    var size=smoke.sizeOverLifetime;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.7f,1,2.3f));
                    var emission=smoke.emission;emission.rateOverTime=1.8f;
                    smoke.Simulate(12,true,true);smoke.Pause(true);
                }
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var fm=ps.main;fm.startSize=new ParticleSystem.MinMaxCurve(2.1f,4.0f);fm.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,1.7f);fm.startSpeed=.9f;fm.maxParticles=24;
                var shape=ps.shape;shape.radius=1.7f;shape.angle=8;
                var em=ps.emission;em.rateOverTime=9;
                var pr=ps.GetComponent<ParticleSystemRenderer>();pr.sortMode=ParticleSystemSortMode.Distance;
                pr.sharedMaterial.SetFloat("_Intensity",1.45f);ps.Simulate(3,true,true);ps.Pause(true);
                foreach(var light in ruins.GetComponentsInChildren<Light>().Where(l=>l.name=="fire bounce"&&Vector2.Distance(new Vector2(l.transform.position.x,l.transform.position.z),new Vector2(p.x,p.z))<3))
                {light.transform.position=p+Vector3.up*1.6f;light.intensity=1.7f;light.range=12;light.color=new Color(1,.37f,.12f);}
                SurfaceMark("soot from burning packaging",new Vector3(p.x,floor+.02f,p.z),Vector3.up,new Vector2(8,7),new Color(.055f,.045f,.035f,.72f),0,count,parent);
                for(int i=0;i<7;i++)
                {
                    var slat=ArchitectureBox("charred packing skid slat",p+new Vector3((i%2)*1.8f-.9f,.04f+(i%3)*.035f,-1.3f+i*.39f),new Vector3(2.1f,.11f,.2f),charred,parent);
                    slat.transform.rotation=Quaternion.Euler(2,i%2==0?13:-17,3);
                }
                for(int i=0;i<5;i++)
                {
                    var chunk=new GameObject("small ember within fuel bed");chunk.transform.SetParent(parent,true);
                    chunk.transform.position=p+new Vector3(-1.2f+i*.6f,.07f,Mathf.Sin(i*2)*.8f);chunk.transform.localScale=new Vector3(.34f,.12f,.25f);
                    chunk.AddComponent<MeshFilter>().sharedMesh=SurfaceFracture(340+i%2);chunk.AddComponent<MeshRenderer>().sharedMaterial=ember;
                }
                if(b02)
                {
                    ArchitectureFoldedPanel("charred roof edge beside fuel",new Vector3(p.x+2.2f,floor+.2f,p.z+.7f),2.8f,3.4f,31,Quaternion.Euler(84,44,0),charred,parent);
                    var sheet=parent.GetChild(parent.childCount-1);sheet.position+=Vector3.up*(floor+.04f-sheet.GetComponent<MeshRenderer>().bounds.min.y);
                }
                count++;
            }
            report.AppendLine("A10: "+count+" fire events tied to grounded charred packing skids, small ember pockets, irregular soot and lower/less additive flames. Large glowing cubes removed. B02 fuel kept on y=1 slab; working maintenance bay remains separate.");
        }

        static void CompleteZ2ProcessDamage(Transform parent,StringBuilder report)
        {
            // The former isolated pile becomes a failed north-side B04 process line.
            // Its source is an actual wall port; the damage remains east of the mech/drone paths.
            var hall=Existing("B04");float wallZ=hall.TransformPoint(new Vector3(0,0,27.55f)).z;
            var root=ArchitectureGroup("Z2 B04 process-line rupture and fire trail",parent);
            var g=new IndustrialGeometry(root,"Z2 connected damaged process line");
            var pipe=ThemeMaterial(3,"metal",0);var zinc=ThemeMaterial(3,"galvanized",0);
            var support=ThemeMaterial(3,"rust",0);var baseMat=ThemeMaterial(3,"concrete",0);
            int removed=0;
            foreach(var r in ruins.GetComponentsInChildren<MeshRenderer>().Concat(parent.GetComponentsInChildren<MeshRenderer>()).ToArray())
            {
                if(r==null)continue;var p=r.bounds.center;
                bool oldRubble=r.name=="fractured slab"||r.name=="fallen buckled steel - old rubble placement";
                bool oldDust=r.name=="fall dust at grounded debris";
                if((oldRubble||oldDust)&&p.x>65&&p.x<111&&p.z>199&&p.z<240)
                {Obj.DestroyImmediate(r.gameObject);removed++;}
            }
            foreach(float x in new[]{92f,96f})
            {
                float y=x<94?7.8f:9f,radius=x<94?.4f:.3f;
                g.Box(new Vector3(x,y,wallZ+.025f),new Vector3(2.1f,2.1f,.17f),support,.04f);
                g.Tube(new[]{new Vector3(x,y,wallZ-.13f),new Vector3(x,y,197.5f),new Vector3(x,y,x<94?204:202.8f)},radius,pipe,20,true);
                foreach(float z in new[]{wallZ+.3f,190.5f})IndustrialFlange(g,new Vector3(x,y,z),Vector3.forward,radius,zinc);
                SurfaceRuptureLip(new Vector3(x,y,x<94?204:202.8f),Vector3.forward,radius,(int)x,root,zinc);
                // A wall-attached bracket and bolted port make the origin a facility connection.
                g.IBeam(new Vector3(x,y-.7f,wallZ+.12f),new Vector3(x,y-.7f,wallZ+1.4f),.18f,.25f,support);
                g.Bar(new Vector3(x,y-1.5f,wallZ+.12f),new Vector3(x,y-.7f,wallZ+1.4f),.12f,.14f,zinc);
            }
            foreach(float z in new[]{187.5f,198.5f})
            {
                foreach(float x in new[]{89f,99f})
                {
                    g.Box(new Vector3(x,.3f,z),new Vector3(1.65f,.6f,1.65f),baseMat,.11f);
                    g.Box(new Vector3(x,.66f,z),new Vector3(1,.12f,1),zinc,.025f);
                    g.IBeam(new Vector3(x,.73f,z),new Vector3(x,7.15f,z),.32f,.48f,support);
                    foreach(float dx in new[]{-.34f,.34f})foreach(float dz in new[]{-.34f,.34f})
                        g.Rod(new Vector3(x+dx,.64f,z+dz),new Vector3(x+dx,.85f,z+dz),.045f,zinc,6);
                }
                g.IBeam(new Vector3(88.6f,7.15f,z),new Vector3(99.4f,7.15f,z),.3f,.38f,support);
                g.Box(new Vector3(92,7.38f,z),new Vector3(1.1f,.14f,.55f),zinc,.02f);
                g.IBeam(new Vector3(96,7.3f,z),new Vector3(96,8.6f,z),.22f,.3f,support);
                g.Box(new Vector3(96,8.65f,z),new Vector3(1,.1f,.55f),zinc,.02f);
            }
            // The third support has torn off its north foot. Its crosshead carries the fallen pipe.
            foreach(float x in new[]{89f,99f})
            {
                g.Box(new Vector3(x,.28f,209),new Vector3(1.65f,.56f,1.65f),baseMat,.15f);
                g.Box(new Vector3(x,.6f,209),new Vector3(.95f,.1f,.95f),support,.025f);
                foreach(float dx in new[]{-.3f,.3f})
                    g.Tube(new[]{new Vector3(x+dx,.45f,209),new Vector3(x+dx,.95f,209),new Vector3(x+dx+.12f,1.02f,209.15f)},.035f,zinc,8);
            }
            g.IBeam(new Vector3(89,.66f,209),new Vector3(89.8f,4.9f,212),.34f,.55f,support);
            g.IBeam(new Vector3(89.8f,4.9f,212),new Vector3(97.2f,4.4f,212.5f),.34f,.55f,support);
            g.IBeam(new Vector3(97.2f,4.4f,212.5f),new Vector3(99,.4f,216),.34f,.55f,support);
            g.IBeam(new Vector3(99,.64f,209),new Vector3(99,1.25f,209),.34f,.55f,support);
            var fallA=new[]{new Vector3(92.2f,6.8f,205.2f),new Vector3(93.5f,5.31f,212),new Vector3(95,.43f,226)};
            var fallB=new[]{new Vector3(96.5f,.34f,205.5f),new Vector3(97.5f,1.35f,214),new Vector3(98.3f,.34f,222.5f)};
            g.Tube(fallA,.4f,pipe,22,true);g.Tube(fallB,.3f,pipe,20,true);
            SurfaceRuptureLip(fallA[0],(fallA[0]-fallA[1]).normalized,.4f,71,root,zinc);
            SurfaceRuptureLip(fallA[2],(fallA[2]-fallA[1]).normalized,.4f,72,root,zinc);
            SurfaceRuptureLip(fallB[0],(fallB[0]-fallB[1]).normalized,.3f,73,root,zinc);
            IndustrialFlange(g,Vector3.Lerp(fallA[1],fallA[2],.75f),(fallA[2]-fallA[1]).normalized,.4f,zinc);
            g.Finish();
            for(int i=0;i<6;i++)
            {
                var shard=new GameObject("shattered support foundation fragment "+i);shard.transform.SetParent(root,true);
                shard.transform.SetPositionAndRotation(new Vector3(i<3?88.5f+i*.65f:98.5f+(i-3)*.5f,.2f,210+i%3*1.5f),Quaternion.Euler(8,i*37,4));
                shard.transform.localScale=new Vector3(.75f+i%3*.2f,.3f,.8f+i%2*.5f);
                shard.AddComponent<MeshFilter>().sharedMesh=SurfaceFracture(360+i%3);var renderer=shard.AddComponent<MeshRenderer>();renderer.sharedMaterial=baseMat;
                shard.transform.position+=Vector3.up*(.018f-renderer.bounds.min.y);
            }
            SurfaceMark("foundation failure dust",new Vector3(94,.014f,212.5f),Vector3.up,new Vector2(13,11),new Color(.31f,.27f,.2f,.46f),0,2,root);
            SurfaceMark("dragged north support scar",new Vector3(99,.016f,213),Vector3.up,new Vector2(1.4f,8),new Color(.11f,.105f,.09f,.65f),2,1,root);
            // A dark, continuous spill reaches the existing fire at (90,245), rather than
            // spawning a second unrelated effect. The near source is the open pipe end.
            var spill=new[]{new Vector3(95,.021f,226),new Vector3(94,.021f,230),new Vector3(93,.021f,234),new Vector3(91.5f,.021f,238),new Vector3(90.5f,.021f,242),new Vector3(90,.021f,245)};
            for(int i=0;i<spill.Length;i++)
                SurfaceMark("process fluid spill reaching ignition",spill[i],Vector3.up,new Vector2(i==0?4.2f:3.5f,6.5f),new Color(.045f,.035f,.025f,.82f),3,i,root);
            SurfaceMark("ignition blackening at end of spill",new Vector3(90,.024f,244.5f),Vector3.up,new Vector2(6.5f,8),new Color(.04f,.032f,.026f,.7f),0,3,root);
            report.AppendLine("A8/A10 Z2: replaced "+removed+" disconnected pile/dust objects by a B04 north-wall process line at z="+wallZ.ToString("F2",Inv)+" -> two retained trestles -> torn third base at z209 -> broken open pipe resting on fallen crosshead -> fluid trail z226..245 -> existing fire (90,245). Main damage x[88,100], z[202,246]; mech x0, boss z>=280 and western drone approach remain outside this addition.");
        }

        static void SurfaceRuptureLip(Vector3 center,Vector3 axis,float radius,int seed,Transform parent,Material material)
        {
            const int segments=24;var vertices=new List<Vector3>();var indices=new List<int>();
            var rotation=Quaternion.LookRotation(axis);float thickness=.045f;
            Func<int,int,int,Vector3> point=(i,ring,inner)=>
            {
                float angle=(i%segments)*Mathf.PI*2/segments;
                float edge=.035f+.15f*Mathf.PerlinNoise(i*.47f,seed*.1f);
                float r=radius-inner*thickness+(ring==1?.025f*Mathf.Sin(i*2.1f+seed):0);
                return rotation*new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,ring==0?-.13f:edge);
            };
            for(int i=0;i<segments;i++)
            {
                ArchitectureQuad(vertices,indices,point(i,0,0),point(i+1,0,0),point(i+1,1,0),point(i,1,0));
                ArchitectureQuad(vertices,indices,point(i,1,1),point(i+1,1,1),point(i+1,0,1),point(i,0,1));
                ArchitectureQuad(vertices,indices,point(i,1,0),point(i+1,1,0),point(i+1,1,1),point(i,1,1));
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);
            var go=new GameObject("ragged open pipe tear "+seed);go.transform.SetParent(parent,true);go.transform.position=center;
            go.AddComponent<MeshFilter>().sharedMesh=SaveDetailMesh(mesh,"Completion_pipe_tear_"+seed);go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        static void CombineSurfaceOpaque(Transform root,string key)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TextMesh>()==null&&r.GetComponent<MeshFilter>()!=null&&r.sharedMaterial!=null&&r.sharedMaterial.renderQueue<2501).ToArray();
            int index=0;
            foreach(var group in renderers.GroupBy(r=>r.sharedMaterial))
            {
                var combine=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine,true,true);
                var go=new GameObject("combined surface fittings "+index);go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=SaveDetailMesh(mesh,"Completion_"+key+"_"+index++);go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var r in renderers)Obj.DestroyImmediate(r.gameObject);
        }
    }
}
