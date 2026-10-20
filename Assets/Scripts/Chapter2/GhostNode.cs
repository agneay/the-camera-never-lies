using System.Collections.Generic;
using UnityEngine;

/// A point in the ghost's hand-authored navigation graph. Links are made symmetric at runtime.
public class GhostNode : MonoBehaviour
{
    public List<GhostNode> links = new List<GhostNode>();

    public Vector3 Pos => transform.position;

    public static void Symmetrize(GhostNode[] nodes)
    {
        foreach (var n in nodes)
            foreach (var l in n.links)
                if (l && !l.links.Contains(n)) l.links.Add(n);
    }

    /// Breadth-first path (graph is small; BFS is plenty).
    public static List<GhostNode> Path(GhostNode from, GhostNode to)
    {
        var prev = new Dictionary<GhostNode, GhostNode> { [from] = null };
        var q = new Queue<GhostNode>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            if (n == to) break;
            foreach (var l in n.links)
                if (l && !prev.ContainsKey(l)) { prev[l] = n; q.Enqueue(l); }
        }
        var path = new List<GhostNode>();
        if (!prev.ContainsKey(to)) return path;
        for (var n = to; n != null; n = prev[n]) path.Add(n);
        path.Reverse();
        return path;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(Pos, 0.15f);
        foreach (var l in links) if (l) Gizmos.DrawLine(Pos, l.Pos);
    }
}
