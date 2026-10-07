using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathRenderer : MonoBehaviour
{
    [Header("引用")]
    public PathAgent agent;

    [Header("外观")]
    public Color color = new Color(1f, 0.85f, 0.3f, 0.85f);
    [Range(0.05f, 0.9f)] public float dotSizeRatio = 0.32f;
    public int sortingOrder = 5;
    public float zOffset = -0.05f;      // 比棋盘更靠近相机

    readonly List<SpriteRenderer> _dots = new();

    void OnEnable()
    {
        if (agent == null) { enabled = false; return; }
        agent.OnPathChanged += Redraw;
        Redraw();
    }

    void OnDisable()
    {
        if (agent != null) agent.OnPathChanged -= Redraw;
    }

    void Redraw()
    {
        if (agent == null || agent.map == null) return;

        var path = agent.Path;
        int n = path.Count;

        while (_dots.Count < n)
        {
            var go = new GameObject($"Dot{_dots.Count}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.WhiteDisc;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            _dots.Add(sr);
        }

        float size = agent.map.cellSize * dotSizeRatio;

        for (int i = 0; i < _dots.Count; i++)
        {
            bool on = i < n;
            _dots[i].gameObject.SetActive(on);
            if (!on) continue;

            _dots[i].transform.position =
                agent.map.CellCenterWorld(path[i]) + new Vector3(0f, 0f, zOffset);
            _dots[i].transform.localScale = Vector3.one * size;
        }
    }
}
