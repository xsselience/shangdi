using System;
using System.Collections.Generic;
using UnityEngine;

public class PathAgent : MonoBehaviour
{
    [Header("引用")]
    public Gridmap map;

    public UnitIntent intent = new UnitIntent();

    [Tooltip("越小越先算路。单位互不阻挡后这个值暂时只影响算路顺序")]
    public int priority;

    [Header("出发位置")]
    [Tooltip("单位初始所在的格子。移动不会改变它，重置时会退回这里")]
    [SerializeField] Vector2Int gridPos;

    public Vector2Int StartPos => gridPos;
    public Vector2Int GridPos => _current;

    [Header("行动")]
    [Tooltip("每次轮到它行动时前进几格")]
    public int stepsPerTick = 1;

    // 被 Despawn() 之后为 false。显示层靠它决定要不要藏起来
    public bool IsActive { get; private set; } = true;

    public event Action OnPathChanged;
    public event Action OnStep;
    public event Action OnReset;

    readonly List<Vector2Int> _path = new();
    public IReadOnlyList<Vector2Int> Path => _path;
    public bool HasPath => _path.Count > 0;

    Vector2Int _current;

    void Awake()
    {
        _current = gridPos;
        IsActive = true;
    }

    // ── 算路 ──
    public void Recompute()
    {
        if (intent.locked || map == null) return;

        _path.Clear();
        _path.AddRange(AStar(_current, intent.target, out bool reached));

        OnPathChanged?.Invoke();

        if (!reached)
            Debug.Log($"{name}: 目标不可达，退回到最接近目标格子", this);
    }

    // ── 前进一格 ──
    public bool TryStep()
    {
        if (!IsActive) return false;
        if (!intent.locked) return false;
        if (_path.Count == 0) return false;

        _current = _path[0];
        _path.RemoveAt(0);

        OnStep?.Invoke();
        OnPathChanged?.Invoke();
        return true;
    }

    // ── 退场：整个 GameObject 设为不活动 ──
    // UnitView 的 Visual、PathRenderer 的 Dot 都是这个物体的子物体，
    // 所以它们会一起消失，不需要另外通知
    public void Despawn()
    {
        if (!IsActive) return;

        IsActive = false;
        _path.Clear();
        intent.locked = true;

        OnPathChanged?.Invoke();
        gameObject.SetActive(false);
    }

    // ── 重置回出发格（同时让退场的单位重新出现）──
    public void ResetToStart()
    {
        _path.Clear();
        _current = gridPos;
        intent.locked = false;
        IsActive = true;

        gameObject.SetActive(true);     // 先激活，让显示层重新订阅
        OnReset?.Invoke();              // 再通知，UnitView 会直接归位
        OnPathChanged?.Invoke();
    }

    // ── A* ──
    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1),
        new Vector2Int(-1, 0), new Vector2Int(0, -1)
    };

    List<Vector2Int> AStar(Vector2Int start, Vector2Int goal, out bool reached)
    {
        reached = false;

        var open = new List<Vector2Int> { start };
        var came = new Dictionary<Vector2Int, Vector2Int>();
        var g = new Dictionary<Vector2Int, int> { [start] = 0 };
        var f = new Dictionary<Vector2Int, int> { [start] = H(start, goal) };

        Vector2Int best = start;
        int bestH = H(start, goal);

        while (open.Count > 0)
        {
            int bi = 0;
            for (int i = 1; i < open.Count; i++)
                if (Better(open[i], open[bi], f)) bi = i;

            var cur = open[bi];
            open.RemoveAt(bi);

            if (cur == goal) { reached = true; return Build(came, cur); }

            int h = H(cur, goal);
            if (h < bestH) { bestH = h; best = cur; }

            foreach (var d in Dirs)
            {
                var n = cur + d;

                // 只有墙和边界阻挡。单位、营地都不挡
                if (!map.Walkable(n)) continue;

                int ng = g[cur] + map.Cost(n);
                if (g.TryGetValue(n, out var old) && ng >= old) continue;

                g[n] = ng;
                f[n] = ng + H(n, goal);
                came[n] = cur;
                if (!open.Contains(n)) open.Add(n);
            }
        }

        return Build(came, best);
    }

    bool Better(Vector2Int a, Vector2Int b, Dictionary<Vector2Int, int> f)
    {
        int fa = f[a], fb = f[b];
        if (fa != fb) return fa < fb;
        return a.y * map.width + a.x < b.y * map.width + b.x;
    }

    static int H(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    static List<Vector2Int> Build(Dictionary<Vector2Int, Vector2Int> came, Vector2Int end)
    {
        var path = new List<Vector2Int>();
        var cur = end;

        while (came.TryGetValue(cur, out var prev))
        {
            path.Add(cur);
            cur = prev;
        }

        path.Reverse();
        return path;
    }
}
