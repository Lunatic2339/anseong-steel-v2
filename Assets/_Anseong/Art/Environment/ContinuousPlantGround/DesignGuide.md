# Continuous road-based ground design

Factory parcels from existing curved roads: 6; building-based material rectangles:0.

- Surface1: 323722.6m²
- Surface2: 173897.0m²
- Surface3: 261638.3m²
- Surface4: 4616.0m²
- Surface5: 78117.0m²
- Surface6: 35454.9m²

Checks passed: road exclusion, building clearance for green strips, empty combat core, no material-zone overlap, full factory coverage, exact triangulation area.

Visual reference: supplied aerial steelworks photo and POSCO official Pohang/Gwangyang overview photographs. https://posco.co.kr/homepage/docs/kor7/jsp/common/posco/s91a1000012c.jsp
Building-footprint material rectangles are disabled, while their meshes remain available for rollback. Existing screening-tree children remain active. Base floors share one pale concrete. Subtle light paving variations follow road-delimited factory parcels, never individual building boxes. Roadside green strips and pale walk/curb bands follow the curved road geometry and exclude buildings, roads and the combat core. No new bitmap textures or shaders; existing quay shader and compressed grain map reused. One combined six-submesh ground renderer. Greenery uses solid opaque geometry, no grass blades, colliders or runtime scripts. VR performance unmeasured.

Mesh optimisation:39351 unshared triangle vertices welded to13144 unique vertices; same triangles and six material slots. Serialized Unity mesh source approximately0.95MB; build format size depends on Unity import/build settings.
