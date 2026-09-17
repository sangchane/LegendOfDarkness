#!/usr/bin/env python3
"""원작 배경음악 64곡을 모바일 클라이언트로 옮긴다.

  python3 scripts/build-client-music.py [--쓰기]

곡은 `database/assets/MusicFiles/<번호>.mus` 에 있다 — 확장자만 `.mus` 이고 **속은 MP3** 다(ID3 태그가 남아
있어 `45.mus` 가 "woodlands", `44.mus` 가 "FORTE FOREST" 인 것까지 보인다). 5.99 클라이언트의 `music/` 과
같은 파일이다.

**맵이 어느 곡을 트나**는 서버 맵 정의의 `Music` 칸이 정하고, 클라이언트는 **번호에서 128 을 뺀 것**을 곡
번호로 쓴다(원작 `Legend.exe 0x54c440` — 128 미만이면 효과음, 228 이면 끈다). 그래서 우드랜드의 173 은
`45.mus`("woodlands")다.

그대로 넣으면 65MB 라 Ogg Vorbis 로 줄여 넣는다(33kbps 남짓, 합쳐 30MB 남짓).
"""

import argparse
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
FROM = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/assets/MusicFiles"
TO = ROOT / "mobile/client/assets/music"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    songs = sorted(FROM.glob("*.mus"), key=lambda path: int(path.stem))

    if not songs:
        print(f"곡이 없습니다 — {FROM}")
        return 1

    if writing:
        TO.mkdir(parents=True, exist_ok=True)

    made = 0
    before = after = 0

    for song in songs:
        target = TO / f"{song.stem}.ogg"
        before += song.stat().st_size

        if writing:
            subprocess.run(
                # 이 맥의 ffmpeg 에는 libvorbis 가 없어 내장 인코더를 쓴다 — 실험용이라 -strict -2 가 필요하고
                # 스테레오만 받는다(원본 모노도 -ac 2 로 맞춘다). q1 이면 33kbps 남짓으로 곡 길이는 그대로다.
                ["ffmpeg", "-y", "-loglevel", "error", "-i", str(song),
                 "-ac", "2", "-c:a", "vorbis", "-strict", "-2", "-q:a", "1", str(target)],
                check=True,
            )
            after += target.stat().st_size

        made += 1

    print(f"곡 {made} 개 · {before / 1e6:.0f}MB" + (f" → {after / 1e6:.0f}MB" if writing else ""))
    print("적었습니다." if writing else "미리 본 것입니다 — 적으려면 --쓰기")

    return 0


if __name__ == "__main__":
    sys.exit(main())
