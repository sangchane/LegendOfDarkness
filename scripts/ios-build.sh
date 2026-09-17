#!/bin/bash
# 아이폰·아이패드에 넣을 .ipa 를 만들고, 같은 Wi-Fi 로 짝지은 기기에 무선으로 넣는다(무료 애플 계정).
#
#   scripts/ios-build.sh build            .ipa 를 만든다
#   scripts/ios-build.sh install          만들고 기기에 넣는다(기기 이름·번호는 --device 로)
#   scripts/ios-build.sh renew            서명을 새로 받는다(기기가 보여야 한다)
#   scripts/ios-build.sh check            며칠 남았나 — 이틀 이하면 스스로 갱신한다
#   scripts/ios-build.sh watch-sign       날마다 check 를 돌게 맥에 등록한다
#   scripts/ios-build.sh unwatch-sign     그 등록을 지운다
#
# **무료 계정은 서명이 7일이면 끝난다.** 명령줄로 새로 받을 수 있다 — 2026-09-18 확인: 보관본을 다시
# 내보내는 것(`-exportArchive -allowProvisioningUpdates`)으로는 안 되고("No profiles ... were found"),
# **Xcode 프로젝트를 기기를 지정해 빌드**하면 만들어진다:
#   xcodebuild -project …/LodClient.xcodeproj -target LodClient -destination "id=<기기>" -allowProvisioningUpdates build
# 기기는 케이블로 한 번 짝지어 두면 같은 Wi-Fi 에서 보인다(`xcrun devicectl list devices`).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT="$ROOT/mobile/client"
PRESETS="$CLIENT/export_presets.cfg"
IPA="$CLIENT/build/ios/LodClient.ipa"
GODOT="$ROOT/.tools/godot-4.6-mono/Godot_mono.app/Contents/MacOS/Godot"
PROFILES="$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"
AGENTS="$HOME/Library/LaunchAgents"
LOGS="$HOME/Library/Logs/LOD"

export DOTNET_ROOT="$ROOT/.tools/dotnet-9.0.317"
export PATH="$DOTNET_ROOT:$PATH"
export DEVELOPER_DIR="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"

# 우리 앱의 서명. 서명 파일은 그대로 읽을 수 없어 하나씩 풀어 이름을 본다.
profile() {
    local wanted
    wanted="$(grep '^application/bundle_identifier=' "$PRESETS" | cut -d'"' -f2)"

    local found
    for found in "$PROFILES"/*.mobileprovision; do
        [ -e "$found" ] || continue
        if security cms -D -i "$found" 2>/dev/null | grep -q "<string>[A-Z0-9]*\.$wanted</string>"; then
            echo "$found"
            return
        fi
    done
}

read_profile() {
    security cms -D -i "$1" 2>/dev/null | plutil -extract "$2" raw -o - - 2>/dev/null || true
}

# 팀 번호는 서명에서 읽는다 — 저장소에 적어 두지 않는다(빌드가 끝나면 다시 비운다).
team() {
    if [ -n "${LOD_TEAM_ID:-}" ]; then
        echo "$LOD_TEAM_ID"
        return
    fi

    local found
    found="$(profile)"

    if [ -z "$found" ]; then
        echo "서명이 없습니다 — Xcode 에서 기기에 한 번 실행해 받으십시오(무료 계정)." >&2
        exit 1
    fi

    read_profile "$found" TeamIdentifier.0
}

days_left() {
    local found
    found="$(profile)"
    [ -z "$found" ] && { echo -1; return; }

    # 만료일은 2026-09-18T13:14:38Z 꼴로 나온다.
    local ends now
    ends="$(date -j -u -f '%Y-%m-%dT%H:%M:%SZ' "$(read_profile "$found" ExpirationDate)" +%s 2>/dev/null || echo 0)"
    now="$(date +%s)"

    echo $(( (ends - now) / 86400 ))
}

check() {
    local left
    left="$(days_left)"

    if [ "$left" -ge 0 ]; then
        echo "서명이 ${left}일 남았습니다."
        [ "$left" -gt 2 ] && return 0
    fi

    if renew; then
        say "서명을 새로 받았습니다 — $(days_left)일 남았습니다."
    else
        say "서명을 새로 받지 못했습니다 — 아이패드를 켜고 같은 Wi-Fi 에 두십시오."
    fi
}

# 기기 하나를 고른다. LOD_DEVICE_ID 가 있으면 그것을, 없으면 지금 붙어 있는(짝지은) 첫 기기를.
device_id() {
    if [ -n "${LOD_DEVICE_ID:-}" ]; then
        echo "$LOD_DEVICE_ID"
        return
    fi

    # 상태 칸이 "available (paired)" 나 "connected" 인 것만. "unavailable" 도 available 을 품고 있다.
    xcrun devicectl list devices 2>/dev/null | awk -F'  +' '$4 ~ /^(available|connected)/ {print $3; exit}'
}

# 서명 새로 받기. 기기를 지정해 Xcode 프로젝트를 빌드하면 7일짜리 서명이 새로 만들어진다.
renew() {
    local project="$CLIENT/build/ios/LodClient.xcodeproj"

    if [ ! -d "$project" ]; then
        echo "Xcode 프로젝트가 없습니다 — 먼저 build 로 한 번 만드십시오." >&2
        return 1
    fi

    local device
    device="$(device_id)"

    if [ -z "$device" ]; then
        echo "기기가 보이지 않습니다 — 아이패드를 켜고 같은 Wi-Fi 에 두십시오." >&2
        return 1
    fi

    echo "기기 $device 로 서명을 받습니다..."
    xcodebuild -project "$project" -target LodClient -configuration Debug \
        -destination "id=$device" -allowProvisioningUpdates build > "$LOGS/ios-renew.log" 2>&1 || {
        echo "실패했습니다 — $LOGS/ios-renew.log" >&2
        return 1
    }

    echo "서명을 새로 받았습니다 — $(days_left)일 남았습니다."
}

say() {
    echo "$1"
    osascript -e "display notification \"$1\" with title \"어둠의 전설 — 아이폰 서명\"" 2>/dev/null || true
}

restore() {
    # 팀 번호는 커밋되지 않게 반드시 되돌린다.
    sed -i '' 's|^application/app_store_team_id=.*|application/app_store_team_id=""|' "$PRESETS"
}

build() {
    trap restore EXIT

    local id
    id="$(team)"
    echo "팀 $id · 서명 $(days_left)일 남음"

    sed -i '' "s|^application/app_store_team_id=.*|application/app_store_team_id=\"$id\"|" "$PRESETS"
    mkdir -p "$CLIENT/build/ios"
    "$GODOT" --headless --path "$CLIENT" --export-debug "iOS" "$IPA"

    # EXPORT SUCCEEDED 를 믿지 않는다 — C# 이 빠진 채로도 성공으로 끝난다(docs/mobile-client.md).
    if ! unzip -l "$IPA" | grep -q 'LodClient.framework'; then
        echo "C# 이 빠진 .ipa 입니다 — 기기에서 엔진이 뜬 직후 죽습니다." >&2
        exit 1
    fi

    echo "만들었습니다 — $IPA ($(du -h "$IPA" | cut -f1))"
}

# 케이블로 한 번 짝지어 두면 같은 Wi-Fi 에서 이름이나 번호로 넣을 수 있다.
install_to() {
    local device="${1:-}"

    if [ -z "$device" ]; then
        device="$(device_id)"
    fi

    if [ -z "$device" ]; then
        echo "기기가 보이지 않습니다 — 케이블로 한 번 짝짓고, Xcode 의 기기 창에서 '네트워크로 연결'을 켜십시오." >&2
        exit 1
    fi

    xcrun devicectl device install app --device "$device" "$IPA"
}

watch_sign() {
    mkdir -p "$LOGS" "$AGENTS"

    cat > "$AGENTS/com.lod.iossign.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>com.lod.iossign</string>
    <key>ProgramArguments</key>
    <array><string>/bin/bash</string><string>$ROOT/scripts/ios-build.sh</string><string>check</string></array>
    <key>WorkingDirectory</key><string>$ROOT</string>
    <key>StandardOutPath</key><string>$LOGS/com.lod.iossign.log</string>
    <key>StandardErrorPath</key><string>$LOGS/com.lod.iossign.log</string>
    <key>StartCalendarInterval</key>
    <dict><key>Hour</key><integer>10</integer><key>Minute</key><integer>0</integer></dict>
</dict>
</plist>
PLIST

    launchctl unload "$AGENTS/com.lod.iossign.plist" 2>/dev/null || true
    launchctl load "$AGENTS/com.lod.iossign.plist"
    echo "등록했습니다 — 날마다 오전 10시에 살펴보고, 이틀 이하로 남으면 스스로 새로 받습니다."
}

unwatch_sign() {
    launchctl unload "$AGENTS/com.lod.iossign.plist" 2>/dev/null || true
    rm -f "$AGENTS/com.lod.iossign.plist"
    echo "지웠습니다 — com.lod.iossign"
}

mkdir -p "$LOGS"

case "${1:-check}" in
    build) build ;;
    install) build; install_to "${2:-}" ;;
    renew) renew ;;
    check) check ;;
    watch-sign) watch_sign ;;
    unwatch-sign) unwatch_sign ;;
    *) echo "쓸 수 있는 것: build install [기기] renew check watch-sign unwatch-sign"; exit 2 ;;
esac
