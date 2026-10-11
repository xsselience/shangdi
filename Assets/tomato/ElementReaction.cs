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
    [SerializeField] private int forestBurnRange = 1;
    [SerializeField] private int fireSpreadLimit;

    public int WaterHealAmount { get { return waterHealAmount; } }
    public bool EarthBlocksPath { get { return earthBlocksPath; } }
    public int WindLiftHeight { get { return windLiftHeight; } }
    public int ForestBurnRange { get { return forestBurnRange; } }
    public int FireSpreadLimit { get { return fireSpreadLimit; } }

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

    public void HandleUnitStep(PathAgent agent)
    {
        if (agent == null || agent.map == null) return;

        switch (agent.map.GetSpecial(agent.GridPos))
        {
            case SpecialElement.Water:
                ApplyWaterReaction(agent);
                break;
            case SpecialElement.Fire:
                ApplyFireReaction(agent.map, agent.GridPos);
                break;
        }
    }

    private void ApplyWaterReaction(PathAgent agent)
    {
        UnitHealth health = agent.GetComponent<UnitHealth>();
        if (health != null) health.Heal(waterHealAmount);
    }

    private bool IsEarthPathBlocked(Gridmap map, Vector2Int position)
    {
        return earthBlocksPath && map.GetSpecial(position) == SpecialElement.Ground;
    }

    private void ApplyFireReaction(Gridmap map, Vector2Int position)
    {
    }

    private void SpreadFire(Gridmap map, Vector2Int position, int remainingSpread)
    {
    }
}
