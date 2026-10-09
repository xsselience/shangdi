using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialRenderer : MonoBehaviour
{
    [Header("引用")]
    public Gridmap map;

    [Header("配色")]
    public Color fireColor = new Color(1f, 0.35f, 0.30f, 0.95f);
    public Color waterColor = new Color(0.35f, 0.68f, 1f, 0.95f);
    public Color windColor = new Color(0.42f, 0.95f, 0.72f, 0.95f);
    public Color groundColor = new Color(1f, 0.78f, 0.35f, 0.95f);

    [Header("外观")]
    [Tooltip("标记占一格的比例")]
    [Range(0.2f, 1.2f)] public float sizeRatio = 0.52f;

    public int sortingOrder = 3;
    public float zOffset = -0.02f;

    SpriteRenderer[,] _marks;
    Transform _root;

    void OnEnable()
    {
        if (map == null)
        {
            Debug.LogError($"{name}: SpecialRenderer.map 未指定 —— 特殊棋子不会显示", this);
            enabled = false;
            return;
        }

        Build();
        Refresh();
        map.OnChanged += Refresh;
    }

    void OnDisable()
    {
        if (map != null) map.OnChanged -= Refresh;
    }

    void Build()
    {
        _root = new GameObject("Specials").transform;
        _root.SetParent(map.transform, false);

        _marks = new SpriteRenderer[map.width, map.height];

        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                var c = new Vector2Int(x, y);

                var go = new GameObject($"S_{x}_{y}");
                go.transform.SetParent(_root, false);
                go.transform.localPosition = map.CellCenterLocal(c)
                                           + new Vector3(0f, 0f, zOffset);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.WhiteDisc;
                sr.sortingOrder = sortingOrder;

                go.SetActive(false);
                _marks[x, y] = sr;
            }
    }

    [ContextMenu("强制刷新")]
    public void Refresh()
    {
        if (_marks == null || map == null) return;

        float size = map.cellSize * sizeRatio;

        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                var c = new Vector2Int(x, y);
                SpecialElement e = map.GetSpecial(c);
                var sr = _marks[x, y];

                bool on = e != SpecialElement.None;
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                if (!on) continue;

                sr.color = ColorOf(e);
                sr.transform.localPosition = map.CellCenterLocal(c)
                                           + new Vector3(0f, 0f, zOffset);
                sr.transform.localScale = Vector3.one * size;   // 白圆直径 1 世界单位
            }
    }

    Color ColorOf(SpecialElement e)
    {
        switch (e)
        {
            case SpecialElement.Fire: return fireColor;
            case SpecialElement.Water: return waterColor;
            case SpecialElement.Wind: return windColor;
            case SpecialElement.Ground: return groundColor;
            default: return Color.white;
        }
    }
}
