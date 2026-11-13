"""Cascadeur 스크립트 서버(Scripts → MCP → Start script server)에 파이썬 코드를 보내는 도구. 선우 PC에서만 쓴다.

    python tools/casc/casc_run.py 파일.py      파일 내용을 Cascadeur 안에서 실행
    python tools/casc/casc_run.py -c "코드"    한 줄 실행
    python tools/casc/casc_run.py --ping       서버가 켜져 있는지 확인(꺼져 있으면 종료 코드 2)
"""
import json
import sys
import urllib.error
import urllib.request

URL = "http://127.0.0.1:8765/run"


def run(code, timeout=600):
    req = urllib.request.Request(URL, data=json.dumps({"code": code}).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as res:
        return res.read().decode("utf-8", "replace")


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    if argv[1] == "--ping":
        code = "print('ok')"
    elif argv[1] == "-c":
        code = argv[2]
    else:
        with open(argv[1], encoding="utf-8") as f:
            code = f.read()
    try:
        print(run(code))
    except (urllib.error.URLError, ConnectionError, TimeoutError) as e:
        print(f"Cascadeur 스크립트 서버에 연결 못 함: {e}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
