from pathlib import Path
import sys,json
root=Path(__file__).parent
sys.path.insert(0,str(root/'road_geometry_lib'))
from shapely import Polygon,box,union_all,constrained_delaunay_triangles
from shapely.geometry.polygon import orient
local=(root/'obstacles.json').exists()
work=root if local else root/'TruckBuildProject/Tools/ContinuousPlantGround'
sources=root.parent if local else root
old=json.loads((sources/'SurfaceZones/surface_zone_plan.json' if local else root/'surface_zone_plan.json').read_text())
xy=old['areas'][0]['xy']
factory=union_all([Polygon(list(zip(xy[::2],xy[1::2]))),box(-215,-280,-151,445),box(-151,269,180,445)]).difference(box(174,-600,250,640))
data=json.loads((sources/'CentrelineRoads/road_union_result.json' if local else root/'road_union_result.json').read_text())
xy=next(a['xy'] for a in data['layers'] if a['channel']==0)
road=union_all([Polygon([(xy[i],xy[i+1]),(xy[i+2],xy[i+3]),(xy[i+4],xy[i+5])]) for i in range(0,len(xy),6)])
ob=json.loads((work/'obstacles.json').read_text())
obstacles=union_all([Polygon(list(zip(a['xy'][::2],a['xy'][1::2]))) for a in ob['areas']])
def parts(g):
    if g.is_empty:return []
    if g.geom_type=='Polygon':return [g]
    return [p for a in getattr(g,'geoms',[]) for p in parts(a)]
plant_clear=factory.difference(road.buffer(.12))
green_space=road.buffer(11,quad_segs=6).difference(road.buffer(3.4,quad_segs=6)).intersection(factory).difference(obstacles)
green_space=green_space.buffer(-.8,quad_segs=4).buffer(.8,quad_segs=4).intersection(green_space)
grass=union_all([p.simplify(.12,preserve_topology=True) for p in parts(green_space) if p.area>50 and p.length>35]).difference(obstacles).intersection(plant_clear)
walk=road.buffer(2.8,quad_segs=6).difference(road.buffer(.25,quad_segs=6)).intersection(factory).difference(obstacles)
left=box(-900,-300,-151,1040)
extra_walk=road.buffer(5.2,quad_segs=6).difference(road.buffer(.25,quad_segs=6)).intersection(left).intersection(factory).difference(obstacles)
walk=union_all([walk,extra_walk])
grass=grass.difference(walk)
curb=grass.buffer(.22,quad_segs=3).difference(grass).intersection(plant_clear).difference(obstacles)
walk=union_all([walk,curb]).difference(grass).intersection(plant_clear)
remaining=plant_clear.difference(union_all([grass,walk]))
# A parcel is a connected block bounded by roads, not an individual prefab footprint.
parcels=parts(factory.difference(road.buffer(2.9,quad_segs=6)))
zones=[[],[],[],[],[grass],[walk]]
covered=Polygon();parcel_count=0
for p in sorted(parcels,key=lambda a:a.area,reverse=True):
    if p.area<1500:continue
    q=p.intersection(remaining).difference(covered)
    if q.is_empty:continue
    c=p.representative_point()
    typ=1 if c.x<-480 else 2 if c.y>630 else 0
    zones[typ].append(q);covered=union_all([covered,q]);parcel_count+=1
zones[0].append(remaining.difference(covered))
# A narrow continuous service band follows roads; no checkerboard of building pads.
service=road.buffer(3.4,quad_segs=6).difference(road.buffer(2.8,quad_segs=6)).intersection(remaining).difference(obstacles)
clean=[union_all(x) for x in zones]
for i in range(3):clean[i]=clean[i].difference(service)
clean[3]=service
layers=[];report='# Continuous road-based ground design\n\n'
report+=f'Left sidewalks extend up to 5.2m from road edges; added {extra_walk.area:.1f} square metres of left-side sidewalk coverage including existing strips.\n\n'
report+=f'Factory parcels from existing curved roads: {parcel_count}; building-based material rectangles:0.\n\n'
for i,g in enumerate(clean):
    assert g.is_valid
    coords=[];total=0
    for p in parts(g):
        for tri in constrained_delaunay_triangles(p).geoms:
            total+=tri.area
            for x,z in list(orient(tri,sign=1).exterior.coords)[:3]:coords.extend([x,z])
    assert abs(total-g.area)<.1
    assert g.intersection(road).area<.001
    layers.append(dict(channel=i,xy=coords));report+=f'- Surface{i+1}: {g.area:.1f}m²\n'
for i,a in enumerate(clean):
    for b in clean[i+1:]:assert a.intersection(b).area<.01
assert grass.intersection(obstacles).area<.01
assert union_all(clean).symmetric_difference(plant_clear).area<.1
assert grass.intersection(box(-151,-269,151,269)).area<.001
report+='\nChecks passed: road exclusion, building clearance for green strips, empty combat core, no material-zone overlap, full factory coverage, exact triangulation area.\n'
(work/'ground_design.json').write_text(json.dumps(dict(layers=layers,report=report)))
print(report)

