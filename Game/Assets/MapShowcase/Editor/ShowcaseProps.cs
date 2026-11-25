using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Anseong.MapShowcase.Editor
{
    /// Human-scale props (vehicles, lights, containers, barriers) so the 35 m mech reads as huge.
    /// Kept out of the 32 m mech/boss lanes, the diagonal M6 route and the space under the crane.
    public static class ShowcaseProps
    {
        const string L = "Assets/LegacySteelworks/Environment/Assets/";
        static readonly Dictionary<string, string> Prefabs = new Dictionary<string, string>
        {
            { "truck", L + "SteelTruck/SteelTruck.prefab" },
            { "dump", L + "ScrapDumpTruck/ScrapDumpTruck.prefab" },
            { "car", L + "StaffDormitory/StaffPassengerCar.prefab" },
            { "light", L + "StreetLight/StreetLight.prefab" },
            { "container", L + "ShippingContainer/ShippingContainer.prefab" },
            { "barrier", L + "ContainerYardEquipment/ConcreteSafetyBarrier.prefab" },
            { "cabinet", L + "ContainerYardEquipment/YardPowerCabinet.prefab" },
            { "stacker", L + "ContainerYardEquipment/ReachStacker.prefab" },
            { "tractor", L + "ContainerYardEquipment/TerminalTractorChassis.prefab" },
            { "booth", L + "ContainerYardEquipment/YardControlBooth.prefab" },
        };

        const float HarborYaw = 270f;   // jib out over the water (east)
        const float LightYaw = 0f;   // turns the generated mast so its arm reaches over the lane

        static bool Blocked(float x, float z)
        {
            if (Mathf.Abs(x) < 18 && z > -5 && z < 410) return true;                 // central 32 m lane Z1..Z2..connector south
            if (z > 387 && z < 423 && x > -18 && x < 108) return true;               // connector east
            if (x > 72 && x < 108 && z > 387 && z < 520) return true;                // connector north to M10
            if (x > 30 && x < 150 && z > 515 && z < 595) return true;                // under the crane
            if (x > 133 && x < 217 && z > 278 && z < 512) return true;               // boss east transport
            // diagonal M6 (0,360) -> (90,490), 18 m either side
            Vector2 a = new Vector2(0, 360), b = new Vector2(90, 490), p = new Vector2(x, z);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            if ((p - (a + t * (b - a))).magnitude < 18) return true;
            return false;
        }

        public static void Apply()
        {
            // generated props (Meshy): built into prefabs once, then instanced many times
            var gen = new Dictionary<string, GameObject>();
            foreach (var g in new[] { new { k = "truck", id = "Prop_CoilTruck", h = 4.2f, lz = true }, new { k = "dump", id = "Prop_DumpTruck", h = 5.0f, lz = true }, new { k = "wreck", id = "Prop_Wreckage", h = 5.0f, lz = false }, new { k = "stock", id = "Prop_SteelStock", h = 4.0f, lz = true },
                new { k = "car", id = "Prop_Sedan", h = 1.45f, lz = true }, new { k = "container", id = "Prop_Container", h = 2.6f, lz = true }, new { k = "light", id = "Prop_LightMast", h = 12f, lz = false },
                new { k = "booth", id = "Prop_Booth", h = 3.3f, lz = true }, new { k = "barrier", id = "Prop_Barrier", h = 1.1f, lz = true },
                new { k = "cabinet", id = "Prop_Transformer", h = 3.0f, lz = true }, new { k = "pipe", id = "Prop_PipeBridge", h = 11f, lz = true }, new { k = "conveyor", id = "Prop_Conveyor", h = 18f, lz = true }, new { k = "hcrane", id = "Prop_HarborCrane", h = 41f, lz = true } })
            {
                var pfb = ShowcaseBuildings.BuildPrefab(g.id, "Assets/MapShowcase/Props/" + g.id, g.h, 2048, g.lz);
                if (pfb != null) { gen[g.k] = pfb; var tb = Object.Instantiate(pfb); var trs = tb.GetComponentsInChildren<Renderer>(); var bb = trs[0].bounds; foreach (var r in trs) bb.Encapsulate(r.bounds); Debug.Log("SHOWCASE genprop " + g.id + " size " + bb.size.ToString("F1")); Object.DestroyImmediate(tb); }
            }
            // flat-colour primitive coils and slab stacks in the storage yard are replaced by the generated steel stock
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (gen.ContainsKey("stock") && (r.name == "stored coil" || r.name == "stacked slab")) r.enabled = false;
            var root = new GameObject("SC_Props");
            // old flat-grey invasion debris primitives -> generated wreckage heaps at the same clusters
            var wreckSpots = new List<Vector3>();
            if (gen.ContainsKey("wreck"))
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (r.enabled && (r.name.StartsWith("fractured slab") || r.name.StartsWith("Z2A") ))
                    { r.enabled = false; var c = r.bounds.center; c.y = 0; if (!wreckSpots.Exists(w => (w - c).magnitude < 28f)) wreckSpots.Add(c); }
            // leftover grey scale-marker boxes ("service vehicle 6m", "1.8m person scale") are blockout aids, not scenery
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.name == "service vehicle 6m" || r.name == "1.8m person scale") r.enabled = false;
            var barriers = new List<Vector2>();
            int placed = 0, skipped = 0;
            System.Action<string, float, float, float> put = (kind, x, z, yaw) =>
            {
                if (Blocked(x, z)) { skipped++; Debug.LogWarning("SHOWCASE prop blocked " + kind + " " + x + "," + z); return; }
                bool g = gen.ContainsKey(kind);
                if (g && (kind == "container" || kind == "booth")) yaw += 90;   // generated ones are long in Z, the old ones were long in X
                if (g && kind == "light") yaw += LightYaw;
                if (g && kind == "barrier") { var q = new Vector2(x, z); if (barriers.Exists(o => (o - q).magnitude < 9f)) return; barriers.Add(q); }   // one generated piece is a 9 m row of three
                if ((kind == "tractor") && gen.ContainsKey("truck")) kind = "truck";
                if ((kind == "stacker") && gen.ContainsKey("dump")) kind = "dump";
                var pf = gen.ContainsKey(kind) ? gen[kind] : (Prefabs.ContainsKey(kind) ? AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[kind]) : null); if (pf == null) { Debug.LogWarning("SHOWCASE missing prop " + kind); return; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
                if (kind != "light")
                {
                    var prs = go.GetComponentsInChildren<Renderer>(); var pb = prs[0].bounds; foreach (var r in prs) pb.Encapsulate(r.bounds);
                    var holder = new GameObject("shadow"); holder.transform.SetParent(go.transform, false);
                    ShowcaseBuild.ContactShadow(holder.transform, new Vector3(pb.center.x - go.transform.position.x, 0.03f, pb.center.z - go.transform.position.z), pb.size.x * 1.5f + 1.2f, pb.size.z * 1.25f + 1.2f, 0.6f);
                }
                if (kind != "wreck")   // simple blocking volume so the mech and shots do not pass through; wreckage stays walk-over
                {
                    var crs = go.GetComponentsInChildren<Renderer>(); var cb = crs[0].bounds; foreach (var r in crs) if (r.name != "SC_ContactShadow") cb.Encapsulate(r.bounds);
                    cb = crs[0].bounds; foreach (var r in crs) if (r.name != "SC_ContactShadow") cb.Encapsulate(r.bounds);
                    var bc = go.AddComponent<BoxCollider>(); bc.center = cb.center - go.transform.position; bc.size = kind == "light" ? new Vector3(0.8f, cb.size.y, 0.8f) : cb.size;
                }
                go.transform.position = new Vector3(x, kind == "wreck" ? -0.25f : 0.02f, z); go.transform.rotation = Quaternion.Euler(0, yaw, 0); placed++;
            };
            foreach (var kv in Prefabs)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value); if (pf == null) continue;
                var tmp = Object.Instantiate(pf); var rs = tmp.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); Debug.Log("SHOWCASE propsize " + kv.Key + " " + b.size.ToString("F1") + " minY " + b.min.y.ToString("F2")); }
                Object.DestroyImmediate(tmp);
            }
            // street lights along both sides of the central lane and the connectors
            for (int z = 20; z <= 400; z += 38) { put("light", -21, z, 90); put("light", 21, z, -90); }
            for (int x = 25; x <= 70; x += 40) { put("light", x, 426, 180); }
            for (int z = 430; z <= 505; z += 37) { put("light", 111, z, -90); put("light", 69, z, 90); }
            put("car", 48, 55, 0); put("car", 53, 55, 180); put("car", 58, 55.5f, 0);   // where the scale-marker boxes stood
            // Z1 yard: parked service vehicles and cars by the buildings
            put("car", 34, 30, 0); put("car", 37.5f, 30, 0); put("car", 41, 30.5f, 4); put("car", 48, 30, 0);
            put("truck", -42, 72, 100); put("dump", -50, 86, 80); put("cabinet", 30, 62, 90); put("barrier", 24, 70, 0); put("barrier", 24, 74, 0); put("barrier", 24, 78, 0);
            // along the steel hall B04 west side
            put("truck", 44, 112, 0); put("truck", 44, 128, 0); put("tractor", 43, 150, 180); put("car", 47, 170, 0); put("car", 47, 175, 0); put("container", 40, 190, 90);
            // Z2 storage yard edges
            put("container", -70, 200, 0); put("container", -70, 203, 0); put("container", -70, 201.5f, 0); put("container", -84, 200, 90);
            put("stacker", -60, 215, 30); put("truck", -80, 265, 90); put("truck", -80, 272, 90); put("dump", -75, 300, 60);
            put("truck", 70, 215, -90); put("tractor", 72, 228, -90); put("car", 95, 200, 0); put("car", 98.5f, 200, 0); put("car", 102, 200, 0);
            put("container", 95, 300, 0); put("container", 95, 303, 0); put("container", 95, 306, 0); put("container", 82, 303, 90); put("stacker", 76, 318, 200);
            put("booth", 40, 345, 180); put("barrier", 30, 352, 90); put("barrier", 34, 352, 90); put("barrier", 38, 352, 90);
            // diagonal route sides
            put("truck", -30, 392, 60); put("car", -34, 420, 0); put("dump", 20, 445, 120); put("truck", 40, 470, 40); put("cabinet", 62, 440, 0);
            // Z3 crane yard: outside the crane footprint
            put("truck", 22, 540, 0); put("truck", 22, 556, 0); put("car", 20, 580, 0); put("car", 20, 584, 0);
            put("stacker", 160, 560, -90); put("container", 165, 585, 0); put("container", 165, 588, 0); put("container", 178, 586, 90);
            put("truck", 60, 612, 90); put("dump", 120, 615, 80); put("car", 70, 640, 90); put("car", 76, 640, 90); put("car", 82, 640.5f, 92); put("booth", 150, 610, 180);
            for (int x = 30; x <= 150; x += 40) put("light", x, 604, 180);
            // large process structures along the yard edges, outside every lane, so the open concrete is framed by plant
            for (int i = 0; i < 7; i++) put("pipe", -112, 305 + i * 30, 0);                       // pipe bridge run from the warehouse towards gas cleaning
            for (int i = 0; i < 4; i++) put("pipe", 232 + i * 30, 262, 90);                       // east run towards the harbour
            put("conveyor", 236, 205, 0); put("conveyor", -62, 662, 90); put("conveyor", -205, 95, 180);
            // the blockout harbour cranes and the fractured pipe bridge
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                if (gen.ContainsKey("hcrane") && r.name.StartsWith("HARBOR crane")) { var c = r.bounds.center; r.enabled = false; put("hcrane", c.x, c.z, HarborYaw); }
            }
            // steel stock where the primitive coils / slabs stood, plus more along the yard edges
            put("stock", -53, 232, 0); put("stock", -53, 248, 0); put("stock", 50, 250, 90); put("stock", 62, 262, 90); put("stock", -95, 320, 0); put("stock", 100, 240, 0);
            put("stock", 30, 100, 90); put("stock", 170, 610, 90); put("stock", -40, 330, 90); put("stock", 60, 180, 90);
            // invasion wreckage outside the lanes
            put("wreck", -45, 150, 20); put("wreck", 93, 240, 75);   /* the burn scar and red glow at (92,238) had nothing lying on it */ put("wreck", 62, 330, 140); put("wreck", -70, 345, 250); put("wreck", -32, 470, 60); put("wreck", 125, 602, 200);
            put("wreck", 30, 500, 300); put("wreck", -100, 215, 100); put("wreck", 118, 380, 30); put("wreck", 25, 620, 170); put("wreck", -40, 40, 80);
            int wi = 0; foreach (var w in wreckSpots) { if (wi >= 14) break; put("wreck", w.x, w.z, (wi * 67) % 360); wi++; }
            Debug.Log("SHOWCASE wreck clusters " + wreckSpots.Count);
            // the kept crane trolley / hoist blockout was flat pale yellow on top of the rusty generated crane
            {
                var mp = "Assets/MapShowcase/Materials/SC_CraneMachinery.mat"; var cm = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (cm == null) { cm = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(cm, mp); }
                cm.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MapShowcase/Textures/SC_GroundDetail_Concrete.png")); cm.SetTextureScale("_MainTex", new Vector2(3, 3));
                cm.color = new Color(.62f, .46f, .22f); cm.SetFloat("_Metallic", 0.35f); cm.SetFloat("_Glossiness", 0.22f); EditorUtility.SetDirty(cm);
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name == "B07 Crane 68m / underside 62m")
                        foreach (var r in t.GetComponentsInChildren<Renderer>()) if (r.enabled) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = cm; r.sharedMaterials = ms; }
            }
            var pp = "Assets/MapShowcase/Prefabs/SC_Props.prefab"; AssetDatabase.DeleteAsset(pp);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, pp, InteractionMode.AutomatedAction);
            Debug.Log("SHOWCASE props placed " + placed + " skipped " + skipped);
        }
    }
}
