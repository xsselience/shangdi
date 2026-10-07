using System;
using UnityEngine;

public enum TileType { Ground, Wall }

public class Gridmap : MonoBehaviour
{
    [Header("棋盘尺寸")]
    public int width = 12;
    public int height = 8;
    public float cellSize = 1f;

    [Header("位置微调")]
    public Vector2 originOffset = Vector2.zero;

    [Header("编辑权限")]
    [Tooltip("关掉后 Set() 一律被拒绝。开战时会自动关掉。只管地形，不管营地")]
    public bool editable = true;

    [Header("营地")]
    [Tooltip("营地对寻路的额外代价。0 = A* 完全无视营地")]
    public int campCostPenalty = 4;

    // 棋盘状态变了（地形或营地）。
    // GridRenderer 重画、路径重算，都靠这一个事件
    public event Action OnChanged;

    TileType[] _tiles;
    bool[] _camps;

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

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

    public TileType Get(Vector2Int c) =>
        InBounds(c) ? Tiles[c.y * width + c.x] : TileType.Wall;

    public bool Walkable(Vector2Int c) =>
        InBounds(c) && Get(c) != TileType.Wall;

    // ── 寻路代价 ─────────────────────────────────
    public int Cost(Vector2Int c)
    {
        int cost = 1;
        if (HasCamp(c)) cost += Mathf.Max(0, campCostPenalty);
        return cost;
    }

    // ── 地形写入（受 editable 管辖）────────────────
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

    // ── 营地（不受 editable 管辖，营地是游戏行为不是玩家编辑）──
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

    // ── 快照：只管地形，不碰营地 ──────────────────
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

    // ── 坐标换算：唯一出口 ─────────────────────────
    public Vector3 CellCenterLocal(Vector2Int c) => new Vector3(
        originOffset.x + (c.x + 0.5f) * cellSize,
        originOffset.y + (c.y + 0.5f) * cellSize,
        0f);

    public Vector3 CellCenterWorld(Vector2Int c) =>
        transform.TransformPoint(CellCenterLocal(c));

    public Vector3 BoardCenterWorld => transform.TransformPoint(new Vector3(
        originOffset.x + width * cellSize * 0.5f,
        originOffset.y + height * cellSize * 0.5f,
        0f));

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world)
                      - new Vector3(originOffset.x, originOffset.y, 0f);

        return new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.y / cellSize));
    }

    void OnDrawGizmos()
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var c = new Vector2Int(x, y);

                if (HasCamp(c)) Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.45f);
                else if (Get(c) == TileType.Wall) Gizmos.color = new Color(1f, 1f, 1f, 0.30f);
                else Gizmos.color = new Color(1f, 1f, 1f, 0.06f);

                Gizmos.DrawCube(CellCenterWorld(c), Vector3.one * cellSize * 0.9f);
            }
    }
}
