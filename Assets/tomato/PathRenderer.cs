using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathRenderer : MonoBehaviour
{
    [Header("引用")]
    public PathAgent agent;

    [Header("路径")]
    public bool showPath = true;
    public Color pathColor = new Color(1f, 0.85f, 0.30f, 0.85f);
    [Range(0.05f, 0.9f)] public float dotSizeRatio = 0.32f;

    [Header("终点标记")]
    public bool showTarget = true;
    public Color targetColor = new Color(1f, 0.35f, 0.35f, 0.45f);
    [Range(0.1f, 1.5f)] public float targetSizeRatio = 0.95f;

    [Header("排序")]
    public int targetSortingOrder = 4;
    public int pathSortingOrder = 5;
    public float zOffset = -0.05f;

    readonly List<SpriteRenderer> _dots = new();
    SpriteRenderer _target;

    void OnEnable()
    {
        if (agent == null)
        {
            Debug.LogError($"{name}: PathRenderer.agent 未指定 —— 看不见路径和终点", this);
            return;
        }
        if (agent.map == null)
        {
            Debug.LogError($"{name}: PathAgent.map 未指定 —— 看不见路径和终点", this);
            return;
        }

        agent.OnPathChanged += Redraw;
        Redraw();
    }

    void OnDisable()
    {
        if (agent != null) agent.OnPathChanged -= Redraw;
    }

    // 位置每帧重算。棋盘被缩放或移动时，这一步让它跟着走
    void LateUpdate()
    {
        RefreshVisuals();
    }

    // 路径内容变了（重算 / 走掉一格 / 退场）时才需要补齐点数
    void Redraw()
    {
        if (agent == null || agent.map == null) return;

        RefreshVisuals();
    }

    void RefreshVisuals()
    {
        if (agent == null || agent.map == null) return;

        var path = agent.Path;
        int n = (showPath && agent.IsActive) ? path.Count : 0;

        // 补足路径点的数量
        while (_dots.Count < n) BuildDot(_dots.Count);

        // 终点标记按需创建
        if (showTarget && agent.IsActive && _target == null) BuildTarget();

        // 把尺寸换算到"世界里的目标大小"，
        // 这样不管这个物体是不是 Gridmap 的子物体，结果都一致
        float mapScale = Mathf.Abs(agent.map.transform.lossyScale.x);
        float ownScale = Mathf.Abs(transform.lossyScale.x);
        float scaleBase = agent.map.cellSize * mapScale / Mathf.Max(0.0001f, ownScale);

        // ── 路径点 ──
        for (int i = 0; i < _dots.Count; i++)
        {
            bool on = i < n;
            _dots[i].gameObject.SetActive(on);
            if (!on) continue;

            _dots[i].color = pathColor;
            _dots[i].sortingOrder = pathSortingOrder;
            _dots[i].transform.position =
                agent.map.CellCenterWorld(path[i]) + new Vector3(0f, 0f, zOffset);
            _dots[i].transform.localScale =
                Vector3.one * (scaleBase * dotSizeRatio);
        }

        // ── 终点标记 ──
        if (_target != null)
        {
            bool on = showTarget && agent.IsActive;
            _target.gameObject.SetActive(on);

            if (on)
            {
                _target.color = targetColor;
                _target.sortingOrder = targetSortingOrder;
                _target.transform.position =
                    agent.map.CellCenterWorld(agent.intent.target) + new Vector3(0f, 0f, zOffset);
                _target.transform.localScale =
                    Vector3.one * (scaleBase * targetSizeRatio);
            }
        }
    }

    void BuildDot(int index)
    {
        var go = new GameObject($"Dot{index}");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.WhiteDisc;
        sr.sortingOrder = pathSortingOrder;
        _dots.Add(sr);
    }

    void BuildTarget()
    {
        var go = new GameObject("Target");
        go.transform.SetParent(transform, false);

        _target = go.AddComponent<SpriteRenderer>();
        _target.sprite = SpriteFactory.WhiteSquare;
        _target.sortingOrder = targetSortingOrder;
    }
}
