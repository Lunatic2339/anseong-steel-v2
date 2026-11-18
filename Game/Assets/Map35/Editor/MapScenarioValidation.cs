using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Tests authored, active spatial poses. This is not a swept/animated collision or gameplay test.
        static void ValidateScenarioGeometry(MapScenarioPreview preview,List<string> errors,StringBuilder summary)
        {
            var meshes=Obj.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var cache=new Dictionary<MeshFilter,ScenarioValidationMesh>();
            var unreadable=new HashSet<MeshFilter>();
            summary.AppendLine("Geometry validation: actual mesh support rays; triangle/AABB clearance; two-sided mesh LOS. Static active poses only.");
            for(int index=0;index<preview.steps.Length;index++)
            {
                preview.SelectStep(index);Physics.SyncTransforms();var step=preview.steps[index];
                bool settled=step.id=="z3-hit"||step.id=="z3-miss"||step.id.EndsWith("-follow",StringComparison.Ordinal);
                if(settled&&step.id.StartsWith("z3-",StringComparison.Ordinal))
                    ValidateScenarioSettledCoil(step,cache,unreadable,errors,summary);
                if(step.id=="z3-evade"||step.id=="z3-hit"||step.id=="z3-miss")
                    ValidateScenarioEvacuatedMech(step,cache,unreadable,errors,summary);
                if(step.id=="z2-defend")
                    ValidateScenarioCameraRay(preview,step,0,"Target T1",meshes,cache,unreadable,errors,summary);
                if(step.id=="z2-open"||step.id=="z3-hit-follow"||step.id=="z3-miss-follow")
                {
                    ValidateScenarioCameraRay(preview,step,1,"Target T2",meshes,cache,unreadable,errors,summary);
                    ValidateScenarioCameraRay(preview,step,3,"Target T2",meshes,cache,unreadable,errors,summary);
                    ValidateScenarioEmitterRay(preview,step,step.id=="z2-open"?"Target E01":"Target E02",meshes,cache,unreadable,errors,summary);
                }
                if(step.id=="z2-neck"||step.id=="z3-hit-follow"||step.id=="z3-miss-follow")
                    ValidateScenarioCameraRay(preview,step,0,"Target Neck",meshes,cache,unreadable,errors,summary);
                if(step.id=="z3-evade")
                {
                    ValidateScenarioCameraRay(preview,step,1,"Target CraneRelease",meshes,cache,unreadable,errors,summary);
                    ValidateScenarioCameraRay(preview,step,3,"Target CraneRelease",meshes,cache,unreadable,errors,summary);
                }
            }
            foreach(var item in unreadable)errors.Add("Geometry validation could not read relevant mesh: "+ScenarioValidationPath(item.transform));
            summary.AppendLine("Geometry mesh cache="+cache.Count+"; unreadable relevant meshes="+unreadable.Count);
        }

        static void ValidateScenarioSettledCoil(ScenarioSpatialStep step,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable,List<string> errors,StringBuilder summary)
        {
            var root=step.root.transform;
            var coil=root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Scenario coil ",StringComparison.Ordinal));
            var boss=root.Find(step.id);
            if(coil==null||boss==null){errors.Add(step.id+" settled coil or active boss missing");return;}
            var coilMeshes=coil.GetComponentsInChildren<MeshFilter>().Where(ScenarioValidationRendered).ToArray();
            var points=new List<Vector3>();
            foreach(var mf in coilMeshes){var data=ScenarioValidationData(mf,cache,unreadable);if(data!=null)points.AddRange(data.vertices);}
            if(points.Count==0){errors.Add(step.id+" settled coil has no readable rendered geometry");return;}
            var bounds=ScenarioValidationBounds(points);
            bool success=step.id=="z3-hit"||step.id=="z3-hit-follow";
            string supportName=success?"R1 receiving slab":"Z3 crane yard 240x200";
            var support=Obj.FindObjectsByType<Transform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(t=>t.name==supportName);
            if(support==null){errors.Add(step.id+" missing support "+supportName);return;}
            var supportMeshes=support.GetComponentsInChildren<MeshFilter>().Where(ScenarioValidationRendered).ToArray();
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;int samples=0,unsupported=0;
            // Vertices in the lowest 15 mm include the wound-sheet edge, not just nominal coil size.
            foreach(var p in points.Where(p=>p.y<=bounds.min.y+.015f))
            {
                var ray=new Ray(p+Vector3.up*.10f,Vector3.down);float distance=float.PositiveInfinity;
                foreach(var mf in supportMeshes)
                    distance=Mathf.Min(distance,ScenarioValidationMeshRay(mf,ray,1,cache,unreadable));
                // Primitive floor colliders are a fallback only when the support has no rendered mesh.
                if(supportMeshes.Length==0)
                    foreach(var collider in support.GetComponentsInChildren<Collider>())
                        if(collider.enabled&&collider.Raycast(ray,out var hit,1))distance=Mathf.Min(distance,hit.distance);
                if(float.IsPositiveInfinity(distance)){unsupported++;continue;}
                float gap=distance-.10f;minimum=Mathf.Min(minimum,gap);maximum=Mathf.Max(maximum,gap);samples++;
            }
            summary.AppendLine(step.id+" coil bottom="+bounds.min.y.ToString("F4",Inv)+" support="+supportName+" sampled contacts="+samples+" gap range="+minimum.ToString("F4",Inv)+".."+maximum.ToString("F4",Inv)+" unsupported="+unsupported);
            if(samples==0||unsupported>0||minimum<-.015f||minimum>.040f)
                errors.Add(step.id+" settled coil is not supported within -15/+40 mm of its actual lowest mesh edge");
            // Using the coil's occupied envelope is deliberate and conservative; never the whole boss AABB.
            var envelope=bounds;envelope.Expand(-.02f);
            ValidateScenarioBossAgainstBox(step,boss,envelope,"settled coil envelope",cache,unreadable,errors,summary);
        }

        static void ValidateScenarioEvacuatedMech(ScenarioSpatialStep step,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable,List<string> errors,StringBuilder summary)
        {
            var mech=step.root.transform.Find("Mech spatial footprint");var boss=step.root.transform.Find(step.id);
            if(mech==null||boss==null){errors.Add(step.id+" evacuated mech or active boss missing");return;}
            var parts=mech.GetComponentsInChildren<MeshFilter>().Where(ScenarioValidationRendered).ToArray();
            var vertices=new List<Vector3>();foreach(var mf in parts){var data=ScenarioValidationData(mf,cache,unreadable);if(data!=null)vertices.AddRange(data.vertices);}
            if(vertices.Count==0){errors.Add(step.id+" evacuated mech has no readable geometry");return;}
            var bounds=ScenarioValidationBounds(vertices);bounds.Expand(-.02f);
            summary.AppendLine(step.id+" evacuated mech bounds min="+bounds.min.ToString("F3")+" max="+bounds.max.ToString("F3"));
            ValidateScenarioBossAgainstBox(step,boss,bounds,"evacuated folded mech envelope",cache,unreadable,errors,summary);
        }

        static void ValidateScenarioBossAgainstBox(ScenarioSpatialStep step,Transform boss,Bounds bounds,string label,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable,List<string> errors,StringBuilder summary)
        {
            int candidates=0,triangles=0;string obstruction=null;
            foreach(var mf in boss.GetComponentsInChildren<MeshFilter>().Where(ScenarioValidationRendered))
            {
                if(!mf.GetComponent<Renderer>().bounds.Intersects(bounds))continue;
                candidates++;var data=ScenarioValidationData(mf,cache,unreadable);if(data==null)continue;
                for(int i=0;i<data.indices.Length;i+=3)
                {
                    triangles++;var a=data.vertices[data.indices[i]];var b=data.vertices[data.indices[i+1]];var c=data.vertices[data.indices[i+2]];
                    if(!ScenarioValidationTriangleBox(a,b,c,bounds))continue;
                    obstruction=ScenarioValidationPath(mf.transform);break;
                }
                // Also catch a fully enclosed box, which cannot intersect a boundary triangle.
                if(obstruction==null&&ScenarioValidationInsideClosedMesh(bounds.center,data))obstruction=ScenarioValidationPath(mf.transform)+" (enclosed volume)";
                if(obstruction!=null)break;
            }
            summary.AppendLine(step.id+" "+label+": candidate surfaces="+candidates+" tested triangles="+triangles+" intersection="+(obstruction??"none"));
            if(obstruction!=null)errors.Add(step.id+" boss intersects "+label+": "+obstruction);
        }

        static void ValidateScenarioCameraRay(MapScenarioPreview preview,ScenarioSpatialStep step,int role,string name,MeshFilter[] meshes,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable,List<string> errors,StringBuilder summary)
        {
            var target=step.targets.FirstOrDefault(t=>t!=null&&t.name==name&&t.gameObject.activeInHierarchy);
            if(target==null){errors.Add(step.id+" required active target missing: "+name);return;}
            preview.SelectRole(role);var camera=preview.viewer.views[preview.firstCamera+role];
            var p=camera.WorldToViewportPoint(target.position);bool frame=p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;
            if(!frame)errors.Add(step.id+" role "+role+" required target outside frame: "+name);
            string blocker;float hitDistance;bool clear=ScenarioValidationLine(camera.transform.position,target,camera.cullingMask,name=="Target CraneRelease"?.18f:.025f,meshes,cache,unreadable,out blocker,out hitDistance);
            summary.AppendLine(step.id+" role="+role+" target="+name+" frame="+frame+" mesh LOS="+clear+" first="+blocker+" distance="+hitDistance.ToString("F3",Inv));
            if(!clear)errors.Add(step.id+" role "+role+" "+name+" occluded by "+blocker);
        }

        static void ValidateScenarioEmitterRay(MapScenarioPreview preview,ScenarioSpatialStep step,string name,MeshFilter[] meshes,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable,List<string> errors,StringBuilder summary)
        {
            var emitter=preview.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==name);
            var target=step.targets.FirstOrDefault(t=>t!=null&&t.name=="Target T2"&&t.gameObject.activeInHierarchy);
            if(emitter==null||target==null){errors.Add(step.id+" emitter nozzle or T2 missing: "+name);return;}
            var origin=emitter.position+emitter.forward*.015f;
            float angle=Vector3.Angle(emitter.forward,target.position-origin);
            string blocker;float distance;bool clear=ScenarioValidationLine(origin,target,~0,.025f,meshes,cache,unreadable,out blocker,out distance);
            summary.AppendLine(step.id+" physical "+name+" nozzle="+emitter.position.ToString("F3")+" aim error="+angle.ToString("F3",Inv)+"deg mesh LOS="+clear+" first="+blocker+" distance="+distance.ToString("F3",Inv));
            if(angle>1)errors.Add(step.id+" "+name+" nozzle does not face the physical T2 target");
            if(!clear)errors.Add(step.id+" "+name+" to T2 blocked by "+blocker);
        }

        static bool ScenarioValidationLine(Vector3 origin,Transform target,int mask,float endAllowance,MeshFilter[] meshes,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable,out string first,out float distance)
        {
            var delta=target.position-origin;float length=delta.magnitude;var ray=new Ray(origin,delta.normalized);
            MeshFilter nearest=null;distance=float.PositiveInfinity;
            foreach(var mf in meshes)
            {
                if(!ScenarioValidationRendered(mf)||(mask&(1<<mf.gameObject.layer))==0)continue;
                float hit=ScenarioValidationMeshRay(mf,ray,length+.03f,cache,unreadable);
                if(hit<distance){nearest=mf;distance=hit;}
            }
            first=nearest==null?"none":ScenarioValidationPath(nearest.transform);
            // A target's own front/side surface is a successful sight line, even when it lies before its anchor.
            if(nearest==null)return true;
            if(nearest.transform==target||nearest.transform.IsChildOf(target))return true;
            return distance>=length-endAllowance;
        }

        sealed class ScenarioValidationMesh
        {
            public Matrix4x4 matrix;
            public Mesh mesh;
            public Vector3[] vertices;
            public int[] indices;
        }

        static bool ScenarioValidationRendered(MeshFilter mf)
        {
            if(mf==null||mf.sharedMesh==null||!mf.gameObject.activeInHierarchy)return false;
            var r=mf.GetComponent<Renderer>();return r!=null&&r.enabled&&r.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        static ScenarioValidationMesh ScenarioValidationData(MeshFilter mf,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable)
        {
            var matrix=mf.transform.localToWorldMatrix;
            if(cache.TryGetValue(mf,out var data)&&data.matrix==matrix&&data.mesh==mf.sharedMesh)return data;
            if(unreadable.Contains(mf))return null;
            try
            {
                var vertices=mf.sharedMesh.vertices;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                data=new ScenarioValidationMesh{matrix=matrix,mesh=mf.sharedMesh,vertices=vertices,indices=mf.sharedMesh.triangles};
                cache[mf]=data;return data;
            }
            catch(Exception){unreadable.Add(mf);return null;}
        }

        static float ScenarioValidationMeshRay(MeshFilter mf,Ray ray,float maximum,Dictionary<MeshFilter,ScenarioValidationMesh> cache,HashSet<MeshFilter> unreadable)
        {
            var renderer=mf.GetComponent<Renderer>();
            if(renderer==null||!renderer.bounds.IntersectRay(ray,out var broad)||broad>maximum)return float.PositiveInfinity;
            var data=ScenarioValidationData(mf,cache,unreadable);if(data==null)return float.PositiveInfinity;
            float nearest=float.PositiveInfinity;
            for(int i=0;i<data.indices.Length;i+=3)
            {
                float hit=ScenarioValidationTriangleRay(ray,data.vertices[data.indices[i]],data.vertices[data.indices[i+1]],data.vertices[data.indices[i+2]]);
                if(hit<=maximum&&hit<nearest)nearest=hit;
            }
            return nearest;
        }

        static float ScenarioValidationTriangleRay(Ray ray,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var p=Vector3.Cross(ray.direction,ac);float determinant=Vector3.Dot(ab,p);
            if(Mathf.Abs(determinant)<.000001f)return float.PositiveInfinity;
            float inverse=1/determinant;var d=ray.origin-a;float u=Vector3.Dot(d,p)*inverse;
            if(u<-.000001f||u>1.000001f)return float.PositiveInfinity;
            var q=Vector3.Cross(d,ab);float v=Vector3.Dot(ray.direction,q)*inverse;
            if(v<-.000001f||u+v>1.000001f)return float.PositiveInfinity;
            float distance=Vector3.Dot(ac,q)*inverse;return distance>=-.00001f?Mathf.Max(0,distance):float.PositiveInfinity;
        }

        static bool ScenarioValidationTriangleBox(Vector3 a,Vector3 b,Vector3 c,Bounds box)
        {
            a-=box.center;b-=box.center;c-=box.center;var extents=box.extents;
            if(ScenarioValidationSeparated(a,b,c,extents,Vector3.right)||ScenarioValidationSeparated(a,b,c,extents,Vector3.up)||ScenarioValidationSeparated(a,b,c,extents,Vector3.forward))return false;
            var edges=new[]{b-a,c-b,a-c};
            if(ScenarioValidationSeparated(a,b,c,extents,Vector3.Cross(edges[0],edges[1])))return false;
            foreach(var edge in edges)
                if(ScenarioValidationSeparated(a,b,c,extents,Vector3.Cross(edge,Vector3.right))||ScenarioValidationSeparated(a,b,c,extents,Vector3.Cross(edge,Vector3.up))||ScenarioValidationSeparated(a,b,c,extents,Vector3.Cross(edge,Vector3.forward)))return false;
            return true;
        }

        static bool ScenarioValidationSeparated(Vector3 a,Vector3 b,Vector3 c,Vector3 extents,Vector3 axis)
        {
            if(axis.sqrMagnitude<.00000001f)return false;
            float p=Vector3.Dot(a,axis),q=Vector3.Dot(b,axis),r=Vector3.Dot(c,axis);
            float radius=extents.x*Mathf.Abs(axis.x)+extents.y*Mathf.Abs(axis.y)+extents.z*Mathf.Abs(axis.z);
            return Mathf.Min(p,Mathf.Min(q,r))>radius||Mathf.Max(p,Mathf.Max(q,r))<-radius;
        }

        static bool ScenarioValidationInsideClosedMesh(Vector3 point,ScenarioValidationMesh mesh)
        {
            // Boss batches consist of closed authored parts. A solid-angle winding test also catches
            // complete containment; unlike one-ray parity it does not cancel overlapping same-winding parts.
            double total=0;
            for(int i=0;i<mesh.indices.Length;i+=3)
            {
                var a=mesh.vertices[mesh.indices[i]]-point;var b=mesh.vertices[mesh.indices[i+1]]-point;var c=mesh.vertices[mesh.indices[i+2]]-point;
                double la=a.magnitude,lb=b.magnitude,lc=c.magnitude;
                double numerator=Vector3.Dot(a,Vector3.Cross(b,c));
                double denominator=la*lb*lc+Vector3.Dot(a,b)*lc+Vector3.Dot(b,c)*la+Vector3.Dot(c,a)*lb;
                total+=2*Math.Atan2(numerator,denominator);
            }
            return Math.Abs(total)>2*Math.PI;
        }

        static Bounds ScenarioValidationBounds(IEnumerable<Vector3> points)
        {
            var enumerator=points.GetEnumerator();enumerator.MoveNext();var result=new Bounds(enumerator.Current,Vector3.zero);
            while(enumerator.MoveNext())result.Encapsulate(enumerator.Current);enumerator.Dispose();return result;
        }

        static string ScenarioValidationPath(Transform transform)
        {
            var parts=new List<string>();while(transform!=null){parts.Add(transform.name);transform=transform.parent;}parts.Reverse();return string.Join("/",parts);
        }
    }
}
