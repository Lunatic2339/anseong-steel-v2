using System.IO;
using UnityEditor;
using UnityEngine;
namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static void Surface(Material material,float metres,float calm,float normal,float macro)
        {
            material.SetFloat("_TileMetres",metres);material.SetFloat("_SurfaceCalm",calm);material.SetFloat("_NormalStrength",normal);material.SetFloat("_MacroStrength",macro);EditorUtility.SetDirty(material);
        }
        static void RefineSurfaceScale()
        {
            // A source material named Steel does not mean that a whole hangar wall is a structural beam.
            foreach(string name in new[]{"west wall","east wall","back wall","left front pier","right front pier","door lintel 42m clear"})
            {var part=Existing("B01").Find(name);if(part!=null&&!part.GetComponent<MeshRenderer>().sharedMaterial.name.Contains("Palette_"))part.GetComponent<MeshRenderer>().sharedMaterial=agedSteel;}
            Surface(agedSteel,6,.1f,.2f,.10f);agedSteel.SetFloat("_AlbedoGain",1);agedSteel.SetFloat("_Metallic",.08f);
            agedSteel.SetFloat("_GrimeStrength",.55f);
            Surface(structuralSteel,3.5f,.30f,.35f,.06f);
            Surface(agedConcrete,4,.12f,.3f,.1f);
            agedConcrete.SetFloat("_GrimeStrength",.35f);
            Surface(damagedConcrete,3,.08f,.65f,.08f);
            Surface(agedRust,4,.1f,.45f,.12f);
            Surface(agedRoof,6,.35f,.25f,.1f);
            Surface(agedYellow,2,.40f,.25f,.02f);
            Surface(asphalt,5,.12f,.55f,.1f);
            File.WriteAllText(WideOutput+"/surface-scale-audit.txt","World-metre UVs retained. Exterior coated steel: Metal049A-derived low metalness coating, 6x6m tile, low normal strength; structural metal: 3.5m; concrete: Concrete034 4x2m preserving 2:1 source aspect; broken concrete: 3m; rusty damaged sheets: 4m; roof: 6m.\nBroad 19m world-space variation is separate from close PBR maps. Imported models retain authored UV0 and are excluded. Worn classifier red token bug fixed: Weathered_Grey_Steel is no longer rust.\n");
        }
    }
}
