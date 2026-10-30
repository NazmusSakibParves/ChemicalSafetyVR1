#!/bin/bash
cd "$(dirname "$0")/.."
mcp() { python Tools/mcp_http.py "$@" 2>&1; }
before=$(grep -ac "\[Presentable\] Built" Logs/Editor.log)
for i in $(seq 1 30); do out=$(mcp execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Build Presentable Scene"}'); echo "$out" | grep -q '"success":true' && break; sleep 5; done
for i in $(seq 1 150); do now=$(grep -ac "\[Presentable\] Built" Logs/Editor.log); [ "$now" -gt "$before" ] && break; sleep 5; done
echo "built $before $now"; sleep 8
rm -f Temp/Smoke/sc10_*.png
mcp execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Preview SC10 (screenshots)"}' >/dev/null
for i in $(seq 1 40); do [ -f Temp/Smoke/sc10_right.png ] && break; sleep 3; done
ls Temp/Smoke | grep "^sc10_"
