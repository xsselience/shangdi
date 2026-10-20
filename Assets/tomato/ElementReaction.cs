using System;
using UnityEngine;

[Serializable]
public class ElementReaction
{
    [Header("水元素")]
    [SerializeField] private int waterHealAmount = 20;

    [Header("土元素")]
    [SerializeField] private bool earthBlocksPath = true;

    [Header("风元素")]
    [SerializeField] private int windLiftHeight = 1;

    [Header("火元素")]
    [SerializeField] private int fireDamage = 20;

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
        if (map == null || !map.InBounds(to)) return false;

        if (map.GetSpecial(to) == SpecialElement.Wind)
        {
            nextElevation = Mathf.Max(1, windLiftHeight);
            return true;
        }

        bool wall = map.Get(to) == TileType.Wall;
        bool earth = IsEarthPathBlocked(map, to);

        if (wall || earth)
        {
            int requiredElevation = Mathf.Max(1, windLiftHeight);
            if (currentElevation < requiredElevation) return false;

            nextElevation = currentElevation;
            return true;
        }

        return true;
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

    private bool IsEarthPathBlocked(Gridmap map, Vector2Int position)
    {
        return earthBlocksPath && map.GetSpecial(position) == SpecialElement.Ground;
    }
}