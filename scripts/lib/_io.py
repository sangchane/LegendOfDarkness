"""파일 읽기 — 생성기마다 따로 적던 `read` 들."""
import json
import re

#: 꼬리 쉼표가 남은 정의가 있다(하데스가 제 손으로 쓴 것).
LENIENT = re.compile(r",(\s*[\]}])")


def read_json(path):
    """BOM 이 붙어 있어도 읽는다."""
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_lenient_json(path, errors="strict"):
    """꼬리 쉼표를 너그럽게 넘긴다."""
    return json.loads(LENIENT.sub(r"\1", path.read_text(encoding="utf-8-sig", errors=errors)))


def read_text(path, *encodings, fallback=None):
    """인코딩을 차례로 해 본다. 다 안 되면 `fallback`, 없으면 UTF-8 로 깨진 글자만 바꿔 읽는다."""
    for encoding in encodings:
        try:
            return path.read_text(encoding=encoding)
        except (ValueError, LookupError):
            continue
    return path.read_text(encoding="utf-8", errors="replace") if fallback is None else fallback


def read_source(path):
    """서버팩 스크립트 — UTF-8 이나 CP949. 둘 다 아니면 빈 글."""
    return read_text(path, "utf-8", "cp949", fallback="")
