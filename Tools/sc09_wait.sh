#!/bin/bash
# waits until Unity is idle (not compiling, not playing)
cd /e/WorkArea/VR/ChemicalSafetyVR1
sleep 8
for i in $(seq 1 90); do
  out=$(python Tools/mcp_http.py execute_code '{"action":"execute","code":"return UnityEditor.EditorApplication.isCompiling.ToString()+UnityEditor.EditorApplication.isPlaying.ToString();"}' 2>&1)
  echo "$out" | grep -q 'FalseFalse' && { echo idle; exit 0; }
  sleep 4
done
echo timeout
