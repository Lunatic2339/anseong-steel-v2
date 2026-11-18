using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.Map35.Editor
{
    public static class LegacyEnvironmentAudit
    {
        public const string Root="Assets/LegacySteelworks/Environment";
        public const string Output="../docs/validation/map-wide";
        static Color SavedColor(Material material)
        {
            var colors=new SerializedObject(material).FindProperty("m_SavedProperties.m_Colors");
            foreach(string key in new[]{"_BaseColor","_Color","_Deep"})
                for(int i=0;i<colors.arraySize;i++)
                {var e=colors.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("first").stringValue==key)return e.FindPropertyRelative("second").colorValue;}
            return new Color(.4f,.43f,.45f);
        }
        static Texture SavedTexture(Material material)
        {
            var textures=new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
            foreach(string key in new[]{"_BaseMap","_MainTex"})
                for(int i=0;i<textures.arraySize;i++)
                {var e=textures.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("first").stringValue==key)
                    return e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;}
            return null;
        }
        public static void AdaptCopiedMaterials()
        {
            // Mesh and particle shaders have different depth/blending contracts. In the
            // first import Smoke was also made opaque Standard, leaving square billboards.
            var particleMaterials=new HashSet<Material>();
            foreach(var id in AssetDatabase.FindAssets("t:Prefab",new[]{Root}))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id));
                if(prefab==null)continue;
                foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    foreach(var material in renderer.sharedMaterials)if(material!=null)particleMaterials.Add(material);
            }
            int count=0,particles=0;
            foreach(var id in AssetDatabase.FindAssets("t:Material",new[]{Root}))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));
                if(particleMaterials.Contains(m))
                {
                    if(m.name.IndexOf("smoke",StringComparison.OrdinalIgnoreCase)>=0)
                    {
                        m.shader=Shader.Find("Anseong/SoftIndustrialSmoke");
                        if(m.shader==null)throw new InvalidOperationException("Smoke shader unavailable during legacy adaptation.");
                        m.SetColor("_Color",new Color(.39f,.42f,.44f,.58f));
                        m.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
                        m.shaderKeywords=Array.Empty<string>();EditorUtility.SetDirty(m);particles++;
                    }
                    // Other particle effects retain their authored shader rather than
                    // being silently treated as an opaque architectural material.
                    continue;
                }
                if(m.shader!=null && m.shader.name=="Standard")continue;
                var c=SavedColor(m);var texture=SavedTexture(m);m.shader=Shader.Find("Standard");
                m.color=c;m.mainTexture=texture;m.SetFloat("_Glossiness",.18f);m.enableInstancing=true;
                m.shaderKeywords=Array.Empty<string>();EditorUtility.SetDirty(m);count++;
            }
            AssetDatabase.SaveAssets();Debug.Log("LEGACY_MATERIALS_ADAPTED "+count+" / smoke materials recovered "+particles);
        }
        public static Bounds BoundsOf(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);Bounds b=new Bounds(root.transform.position,Vector3.zero);bool first=true;
            foreach(var r in renderers){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}return b;
        }
        public static void StripSimulation(GameObject root)
        {
            foreach(var joint in root.GetComponentsInChildren<Joint>(true))UnityEngine.Object.DestroyImmediate(joint);
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))if(behaviour!=null)UnityEngine.Object.DestroyImmediate(behaviour);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach(var rb in root.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(rb);
        }
        public static void Render(Camera camera,string file,int width=1600,int height=1000)
        {
            var target=new RenderTexture(width,height,24);camera.targetTexture=target;camera.aspect=(float)width/height;camera.Render();
            var before=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG());
            RenderTexture.active=before;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
        }
        [MenuItem("Anseong/Map35/Inspect Existing Environment")]
        public static void Audit()
        {
            Directory.CreateDirectory(Output);AdaptCopiedMaterials();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var inventory=new StringBuilder("asset,size_x,size_y,size_z,renderers,triangles,behaviours\n");
            foreach(var id in AssetDatabase.FindAssets("t:Prefab",new[]{Root}))
            {
                string path=AssetDatabase.GUIDToAssetPath(id);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var b=BoundsOf(go);long tris=0;
                foreach(var mesh in go.GetComponentsInChildren<MeshFilter>(true))if(mesh.sharedMesh!=null)
                    for(int n=0;n<mesh.sharedMesh.subMeshCount;n++)tris+=(long)mesh.sharedMesh.GetIndexCount(n)/3;
                inventory.AppendLine(path+","+b.size.x+","+b.size.y+","+b.size.z+","+go.GetComponentsInChildren<Renderer>(true).Length+","+tris+","+go.GetComponentsInChildren<MonoBehaviour>(true).Length);
                StripSimulation(go);UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(Output+"/legacy-prefabs.csv",inventory.ToString());
            var scene=EditorSceneManager.OpenScene("Assets/LegacySteelworks/SourceLayout/OriginalReferenceLayout.unity");
            var report=new StringBuilder("Existing map roots and bounds (copied scene; missing robot is not imported):\n");
            foreach(var root in scene.GetRootGameObjects())
            {
                report.AppendLine(root.name+" | "+BoundsOf(root)+" | renderers="+root.GetComponentsInChildren<Renderer>(true).Length);
                StripSimulation(root);
            }
            foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            var view=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c=>c.name.Contains("ReferenceOverview"));
            if(view!=null){view.enabled=true;Render(view,Output+"/legacy-overview.png");report.AppendLine("Original overview camera: "+view.transform.position+" rot="+view.transform.eulerAngles);}
            File.WriteAllText(Output+"/legacy-layout.txt",report.ToString());
            Directory.CreateDirectory("Assets/Map35/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Map35/Scenes/LegacyReferenceReview.unity");
            Debug.Log("LEGACY_AUDIT_COMPLETE");
        }
    }
}
