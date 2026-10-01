#!/bin/bash
# 클라우드(Oracle Cloud 무료 ARM · Ubuntu) 에서 도는 서버를 맥에서 다룬다.
#
#   scripts/ops/cloud-server.sh setup     처음 한 번: .NET 9 · 방화벽 · 자동 실행 · 날마다 백업, 그리고 캐릭터까지 올린다
#   scripts/ops/cloud-server.sh deploy    서버 실행 파일과 자료만 다시 올리고 서버를 새로 띄운다(캐릭터는 클라우드 것을 둔다)
#   scripts/ops/cloud-server.sh status|logs [줄수]|restart
#   scripts/ops/cloud-server.sh backup    클라우드의 캐릭터를 맥(~/LOD-backups/cloud)으로 받아 온다
#   scripts/ops/cloud-server.sh app       앱 주소(server.cfg)를 클라우드로 — 맥 서버로 돌아가려면 lod-server.sh config
#   scripts/ops/cloud-server.sh bot-config  동료 봇 설정 파일을 클라우드에 만든다(비밀번호를 여기서 묻고 클라우드에만 적는다)
#   scripts/ops/cloud-server.sh bot-logs [줄수] [봇번호]   동료 봇 기록(줄마다 [봇 이름]) — 파일 기록은 클라우드 ~/lod-bot/logs/
#
# 동료 봇(성직자, mobile/bots/Lod.CompanionBot)은 서버와 같은 기계에서 봇마다 lod-bot@1~5 로 돈다(2026-09-27 — 다섯까지).
# N 번째 봇 = 서버 설정 CompanionBots 의 N 번째 이름, 설정은 클라우드의 ~/lod-bot/companion-bot-N.json(비밀번호, 여기에만) —
# 그 파일이 없으면 lod-bot@N 은 뜨지 않는다. deploy 가 봇 프로그램과 맵 벽 파일(앱의 map*.txt)도 올린다.
#
# 주소는 LOD_CLOUD_IP(공인 IP) 하나. 열쇠는 ~/.ssh/lod_oracle. 올린 뒤로는 **클라우드의 캐릭터가 진짜**다 —
# deploy 는 캐릭터(database/server/aislings)를 덮지 않는다.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FORK="$ROOT/sources/wren11/Dark-Ages-Private-Server"
IP="${LOD_CLOUD_IP:?LOD_CLOUD_IP=<공인 IP> 를 붙여 주세요}"
KEY="$HOME/.ssh/lod_oracle"
HOST="ubuntu@$IP"
REMOTE=/home/ubuntu/lod          # 클라우드 쪽 FORK
BOT_REMOTE=/home/ubuntu/lod-bot  # 동료 봇: app/(프로그램) · world/(맵 벽) · companion-bot-N.json(비밀번호, 여기에만) · logs/
BOT_PROJECT="$ROOT/mobile/bots/Lod.CompanionBot"
SSH=(ssh -i "$KEY" -o StrictHostKeyChecking=accept-new "$HOST")

remote() { "${SSH[@]}" "$@"; }

# 설정 두 개를 클라우드 경로·공인 IP 로 만들어 함께 올린다(맥의 Staging 설정은 건드리지 않는다).
upload() {
    local conf
    conf="$(mktemp -d)"
    for pair in "LoruleConfig.template.json:LoruleConfig.json" "MServerTable.template.xml:MServerTable.xml"; do
        sed -e "s|{{FORK}}|$REMOTE|g" -e "s|{{SERVER_IP}}|$IP|g" "$ROOT/scripts/ops/server-config/${pair%%:*}" > "$conf/${pair##*:}"
    done
    "$ROOT/scripts/ops/check-server-config.sh" "$conf"

    remote "mkdir -p $REMOTE/Staging/net9.0 $REMOTE/database"
    # 기록 파일(Hades_*.txt)은 맥 것이라 올리지 않는다. archives(414MB)는 서버가 읽지 않는다.
    rsync -az --partial --timeout=60 --delete -e "ssh -i $KEY" --exclude 'Hades_*.txt' --exclude 'LoruleConfig.json' --exclude 'MServerTable.xml' \
        "$FORK/Staging/net9.0/" "$HOST:$REMOTE/Staging/net9.0/"
    rsync -az --partial --timeout=60 -e "ssh -i $KEY" "$conf/" "$HOST:$REMOTE/Staging/net9.0/"
    rsync -az --partial --timeout=60 -e "ssh -i $KEY" --exclude 'aislings/' "$FORK/database/server" "$FORK/database/assets" "$HOST:$REMOTE/database/"
    rm -rf "$conf"

    # 끊겼다 이어 올릴 때 rsync 가 남긴 조각(.이름.XXXXXX)을 치운다 — 빈 조각 하나가 메타파일 읽기를 깨뜨렸다.
    remote "find $REMOTE -name '.*.??????' -type f -delete"
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
    local out
    out="$(mktemp -d)"
    DOTNET_ROOT="$ROOT/.tools/dotnet-9.0.317" "$ROOT/.tools/dotnet-9.0.317/dotnet" publish "$BOT_PROJECT" \
        -c Release -o "$out" -p:UseAppHost=false --nologo -v quiet >/dev/null

    remote "mkdir -p $BOT_REMOTE/app $BOT_REMOTE/world"
    rsync -az --partial --timeout=60 --delete -e "ssh -i $KEY" --exclude 'companion-bot*.json' "$out/" "$HOST:$BOT_REMOTE/app/"
    rsync -az --partial --timeout=60 --delete -e "ssh -i $KEY" --include 'map*.txt' --exclude '*' \
        "$ROOT/mobile/client/assets/world/" "$HOST:$BOT_REMOTE/world/"
    rm -rf "$out"

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
        printf '{\n  "Host": "127.0.0.1",\n  "LoginPort": 2610,\n  "Name": "%s",\n  "Password": "%s",\n  "MapFolder": "world",\n  "HealOwnerPercent": 70,\n  "HealSelfPercent": 50\n}\n' \
            "$name" "$password" | remote "cd $BOT_REMOTE && umask 077 && if [ -f companion-bot-$n.json ]; then cat >/dev/null; echo '$n 번 봇 설정은 이미 있습니다 — 그대로 둡니다.'; else cat > companion-bot-$n.json; echo '$n 번 봇($name) 설정을 적었습니다.'; fi"
    done < <(bot_names)

    bot_upload
    bot_restart
}

bot_restart() {
    remote "$BOT_EACH; sudo systemctl restart \"\${bots[@]}\""
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

# 날마다 새벽 4시 캐릭터 백업, 30개만 둔다.
mkdir -p /home/ubuntu/backups
( { crontab -l 2>/dev/null || true; } | { grep -v lod-backup || true; }; echo '0 4 * * * tar -czf /home/ubuntu/backups/aislings-$(date +\%Y\%m\%d-\%H\%M).tar.gz -C /home/ubuntu/lod/database/server aislings && ls -1t /home/ubuntu/backups/aislings-*.tar.gz | tail -n +31 | xargs -r rm -- # lod-backup' ) | crontab -
sudo systemctl daemon-reload
sudo systemctl enable lod >/dev/null
SH

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

backup() {
    local dir="$HOME/LOD-backups/cloud" name="aislings-$(date +%Y%m%d-%H%M).tar.gz"
    mkdir -p "$dir"
    remote "tar -czf - -C $REMOTE/database/server aislings" > "$dir/$name"
    echo "받았습니다 — $dir/$name"
}

case "${1:-status}" in
    setup) setup ;;
    deploy) upload; bot_upload; restart ;;
    restart) restart ;;
    status) remote "systemctl is-active lod; $BOT_EACH; for b in \"\${bots[@]}\"; do echo \"\$b: \$(systemctl is-active \$b)\"; done; ss -ltn | grep -E ':(2610|2615) '" ;;
    logs) logs "${2:-40}" ;;
    backup) backup ;;
    app) app ;;
    bot-config) bot_config ;;
    bot-logs) bot_logs "${2:-40}" "${3:-}" ;;
    *) echo "쓸 수 있는 것: setup deploy restart status logs backup app bot-config bot-logs"; exit 2 ;;
esac
