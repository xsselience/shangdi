// B部分刷新配置、环境影响快照和刷新执行。
// 原有多个脚本已合并；公开类型名称保持不变。
using System;
using System.Collections.Generic;
using UnityEngine;


// ===== 原文件：SpawnProfile.cs =====
namespace Game.Board
{
    /// <summary>整数矩形，左下角包含，右上边界不包含。</summary>
    [Serializable]
    public struct SpawnArea
    {
        public int x;
        public int y;
        [Min(1)] public int width;
        [Min(1)] public int height;

        public SpawnArea(int x, int y, int width, int height)
        {
            this.x = x; this.y = y; this.width = width; this.height = height;
        }

        public bool Contains(GridCoord coord)
        {
            return coord.X >= x && coord.Y >= y &&
                   (long)coord.X < (long)x + width &&
                   (long)coord.Y < (long)y + height;
        }

        internal void Validate()
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("刷新矩形的宽高必须大于 0。");
        }
    }

    /// <summary>
    /// 默认元素权重配置。直接修改这些数值即可改变元素生成概率。
    /// 权重值越大，该元素生成概率越高。0 表示不生成该元素。
    /// 示例：火40% 水30% 风20% 土10% → 使用 (40, 30, 20, 10)
    /// </summary>
    public static class DefaultElementWeights
    {
        // ===== 默认配置：四元素均匀分布（各25%） =====
        public const float Fire = 1.0f;    // 火属性权重（修改这里改变火元素生成概率）
        public const float Water = 1.0f;   // 水属性权重（修改这里改变水元素生成概率）
        public const float Wind = 1.0f;    // 风属性权重（修改这里改变风元素生成概率）
        public const float Ground = 1.0f;  // 土属性权重（修改这里改变土元素生成概率）

        // ===== 预设配置示例（取消注释使用） =====

        // 火焰关卡（火60% 水20% 风10% 土10%）
        // public const float Fire = 6.0f;
        // public const float Water = 2.0f;
        // public const float Wind = 1.0f;
        // public const float Ground = 1.0f;

        // 水域关卡（水60% 火10% 风20% 土10%）
        // public const float Fire = 1.0f;
        // public const float Water = 6.0f;
        // public const float Wind = 2.0f;
        // public const float Ground = 1.0f;

        // 只生成火和水（火50% 水50% 风0% 土0%）
        // public const float Fire = 1.0f;
        // public const float Water = 1.0f;
        // public const float Wind = 0.0f;
        // public const float Ground = 0.0f;

        /// <summary>创建默认权重配置</summary>
        public static ElementWeights Create()
        {
            return new ElementWeights(Fire, Water, Wind, Ground);
        }
    }

    /// <summary>四属性数值；用于基础权重或倍率。0 表示不出现。</summary>
    [Serializable]
    public struct ElementWeights
    {
        [Min(0)] public float fire;
        [Min(0)] public float water;
        [Min(0)] public float wind;
        [Min(0)] public float ground;

        public ElementWeights(float fire, float water, float wind, float ground)
        {
            this.fire = fire; this.water = water; this.wind = wind; this.ground = ground;
        }

        public double Get(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fire;
                case ElementType.Water: return water;
                case ElementType.Wind: return wind;
                case ElementType.ground: return ground;
                default: throw new ArgumentOutOfRangeException(nameof(element));
            }
        }

        internal void Validate()
        {
            ValidateValue(fire); ValidateValue(water); ValidateValue(wind); ValidateValue(ground);
        }

        private static void ValidateValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentException("权重和倍率必须是有限的非负数。");
        }
    }

    [Serializable]
    public struct RegionWeightRule
    {
        public SpawnArea area;
        [Tooltip("区域属性倍率；1 不变，2 加倍，0 禁止。重叠区域的倍率相乘。")]
        public ElementWeights multipliers;

        public RegionWeightRule(SpawnArea area, ElementWeights multipliers)
        {
            this.area = area; this.multipliers = multipliers;
        }
    }

    /// <summary>刷新配置模板。运行时用 CreateRules 获取独立快照，不修改资源。</summary>
    [CreateAssetMenu(fileName = "SpawnProfile", menuName = "Game/Board/Spawn Profile")]
    public sealed class SpawnProfile : ScriptableObject
    {
        [Tooltip("矩形取并集。只在这些区域内刷新；越过棋盘的部分自动忽略。")]
        [SerializeField] private SpawnArea[] areas = { new SpawnArea(0, 0, 10, 10) };

        [Tooltip("硬性属性白名单。空列表代表没有允许的属性，而不是允许全部。")]
        [SerializeField] private ElementType[] allowedElements =
        { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.ground };

        [SerializeField] private ElementWeights baseWeights = new ElementWeights(1, 1, 1, 1);
        [SerializeField] private RegionWeightRule[] regionWeights = new RegionWeightRule[0];

        public SpawnRules CreateRules()
        {
            return new SpawnRules(areas, allowedElements, baseWeights, regionWeights);
        }

        /// <summary>
        /// 运行时初始化默认配置（用于代码中动态创建SpawnProfile）
        /// </summary>
        public void InitializeDefaults(int boardWidth = 10, int boardHeight = 10)
        {
            areas = new SpawnArea[] { new SpawnArea(0, 0, boardWidth, boardHeight) };
            allowedElements = new ElementType[]
            {
                ElementType.Fire,
                ElementType.Water,
                ElementType.Wind,
                ElementType.ground
            };
            baseWeights = DefaultElementWeights.Create();
            regionWeights = new RegionWeightRule[0];
        }
    }

    /// <summary>纯配置快照，可在不创建 Unity 对象的情况下测试刷新规则。</summary>
    public sealed class SpawnRules
    {
        private readonly SpawnArea[] areas;
        private readonly HashSet<ElementType> allowedElements;
        private readonly ElementWeights baseWeights;
        private readonly RegionWeightRule[] regionWeights;

        public SpawnRules(SpawnArea[] areas, ElementType[] allowedElements,
            ElementWeights baseWeights, RegionWeightRule[] regionWeights)
        {
            if (areas == null || areas.Length == 0)
                throw new ArgumentException("必须指定至少一个刷新矩形。", nameof(areas));
            if (allowedElements == null) throw new ArgumentNullException(nameof(allowedElements));
            if (regionWeights == null) throw new ArgumentNullException(nameof(regionWeights));
            this.areas = (SpawnArea[])areas.Clone();
            this.allowedElements = new HashSet<ElementType>(allowedElements);
            this.baseWeights = baseWeights;
            this.regionWeights = (RegionWeightRule[])regionWeights.Clone();
            foreach (SpawnArea area in this.areas) area.Validate();
            baseWeights.Validate();
            foreach (ElementType element in this.allowedElements)
                if (element != ElementType.Fire && element != ElementType.Water &&
                    element != ElementType.Wind && element != ElementType.ground)
                    throw new ArgumentException("白名单只能包含火、水、风、土。");
            foreach (RegionWeightRule rule in this.regionWeights)
            {
                rule.area.Validate();
                rule.multipliers.Validate();
            }
        }

        public bool Contains(GridCoord coord)
        {
            foreach (SpawnArea area in areas) if (area.Contains(coord)) return true;
            return false;
        }

        public double GetWeight(GridCoord coord, ElementType element)
        {
            if (!allowedElements.Contains(element)) return 0;
            double weight = baseWeights.Get(element);
            foreach (RegionWeightRule rule in regionWeights)
                if (rule.area.Contains(coord)) weight *= rule.multipliers.Get(element);
            return weight;
        }
    }
}

// ===== 原文件：SpawnContext.cs =====
namespace Game.Board
{
    /// <summary>
    /// 外部环境系统提供的当前刷新影响快照。
    /// 不保存 GameObject，不自行推断水火风土的环境效果。
    /// 使用白名单交集和权重倍率，不能恢复关卡已禁止的属性。
    /// </summary>
    public sealed class SpawnContext
    {
        public static readonly SpawnContext Neutral = new SpawnContext();

        private readonly ElementWeights globalMultipliers;
        private readonly RegionWeightRule[] regionalMultipliers;
        private readonly HashSet<ElementType> allowedElements;

        /// <param name="allowedElements">null 表示不额外限制；空数组表示全部禁止。</param>
        public SpawnContext(ElementWeights? globalMultipliers = null,
            RegionWeightRule[] regionalMultipliers = null,
            ElementType[] allowedElements = null)
        {
            this.globalMultipliers = globalMultipliers ?? new ElementWeights(1, 1, 1, 1);
            this.globalMultipliers.Validate();
            this.regionalMultipliers = regionalMultipliers == null
                ? new RegionWeightRule[0] : (RegionWeightRule[])regionalMultipliers.Clone();
            foreach (RegionWeightRule rule in this.regionalMultipliers)
            {
                rule.area.Validate();
                rule.multipliers.Validate();
            }
            if (allowedElements != null)
            {
                this.allowedElements = new HashSet<ElementType>(allowedElements);
                foreach (ElementType element in this.allowedElements)
                    if (element != ElementType.Fire && element != ElementType.Water &&
                        element != ElementType.Wind && element != ElementType.ground)
                        throw new ArgumentException("环境白名单只能包含火、水、风、土。");
            }
        }

        public double GetMultiplier(GridCoord coord, ElementType element)
        {
            if (allowedElements != null && !allowedElements.Contains(element)) return 0;
            double value = globalMultipliers.Get(element);
            foreach (RegionWeightRule rule in regionalMultipliers)
                if (rule.area.Contains(coord)) value *= rule.multipliers.Get(element);
            return value;
        }
    }
}

// ===== 原文件：SpawnResolver.cs =====
namespace Game.Board
{
    public enum SpawnFailureReason
    {
        None = 0,
        // 没有空的、可操作且无障碍的格子。不等同于整个二维数组被填满。
        NoAvailableBoardCell = 1,
        // 棋盘有空位，但指定范围和刷新权限内没有空位。
        NoSpawnableCell = 2,
        // 范围内有空位，但所有有效属性权重都是 0。
        NoLegalAttribute = 3,
        // 倍率连乘溢出等无效配置。
        InvalidWeight = 4
    }

    public sealed class SpawnResult
    {
        public bool Success { get; private set; }
        public SpawnFailureReason FailureReason { get; private set; }
        public GridCoord Coord { get; private set; }
        public PieceState Piece { get; private set; }
        public int CandidateCount { get; private set; }
        /// <summary>是否进入了全棋盘随机位置兜底；失败时也可为 true。</summary>
        public bool UsedFallback { get; private set; }

        internal SpawnResult(bool success, SpawnFailureReason failureReason,
            GridCoord coord, PieceState piece, int candidateCount, bool usedFallback = false)
        {
            Success = success; FailureReason = failureReason;
            Coord = coord; Piece = piece; CandidateCount = candidateCount;
            UsedFallback = usedFallback;
        }
    }

    /// <summary>
    /// 初始生成和回合刷新共用的普通棋子生成算法。
    /// 先均匀选择合法格，再按该格权重选择属性。
    /// 不移动、不合成、不扣步、不自行结束阶段。
    /// </summary>
    public static class SpawnResolver
    {
        private static readonly ElementType[] Elements =
        { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.ground };

        private sealed class Candidate
        {
            public GridCoord Coord;
            public double[] Weights;
            public double Total;
        }

        public static SpawnResult TrySpawnOne(BoardState board,
            SpawnProfile profile, SpawnContext context = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return TrySpawnOne(board, profile.CreateRules(), context);
        }

        /// <summary>
        /// 玩家移动后需要刷新时使用：先按指定范围刷新，范围内无合法候选格
        /// 再在全棋盘合法空格中随机选位置。只放宽矩形范围，不放宽格子权限、
        /// 障碍、占用、属性白名单、属性权重及环境限制。
        /// 不负责判断本次移动是否合成；成功合成的回合不要调用此方法。
        /// 初始棋盘生成仍使用 TrySpawnOne，不自动启用这个兜底。
        /// </summary>
        public static SpawnResult TrySpawnAfterMove(BoardState board,
            SpawnProfile profile, SpawnContext context = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return TrySpawnAfterMove(board, profile.CreateRules(), context);
        }

        public static SpawnResult TrySpawnAfterMove(BoardState board,
            SpawnRules rules, SpawnContext context = null)
        {
            SpawnResult result = TrySpawnOne(board, rules, context);
            if (result.Success ||
                (result.FailureReason != SpawnFailureReason.NoSpawnableCell &&
                 result.FailureReason != SpawnFailureReason.NoLegalAttribute))
                return result;

            return TrySpawnCore(board, rules, context, true);
        }

        /// <summary>
        /// 成功时只添加一颗普通棋子。
        /// 普通失败时不修改棋盘。
        /// Coord 和 Piece 只在 Success 为 true 时有效。
        /// </summary>
        public static SpawnResult TrySpawnOne(BoardState board,
            SpawnRules rules, SpawnContext context = null)
        {
            return TrySpawnCore(board, rules, context, false);
        }

        private static SpawnResult TrySpawnCore(BoardState board,
            SpawnRules rules, SpawnContext context, bool useFallback)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (context == null) context = SpawnContext.Neutral;

            List<Candidate> candidates = new List<Candidate>();
            bool hasBoardSpace = false;
            bool hasSpawnSpace = false;

            // 顺序固定；矩形重叠不会重复添加一个格子。
            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                GridCoord coord = new GridCoord(x, y);
                CellState cell = board.GetCell(coord);
                if (!cell.CanOperate || cell.HasObstacle || cell.HasPiece) continue;
                hasBoardSpace = true;
                if (!cell.CanSpawn || (!useFallback && !rules.Contains(coord))) continue;
                hasSpawnSpace = true;

                double[] weights = new double[Elements.Length];
                double total = 0;
                for (int i = 0; i < Elements.Length; i++)
                {
                    double baseWeight = rules.GetWeight(coord, Elements[i]);
                    if (!IsValidWeight(baseWeight)) return Failure(SpawnFailureReason.InvalidWeight, useFallback);
                    // 硬限制禁止的属性不会因环境倍率而重新出现。
                    if (baseWeight == 0) continue;
                    double multiplier = context.GetMultiplier(coord, Elements[i]);
                    if (!IsValidWeight(multiplier)) return Failure(SpawnFailureReason.InvalidWeight, useFallback);
                    double weight = baseWeight * multiplier;
                    if (!IsValidWeight(weight)) return Failure(SpawnFailureReason.InvalidWeight, useFallback);

                    // 检查：生成这个属性的棋子是否会立即形成五连
                    if (WouldFormSynthesis(board, coord, Elements[i]))
                    {
                        weight = 0; // 禁止在此位置生成此属性
                    }

                    weights[i] = weight;
                    total += weight;
                }
                if (!IsValidWeight(total)) return Failure(SpawnFailureReason.InvalidWeight, useFallback);
                if (total <= 0) continue;
                candidates.Add(new Candidate { Coord = coord, Weights = weights, Total = total });
            }

            if (!hasBoardSpace) return Failure(SpawnFailureReason.NoAvailableBoardCell, useFallback);
            if (!hasSpawnSpace) return Failure(SpawnFailureReason.NoSpawnableCell, useFallback);
            if (candidates.Count == 0) return Failure(SpawnFailureReason.NoLegalAttribute, useFallback);

            // 使用Unity的Random系统
            Candidate chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            double ticket = UnityEngine.Random.value * chosen.Total;
            double accumulated = 0;
            int selectedIndex = -1;
            int lastPositiveIndex = -1;
            for (int i = 0; i < Elements.Length; i++)
            {
                if (chosen.Weights[i] <= 0) continue;
                lastPositiveIndex = i;
                accumulated += chosen.Weights[i];
                if (ticket < accumulated) { selectedIndex = i; break; }
            }
            // 浮点舍入兜底：只选最后一个正权重属性，不突破白名单。
            if (selectedIndex < 0) selectedIndex = lastPositiveIndex;
            PieceState piece = board.CreatePieceAt(chosen.Coord,
                Elements[selectedIndex], PieceKind.Normal);
            return new SpawnResult(true, SpawnFailureReason.None,
                chosen.Coord, piece, candidates.Count, useFallback);
        }

        private static bool IsValidWeight(double weight)
        {
            return !double.IsNaN(weight) && !double.IsInfinity(weight) && weight >= 0;
        }

        private static SpawnResult Failure(SpawnFailureReason reason, bool usedFallback)
        {
            return new SpawnResult(false, reason, default(GridCoord), null, 0, usedFallback);
        }

        /// <summary>
        /// 检查在指定位置生成指定属性的棋子是否会立即形成五连。
        /// 临时创建棋子进行检测，检测后立即移除。
        /// </summary>
        private static bool WouldFormSynthesis(BoardState board, GridCoord coord, ElementType element)
        {
            // 临时创建一个普通棋子
            PieceState tempPiece = board.CreatePieceAt(coord, element, PieceKind.Normal);

            // 检查是否形成五连
            bool hasFiveLine = SynthesisResolver.HasFiveLine(board, coord);

            // 立即移除临时棋子
            board.RemovePieceAt(coord);

            return hasFiveLine;
        }
    }
}

