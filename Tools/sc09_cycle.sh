#!/bin/bash
# dev helper: compile, rebuild the Presentable scene, render the SC09 preview (views in Temp/sc09_views.txt)
cd /e/WorkArea/VR/ChemicalSafetyVR1
SCENE=Assets/Scenes/ChemicalSafety_Presentable.unity
before=$(stat -c %Y "$SCENE")
python Tools/mcp_http.py refresh_unity '{"mode":"force","compile":"request","scope":"all","wait_for_ready":false}' >/dev/null 2>&1
Tools/sc09_wait.sh
python Tools/mcp_http.py read_console '{"count":20,"types":["error"]}' 2>&1 | grep -i "error CS" | head -10
if [ "$1" != "nobuild" ]; then
  python Tools/mcp_http.py execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Build Presentable Scene"}' >/dev/null 2>&1
  for i in $(seq 1 120); do
    sleep 5
    now=$(stat -c %Y "$SCENE")
    if [ "$now" != "$before" ]; then break; fi
  done
  Tools/sc09_wait.sh
  sleep 10
fi
rm -f Temp/Smoke/sc09_*.png
python Tools/mcp_http.py execute_menu_item '{"menu_path":"Chemical Safety/Presentable/Preview SC09 (screenshots)"}' >/dev/null 2>&1
sleep 14
ls Temp/Smoke | grep sc09_
