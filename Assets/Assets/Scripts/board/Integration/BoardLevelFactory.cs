using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>把现有导表关卡数据转换成 B 部分输入，不依赖 UI，也不修改配置资产。</summary>
    public static class BoardLevelFactory
    {
        private static readonly ElementType[] Elements =
        {
            ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.ground
        };

        public static BoardStageSession Create(LevelConfig level, BoardLayoutConfig layout)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            BoardConfig config = Resources.Load<BoardConfig>("Configs/Boards/BoardConfig_" + level.BoardId);
            if (config == null) throw new InvalidOperationException("找不到棋盘配置：BoardConfig_" + level.BoardId);
            BoardInitialLayoutResult initial;
            string error;
            if (!BoardInitialLayoutGenerator.TryGenerate(config.Width, config.Height, layout, out initial, out error))
                throw new InvalidOperationException(error);

            SpawnRules rules = CreateSpawnRules(level.LevelId, config.Width, config.Height);
            for (int i = 0; i < level.InitPieceCount; i++)
            {
                SpawnResult spawn = SpawnResolver.TrySpawnOne(initial.Board, rules, SpawnContext.Neutral);
                if (!spawn.Success)
                    throw new InvalidOperationException("第 " + (i + 1) + " 个初始棋子生成失败：" + spawn.FailureReason);
            }
            return new BoardStageSession(initial.Board, level.InitSteps, rules, SpawnContext.Neutral);
        }

        /// <summary>
        /// 表中区域使用包含端点的矩形；高优先级覆盖低优先级，而 B 部分区域权重相乘。
        /// 先为每格选出唯一有效区域，再生成互不重叠的权重规则，避免把覆盖误做成相乘。
        /// 尚未接入环境状态的条件区域不启用。无区域的关卡临时使用全盘默认权重。
        /// </summary>
        public static SpawnRules CreateSpawnRules(int levelId, int width, int height)
        {
            var zones = new List<RefreshZoneConfig>();
            foreach (RefreshZoneConfig zone in Resources.LoadAll<RefreshZoneConfig>("Configs/Zones"))
            {
                if (zone.LevelId != levelId) continue;
                if (!string.IsNullOrWhiteSpace(zone.StateCondition))
                {
                    Debug.LogWarning("棋盘暂不启用条件刷新区域 " + zone.ZoneId + "（" + zone.StateCondition +
                                     "）：环境状态系统尚未接入。", zone);
                    continue;
                }
                zones.Add(zone);
            }
            zones.Sort((a, b) =>
            {
                int priority = b.Priority.CompareTo(a.Priority);
                return priority != 0 ? priority : a.ZoneId.CompareTo(b.ZoneId);
            });
            var areas = new List<SpawnArea>();
            var weights = new List<RegionWeightRule>();
            if (zones.Count == 0)
            {
                areas.Add(new SpawnArea(0, 0, width, height));
                return new SpawnRules(areas.ToArray(), Elements, DefaultElementWeights.Create(), weights.ToArray());
            }
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                foreach (RefreshZoneConfig zone in zones)
                {
                    if (x < zone.RectX1 || x > zone.RectX2 || y < zone.RectY1 || y > zone.RectY2) continue;
                    var area = new SpawnArea(x, y, 1, 1);
                    areas.Add(area);
                    weights.Add(new RegionWeightRule(area, ReadWeights(zone)));
                    break;
                }
            }
            if (areas.Count == 0) throw new InvalidOperationException("刷新区域没有覆盖棋盘：关卡 " + levelId);
            return new SpawnRules(areas.ToArray(), Elements, new ElementWeights(1, 1, 1, 1), weights.ToArray());
        }

        private static ElementWeights ReadWeights(RefreshZoneConfig zone)
        {
            float[] values = new float[4];
            foreach (ZoneElemWeightEntry entry in zone.Weights)
            {
                if (entry.ElemId < 1 || entry.ElemId > 4 || entry.Weight < 0)
                    throw new InvalidOperationException("刷新区域 " + zone.ZoneId + " 的元素/权重无效。");
                values[entry.ElemId - 1] = entry.Weight;
            }
            return new ElementWeights(values[0], values[1], values[2], values[3]);
        }
    }
}
