using System;
using UnityEngine;

[Serializable]
public class ElementReaction
{
    [Header("水元素")]
    [SerializeField] private int waterHealAmount = 25;

    [Header("土元素")]
    [SerializeField] private bool earthBlocksPath = true;

    [Header("风元素")]
    [SerializeField] private int windLiftHeight = 1;

    [Header("火元素")]
    [SerializeField] private int fireDamage = 25;

    [Header("调试")]
    [Tooltip("勾上会在 Console 打印每一次移动的高度判定")]
    [SerializeField] private bool logElevation = false;

    public int WaterHealAmount { get { return waterHealAmount; } }
    public bool EarthBlocksPath { get { return earthBlocksPath; } }
    public int WindLiftHeight { get { return windLiftHeight; } }
    public int FireDamage { get { return fireDamage; } }

    public int GetStartingElevation(Gridmap map, Vector2Int position)
    {
        return map != null && map.GetSpecial(position) == SpecialElement.Wind
            ? Mathf.Max(1, windLiftHeight)
            : 0;
    }

    public bool TryGetNextElevation(Gridmap map, Vector2Int from, int currentElevation,
        Vector2Int to, out int nextElevation)
    {
        nextElevation = 0;

        bool ok = Evaluate(map, from, currentElevation, to, out nextElevation);

        if (logElevation)
            Debug.Log($"[高度] {from}(e{currentElevation}) → {to}(e{nextElevation}) " +
                      $"{(ok ? "放行" : "拦截")}");

        return ok;
    }

    private bool Evaluate(Gridmap map, Vector2Int from, int currentElevation,
        Vector2Int to, out int nextElevation)
    {
        nextElevation = 0;
        if (map == null || !map.InBounds(to)) return false;

        // ① 目标是风格 → 抬升
        if (map.GetSpecial(to) == SpecialElement.Wind)
        {
            nextElevation = Mathf.Max(1, windLiftHeight);
            return true;
        }

        bool targetIsBlocker = IsBlocker(map, to);

        // ② 目标是普通格 → 从哪儿来都能走，高度归 0（这就是"下来"）
        if (!targetIsBlocker)
        {
            nextElevation = 0;
            return true;
        }

        // ③ 目标是高台（山脉 / 土）
        //    站在高台上时天然有高度；站在普通格上则需要靠风抬起来
        bool standingOnBlocker = IsBlocker(map, from);
        int required = Mathf.Max(1, windLiftHeight);

        if (!standingOnBlocker && currentElevation < required) return false;

        nextElevation = Mathf.Max(currentElevation, required);
        return true;
    }

    private bool IsBlocker(Gridmap map, Vector2Int c)
    {
        if (map == null || !map.InBounds(c)) return true;
        if (map.Get(c) == TileType.Wall) return true;
        return earthBlocksPath && map.GetSpecial(c) == SpecialElement.Ground;
    }

    private bool IsEarthPathBlocked(Gridmap map, Vector2Int position)
    {
        return earthBlocksPath && map.GetSpecial(position) == SpecialElement.Ground;
    }

    /// <summary>走上一格之后触发的效果。由 PathAgent 转调，BattleFlow 决定什么时候调。</summary>
    public void HandleUnitStep(PathAgent agent)
    {
        if (agent == null || agent.map == null) return;

        switch (agent.map.GetSpecial(agent.GridPos))
        {
            case SpecialElement.Water:
                ApplyWaterReaction(agent);
                break;

            case SpecialElement.Fire:
                ApplyFireReaction(agent);
                break;
        }
    }

    private void ApplyWaterReaction(PathAgent agent)
    {
        UnitHealth health = agent.GetComponent<UnitHealth>();
        if (health != null) health.Heal(waterHealAmount);
    }

    private void ApplyFireReaction(PathAgent agent)
    {
        UnitHealth health = agent.GetComponent<UnitHealth>();
        if (health != null) health.TakeDamage(fireDamage, "特殊火");
    }

}