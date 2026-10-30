#!/bin/bash
# Dev helper (untracked): compile only (no scene build), then the SC10-only smoke test.
cd "$(dirname "$0")/.."
mcp() { python Tools/mcp_http.py "$@" 2>&1; }
mcp refresh_unity '{"compile":"request","mode":"force","wait_for_ready":false}' >/dev/null
for i in $(seq 1 60); do
  sleep 5
  newest=$(ls -t Assets/Scripts/ChemicalSafety/Presentable/*.cs | head -1)
  [ Library/ScriptAssemblies/Assembly-CSharp.dll -nt "$newest" ] && break
done
sleep 15
for i in $(seq 1 20); do out=$(mcp execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Dev: start at Scenario 10"}'); echo "$out" | grep -q '"success":true' && break; sleep 6; done
sleep 3
sd=$(grep -ac "\[SMOKE\] DONE" Logs/Editor.log)
for i in $(seq 1 20); do out=$(mcp execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Run Smoke Test"}'); echo "$out" | grep -q '"success":true' && break; sleep 6; done
for i in $(seq 1 200); do
  nd=$(grep -ac "\[SMOKE\] DONE" Logs/Editor.log)
  [ "$nd" -gt "$sd" ] && break
  sleep 5
done
sleep 5
for i in $(seq 1 20); do out=$(mcp execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Dev: start at Scenario 10"}'); echo "$out" | grep -q '"success":true' && break; sleep 6; done
nd=$(grep -ac "\[SMOKE\] DONE" Logs/Editor.log)
[ "$nd" -gt "$sd" ] && echo "NEW RUN OK" || echo "NO NEW RUN"
grep -a "touchable\|door clear\|DONE\|cannot be touched" Logs/Editor.log | tail -14 | cut -c1-300
