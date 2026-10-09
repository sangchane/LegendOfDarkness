#!/bin/bash
# 기술·마법 운영 대시보드를 클라우드에 올린다. 게임 서버는 재시작하지 않는다.
#
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh setup
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh deploy|backup|status|logs|credentials|cert
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh release [ios|windows|android]   맥의 최신 앱 파일을 내려받기 페이지(/download/)에 올린다
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh member-password <새것>   손님(내려받기·보기만) 비밀번호
#   LOD_CLOUD_IP=... scripts/ops/cloud-dashboard.sh nginx    nginx 설정만 다시 깐다(페이지·서비스는 그대로) — 내려받기 파일 종류를 늘렸을 때
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
    # 앱 번호(download/version-*.txt)는 release 가 클라우드에서만 만든다 — 지우면 옛 앱이 새 판을 못 알아챈다.
    rsync -az --partial --timeout=60 --delete -e "ssh -i $KEY" --exclude 'download/version-*.txt' \
        "$ROOT/docs/" "$HOST:$REMOTE/www/"
    rsync -az --partial --timeout=60 -e "ssh -i $KEY" \
        "$ROOT/scripts/ops/ability-ops-service.py" "$ROOT/scripts/ops/activity_store.py" "$ROOT/data/game-data/ability-operations.json" \
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
    remote "sudo systemctl restart lod-ability-ops && sudo systemctl reload nginx"
    save_credentials
    echo "대시보드 준비 완료 — https://$IP/?view=abilities"
}

# nginx 는 HTTPS 만 맡고 모든 요청을 운영 서비스로 넘긴다. 로그인은 서비스가 페이지 안에서 받는다(쿠키) —
# 브라우저 Basic 팝업을 쓰지 않는다(사용자 2026-09-30). setup·deploy 둘 다 부른다.
nginx_site() {
    remote "bash -s" <<'SH'
set -euo pipefail
sudo tee /etc/nginx/conf.d/lod-activity.conf >/dev/null <<'LOG'
log_format lod_activity escape=json '{"id":"$request_id","at":"$time_iso8601","ip":"$remote_addr","agent":"$http_user_agent","path":"$uri","method":"$request_method","status":$status,"bytes":$body_bytes_sent}';
LOG
sudo touch /var/log/nginx/lod-activity.jsonl
sudo chown www-data:adm /var/log/nginx/lod-activity.jsonl
sudo chmod 640 /var/log/nginx/lod-activity.jsonl
sudo tee /etc/logrotate.d/lod-activity >/dev/null <<'ROTATE'
/var/log/nginx/lod-activity.jsonl {
    daily
    rotate 90
    missingok
    notifempty
    nocompress
    create 0640 www-data adm
    sharedscripts
    postrotate
        /usr/sbin/nginx -s reopen
    endscript
}
ROTATE
sudo mkdir -p /etc/systemd/system/lod-ability-ops.service.d /etc/systemd/system/lod.service.d
sudo tee /etc/systemd/system/lod-ability-ops.service.d/activity.conf >/dev/null <<'UNIT'
[Service]
SupplementaryGroups=adm
ReadWritePaths=/home/ubuntu/lod-activity
Environment="LOD_WEB_ACTIVITY=/var/log/nginx/lod-activity.jsonl*"
Environment="LOD_GAME_ACTIVITY=/home/ubuntu/lod-activity/*.jsonl"
Environment=LOD_CHARACTER_DIR=/home/ubuntu/lod/database/server/aislings
Environment=LOD_SERVER_CONFIG=/home/ubuntu/lod/Staging/net9.0/LoruleConfig.json
UNIT
mkdir -p /home/ubuntu/lod-activity
chmod 700 /home/ubuntu/lod-activity
sudo tee /etc/systemd/system/lod.service.d/activity.conf >/dev/null <<'UNIT'
[Service]
Environment=LOD_ACTIVITY_DIR=/home/ubuntu/lod-activity
UNIT
sudo systemctl daemon-reload
sudo tee /etc/nginx/sites-available/lod-ops >/dev/null <<'NGINX'
server {
    listen 443 ssl;
    server_name _;
    access_log /var/log/nginx/lod-activity.jsonl lod_activity;
    access_log /var/log/nginx/access.log combined;
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

    # 앱 내려받기 — 안내는 대시보드 「앱 내려받기」 탭(사용자 2026-10-09 통합), 파일은 로그인(관리자·손님 비밀번호)해야 받는다.
    # nginx 가 요청마다 운영 서비스에 쿠키를 묻고(/api/signed-in 204·401), 아니면 로그인 화면으로 보냈다가 돌아오게 한다.
    # 앱 파일(아이폰 .ipa·윈도우 .zip·안드로이드 .apk)은 release 가 올린 lod-ops/release/, manifest.plist 틀은 docs/download/.
    location = /_signed_in {
        internal;
        proxy_pass http://127.0.0.1:8787/api/signed-in;
        proxy_pass_request_body off;
        # 여기서 하나라도 적으면 server 의 proxy_set_header 를 물려받지 않는다 — 횟수 제한이 보는 X-Real-IP 를 다시 적는다.
        proxy_set_header Content-Length "";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Original-URI $request_uri;
    }
    location @login { return 302 /login.html?next=$request_uri; }
    # 옛 공개 페이지 주소 — 앱의 「새 판」 단추가 아직 이리로 연다.
    location = /download { return 302 /?view=download; }
    location = /download/ { return 302 /?view=download; }
    # 「내 아이폰에 설치」는 사파리가 아니라 아이폰 시스템이 쿠키 없이 manifest·.ipa 를 받는다 — 주소에 단 표(?ota=)로 연다.
    location = /download/LodClient.ipa {
        auth_request /_signed_in;
        error_page 401 = @login;
        alias /home/ubuntu/lod-ops/release/LodClient.ipa;
        default_type application/octet-stream;
        add_header Content-Disposition 'attachment; filename="LodClient.ipa"' always;
        add_header Cache-Control no-cache always;
        add_header X-Content-Type-Options nosniff always;
    }
    location = /download/LodClient-windows.zip {
        auth_request /_signed_in;
        error_page 401 = @login;
        alias /home/ubuntu/lod-ops/release/LodClient-windows.zip;
        default_type application/octet-stream;
        add_header Content-Disposition 'attachment; filename="LodClient-windows.zip"' always;
        add_header Cache-Control no-cache always;
        add_header X-Content-Type-Options nosniff always;
    }
    # 안드로이드는 파일 종류를 .apk 로 알려야 크롬이 「설치 파일」로 받는다.
    location = /download/LodClient.apk {
        auth_request /_signed_in;
        error_page 401 = @login;
        alias /home/ubuntu/lod-ops/release/LodClient.apk;
        types { }
        default_type application/vnd.android.package-archive;
        add_header Content-Disposition 'attachment; filename="LodClient.apk"' always;
        add_header Cache-Control no-cache always;
        add_header X-Content-Type-Options nosniff always;
    }
    # 등록된 기기가 사파리에서 바로 설치하는 안내서 — 아이폰은 xml 로 받아야 읽는다.
    location = /download/manifest.plist {
        proxy_pass http://127.0.0.1:8787/api/ota-manifest$is_args$args;
    }
    # 앱이 켜질 때 쿠키 없이 새 판 번호를 묻는다(AppUpdate) — 로그인 없이 둔다(정규식이라 아래 /download/ 보다 먼저 잡힌다).
    location ~ ^/download/(version-[a-z]+\.txt)$ {
        alias /home/ubuntu/lod-ops/www/download/$1;
        add_header Cache-Control no-cache always;
        add_header X-Content-Type-Options nosniff always;
    }
    location /download/ {
        auth_request /_signed_in;
        error_page 401 = @login;
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
    rsync -az --timeout=60 --exclude '*credential' -e "ssh -i $KEY" "$HOST:$REMOTE/data/" "$out/"
    echo "관리 페이지 값 백업 — $out"
    ls -la "$out"
}

deploy() {
    upload
    nginx_site
    remote "sudo systemctl restart lod-ability-ops && sudo systemctl reload nginx"
    echo "대시보드 갱신 완료 — https://$DOMAIN"
}

# 내려받기 페이지(/download/)의 앱 파일을 맥의 최신판으로 바꾼다 — release [ios|windows|android]. 다 올린 뒤 이름을 바꿔,
# 받는 중인 사람에게 반쪽 파일이 가지 않는다. ios-build.sh install(성공 뒤)·windows-build.sh release·android-build.sh release 가 부른다.
release() {
    local file
    case "${1:-ios}" in
        ios) file="$ROOT/mobile/client/build/ios/LodClient.ipa" ;;
        windows) file="$ROOT/mobile/client/build/windows/LodClient-windows.zip" ;;
        android) file="$ROOT/mobile/client/build/android/LodClient.apk" ;;
        *) echo "release ios|windows|android" >&2; exit 2 ;;
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
    # 앱 번호 — 옛 앱이 켜질 때 이것을 보고 새로 받으라고 알린다(AppUpdate). 앱 파일을 다 올린 뒤에 바꾼다.
    if [ -s "$(dirname "$file")/version.txt" ]; then
        rsync -az --timeout=60 -e "ssh -i $KEY" "$(dirname "$file")/version.txt" "$HOST:$REMOTE/www/download/version-${1:-ios}.txt"
        remote "chmod 644 $REMOTE/www/download/version-${1:-ios}.txt"
    fi
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
    # 비밀번호는 원격 명령 글자에 끼우지 않고 stdin 으로 넘긴다 — 따옴표·역슬래시·공백이 있어도 그대로 도착한다.
    printf '%s\n' "$new" | remote 'set -euo pipefail
IFS= read -r password
umask 077
# nginx 쪽이 되고 나서 운영 API 쪽 — 거꾸로면 htpasswd 가 실패했을 때 둘이 다른 비밀번호가 된다.
printf "%s\n" "$password" | sudo htpasswd -i /etc/nginx/lod-ops.htpasswd lod-admin >/dev/null
printf "lod-admin:%s\n" "$password" > /home/ubuntu/lod-ops/data/credential
sudo systemctl restart lod-ability-ops'
    save_credentials
}

# 손님 비밀번호 — 내려받기·보기만(사용자 2026-10-09). 관리자 비밀번호와 따로 서비스 옆 member-credential 에.
#   LOD_CLOUD_IP=… scripts/ops/cloud-dashboard.sh member-password <새비밀번호>
set_member_password() {
    printf '%s\n' "$1" | remote 'set -euo pipefail
IFS= read -r password
umask 077
# 관리자 비밀번호와 같으면 로그인이 관리자로 먼저 맞춰 손님이 관리자가 된다.
if [ "$password" = "$(cut -d: -f2- /home/ubuntu/lod-ops/data/credential)" ]; then echo "관리자 비밀번호와 달라야 합니다" >&2; exit 1; fi
printf "member:%s\n" "$password" > /home/ubuntu/lod-ops/data/member-credential
sudo systemctl restart lod-ability-ops'
    echo "손님 비밀번호를 바꿨습니다 — 손님 로그인은 모두 풀립니다."
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
    member-password) set_member_password "${2:?새 손님 비밀번호를 붙여 주세요}" ;;
    release) release "${2:-ios}" ;;
    nginx) nginx_site; remote "sudo nginx -t && sudo systemctl reload nginx" ;;
    *) echo "쓸 수 있는 것: setup deploy backup status logs credentials cert password member-password release nginx"; exit 2 ;;
esac
