#!/bin/bash
# 클라우드(Oracle Cloud · Ubuntu · x86_64 AMD EPYC 2 vCPU · 11GB, 2026-10-06 nproc 확인) 에서 도는 서버를 맥에서 다룬다.
#
#   scripts/ops/cloud-server.sh setup     처음 한 번: .NET 9 · 방화벽 · 자동 실행 · 날마다 백업, 그리고 캐릭터까지 올린다
#   scripts/ops/cloud-server.sh deploy    서버를 빌드하고 봇까지 모두 만든 뒤 운영 폴더 옆 .next 로 올려 한 번에 바꾼다 —
#                                         새 서버가 안 뜨면 옛 판(.prev)으로 되돌린다(캐릭터는 클라우드 것을 둔다)
#   scripts/ops/cloud-server.sh status|logs [줄수]|restart
#   scripts/ops/cloud-server.sh backup    클라우드의 캐릭터를 맥(~/LOD-backups/cloud)으로 받아 온다
#   scripts/ops/cloud-server.sh app       앱 주소(server.cfg)를 클라우드로 — 맥 서버로 돌아가려면 lod-server.sh config
#   scripts/ops/cloud-server.sh bot-config  동료 봇 설정 파일을 클라우드에 만든다(비밀번호를 여기서 묻고 클라우드에만 적는다)
#   scripts/ops/cloud-server.sh bot         동료 봇만 올리고 다시 켠다(게임 서버는 그대로 — 접속한 사람이 안 끊긴다)
#   scripts/ops/cloud-server.sh bot-logs [줄수] [봇번호]   동료 봇 기록(줄마다 [봇 이름]) — 파일 기록은 클라우드 ~/lod-bot/logs/
#   scripts/ops/cloud-server.sh proxy       대신 사냥 대리 프로그램만 올리고 다시 켠다(게임 서버는 그대로)
#   scripts/ops/cloud-server.sh proxy-logs [줄수]   대신 사냥 기록 — 파일 기록은 클라우드 ~/lod-proxy/logs/
#   scripts/ops/cloud-server.sh eco-config  생태계 봇 설정 파일을 클라우드에 만든다(비밀번호를 여기서 묻고 클라우드에만, 이미 있으면 그대로)
#   scripts/ops/cloud-server.sh eco         생태계 봇 프로그램만 올리고 다시 켠다(게임 서버는 그대로)
#   scripts/ops/cloud-server.sh eco-logs [줄수]     생태계 봇 기록(5분마다 요약) — 사건 기록(머신러닝)은 클라우드 ~/lod-eco/eco/
#   scripts/ops/cloud-server.sh ml-pull     학습용 사본(~/lod-ml — 활동 기록 가명·IP 뺌 + 봇 사건)을 맥 ~/LOD-backups/ml 로 받는다
#   scripts/ops/cloud-server.sh auction-logs [줄수]  오늘 경매장 사건 기록(database/server/auction/events-날짜.jsonl)
#   scripts/ops/cloud-server.sh auction-report      경매장 보고 — 사건 수(사람·봇), 봇 올림 상한, 끊긴 조작, 금화 흐름(scripts/ops/auction-report.py)
#
# 동료 봇(성직자, mobile/bots/Lod.CompanionBot)은 서버와 같은 기계에서 봇마다 lod-bot@1~5 로 돈다(2026-09-27 — 다섯까지).
# N 번째 봇 = 서버 설정 CompanionBots 의 N 번째 이름, 설정은 클라우드의 ~/lod-bot/companion-bot-N.json(비밀번호, 여기에만) —
# 그 파일이 없으면 lod-bot@N 은 뜨지 않는다. deploy 가 봇 프로그램과 맵 벽 파일(앱의 map*.txt)도 올린다.
#
# 주소는 LOD_CLOUD_IP(공인 IP) 하나. 열쇠는 ~/.ssh/lod_oracle. 올린 뒤로는 **클라우드의 캐릭터가 진짜**다 —
# deploy 는 캐릭터(database/server/aislings)와 경매장(database/server/auction)을 덮지 않는다.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FORK="$ROOT/sources/wren11/Dark-Ages-Private-Server"
IP="${LOD_CLOUD_IP:?LOD_CLOUD_IP=<공인 IP> 를 붙여 주세요}"
KEY="$HOME/.ssh/lod_oracle"
HOST="ubuntu@$IP"
REMOTE=/home/ubuntu/lod          # 클라우드 쪽 FORK
BOT_REMOTE=/home/ubuntu/lod-bot  # 동료 봇: app/(프로그램) · world/(맵 벽) · companion-bot-N.json(비밀번호, 여기에만) · logs/
BOT_PROJECT="$ROOT/mobile/bots/Lod.CompanionBot"
PROXY_REMOTE=/home/ubuntu/lod-proxy  # 대신 사냥: app/ · jobs/(서버가 쓰는 작업 파일, 0700) · hunt-proxy.json(비밀 없음) · logs/
PROXY_PROJECT="$ROOT/mobile/bots/Lod.HuntProxy"
ECO_REMOTE=/home/ubuntu/lod-eco      # 생태계 봇: app/ · eco-bots.json(비밀번호, 여기에만) · eco/(사건 기록) · logs/ · ml/(내보내기 스크립트)
ECO_PROJECT="$ROOT/mobile/bots/Lod.EcoBots"
ML_REMOTE=/home/ubuntu/lod-ml        # 학습용 사본: activity/ · eco/ · salt(비밀 소금, 여기에만)
SSH=(ssh -i "$KEY" -o StrictHostKeyChecking=accept-new "$HOST")
NEXT=""   # deploy 때만 .next — 운영 폴더 옆에 올리고, 바꾸는 것은 마지막 전환(switch_script) 한 번
BUILT=""  # deploy 때 미리 만든 봇·대리·생태계 프로그램 폴더

remote() { "${SSH[@]}" "$@"; }

# 맥 → 원격 폴더 $1(나머지는 rsync 인자와 원본). deploy 때는 옆의 .next 로 — 안 바뀐 파일은 운영 폴더와 hardlink 라 빠르고 자리도 안 먹는다.
send() {
    local to=$1
    shift
    rsync -az --partial --timeout=60 -e "ssh -i $KEY" ${NEXT:+"--link-dest=$to"} "$@" "$HOST:$to$NEXT/"
}

# 봇·대리·생태계 프로그램을 맥에서 만든다 — $1 프로젝트 → $2 폴더.
publish() {
    DOTNET_ROOT="$ROOT/.tools/dotnet-9.0.317" "$ROOT/.tools/dotnet-9.0.317/dotnet" publish "$1" \
        -c Release -o "$2" -p:UseAppHost=false --nologo -v quiet >/dev/null
}

# 서버를 지금 소스로 빌드한다(결과는 Staging/net9.0). 빌드가 맥 서버 설정 두 개를 덮어쓰므로 빌드 뒤(실패해도) 되돌려 둔다.
server_build() {
    local keep status=0
    keep="$(mktemp -d)"
    cp -p "$FORK/Staging/net9.0/LoruleConfig.json" "$FORK/Staging/net9.0/MServerTable.xml" "$keep/" 2>/dev/null || true
    DOTNET_ROOT="$ROOT/.tools/dotnet-9.0.317" "$ROOT/.tools/dotnet-9.0.317/dotnet" build \
        "$FORK/src/Lorule.GameServer/Lorule.GameServer.csproj" --nologo -v q || status=$?
    cp -p "$keep/"* "$FORK/Staging/net9.0/" 2>/dev/null || true
    rm -rf "$keep"
    return "$status"
}

# 이 판이 무엇인지 — 서버 폴더에 같이 올라간다(deploy-manifest.json): 루트·서버 커밋, 커밋 안 한 변경, SDK, 만든 시각.
manifest() {
    local root_dirty=false fork_dirty=false
    [ -z "$(git -C "$ROOT" status --porcelain --untracked-files=no --ignore-submodules=all 2>/dev/null)" ] || root_dirty=true
    [ -z "$(git -C "$FORK" status --porcelain --untracked-files=no 2>/dev/null)" ] || fork_dirty=true
    printf '{\n  "root": "%s",\n  "rootDirty": %s,\n  "server": "%s",\n  "serverDirty": %s,\n  "sdk": "%s",\n  "builtAt": "%s"\n}\n' \
        "$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo unknown)" "$root_dirty" \
        "$(git -C "$FORK" rev-parse HEAD 2>/dev/null || echo unknown)" "$fork_dirty" \
        "$(DOTNET_ROOT="$ROOT/.tools/dotnet-9.0.317" "$ROOT/.tools/dotnet-9.0.317/dotnet" --version)" "$(date -u +%FT%TZ)"
}

# 설정 두 개를 클라우드 경로·공인 IP 로 만들어 함께 올린다(맥의 Staging 설정은 건드리지 않는다).
upload() {
    local conf
    conf="$(mktemp -d)"
    for pair in "LoruleConfig.template.json:LoruleConfig.json" "MServerTable.template.xml:MServerTable.xml"; do
        sed -e "s|{{FORK}}|$REMOTE|g" -e "s|{{SERVER_IP}}|$IP|g" "$ROOT/scripts/ops/server-config/${pair%%:*}" > "$conf/${pair##*:}"
    done
    "$ROOT/scripts/ops/check-server-config.sh" "$conf"
    manifest > "$conf/deploy-manifest.json"

    remote "mkdir -p $REMOTE/Staging/net9.0$NEXT $REMOTE/database$NEXT"
    # 기록 파일(Hades_*.txt)은 맥 것이라 올리지 않는다. archives(414MB)는 서버가 읽지 않는다.
    send "$REMOTE/Staging/net9.0" --delete --exclude 'Hades_*.txt' --exclude 'activity/' --exclude 'LoruleConfig.json' --exclude 'MServerTable.xml' \
        "$FORK/Staging/net9.0/"
    send "$REMOTE/Staging/net9.0" "$conf/"
    # 경매장(auction/)도 캐릭터처럼 클라우드 것이 진짜다 — 맥에서 서버를 띄워 생긴 auction/ 이 올라가면 클라우드 경매를 덮는다.
    # .next 는 맥과 똑같이 맞추고(--delete), 운영 자리에는 지금처럼 덧쓰기만 한다(switch_script).
    send "$REMOTE/database" ${NEXT:+--delete} --exclude 'aislings/' --exclude 'auction/' "$FORK/database/server" "$FORK/database/assets"
    rm -rf "$conf"

    # 끊겼다 이어 올릴 때 rsync 가 남긴 조각(.이름.XXXXXX)을 치운다 — 빈 조각 하나가 메타파일 읽기를 깨뜨렸다.
    remote "find $REMOTE/Staging/net9.0$NEXT $REMOTE/database$NEXT -name '.*.??????' -type f -delete"
}

# 봇 이름 — 서버 설정 CompanionBots 차례대로. lod-bot@N 은 N 번째 이름으로 접속한다.
bot_names() {
    python3 - "$ROOT/scripts/ops/server-config/LoruleConfig.template.json" <<'PY'
import re, sys
text = open(sys.argv[1], encoding="utf-8").read()
listed = re.search(r'"CompanionBots"\s*:\s*\[([^\]]*)\]', text).group(1)
print("\n".join(re.findall(r'"([^"]+)"', listed)))
PY
}

# 설정 파일이 있는 봇마다(lod-bot@N) 무엇을 한다 — 없으면 예전 하나짜리 lod-bot.
BOT_EACH='shopt -s nullglob; bots=(); for f in /home/ubuntu/lod-bot/companion-bot-*.json; do n=${f##*-}; bots+=("lod-bot@${n%.json}"); done; [ ${#bots[@]} -gt 0 ] || bots=(lod-bot)'

# 동료 봇 — 프로그램(.NET, 서버와 같은 런타임)과 맵 벽 파일을 올리고 lod-bot@ 서비스를 깐다. 여러 번 해도 같다.
bot_upload() {
    local out="$BUILT/bot"
    [ -n "$BUILT" ] || { out="$(mktemp -d)"; publish "$BOT_PROJECT" "$out"; }

    remote "mkdir -p $BOT_REMOTE/app$NEXT $BOT_REMOTE/world$NEXT"
    send "$BOT_REMOTE/app" --delete --exclude 'companion-bot*.json' "$out/"
    send "$BOT_REMOTE/world" --delete --include 'map*.txt' --include 'guide.txt' --include 'eco-grounds.txt' --include 'class-kit.txt' --exclude '*' \
        "$ROOT/mobile/client/assets/world/"
    [ -n "$BUILT" ] || rm -rf "$out"

    remote 'bash -s' <<'SH'
set -euo pipefail
sudo tee /etc/systemd/system/lod-bot@.service >/dev/null <<UNIT
[Unit]
Description=LOD companion bot %i (priest)
After=lod.service
# 비밀번호가 든 설정 파일이 있어야 뜬다 — scripts/ops/cloud-server.sh bot-config
ConditionPathExists=/home/ubuntu/lod-bot/companion-bot-%i.json

[Service]
User=ubuntu
WorkingDirectory=/home/ubuntu/lod-bot
Environment=DOTNET_ROOT=/opt/dotnet
ExecStart=/opt/dotnet/dotnet /home/ubuntu/lod-bot/app/Lod.CompanionBot.dll /home/ubuntu/lod-bot/companion-bot-%i.json
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
UNIT
sudo systemctl daemon-reload

shopt -s nullglob
numbered=(/home/ubuntu/lod-bot/companion-bot-*.json)
for f in "${numbered[@]}"; do
    n=${f##*-}
    sudo systemctl enable "lod-bot@${n%.json}" >/dev/null 2>&1
done

# 예전 하나짜리(lod-bot · companion-bot.json)는 번호 붙은 설정이 생기면 끈다 — 같은 계정이 둘 접속하지 않게.
if [ ${#numbered[@]} -gt 0 ] && systemctl list-unit-files lod-bot.service >/dev/null 2>&1; then
    sudo systemctl disable --now lod-bot >/dev/null 2>&1 || true
fi
SH
}

# 봇 설정 파일(companion-bot-N.json)을 클라우드에 만든다 — 이미 있는 것은 그대로 둔다. 비밀번호는 맥의
# ~/LOD-backups/companion-bot-password.txt(LOD_BOT_PASSWORD_FILE 로 바꿈)에서 읽고, 없으면 여기서 한 번 묻는다 —
# 저장소에는 남기지 않는다. 예전 하나짜리 companion-bot.json 은 1번(companion-bot-1.json)으로 이름만 바꾼다.
# 계정이 없으면 봇이 처음 접속할 때 성직자로 만든다(BotLogin).
bot_config() {
    local file="${LOD_BOT_PASSWORD_FILE:-$HOME/LOD-backups/companion-bot-password.txt}" password n=0 name
    if [ -f "$file" ]; then
        password="$(head -n 1 "$file")"
    else
        read -r -s -p "새 봇 계정 비밀번호(모든 봇 같게): " password
        echo
    fi
    [ -n "$password" ] || { echo "비밀번호가 비었습니다." >&2; return 1; }

    remote "mkdir -p $BOT_REMOTE && cd $BOT_REMOTE && if [ -f companion-bot.json ] && [ ! -f companion-bot-1.json ]; then mv companion-bot.json companion-bot-1.json; fi"

    while IFS= read -r name; do
        n=$((n + 1))
        # JSON 은 serializer 로 만든다 — 따옴표·역슬래시가 든 비밀번호도 그대로, 값은 환경변수 → ssh stdin 으로만 간다.
        # 먼저 만들어 두고 된 것만 보낸다 — 만들다 실패하면 원격에 빈 설정이 남아 다음에 「이미 있습니다」로 건너뛰었다.
        json="$(BOT_NAME="$name" BOT_PASSWORD="$password" python3 -c 'import json, os; print(json.dumps({"Host": "127.0.0.1", "LoginPort": 2610, "Name": os.environ["BOT_NAME"], "Password": os.environ["BOT_PASSWORD"], "MapFolder": "world", "HealOwnerPercent": 70, "HealSelfPercent": 50}, ensure_ascii=False, indent=2))')"
        printf '%s\n' "$json" | remote "cd $BOT_REMOTE && umask 077 && if [ -f companion-bot-$n.json ]; then cat >/dev/null; echo '$n 번 봇 설정은 이미 있습니다 — 그대로 둡니다.'; else cat > companion-bot-$n.json; echo '$n 번 봇($name) 설정을 적었습니다.'; fi"
    done < <(bot_names)

    bot_upload
    bot_restart
}

bot_restart() {
    remote "$BOT_EACH; sudo systemctl restart \"\${bots[@]}\""
}

# 대신 사냥 대리 — 프로그램을 올리고 lod-proxy 서비스 하나를 깐다. 맵 벽·출구(guide.txt)는 봇의 world/ 를 같이 쓴다(bot_upload).
proxy_upload() {
    local out="$BUILT/proxy"
    [ -n "$BUILT" ] || { out="$(mktemp -d)"; publish "$PROXY_PROJECT" "$out"; }

    remote "mkdir -p $PROXY_REMOTE/app$NEXT $PROXY_REMOTE/jobs && chmod 700 $PROXY_REMOTE/jobs"
    send "$PROXY_REMOTE/app" --delete --exclude 'hunt-proxy.json' "$out/"
    [ -n "$BUILT" ] || rm -rf "$out"

    remote 'bash -s' <<'SH'
set -euo pipefail
[ -f /home/ubuntu/lod-proxy/hunt-proxy.json ] || cat > /home/ubuntu/lod-proxy/hunt-proxy.json <<JSON
{
  "Host": "127.0.0.1",
  "LoginPort": 2610,
  "JobFolder": "jobs",
  "MapFolder": "/home/ubuntu/lod-bot/world",
  "Max": 10
}
JSON
sudo tee /etc/systemd/system/lod-proxy.service >/dev/null <<UNIT
[Unit]
Description=LOD hunt proxy (대신 사냥)
After=lod.service

[Service]
User=ubuntu
WorkingDirectory=/home/ubuntu/lod-proxy
Environment=DOTNET_ROOT=/opt/dotnet
ExecStart=/opt/dotnet/dotnet /home/ubuntu/lod-proxy/app/Lod.HuntProxy.dll /home/ubuntu/lod-proxy/hunt-proxy.json
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
UNIT
sudo systemctl daemon-reload
sudo systemctl enable lod-proxy >/dev/null 2>&1
SH
}

proxy_restart() {
    remote "sudo systemctl restart lod-proxy"
}

# 생태계 봇 — 프로그램 하나가 봇 여럿(설계 autopilot/eco-bots/). 맵 벽·guide.txt·eco-grounds.txt·class-kit.txt 는 봇의 world/ 를 같이 쓴다(bot_upload).
# 설정 파일(eco-bots.json)이 없으면 서비스가 뜨지 않는다 — eco-config. 날마다 4시 30분 학습용 사본을 만든다(cron).
eco_upload() {
    local out="$BUILT/eco"
    [ -n "$BUILT" ] || { out="$(mktemp -d)"; publish "$ECO_PROJECT" "$out"; }

    remote "mkdir -p $ECO_REMOTE/app$NEXT $ECO_REMOTE/ml$NEXT $ML_REMOTE && chmod 700 $ML_REMOTE"
    send "$ECO_REMOTE/app" --delete --exclude 'eco-bots.json' "$out/"
    send "$ECO_REMOTE/ml" "$ROOT/scripts/ml/export-activity.py"
    [ -n "$BUILT" ] || rm -rf "$out"

    remote 'bash -s' <<'SH'
set -euo pipefail
[ -f /home/ubuntu/lod-ml/salt ] || (umask 077 && head -c 32 /dev/urandom | base64 > /home/ubuntu/lod-ml/salt)
sudo tee /etc/systemd/system/lod-eco.service >/dev/null <<UNIT
[Unit]
Description=LOD eco bots (생태계 봇)
After=lod.service
# 비밀번호가 든 설정 파일이 있어야 뜬다 — scripts/ops/cloud-server.sh eco-config
ConditionPathExists=/home/ubuntu/lod-eco/eco-bots.json

[Service]
User=ubuntu
WorkingDirectory=/home/ubuntu/lod-eco
Environment=DOTNET_ROOT=/opt/dotnet
ExecStart=/opt/dotnet/dotnet /home/ubuntu/lod-eco/app/Lod.EcoBots.dll /home/ubuntu/lod-eco/eco-bots.json
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
UNIT
sudo systemctl daemon-reload
sudo systemctl enable lod-eco >/dev/null 2>&1
( { crontab -l 2>/dev/null || true; } | { grep -v lod-ml || true; }; echo '30 4 * * * python3 /home/ubuntu/lod-eco/ml/export-activity.py --activity /home/ubuntu/lod/Staging/net9.0/activity --eco /home/ubuntu/lod-eco/eco --out /home/ubuntu/lod-ml --salt /home/ubuntu/lod-ml/salt --config /home/ubuntu/lod/Staging/net9.0/LoruleConfig.json >/dev/null # lod-ml' ) | crontab -
SH
}

eco_restart() {
    remote "sudo systemctl restart lod-eco"
}

# 생태계 봇 설정 — 이름·직업은 서버 설정 EcoBots(이름 앞: 전사봇 1 · 도적봇 2 · 사제봇 4 · 무도봇 5, 사제봇은 파티 성직자 — 목록 앞에 둬 MaxOnline 안에 든다), 비밀번호는 맥 ~/LOD-backups/eco-bot-password.txt
# (LOD_ECO_PASSWORD_FILE 로 바꿈)에서 읽고 없으면 한 번 묻는다. 저장소에는 남기지 않는다. 이미 있으면 그대로 둔다.
eco_config() {
    local file="${LOD_ECO_PASSWORD_FILE:-$HOME/LOD-backups/eco-bot-password.txt}" password
    if [ -f "$file" ]; then
        password="$(tr -d '\r\n' < "$file")"
    else
        read -r -s -p "생태계 봇 계정 비밀번호(모든 봇 같게): " password
        echo
    fi

    ECO_PASSWORD="$password" python3 - "$ROOT/scripts/ops/server-config/LoruleConfig.template.json" <<'PY' | remote "mkdir -p $ECO_REMOTE && cd $ECO_REMOTE && umask 077 && if [ -f eco-bots.json ]; then cat >/dev/null; echo '생태계 봇 설정은 이미 있습니다 — 그대로 둡니다.'; else cat > eco-bots.json; echo '생태계 봇 설정을 적었습니다.'; fi"
import json, os, re, sys
text = open(sys.argv[1], encoding="utf-8").read()
names = re.findall(r'"([^"]+)"', re.search(r'"EcoBots"\s*:\s*\[([^\]]*)\]', text).group(1))
path = {"전사봇": 1, "도적봇": 2, "사제봇": 4, "무도봇": 5}
print(json.dumps({
    "Host": "127.0.0.1", "LoginPort": 2610, "MapFolder": "/home/ubuntu/lod-bot/world", "Password": os.environ["ECO_PASSWORD"], "MaxOnline": 40,
    "Bots": [{"Name": n, "Path": path[n[:3]]} for n in sorted(names, key=lambda n: path[n[:3]] != 4)],
}, ensure_ascii=False, indent=2))
PY
    bot_upload
    eco_upload
    eco_restart
}

# 날마다 새벽 4시 캐릭터·경매장 백업, 30개만 둔다. setup·deploy 가 깐다(여러 번 해도 같다). 경매장 폴더가 아직 없으면 캐릭터만.
backup_cron() {
    remote 'bash -s' <<'SH'
mkdir -p /home/ubuntu/backups
( { crontab -l 2>/dev/null || true; } | { grep -v lod-backup || true; }; echo '0 4 * * * tar --ignore-failed-read -czf /home/ubuntu/backups/aislings-$(date +\%Y\%m\%d-\%H\%M).tar.gz -C /home/ubuntu/lod/database/server aislings auction && ls -1t /home/ubuntu/backups/aislings-*.tar.gz | tail -n +31 | xargs -r rm -- # lod-backup' ) | crontab -
SH
}

setup() {
    remote 'bash -s' <<'SH'
set -euo pipefail
sudo timedatectl set-timezone Asia/Seoul
sudo apt-get update -qq
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq libicu-dev rsync iptables-persistent curl cron >/dev/null

if [ ! -x /opt/dotnet/dotnet ]; then
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    sudo bash /tmp/dotnet-install.sh --runtime dotnet --channel 9.0 --install-dir /opt/dotnet
fi

# Oracle 의 Ubuntu 는 22 번 말고 모두 막아 둔다 — 로그인 2610 · 게임 2615 를 연다.
for port in 2610 2615; do
    sudo iptables -C INPUT -p tcp --dport $port -j ACCEPT 2>/dev/null || sudo iptables -I INPUT 1 -p tcp --dport $port -j ACCEPT
done
sudo netfilter-persistent save >/dev/null

# 서버 코드와 자료가 맥(대소문자 안 가림)에서 자라 이름의 대소문자가 섞여 있다(community/boards ↔ Boards,
# notification.txt ↔ Notification.txt …). 게임 폴더를 대소문자를 안 가리는 ext4(casefold) 위에 둔다.
if ! mountpoint -q /home/ubuntu/lod-ci; then
    [ -f /home/ubuntu/lod-ci.img ] || { sudo fallocate -l 6G /home/ubuntu/lod-ci.img; sudo mkfs.ext4 -q -F -O casefold -E encoding=utf8 /home/ubuntu/lod-ci.img; }
    mkdir -p /home/ubuntu/lod-ci
    sudo mount -o loop /home/ubuntu/lod-ci.img /home/ubuntu/lod-ci
    grep -q lod-ci.img /etc/fstab || echo '/home/ubuntu/lod-ci.img /home/ubuntu/lod-ci ext4 loop,defaults 0 2' | sudo tee -a /etc/fstab >/dev/null
fi
[ -d /home/ubuntu/lod-ci/lod ] || { sudo mkdir /home/ubuntu/lod-ci/lod; sudo chattr +F /home/ubuntu/lod-ci/lod; }
sudo chown ubuntu:ubuntu /home/ubuntu/lod-ci /home/ubuntu/lod-ci/lod
[ -e /home/ubuntu/lod ] || ln -s /home/ubuntu/lod-ci/lod /home/ubuntu/lod

sudo tee /etc/systemd/system/lod.service >/dev/null <<UNIT
[Unit]
Description=LOD Hades game server
After=network-online.target

[Service]
User=ubuntu
WorkingDirectory=/home/ubuntu/lod/Staging/net9.0
Environment=DOTNET_ROOT=/opt/dotnet
ExecStart=/opt/dotnet/dotnet Lorule.GameServer.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
UNIT

sudo systemctl daemon-reload
sudo systemctl enable lod >/dev/null
SH
    backup_cron

    upload
    bot_upload
    # 처음 한 번만 맥의 캐릭터를 올린다.
    rsync -az --partial --timeout=60 -e "ssh -i $KEY" "$FORK/database/server/aislings" "$HOST:$REMOTE/database/server/"
    restart
    app
}

# 앱이 클라우드로 붙게 한다(맥 서버로 돌아가려면 scripts/ops/lod-server.sh config). 앱을 다시 설치해야 반영된다.
app() {
    echo "$IP:2610" > "$ROOT/mobile/client/server.cfg"
    echo "앱 주소(server.cfg) — $IP:2610 · scripts/ops/ios-build.sh install 로 다시 설치"
}

restart() {
    remote 'sudo systemctl restart lod'
    for _ in $(seq 1 60); do
        if remote 'ss -ltn | grep -q ":2610 " && ss -ltn | grep -q ":2615 "'; then
            echo "켰습니다 — $IP · 로그인 2610 · 게임 2615"
            # 서버가 새로 뜨면 봇도 다시 붙게 한다(설정 파일이 없으면 systemd 가 조건으로 건너뛴다).
            bot_restart || true
            proxy_restart || true
            eco_restart || true
            return
        fi
        sleep 1
    done
    echo "서버가 포트를 안 엽니다. 기록:" >&2
    logs 30 >&2
    return 1
}

logs() { remote "journalctl -u lod -n ${1:-40} --no-pager"; }

# 봇 기록 — 줄마다 [봇 이름]. 봇 번호를 주면 그 봇만.
bot_logs() {
    if [ -n "${2:-}" ]; then
        remote "journalctl -u lod-bot@$2 -n ${1:-40} --no-pager"
    else
        remote "journalctl -u 'lod-bot*' -n ${1:-40} --no-pager"
    fi
}

# 원격 전환 — deploy 가 .next 를 모두 올린 뒤 한 번 돌린다. 인자: 서버·동료 봇·대리·생태계 폴더.
# 정지 → 코드 폴더마다 .prev 로 보존(hardlink) → .next 반영 → 서버 시작 → 확인. 반영·시작·확인이 실패하면 .prev 로 되돌려 다시 켠다.
# 거르는 것은 맥에서 올릴 때의 exclude 그대로라 live 자료(캐릭터·경매·활동 기록·Hades_*.txt)는 옮기지도 지우지도 않는다.
# 봇 설정(companion-bot-N.json · hunt-proxy.json · jobs/ · eco-bots.json · eco-unlisted.json · eco/)은 코드 폴더 밖이라 닿지 않는다.
# 확인은 포트 2610·2615 + 이번 기동의 서버 기록 "Game server is online." 까지다 — 계정으로 한 번 들어가 보고 끝나는
# 도구가 저장소에 없어 실제 로그인은 보지 않는다(로그인 단계에서만 나는 고장은 못 잡는다).
switch_script() {
    cat <<'SH'
set -uo pipefail
# 맥과 연결이 끊겨도(덮개 닫힘 등) 전환·되돌림은 끝까지 간다 — 반쯤 바꾼 채 멈추지 않게. 끊긴 뒤 안내는 못 받는다.
trap '' HUP PIPE
R=$1 BOT=$2 PROXY=$3 ECO=$4
SLOTS="$R/Staging/net9.0 $R/database $BOT/app $BOT/world $PROXY/app $ECO/app $ECO/ml"
bots=""
for f in "$BOT"/companion-bot-*.json; do [ -e "$f" ] || continue; n=${f##*-}; bots="$bots lod-bot@${n%.json}"; done
HELPERS="${bots:-lod-bot} lod-proxy lod-eco"

# put 칸 원본 대상 [rsync 인자…] — 칸마다 live 자료를 거른다(설정 두 파일은 지금처럼 .next 의 새 것이 간다).
put() {
    local slot=$1 from=$2 to=$3
    shift 3
    case "$slot" in
        "$R/Staging/net9.0") set -- "$@" --exclude 'Hades_*.txt' --exclude 'activity/' ;;
        "$R/database") set -- "$@" --exclude 'aislings/' --exclude 'auction/' ;;
        "$BOT/app") set -- "$@" --exclude 'companion-bot*.json' ;;
        "$BOT/world") set -- "$@" --include 'map*.txt' --include 'guide.txt' --include 'eco-grounds.txt' --include 'class-kit.txt' --exclude '*' ;;
        "$PROXY/app") set -- "$@" --exclude 'hunt-proxy.json' ;;
        "$ECO/app") set -- "$@" --exclude 'eco-bots.json' ;;
    esac
    mkdir -p "$to" && rsync -a "$@" "$from/" "$to/"
}

# 서버를 켜고 기다린다 — 포트 둘이 열리고 이번 기동의 준비 줄(격리 서버 시험과 같은 신호)이 보이면 성공.
# 옛 restart 는 ssh 왕복 포함 80초쯤 기다렸다 — 맵을 다 읽기 전에 되돌리지 않게 넉넉히 120초.
up() {
    local since
    since=$(date +%s)
    sudo systemctl start lod || return 1
    for _ in $(seq 1 120); do
        if ss -ltn | grep -q ":2610 " && ss -ltn | grep -q ":2615 " \
            && journalctl -u lod --since "@$since" --no-pager 2>/dev/null | grep -q "Game server is online."; then
            return 0
        fi
        sleep 1
    done
    return 1
}

# 봇·대리·생태계 — 설정 파일이 없으면 systemd 가 조건으로 건너뛴다. 실패는 restart 처럼 넘긴다.
helpers() {
    for unit in $HELPERS; do sudo systemctl "$1" "$unit" >/dev/null 2>&1 || true; done
}

rollback() {
    echo "$1 — 옛 판(.prev)으로 되돌립니다." >&2
    sudo systemctl stop lod || true
    for s in $SLOTS; do
        case "$s" in
            # 새 판이 더한 스크립트는 지워야 옛 서버가 컴파일된다(--delete). 서버가 쓰는 게시판은 그동안 쓴 글을 지우지 않게 거른다.
            "$R/database") put "$s" "$s.prev" "$s" --delete --exclude 'community/' --exclude 'Community/' || echo "되돌리지 못한 폴더: $s" >&2 ;;
            *) put "$s" "$s.prev" "$s" --delete || echo "되돌리지 못한 폴더: $s" >&2 ;;
        esac
    done
    if up; then
        helpers start
        echo "옛 판으로 되돌려 다시 켰습니다. 배포는 실패입니다 — 기록: cloud-server.sh logs" >&2
    else
        echo "되돌린 옛 판도 켜지지 않습니다 — cloud-server.sh logs 로 기록을 보세요." >&2
    fi
    exit 1
}

main() {
    # 한 번에 하나만 — 두 배포가 같은 .prev 를 겹쳐 쓰지 않게.
    exec 9>/tmp/lod-deploy.lock
    flock -n 9 || { echo "다른 배포가 바꾸는 중입니다 — 아무것도 바꾸지 않았습니다." >&2; exit 1; }
    for s in $SLOTS; do
        [ -d "$s.next" ] || { echo "올라온 새 판이 없습니다($s.next) — 아무것도 바꾸지 않았습니다." >&2; exit 1; }
    done
    helpers stop
    if ! sudo systemctl stop lod; then
        helpers start
        echo "서버를 멈추지 못했습니다 — 아무것도 바꾸지 않았습니다." >&2
        exit 1
    fi
    for s in $SLOTS; do
        if ! put "$s" "$s" "$s.prev" --delete --link-dest="$s"; then
            up && helpers start
            echo "옛 판을 .prev 로 보존하지 못했습니다 — 아무것도 바꾸지 않고 다시 켰습니다." >&2
            exit 1
        fi
    done
    for s in $SLOTS; do
        case "$s" in
            # 자료와 ml 은 지금처럼 덧쓰기만 — 클라우드에만 있는 파일(게시판 등)을 지우지 않는다.
            "$R/database"|"$ECO/ml") put "$s" "$s.next" "$s" || rollback "새 판을 반영하지 못했습니다($s)" ;;
            *) put "$s" "$s.next" "$s" --delete || rollback "새 판을 반영하지 못했습니다($s)" ;;
        esac
    done
    up || rollback "새 서버가 뜨지 않거나 포트·준비 줄이 보이지 않습니다"
    helpers start
    echo "켰습니다 — 로그인 2610 · 게임 2615. 옛 판은 각 폴더 옆 .prev 에 있습니다."
}

# 함수 안에서 돌려야 bash -s 가 나머지 스크립트를 다 읽은 뒤 시작한다(중간 명령이 stdin 을 먹지 않게).
main
SH
}

# 배포 — ① 맥에서 모두 만든다 ② 운영 폴더 옆 .next 로만 올린다 ③ 원격에서 한 번에 바꾼다(switch_script).
# ①·② 가 실패하면 운영 폴더·서비스는 그대로, ③ 이 실패하면 .prev 로 되돌아간다.
deploy() {
    BUILT="$(mktemp -d)"
    trap 'status=$?; rm -rf "$BUILT"; [ "$status" -eq 0 ] || [ -n "${SWITCHING:-}" ] || echo "배포를 멈췄습니다 — 운영 폴더·서비스는 그대로입니다(올라간 것은 .next 에만)." >&2' EXIT
    server_build
    publish "$BOT_PROJECT" "$BUILT/bot"
    publish "$PROXY_PROJECT" "$BUILT/proxy"
    publish "$ECO_PROJECT" "$BUILT/eco"

    NEXT=.next
    upload
    bot_upload
    proxy_upload
    eco_upload
    backup_cron

    echo "모두 올렸습니다 — 바꿉니다(접속한 사람이 끊깁니다)."
    SWITCHING=1
    switch_script | remote "bash -s -- $REMOTE $BOT_REMOTE $PROXY_REMOTE $ECO_REMOTE"
}

backup() {
    local dir="$HOME/LOD-backups/cloud" name="aislings-$(date +%Y%m%d-%H%M).tar.gz"
    mkdir -p "$dir"
    remote "tar --ignore-failed-read -czf - -C $REMOTE/database/server aislings auction" > "$dir/$name"
    echo "받았습니다 — $dir/$name"
}

case "${1:-status}" in
    setup) setup ;;
    deploy) deploy ;;
    restart) restart ;;
    bot) bot_upload; bot_restart ;;
    proxy) bot_upload; proxy_upload; proxy_restart ;;
    eco) bot_upload; eco_upload; eco_restart ;;
    eco-config) eco_config ;;
    eco-logs) remote "journalctl -u lod-eco -n ${2:-40} --no-pager" ;;
    auction-logs) remote "tail -n ${2:-40} $REMOTE/database/server/auction/events-\$(date -u +%F).jsonl 2>/dev/null || echo '오늘 경매 기록이 없습니다'" ;;
    auction-report) remote "python3 - $REMOTE/database/server/auction $REMOTE/Staging/net9.0/LoruleConfig.json" < "$ROOT/scripts/ops/auction-report.py" ;;
    ml-pull) mkdir -p "$HOME/LOD-backups/ml"; rsync -az --timeout=60 -e "ssh -i $KEY" --exclude salt "$HOST:$ML_REMOTE/" "$HOME/LOD-backups/ml/"; echo "받았습니다 — $HOME/LOD-backups/ml" ;;
    proxy-logs) remote "journalctl -u lod-proxy -n ${2:-40} --no-pager" ;;
    status) remote "systemctl is-active lod; $BOT_EACH; for b in \"\${bots[@]}\"; do echo \"\$b: \$(systemctl is-active \$b)\"; done; echo \"lod-proxy: \$(systemctl is-active lod-proxy)\"; echo \"lod-eco: \$(systemctl is-active lod-eco)\"; ps -C dotnet -o pcpu=,rss=,args= | sed 's|/opt/dotnet/dotnet ||'; ss -ltn | grep -E ':(2610|2615) '" ;;
    logs) logs "${2:-40}" ;;
    backup) backup ;;
    app) app ;;
    bot-config) bot_config ;;
    bot-logs) bot_logs "${2:-40}" "${3:-}" ;;
    *) echo "쓸 수 있는 것: setup deploy restart status logs backup app bot-config bot-logs proxy proxy-logs eco eco-config eco-logs ml-pull auction-logs auction-report"; exit 2 ;;
esac
