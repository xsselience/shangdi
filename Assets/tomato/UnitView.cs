using System.Collections;
using UnityEngine;

public class UnitView : MonoBehaviour
{
    [Header("引用")]
    public PathAgent agent;

    [Tooltip("留空会自动创建子物体来显示")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("留空则用自动生成的占位圆点")]
    public Sprite sprite;

    [Header("外观")]
    public Color placeholderColor = new Color(0.40f, 0.78f, 1f, 1f);
    public Color tint = Color.white;
    [Range(0.1f, 1.5f)] public float fitRatio = 0.8f;
    public bool autoFitToCell = true;
    public int sortingOrder = 10;
    public float zOffset = -0.1f;

    [Header("跟随")]
    [Tooltip("越大越跟手，越小越飘")]
    public float followSharpness = 14f;

    [Header("死亡动画")]
    [Tooltip("按顺序排好。留空则死亡时直接消失")]
    public Sprite[] deathFrames;

    [Tooltip("每帧停留多少秒")]
    public float deathFrameDuration = 0.12f;

    [Header("开火动画（撞击时）")]
    [Tooltip("按顺序排好。留空则撞击时直接消失")]
    public Sprite[] attackFrames;

    [Tooltip("每帧停留多少秒")]
    public float attackFrameDuration = 0.1f;

    /// <summary>正在播一次性动画（死亡 / 开火）时为 true，期间不跟随、不覆盖贴图</summary>
    public bool IsBusy { get; private set; }

    Sprite _applied;
    bool _warned;
    bool _needsSnap = true;

    void Awake()
    {
        EnsureVisual();
        if (agent == null) Warn("agent 未指定");
    }

    void OnEnable()
    {
        _needsSnap = true;
        if (agent != null) agent.OnReset += SnapToCell;
    }

    void OnDisable()
    {
        if (agent != null) agent.OnReset -= SnapToCell;
    }

    void LateUpdate()
    {
        if (agent == null || agent.map == null)
        {
            Warn("agent 或 agent.map 未指定");
            return;
        }

        // 一次性动画期间不要覆盖贴图、不要跟随
        if (IsBusy) return;

        EnsureVisual();
        ApplySprite();

        if (_needsSnap)
        {
            _needsSnap = false;
            SnapToCell();
            return;
        }

        Follow();
    }

    // ─────────────────────────────────────────────
    //  一次性动画
    // ─────────────────────────────────────────────
    /// <summary>播放死亡帧动画，播完之后调 onDone。</summary>
    public void PlayDeath(System.Action onDone)
    {
        PlayOnce(deathFrames, deathFrameDuration, onDone);
    }

    /// <summary>播放开火帧动画，播完之后调 onDone。</summary>
    public void PlayAttack(System.Action onDone)
    {
        PlayOnce(attackFrames, attackFrameDuration, onDone);
    }

    /// <summary>通用：播一组帧一次。没有配帧 / 正在忙，都会立刻调 onDone。</summary>
    public void PlayOnce(Sprite[] frames, float frameDuration, System.Action onDone)
    {
        if (IsBusy)
        {
            if (onDone != null) onDone.Invoke();
            return;
        }

        if (frames == null || frames.Length == 0 || spriteRenderer == null)
        {
            if (onDone != null) onDone.Invoke();
            return;
        }

        IsBusy = true;
        StartCoroutine(PlayOnceRoutine(frames, frameDuration, onDone));
    }

    IEnumerator PlayOnceRoutine(Sprite[] frames, float frameDuration, System.Action onDone)
    {
        float delay = Mathf.Max(0.02f, frameDuration);

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) spriteRenderer.sprite = frames[i];

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / delay;
                yield return null;
            }
        }

        IsBusy = false;

        if (onDone != null) onDone.Invoke();
    }

    // ─────────────────────────────────────────────
    //  位置
    // ─────────────────────────────────────────────
    public void SnapToCell()
    {
        if (agent == null || agent.map == null || spriteRenderer == null) return;

        spriteRenderer.transform.position =
            agent.map.CellCenterWorld(agent.GridPos) + new Vector3(0f, 0f, zOffset);
    }

    void Follow()
    {
        Vector3 target = agent.map.CellCenterWorld(agent.GridPos)
                       + new Vector3(0f, 0f, zOffset);

        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        spriteRenderer.transform.position =
            Vector3.Lerp(spriteRenderer.transform.position, target, k);
    }

    // ─────────────────────────────────────────────
    //  外观
    // ─────────────────────────────────────────────
    void Warn(string msg)
    {
        if (_warned) return;
        _warned = true;
        Debug.LogError($"{name}: UnitView {msg} —— 棋子不会显示", this);
    }

    [ContextMenu("创建显示物体")]
    void CreateVisual()
    {
        if (spriteRenderer != null) return;

        var go = new GameObject("Visual");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        spriteRenderer = go.AddComponent<SpriteRenderer>();
    }

    void EnsureVisual()
    {
        if (spriteRenderer == null) CreateVisual();
    }

    void ApplySprite()
    {
        if (spriteRenderer == null) return;

        Sprite want = sprite != null ? sprite : SpriteFactory.WhiteDisc;
        if (spriteRenderer.sprite != want)
        {
            spriteRenderer.sprite = want;
            _applied = null;
        }

        spriteRenderer.color = sprite != null ? tint : placeholderColor;
        spriteRenderer.sortingOrder = sortingOrder;

        if (autoFitToCell && _applied != spriteRenderer.sprite)
        {
            _applied = spriteRenderer.sprite;
            FitToCell();
        }
    }

    void FitToCell()
    {
        Sprite s = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (s == null || agent == null || agent.map == null) return;

        float w = s.bounds.size.x;
        if (w < 0.0001f) return;

        spriteRenderer.transform.localScale =
            Vector3.one * (agent.map.cellSize * fitRatio / w);
    }
}