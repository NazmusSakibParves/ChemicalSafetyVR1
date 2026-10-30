#!/bin/bash
# Dev helper (untracked on purpose): compile, build the Presentable scene, run the full smoke test, print SC10 lines.
cd "$(dirname "$0")/.."
mcp() { python Tools/mcp_http.py "$@" 2>&1; }
mcp refresh_unity '{"compile":"request","mode":"force","wait_for_ready":false}' >/dev/null
sleep 100
before=$(grep -ac "\[Presentable\] Built" Logs/Editor.log)
for i in $(seq 1 30); do
  out=$(mcp execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Build Presentable Scene"}')
  echo "$out" | grep -q '"success":true' && break
  sleep 5
done
for i in $(seq 1 150); do
  now=$(grep -ac "\[Presentable\] Built" Logs/Editor.log)
  [ "$now" -gt "$before" ] && break
  sleep 5
done
echo "built $before $now"
sleep 8
sleep 3
sd=$(grep -ac "\[SMOKE\] DONE" Logs/Editor.log)
mcp execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Run Smoke Test"}' >/dev/null
for i in $(seq 1 400); do
  nd=$(grep -ac "\[SMOKE\] DONE" Logs/Editor.log)
  [ "$nd" -gt "$sd" ] && break
  sleep 5
done
sleep 5
grep -a "\[SMOKE\] SC10\|\[SMOKE\] scenario 10\|\[SMOKE\] DONE" Logs/Editor.log | tail -60 | cut -c1-240
