using System;
using System.Collections.Generic;
using UnityEngine;

public class PathAgent : MonoBehaviour
{
    private struct SearchState : IEquatable<SearchState>
    {
        public readonly Vector2Int Position;
        public readonly int Elevation;

        public SearchState(Vector2Int position, int elevation)
        {
            Position = position;
            Elevation = elevation;
        }

        public bool Equals(SearchState other)
        {
            return Position == other.Position && Elevation == other.Elevation;
        }

        public override bool Equals(object obj)
        {
            return obj is SearchState && Equals((SearchState)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return (Position.GetHashCode() * 397) ^ Elevation; }
        }
    }

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
    public double stepsPerTick = 1;

    // 被 Despawn() 之后为 false。显示层靠它决定要不要藏起来
    public bool IsActive { get; private set; } = true;

    public event Action OnPathChanged;
    public event Action OnStep;
    public event Action OnReset;

    readonly List<Vector2Int> _path = new();
    readonly List<int> _pathElevations = new();
    [SerializeField] private ElementReaction _elementReaction = new ElementReaction();
    public IReadOnlyList<Vector2Int> Path => _path;
    public bool HasPath => _path.Count > 0;
    public int Elevation { get; private set; }

    Vector2Int _current;
    double _stepProgress;

    public int GetStepsThisTick()
    {
        if (double.IsNaN(stepsPerTick) || double.IsInfinity(stepsPerTick) || stepsPerTick <= 0)
            return 0;

        // 脚下地形的行动力倍率：森林 0.5，草原 / 沙漠 1
        double speed = map != null ? map.MoveSpeedMultiplier(_current) : 1.0;

        _stepProgress += stepsPerTick * speed;
        double roundingTolerance = Math.Abs(_stepProgress) * 1e-12;
        int steps = (int)Math.Min(Math.Floor(_stepProgress + roundingTolerance), int.MaxValue);
        _stepProgress -= steps;
        return steps;
    }

    public int GetPathElevation(int index)
    {
        return index >= 0 && index < _pathElevations.Count ? _pathElevations[index] : 0;
    }

    public void SetElementReaction(ElementReaction elementReaction)
    {
        _elementReaction = elementReaction ?? new ElementReaction();
        if (map != null) Elevation = _elementReaction.GetStartingElevation(map, _current);
        Recompute();
    }

    void Awake()
    {
        _current = gridPos;
        IsActive = true;
        Elevation = _elementReaction.GetStartingElevation(map, _current);
    }

    // ── 算路 ──
    public void Recompute()
    {
        if (intent.locked || map == null) return;

        List<SearchState> states = AStar(new SearchState(_current, Elevation), intent.target, out bool reached);
        _path.Clear();
        _pathElevations.Clear();
        foreach (SearchState state in states)
        {
            _path.Add(state.Position);
            _pathElevations.Add(state.Elevation);
        }

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
        Elevation = _pathElevations[0];
        _path.RemoveAt(0);
        _pathElevations.RemoveAt(0);

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
        _pathElevations.Clear();
        _stepProgress = 0;
        _current = gridPos;
        Elevation = _elementReaction.GetStartingElevation(map, _current);
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

    List<SearchState> AStar(SearchState start, Vector2Int goal, out bool reached)
    {
        reached = false;

        var open = new List<SearchState> { start };
        var came = new Dictionary<SearchState, SearchState>();
        var g = new Dictionary<SearchState, int> { [start] = 0 };
        var f = new Dictionary<SearchState, int> { [start] = H(start.Position, goal) };

        SearchState best = start;
        int bestH = H(start.Position, goal);

        while (open.Count > 0)
        {
            int bi = 0;
            for (int i = 1; i < open.Count; i++)
                if (Better(open[i], open[bi], f)) bi = i;

            SearchState cur = open[bi];
            open.RemoveAt(bi);

            if (cur.Position == goal) { reached = true; return Build(came, cur); }

            int h = H(cur.Position, goal);
            if (h < bestH) { bestH = h; best = cur; }

            foreach (var d in Dirs)
            {
                Vector2Int position = cur.Position + d;
                int nextElevation;
                if (!_elementReaction.TryGetNextElevation(
                    map, cur.Position, cur.Elevation, position, out nextElevation)) continue;
                SearchState next = new SearchState(position, nextElevation);

                int ng = g[cur] + map.Cost(position);
                if (g.TryGetValue(next, out var old) && ng >= old) continue;

                g[next] = ng;
                f[next] = ng + H(position, goal);
                came[next] = cur;
                if (!open.Contains(next)) open.Add(next);
            }
        }

        return Build(came, best);
    }

    bool Better(SearchState a, SearchState b, Dictionary<SearchState, int> f)
    {
        int fa = f[a], fb = f[b];
        if (fa != fb) return fa < fb;
        int aIndex = a.Position.y * map.width + a.Position.x;
        int bIndex = b.Position.y * map.width + b.Position.x;
        if (aIndex != bIndex) return aIndex < bIndex;
        return a.Elevation < b.Elevation;
    }

    static int H(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    static List<SearchState> Build(Dictionary<SearchState, SearchState> came, SearchState end)
    {
        var path = new List<SearchState>();
        SearchState cur = end;

        while (came.TryGetValue(cur, out var prev))
        {
            path.Add(cur);
            cur = prev;
        }

        path.Reverse();
        return path;
    }

    public void ResolveElementEffect()
    {
        if (_elementReaction != null) _elementReaction.HandleUnitStep(this);
    }
}
