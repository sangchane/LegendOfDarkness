"""git 에서 읽는 것."""
import subprocess
import sys


def git_pointer(repo):
    """`repo` 가 가리키는 커밋(짧게). 못 구하면 "unknown"."""
    try:
        return subprocess.run(
            ["git", "-C", str(repo), "rev-parse", "--short", "HEAD"],
            capture_output=True, text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError) as e:
        print(f"git 포인터 못 구함: {e}", file=sys.stderr)
        return "unknown"
