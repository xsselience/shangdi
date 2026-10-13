using System;
using System.Collections.Generic;
using UnityEngine;

public enum TileType
{
    Ground = 0,   // 草原：正常通行，无效果
    Wall = 1,   // 山：阻挡前进（有高度可以越）
    Forest = 2,   // 森林：减速
    Desert = 3,   // 沙漠：暂无效果
}

/// 第二棋盘上的特殊棋子类型。和第一棋盘的 ElementType 一一对应
public enum SpecialElement { None = 0, Fire = 1, Water = 2, Wind = 3, Ground = 4 }

public class Gridmap : MonoBehaviour
{
    [Header("棋盘尺寸")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    [Header("支点")]
    [Tooltip("缩放/旋转的支点，归一化坐标。(-0.5,-0.5)=左下角，(0,0)=中心，(0,0.5)=正上方中点")]
    public Vector2 anchor = new Vector2(0f, 0.5f);

    [Tooltip("支点额外偏移（世界单位）。正常留 0")]
    public Vector2 originOffset = Vector2.zero;

    [Header("编辑权限")]
    [Tooltip("关掉后 Set() 一律被拒绝。开战时会自动关掉。只管地形")]
    public bool editable = true;

    [Header("营地")]
    [Tooltip("营地对寻路的额外代价。0 = A* 忽略营地")]
    public int campCostPenalty = 0;

    [Header("地形行动力倍率")]
    [Tooltip("草原：1 = 正常速度")]
    [Range(0f, 2f)] public float groundSpeed = 1f;

    [Tooltip("森林：0.5 = 走一格要两回合")]
    [Range(0f, 2f)] public float forestSpeed = 0.5f;

    [Tooltip("沙漠：暂无效果")]
    [Range(0f, 2f)] public float desertSpeed = 1f;

    [Tooltip("山：本来就走不上去，这里只是兜底")]
    [Range(0f, 2f)] public float wallSpeed = 1f;

    // ══ 地形：序列化，随场景保存，在自定义 Inspector 里点画 ══
    [SerializeField, HideInInspector] private TileType[] _tiles;
    [SerializeField, HideInInspector] private int _tilesW = -1;
    [SerializeField, HideInInspector] private int _tilesH = -1;

    public event Action OnChanged;

    // 营地与特殊棋子都是运行时数据，不进场景
    bool[] _camps;
    SpecialElement[] _specials;

    TileType[] Tiles
    {
        get
        {
            if (_tiles == null || _tiles.Length != width * height)
            {
                _tiles = new TileType[width * height];
                _tilesW = width;
                _tilesH = height;
            }
            return _tiles;
        }
    }

    bool[] Camps
    {
        get
        {
            if (_camps == null || _camps.Length != width * height)
                _camps = new bool[width * height];
            return _camps;
        }
    }

    SpecialElement[] Specials
    {
        get
        {
            if (_specials == null || _specials.Length != width * height)
                _specials = new SpecialElement[width * height];
            return _specials;
        }
    }

    void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (_tiles != null && _tilesW == width && _tilesH == height) return;

        var next = new TileType[width * height];

        if (_tiles != null && _tilesW > 0 && _tilesH > 0)
        {
            int cw = Mathf.Min(_tilesW, width);
            int ch = Mathf.Min(_tilesH, height);
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    next[y * width + x] = _tiles[y * _tilesW + x];
        }

        _tiles = next;
        _tilesW = width;
        _tilesH = height;
    }

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

    public TileType Get(Vector2Int c) =>
        InBounds(c) ? Tiles[c.y * width + c.x] : TileType.Wall;

    public bool Walkable(Vector2Int c) =>
        InBounds(c) && Get(c) != TileType.Wall;

    public int Cost(Vector2Int c)
    {
        int cost = 1;
        if (HasCamp(c)) cost += Mathf.Max(0, campCostPenalty);
        return cost;
    }

    /// <summary>
    /// 站在这个格子上时，行动力要乘多少。
    /// 1 = 正常，0.5 = 走一格要两回合，0 = 完全动不了。
    /// </summary>
    public float MoveSpeedMultiplier(Vector2Int c)
    {
        switch (Get(c))
        {
            case TileType.Forest: return Mathf.Max(0f, forestSpeed);
            case TileType.Desert: return Mathf.Max(0f, desertSpeed);
            case TileType.Wall: return Mathf.Max(0f, wallSpeed);
            default: return Mathf.Max(0f, groundSpeed);
        }
    }

    /// <summary>地形名字，给日志和 UI 用</summary>
    public static string TerrainName(TileType t)
    {
        switch (t)
        {
            case TileType.Wall: return "山";
            case TileType.Forest: return "森林";
            case TileType.Desert: return "沙漠";
            default: return "草原";
        }
    }

    public bool Set(Vector2Int c, TileType t)
    {
        if (!editable) return false;
        if (!InBounds(c)) return false;

        int i = c.y * width + c.x;
        if (Tiles[i] == t) return false;

        Tiles[i] = t;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>编辑器绘制专用：绕过 editable。运行时请用 Set()。</summary>
    public void EditorSetTile(Vector2Int c, TileType t)
    {
        if (!InBounds(c)) return;
        Tiles[c.y * width + c.x] = t;
    }

    // ── 营地 ──
    public bool HasCamp(Vector2Int c) => InBounds(c) && Camps[c.y * width + c.x];

    public bool AddCamp(Vector2Int c)
    {
        if (!InBounds(c)) return false;

        int i = c.y * width + c.x;
        if (Camps[i]) return false;

        Camps[i] = true;
        OnChanged?.Invoke();
        return true;
    }

    public bool RemoveCamp(Vector2Int c)
    {
        if (!InBounds(c)) return false;

        int i = c.y * width + c.x;
        if (!Camps[i]) return false;

        Camps[i] = false;
        OnChanged?.Invoke();
        return true;
    }

    [ContextMenu("清空营地")]
    public void ClearCamps()
    {
        for (int i = 0; i < Camps.Length; i++) Camps[i] = false;
        OnChanged?.Invoke();
    }

    public int CampCount()
    {
        int n = 0;
        var a = Camps;
        for (int i = 0; i < a.Length; i++) if (a[i]) n++;
        return n;
    }

    // ── 特殊棋子 ──
    public SpecialElement GetSpecial(Vector2Int c) =>
        InBounds(c) ? Specials[c.y * width + c.x] : SpecialElement.None;

    public bool SetSpecial(Vector2Int c, SpecialElement element)
    {
        if (!InBounds(c)) return false;

        int i = c.y * width + c.x;
        if (Specials[i] == element) return false;

        Specials[i] = element;
        OnChanged?.Invoke();
        return true;
    }

    [ContextMenu("清空特殊棋子")]
    public void ClearSpecials()
    {
        var a = Specials;
        for (int i = 0; i < a.Length; i++) a[i] = SpecialElement.None;
        OnChanged?.Invoke();
    }

    public int SpecialCount()
    {
        int n = 0;
        var a = Specials;
        for (int i = 0; i < a.Length; i++) if (a[i] != SpecialElement.None) n++;
        return n;
    }

    // ── 快照：只管地形 ──
    public TileType[] CaptureSnapshot()
    {
        var copy = new TileType[width * height];
        Array.Copy(Tiles, copy, copy.Length);
        return copy;
    }

    public void RestoreSnapshot(TileType[] snap)
    {
        if (snap == null || snap.Length != width * height) return;

        Array.Copy(snap, Tiles, snap.Length);
        OnChanged?.Invoke();
    }

    // ── 坐标换算 ──
    public Vector3 CellCenterLocal(Vector2Int c) => new Vector3(
        originOffset.x + (c.x + 0.5f - width * (0.5f + anchor.x)) * cellSize,
        originOffset.y + (c.y + 0.5f - height * (0.5f + anchor.y)) * cellSize,
        0f);

    public Vector3 CellCenterWorld(Vector2Int c) =>
        transform.TransformPoint(CellCenterLocal(c));

    public Vector3 BoardCenterLocal => new Vector3(
        originOffset.x - anchor.x * width * cellSize,
        originOffset.y - anchor.y * height * cellSize,
        0f);

    public Vector3 BoardCenterWorld => transform.TransformPoint(BoardCenterLocal);

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world)
                      - new Vector3(originOffset.x, originOffset.y, 0f);

        return new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize + width * (0.5f + anchor.x)),
            Mathf.FloorToInt(local.y / cellSize + height * (0.5f + anchor.y)));
    }

    void OnDrawGizmos()
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var c = new Vector2Int(x, y);

                if (GetSpecial(c) != SpecialElement.None) Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.6f);
                else if (HasCamp(c)) Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.45f);
                else
                {
                    switch (Get(c))
                    {
                        case TileType.Wall: Gizmos.color = new Color(1f, 1f, 1f, 0.30f); break;
                        case TileType.Forest: Gizmos.color = new Color(0.35f, 0.9f, 0.45f, 0.35f); break;
                        case TileType.Desert: Gizmos.color = new Color(0.95f, 0.85f, 0.45f, 0.30f); break;
                        default: Gizmos.color = new Color(1f, 1f, 1f, 0.06f); break;
                    }
                }

                Gizmos.DrawCube(CellCenterWorld(c), Vector3.one * cellSize * 0.9f);
            }
    }
}
