using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitView : MonoBehaviour
{
    [Header("引用")]
    public PathAgent agent;

    [Tooltip("留空会自动创建子物体来显示")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("留空则用自动生成的占位圆点，方便美术资源到位前先调逻辑")]
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

    Sprite _applied;
    bool _warned;

    void Awake()
    {
        EnsureVisual();
        if (agent == null) Warn("agent 未指定");
    }

    void Start() => Snap();

    void Update()
    {
        if (agent == null || agent.map == null)
        {
            Warn("agent 或 agent.map 未指定");
            return;
        }

        EnsureVisual();
        ApplySprite();
        Follow();
    }

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

    void Follow()
    {
        Vector3 target = agent.map.CellCenterWorld(agent.GridPos)
                       + new Vector3(0f, 0f, zOffset);
        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        spriteRenderer.transform.position =
            Vector3.Lerp(spriteRenderer.transform.position, target, k);
    }

    void Snap()
    {
        if (agent == null || agent.map == null || spriteRenderer == null) return;
        spriteRenderer.transform.position =
            agent.map.CellCenterWorld(agent.GridPos) + new Vector3(0f, 0f, zOffset);
    }
}
