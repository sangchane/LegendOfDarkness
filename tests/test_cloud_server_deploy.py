"""운영 스크립트(cloud-server.sh · cloud-dashboard.sh)를 가짜 원격으로 돌려 본다 — 리뷰 2026-10-08 4·10·13번.

진짜 클라우드·~/.ssh·관리자 명령은 하나도 쓰지 않는다. PATH 앞의 가짜 ssh 가 호스트를 버리고 명령을 이 기계의
bash 로 돌리며, 원격 /home/ubuntu 를 시험 폴더로 바꾼다. sudo·systemctl·ss·journalctl·crontab·sleep 도 가짜,
dotnet 은 가짜 저장소 뿌리의 .tools 에 둔다. rsync 는 진짜이고, FAKE_FAIL_RSYNC 가 인자에 들어 있으면 그 호출만 실패한다.
"""
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = "sources/wren11/Dark-Ages-Private-Server"
IP = "203.0.113.9"  # 문서용 주소 — 어디에도 닿지 않는다
REAL_RSYNC = shutil.which("rsync")
PASSWORD = "it's \"quoted\" back\\slash sp ace 한글 $HOME `id`"

FAKES = {
    "ssh": r"""#!/bin/bash
# 가짜 ssh — 옵션·호스트를 버리고 명령을 이 기계의 bash 로. 원격 /home/ubuntu 는 FAKE_HOME 으로.
while [ $# -gt 0 ]; do
  case "$1" in -[ilopFJ]) shift 2 ;; -*) shift ;; *) echo "$1" >> "$FAKE_STATE/ssh-hosts"; shift; break ;; esac
done
cmd="$*"; cmd="${cmd//\/home\/ubuntu/$FAKE_HOME}"
case "$cmd" in
  *"bash -s"*) sed "s|/home/ubuntu|$FAKE_HOME|g" | bash -c "$cmd" ;;
  *) exec bash -c "$cmd" ;;
esac
""",
    "sudo": r"""#!/bin/bash
# 가짜 sudo — 관리자 명령은 돌리지 않고 적어만 둔다. systemctl 은 가짜로, tee·htpasswd 는 받은 stdin 을 남긴다.
echo "sudo $*" >> "$FAKE_STATE/sudo.log"
case "$1" in
  systemctl) shift; exec systemctl "$@" ;;
  tee|htpasswd) cat > "$FAKE_STATE/$1.stdin" ;;
esac
exit 0
""",
    "systemctl": r"""#!/bin/bash
# 가짜 systemctl — lod 를 켤 때 지금 운영 폴더의 서버 dll 을 보고 뜰지(nostart)·준비될지(notready) 정한다.
echo "$*" >> "$FAKE_STATE/systemctl.log"
verb=$1; shift
for unit in "$@"; do
  [ "$unit" = lod ] || continue
  case "$verb" in
    start|restart)
      rm -f "$FAKE_STATE/ready"; : > "$FAKE_STATE/journal"
      dll="$(cat "$FAKE_HOME/lod/Staging/net9.0/Lorule.GameServer.dll" 2>/dev/null)"
      case "$dll" in *nostart*) exit 1 ;; esac
      # 준비가 안 되는 새 서버도 그사이 게시판에 글을 쓴다 — 되돌림이 그 글을 지우면 안 된다(보안 리뷰 2026-10-08).
      case "$dll" in *notready*) mkdir -p "$FAKE_HOME/lod/database/server/community/Boards"; echo "written while new" > "$FAKE_HOME/lod/database/server/community/Boards/new-post.json" ;;
        *) touch "$FAKE_STATE/ready"; echo "Game server is online." > "$FAKE_STATE/journal" ;; esac ;;
    stop) rm -f "$FAKE_STATE/ready"; : > "$FAKE_STATE/journal" ;;
  esac
done
[ "$verb" = list-unit-files ] && exit 1  # 옛 하나짜리 lod-bot 은 없다
exit 0
""",
    "ss": """#!/bin/bash
[ -f "$FAKE_STATE/ready" ] && printf 'LISTEN 0 512 0.0.0.0:2610 0.0.0.0:*\\nLISTEN 0 512 0.0.0.0:2615 0.0.0.0:*\\n'
exit 0
""",
    "journalctl": """#!/bin/bash
cat "$FAKE_STATE/journal" 2>/dev/null
exit 0
""",
    "crontab": """#!/bin/bash
# 가짜 crontab — 이 맥의 진짜 crontab 을 건드리지 않는다.
if [ "${1:-}" = -l ]; then cat "$FAKE_STATE/crontab" 2>/dev/null; else cat > "$FAKE_STATE/crontab"; fi
""",
    "sleep": "#!/bin/bash\nexit 0\n",
    "flock": "#!/bin/bash\nexit 0\n",  # 맥에는 없다 — 클라우드(우분투)의 util-linux flock 자리
    "rsync": f"""#!/bin/bash
if [ -n "${{FAKE_FAIL_RSYNC:-}}" ] && [[ "$*" == *"$FAKE_FAIL_RSYNC"* ]]; then echo "rsync: 주입한 실패" >&2; exit 23; fi
exec {REAL_RSYNC} "$@"
""",
}

FAKE_DOTNET = r"""#!/bin/bash
# 가짜 dotnet — build 는 서버 dll 을 Staging 에(진짜처럼 맥 설정도 덮어쓴다), publish 는 프로젝트 이름의 dll 을 -o 폴더에.
case "$1" in
  --version) echo 9.0.317-fake ;;
  build)
    [ -z "${FAKE_BUILD_FAIL:-}" ] || { echo "error CS0001: 주입한 빌드 실패" >&2; exit 1; }
    staging="$(dirname "$2")/../../Staging/net9.0"
    printf '%s' "${FAKE_SERVER_DLL:-new-server build}" > "$staging/Lorule.GameServer.dll"
    echo "BUILD OUTPUT CONFIG" > "$staging/LoruleConfig.json" ;;
  publish)
    project=$2; shift 2
    while [ $# -gt 0 ]; do [ "$1" = -o ] && out=$2; shift; done
    mkdir -p "$out"; printf 'new %s build' "$(basename "$project")" > "$out/$(basename "$project").dll" ;;
esac
"""

# 맥 쪽 — 서버 소스 뿌리(FORK) 아래와 저장소 나머지.
MAC_FILES = {
    f"{FORK}/src/Lorule.GameServer/Lorule.GameServer.csproj": "<Project />",
    f"{FORK}/Staging/net9.0/Shared.dll": "shared library v2",
    f"{FORK}/Staging/net9.0/LoruleConfig.json": "MAC CONFIG",
    f"{FORK}/Staging/net9.0/MServerTable.xml": "MAC TABLE",
    f"{FORK}/Staging/net9.0/Hades_General.txt": "mac log",
    f"{FORK}/Staging/net9.0/activity/mac.jsonl": "mac activity",
    f"{FORK}/database/server/templates/item.json": "new template data",
    f"{FORK}/database/server/aislings/mac-hero.json": "MAC HERO",
    f"{FORK}/database/server/auction/mac-book.json": "MAC BOOK",
    f"{FORK}/database/assets/tile.bin": "new asset data",
    "mobile/bots/Lod.CompanionBot/Lod.CompanionBot.csproj": "<Project />",
    "mobile/bots/Lod.HuntProxy/Lod.HuntProxy.csproj": "<Project />",
    "mobile/bots/Lod.EcoBots/Lod.EcoBots.csproj": "<Project />",
    "mobile/client/assets/world/map1.txt": "new map data",
    "mobile/client/assets/world/guide.txt": "guide",
    "mobile/client/assets/world/eco-grounds.txt": "grounds",
    "mobile/client/assets/world/class-kit.txt": "kit",
    "mobile/client/assets/world/readme.md": "not for bots",
    "scripts/ml/export-activity.py": "new export script",
    "scripts/ops/ability-ops-service.py": "new service",
    "scripts/ops/activity_store.py": "new store",
    "data/game-data/ability-operations.json": "{}",
    "docs/index.html": "new dashboard page",
    "docs/download/index.html": "download page",
}

# 클라우드 쪽 코드(배포가 바꾸는 것) — 옛 것. mtime 은 과거로 둬 rsync 가 새것과 가린다.
CLOUD_CODE = {
    "lod/Staging/net9.0/Lorule.GameServer.dll": "old-server",
    "lod/Staging/net9.0/Obsolete.dll": "obsolete",
    "lod/Staging/net9.0/LoruleConfig.json": "old cloud config",
    "lod/Staging/net9.0/MServerTable.xml": "old cloud table",
    "lod/database/server/templates/item.json": "old template",
    "lod/database/server/community/Boards/news.json": "cloud board",
    "lod/database/assets/tile.bin": "old asset",
    "lod-bot/app/Lod.CompanionBot.dll": "old bot",
    "lod-bot/world/map1.txt": "old map",
    "lod-bot/world/notes.dat": "cloud only",
    "lod-proxy/app/Lod.HuntProxy.dll": "old proxy",
    "lod-eco/app/Lod.EcoBots.dll": "old eco",
    "lod-eco/ml/export-activity.py": "old export",
    "lod-ops/www/index.html": "old dashboard page",
    "lod-ops/www/stale.html": "removed page",
    "lod-ops/data/credential": "lod-admin:old",
}

# live 자료 — 배포가 어떤 경우에도 옮기거나 지우거나 덮으면 안 된다.
CLOUD_LIVE = {
    "lod/Staging/net9.0/Hades_General.txt": "cloud log",
    "lod/Staging/net9.0/activity/2026-10-08.jsonl": "cloud activity",
    "lod/database/server/aislings/hero.json": "HERO",
    "lod/database/server/auction/book.json": "BOOK",
    "lod-bot/companion-bot-1.json": "BOT SECRET",
    "lod-proxy/hunt-proxy.json": "{}",
    "lod-proxy/jobs/job1.json": "JOB",
    "lod-eco/eco-bots.json": "ECO SECRET",
    "lod-eco/eco-unlisted.json": '["Stick"]',
    "lod-eco/eco/events.jsonl": "EVENTS",
    "lod-ml/salt": "SALT",
    "lod-ops/www/download/version-ios.txt": "42",
    "lod-ops/www/download/version-windows.txt": "7",
}


def write(base: Path, files, old=False):
    for name, text in files.items():
        path = base / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        if old:
            os.utime(path, (1_577_836_800, 1_577_836_800))  # 2020-01-01


def tree(base: Path):
    """운영 폴더의 파일 → 내용. 배포가 쓰는 옆 폴더(.next·.prev)는 뺀다."""
    return {str(p.relative_to(base)): p.read_text(encoding="utf-8")
            for p in sorted(base.rglob("*"))
            if p.is_file() and not any(part.endswith((".next", ".prev")) for part in p.relative_to(base).parts)}


@unittest.skipUnless(REAL_RSYNC, "rsync 가 없다")
class FakeCloudCase(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        base = Path(self.scratch.name)
        self.root, self.remote, self.mac_home, self.bin, self.state = (
            base / "root", base / "remote", base / "mac-home", base / "bin", base / "state")
        for folder in (self.root, self.remote, self.mac_home, self.bin, self.state):
            folder.mkdir()
        (self.root / "scripts/ops").mkdir(parents=True)
        for name in ("cloud-server.sh", "cloud-dashboard.sh", "check-server-config.sh"):
            shutil.copy2(ROOT / "scripts/ops" / name, self.root / "scripts/ops" / name)
        shutil.copytree(ROOT / "scripts/ops/server-config", self.root / "scripts/ops/server-config")
        write(self.root, MAC_FILES)
        write(self.root, {".tools/dotnet-9.0.317/dotnet": FAKE_DOTNET})
        os.chmod(self.root / ".tools/dotnet-9.0.317/dotnet", 0o755)
        for name, text in FAKES.items():
            (self.bin / name).write_text(text, encoding="utf-8")
            os.chmod(self.bin / name, 0o755)
        write(self.remote, CLOUD_CODE, old=True)
        write(self.remote, CLOUD_LIVE, old=True)
        self.before = tree(self.remote)

    def tearDown(self):
        self.scratch.cleanup()

    def run_script(self, script, *args, **env):
        environment = {
            "PATH": f"{self.bin}:/usr/bin:/bin:/usr/sbin:/sbin:/opt/homebrew/bin",
            "HOME": str(self.mac_home), "LOD_CLOUD_IP": IP, "LANG": "en_US.UTF-8",
            "FAKE_HOME": str(self.remote), "FAKE_STATE": str(self.state), **env}
        return subprocess.run(["bash", str(self.root / "scripts/ops" / script), *args], env=environment,
                              capture_output=True, text=True, timeout=180)

    def systemctl_log(self):
        path = self.state / "systemctl.log"
        return path.read_text(encoding="utf-8").splitlines() if path.exists() else []

    def assert_live_kept(self, after):
        for name, text in CLOUD_LIVE.items():
            self.assertEqual(after.get(name), text, f"live 자료 {name} 이 바뀌었다")

    def assert_only_fake_host(self):
        hosts = self.state / "ssh-hosts"
        if hosts.exists():
            self.assertEqual(set(hosts.read_text().split()), {f"ubuntu@{IP}"})


class CloudServerDeployTests(FakeCloudCase):
    """리뷰 4 — 모두 만들고 .next 로 올린 뒤 한 번에 전환, 실패하면 운영 폴더는 옛것 그대로."""

    def test_success_switches_every_code_folder_and_keeps_live_data(self):
        result = self.run_script("cloud-server.sh", "deploy")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        after = tree(self.remote)
        self.assert_live_kept(after)
        self.assertEqual(after["lod/Staging/net9.0/Lorule.GameServer.dll"], "new-server build")
        self.assertEqual(after["lod/Staging/net9.0/Shared.dll"], "shared library v2")
        self.assertNotIn("lod/Staging/net9.0/Obsolete.dll", after)
        # 설정 두 파일은 맥 것이 아니라 틀에서 클라우드 주소로 만든 것.
        self.assertIn(IP, after["lod/Staging/net9.0/LoruleConfig.json"])
        self.assertIn("/home/ubuntu/lod/database", after["lod/Staging/net9.0/LoruleConfig.json"])
        self.assertIn(IP, after["lod/Staging/net9.0/MServerTable.xml"])
        manifest = json.loads(after["lod/Staging/net9.0/deploy-manifest.json"])
        self.assertEqual(set(manifest), {"root", "rootDirty", "server", "serverDirty", "sdk", "builtAt"})
        self.assertEqual(manifest["sdk"], "9.0.317-fake")
        # 맥의 live 자료는 올라가지 않는다.
        for name in ("lod/database/server/aislings/mac-hero.json", "lod/database/server/auction/mac-book.json",
                     "lod/Staging/net9.0/activity/mac.jsonl"):
            self.assertNotIn(name, after)
        self.assertEqual(after["lod/database/server/templates/item.json"], "new template data")
        self.assertEqual(after["lod/database/server/community/Boards/news.json"], "cloud board")  # 자료는 덧쓰기만
        self.assertEqual(after["lod/database/assets/tile.bin"], "new asset data")
        self.assertEqual(after["lod-bot/app/Lod.CompanionBot.dll"], "new Lod.CompanionBot build")
        self.assertEqual(after["lod-bot/world/map1.txt"], "new map data")
        self.assertEqual(after["lod-bot/world/notes.dat"], "cloud only")
        self.assertNotIn("lod-bot/world/readme.md", after)
        self.assertEqual(after["lod-proxy/app/Lod.HuntProxy.dll"], "new Lod.HuntProxy build")
        self.assertEqual(after["lod-eco/app/Lod.EcoBots.dll"], "new Lod.EcoBots build")
        self.assertEqual(after["lod-eco/ml/export-activity.py"], "new export script")
        # 옛 코드는 .prev 에 남아 손으로도 되돌릴 수 있다.
        self.assertEqual((self.remote / "lod/Staging/net9.0.prev/Lorule.GameServer.dll").read_text(), "old-server")
        # 빌드가 덮어쓴 맥 서버 설정은 되돌려 둔다.
        self.assertEqual((self.root / FORK / "Staging/net9.0/LoruleConfig.json").read_text(), "MAC CONFIG")
        log = self.systemctl_log()
        self.assertLess(log.index("stop lod"), log.index("start lod"))
        self.assertIn("start lod-bot@1", log)
        self.assertTrue((self.state / "ready").exists())
        self.assert_only_fake_host()

    def assert_untouched(self, result):
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(tree(self.remote), self.before)
        self.assertFalse([line for line in self.systemctl_log() if line.split()[0] in {"stop", "start", "restart"}])
        self.assertIn("그대로입니다", result.stderr)

    def test_build_failure_touches_nothing_remote(self):
        result = self.run_script("cloud-server.sh", "deploy", FAKE_BUILD_FAIL="1")
        self.assert_untouched(result)
        self.assertFalse((self.state / "ssh-hosts").exists(), "빌드가 실패했는데 원격에 접속했다")
        self.assertEqual((self.root / FORK / "Staging/net9.0/LoruleConfig.json").read_text(), "MAC CONFIG")

    def test_transfer_failure_leaves_live_folders_and_services_alone(self):
        # 마지막 코드 전송(생태계 봇)이 끊긴다 — 앞의 것들은 이미 .next 에 올라갔다.
        result = self.run_script("cloud-server.sh", "deploy", FAKE_FAIL_RSYNC="lod-eco/app.next")
        self.assert_untouched(result)

    def assert_rolled_back(self, result, written=None):
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(tree(self.remote), {**self.before, **(written or {})})  # 코드는 옛것, live 자료는 그대로(그사이 쓴 것까지)
        self.assertIn("되돌", result.stderr)
        log = self.systemctl_log()
        self.assertEqual([line for line in log if line in ("stop lod", "start lod", "restart lod")][-1], "start lod")
        self.assertTrue((self.state / "ready").exists(), "되돌린 옛 서버가 다시 뜨지 않았다")
        self.assertIn("start lod-bot@1", log)

    def test_failure_while_switching_restores_prev(self):
        # 반영 도중(생태계 봇 폴더) 실패 — 서버·자료·봇·대리 폴더는 이미 새것으로 바뀐 뒤다.
        self.assert_rolled_back(self.run_script("cloud-server.sh", "deploy", FAKE_FAIL_RSYNC="lod-eco/app.next/ "))

    def test_start_failure_restores_prev(self):
        self.assert_rolled_back(self.run_script("cloud-server.sh", "deploy", FAKE_SERVER_DLL="nostart-server"))

    def test_check_failure_restores_prev(self):
        # 준비가 안 된 새 서버가 그사이 쓴 게시판 글도 되돌림 뒤에 남는다.
        self.assert_rolled_back(self.run_script("cloud-server.sh", "deploy", FAKE_SERVER_DLL="notready-server"),
                                written={"lod/database/server/community/Boards/new-post.json": "written while new\n"})


class SecretPassingTests(FakeCloudCase):
    """리뷰 13 — 비밀번호는 shell 글자에 끼우지 않는다. 따옴표·역슬래시·공백·한글이 그대로 도착해야 한다."""

    def test_companion_bot_password_reaches_the_cloud_file_verbatim(self):
        (self.remote / "lod-bot/companion-bot-1.json").unlink()
        secret = self.mac_home / "bot-password.txt"
        secret.write_text(PASSWORD + "\n", encoding="utf-8")
        result = self.run_script("cloud-server.sh", "bot-config", LOD_BOT_PASSWORD_FILE=str(secret))
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        first = json.loads((self.remote / "lod-bot/companion-bot-1.json").read_text(encoding="utf-8"))
        self.assertEqual((first["Name"], first["Password"]), ("동료사제", PASSWORD))
        second = json.loads((self.remote / "lod-bot/companion-bot-2.json").read_text(encoding="utf-8"))
        self.assertEqual((second["Name"], second["Password"]), ("동료사제2", PASSWORD))
        self.assertEqual(first["MapFolder"], "world")
        self.assertEqual((self.remote / "lod-bot/app/Lod.CompanionBot.dll").read_text(), "new Lod.CompanionBot build")
        self.assert_only_fake_host()

    def test_dashboard_password_reaches_credential_and_htpasswd_verbatim(self):
        result = self.run_script("cloud-dashboard.sh", "password", PASSWORD)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((self.remote / "lod-ops/data/credential").read_text(encoding="utf-8"), f"lod-admin:{PASSWORD}\n")
        self.assertEqual((self.state / "htpasswd.stdin").read_text(encoding="utf-8"), PASSWORD + "\n")
        saved = self.mac_home / "LOD-backups/cloud/ability-ops-credentials.txt"
        self.assertEqual(saved.read_text(encoding="utf-8"), f"lod-admin:{PASSWORD}\n")
        self.assertNotIn(PASSWORD, result.stdout + result.stderr)
        self.assert_only_fake_host()


class DashboardDeployTests(FakeCloudCase):
    """리뷰 10 — 대시보드 동기화가 앱 release 가 만든 버전 파일을 지우지 않는다."""

    def test_app_version_files_survive_a_dashboard_deploy(self):
        result = self.run_script("cloud-dashboard.sh", "deploy")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        after = tree(self.remote)
        self.assertEqual(after["lod-ops/www/download/version-ios.txt"], "42")
        self.assertEqual(after["lod-ops/www/download/version-windows.txt"], "7")
        self.assertEqual(after["lod-ops/www/index.html"], "new dashboard page")
        self.assertNotIn("lod-ops/www/stale.html", after)  # 나머지는 지금처럼 맥과 똑같이
        self.assert_only_fake_host()


if __name__ == "__main__":
    unittest.main()
