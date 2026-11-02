from pathlib import Path
import sys,json
root=Path(__file__).parent
sys.path.insert(0,str(root/'road_geometry_lib'))
from shapely import Polygon,box,union_all,constrained_delaunay_triangles
from shapely.geometry.polygon import orient
local=(root/'factory_paving_plan.json').exists()
plan_path=root/'factory_paving_plan.json' if local else root/'TruckBuildProject/Tools/FactoryPavingDesign/factory_paving_plan.json'
plan=json.loads(plan_path.read_text())
old=json.loads((root.parent/'SurfaceZones/surface_zone_plan.json' if local else root/'surface_zone_plan.json').read_text())
boundary=Polygon(list(zip(old['areas'][0]['xy'][::2],old['areas'][0]['xy'][1::2])))
boundary=union_all([boundary,box(-215,-280,-151,445),box(-151,269,180,445)])
r=json.loads((root.parent/'CentrelineRoads/road_union_result.json' if local else root/'road_union_result.json').read_text())
xy=next(a['xy'] for a in r['layers'] if a['channel']==0)
road=union_all([Polygon([(xy[i],xy[i+1]),(xy[i+2],xy[i+3]),(xy[i+4],xy[i+5])]) for i in range(0,len(xy),6)]).buffer(.12)
source=[[] for _ in range(5)]
for a in plan['areas']:source[a['type']].append(Polygon(list(zip(a['xy'][::2],a['xy'][1::2]))))
source[0]=[boundary]
clean=[None]*5;covered=Polygon()
for i in [4,3,2,1,0]:
    clean[i]=union_all(source[i]).intersection(boundary).difference(road).difference(covered)
    covered=union_all([covered,clean[i]])
layers=[];report='# Factory five-surface paving\n\n'
for i,g in enumerate(clean):
    assert g.is_valid and g.area>1
    coords=[];area=0
    for tri in constrained_delaunay_triangles(g).geoms:
        area+=tri.area
        for x,z in list(orient(tri,sign=1).exterior.coords)[:3]:coords.extend([x,z])
    assert abs(area-g.area)<.01
    layers.append(dict(channel=i,xy=coords));report+=f'- Type{i+1}: {g.area:.1f}m²\n'
assert sum(a.intersection(b).area for i,a in enumerate(clean) for b in clean[i+1:])<.01
assert covered.symmetric_difference(boundary.difference(road)).area<.01
report+='\nFive populated surface types; road exclusion, mutual non-overlap, full boundary coverage and triangulation area checks passed.\n'
(root/'factory_paving_result.json' if local else root/'TruckBuildProject/Tools/FactoryPavingDesign/factory_paving_result.json').write_text(json.dumps(dict(layers=layers,report=report)))
print(report)
