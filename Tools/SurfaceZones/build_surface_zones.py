from pathlib import Path
import sys,json
root=Path(__file__).parent
sys.path.insert(0,str(root/'road_geometry_lib'))
from shapely import Polygon,union_all,constrained_delaunay_triangles
from shapely.geometry.polygon import orient
data=json.loads((root/'surface_zone_plan.json').read_text(encoding='utf-8-sig'))
source=[[],[],[],[],[],[]]
for a in data['areas']:
    source[a['type']].append(Polygon(list(zip(a['xy'][::2],a['xy'][1::2]))))
zones=[union_all(g) for g in source]
zones[5]=zones[0].buffer(9,join_style='mitre').difference(zones[0])
road_data=json.loads((root/'road_union_result.json').read_text(encoding='utf-8'))
road_vertices=next(a['xy'] for a in road_data['layers'] if a['channel']==0)
road=union_all([Polygon([(road_vertices[j],road_vertices[j+1]),(road_vertices[j+2],road_vertices[j+3]),(road_vertices[j+4],road_vertices[j+5])]) for j in range(0,len(road_vertices),6)])
covered=Polygon()
clean=[None]*6
for typ in [3,2,1,0,4,5]:
    clean[typ]=zones[typ].difference(road.buffer(.08)).difference(covered).simplify(.025)
    covered=union_all([covered,clean[typ]])
def parts(g):
    if g.is_empty:return []
    if g.geom_type=='Polygon':return [g]
    return [p for a in getattr(g,'geoms',[]) for p in parts(a)]
layers=[]
for typ,g in enumerate(clean):
    coords=[];total=0
    for p in parts(g):
        if p.area<.02:continue
        for tri in constrained_delaunay_triangles(p).geoms:
            tri=orient(tri,sign=1);total+=tri.area
            for x,y in list(tri.exterior.coords)[:3]:coords.extend([round(x,5),round(y,5)])
    assert abs(total-g.area)<.2
    layers.append({'channel':typ,'xy':coords})
for i,a in enumerate(clean):
    assert a.is_valid
    assert a.intersection(road).area<.01
    for b in clean[i+1:]:assert a.intersection(b).area<.01
report='# Functional ground zoning\n\nProduction / warehouse dispatch / container handling / quay / shared haul yard / unpaved boundary verge. Derived from actual scene footprint positions; existing road surface subtracted, patches mutually non-overlapping.\n\n'
for typ,g in enumerate(clean):report+=f'- Zone{typ+1}: {g.area:.1f} m²\n'
report+='\nPolygon validity, no road overlap, no inter-zone overlap and triangulation area checks: passed.\n'
(root/'surface_zone_result.json').write_text(json.dumps({'layers':layers,'report':report}),encoding='utf-8')
print(report)
