using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathRenderer : MonoBehaviour
{
    [Header("引用")]
    public PathAgent agent;

    [Header("开关")]
    public bool showPath = true;
    public bool showTarget = true;

    [Header("红线")]
    [Tooltip("线宽占一格的比例")]
    [Range(0.03f, 0.5f)] public float lineWidthRatio = 0.12f;

    public Color lineColor = new Color(1f, 0.28f, 0.28f, 0.9f);

    [Tooltip("拐角处补的小圆，用来填住两段线之间的缝")]
    [Range(0.03f, 0.6f)] public float nodeSizeRatio = 0.13f;

    [Header("呼吸灯")]
    public bool showPulse = true;

    [Tooltip("呼吸灯只在赛前显示，开战后自动停")]
    public bool pulseOnlyInPreBattle = true;

    [Tooltip("光点从单位跑到终点用多少秒")]
    public float pulsePeriod = 1.2f;

    [Tooltip("光点大小占一格的比例")]
    [Range(0.05f, 0.8f)] public float pulseSizeRatio = 0.35f;

    public Color pulseColor = new Color(1f, 0.95f, 0.72f, 1f);

    [Tooltip("整条线的呼吸幅度。0 = 不呼吸")]
    [Range(0f, 0.8f)] public float breatheAmount = 0.25f;

    [Header("终点标记")]
    public Color targetColor = new Color(1f, 0.35f, 0.35f, 0.45f);

    [Range(0.1f, 1.5f)] public float targetSizeRatio = 0.95f;

    [Header("排序")]
    public int targetSortingOrder = 4;
    public int lineSortingOrder = 5;
    public int pulseSortingOrder = 6;
    public float zOffset = -0.05f;

    readonly List<SpriteRenderer> _segments = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _nodes = new List<SpriteRenderer>();
    readonly List<Vector2Int> _route = new List<Vector2Int>();

    SpriteRenderer _pulse;
    SpriteRenderer _target;

    /// <summary>赛前 = intent 还没被锁。BattleFlow.StartBattle() 会把它锁上。</summary>
    bool IsPreBattle => agent != null && !agent.intent.locked;
    void OnEnable()
    {
        if (agent == null)
        {
            Debug.LogError($"{name}: PathRenderer.agent 未指定 —— 看不见路径", this);
            enabled = false;
            return;
        }
        if (agent.map == null)
        {
            Debug.LogError($"{name}: PathAgent.map 未指定 —— 看不见路径", this);
            enabled = false;
        }
    }

    // 位置每帧重算：棋盘可能被缩放，路径也会随行走变短
    void LateUpdate()
    {
        Refresh();
    }

    [ContextMenu("强制刷新")]
    public void Refresh()
    {
        if (agent == null || agent.map == null) return;

        // ── 组装完整路线：单位当前格 + 剩余路径 ──
        _route.Clear();

        if (showPath && agent.IsActive)
        {
            _route.Add(agent.GridPos);

            IReadOnlyList<Vector2Int> path = agent.Path;
            for (int i = 0; i < path.Count; i++) _route.Add(path[i]);
        }

        DrawLine();
        UpdatePulse();
        UpdateTarget();
    }

    // ─────────────────────────────────────────────
    //  红线
    // ─────────────────────────────────────────────
    void DrawLine()
    {
        Color color = CurrentLineColor();

        // 相邻两点之间一段
        int segmentCount = Mathf.Max(0, _route.Count - 1);

        while (_segments.Count < segmentCount) BuildRenderer(_segments, "Seg", SpriteFactory.WhiteSquare, lineSortingOrder);

        for (int i = 0; i < _segments.Count; i++)
        {
            bool on = i < segmentCount;
            _segments[i].gameObject.SetActive(on);
            if (!on) continue;

            PlaceSegment(_segments[i], _route[i], _route[i + 1], color);
        }

        // 每个格子中心补一个小圆，填住拐角
        while (_nodes.Count < _route.Count) BuildRenderer(_nodes, "Node", SpriteFactory.WhiteDisc, lineSortingOrder);

        for (int i = 0; i < _nodes.Count; i++)
        {
            bool on = i < _route.Count;
            _nodes[i].gameObject.SetActive(on);
            if (!on) continue;

            PlaceNode(_nodes[i], _route[i], color);
        }
    }

    void PlaceSegment(SpriteRenderer sr, Vector2Int a, Vector2Int b, Color color)
    {
        Vector3 pa = agent.map.CellCenterWorld(a) + new Vector3(0f, 0f, zOffset);
        Vector3 pb = agent.map.CellCenterWorld(b) + new Vector3(0f, 0f, zOffset);

        float angle = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;

        sr.transform.position = (pa + pb) * 0.5f;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 白方块在 scale=1 时正好是 1 个世界单位，
        // 所以横向拉到 cellSize 就等于两格的间距
        sr.transform.localScale = new Vector3(agent.map.cellSize,
                                              agent.map.cellSize * lineWidthRatio,
                                              1f);
        sr.color = color;
    }

    void PlaceNode(SpriteRenderer sr, Vector2Int c, Color color)
    {
        sr.transform.position = agent.map.CellCenterWorld(c)
                              + new Vector3(0f, 0f, zOffset + 0.001f);
        sr.transform.rotation = Quaternion.identity;
        sr.transform.localScale = Vector3.one * (agent.map.cellSize * nodeSizeRatio);
        sr.color = color;
    }

    Color CurrentLineColor()
    {
        if (breatheAmount <= 0f) return lineColor;
        if (pulseOnlyInPreBattle && !IsPreBattle) return lineColor;

        float period = Mathf.Max(0.05f, pulsePeriod);
        float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period) + 1f) * 0.5f;

        Color c = lineColor;
        c.a = lineColor.a * (1f - breatheAmount + breatheAmount * wave);
        return c;
    }

    // ─────────────────────────────────────────────
    //  呼吸光点
    // ─────────────────────────────────────────────
    void UpdatePulse()
    {
        if (!showPulse || !agent.IsActive || _route.Count < 2 ||
            (pulseOnlyInPreBattle && !IsPreBattle))
        {
            if (_pulse != null) _pulse.gameObject.SetActive(false);
            return;
        }

        if (_pulse == null)
        {
            var go = new GameObject("Pulse");
            go.transform.SetParent(transform, false);

            _pulse = go.AddComponent<SpriteRenderer>();
            _pulse.sprite = SpriteFactory.WhiteDisc;
            _pulse.sortingOrder = pulseSortingOrder;
        }

        _pulse.gameObject.SetActive(true);

        // 沿整条路线从头跑到尾，循环
        float t = Mathf.Repeat(Time.unscaledTime / Mathf.Max(0.05f, pulsePeriod), 1f);
        float f = t * (_route.Count - 1);

        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, _route.Count - 2);
        float k = Mathf.Clamp01(f - i);

        Vector3 pa = agent.map.CellCenterWorld(_route[i]);
        Vector3 pb = agent.map.CellCenterWorld(_route[i + 1]);

        _pulse.transform.position = Vector3.Lerp(pa, pb, k)
                                  + new Vector3(0f, 0f, zOffset - 0.002f);
        _pulse.transform.rotation = Quaternion.identity;
        _pulse.transform.localScale = Vector3.one * (agent.map.cellSize * pulseSizeRatio);
        _pulse.color = pulseColor;
    }

    // ─────────────────────────────────────────────
    //  终点标记
    // ─────────────────────────────────────────────
    void UpdateTarget()
    {
        if (!showTarget || !agent.IsActive)
        {
            if (_target != null) _target.gameObject.SetActive(false);
            return;
        }

        if (_target == null)
        {
            var go = new GameObject("Target");
            go.transform.SetParent(transform, false);

            _target = go.AddComponent<SpriteRenderer>();
            _target.sprite = SpriteFactory.WhiteSquare;
            _target.sortingOrder = targetSortingOrder;
        }

        _target.gameObject.SetActive(true);
        _target.color = targetColor;
        _target.transform.position = agent.map.CellCenterWorld(agent.intent.target)
                                   + new Vector3(0f, 0f, zOffset);
        _target.transform.rotation = Quaternion.identity;
        _target.transform.localScale = Vector3.one * (agent.map.cellSize * targetSizeRatio);
    }

    // ─────────────────────────────────────────────
    //  对象池
    // ─────────────────────────────────────────────
    void BuildRenderer(List<SpriteRenderer> pool, string prefix, Sprite sprite, int sortingOrder)
    {
        var go = new GameObject($"{prefix}{pool.Count}");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        pool.Add(sr);
    }
}
