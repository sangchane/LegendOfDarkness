#!/usr/bin/env bash
# 클라우드 봇 이름 「사제」→「성직자」 옮기기 — 캐릭터 파일·봇 설정·경매장(scripts/ops/rename-bots.py 가 실제로 바꾼다).
#
#   LOD_CLOUD_IP=<공인 IP> scripts/ops/rename-bots.sh            미리보기(아무것도 안 바꿈)
#   LOD_CLOUD_IP=<공인 IP> scripts/ops/rename-bots.sh --apply    바꾸기 — 게임 서버(lod)·생태계 봇(lod-eco)·동료 봇(lod-bot@N)이
#                                                                모두 멈춰 있어야 한다. 바꾸기 전 ~/lod-backups/rename-<시각> 에 백업.
# 순서: 서버·봇 멈춤 → --apply → `cloud-server.sh deploy`(새 이름이 든 서버 설정 틀을 올리고 다시 켠다).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IP="${LOD_CLOUD_IP:?LOD_CLOUD_IP=<공인 IP> 를 붙여 주세요}"
KEY="$HOME/.ssh/lod_oracle"
HOST="ubuntu@$IP"
SSH=(ssh -i "$KEY" -o StrictHostKeyChecking=accept-new "$HOST")

case "${1:-}" in
    "") APPLY="" ;;
    --apply) APPLY="--apply" ;;
    *) echo "쓰는 법: $0 [--apply]" >&2; exit 2 ;;
esac

# 멈춤 확인과 바꾸기를 한 번의 접속에서 — 확인한 뒤 누가 켜는 틈을 줄인다. 돌고 있으면 바꾸지 않는다(돌던 서버·봇이 옛 이름으로 다시 쓴다).
CHECK='shopt -s nullglob; units=(lod lod-eco); for f in /home/ubuntu/lod-bot/companion-bot-*.json; do n=${f##*-}; units+=("lod-bot@${n%.json}"); done
for u in "${units[@]}"; do s=$(systemctl is-active "$u" || true)
  case "$u:$s" in lod:inactive|lod:failed) ;; lod:*) echo "거절: 게임 서버 lod 가 $s 입니다 — 멈춘 뒤 다시." >&2; exit 1 ;;
  *:inactive|*:failed|*:unknown) ;; *) echo "거절: $u 가 $s 입니다 — 멈춘 뒤 다시." >&2; exit 1 ;; esac
done'

if [ -n "$APPLY" ]; then
    "${SSH[@]}" "$CHECK"$'\n'"python3 - --apply" < "$ROOT/scripts/ops/rename-bots.py"
else
    "${SSH[@]}" "python3 -" < "$ROOT/scripts/ops/rename-bots.py"
fi
