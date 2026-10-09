using System;
using UnityEngine;

namespace Game.Board
{
    /// <summary>一局棋盘阶段的设置资源，不保存运行中状态。</summary>
    [CreateAssetMenu(fileName = "BoardStageConfig", menuName = "Game/Board/Stage Config")]
    public sealed class BoardStageConfig : ScriptableObject
    {
        [Header("棋盘与测试初始棋子（不是关卡难度定案）")]
        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 10;
        [Tooltip("新建空棋盘时，按严格刷新规则生成的普通棋子数量。外部传入已准备棋盘时不使用此项。")]
        [SerializeField, Min(0)] private int initialPieceCount = 10;
        [SerializeField, Min(0)] private int stepLimit = 20;
        [Tooltip("同一配置和种子复现同一初始棋盘。初始生成和回合刷新使用不同随机流。")]
        [SerializeField] private int seed = 12345;
        [Header("初始棋盘布局")]
        [SerializeField] private BoardLayoutConfig layout = new BoardLayoutConfig();
        [Header("已有的规则资源")]
        [SerializeField] private SpawnProfile spawnProfile;

        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public int InitialPieceCount { get { return initialPieceCount; } }
        public int StepLimit { get { return stepLimit; } }
        public int Seed { get { return seed; } }
        public BoardLayoutConfig Layout { get { return layout; } }
        public SpawnProfile SpawnProfile { get { return spawnProfile; } }

        public void Validate()
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("棋盘宽高必须大于 0。");
            if (initialPieceCount < 0 || stepLimit < 0) throw new ArgumentException("初始棋子数和步数不能小于 0。");
            if (initialPieceCount > (long)width * height) throw new ArgumentException("初始棋子数超过棋盘格子总数。");
            if (layout == null) throw new ArgumentException("请在阶段配置中指定初始棋盘布局配置。");
            layout.Validate(width, height);
            if (spawnProfile == null) throw new ArgumentException("请给阶段配置指定 SpawnProfile 刷新规则资源。");
        }
    }
}
