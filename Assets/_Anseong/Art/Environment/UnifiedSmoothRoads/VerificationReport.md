# Unified smooth road geometry

Merged existing active road triangles into a planar polygon union. Closed small junction gaps/notches and filleted turning corners (8m radius); rounded square caps/lips (1.3m opening). Retained irregular centre-line layout and 7/14m road widths away from corners. Regenerated continuous edge paint from the new outline; retained interior dashed lanes, with junction clearance. No stacked asphalt polygons or doubled paint. Source road objects disabled for recovery; terrain, buildings, vehicles and retained rear boundary unchanged.

- Original asphalt area: 105018.47 m²
- Clean asphalt area: 106032.11 m²
- Original coverage retained: 99.973%
- Connected asphalt components: 1 (includes separate existing service approaches)
- New building intrusion clipped: 0.000 m²
- Polygon validity and triangulated area checks: passed for all layers
- VR performance and vehicle driving not tested
