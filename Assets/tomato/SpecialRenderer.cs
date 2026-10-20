using System.Collections.Generic;
using UnityEngine;

/// 在第二棋盘上画特殊棋子。数据来自 Gridmap.GetSpecial()。
/// 水是"本体 + 上下岸边"三段式；其它元素一张图。
[DisallowMultipleComponent]
public class SpecialRenderer : MonoBehaviour
{
    [Header("引用")]
    public Gridmap map;

    [Header("火 / 风 / 土 / 灰烬（一张图）")]
    public Sprite fireSprite;
    public Sprite waterSprite;      // ← 水的"本体"
    public Sprite windSprite;
    public Sprite groundSprite;
    public Sprite ashSprite;

    [Header("水：上下岸边")]
    [Tooltip("水格上边缘的岸边。上方也是水时不画")]
    public Sprite waterShoreTopSprite;

    [Tooltip("水格下边缘的岸边。下方也是水时不画")]
    public Sprite waterShoreBottomSprite;

    [Tooltip("岸边占一格高度的比例")]
    [Range(0.05f, 0.6f)] public float shoreHeightRatio = 0.35f;

    [Header("兜底配色（没配图时用纯色圆点）")]
    public Color fireColor = new Color(1f, 0.35f, 0.30f, 0.95f);
    public Color waterColor = new Color(0.35f, 0.68f, 1f, 0.95f);
    public Color windColor = new Color(0.42f, 0.95f, 0.72f, 0.95f);
    public Color groundColor = new Color(1f, 0.78f, 0.35f, 0.95f);
    public Color ashColor = new Color(0.45f, 0.42f, 0.40f, 0.95f);

    [Header("外观")]
    [Range(0.2f, 1.2f)] public float sizeRatio = 0.9f;
    public int sortingOrder = 3;
    public float zOffset = -0.02f;

    SpriteRenderer[,] _marks;
    SpriteRenderer[,] _shoresTop;
    SpriteRenderer[,] _shoresBottom;
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
        _shoresTop = new SpriteRenderer[map.width, map.height];
        _shoresBottom = new SpriteRenderer[map.width, map.height];

        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                _marks[x, y] = CreateCell($"{x}_{y}_Body", sortingOrder);
                _shoresTop[x, y] = CreateCell($"{x}_{y}_ShoreT", sortingOrder + 1);
                _shoresBottom[x, y] = CreateCell($"{x}_{y}_ShoreB", sortingOrder + 1);
            }
    }

    SpriteRenderer CreateCell(string label, int order)
    {
        var go = new GameObject(label);
        go.transform.SetParent(_root, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.WhiteDisc;
        sr.sortingOrder = order;

        go.SetActive(false);
        return sr;
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

                SpriteRenderer body = _marks[x, y];
                SpriteRenderer top = _shoresTop[x, y];
                SpriteRenderer bot = _shoresBottom[x, y];

                bool on = e != SpecialElement.None;

                if (body.gameObject.activeSelf != on) body.gameObject.SetActive(on);

                // 岸边先一律关掉，下面按需要打开
                if (top.gameObject.activeSelf) top.gameObject.SetActive(false);
                if (bot.gameObject.activeSelf) bot.gameObject.SetActive(false);

                if (!on) continue;

                // ── 本体 ──
                Sprite art = SpriteOf(e);
                bool hasArt = art != null;
                Sprite sprite = hasArt ? art : SpriteFactory.WhiteDisc;

                if (body.sprite != sprite) body.sprite = sprite;
                body.color = hasArt ? Color.white : ColorOf(e);

                Vector2 b = sprite.bounds.size;
                body.transform.localScale = new Vector3(
                    b.x > 0.0001f ? size / b.x : size,
                    b.y > 0.0001f ? size / b.y : size,
                    1f);

                body.transform.localPosition = map.CellCenterLocal(c)
                                             + new Vector3(0f, 0f, zOffset);

                // ── 水：上下岸边 ──
                if (e == SpecialElement.Water)
                {
                    bool waterAbove = map.GetSpecial(c + Vector2Int.up) == SpecialElement.Water;
                    bool waterBelow = map.GetSpecial(c + Vector2Int.down) == SpecialElement.Water;

                    if (!waterAbove && waterShoreTopSprite != null)
                        PlaceShore(top, c, waterShoreTopSprite, true);

                    if (!waterBelow && waterShoreBottomSprite != null)
                        PlaceShore(bot, c, waterShoreBottomSprite, false);
                }
            }
    }

    void PlaceShore(SpriteRenderer sr, Vector2Int c, Sprite sprite, bool isTop)
    {
        sr.gameObject.SetActive(true);
        sr.sprite = sprite;
        sr.color = Color.white;

        float h = map.cellSize * shoreHeightRatio;

        Vector2 b = sprite.bounds.size;
        sr.transform.localScale = new Vector3(
            b.x > 0.0001f ? map.cellSize / b.x : 1f,
            b.y > 0.0001f ? h / b.y : 1f,
            1f);

        // 贴着格子的上边缘 / 下边缘
        float dy = (map.cellSize * 0.5f) - (h * 0.5f);

        sr.transform.localPosition = map.CellCenterLocal(c)
                                   + new Vector3(0f, isTop ? dy : -dy, zOffset - 0.002f);
    }

    Sprite SpriteOf(SpecialElement e)
    {
        switch (e)
        {
            case SpecialElement.Fire: return fireSprite;
            case SpecialElement.Water: return waterSprite;
            case SpecialElement.Wind: return windSprite;
            case SpecialElement.Ground: return groundSprite;
            case SpecialElement.Ash: return ashSprite;
            default: return null;
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
            case SpecialElement.Ash: return ashColor;
            default: return Color.white;
        }
    }
}