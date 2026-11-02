from pathlib import Path
import sys,json
root=Path(__file__).parent
sys.path.insert(0,str(root/'road_geometry_lib'))
from shapely import Polygon,box,union_all,constrained_delaunay_triangles
from shapely.geometry.polygon import orient
data=json.loads((root/'road_union_result.json').read_text())
xy=next(a['xy'] for a in data['layers'] if a['channel']==0)
roads=union_all([Polygon([(xy[i],xy[i+1]),(xy[i+2],xy[i+3]),(xy[i+4],xy[i+5])]) for i in range(0,len(xy),6)]).buffer(.12)
zones=[box(174,-600,188,640),box(241.5,-600,248.5,640)]
cuts=[-600,-320,-80,160,340,500,640]
zones += [box(188,a,241.5,b) for a,b in zip(cuts,cuts[1:])]
layers=[]
for i,zone in enumerate(zones):
    g=zone.difference(roads);coords=[];area=0
    assert g.is_valid
    for tri in constrained_delaunay_triangles(g).geoms:
        area+=tri.area
        for x,z in list(orient(tri,sign=1).exterior.coords)[:3]:coords.extend([x,z])
    assert abs(area-g.area)<.01
    layers.append(dict(channel=i,xy=coords))
assert sum(a.intersection(b).area for i,a in enumerate(zones) for b in zones[i+1:])<.001
(root/'quay_detail_result.json').write_text(json.dumps(dict(layers=layers,report='Eight surface zones; disjoint polygons, road subtraction and triangulation area checks passed.')))
print('QUAY_GEOMETRY_PASS zones=8 roadOverlap=0')
