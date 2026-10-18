"""Tiny MCP-over-HTTP client for the local MCP for Unity server. usage: python Tools/mcp_http.py <tool> '<json args>'"""
import json
import os
import sys
import urllib.request

URL = "http://127.0.0.1:8080/mcp"
SESS = os.path.join(os.environ.get("TEMP", "."), "mcp_session.txt")
HDR = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}


def post(body, sid=None, timeout=600):
    h = dict(HDR)
    if sid:
        h["mcp-session-id"] = sid
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), headers=h, method="POST")
    with urllib.request.urlopen(req, timeout=timeout) as r:
        sid2 = r.headers.get("mcp-session-id")
        txt = r.read().decode("utf-8", "replace")
    msgs = []
    for line in txt.splitlines():
        if line.startswith("data:"):
            try:
                msgs.append(json.loads(line[5:].strip()))
            except Exception:
                pass
    return sid2, msgs


def session():
    if os.path.exists(SESS):
        return open(SESS).read().strip()
    sid, _ = post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": "2025-03-26", "capabilities": {}, "clientInfo": {"name": "cc-bridge", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    open(SESS, "w").write(sid)
    return sid


def call(tool, args, retry=True):
    sid = session()
    try:
        _, msgs = post({"jsonrpc": "2.0", "id": 2, "method": "tools/call",
                        "params": {"name": tool, "arguments": args}}, sid)
    except Exception as e:
        if retry:
            if os.path.exists(SESS):
                os.remove(SESS)
            return call(tool, args, False)
        return "ERR " + str(e)
    for m in msgs:
        if "result" in m:
            parts = m["result"].get("content", [])
            return "\n".join(p.get("text", "") for p in parts)
        if "error" in m:
            if retry and "session" in json.dumps(m["error"]).lower():
                os.remove(SESS)
                return call(tool, args, False)
            return "ERR " + json.dumps(m["error"])
    return "no result"


if __name__ == "__main__":
    print(call(sys.argv[1], json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}))
