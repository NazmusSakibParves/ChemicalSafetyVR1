#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Quadric edge-collapse decimator used to bring the client's heavy glb meshes (supervisor 132k tris, hydraulic press 1.2M tris)
    /// down to Quest budgets. Vertices on open edges (UV seams, hard-normal splits, submesh borders) are frozen, so texturing and
    /// shading stay intact; interior vertices collapse onto a neighbouring vertex in order of lowest quadric error.
    /// </summary>
    public static class ScCheckDecimator
    {
        private struct Quad
        {
            public double a, b, c, d, e, f, g, h, i, j; // symmetric 4x4: aa ab ac ad / bb bc bd / cc cd / dd

            public static Quad FromPlane(double pa, double pb, double pc, double pd, double w)
            {
                return new Quad
                {
                    a = w * pa * pa, b = w * pa * pb, c = w * pa * pc, d = w * pa * pd,
                    e = w * pb * pb, f = w * pb * pc, g = w * pb * pd,
                    h = w * pc * pc, i = w * pc * pd, j = w * pd * pd
                };
            }

            public static Quad operator +(Quad x, Quad y)
            {
                return new Quad
                {
                    a = x.a + y.a, b = x.b + y.b, c = x.c + y.c, d = x.d + y.d, e = x.e + y.e,
                    f = x.f + y.f, g = x.g + y.g, h = x.h + y.h, i = x.i + y.i, j = x.j + y.j
                };
            }

            public double Error(Vector3 p)
            {
                double x = p.x, y = p.y, z = p.z;
                return a * x * x + 2 * b * x * y + 2 * c * x * z + 2 * d * x + e * y * y + 2 * f * y * z + 2 * g * y + h * z * z + 2 * i * z + j;
            }
        }

        private struct Cand
        {
            public double cost;
            public int from, to, stampFrom, stampTo;
        }

        /// <summary>Decimates one triangle list. Returns the new triangle indices (vertex arrays are unchanged, use Compact afterwards).</summary>
        public static int[] Decimate(Vector3[] pos, Vector2[] uv, int[] tris, int targetTris, bool freezeOpen = true)
        {
            var nv = pos.Length;
            var nf = tris.Length / 3;
            if (nf <= targetTris)
                return tris;

            var alive = new bool[nf];
            var fv = new int[nf * 3];
            System.Array.Copy(tris, fv, tris.Length);
            for (var f = 0; f < nf; f++)
                alive[f] = true;

            var vFaces = new List<int>[nv];
            for (var v = 0; v < nv; v++)
                vFaces[v] = new List<int>(6);
            for (var f = 0; f < nf; f++)
                for (var k = 0; k < 3; k++)
                    vFaces[fv[f * 3 + k]].Add(f);

            // frozen = on an open edge
            var frozen = new bool[nv];
            var edgeCount = new Dictionary<long, int>(nf * 3);
            for (var f = 0; f < nf; f++)
                for (var k = 0; k < 3; k++)
                {
                    var key = EdgeKey(fv[f * 3 + k], fv[f * 3 + (k + 1) % 3]);
                    edgeCount.TryGetValue(key, out var c);
                    edgeCount[key] = c + 1;
                }

            for (var f = 0; f < nf; f++)
                for (var k = 0; k < 3; k++)
                {
                    var a = fv[f * 3 + k];
                    var b = fv[f * 3 + (k + 1) % 3];
                    if (freezeOpen && edgeCount[EdgeKey(a, b)] == 1)
                    {
                        frozen[a] = true;
                        frozen[b] = true;
                    }
                }

            // vertices sharing a position with a frozen vertex stay put too (split duplicates)
            var byPos = new Dictionary<Vector3Int, List<int>>();
            for (var v = 0; v < nv; v++)
            {
                var q = new Vector3Int(Mathf.RoundToInt(pos[v].x * 4000f), Mathf.RoundToInt(pos[v].y * 4000f), Mathf.RoundToInt(pos[v].z * 4000f));
                if (!byPos.TryGetValue(q, out var l))
                    byPos[q] = l = new List<int>(2);
                l.Add(v);
            }

            foreach (var l in byPos.Values)
            {
                if (l.Count < 2)
                    continue;
                for (var k = 0; k < l.Count; k++)
                    frozen[l[k]] = true;
            }

            var quad = new Quad[nv];
            for (var f = 0; f < nf; f++)
            {
                var p0 = pos[fv[f * 3]];
                var p1 = pos[fv[f * 3 + 1]];
                var p2 = pos[fv[f * 3 + 2]];
                var n = Vector3.Cross(p1 - p0, p2 - p0);
                var area = n.magnitude * 0.5f;
                if (area < 1e-12f)
                    continue;
                n.Normalize();
                var q = Quad.FromPlane(n.x, n.y, n.z, -Vector3.Dot(n, p0), area);
                for (var k = 0; k < 3; k++)
                    quad[fv[f * 3 + k]] = quad[fv[f * 3 + k]] + q;
            }

            var stamp = new int[nv];
            var heap = new List<Cand>(nf * 2);

            void Push(Cand c)
            {
                heap.Add(c);
                var i = heap.Count - 1;
                while (i > 0)
                {
                    var p = (i - 1) / 2;
                    if (heap[p].cost <= heap[i].cost)
                        break;
                    (heap[p], heap[i]) = (heap[i], heap[p]);
                    i = p;
                }
            }

            Cand Pop()
            {
                var top = heap[0];
                var last = heap[heap.Count - 1];
                heap.RemoveAt(heap.Count - 1);
                if (heap.Count > 0)
                {
                    heap[0] = last;
                    var i = 0;
                    while (true)
                    {
                        var l = i * 2 + 1;
                        var r = l + 1;
                        var m = i;
                        if (l < heap.Count && heap[l].cost < heap[m].cost)
                            m = l;
                        if (r < heap.Count && heap[r].cost < heap[m].cost)
                            m = r;
                        if (m == i)
                            break;
                        (heap[m], heap[i]) = (heap[i], heap[m]);
                        i = m;
                    }
                }

                return top;
            }

            void PushEdge(int from, int to)
            {
                if (frozen[from])
                    return;
                var uvPenalty = (uv[from] - uv[to]).sqrMagnitude * 0.02;
                var cost = (quad[from] + quad[to]).Error(pos[to]) + uvPenalty;
                Push(new Cand { cost = cost, from = from, to = to, stampFrom = stamp[from], stampTo = stamp[to] });
            }

            for (var f = 0; f < nf; f++)
                for (var k = 0; k < 3; k++)
                {
                    var a = fv[f * 3 + k];
                    var b = fv[f * 3 + (k + 1) % 3];
                    PushEdge(a, b);
                    PushEdge(b, a);
                }

            var live = nf;
            var neigh = new HashSet<int>();
            while (live > targetTris && heap.Count > 0)
            {
                var c = Pop();
                if (stamp[c.from] != c.stampFrom || stamp[c.to] != c.stampTo || frozen[c.from])
                    continue;

                // collapse c.from into c.to: reject if any surviving face would flip or collapse to a sliver
                var ok = true;
                foreach (var f in vFaces[c.from])
                {
                    if (!alive[f])
                        continue;
                    var has = false;
                    for (var k = 0; k < 3; k++)
                        if (fv[f * 3 + k] == c.to)
                            has = true;
                    if (has)
                        continue;
                    var a = fv[f * 3];
                    var b = fv[f * 3 + 1];
                    var d = fv[f * 3 + 2];
                    var before = Vector3.Cross(pos[b] - pos[a], pos[d] - pos[a]);
                    if (a == c.from) a = c.to;
                    else if (b == c.from) b = c.to;
                    else d = c.to;
                    var after = Vector3.Cross(pos[b] - pos[a], pos[d] - pos[a]);
                    if (after.sqrMagnitude < 1e-14f || Vector3.Dot(before.normalized, after.normalized) < 0.35f)
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                    continue;

                foreach (var f in vFaces[c.from])
                {
                    if (!alive[f])
                        continue;
                    var has = false;
                    for (var k = 0; k < 3; k++)
                        if (fv[f * 3 + k] == c.to)
                            has = true;
                    if (has)
                    {
                        alive[f] = false;
                        live--;
                        continue;
                    }

                    for (var k = 0; k < 3; k++)
                        if (fv[f * 3 + k] == c.from)
                            fv[f * 3 + k] = c.to;
                    vFaces[c.to].Add(f);
                }

                vFaces[c.from].Clear();
                quad[c.to] = quad[c.to] + quad[c.from];
                stamp[c.from]++;
                stamp[c.to]++;

                neigh.Clear();
                foreach (var f in vFaces[c.to])
                {
                    if (!alive[f])
                        continue;
                    for (var k = 0; k < 3; k++)
                        if (fv[f * 3 + k] != c.to)
                            neigh.Add(fv[f * 3 + k]);
                }

                foreach (var n in neigh)
                {
                    PushEdge(c.to, n);
                    PushEdge(n, c.to);
                }
            }

            var res = new List<int>(live * 3);
            for (var f = 0; f < nf; f++)
            {
                if (!alive[f])
                    continue;
                if (fv[f * 3] == fv[f * 3 + 1] || fv[f * 3 + 1] == fv[f * 3 + 2] || fv[f * 3] == fv[f * 3 + 2])
                    continue;
                res.Add(fv[f * 3]);
                res.Add(fv[f * 3 + 1]);
                res.Add(fv[f * 3 + 2]);
            }

            return res.ToArray();
        }

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    }
}
#endif
