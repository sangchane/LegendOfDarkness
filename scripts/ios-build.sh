#!/bin/bash
# 아이폰·아이패드에 넣을 .ipa 를 만들고, 같은 Wi-Fi 로 짝지은 기기에 무선으로 넣는다(무료 애플 계정).
#
#   scripts/ios-build.sh build            .ipa 를 만든다
#   scripts/ios-build.sh install          만들고 기기에 넣는다(기기 이름·번호는 --device 로)
#   scripts/ios-build.sh devices          지금 보이는 기기를 이름·번호로 보여 준다(아이패드·아이폰 따로)
#   scripts/ios-build.sh renew            서명을 새로 받는다(LOD_DEVICE_ID 로 기기를 고른다 — 그 기기가
#                                         프로필에 실제로 들어갔는지까지 확인한다)
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

# xcode-select 가 CommandLineTools 를 가리키면 `xcrun devicectl` 이 없다("not a developer tool").
# sudo 없이 고치는 길 — 이 스크립트 안에서만 Xcode 를 보게 한다 (2026-09-19).
if [ -z "${DEVELOPER_DIR:-}" ] && [ -d /Applications/Xcode.app/Contents/Developer ]; then
    export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi

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

# 지금 보이는 기기를 이름·번호와 함께 보여 준다. 아이패드와 아이폰을 따로 다루려면 이것부터 본다.
devices() {
    printf '%-22s %s\n' "이름" "번호(UDID)"
    xcrun devicectl list devices 2>/dev/null | awk -F'  +' '$4 ~ /^(available|connected)/ {print $1 "\t" $3}' |
    while IFS=$'\t' read -r name ident; do
        [ -z "$ident" ] && continue
        local udid
        udid="$(xcrun devicectl device info details --device "$ident" 2>/dev/null | awk -F': ' '/• udid:/ {print $2; exit}')"
        printf '%-22s %s\n' "$name" "${udid:-$ident}"
    done
}

# 프로필에 든 기기 번호들. 없으면 아무것도 안 찍는다.
profile_devices() {
    local found
    found="$(profile)"
    [ -z "$found" ] && return
    security cms -D -i "$found" 2>/dev/null |
        plutil -extract ProvisionedDevices xml1 -o - - 2>/dev/null |
        sed -n 's/.*<string>\(.*\)<\/string>.*/\1/p'
}

# 그 기기가 프로필에 들어 있나. **`renew` 는 이것으로 스스로를 검사한다.**
profile_has_device() {
    local want="$1"
    profile_devices | grep -qxF "$want"
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

    # 서명 파일은 하드웨어 UDID 로 기기를 적는다. devicectl 의 번호(연결용)를 받았으면 UDID 로 바꾼다 —
    # 그대로 비교하면 늘 "프로필에 없다" 가 된다(2026-09-24).
    local udid
    udid="$(xcrun devicectl device info details --device "$device" 2>/dev/null | awk -F': ' '/• udid:/ {print $2; exit}')"
    [ -n "$udid" ] && device="$udid"

    # 아직 살아 있는 서명이 있으면 Xcode 가 그것을 다시 써서 날수가 늘지 않는다. 옆으로 치워 두고 새로 받는다
    # (지우지 않는다 — ~/LOD-backups/profiles-<날짜>).
    local old
    old="$(profile)"
    if [ -n "$old" ]; then
        local keep="$HOME/LOD-backups/profiles-$(date +%Y%m%d)"
        mkdir -p "$keep"
        mv "$old" "$keep/"
    fi

    echo "기기 $device 로 서명을 받습니다..."
    # **-scheme 이어야 한다.** -target 으로 부르면 xcodebuild 가 -destination 을 통째로 무시하고
    # ("Ignoring provided run destination because no scheme was passed") 기기를 등록하지 않는다.
    # 그래서 빌드는 성공하는데 프로필에는 옛 기기만 남아, 다른 기기에 넣으면 거절당했다 (2026-09-19).
    xcodebuild -project "$project" -scheme LodClient -configuration Debug \
        -destination "id=$device" -allowProvisioningUpdates build > "$LOGS/ios-renew.log" 2>&1 || {
        echo "실패했습니다 — $LOGS/ios-renew.log" >&2
        return 1
    }

    # 빌드가 성공해도 그 기기가 프로필에 들어갔는지는 별개다. 확인하지 않으면 "새로 받았습니다" 가 거짓말이 된다.
    if ! profile_has_device "$device"; then
        echo "빌드는 됐는데 기기 $device 가 프로필에 없습니다 — $LOGS/ios-renew.log" >&2
        echo "프로필에 든 기기: $(profile_devices | tr '\n' ' ')" >&2
        return 1
    fi

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
    # 목록을 먼저 받아 두고 본다. `unzip | grep -q` 로 이으면 grep 이 먼저 닫아 unzip 이 실패로 끝나고,
    # set -o pipefail 때문에 멀쩡한 .ipa 도 빠진 것으로 읽힌다(2026-09-18).
    local listing
    listing="$(unzip -l "$IPA")"

    if ! grep -q 'LodClient.framework' <<< "$listing"; then
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

    # 집 Wi-Fi 에서는 기기를 로컬 네트워크 알림(Bonjour)으로 찾는데, 화면이 꺼진 아이폰은 잠시 뒤 알림을 멈춰 "unavailable" 이
    # 된다 — 빌드(수 분) 사이에 잠기면 설치 때 안 보였다. 핫스팟에서는 아이폰이 공유기라 늘 보인다(사용자 2026-09-26).
    # 짝지은 아이폰이 있으면 2분까지 다시 보이기를 기다린다.
    if [ -z "$device" ]; then
        local paired
        paired="$(xcrun devicectl list devices 2>/dev/null | awk -F'  +' '$4 ~ /paired|unavailable/ && $5 ~ /iPhone/ {print $3; exit}')"
        if [ -n "$paired" ]; then
            echo "아이폰이 잠들어 안 보입니다 — 화면을 켜고 잠금을 풀어 두십시오(2분 기다립니다)." >&2
            for _ in $(seq 1 24); do
                xcrun devicectl device info details --device "$paired" >/dev/null 2>&1 || true
                device="$(device_id)"
                [ -n "$device" ] && break
                sleep 5
            done
        fi
    fi

    if [ -z "$device" ]; then
        echo "기기가 보이지 않습니다 — 아이폰 화면을 켠 채 맥과 같은 Wi-Fi 에 두십시오. 처음이면 케이블로 한 번 짝짓고, Xcode 의 기기 창에서 '네트워크로 연결'을 켜십시오." >&2
        exit 1
    fi

    # 와이파이에서 연결이 한 번 끊기면("Connection interrupted") 맥의 CoreDevice 서비스가 그 연결을 붙잡고
    # "Failed to allocate RSD device" 만 되풀이했다 — 용량 탓이 아니다(2026-09-26). 서비스를 내리면 스스로 다시 뜬다.
    for attempt in 1 2 3; do
        if xcrun devicectl device install app --device "$device" "$IPA"; then
            return
        fi
        [ "$attempt" = 3 ] && break
        echo "설치가 끊겼습니다 — 맥의 기기 연결 서비스를 다시 켜고 한 번 더 합니다($attempt/2)." >&2
        killall CoreDeviceService remotepairingd 2>/dev/null || true
        for _ in $(seq 1 12); do
            xcrun devicectl list devices 2>/dev/null | awk -F'  +' '$4 ~ /^(available|connected)/' | grep -q . && break
            sleep 5
        done
    done
    exit 1
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
    devices) devices ;;
    check) check ;;
    watch-sign) watch_sign ;;
    unwatch-sign) unwatch_sign ;;
    *) echo "쓸 수 있는 것: build install [기기] devices renew check watch-sign unwatch-sign"; exit 2 ;;
esac
