"""
손으로 짠(절차적) 메카 무기·부품을 만드는 Blender 공용 도구. `build_weapons.py` 등이 불러 쓴다.

좌표(만드는 동안): 손 뼈 기준, 단위 = 팔뚝 길이 1.
  Z = 손가락 쪽(Unity 손 뼈 +Y), X = 손 뼈 X(오른손은 -X가 손등, 왼손은 +X가 손등), +Y = 엄지 쪽(Unity 손 뼈 -Z).
내보낼 때 Y-up FBX로 쓰면 Unity 손 뼈 좌표와 같아진다(AiModelFit의 Authored 칸이 회전 없이 붙인다).

결과: <이름>.fbx + _BaseColor(AO·모서리 닳음 포함) + _MetallicSmoothness(R 금속, A 매끈함) + _Emission,
보스 색 한 벌은 _Boss_BaseColor / _Boss_Emission (같은 UV).
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

# 재질 슬롯(04_art_spec 공용 슬롯 이름). (색, 금속, 매끈함, 발광)
PLAYER = {
    "ArmorMain":   ((0.40, 0.415, 0.43), 0.55, 0.42, None),
    "ArmorAccent": ((0.50, 0.05, 0.04), 0.35, 0.50, None),
    "Metal":       ((0.30, 0.31, 0.33), 1.00, 0.55, None),
    "Dark":        ((0.045, 0.048, 0.055), 0.80, 0.35, None),
    "Glow":        ((0.10, 0.45, 0.60), 0.00, 0.80, (0.25, 0.85, 1.0)),
    "Trim":        ((0.72, 0.74, 0.78), 1.00, 0.72, None),
    "Blade":       ((0.20, 0.21, 0.235), 1.00, 0.74, None),
    "Core":        ((0.05, 0.25, 0.35), 0.00, 0.85, (0.2, 0.8, 1.0)),
}
BOSS = {
    "ArmorMain":   ((0.075, 0.045, 0.045), 0.60, 0.45, None),
    "ArmorAccent": ((0.42, 0.03, 0.02), 0.40, 0.50, None),
    "Metal":       ((0.16, 0.14, 0.14), 1.00, 0.55, None),
    "Dark":        ((0.02, 0.02, 0.022), 0.80, 0.35, None),
    "Glow":        ((0.60, 0.15, 0.02), 0.00, 0.80, (1.0, 0.32, 0.04)),
    "Trim":        ((0.45, 0.33, 0.12), 1.00, 0.65, None),
    "Blade":       ((0.14, 0.12, 0.13), 1.00, 0.80, None),
    "Core":        ((0.30, 0.03, 0.01), 0.00, 0.85, (1.0, 0.16, 0.03)),
}
SLOTS = list(PLAYER.keys())


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name in SLOTS:
        bpy.data.materials.new(name)


def _finish(bm, name, mat, bevel, segments=2):
    mesh = bpy.data.meshes.new(name)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    mesh.materials.append(bpy.data.materials[mat])
    if bevel > 0:
        m = ob.modifiers.new("b", 'BEVEL')
        m.width = bevel
        m.segments = segments
        m.limit_method = 'ANGLE'
        m.angle_limit = math.radians(28)
        m.harden_normals = True
    return ob


def prism(name, pts, lo, hi, mat, bevel=0.012, plane='YZ', taper=1.0, taper_center=None):
    """2D 윤곽(pts)을 plane에 그리고 나머지 축으로 lo~hi 밀어낸 판. plane 'YZ' → 두께 X, 'XZ' → 두께 Y, 'XY' → 두께 Z.
    taper < 1이면 hi 쪽 윤곽을 taper_center 기준으로 줄인다(경사진 판)."""
    def put(u, v, w):
        if plane == 'YZ': return Vector((w, u, v))
        if plane == 'XZ': return Vector((u, w, v))
        return Vector((u, v, w))
    if taper_center is None:
        taper_center = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
    bm = bmesh.new()
    a = [bm.verts.new(put(u, v, lo)) for u, v in pts]
    b = [bm.verts.new(put(taper_center[0] + (u - taper_center[0]) * taper, taper_center[1] + (v - taper_center[1]) * taper, hi)) for u, v in pts]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(pts)
    for i in range(n):
        bm.faces.new([a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]])
    return _finish(bm, name, mat, bevel)


def box(name, center, size, mat, bevel=0.012, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    ob = _finish(bm, name, mat, bevel)
    ob.scale = size
    ob.location = center
    ob.rotation_euler = [math.radians(r) for r in rot]
    return ob


def cyl(name, p0, p1, r, mat, bevel=0.008, seg=20, r2=None):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=d.length)
    ob = _finish(bm, name, mat, bevel)
    ob.location = (p0 + p1) / 2
    ob.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return ob


def tube(name, p0, p1, r, wall, mat, bevel=0.006, seg=24):
    """속이 빈 관(포신)."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    L = d.length
    bm = bmesh.new()
    rings = []
    for rad, z in ((r, -L / 2), (r, L / 2), (r - wall, L / 2), (r - wall, -L / 2 + wall)):
        rings.append([bm.verts.new((rad * math.cos(2 * math.pi * i / seg), rad * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    for k in range(3):
        for i in range(seg):
            bm.faces.new([rings[k][i], rings[k][(i + 1) % seg], rings[k + 1][(i + 1) % seg], rings[k + 1][i]])
    bm.faces.new(rings[3])
    bm.faces.new(list(reversed(rings[0])))
    ob = _finish(bm, name, mat, bevel)
    ob.location = (p0 + p1) / 2
    ob.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return ob


def mirror_y(ob):
    """엄지 쪽 ↔ 새끼 쪽 대칭 복사."""
    c = ob.copy()
    c.data = ob.data.copy()
    bpy.context.scene.collection.objects.link(c)
    c.matrix_world = Matrix.Scale(-1, 4, (0, 1, 0)) @ ob.matrix_world
    return c


def join_all(name, only=None, uv=True):
    """수정자 적용 → 하나로 합침 → 뒤집힌 면 고침 → UV. only = 합칠 오브젝트(비우면 씬의 메시 전부)."""
    obs = list(only) if only is not None else [o for o in bpy.context.scene.objects if o.type == 'MESH']
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    mats = [bpy.data.materials[s] for s in SLOTS]
    for o in obs:
        ev = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        me.transform(o.matrix_world)
        if o.matrix_world.determinant() < 0:
            me.flip_normals()
        idx = SLOTS.index(o.data.materials[0].name)
        start = len(bm.faces)
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        for i in range(start, len(bm.faces)):
            bm.faces[i].material_index = idx
        bpy.data.meshes.remove(me)
    for o in obs:
        bpy.data.objects.remove(o)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for m in mats:
        mesh.materials.append(m)
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    for p in mesh.polygons:
        p.use_smooth = True
    mesh.set_sharp_from_angle(angle=math.radians(32))
    if uv:
        unwrap(ob)
    return ob


def unwrap(ob):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    return ob


def _bake_material(palette, mode):
    """mode: 'color' | 'ms' | 'emit'. 슬롯마다 Emission으로 값을 내보내는 재질을 만든다."""
    for s in SLOTS:
        color, metal, smooth, glow = palette[s]
        m = bpy.data.materials[s]
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(em.outputs[0], out.inputs[0])
        # 모서리 마스크: 둥근 법선과 실제 법선이 다른 곳 = 모서리.
        bev = nt.nodes.new("ShaderNodeBevel"); bev.samples = 6; bev.inputs["Radius"].default_value = 0.012
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = 'DOT_PRODUCT'
        nt.links.new(bev.outputs[0], dot.inputs[0]); nt.links.new(geo.outputs["True Normal"], dot.inputs[1])
        edge = nt.nodes.new("ShaderNodeMapRange")
        edge.inputs["From Min"].default_value = 0.995; edge.inputs["From Max"].default_value = 0.90
        nt.links.new(dot.outputs["Value"], edge.inputs["Value"])
        noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 9.0; noise.inputs["Detail"].default_value = 8.0
        noise.inputs["Roughness"].default_value = 0.7
        coord = nt.nodes.new("ShaderNodeTexCoord")
        nt.links.new(coord.outputs["Object"], noise.inputs["Vector"])
        chip = nt.nodes.new("ShaderNodeMath"); chip.operation = 'MULTIPLY'   # 모서리 × 얼룩 = 군데군데 벗겨짐
        ramp = nt.nodes.new("ShaderNodeMapRange"); ramp.inputs["From Min"].default_value = 0.35; ramp.inputs["From Max"].default_value = 0.65
        nt.links.new(noise.outputs["Fac"], ramp.inputs["Value"])
        nt.links.new(edge.outputs[0], chip.inputs[0]); nt.links.new(ramp.outputs[0], chip.inputs[1])
        painted = s in ("ArmorMain", "ArmorAccent")
        if mode == 'color':
            base = nt.nodes.new("ShaderNodeMixRGB"); base.blend_type = 'MULTIPLY'; base.inputs["Fac"].default_value = (0.15 if s == "ArmorAccent" else 0.35) if painted else 0.2
            base.inputs["Color1"].default_value = (*color, 1)
            big = nt.nodes.new("ShaderNodeTexNoise"); big.inputs["Scale"].default_value = 2.2; big.inputs["Detail"].default_value = 5.0
            nt.links.new(coord.outputs["Object"], big.inputs["Vector"])
            nt.links.new(big.outputs["Fac"], base.inputs["Color2"])
            mix = nt.nodes.new("ShaderNodeMixRGB")
            worn = (tuple(c * 0.5 + 0.22 for c in color) + (1,)) if painted else tuple(min(1, c * 1.8 + 0.05) for c in color) + (1,)
            mix.inputs["Color2"].default_value = worn
            nt.links.new(base.outputs[0], mix.inputs["Color1"])
            if s == "ArmorAccent":
                half = nt.nodes.new("ShaderNodeMath"); half.operation = 'MULTIPLY'; half.inputs[1].default_value = 0.45
                nt.links.new(chip.outputs[0], half.inputs[0]); nt.links.new(half.outputs[0], mix.inputs["Fac"])
            else:
                nt.links.new(chip.outputs[0] if painted else edge.outputs[0], mix.inputs["Fac"])
            nt.links.new(mix.outputs[0], em.inputs["Color"])
        elif mode == 'ms':
            # R = 금속, G = 매끈함(벗겨진 곳은 금속 1, 얼룩으로 매끈함이 조금 흔들림)
            comb = nt.nodes.new("ShaderNodeCombineColor")
            mr = nt.nodes.new("ShaderNodeMapRange"); mr.inputs["To Min"].default_value = metal; mr.inputs["To Max"].default_value = 1.0 if painted else metal
            nt.links.new(chip.outputs[0], mr.inputs["Value"])
            sm = nt.nodes.new("ShaderNodeMapRange"); sm.inputs["To Min"].default_value = smooth - 0.12; sm.inputs["To Max"].default_value = smooth + 0.08
            nt.links.new(noise.outputs["Fac"], sm.inputs["Value"])
            nt.links.new(mr.outputs[0], comb.inputs[0]); nt.links.new(sm.outputs[0], comb.inputs[1])
            nt.links.new(comb.outputs[0], em.inputs["Color"])
        elif s == "Core":
            # 핵: 평평한 단색이면 스티커처럼 보인다 → 안쪽에서 끓는 무늬(밝은 줄기 + 어두운 틈).
            vor = nt.nodes.new("ShaderNodeTexNoise"); vor.inputs["Scale"].default_value = 3.2; vor.inputs["Detail"].default_value = 6.0
            vor.inputs["Roughness"].default_value = 0.75; vor.inputs["Distortion"].default_value = 1.4
            nt.links.new(coord.outputs["Object"], vor.inputs["Vector"])
            cr = nt.nodes.new("ShaderNodeValToRGB")
            e = cr.color_ramp.elements
            e[0].position = 0.38; e[0].color = (glow[0] * 0.10, glow[1] * 0.04, glow[2] * 0.04, 1)
            e[1].position = 0.62; e[1].color = (min(1, glow[0] * 1.0 + 0.0), min(1, glow[1] * 2.6 + 0.25), min(1, glow[2] * 2.0 + 0.1), 1)
            mid = cr.color_ramp.elements.new(0.50); mid.color = (*glow, 1)
            nt.links.new(vor.outputs["Fac"], cr.inputs["Fac"])
            nt.links.new(cr.outputs["Color"], em.inputs["Color"])
        else:
            em.inputs["Color"].default_value = (*(glow or (0, 0, 0)), 1)


def _bake(ob, name, res, kind='EMIT', samples=8):
    img = bpy.data.images.new(name, res, res, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'sRGB' if kind == 'EMIT' and name.endswith(("color", "emit")) else 'Non-Color'
    for m in ob.data.materials:
        nt = m.node_tree
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = img
        nt.nodes.active = n
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.render.bake.margin = 6
    bpy.ops.object.bake(type=kind)
    return img


def _pixels(img):
    import numpy as np
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def _save(arr, path, srgb):
    import numpy as np
    h, w, _ = arr.shape
    img = bpy.data.images.new("out", w, h, alpha=True)
    img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    img.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def bake_and_export(ob, folder, name, res=2048, shift_z=0.0, palettes=None):
    """텍스처 굽기(우리 색·보스 색) + FBX. shift_z = 손목에서 붙이는 점까지(PlayerWeaponVisuals의 along)만큼 원점을 옮긴다."""
    bake_textures(ob, folder, name, res, palettes)
    ob.data.transform(Matrix.Translation((0, 0, -shift_z)))
    bpy.ops.export_scene.fbx(filepath=f"{folder}/{name}.fbx", use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE',
                             use_mesh_modifiers=False, add_leaf_bones=False, apply_scale_options='FBX_SCALE_ALL')
    print("EXPORTED", name, "tris", len(ob.data.polygons), "dims", [round(x, 3) for x in ob.dimensions])


def bake_textures(ob, folder, name, res=2048, palettes=None):
    """텍스처만 굽고 재질을 하나(이름 = name)로 줄인다."""
    import numpy as np
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    # AO (한 번, 색과 무관)
    for s in SLOTS:
        m = bpy.data.materials[s]; m.use_nodes = True; m.node_tree.nodes.clear()
        o = m.node_tree.nodes.new("ShaderNodeOutputMaterial"); d = m.node_tree.nodes.new("ShaderNodeBsdfDiffuse")
        m.node_tree.links.new(d.outputs[0], o.inputs[0])
    ao = _pixels(_bake(ob, "ao", res, 'AO', samples=48))[..., :3].mean(axis=2, keepdims=True)
    ao = 0.25 + 0.75 * np.clip(ao, 0, 1) ** 1.3
    for tag, palette in (palettes or (("", PLAYER), ("_Boss", BOSS))):
        _bake_material(palette, 'color')
        col = _pixels(_bake(ob, "color", res))
        col[..., :3] *= ao
        col[..., 3] = 1
        _save(col, f"{folder}/{name}{tag}_BaseColor.png", True)
        _bake_material(palette, 'emit')
        em = _pixels(_bake(ob, "emit", res)); em[..., 3] = 1
        _save(em, f"{folder}/{name}{tag}_Emission.png", True)
        if tag == "":
            _bake_material(palette, 'ms')
            ms = _pixels(_bake(ob, "ms", res))
            out = np.zeros_like(ms)
            out[..., 0] = ms[..., 0]; out[..., 1] = ms[..., 0]; out[..., 2] = ms[..., 0]; out[..., 3] = ms[..., 1]
            _save(out, f"{folder}/{name}_MetallicSmoothness.png", False)
    # 재질 하나로(Unity 쪽은 텍스처 한 벌)
    one = bpy.data.materials.new(name)
    ob.data.materials.clear()
    ob.data.materials.append(one)
    for p in ob.data.polygons:
        p.material_index = 0
