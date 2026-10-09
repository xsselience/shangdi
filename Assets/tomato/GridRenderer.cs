using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    [Header("引用")]
    public Gridmap map;

    [Header("底图")]
    [Tooltip("整块棋盘的底色。就算一格贴图都没配，也能看见棋盘范围")]
    public bool showPlate = true;

    [Tooltip("留空则用自动生成的白色方块")]
    public Sprite plateSprite;
    public Color plateTint = new Color(1f, 1f, 1f, 0.18f);
    public int plateSortingOrder = -10;

    [Header("格子贴图（留空用占位方块）")]
    public Sprite groundSprite;
    public Sprite wallSprite;
    public Sprite campSprite;

    [Header("格子染色")]
    public Color groundTint = new Color(1f, 1f, 1f, 0.06f);
    public Color wallTint = new Color(1f, 1f, 1f, 0.45f);
    public Color campTint = new Color(1f, 0.35f, 0.35f, 0.40f);

    [Header("格子")]
    [Tooltip("格子贴图占一格的比例。1 = 铺满，0.94 = 留出网格线")]
    [Range(0.5f, 1.5f)] public float cellFitRatio = 0.94f;

    public int cellSortingOrder = 0;

    [Header("调试")]
    public bool logBuild = true;

    SpriteRenderer[,] _cells;
    SpriteRenderer _plate;
    Transform _root;

    void OnEnable()
    {
        if (map == null)
        {
            Debug.LogError($"{name}: GridRenderer.map 未指定 —— 棋盘不会显示", this);
            enabled = false;
            return;
        }

        Build();
        map.OnChanged += Refresh;
    }

    void OnDisable()
    {
        if (map != null) map.OnChanged -= Refresh;
    }

    [ContextMenu("重建棋盘")]
    public void Build()
    {
        Clear();

        if (showPlate) BuildPlate();

        _root = new GameObject("Cells").transform;
        _root.SetParent(map.transform, false);

        _cells = new SpriteRenderer[map.width, map.height];

        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                var c = new Vector2Int(x, y);

                var go = new GameObject($"C_{x}_{y}");
                go.transform.SetParent(_root, false);
                go.transform.localPosition = map.CellCenterLocal(c);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = cellSortingOrder;

                _cells[x, y] = sr;
            }

        Refresh();

        if (logBuild)
            Debug.Log($"{name}: 棋盘已生成 {map.width}x{map.height}={map.width * map.height}格  " +
                      $"cellSize={map.cellSize}  cellFitRatio={cellFitRatio}\n" +
                      $"        网格物体位置={map.transform.position}  首格世界坐标={map.CellCenterWorld(Vector2Int.zero)}\n" +
                      $"        相机位置={(Camera.main != null ? Camera.main.transform.position.ToString() : "无 MainCamera")}" +
                      $"  正交={(Camera.main != null ? Camera.main.orthographic.ToString() : "-")}" +
                      $"  视野高度={(Camera.main != null ? (Camera.main.orthographicSize * 2f).ToString("F2") : "-")}",
                      this);
    }

    void BuildPlate()
    {
        var go = new GameObject("Plate");
        go.transform.SetParent(map.transform, false);

        _plate = go.AddComponent<SpriteRenderer>();
        _plate.sprite = plateSprite != null ? plateSprite : SpriteFactory.WhiteSquare;
        _plate.color = plateTint;
        _plate.sortingOrder = plateSortingOrder;

        // 用 BoardCenterLocal，不是 originOffset ——
        // 因为支点现在在正上方，而底图要盖住棋盘，得放在中心
        go.transform.localPosition = map.BoardCenterLocal + new Vector3(0f, 0f, 0.01f);

        var b = _plate.sprite.bounds.size;

        go.transform.localScale = new Vector3(
            map.width * map.cellSize / Mathf.Max(0.0001f, b.x),
            map.height * map.cellSize / Mathf.Max(0.0001f, b.y),
            1f);
    }

    [ContextMenu("强制刷新")]
    public void Refresh()
    {
        if (_cells == null || map == null) return;

        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                var c = new Vector2Int(x, y);
                var sr = _cells[x, y];

                Sprite s;
                Color col;

                if (map.HasCamp(c)) { s = campSprite; col = campTint; }
                else if (map.Get(c) == TileType.Wall) { s = wallSprite; col = wallTint; }
                else { s = groundSprite; col = groundTint; }

                if (s == null) s = SpriteFactory.WhiteSquare;

                if (sr.sprite != s)
                {
                    sr.sprite = s;

                    var b = s.bounds.size;
                    float size = map.cellSize * cellFitRatio;

                    if (b.x > 0.0001f && b.y > 0.0001f)
                        sr.transform.localScale =
                            new Vector3(size / b.x, size / b.y, 1f);
                }

                sr.color = col;
                sr.sortingOrder = cellSortingOrder;
            }
    }

    void Clear()
    {
        Kill(_root != null ? _root.gameObject : null);
        Kill(_plate != null ? _plate.gameObject : null);
        _root = null;
        _plate = null;
        _cells = null;
    }

    static void Kill(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
