#!/bin/bash
# 안드로이드판(.apk)을 만들어 내려받기 페이지(/download/)에 올린다. 맥에서 그대로 만든다(구글 플레이가 아니라 받은 사람이 직접 설치).
#
#   scripts/ops/android-build.sh build      build/android/LodClient.apk 를 만든다
#   scripts/ops/android-build.sh release    만들고 클라우드 내려받기 페이지에 올린다
#
# 도구는 작업공간 안에 있다(시스템에는 깔지 않음) — JDK 17 은 .tools/jdk-17.0.20.1, 안드로이드 SDK 는 .tools/android-sdk.
# 고도가 두 경로를 편집기 설정(~/Library/Application Support/Godot/editor_settings-4.6.tres)에서 읽으므로 build 가 맞춰 둔다.
# 서명 키는 저장소 밖 ~/.lod/android/ — lod-release.keystore 와 암호 파일 keystore.env(LOD_ANDROID_KEYSTORE·_KEY_ALIAS·_KEY_PASSWORD).
# 암호는 환경 변수로만 넘기고 화면에 내지 않는다. **키를 잃으면 이미 깐 사람이 새 판을 덮어 깔지 못한다**(서명이 달라져
# 지우고 다시 깔아야 한다) — 두 파일을 따로 백업해 둔다.
#
# 고도는 .NET 런타임 팩에서 안 쓰는 정적 라이브러리(lib/*/*.a, 약 57MB)까지 APK 에 싣는다. 안드로이드는 .so 만 읽으므로
# 빼고 다시 정렬·서명한다(240MB → 180MB). 실기기에서 뜨는지는 폰으로 직접 확인해야 한다(이 스크립트는 구조만 본다).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CLIENT="$ROOT/mobile/client"
OUT="$CLIENT/build/android"
APK="$OUT/LodClient.apk"
GODOT="$ROOT/.tools/godot-4.6-mono/Godot_mono.app/Contents/MacOS/Godot"
export DOTNET_ROOT="$ROOT/.tools/dotnet-9.0.317"
export JAVA_HOME="$ROOT/.tools/jdk-17.0.20.1/Contents/Home"
export ANDROID_HOME="$ROOT/.tools/android-sdk"
export PATH="$DOTNET_ROOT:$JAVA_HOME/bin:$PATH"
BT="$ANDROID_HOME/build-tools/35.0.1"
SETTINGS="$HOME/Library/Application Support/Godot/editor_settings-4.6.tres"
KEYSTORE_ENV="$HOME/.lod/android/keystore.env"
PACKAGE="com.fallendev.lod.client"

# 고도 편집기 설정의 JDK·SDK 경로를 이 작업공간의 것으로 맞춘다(이미 같으면 건드리지 않는다).
point_godot_at_tools() {
    local key value want
    for key in java_sdk_path android_sdk_path; do
        case "$key" in java_sdk_path) value="$JAVA_HOME" ;; *) value="$ANDROID_HOME" ;; esac
        want="export/android/$key = \"$value\""
        grep -q "^export/android/$key = " "$SETTINGS" 2>/dev/null \
            || { echo "고도 편집기 설정에 export/android/$key 가 없습니다 — $SETTINGS" >&2; exit 1; }
        grep -qxF "$want" "$SETTINGS" || sed -i '' "s|^export/android/$key = .*|$want|" "$SETTINGS"
    done
}

build() (
    for tool in "$JAVA_HOME/bin/keytool" "$BT/apksigner" "$BT/zipalign" "$BT/aapt2"; do
        [ -x "$tool" ] || { echo "도구가 없습니다 — $tool (.tools 를 확인하십시오)" >&2; exit 1; }
    done
    [ -r "$KEYSTORE_ENV" ] || { echo "서명 키 설정이 없습니다 — $KEYSTORE_ENV" >&2; exit 1; }
    point_godot_at_tools

    # 암호는 이 하위 셸 안의 환경 변수로만 둔다. 고도가 GODOT_ANDROID_KEYSTORE_RELEASE_* 를 읽는다.
    set -a; . "$KEYSTORE_ENV"; set +a
    export GODOT_ANDROID_KEYSTORE_RELEASE_PATH="$LOD_ANDROID_KEYSTORE"
    export GODOT_ANDROID_KEYSTORE_RELEASE_USER="$LOD_ANDROID_KEY_ALIAS"
    export GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD="$LOD_ANDROID_KEY_PASSWORD"
    [ -s "$GODOT_ANDROID_KEYSTORE_RELEASE_PATH" ] || { echo "서명 키 파일이 없습니다 — $GODOT_ANDROID_KEYSTORE_RELEASE_PATH" >&2; exit 1; }

    mkdir -p "$OUT"
    stage="$(mktemp -d "$OUT/.build.XXXXXX")"
    trap 'rm -rf "$stage"' EXIT
    "$GODOT" --headless --path "$CLIENT" --import
    # 앱이 켜질 때 내려받기 페이지의 번호와 견준다(AppUpdate) — release 가 build/android/version.txt 를 version-android.txt 로 올린다.
    date +%Y%m%d%H%M > "$CLIENT/app-version.txt"
    "$GODOT" --headless --path "$CLIENT" --export-release "Android" "$stage/raw.apk"
    cp "$CLIENT/app-version.txt" "$stage/version.txt"

    # 새로 내보낸 파일만 검사한다 — 이전 성공본으로 이번 실패를 가리지 않는다. C# 이 빠진 채 만들어지면(iOS 에서 있었던 일)
    # 앱이 켜지자마자 죽으므로 C# 어셈블리와 mono 런타임이 들어 있는지 본다.
    if [ ! -s "$stage/raw.apk" ]; then
        echo "안드로이드판을 만들지 못했습니다 — APK 가 없습니다." >&2
        exit 1
    fi
    listing="$(unzip -l "$stage/raw.apk")"
    for need in assets/.godot/mono/publish/arm64/LodClient.dll assets/.godot/mono/publish/arm64/Lod.Mobile.Core.dll \
                lib/arm64-v8a/libmonosgen-2.0.so lib/arm64-v8a/libgodot_android.so; do
        grep -q " $need\$" <<<"$listing" || { echo "APK 에 $need 가 없습니다 — C# 내보내기가 빠졌습니다." >&2; exit 1; }
    done

    # 맥 시험용 자동 로그인(login.cfg)이 실리면 받는 사람 모두 그 계정으로 들어간다. 내보내기 설정(exclude_filter)이 빼지만,
    # 올리기 전에 한 번 더 본다. 파일은 APK 안에 낱개로 들어가므로 이름으로 찾는다.
    if grep -Eq '/(login|hunt)\.cfg$' <<<"$listing"; then
        echo "시험 계정(login.cfg·hunt.cfg)이 실렸습니다 — export_presets.cfg 의 exclude_filter 를 보십시오." >&2
        exit 1
    fi

    zip -qd "$stage/raw.apk" 'lib/*.a' || [ $? -eq 12 ]  # 12 = 지울 것이 없음
    "$BT/zipalign" -f -P 16 4 "$stage/raw.apk" "$stage/LodClient.apk"
    "$BT/apksigner" sign --ks "$GODOT_ANDROID_KEYSTORE_RELEASE_PATH" --ks-pass env:GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD \
        --ks-key-alias "$GODOT_ANDROID_KEYSTORE_RELEASE_USER" "$stage/LodClient.apk"
    "$BT/apksigner" verify "$stage/LodClient.apk"
    badging="$("$BT/aapt2" dump badging "$stage/LodClient.apk" 2>/dev/null)"
    grep -q "^package: name='$PACKAGE'" <<<"$badging" || { echo "APK 의 패키지 이름이 $PACKAGE 가 아닙니다." >&2; exit 1; }

    mv -f "$stage/LodClient.apk" "$APK"
    mv -f "$stage/version.txt" "$OUT/version.txt"
    echo "만들었습니다 — $APK ($(du -h "$APK" | cut -f1))"
)

case "${1:-build}" in
    build) build ;;
    release) build; LOD_CLOUD_IP="${LOD_CLOUD_IP:-161.33.43.117}" "$ROOT/scripts/ops/cloud-dashboard.sh" release android ;;
    *) echo "쓸 수 있는 것: build release"; exit 2 ;;
esac
