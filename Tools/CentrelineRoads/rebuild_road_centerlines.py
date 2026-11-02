import sys,json,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'road_geometry_lib'))
from shapely import LineString,Point,box,union_all,constrained_delaunay_triangles
from shapely.geometry.polygon import orient
root=Path(__file__).parent
data=json.loads((root/'road_union_input.json').read_text(encoding='utf-8-sig'))
routes=[]
def fillet(points,distance=22):
    out=[points[0]]
    for i in range(1,len(points)-1):
        a,b,c=points[i-1:i+2]
        la=math.dist(a,b);lc=math.dist(b,c);d=min(distance,la*.25,lc*.25)
        p=(b[0]+(a[0]-b[0])*d/la,b[1]+(a[1]-b[1])*d/la)
        q=(b[0]+(c[0]-b[0])*d/lc,b[1]+(c[1]-b[1])*d/lc)
        out.append(p)
        for j in range(1,13):
            t=j/12;u=1-t
            out.append((u*u*p[0]+2*u*t*b[0]+t*t*q[0],u*u*p[1]+2*u*t*b[1]+t*t*q[1]))
    out.append(points[-1]);return out
def add(name,points,width=14,curve=True):
    line=LineString(fillet(points) if curve and len(points)>2 else points)
    routes.append((name,line,width))
def catmull(points):
    out=[]
    for i in range(len(points)-1):
        a=points[max(0,i-1)];b=points[i];c=points[i+1];d=points[min(len(points)-1,i+2)]
        for j in range(32):
            t=j/32;t2=t*t;t3=t2*t
            out.append(tuple(.5*(2*b[k]+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-d[k])*t2+(-a[k]+3*b[k]-3*c[k]+d[k])*t3) for k in range(2)))
    return out+[points[-1]]
# Continuous road centre-lines, not reconstructed from old jagged asphalt outlines.
spine=catmull([(-470,-270),(-490,0),(-470,300),(-330,530),(-230,780),(-260,1080)])
spine=LineString(spine).intersection(box(-1000,-400,250,1030))
routes.append(('Factory curved spine',spine,14))
for z in (610,970):add('Cross-site road', [(-810,z),(-540,z),(-300,z+60),(145,z+60)])
add('West rear service', [(-760,510),(-760,970)])
add('East rear service',[(125,670),(125,1030)],7)
add('West lower service',[(-570,-250),(-570,350)],7)
for z in (-200,100,360):add('Lower branch',[(-570,z),(-215,z+35)],7)
add('Yard north-west connector',[(-160,280),(-215,395),(-330,530)])
add('West yard avenue',[(-160,-280),(-160,280)])
add('East quay avenue',[(160,-280),(160,520)])
add('South yard road',[(-160,-280),(160,-280)])
add('North yard road',[(-160,280),(160,280)])
add('Dispatch loading access',[(-160,160),(-85,160),(-85,181)],7)
add('Workshop loading access',[(160,-55),(90,-55),(90,-28)],7)
add('Maintenance access',[(-160,-65),(-100,-65),(-100,-34)],7)
# Existing foreground warehouse aprons: short, deliberate driveways only.
add('South-west warehouse access',[(-160,-240),(-210,-240),(-210,-290)],7)

asphalt=union_all([line.buffer(w/2,cap_style='flat',join_style='round',quad_segs=12) for _,line,w in routes],grid_size=.001)
# Small tangent return inside intersections; unlike old closing this cannot inherit old protrusions.
asphalt=asphalt.buffer(2.5,quad_segs=8).buffer(-2.5,quad_segs=8).simplify(.015)
def point_parts(g):
    if g.is_empty:return []
    if g.geom_type=='Point':return [g]
    if hasattr(g,'geoms'):return [p for a in g.geoms for p in point_parts(a)]
    return []
junctions=[]
for i,(_,a,wa) in enumerate(routes):
    for _,b,wb in routes[i+1:]:
        junctions+=point_parts(a.intersection(b))
mask=union_all([p.buffer(12,quad_segs=12) for p in junctions])
white=[];yellow=[]
def dashed(line,dash=6,gap=8):
    from shapely.ops import substring
    return [substring(line,s,min(s+dash,line.length)).buffer(.055,cap_style='flat') for s in range(0,math.ceil(line.length),dash+gap) if min(s+dash,line.length)>s]
for name,line,w in routes:
    yellow.append(line.offset_curve(.18).buffer(.055,cap_style='flat'))
    yellow.append(line.offset_curve(-.18).buffer(.055,cap_style='flat'))
    if w<10:continue
    for side in (-1,1):
        offset=line.offset_curve(side*3.5)
        if offset.geom_type=='LineString':white+=dashed(offset)
inside=asphalt.buffer(-.6)
white=union_all([asphalt.buffer(-.28).difference(asphalt.buffer(-.39)),union_all(white).difference(mask).intersection(inside)])
yellow=union_all(yellow).difference(mask).intersection(inside)
obstacles=union_all([box(*b['xy']) for b in data['buildings']])
overlap=asphalt.intersection(obstacles)
print('Road-building overlap m2:',overlap.area)
print('Overlap bounds:',overlap.bounds)
width_checks=0
for name,line,w in routes:
    for distance in range(20,int(line.length)-20,15):
        q=line.interpolate(distance)
        if any(other is not line and other.distance(q)<22 for _,other,_ in routes):continue
        a=line.interpolate(distance-.2);b=line.interpolate(distance+.2)
        dx=b.x-a.x;dy=b.y-a.y;length=math.hypot(dx,dy)
        normal=(-dy/length,dx/length)
        section=LineString([(q.x-normal[0]*20,q.y-normal[1]*20),(q.x+normal[0]*20,q.y+normal[1]*20)])
        measured=section.intersection(asphalt).length
        assert abs(measured-w)<.45,(name,distance,w,measured)
        width_checks+=1
print('Uniform width samples passed:',width_checks)
# Do not conceal road/building conflicts with clipped road widths.
if overlap.area>1:
    for name,line,w in routes:
        hit=line.buffer(w/2).intersection(obstacles)
        if hit.area>.1:print('CONFLICT',name,hit.area,hit.bounds)

def polygons(g):
    if g.is_empty:return []
    if g.geom_type=='Polygon':return [g]
    return [p for a in getattr(g,'geoms',[]) for p in polygons(a)]
layers=[]
for channel,g in enumerate((asphalt,white,yellow)):
    coords=[];area=0
    for poly in polygons(g):
        for tri in constrained_delaunay_triangles(poly).geoms:
            tri=orient(tri,sign=1);area+=tri.area
            for x,y in list(tri.exterior.coords)[:3]:coords.extend((round(x,5),round(y,5)))
    assert abs(area-g.area)<.1,(channel,area,g.area)
    layers.append({'channel':channel,'xy':coords})
report=f'''# Centre-line road rebuild

Replaced inherited patchwork asphalt boundaries with {len(routes)} explicit road routes. Main carriageways14m, service access7m. Continuous sampled spline and tangent fillets; clear branch junctions; no inherited widening patches. Centre/lane markings follow uninterrupted route distance and stop12m before junctions. Regenerated outline markings. Source assets retained inactive. Buildings and sea unchanged.

Road polygon valid: {asphalt.is_valid}
Asphalt components: {len(polygons(asphalt))}
Road-building overlap: {overlap.area:.4f}m²
Triangulated area checks: passed
Uniform-width cross-section checks away from junctions: {width_checks} passed (±0.45m tolerance)
32 existing truck instances are realigned to the rebuilt lanes by the Unity import step.
Vehicle driving and VR not tested.
'''
(root/'road_union_result.json').write_text(json.dumps({'layers':layers,'report':report}),encoding='utf-8')
(root/'rebuilt_routes.json').write_text(json.dumps({'routes':[{'name':n,'width':w,'points':[{'x':x,'y':y} for x,y in l.coords]} for n,l,w in routes]}),encoding='utf-8')
print(report)
