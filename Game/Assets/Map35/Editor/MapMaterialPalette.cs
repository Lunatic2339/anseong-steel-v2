using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static readonly Dictionary<MeshRenderer,string[]> originalMaterialSlots=new Dictionary<MeshRenderer,string[]>();
        static readonly Dictionary<string,Material> industrialPalette=new Dictionary<string,Material>();
        static readonly string[] materialThemeNames={"warm ivory repair hangar","cool weathered maintenance","faded teal pump utilities","oxidised red steelmaking","slate blue rolling mill","warm galvanised silo","ochre handling crane","dark heat stained furnace","sage gas cleaning"};
        static readonly Color[] materialThemeColors={new Color(.66f,.64f,.55f),new Color(.47f,.53f,.55f),new Color(.29f,.43f,.4f),new Color(.47f,.29f,.22f),new Color(.36f,.44f,.49f),new Color(.66f,.64f,.55f),new Color(.82f,.59f,.15f),new Color(.31f,.29f,.26f),new Color(.44f,.51f,.44f)};
        static string PaletteRole(string id,string objectName,string source)
        {
            string n=(objectName+" "+source).ToLowerInvariant();
            string src=source.ToLowerInvariant();
            // Shared combined meshes carry several functions: the source slot takes precedence over the building's name.
            if(src=="agedcladding"||src=="whitecladding")return "paint";
            if(src=="openings"||src=="windows_cables"||src=="frameandwindows"||src=="joints"||src=="sootinterior")return "metal";
            if(src=="oxideroof")return "roof";
            if(src=="safetyandutility")return "warning";
            if(n.Contains("glass")||n.Contains("glazing"))return "glass";
            if(n.Contains("scorch")||n.Contains("soot"))return "preserve";
            if(n.Contains("concrete")||n.Contains("foundation")||n.Contains("plinth")||n.Contains("footing")||n.Contains("hardstand"))return "concrete";
            if(n.Contains("torn")||(id=="B02"&&n.Contains("folded"))||n.Contains("buckled")||n.Contains("rust")||n.Contains("oxideedge")||src.Contains("oxidised"))return "rust";
            if(source.ToLowerInvariant().Contains("structural"))return "metal";
            if(n.Contains("roof")||n.Contains("weatheredseams"))return "roof";
            if(n.Contains("safety")||n.Contains("warning")||n.Contains("yellow")||n.Contains("ladder rung"))return "warning";
            if(id=="B01"&&(objectName.EndsWith("wall")||objectName.Contains("front pier")||objectName.StartsWith("door lintel")))return "paint";
            if(id=="B07"&&(n.Contains("girder")||n.Contains("crane beam")))return "paint";
            if(n.Contains("frameandwindows")||n.Contains("structural")||n.Contains("dark_steel")||n.Contains("girder")||n.Contains("beam")||n.Contains("column")||n.Contains("girt")||n.Contains("joint")||n.Contains("gusset")||n.Contains("bolt")||n.Contains("frame")||n.Contains("ladder")||n.Contains("rail"))return "metal";
            if(n.Contains("galvanizedshell")||n.Contains("galvanizedsteel")||n.Contains("pipe")||n.Contains("duct")||n.Contains("shellring"))return "galvanized";
            return "paint";
        }
        static Material ThemeMaterial(int theme,string role,int variant)
        {
            string key="B"+(theme+1).ToString("00")+"_"+role+(role=="paint"?"_"+variant:"");
            if(industrialPalette.TryGetValue(key,out var cached))return cached;
            Color color=materialThemeColors[theme];string source="Metal049A";float tile=6,metal=.06f,normal=.2f,calm=.06f,rough=.6f,grime=.45f;
            switch(role)
            {
                case "paint":
                    source=theme==2?"plaster_grey_04":theme==5?"Metal050A":theme==1||theme==7?"Metal063":theme==3||theme==4||theme==8?"Metal032":"Metal049A";
                    if(variant==1&&theme!=2&&theme!=5&&theme!=6)source="Metal063";
                    color*=variant==1?.92f:variant==2?1.05f:1;metal=theme==5?.5f:.06f;normal=theme==2?.4f:.22f;break;
                case "metal":source="Metal027";color=Color.Lerp(color,new Color(.67f,.7f,.7f),.65f);tile=3.5f;metal=.05f;normal=.25f;calm=.08f;rough=.6f;grime=.2f;break;
                case "concrete":source="Concrete006";color=new Color(.64f+(theme%3)*.025f,.64f+(theme%2)*.015f,.59f+(theme%3)*.025f);tile=3;metal=0;normal=.4f;rough=.8f;grime=.45f;break;
                case "rust":source="rust_coarse_01";color=new Color(.69f,.58f,.46f);tile=2.5f;metal=.02f;normal=.55f;calm=.06f;rough=.8f;grime=.25f;break;
                case "roof":source=theme==0||theme==2?"bitumen":theme==5?"Metal050A":theme==7?"Metal027":"Metal063";color=theme==3?new Color(.57f,.39f,.28f):new Color(.44f,.47f,.47f);tile=4;metal=theme==5?.5f:.08f;normal=.3f;calm=.08f;rough=.7f;grime=.1f;break;
                case "warning":source="Metal049A";color=new Color(.84f,.59f,.14f);tile=2;metal=.04f;normal=.18f;rough=.65f;grime=.4f;break;
                case "galvanized":source="Metal050A";color=theme==5?new Color(.66f,.64f,.55f):new Color(.59f,.63f,.62f);tile=2;metal=.55f;normal=.2f;rough=.48f;grime=.35f;break;
                case "glass":
                    var glass=Material("Palette_"+key,new Color(.055f,.1f,.11f));glass.SetFloat("_Glossiness",.72f);glass.SetFloat("_Metallic",.15f);industrialPalette[key]=glass;return glass;
            }
            var result=PbrMaterial("Palette_"+key,source,color,tile,metal,normal);
            float gain=source=="Metal032"?1.5f:source=="Metal063"?1.75f:source=="Metal027"?1.4f:source=="plaster_grey_04"?1.3f:source=="rust_coarse_01"?1.2f:1;
            result.SetFloat("_AlbedoGain",gain);
            if(source=="bitumen")result.SetFloat("_TileMetres",20);
            if(source=="plaster_grey_04")result.SetFloat("_TileMetres",3);
            result.SetFloat("_SurfaceCalm",calm);result.SetFloat("_RoughnessFloor",rough);result.SetFloat("_MacroStrength",.14f);result.SetFloat("_GrimeStrength",grime);
            result.SetFloat("_Saturation",role=="paint"?.25f:.55f);
            if(theme==1&&role=="paint")
            {
                // Preserve intact paint; local rust is a second physical material, not a global brown tint.
                result.SetTexture("_RustAlbedo",PbrChannel("rust_coarse_01","Color"));
                result.SetTexture("_RustNormal",PbrChannel("rust_coarse_01","NormalGL"));
                result.SetTexture("_RustRoughness",PbrChannel("rust_coarse_01","Roughness"));
                result.SetFloat("_WearAmount",variant==0?.38f:variant==1?.62f:.22f);
                result.SetFloat("_WearSeed",17+variant*31);
                result.SetFloat("_RustTileMetres",1.6f);
                result.SetColor("_RustTint",new Color(1.0f,.82f,.62f));
            }
            var texture=result.mainTexture;if(texture!=null)result.SetVector("_TileAspect",new Vector4(1,(float)texture.height/texture.width,1,1));
            industrialPalette[key]=result;EditorUtility.SetDirty(result);return result;
        }
        static int StablePaletteVariant(string key)
        {unchecked{uint hash=2166136261;foreach(char c in key)hash=(hash^c)*16777619;return (int)(hash%3);}}
        static void PaletteBuilding(Transform root,int theme,StringBuilder report)
        {
            string id="B"+(theme+1).ToString("00");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.GetComponent<TextMesh>()!=null)continue;
                var current=renderer.sharedMaterials;originalMaterialSlots.TryGetValue(renderer,out var original);
                for(int i=0;i<current.Length;i++)
                {
                    if(current[i]==null)continue;
                    string name=original!=null&&i<original.Length?original[i]:current[i].name;
                    if(current[i].shader.name=="Anseong/SurfaceSoot")continue;
                    string role=PaletteRole(id,renderer.name,name);if(role=="preserve")continue;
                    int variant=StablePaletteVariant(root.name+"/"+renderer.name+"/"+i);
                    current[i]=ThemeMaterial(theme,role,variant);
                    report.AppendLine(root.name.Replace(',','_')+","+renderer.name.Replace(',','_')+","+i+","+name.Replace(',','_')+","+role+","+current[i].name);
                }
                renderer.sharedMaterials=current;
            }
        }
        static void ApplyIndustrialPalette()
        {
            industrialPalette.Clear();var report=new StringBuilder("building,mesh,slot,source_material,role,assigned_material\n");
            for(int i=0;i<9;i++)PaletteBuilding(Existing("B"+(i+1).ToString("00")),i,report);
            // Buildings in each background district share a restrained local colour family.
            foreach(Transform child in districts)
            {
                if(child.name.Contains("crane")){PaletteBuilding(child,6,report);continue;}
                if(child.name.IndexOf("district",StringComparison.OrdinalIgnoreCase)<0&&child.name.IndexOf("background",StringComparison.OrdinalIgnoreCase)<0)continue;
                int theme=child.position.z<180?0:child.position.z<420?(child.position.x<0?4:3):(child.position.x<0?8:7);
                PaletteBuilding(child,theme,report);
            }
            File.WriteAllText(WideOutput+"/material-palette-assignment.csv",report.ToString());
            File.WriteAllText(WideOutput+"/material-palette-summary.txt","Unique theme materials: "+industrialPalette.Count+"\n"+string.Join("\n",materialThemeNames.Select((n,i)=>"B"+(i+1).ToString("00")+": "+n))+"\nBuilding colour families are production proposals, not a new user lock. Painted panels have three subtle wear/tone variants; source surface types remain semantic.\n");
        }
    }
}
