#!/bin/bash
# 클라우드(Oracle Cloud 무료 ARM · Ubuntu) 에서 도는 서버를 맥에서 다룬다.
#
#   scripts/cloud-server.sh setup     처음 한 번: .NET 9 · 방화벽 · 자동 실행 · 날마다 백업, 그리고 캐릭터까지 올린다
#   scripts/cloud-server.sh deploy    서버 실행 파일과 자료만 다시 올리고 서버를 새로 띄운다(캐릭터는 클라우드 것을 둔다)
#   scripts/cloud-server.sh status|logs [줄수]|restart
#   scripts/cloud-server.sh backup    클라우드의 캐릭터를 맥(~/LOD-backups/cloud)으로 받아 온다
#   scripts/cloud-server.sh app       앱 주소(server.cfg)를 클라우드로 — 맥 서버로 돌아가려면 lod-server.sh config
#
# 주소는 LOD_CLOUD_IP(공인 IP) 하나. 열쇠는 ~/.ssh/lod_oracle. 올린 뒤로는 **클라우드의 캐릭터가 진짜**다 —
# deploy 는 캐릭터(database/server/aislings)를 덮지 않는다.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FORK="$ROOT/sources/wren11/Dark-Ages-Private-Server"
IP="${LOD_CLOUD_IP:?LOD_CLOUD_IP=<공인 IP> 를 붙여 주세요}"
KEY="$HOME/.ssh/lod_oracle"
HOST="ubuntu@$IP"
REMOTE=/home/ubuntu/lod          # 클라우드 쪽 FORK
SSH=(ssh -i "$KEY" -o StrictHostKeyChecking=accept-new "$HOST")

remote() { "${SSH[@]}" "$@"; }

# 설정 두 개를 클라우드 경로·공인 IP 로 만들어 함께 올린다(맥의 Staging 설정은 건드리지 않는다).
upload() {
    local conf
    conf="$(mktemp -d)"
    for pair in "LoruleConfig.template.json:LoruleConfig.json" "MServerTable.template.xml:MServerTable.xml"; do
        sed -e "s|{{FORK}}|$REMOTE|g" -e "s|{{SERVER_IP}}|$IP|g" "$ROOT/scripts/server-config/${pair%%:*}" > "$conf/${pair##*:}"
    done
    "$ROOT/scripts/check-server-config.sh" "$conf"

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
    # 처음 한 번만 맥의 캐릭터를 올린다.
    rsync -az --partial --timeout=60 -e "ssh -i $KEY" "$FORK/database/server/aislings" "$HOST:$REMOTE/database/server/"
    restart
    app
}

# 앱이 클라우드로 붙게 한다(맥 서버로 돌아가려면 scripts/lod-server.sh config). 앱을 다시 설치해야 반영된다.
app() {
    echo "$IP:2610" > "$ROOT/mobile/client/server.cfg"
    echo "앱 주소(server.cfg) — $IP:2610 · scripts/ios-build.sh install 로 다시 설치"
}

restart() {
    remote 'sudo systemctl restart lod'
    for _ in $(seq 1 60); do
        if remote 'ss -ltn | grep -q ":2610 " && ss -ltn | grep -q ":2615 "'; then
            echo "켰습니다 — $IP · 로그인 2610 · 게임 2615"
            return
        fi
        sleep 1
    done
    echo "서버가 포트를 안 엽니다. 기록:" >&2
    logs 30 >&2
    return 1
}

logs() { remote "journalctl -u lod -n ${1:-40} --no-pager"; }

backup() {
    local dir="$HOME/LOD-backups/cloud" name="aislings-$(date +%Y%m%d-%H%M).tar.gz"
    mkdir -p "$dir"
    remote "tar -czf - -C $REMOTE/database/server aislings" > "$dir/$name"
    echo "받았습니다 — $dir/$name"
}

case "${1:-status}" in
    setup) setup ;;
    deploy) upload; restart ;;
    restart) restart ;;
    status) remote 'systemctl is-active lod; ss -ltn | grep -E ":(2610|2615) "' ;;
    logs) logs "${2:-40}" ;;
    backup) backup ;;
    app) app ;;
    *) echo "쓸 수 있는 것: setup deploy restart status logs backup app"; exit 2 ;;
esac
