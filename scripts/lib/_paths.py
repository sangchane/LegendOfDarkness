"""저장소 뿌리. 스크립트마다 `Path(__file__).resolve().parent.parent` 를 따로 적던 것."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
