#!/bin/bash
# 서버를 켜고 끄고 살피는 한 곳. 설정을 다시 깔고, 캐릭터를 백업하고, 기록을 정리한다.
#
#   scripts/lod-server.sh config          설정 두 개를 틀에서 다시 만든다(빌드하면 덮어써진다)
#   scripts/lod-server.sh start|stop|restart|status
#   scripts/lod-server.sh logs [줄수]     기록 끝을 본다
#   scripts/lod-server.sh backup          캐릭터를 압축해 두고 오래된 것은 지운다
#   scripts/lod-server.sh install-agents  꺼지면 다시 켜기 · 날마다 백업을 맥에 등록한다
#   scripts/lod-server.sh remove-agents   그 등록을 지운다
#
# 접속 주소는 LOD_SERVER_IP 하나로 정한다(없으면 이 맥의 집 안 주소). 로그인 절차가 주소를 두 번
# 알려 주므로(MServerTable.xml → LoruleConfig.json) 둘 다 이 값으로 채운다 — docs/run-procedure.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FORK="$ROOT/sources/wren11/Dark-Ages-Private-Server"
STAGING="$FORK/Staging/net9.0"
DOTNET="$ROOT/.tools/dotnet-9.0.317"
LOGS="$HOME/Library/Logs/LOD"
BACKUPS="$HOME/LOD-backups"
AGENTS="$HOME/Library/LaunchAgents"

# 기록과 백업을 얼마나 두나.
LOG_DAYS=14
BACKUP_KEEP=30

server_ip() {
    if [ -n "${LOD_SERVER_IP:-}" ]; then
        echo "$LOD_SERVER_IP"
        return
    fi

    # 이 맥의 집 안 주소. 무선이 먼저, 없으면 유선.
    ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo 127.0.0.1
}

config() {
    local ip
    ip="$(server_ip)"

    for pair in "LoruleConfig.template.json:LoruleConfig.json" "MServerTable.template.xml:MServerTable.xml"; do
        local from="${pair%%:*}" to="${pair##*:}"
        sed -e "s|{{FORK}}|$FORK|g" -e "s|{{SERVER_IP}}|$ip|g" "$ROOT/scripts/server-config/$from" > "$STAGING/$to"
    done

    echo "설정을 다시 깔았습니다 — 주소 $ip"
}

pid() {
    pgrep -f 'Lorule.GameServer.dll' || true
}

# 자동 실행을 등록해 두었으면 맥이 서버를 직접 돌본다(꺼지면 바로 다시 켠다). 그때는 그쪽에 맡긴다 —
# 스크립트가 켠 서버는 스크립트가 끝나는 순간 맥이 함께 정리해 버린다(그래서 한 번 안 살아났다).
supervised() {
    [ -f "$AGENTS/com.lod.gameserver.plist" ]
}

start() {
    if [ -n "$(pid)" ]; then
        echo "이미 켜져 있습니다 (프로세스 $(pid))."
        return
    fi

    [ -f "$STAGING/LoruleConfig.json" ] || config
    mkdir -p "$LOGS"

    if supervised; then
        launchctl load "$AGENTS/com.lod.gameserver.plist" 2>/dev/null || true
    else
        (cd "$STAGING" && DOTNET_ROOT="$DOTNET" nohup "$DOTNET/dotnet" Lorule.GameServer.dll \
            >> "$LOGS/server.log" 2>&1 & disown)
    fi

    sleep 3
    echo "켰습니다 (프로세스 $(pid)). 기록: $LOGS/server.log"
}

stop() {
    local running
    running="$(pid)"

    if [ -z "$running" ]; then
        echo "꺼져 있습니다."
        return
    fi

    # 돌보는 쪽이 있으면 먼저 손을 떼게 한다 — 그러지 않으면 끄는 족족 다시 켠다.
    if supervised; then
        launchctl unload "$AGENTS/com.lod.gameserver.plist" 2>/dev/null || true
    fi

    kill -TERM $running 2>/dev/null || true
    for _ in $(seq 1 30); do
        [ -z "$(pid)" ] && break
        sleep 1
    done

    echo "껐습니다."
}

status() {
    local running
    running="$(pid)"

    if [ -z "$running" ]; then
        echo "꺼져 있습니다."
    else
        echo "켜져 있습니다 (프로세스 $running)."
    fi

    echo "주소 $(server_ip) · 로그인 2610 · 게임 2615"
    [ -f "$LOGS/server.log" ] && echo "기록 $(du -h "$LOGS/server.log" | cut -f1) $LOGS/server.log"
    ls -1 "$BACKUPS" 2>/dev/null | tail -1 | sed 's/^/마지막 백업 /' || true
}

logs() {
    tail -n "${1:-40}" "$LOGS/server.log"
}

# 캐릭터 파일만 담는다. 나머지(맵·템플릿)는 저장소에 있어 다시 만들 수 있다.
backup() {
    mkdir -p "$BACKUPS"
    local name="aislings-$(date +%Y%m%d-%H%M).tar.gz"

    tar -czf "$BACKUPS/$name" -C "$FORK/database/server" aislings
    ls -1t "$BACKUPS"/aislings-*.tar.gz | tail -n +$((BACKUP_KEEP + 1)) | xargs -I {} rm -- {} 2>/dev/null || true

    # 기록은 여기서 함께 정리한다 — 날마다 도는 것이 이것 하나다.
    find "$LOGS" -name '*.log.*' -mtime "+$LOG_DAYS" -delete 2>/dev/null || true

    if [ -f "$LOGS/server.log" ] && [ "$(stat -f%z "$LOGS/server.log")" -gt 20000000 ]; then
        mv "$LOGS/server.log" "$LOGS/server.log.$(date +%Y%m%d)"
    fi

    echo "백업했습니다 — $BACKUPS/$name"
}

agent() {
    local name="$1" what="$2" extra="$3" plist="$AGENTS/$1.plist"

    mkdir -p "$AGENTS"
    cat > "$plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>$name</string>
    <key>ProgramArguments</key>
    <array><string>/bin/bash</string><string>$ROOT/scripts/lod-server.sh</string><string>$what</string></array>
    <key>WorkingDirectory</key><string>$ROOT</string>
    <key>StandardOutPath</key><string>$LOGS/$name.log</string>
    <key>StandardErrorPath</key><string>$LOGS/$name.log</string>
$extra
</dict>
</plist>
PLIST

    launchctl unload "$plist" 2>/dev/null || true
    launchctl load "$plist"
    echo "등록했습니다 — $plist"
}

install_agents() {
    mkdir -p "$LOGS"

    # 서버는 맥이 직접 돌본다 — 꺼지면 곧바로 다시 켜고(KeepAlive), 기록도 같은 파일에 잇는다.
    cat > "$AGENTS/com.lod.gameserver.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>com.lod.gameserver</string>
    <key>ProgramArguments</key>
    <array><string>$DOTNET/dotnet</string><string>Lorule.GameServer.dll</string></array>
    <key>WorkingDirectory</key><string>$STAGING</string>
    <key>EnvironmentVariables</key>
    <dict><key>DOTNET_ROOT</key><string>$DOTNET</string></dict>
    <key>StandardOutPath</key><string>$LOGS/server.log</string>
    <key>StandardErrorPath</key><string>$LOGS/server.log</string>
    <key>KeepAlive</key><true/>
    <key>RunAtLoad</key><true/>
</dict>
</plist>
PLIST

    launchctl unload "$AGENTS/com.lod.gameserver.plist" 2>/dev/null || true
    launchctl load "$AGENTS/com.lod.gameserver.plist"
    echo "등록했습니다 — $AGENTS/com.lod.gameserver.plist"

    # 날마다 새벽 4시에 캐릭터를 백업하고 기록을 정리한다.
    agent com.lod.backup backup '    <key>StartCalendarInterval</key>
    <dict><key>Hour</key><integer>4</integer><key>Minute</key><integer>0</integer></dict>'
}

remove_agents() {
    for name in com.lod.gameserver com.lod.backup; do
        launchctl unload "$AGENTS/$name.plist" 2>/dev/null || true
        rm -f "$AGENTS/$name.plist"
        echo "지웠습니다 — $name"
    done
}

case "${1:-status}" in
    config) config ;;
    start) start ;;
    stop) stop ;;
    restart) stop; start ;;
    status) status ;;
    logs) logs "${2:-40}" ;;
    backup) backup ;;
    install-agents) install_agents ;;
    remove-agents) remove_agents ;;
    *) echo "쓸 수 있는 것: config start stop restart status logs backup install-agents remove-agents"; exit 2 ;;
esac
