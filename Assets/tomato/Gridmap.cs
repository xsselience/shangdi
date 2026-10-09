using System;
using System.Collections.Generic;
using UnityEngine;

public enum TileType { Ground, Wall }

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

    public event Action OnChanged;

    TileType[] _tiles;
    bool[] _camps;
    SpecialElement[] _specials;

    TileType[] Tiles
    {
        get
        {
            if (_tiles == null || _tiles.Length != width * height)
                _tiles = new TileType[width * height];
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

    // ── 特殊棋子（第三层，跨章保留）──
    public SpecialElement GetSpecial(Vector2Int c) =>
        InBounds(c) ? Specials[c.y * width + c.x] : SpecialElement.None;

    /// <summary>写入一格。传 None 等同于移除。</summary>
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
                else if (Get(c) == TileType.Wall) Gizmos.color = new Color(1f, 1f, 1f, 0.30f);
                else Gizmos.color = new Color(1f, 1f, 1f, 0.06f);

                Gizmos.DrawCube(CellCenterWorld(c), Vector3.one * cellSize * 0.9f);
            }
    }
}
