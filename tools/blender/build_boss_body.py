"""
보스 전용 몸: 로봇 모델(HandFix)에서 머리 뒤 고리를 떼고 뿔·등 날개·어깨 가시·무릎 가시를 붙인 FBX.
뼈 이름·계층·자세는 원본 그대로(추가 조각은 뼈 하나에 통째로 붙는다) → 보스 클립·Cascadeur 리그에 영향 없음.
실행: blender -b -P tools/blender/build_boss_body.py -- <로봇 FBX> <출력 FBX> <텍스처 폴더>
  예: ... -- Assets/_Anseong/Art/Robot/Source/BossUpdatedCompatible_HandFix.fbx Assets/_Anseong/Art/Robot/Source/Boss_Body.fbx Assets/_Anseong/Art/Boss/Extras
좌표(만드는 동안): 로봇 키 = 10, 앞 = -Y, 위 = +Z, 로봇 왼쪽 = +X. 끝에 0.1배.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import mecha_kit as k
from mecha_kit import prism, box, cyl

NAME = "BossExtras"
REMOVE = ("_p0_i1", "RED_Ring")   # 머리 뒤 고리


def blade(name, plane, root, ang, L, w, t, mat="ArmorMain", accent=True, glow=True):
    """칼날 모양 판. plane 'XZ'(앞을 보는 판) 또는 'YZ'(옆을 보는 판), root = 뿌리(x, y, z), ang = 판 안에서 뻗는 방향(도, 0 = +축, 90 = 위)."""
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    def place(pts):
        out = []
        for u, v in pts:
            a, b = u * c - v * s, u * s + v * c
            out.append((root[0] + a, root[2] + b) if plane == 'XZ' else (root[1] + a, root[2] + b))
        return out
    mid = root[1] if plane == 'XZ' else root[0]
    obs = [prism(name, place([(0, -w / 2), (L * 0.15, -w * 0.62), (L * 0.7, -w * 0.36), (L, 0), (L * 0.62, w * 0.3), (0, w / 2)]),
                 mid - t / 2, mid + t / 2, mat, bevel=min(0.03, t * 0.3), plane=plane)]
    if accent:
        obs.append(prism(name + "_a", place([(L * 0.08, -w * 0.30), (L * 0.2, -w * 0.40), (L * 0.55, -w * 0.25), (L * 0.5, w * 0.05), (L * 0.08, w * 0.2)]),
                         mid - t * 0.62, mid + t * 0.62, "ArmorAccent", bevel=0.01, plane=plane))
    if glow:
        obs.append(prism(name + "_g", place([(L * 0.12, w * 0.24), (L * 0.6, w * 0.20), (L * 0.6, w * 0.26), (L * 0.12, w * 0.32)]),
                         mid - t * 0.58, mid + t * 0.58, "Glow", bevel=0.0, plane=plane))
    return obs


def head():
    obs = []
    for sx in (1, -1):
        # 뒤로 누운 뿔(옆을 보는 판을 바깥으로 벌림)
        horn = blade("horn", 'YZ', (0, 0.15, 9.25), 52, 1.9, 0.5, 0.12)
        for o in horn:
            o.rotation_euler = (0, math.radians(sx * 24), 0)
        # 회전 중심이 원점이라 뿌리가 옮겨진다 → 뿌리를 다시 제자리로
        import mathutils
        rot = mathutils.Matrix.Rotation(math.radians(sx * 24), 4, 'Y')
        base = mathutils.Vector((0, 0.15, 9.25))
        shift = mathutils.Vector((sx * 0.42, 0.15, 9.25)) - rot @ base
        for o in horn:
            o.location = shift
        obs += horn
        obs.append(box("horn_root", (sx * 0.45, 0.2, 9.2), (0.16, 0.4, 0.3), "Dark", bevel=0.04))
    obs += blade("crest", 'YZ', (0, -0.2, 9.5), 38, 1.5, 0.42, 0.09, mat="ArmorAccent", accent=False)
    return obs


def back():
    obs = [box("pack", (0, 1.38, 8.05), (1.3, 0.5, 1.5), "Dark", bevel=0.06),
           box("pack_plate", (0, 1.62, 8.15), (0.9, 0.08, 1.0), "ArmorMain", bevel=0.03)]
    for x in (-0.25, 0, 0.25):
        obs.append(box("pack_vent", (x, 1.67, 8.15), (0.1, 0.03, 0.7), "Glow", bevel=0.0))
    for sx in (1, -1):
        up = 62 if sx > 0 else 118
        mid = 22 if sx > 0 else 158
        low = -28 if sx > 0 else 208
        obs += blade("wing_up", 'XZ', (sx * 0.5, 1.45, 8.5), up, 3.3, 0.62, 0.12)
        obs += blade("wing_mid", 'XZ', (sx * 0.6, 1.52, 8.15), mid, 2.7, 0.55, 0.11)
        obs.append(cyl("wing_hinge", (sx * 0.55, 1.25, 8.15), (sx * 0.55, 1.62, 8.15), 0.3, "Metal", bevel=0.03, seg=10))
    return obs


def shoulder(sx):
    obs = []
    out = 58 if sx > 0 else 122
    obs += blade("spike_a", 'XZ', (sx * 1.75, 0.25, 8.75), out, 1.7, 0.5, 0.14)
    obs.append(box("spike_base", (sx * 1.8, 0.3, 8.72), (0.6, 0.5, 0.2), "Dark", bevel=0.05))
    return obs


def knee(sx):
    obs = blade("knee", 'YZ', (sx * 1.05, -0.55, 3.95), 128, 1.15, 0.42, 0.16, glow=False)
    obs.append(box("knee_base", (sx * 1.05, -0.6, 3.9), (0.5, 0.25, 0.6), "Metal", bevel=0.05))
    return obs


GROUPS = [("mixamorig:Head", head), ("mixamorig:Spine", back),
          ("mixamorig:LeftArm", lambda: shoulder(1)), ("mixamorig:RightArm", lambda: shoulder(-1)),
          ("mixamorig:LeftLeg", lambda: knee(1)), ("mixamorig:RightLeg", lambda: knee(-1))]

if __name__ == "__main__":
    src, dst, texdir = sys.argv[sys.argv.index("--") + 1:][:3]
    k.reset()
    parts = []
    for bone, fn in GROUPS:
        ob = k.join_all(bone, only=fn(), uv=False)
        g = ob.vertex_groups.new(name=bone)
        g.add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')
        parts.append(ob)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    extras = parts[0]
    extras.name = NAME
    k.unwrap(extras)
    k.bake_textures(extras, texdir, NAME, palettes=(("", k.BOSS),))
    import mathutils
    extras.data.transform(mathutils.Matrix.Scale(0.1, 4))
    print("EXTRAS tris", len(extras.data.polygons))

    bpy.ops.import_scene.fbx(filepath=src)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    removed = 0
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and o is not extras and any(t in o.name for t in REMOVE):
            bpy.data.objects.remove(o)
            removed += 1
    extras.parent = arm
    extras.modifiers.new("Armature", 'ARMATURE').object = arm
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                             bake_anim=False, path_mode='AUTO')
    print("EXPORTED", dst, "removed", removed)
