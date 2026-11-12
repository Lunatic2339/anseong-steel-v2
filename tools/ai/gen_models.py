"""무기·드론·철판·포탄 모델을 Meshy(text-to-3d)로 만들어 Assets/_Anseong/Art/AI/<이름>/ 에 넣는다.

키는 환경 변수 MESHY_API_KEY 에서만 읽는다. 이미 <이름>.fbx 가 있으면 건너뛴다.
  python tools/ai/gen_models.py balance          # 남은 크레딧(크레딧 안 씀)
  python tools/ai/gen_models.py make [--only Weapon_Sword]
그다음 Unity 메뉴 Anseong → Build Full Game (한 번에) 가 크기·방향을 맞춰 프리팹으로 만들고 칸에 넣는다.
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://api.meshy.ai"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Anseong", "Art", "AI"))

OURS = "heavy industrial mecha part, angular armor plates, white and light grey with blue accents, light-blue emissive lines, worn metal, hard surface, no text, no logo"
BOSS = "heavy alien-built war machine part, angular black armor, red accents, yellow-orange emissive glow, worn dark metal, hard surface, no text, no logo"

MODELS = [
    ("Weapon_Sword", 8000, "A giant mecha sword standing upright, blade pointing up, long broad single straight blade with thick spine, short grip and blocky crossguard at the bottom. " + OURS),
    ("Weapon_Shield", 8000, "A giant mecha arm shield seen from the front, tall rectangular tower shield with chamfered corners, layered armor plates, flat and thick. " + OURS),
    ("Weapon_Cannon", 8000, "A giant mecha forearm-mounted autocannon standing upright, one thick short barrel pointing up, boxy receiver and ammo drum at the bottom, no stand. " + OURS),
    ("Weapon_Scanner", 6000, "A giant mecha forearm-mounted sensor scanner pod standing upright, boxy housing with a big round lens at the top and small antenna fins. " + OURS),
    ("Drone", 5000, "A small hostile combat drone, compact angular body with one big front lens eye and a single gun barrel, four short stubby thruster pods, no propellers. " + BOSS),
    ("Checkup_SteelPlate", 1000, "A thick rectangular steel armor test plate standing upright on a simple welded frame base, scratched bare steel with yellow hazard stripes at the edges, industrial, hard surface, no text, no logo"),
    ("Boss_FistShell", 2000, "A blunt heavy artillery shell shaped like a short thick bullet, glowing hot orange tip. " + BOSS),
]


def key():
    k = os.environ.get("MESHY_API_KEY", "").strip()
    if not k:
        sys.exit("MESHY_API_KEY 환경 변수가 없습니다.")
    return k


def call(method, path, body=None):
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Authorization", "Bearer " + key())
    if data is not None:
        req.add_header("Content-Type", "application/json")
    for attempt in range(4):
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                return json.loads(r.read())
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:500]
            if e.code in (429, 500, 502, 503) and attempt < 3:
                time.sleep(10 * (attempt + 1))
                continue
            raise RuntimeError(f"HTTP {e.code} {path}: {detail}")
        except urllib.error.URLError as e:
            if attempt < 3:
                time.sleep(10)
                continue
            raise RuntimeError(f"연결 실패 {path}: {e}")


def download(url, path):
    req = urllib.request.Request(url, headers={"User-Agent": "python-urllib"})
    with urllib.request.urlopen(req, timeout=600) as r, open(path, "wb") as f:
        while True:
            chunk = r.read(1 << 20)
            if not chunk:
                break
            f.write(chunk)


def wait(task_id, label):
    start = time.time()
    while True:
        task = call("GET", "/openapi/v2/text-to-3d/" + task_id)
        status = task.get("status")
        if status == "SUCCEEDED":
            return task
        if status in ("FAILED", "CANCELED"):
            raise RuntimeError(f"{label} {status}: {task.get('task_error')}")
        if time.time() - start > 1500:
            raise RuntimeError(f"{label} 25분 넘게 안 끝남(task {task_id})")
        time.sleep(10)


def make(name, polys, prompt):
    folder = os.path.join(OUT, name)
    fbx = os.path.join(folder, name + ".fbx")
    if os.path.exists(fbx):
        return "있어서 건너뜀"
    os.makedirs(folder, exist_ok=True)
    preview = call("POST", "/openapi/v2/text-to-3d", {
        "mode": "preview", "prompt": prompt[:800], "ai_model": "latest",
        "should_remesh": True, "topology": "triangle", "target_polycount": polys,
        "target_formats": ["fbx"],
    })["result"]
    wait(preview, name + " preview")
    refine = call("POST", "/openapi/v2/text-to-3d", {
        "mode": "refine", "preview_task_id": preview, "enable_pbr": True, "texture_resolution": "2k",
        "target_formats": ["fbx"],
    })["result"]
    task = wait(refine, name + " refine")
    url = (task.get("model_urls") or {}).get("fbx")
    if not url:
        raise RuntimeError(name + ": fbx 주소가 없음")
    download(url, fbx)
    textures = (task.get("texture_urls") or [{}])[0]
    for kind, suffix in (("base_color", "BaseColor"), ("normal", "Normal"), ("metallic", "Metallic"), ("roughness", "Roughness"), ("emission", "Emission")):
        if textures.get(kind):
            download(textures[kind], os.path.join(folder, f"{name}_{suffix}.png"))
    return f"만듦({os.path.getsize(fbx) // 1024}KB, 크레딧 {task.get('consumed_credits')})"


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("kind", choices=["balance", "make"])
    p.add_argument("--only")
    a = p.parse_args()
    if a.kind == "balance":
        print(call("GET", "/openapi/v1/balance"))
        sys.exit(0)
    for name, polys, prompt in MODELS:
        if a.only and a.only != name:
            continue
        try:
            print(name, "→", make(name, polys, prompt), flush=True)
        except RuntimeError as e:
            print(name, "→ 실패:", e, flush=True)
