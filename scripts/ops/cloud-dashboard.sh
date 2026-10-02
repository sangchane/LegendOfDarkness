#!/bin/bash
# 기술·마법 운영 대시보드를 클라우드에 올린다. 게임 서버는 재시작하지 않는다.
#
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh setup
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh deploy|backup|status|logs|credentials|cert
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh release [ios|windows]   맥의 최신 앱 파일을 내려받기 페이지(/download/)에 올린다
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IP="${LOD_CLOUD_IP:?LOD_CLOUD_IP=<공인 IP> 를 붙여 주세요}"
KEY="$HOME/.ssh/lod_oracle"
HOST="ubuntu@$IP"
DOMAIN="${LOD_OPS_DOMAIN:-lodgame.duckdns.org}"  # 무료 인증서 주소(DuckDNS, 사용자 2026-09-27)
REMOTE=/home/ubuntu/lod-ops
BACKUP_DIR="$HOME/LOD-backups/cloud"
LOCAL_CREDENTIAL="$BACKUP_DIR/ability-ops-credentials.txt"
SSH=(ssh -i "$KEY" -o StrictHostKeyChecking=accept-new "$HOST")

remote() { "${SSH[@]}" "$@"; }

upload() {
    remote "mkdir -p $REMOTE/www $REMOTE/app $REMOTE/data"
    rsync -az --partial --timeout=60 --delete -e "ssh -i $KEY" \
        "$ROOT/docs/" "$HOST:$REMOTE/www/"
    rsync -az --partial --timeout=60 -e "ssh -i $KEY" \
        "$ROOT/scripts/ops/ability-ops-service.py" "$ROOT/data/game-data/ability-operations.json" \
        "$HOST:$REMOTE/app/"
}

setup() {
    upload
    remote "LOD_OPS_IP='$IP' bash -s" <<'SH'
set -euo pipefail
REMOTE=/home/ubuntu/lod-ops
: "${LOD_OPS_IP:?}"

sudo apt-get update -qq
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq nginx apache2-utils openssl iptables-persistent >/dev/null
sudo usermod -aG ubuntu www-data
mkdir -p "$REMOTE/data"

if [ ! -s "$REMOTE/data/credential" ]; then
    umask 077
    printf 'lod-admin:%s\n' "$(openssl rand -base64 27 | tr -d '\n=/+')" > "$REMOTE/data/credential"
fi
chmod 600 "$REMOTE/data/credential"
if [ ! -s "$REMOTE/data/ability-presentation-overrides.json" ]; then
    printf '%s\n' '{"version":1,"revision":0,"updatedAt":null,"abilities":{}}' > "$REMOTE/data/ability-presentation-overrides.json"
fi
chmod 600 "$REMOTE/data/ability-presentation-overrides.json"

password="$(cut -d: -f2- "$REMOTE/data/credential")"
sudo htpasswd -bc /etc/nginx/lod-ops.htpasswd lod-admin "$password" >/dev/null
sudo chmod 640 /etc/nginx/lod-ops.htpasswd
sudo chown root:www-data /etc/nginx/lod-ops.htpasswd

if [ ! -s /etc/ssl/private/lod-ops.key ]; then
    sudo openssl req -x509 -nodes -newkey rsa:2048 -days 825 \
        -keyout /etc/ssl/private/lod-ops.key -out /etc/ssl/certs/lod-ops.crt \
        -subj "/CN=LOD operations" -addext "subjectAltName=IP:$LOD_OPS_IP" >/dev/null 2>&1
fi

sudo tee /etc/systemd/system/lod-ability-ops.service >/dev/null <<UNIT
[Unit]
Description=LOD ability presentation operations API
After=network-online.target

[Service]
User=ubuntu
WorkingDirectory=$REMOTE/app
ExecStart=/usr/bin/python3 $REMOTE/app/ability-ops-service.py --root $REMOTE/www --catalog $REMOTE/app/ability-operations.json --overrides $REMOTE/data/ability-presentation-overrides.json --password-file $REMOTE/data/credential --bind 127.0.0.1 --port 8787
Restart=always
RestartSec=3
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=$REMOTE/data

[Install]
WantedBy=multi-user.target
UNIT

# 다음 게임 서버 재시작부터 저장 즉시 다시 읽는 같은 파일을 쓴다. 여기서는 게임을 재시작하지 않는다.
sudo mkdir -p /etc/systemd/system/lod.service.d
sudo tee /etc/systemd/system/lod.service.d/ability-operations.conf >/dev/null <<UNIT
[Service]
Environment=LOD_ABILITY_OVERRIDES=$REMOTE/data/ability-presentation-overrides.json
UNIT


sudo iptables -C INPUT -p tcp --dport 443 -j ACCEPT 2>/dev/null || sudo iptables -I INPUT 1 -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save >/dev/null
sudo systemctl daemon-reload
sudo systemctl enable --now lod-ability-ops nginx >/dev/null
sudo systemctl restart nginx
SH
    nginx_site
    save_credentials
    echo "대시보드 준비 완료 — https://$IP/?view=abilities"
}

# nginx 는 HTTPS 만 맡고 모든 요청을 운영 서비스로 넘긴다. 로그인은 서비스가 페이지 안에서 받는다(쿠키) —
# 브라우저 Basic 팝업을 쓰지 않는다(사용자 2026-09-30). setup·deploy 둘 다 부른다.
nginx_site() {
    remote "bash -s" <<'SH'
set -euo pipefail
sudo tee /etc/nginx/sites-available/lod-ops >/dev/null <<'NGINX'
server {
    listen 443 ssl;
    server_name _;
    ssl_certificate /etc/ssl/certs/lod-ops.crt;
    ssl_certificate_key /etc/ssl/private/lod-ops.key;
    ssl_protocols TLSv1.2 TLSv1.3;
    add_header X-Content-Type-Options nosniff always;
    add_header Referrer-Policy no-referrer always;
    client_max_body_size 300k;

    proxy_http_version 1.1;
    proxy_set_header Authorization $http_authorization;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-Proto https;

    # 주소만 쳐도 기술·마법 화면으로(사용자 2026-09-27 "?view=abilities 붙여야 해?").
    location = / {
        if ($arg_view = "") { return 302 /?view=abilities; }
        proxy_pass http://127.0.0.1:8787;
    }

    location / {
        proxy_pass http://127.0.0.1:8787;
    }

    # 멀리 있는 사람이 앱을 받는 페이지 — 로그인 없이 nginx 가 바로 준다(사용자 2026-10-02).
    # 페이지는 docs/download/(www 로 올라감), 앱 파일(아이폰 .ipa·윈도우 .zip)은 release 가 올린 lod-ops/release/.
    location = /download { return 301 /download/; }
    location ~ ^/download/(LodClient\.ipa|LodClient-windows\.zip)$ {
        alias /home/ubuntu/lod-ops/release/$1;
        default_type application/octet-stream;
        add_header Content-Disposition 'attachment; filename="$1"' always;
        add_header Cache-Control no-cache always;
        add_header X-Content-Type-Options nosniff always;
    }
    # 등록된 기기가 사파리에서 바로 설치하는 안내서 — 아이폰은 xml 로 받아야 읽는다.
    location = /download/manifest.plist {
        alias /home/ubuntu/lod-ops/www/download/manifest.plist;
        types { }
        default_type application/xml;
        add_header Cache-Control no-cache always;
    }
    location /download/ {
        alias /home/ubuntu/lod-ops/www/download/;
        add_header Cache-Control no-cache always;
        add_header X-Content-Type-Options nosniff always;
    }
}
NGINX
sudo rm -f /etc/nginx/sites-enabled/default
sudo ln -sfn /etc/nginx/sites-available/lod-ops /etc/nginx/sites-enabled/lod-ops
sudo nginx -t 2>&1 | tail -1
SH
}

# 페이지에서 바꾼 값(연출·아이템 이름·바꾼 기록 changes.jsonl)을 맥으로 받는다. 비밀번호 파일은 빼고.
backup() {
    local out="$BACKUP_DIR/ops-data-$(date +%Y%m%d-%H%M%S)"
    mkdir -p "$out"
    rsync -az --timeout=60 --exclude credential -e "ssh -i $KEY" "$HOST:$REMOTE/data/" "$out/"
    echo "관리 페이지 값 백업 — $out"
    ls -la "$out"
}

deploy() {
    upload
    nginx_site
    remote "sudo systemctl restart lod-ability-ops && sudo systemctl reload nginx"
    echo "대시보드 갱신 완료 — https://$DOMAIN"
}

# 내려받기 페이지(/download/)의 앱 파일을 맥의 최신판으로 바꾼다 — release [ios|windows]. 다 올린 뒤 이름을 바꿔,
# 받는 중인 사람에게 반쪽 파일이 가지 않는다. ios-build.sh install(성공 뒤)·windows-build.sh release 가 부른다.
release() {
    local file
    case "${1:-ios}" in
        ios) file="$ROOT/mobile/client/build/ios/LodClient.ipa" ;;
        windows) file="$ROOT/mobile/client/build/windows/LodClient-windows.zip" ;;
        *) echo "release ios|windows" >&2; exit 2 ;;
    esac
    [ -s "$file" ] || { echo "앱 파일이 없습니다 — $file" >&2; exit 1; }
    local name
    name="$(basename "$file")"
    remote "mkdir -p $REMOTE/release && chmod 755 $REMOTE/release"
    rsync -az --timeout=120 -e "ssh -i $KEY" "$file" "$HOST:$REMOTE/release/$name.uploading"
    remote "chmod 644 $REMOTE/release/$name.uploading && mv -f $REMOTE/release/$name.uploading $REMOTE/release/$name"
    # 페이지(docs/download/)도 늘 같이 올린다 — 앱만 바뀌고 안내가 옛것으로 남지 않게(사용자 2026-10-02).
    remote "mkdir -p $REMOTE/www/download"
    rsync -az --timeout=60 -e "ssh -i $KEY" "$ROOT/docs/download/" "$HOST:$REMOTE/www/download/"
    echo "내려받기 페이지 갱신 — https://$DOMAIN/download/ ($name $(du -h "$file" | cut -f1))"
}

# 무료 정식 인증서(Let's Encrypt) — 사용자 2026-09-27: 주소 lodgame.duckdns.org(DuckDNS, IP 161.33.43.117 고정).
# 80 번은 닫혀 있어 443 하나로 받는 TLS-ALPN 방식(acme.sh --alpn)을 쓴다 — 받는 몇 초만 nginx 를 멈춘다(게임 서버는 그대로).
# nginx 가 읽는 자리(/etc/ssl/…/lod-ops.*)에 그대로 깔아 setup 의 자체 서명과 설정을 바꾸지 않는다. 갱신은 acme.sh 의 root cron 이 한다.
cert() {
    remote "LOD_OPS_DOMAIN='$DOMAIN' bash -s" <<'SH'
set -euo pipefail
: "${LOD_OPS_DOMAIN:?}"
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq socat >/dev/null
if ! sudo test -x /root/.acme.sh/acme.sh; then
    curl -fsSL https://raw.githubusercontent.com/acmesh-official/acme.sh/master/acme.sh | sudo sh -s -- --install-online >/dev/null
fi
ACME="sudo /root/.acme.sh/acme.sh"
$ACME --set-default-ca --server letsencrypt >/dev/null
# 이미 받은 인증서가 유효하면 acme.sh 가 2 를 돌려준다 — 실패가 아니다.
$ACME --issue --alpn -d "$LOD_OPS_DOMAIN" \
    --pre-hook "systemctl stop nginx" --post-hook "systemctl start nginx" || [ $? -eq 2 ]
$ACME --install-cert -d "$LOD_OPS_DOMAIN" \
    --key-file /etc/ssl/private/lod-ops.key --fullchain-file /etc/ssl/certs/lod-ops.crt \
    --reloadcmd "systemctl reload nginx" >/dev/null
sudo openssl x509 -in /etc/ssl/certs/lod-ops.crt -noout -subject -issuer -enddate
SH
    echo "인증서 — https://$DOMAIN/?view=abilities"
}

# 비밀번호 바꾸기 — 운영 API(credential)와 nginx(htpasswd)를 함께. 사용자 2026-09-27 "비밀번호가 너무 길다".
#   LOD_CLOUD_IP=… scripts/ops/cloud-dashboard.sh password [새비밀번호]   (없으면 소문자·숫자 10자로 만든다)
set_password() {
    local new="${1:-$(LC_ALL=C tr -dc 'a-z2-9' </dev/urandom | head -c 10)}"
    remote "LOD_OPS_PASSWORD='$new' bash -s" <<'SH'
set -euo pipefail
REMOTE=/home/ubuntu/lod-ops
umask 077
printf 'lod-admin:%s\n' "$LOD_OPS_PASSWORD" > "$REMOTE/data/credential"
sudo htpasswd -b /etc/nginx/lod-ops.htpasswd lod-admin "$LOD_OPS_PASSWORD" >/dev/null 2>&1
sudo systemctl restart lod-ability-ops
SH
    save_credentials
}

save_credentials() {
    mkdir -p "$BACKUP_DIR"
    umask 077
    remote "cat $REMOTE/data/credential" > "$LOCAL_CREDENTIAL"
    chmod 600 "$LOCAL_CREDENTIAL"
    echo "로그인 정보 — $LOCAL_CREDENTIAL (화면에는 비밀번호를 출력하지 않음)"
}

case "${1:-status}" in
    setup) setup ;;
    deploy) deploy ;;
    backup) backup ;;
    status) remote "systemctl is-active lod-ability-ops nginx; ss -ltn | grep -E ':(443|8787) '" ;;
    logs) remote "journalctl -u lod-ability-ops -n ${2:-50} --no-pager" ;;
    credentials) save_credentials ;;
    cert) cert ;;
    password) set_password "${2:-}" ;;
    release) release "${2:-ios}" ;;
    *) echo "쓸 수 있는 것: setup deploy backup status logs credentials cert password release"; exit 2 ;;
esac
