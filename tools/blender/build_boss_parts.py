"""
보스 전용 부품: 가슴 핵(Boss_Core), 팔뚝 포(Boss_ArmCannon). 보스 색으로만 굽는다.
실행: blender -b -P tools/blender/build_boss_parts.py -- <출력 폴더(Assets/_Anseong/Art/Weapons)> [이름 ...]
좌표: 핵은 앞 = -Y, 위 = +Z(Unity에서 앞 +Z·위 +Y가 된다). 팔뚝 포는 +Z = 손 쪽, 팔뚝 가운데가 원점, 어느 쪽으로 돌려 붙여도 되게 좌우 대칭.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import mecha_kit as k
from mecha_kit import prism, box, cyl, tube, _finish


def core():
    # 뒤판 + 바깥 고리(앞을 봄: 축 = Y)
    cyl("back", (0, 0.10, 0), (0, 0.42, 0), 1.0, "Dark", bevel=0.03, seg=8)
    tube("ring", (0, 0.12, 0), (0, -0.16, 0), 1.0, 0.20, "Metal", bevel=0.025, seg=32)
    tube("ring_in", (0, 0.10, 0), (0, -0.07, 0), 0.79, 0.05, "Trim", bevel=0.01, seg=32)
    tube("ring_glow", (0, 0.02, 0), (0, -0.20, 0), 0.905, 0.035, "Glow", bevel=0.0, seg=32)
    # 고리 물림쇠 8개
    for i in range(8):
        a = math.pi * 2 * i / 8 + math.pi / 8
        x, z = math.cos(a), math.sin(a)
        box("lug", (x * 1.0, -0.03, z * 1.0), (0.34, 0.36, 0.26), "ArmorMain" if i % 2 else "ArmorAccent", bevel=0.03, rot=(0, -math.degrees(a), 0))
        cyl("lug_bolt", (x * 1.03, -0.20, z * 1.03), (x * 1.03, -0.235, z * 1.03), 0.055, "Trim", bevel=0.006, seg=6)
    # 핵(끓는 구)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=0.62)
    ob = _finish(bm, "orb", "Core", 0)
    ob.location = (0, 0.02, 0)
    for p in ob.data.polygons:
        p.use_smooth = True
    # 핵을 붙든 발톱 4개(안쪽으로 굽은 판)
    for i in range(4):
        a = math.pi / 2 * i + math.pi / 4
        claw = prism("claw", [(0.86, -0.085), (0.86, 0.085), (0.50, 0.055), (0.30, 0.0), (0.50, -0.055)], -0.44, -0.30, "Dark", bevel=0.015, plane='XZ', taper=0.7)
        claw.rotation_euler = (0, -a, 0)
        tip = prism("claw_tip", [(0.62, -0.03), (0.62, 0.03), (0.36, 0.0)], -0.455, -0.43, "ArmorAccent", bevel=0.004, plane='XZ')
        tip.rotation_euler = (0, -a, 0)
        strut = box("claw_root", (0.84 * math.cos(a), -0.26, 0.84 * math.sin(a)), (0.16, 0.30, 0.2), "Metal", bevel=0.02, rot=(0, -math.degrees(a), 0))


def arm_cannon():
    # 팔뚝을 감싼 고리 둘 + 양옆 포신 둘(±X) + 위아래 탄띠 상자(±Y)
    R = 0.36
    for z in (-0.30, 0.22):
        tube("band", (0, 0, z), (0, 0, z + 0.16), R + 0.07, 0.07, "Metal", bevel=0.012, seg=8)
    tube("sleeve", (0, 0, -0.22), (0, 0, 0.30), R + 0.03, 0.04, "ArmorMain", bevel=0.012, seg=8)
    for sx in (1, -1):
        A = sx * (R + 0.17)
        box("breech", (A, 0, -0.12), (0.26, 0.30, 0.62), "ArmorMain", bevel=0.03)
        box("breech_accent", (A + sx * 0.132, 0, -0.10), (0.02, 0.16, 0.34), "ArmorAccent", bevel=0.005)
        tube("barrel", (A, 0, 0.10), (A, 0, 1.05), 0.095, 0.035, "Metal", bevel=0.006, seg=16)
        tube("jacket", (A, 0, 0.18), (A, 0, 0.55), 0.13, 0.035, "Dark", bevel=0.01, seg=16)
        tube("muzzle", (A, 0, 0.92), (A, 0, 1.12), 0.135, 0.05, "Dark", bevel=0.01, seg=16)
        tube("glow", (A, 0, 0.86), (A, 0, 0.895), 0.11, 0.02, "Glow", bevel=0.0, seg=16)
        cyl("back_cap", (A, 0, -0.52), (A, 0, -0.43), 0.10, "Trim", bevel=0.01, seg=12)
        for sy in (1, -1):
            for dx in (-0.05, 0.05):
                box("vent", (A + dx, sy * 0.155, -0.12), (0.025, 0.015, 0.3), "Glow", bevel=0.0)
    for sy in (1, -1):
        box("ammo", (0, sy * (R + 0.12), -0.05), (0.34, 0.16, 0.5), "Dark", bevel=0.025)
        box("ammo_lid", (0, sy * (R + 0.205), -0.05), (0.24, 0.02, 0.36), "ArmorAccent", bevel=0.006)
        for x in (-0.12, 0.12):
            cyl("ammo_bolt", (x, sy * (R + 0.19), 0.22), (x, sy * (R + 0.225), 0.22), 0.03, "Trim", bevel=0.004, seg=6)


BUILD = {"Boss_Core": core, "Boss_ArmCannon": arm_cannon}

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    out = args[0]
    for name in (args[1:] or list(BUILD)):
        k.reset()
        BUILD[name]()
        ob = k.join_all(name)
        k.bake_and_export(ob, f"{out}/{name}", name, palettes=(("", k.BOSS),))
