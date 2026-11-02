import json, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / 'road_geometry_lib'))
from shapely import Polygon, Point, box, union_all, constrained_delaunay_triangles
from shapely.geometry.polygon import orient

root = Path(__file__).parent
data = json.loads((root/'road_union_input.json').read_text(encoding='utf-8-sig'))
groups = [[], [], []]
for item in data['triangles']:
    p = Polygon(list(zip(item['xy'][::2], item['xy'][1::2])))
    if p.is_valid and p.area > 1e-7:
        groups[item['channel']].append(p)
old = union_all(groups[0], grid_size=0.002)
# Closing fills tiny junction notches; opening removes square lips and rounds caps.
road = old.buffer(8.0, quad_segs=12).buffer(-8.0, quad_segs=12)
road = road.buffer(-1.3, quad_segs=8).buffer(1.3, quad_segs=8)
road = road.simplify(.025, preserve_topology=True)
obstacles = union_all([box(*b['xy']).buffer(.25) for b in data['buildings']])
# Preserve existing access aprons, but never introduce new road intrusion.
new_intrusion = road.difference(old).intersection(obstacles)
road = road.difference(new_intrusion)
components = list(road.geoms) if road.geom_type == 'MultiPolygon' else [road]
components = [p for p in components if p.area >= 8]
road = union_all(components)
inside = road.buffer(-1.0)
intersection_mask = union_all([Point(p['x'],p['y']).buffer(17) for p in data['junctions']])
# New outer edge paint follows the merged junction geometry, never old overlapping strips.
edge = road.buffer(-.28).difference(road.buffer(-.40))
white = union_all(groups[1]).intersection(inside).difference(intersection_mask)
white = union_all([edge, white])
yellow = union_all(groups[2]).intersection(inside).difference(intersection_mask)

def polygon_parts(g):
    if g.is_empty: return []
    if g.geom_type == 'Polygon': return [g]
    if hasattr(g, 'geoms'): return [p for a in g.geoms for p in polygon_parts(a)]
    return []

layers=[]
for channel,g in enumerate([road,white,yellow]):
    coords=[]
    area=0.0
    for p in polygon_parts(g):
        if p.area < .002: continue
        triangles=constrained_delaunay_triangles(p)
        for tri in triangles.geoms:
            tri=orient(tri,sign=1)
            area += tri.area
            for x,y in list(tri.exterior.coords)[:3]: coords.extend([round(x,5), round(y,5)])
    assert abs(area-g.area) < max(.1,g.area*.0005), (channel,area,g.area)
    layers.append({'channel':channel,'xy':coords})

coverage=road.intersection(old).area/old.area
assert road.is_valid and coverage > .97, coverage
assert road.difference(old).intersection(obstacles).area < .01
report=f'''# Unified smooth road geometry

Merged existing active road triangles into a planar polygon union. Closed small junction gaps/notches and filleted turning corners (8m radius); rounded square caps/lips (1.3m opening). Retained irregular centre-line layout and 7/14m road widths away from corners. Regenerated continuous edge paint from the new outline; retained interior dashed lanes, with junction clearance. No stacked asphalt polygons or doubled paint. Source road objects disabled for recovery; terrain, buildings, vehicles and retained rear boundary unchanged.

- Original asphalt area: {old.area:.2f} m²
- Clean asphalt area: {road.area:.2f} m²
- Original coverage retained: {coverage:.3%}
- Connected asphalt components: {len(components)} (includes separate existing service approaches)
- New building intrusion clipped: {new_intrusion.area:.3f} m²
- Polygon validity and triangulated area checks: passed for all layers
- VR performance and vehicle driving not tested
'''
(root/'road_union_result.json').write_text(json.dumps({'layers':layers,'report':report}),encoding='utf-8')
print(report)
