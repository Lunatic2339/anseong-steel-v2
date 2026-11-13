"""
우리 무기 4종(검·방패·포·스캐너)을 Blender로 만든다. 보스는 같은 모델에 보스 색 텍스처(_Boss_)를 쓴다.
실행: blender -b -P tools/blender/build_weapons.py -- <출력 폴더(Assets/_Anseong/Art/Weapons)> [이름 ...]
(pip의 bpy로도 됨: python tools/blender/build_weapons.py -- <출력 폴더>)
모양을 바꾸려면 아래 함수의 숫자를 고치고 다시 돌린 뒤 Unity에서 'Anseong → Art → Fit AI Models'.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mecha_kit as k
from mecha_kit import prism, box, cyl, tube, mirror_y

# 붙이는 점(손목에서 손가락 쪽으로, 팔뚝 배수). PlayerWeaponVisuals.Attach의 along과 같아야 한다.
ALONG = {"Weapon_Sword": 1.3, "Weapon_Shield": 0.2, "Weapon_Cannon": 0.5, "Weapon_Scanner": 0.5}


def mount(s, length=(-0.55, 0.55), half=0.27, accent=True):
    """손등 덮개 + 손목 물림쇠. s = 손등 방향(오른손 -1, 왼손 +1)."""
    z0, z1 = length
    hood = [(s * 0.10, z0), (s * 0.34, z0 + 0.10), (s * 0.46, z0 + 0.50), (s * 0.46, z1 - 0.17), (s * 0.40, z1), (s * 0.12, z1)]
    prism("hood", hood, -half, half, "ArmorMain", bevel=0.03, plane='XZ')
    if accent:
        prism("hood_top", [(s * 0.47, z1 - 0.57), (s * 0.50, z1 - 0.50), (s * 0.50, z1 - 0.22), (s * 0.47, z1 - 0.15)], -half * 0.62, half * 0.62, "ArmorAccent", bevel=0.008, plane='XZ')
    # 판 이음매(어두운 홈)
    for y in (-half * 0.72, half * 0.72):
        box("seam", (s * 0.462, y, (z0 + z1) / 2 + 0.2), (0.012, 0.014, (z1 - z0) * 0.55), "Dark", bevel=0)
    box("hood_vent", (s * 0.42, 0, z0 + 0.27), (0.07, half * 1.1, 0.16), "Dark", bevel=0.006, rot=(0, s * 17, 0))
    for y in (-half * 0.35, 0.0, half * 0.35):
        box("vent_fin", (s * 0.44, y, z0 + 0.27), (0.05, 0.02, 0.15), "Metal", bevel=0.003, rot=(0, s * 17, 0))
    side = [(s * 0.36, z0 + 0.05), (-s * 0.10, z0 + 0.13), (-s * 0.16, z0 + 0.37), (-s * 0.10, z0 + 0.61), (s * 0.36, z0 + 0.75)]
    a = prism("clamp", side, half, half + 0.07, "Metal", bevel=0.012, plane='XZ')
    mirror_y(a)
    c = cyl("clamp_pin", (s * 0.02, half + 0.06, z0 + 0.37), (s * 0.02, half + 0.10, z0 + 0.37), 0.07, "Trim", bevel=0.006, seg=8)
    mirror_y(c)
    for z in (z0 + 0.62, z1 - 0.08):
        for y in (-half * 0.8, half * 0.8):
            cyl("rivet", (s * 0.455, y, z), (s * 0.475, y, z), 0.022, "Trim", bevel=0.003, seg=6)


def sword():
    """오른손 손등(-X)에 얹는 대검. 날은 손가락 쪽(+Z)으로 뻗고 날 끝은 새끼 쪽(-Y), 등은 엄지 쪽(+Y)."""
    D = -0.30  # 손등 위 높이(날 가운데)
    blade = [(0.27, 0.50), (0.27, 2.55), (0.11, 3.22), (0.00, 3.45), (-0.25, 2.98), (-0.34, 2.40), (-0.34, 0.85), (-0.18, 0.50)]
    prism("blade", blade, D - 0.05, D + 0.05, "Blade", bevel=0.022)
    edge = [(-0.325, 0.86), (-0.325, 2.40), (-0.24, 2.96), (-0.005, 3.43), (-0.03, 3.52), (-0.30, 3.02), (-0.41, 2.42), (-0.41, 0.90)]
    prism("edge", edge, D - 0.012, D + 0.012, "Glow", bevel=0.004)
    spine = [(0.30, 0.45), (0.30, 2.42), (0.19, 2.88), (0.07, 2.74), (0.07, 0.45)]
    prism("spine", spine, D - 0.08, D + 0.08, "ArmorMain", bevel=0.018)
    prism("spine_accent", [(0.225, 1.70), (0.225, 2.34), (0.15, 2.64), (0.115, 2.60), (0.115, 1.70)], D - 0.088, D + 0.088, "ArmorAccent", bevel=0.006)
    for z in (0.95, 1.2, 1.45):
        box("spine_slot", (D, 0.185, z), (0.172, 0.13, 0.05), "Dark", bevel=0.004)
    prism("fuller", [(-0.02, 0.95), (-0.02, 2.45), (-0.09, 2.68), (-0.16, 2.45), (-0.16, 0.95)], D - 0.055, D + 0.055, "Dark", bevel=0.006)
    prism("fuller_glow", [(-0.075, 1.05), (-0.075, 2.38), (-0.105, 2.38), (-0.105, 1.05)], D - 0.059, D + 0.059, "Glow", bevel=0.003)
    prism("collar", [(0.35, 0.40), (0.35, 0.78), (0.22, 0.94), (-0.27, 0.94), (-0.40, 0.78), (-0.40, 0.40)], D - 0.105, D + 0.105, "Metal", bevel=0.02)
    for y in (-0.24, 0.0, 0.24):
        cyl("bolt", (D - 0.12, y, 0.62), (D + 0.12, y, 0.62), 0.045, "Trim", bevel=0.006, seg=6)
    mount(-1)
    for y in (-0.15, 0.15):
        cyl("piston", (-0.24, y, -0.78), (-0.24, y, -0.45), 0.065, "Metal", bevel=0.01, seg=12)
        cyl("piston_cap", (-0.24, y, -0.86), (-0.24, y, -0.78), 0.08, "Dark", bevel=0.01, seg=12)
        cyl("piston_glow", (-0.24, y, -0.875), (-0.24, y, -0.86), 0.05, "Glow", bevel=0.0, seg=12)


def shield():
    """왼손 손등(+X) 바깥에 드는 큰 방패. 면은 +X를 본다. 가운데 판 + 뒤로 꺾인 양 날개."""
    X = 0.40
    # 팔 버팀대
    mount(+1, length=(-0.5, 0.5), accent=False)
    for z in (-0.25, 0.3):
        box("strut", (0.36, 0, z), (0.2, 0.34, 0.12), "Metal", bevel=0.012)
    # 뒤 틀
    frame = [(-0.46, -1.15), (0.46, -1.15), (0.50, 1.05), (0.30, 1.38), (-0.30, 1.38), (-0.50, 1.05)]
    prism("frame", frame, X, X + 0.06, "Dark", bevel=0.01)
    # 가운데 장갑판
    main = [(-0.40, -1.22), (0.40, -1.22), (0.44, 1.02), (0.26, 1.32), (0.12, 1.32), (0.07, 1.22), (-0.07, 1.22), (-0.12, 1.32), (-0.26, 1.32), (-0.44, 1.02)]
    prism("main", main, X + 0.06, X + 0.15, "ArmorMain", bevel=0.025)
    # 가운데 등줄기(위로 좁아지는 능선)
    ridge = [(-0.13, -1.10), (0.13, -1.10), (0.16, 0.20), (0.09, 0.95), (-0.09, 0.95), (-0.16, 0.20)]
    prism("ridge", ridge, X + 0.15, X + 0.25, "ArmorMain", bevel=0.02, taper=0.6)
    prism("ridge_glow", [(-0.025, -0.85), (0.025, -0.85), (0.025, 0.55), (-0.025, 0.55)], X + 0.20, X + 0.262, "Glow", bevel=0.003)
    # 빨간 V 무늬
    for sy in (1, -1):
        prism("chev", [(sy * 0.19, 0.42), (sy * 0.40, 0.72), (sy * 0.40, 0.92), (sy * 0.19, 0.62)], X + 0.15, X + 0.175, "ArmorAccent", bevel=0.006)
        prism("chev2", [(sy * 0.19, 0.10), (sy * 0.40, 0.40), (sy * 0.40, 0.50), (sy * 0.19, 0.20)], X + 0.15, X + 0.17, "Dark", bevel=0.004)
        # 아래 덧판
        prism("lower", [(sy * 0.18, -1.16), (sy * 0.38, -1.16), (sy * 0.40, -0.25), (sy * 0.20, -0.05)], X + 0.15, X + 0.19, "Metal", bevel=0.012)
        for z in (-1.0, -0.72, -0.44):
            box("lower_slot", (X + 0.19, sy * 0.29, z), (0.02, 0.13, 0.05), "Dark", bevel=0.003)
        # 날개(뒤로 22도 꺾임)
        wing = box("wing", (X + 0.045, sy * 0.63, -0.02), (0.085, 0.42, 2.25), "ArmorMain", bevel=0.025, rot=(0, 0, -sy * 22))
        box("wing_trim", (X + 0.07, sy * 0.80, -0.02), (0.06, 0.07, 2.31), "Trim", bevel=0.012, rot=(0, 0, -sy * 22))
        box("wing_accent", (X + 0.10, sy * 0.61, 0.62), (0.03, 0.2, 0.42), "ArmorAccent", bevel=0.006, rot=(0, 0, -sy * 22))
        box("wing_glow", (X + 0.103, sy * 0.61, -0.45), (0.02, 0.05, 0.75), "Glow", bevel=0.003, rot=(0, 0, -sy * 22))
        for z in (-1.0, 1.0):
            cyl("wbolt", (X + 0.06, sy * 0.62, z), (X + 0.115, sy * 0.60, z), 0.04, "Trim", bevel=0.005, seg=6)
    # 위 테두리·아래 날
    prism("top_trim", [(-0.30, 1.30), (-0.12, 1.30), (-0.07, 1.20), (0.07, 1.20), (0.12, 1.30), (0.30, 1.30), (0.26, 1.40), (-0.26, 1.40)], X + 0.03, X + 0.18, "Trim", bevel=0.012)
    prism("ram", [(-0.42, -1.20), (0.42, -1.20), (0.30, -1.42), (-0.30, -1.42)], X + 0.02, X + 0.19, "Blade", bevel=0.02)


def cannon():
    """오른손 손등(-X)에 얹는 주포. 포신은 손가락 쪽(+Z)."""
    A = -0.36  # 포신 축 높이
    mount(-1, length=(-0.6, 0.5))
    # 포 몸통
    body = [(-0.20, -0.75), (-0.50, -0.70), (-0.56, -0.35), (-0.56, 0.42), (-0.48, 0.62), (-0.20, 0.62)]
    prism("body", body, -0.21, 0.21, "ArmorMain", bevel=0.03, plane='XZ')
    prism("body_accent", [(-0.565, -0.2), (-0.59, -0.14), (-0.59, 0.30), (-0.565, 0.36)], -0.12, 0.12, "ArmorAccent", bevel=0.008, plane='XZ')
    # 포신: 방열 덮개 → 포신 → 포구 제동기
    tube("sleeve", (A, 0, 0.55), (A, 0, 1.25), 0.185, 0.05, "ArmorMain", bevel=0.012)
    for i in range(8):
        import math
        a = math.pi * 2 * i / 8
        box("sleeve_slot", (A + 0.186 * math.cos(a), 0.186 * math.sin(a), 0.92), (0.02, 0.045, 0.42), "Dark", bevel=0.003, rot=(0, 0, math.degrees(a)))
    tube("barrel", (A, 0, 0.5), (A, 0, 2.15), 0.125, 0.04, "Metal", bevel=0.008)
    for z in (1.30, 1.52, 1.74):
        tube("ring", (A, 0, z), (A, 0, z + 0.05), 0.15, 0.03, "Trim", bevel=0.006)
    tube("glow_ring", (A, 0, 1.90), (A, 0, 1.94), 0.14, 0.02, "Glow", bevel=0.0)
    tube("muzzle", (A, 0, 1.98), (A, 0, 2.32), 0.175, 0.06, "Dark", bevel=0.012)
    for i in range(4):
        a = math.pi / 4 + math.pi / 2 * i
        box("muzzle_port", (A + 0.17 * math.cos(a), 0.17 * math.sin(a), 2.15), (0.03, 0.07, 0.16), "Glow", bevel=0.003, rot=(0, 0, math.degrees(a)))
    # 반동 실린더(양옆)
    for y in (-0.27, 0.27):
        cyl("recoil", (A + 0.05, y, -0.3), (A + 0.05, y, 0.95), 0.055, "Metal", bevel=0.008, seg=12)
        cyl("recoil_cap", (A + 0.05, y, 0.95), (A + 0.05, y, 1.05), 0.07, "Trim", bevel=0.008, seg=12)
        box("recoil_block", (A + 0.05, y, 0.1), (0.16, 0.16, 0.3), "Dark", bevel=0.015)
    # 탄창 드럼(등 위)
    cyl("drum", (-0.62, -0.20, -0.42), (-0.62, 0.20, -0.42), 0.20, "Dark", bevel=0.02, seg=20)
    for y in (-0.215, 0.215):
        cyl("drum_cap", (-0.62, y - 0.012, -0.42), (-0.62, y + 0.012, -0.42), 0.15, "ArmorAccent", bevel=0.006, seg=20)
        cyl("drum_hub", (-0.62, y - 0.03, -0.42), (-0.62, y + 0.03, -0.42), 0.05, "Trim", bevel=0.006, seg=8)
    # 조준기
    box("sight", (-0.63, 0, 0.30), (0.10, 0.14, 0.34), "Metal", bevel=0.015)
    cyl("sight_lens", (-0.63, 0, 0.47), (-0.63, 0, 0.485), 0.04, "Glow", bevel=0.0, seg=12)
    # 뒤 배기구
    for y in (-0.1, 0.1):
        tube("exhaust", (A - 0.02, y, -0.92), (A - 0.02, y, -0.7), 0.075, 0.02, "Metal", bevel=0.006, seg=14)


def scanner():
    """왼손 손등(+X)에 얹는 탐지기. 큰 렌즈와 접시가 손가락 쪽(+Z)을 본다."""
    import math
    A = 0.36
    mount(+1, length=(-0.5, 0.45))
    body = [(0.20, -0.62), (0.50, -0.56), (0.58, -0.2), (0.58, 0.35), (0.50, 0.5), (0.20, 0.5)]
    prism("body", body, -0.22, 0.22, "ArmorMain", bevel=0.03, plane='XZ')
    # 주 렌즈 통
    tube("eye", (A, 0, 0.42), (A, 0, 0.86), 0.21, 0.035, "Metal", bevel=0.012)
    tube("eye_hood", (A, 0, 0.80), (A, 0, 0.95), 0.235, 0.03, "ArmorMain", bevel=0.01)
    cyl("iris", (A, 0, 0.70), (A, 0, 0.76), 0.176, "Dark", bevel=0.004, seg=24)
    cyl("lens", (A, 0, 0.74), (A, 0, 0.785), 0.12, "Glow", bevel=0.01, seg=24)
    tube("lens_ring", (A, 0, 0.76), (A, 0, 0.80), 0.145, 0.02, "Trim", bevel=0.004)
    # 보조 렌즈 셋
    for i, y in enumerate((-0.15, 0.0, 0.15)):
        cyl("sub", (0.60, y, 0.30), (0.60, y, 0.52), 0.05, "Dark", bevel=0.006, seg=12)
        cyl("sub_lens", (0.60, y, 0.52), (0.60, y, 0.53), 0.035, "Glow", bevel=0.0, seg=12)
    # 접시 안테나(등 위, 기둥 + 접시 + 급전봉)
    cyl("mast", (0.56, 0, -0.22), (0.72, 0, -0.22), 0.05, "Metal", bevel=0.006, seg=10)
    cyl("dish_base", (0.76, 0, -0.34), (0.76, 0, -0.26), 0.09, "Dark", bevel=0.01, seg=16)
    cyl("dish", (0.76, 0, -0.27), (0.76, 0, -0.20), 0.08, "ArmorMain", bevel=0.004, seg=28, r2=0.20)
    cyl("dish_in", (0.76, 0, -0.25), (0.76, 0, -0.196), 0.06, "Dark", bevel=0.0, seg=28, r2=0.185)
    cyl("feed", (0.76, 0, -0.25), (0.76, 0, -0.10), 0.014, "Metal", bevel=0.0, seg=6)
    cyl("feed_tip", (0.76, 0, -0.12), (0.76, 0, -0.08), 0.03, "Glow", bevel=0.004, seg=10)
    prism("brow", [(0.585, -0.05), (0.61, 0.0), (0.61, 0.26), (0.585, 0.31)], -0.13, 0.13, "ArmorAccent", bevel=0.008, plane='XZ')
    # 칼날 안테나 둘
    for sy in (1, -1):
        prism("fin", [(0.50, -0.50), (0.60, -0.42), (0.80, -0.78), (0.74, -0.84)], sy * 0.17 - 0.015, sy * 0.17 + 0.015, "Dark", bevel=0.004, plane='XZ')
        box("side_pod", (0.40, sy * 0.27, 0.05), (0.22, 0.10, 0.5), "Metal", bevel=0.02)
        box("side_glow", (0.40, sy * 0.323, 0.05), (0.06, 0.012, 0.36), "Glow", bevel=0.0)
    box("back_pack", (0.42, 0, -0.72), (0.26, 0.30, 0.2), "Dark", bevel=0.02)


BUILD = {"Weapon_Sword": sword, "Weapon_Shield": shield, "Weapon_Cannon": cannon, "Weapon_Scanner": scanner}

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    out = args[0]
    for name in (args[1:] or list(BUILD)):
        k.reset()
        BUILD[name]()
        ob = k.join_all(name)
        k.bake_and_export(ob, f"{out}/{name}", name, shift_z=ALONG[name])
