using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// 赛前地图编辑工具（测试用，不属于正式逻辑）
/// 左键涂墙 / 右键擦除 / R 键清空地图
/// 挂在任意空物体上，填好 map 和 cam 即可。
/// 这个脚本自包含，不引用项目里的其它脚本。
public class MapTestEditor : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("要编辑的棋盘")]
    public Gridmap map;

    [Tooltip("留空会自动使用 Camera.main")]
    public Camera cam;

    [Header("笔刷")]
    public TileType brush = TileType.Wall;
    public TileType erase = TileType.Ground;
    public bool allowErase = true;

    [Header("清空")]
    public bool enableClearKey = true;

    [Header("鼠标光标")]
    public bool showCursor = true;
    public Color cursorColor = new Color(1f, 0.6f, 0.2f, 0.55f);

    [Tooltip("地图锁住时的光标颜色")]
    public Color lockedCursorColor = new Color(0.6f, 0.6f, 0.6f, 0.18f);

    [Range(0.2f, 1.2f)] public float cursorSizeRatio = 0.95f;
    public int cursorSortingOrder = 20;

    [Header("调试")]
    public bool logEdits = false;

    SpriteRenderer _cursor;
    bool _warnedLocked;

    void Start()
    {
        if (!CheckRefs()) { enabled = false; return; }

        if (showCursor) BuildCursor();

        Debug.Log("MapTestEditor: 就绪 —— 左键涂墙 / 右键擦除 / R 清空" +
                  "（鼠标需停在 Game 视图上）", this);
    }

    void Update()
    {
        if (map == null || cam == null) return;

        Vector3 world = ScreenToBoardPlane(MousePos());
        Vector2Int cell = map.WorldToCell(world);

        UpdateCursor(cell);

        if (!map.InBounds(cell)) return;

        // 地图锁住时什么都不做，Set() 那边也会再拦一层
        if (!map.editable)
        {
            if (LeftHeld() || RightHeld()) WarnLocked();
            return;
        }

        _warnedLocked = false;

        if (LeftHeld()) Apply(cell, brush);
        else if (allowErase && RightHeld()) Apply(cell, erase);

        if (enableClearKey && ClearPressed()) ClearAll();
    }

    void WarnLocked()
    {
        if (_warnedLocked) return;
        _warnedLocked = true;
        Debug.Log("MapTestEditor: 地图已锁定（战斗中）。按重置回到赛前才能继续编辑", this);
    }

    bool CheckRefs()
    {
        if (map == null)
        {
            Debug.LogError("MapTestEditor: map 未指定", this);
            return false;
        }

        if (cam == null) cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("MapTestEditor: cam 未指定，且场景里没有带 MainCamera 标签的相机", this);
            return false;
        }

        return true;
    }

    void Apply(Vector2Int c, TileType t)
    {
        if (map.Set(c, t) && logEdits)
            Debug.Log($"MapTestEditor: {c} -> {t}", this);
    }

    [ContextMenu("清空地图")]
    public void ClearAll()
    {
        if (map == null) return;

        if (!map.editable)
        {
            Debug.Log("MapTestEditor: 地图已锁定，无法清空", this);
            return;
        }

        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                map.Set(new Vector2Int(x, y), TileType.Ground);

        Debug.Log("MapTestEditor: 已清空地图", this);
    }

    Vector3 ScreenToBoardPlane(Vector2 screen)
    {
        float d = Mathf.Abs(cam.transform.position.z - map.transform.position.z);
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, d));
    }

    void BuildCursor()
    {
        var go = new GameObject("BrushCursor");
        go.transform.SetParent(transform, false);

        _cursor = go.AddComponent<SpriteRenderer>();
        _cursor.sprite = MakeSquareSprite();
        _cursor.color = cursorColor;
        _cursor.sortingOrder = cursorSortingOrder;
    }

    static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void UpdateCursor(Vector2Int cell)
    {
        if (_cursor == null) return;

        bool on = map.InBounds(cell);
        _cursor.gameObject.SetActive(on);
        if (!on) return;

        _cursor.color = map.editable ? cursorColor : lockedCursorColor;

        _cursor.transform.position =
            map.CellCenterWorld(cell) + new Vector3(0f, 0f, -0.02f);
        _cursor.transform.localScale =
            Vector3.one * (map.cellSize * cursorSizeRatio);
    }

    // ── 输入兼容层 ────────────────────────────────
    bool LeftHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    bool RightHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    bool ClearPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    Vector2 MousePos()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
}

