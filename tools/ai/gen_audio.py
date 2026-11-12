"""사령관 대사·효과음·배경음악을 ElevenLabs로 만들어 Assets/_Anseong/Audio/ 아래에 넣는다.

키는 환경 변수 ELEVENLABS_API_KEY 에서만 읽는다(레포·로그에 남기지 않는다).
이미 있는 파일은 건너뛴다. 다시 만들려면 그 파일(.mp3와 .meta)을 지우고 다시 돌린다.

  python tools/ai/gen_audio.py voice [--voice 이름] [--only VO_Commander_06]
  python tools/ai/gen_audio.py sfx   [--only SFX_PunchHit_01]
  python tools/ai/gen_audio.py bgm
  python tools/ai/gen_audio.py voices     # 쓸 수 있는 목소리 목록
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

API = "https://api.elevenlabs.io"
HERE = os.path.dirname(os.path.abspath(__file__))
AUDIO = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Anseong", "Audio"))
# Creator 요금제: 44.1kHz WAV/PCM은 Pro부터라 MP3로 받는다(Unity가 그대로 읽음).
FORMAT = "mp3_44100_128"
VOICE_MODEL = "eleven_multilingual_v2"
# 낮고 단단한 남자 목소리 후보(계정에 있는 첫 번째를 쓴다).
VOICE_CANDIDATES = ["Bill", "Brian", "George", "Daniel", "Adam", "Antoni"]


def key():
    k = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not k:
        sys.exit("ELEVENLABS_API_KEY 환경 변수가 없습니다. (cmd: setx ELEVENLABS_API_KEY 키  → 새 창에서 다시)")
    return k


def call(method, path, body=None, query=None):
    url = API + path + ("?" + urllib.parse.urlencode(query) if query else "")
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("xi-api-key", key())
    if data is not None:
        req.add_header("Content-Type", "application/json")
    for attempt in range(4):
        try:
            with urllib.request.urlopen(req, timeout=600) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:500]
            if e.code in (429, 500, 502, 503) and attempt < 3:
                time.sleep(5 * (attempt + 1))
                continue
            raise RuntimeError(f"HTTP {e.code} {path}: {detail}")
        except urllib.error.URLError as e:
            if attempt < 3:
                time.sleep(5 * (attempt + 1))
                continue
            raise RuntimeError(f"연결 실패 {path}: {e}")


def save(folder, name, data):
    os.makedirs(os.path.join(AUDIO, folder), exist_ok=True)
    path = os.path.join(AUDIO, folder, name + ".mp3")
    with open(path, "wb") as f:
        f.write(data)
    return path


def exists(folder, name):
    base = os.path.join(AUDIO, folder, name)
    return any(os.path.exists(base + ext) for ext in (".mp3", ".wav", ".ogg"))


def list_voices():
    out = json.loads(call("GET", "/v2/voices", query={"page_size": 100}))
    return out.get("voices", [])


def pick_voice(wanted):
    voices = list_voices()
    names = [wanted] if wanted else VOICE_CANDIDATES
    for n in names:
        for v in voices:
            if v.get("name", "").lower().startswith(n.lower()):
                return v
    if wanted:
        sys.exit(f"목소리 '{wanted}'를 찾지 못했습니다. 'voices'로 목록을 보세요.")
    if not voices:
        sys.exit("계정에 쓸 수 있는 목소리가 없습니다.")
    return voices[0]


def run(kind, only, voice_name):
    manifest = json.load(open(os.path.join(HERE, "audio_manifest.json"), encoding="utf-8"))
    made, skipped, failed = [], [], []
    if kind == "voices":
        for v in list_voices():
            print(v.get("name"), "|", v.get("category"), "|", (v.get("labels") or {}))
        return
    if kind == "voice":
        voice = pick_voice(voice_name)
        print("목소리:", voice.get("name"))
        for item in manifest["voice"]:
            if only and item["file"] != only:
                continue
            if exists("Voice", item["file"]):
                skipped.append(item["file"])
                continue
            try:
                data = call("POST", "/v1/text-to-speech/" + voice["voice_id"],
                            {"text": item["text"], "model_id": VOICE_MODEL, "language_code": "ko",
                             "voice_settings": {"stability": 0.45, "similarity_boost": 0.8, "style": 0.35, "speed": 1.05}},
                            {"output_format": FORMAT})
                save("Voice", item["file"], data)
                made.append(item["file"])
            except RuntimeError as e:
                failed.append(f"{item['file']}: {e}")
    elif kind == "sfx":
        for item in manifest["sfx"]:
            if only and item["file"] != only:
                continue
            if exists("SFX", item["file"]):
                skipped.append(item["file"])
                continue
            try:
                data = call("POST", "/v1/sound-generation",
                            {"text": item["text"], "duration_seconds": item["seconds"], "prompt_influence": 0.5},
                            {"output_format": FORMAT})
                save("SFX", item["file"], data)
                made.append(item["file"])
            except RuntimeError as e:
                failed.append(f"{item['file']}: {e}")
    elif kind == "bgm":
        for item in manifest["bgm"]:
            if only and item["file"] != only:
                continue
            if exists("BGM", item["file"]):
                skipped.append(item["file"])
                continue
            try:
                data = call("POST", "/v1/music",
                            {"prompt": item["text"], "music_length_ms": item["ms"], "force_instrumental": True},
                            {"output_format": FORMAT})
                save("BGM", item["file"], data)
                made.append(item["file"])
            except RuntimeError as e:
                failed.append(f"{item['file']}: {e}")
    print(f"만듦 {len(made)}개, 있어서 건너뜀 {len(skipped)}개, 실패 {len(failed)}개")
    for f in failed:
        print("  실패:", f)


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("kind", choices=["voice", "sfx", "bgm", "voices"])
    p.add_argument("--only")
    p.add_argument("--voice")
    a = p.parse_args()
    run(a.kind, a.only, a.voice)
