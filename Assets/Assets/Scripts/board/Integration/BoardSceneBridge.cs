using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Board
{
    /// <summary>单个阶段的配置。五个阶段的步数、名字都在这里改。</summary>
    [Serializable]
    public sealed class BoardStageSlot
    {
        [Tooltip("阶段名，只用于显示和日志")]
        public string name = "阶段";

        [Tooltip("这个阶段的步数。小于 0 表示沿用关卡表里的 init_steps")]
        public int steps = -1;

        [Tooltip("关掉则跳过这个阶段")]
        public bool enabled = true;

        [Header("刷新权重")]
        [Tooltip("勾上才用下面四个数；不勾则沿用关卡表里的权重")]
        public bool useCustomWeights = false;

        public int weightFire = 1;
        public int weightWater = 1;
        public int weightWind = 1;
        public int weightGround = 1;
    }

    [Serializable]
    public sealed class StageIndexEvent : UnityEvent<int> { }

    /// <summary>
    /// 现有 SubScreen/ChessBoard 的连接层，同时负责棋盘阶段的推进。
    /// 步数的"配置"和"用完后进入下一阶段"都在这里；扣步仍然由 BoardStageSession 负责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardSceneBridge : MonoBehaviour
    {
        [Header("现有 UI 引用")]
        [SerializeField] private TMP_Text levelIdText;
        [SerializeField] private TMP_Text stepsText;
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private RectTransform gridRoot;

        [Header("临时测试布局（正式关卡障碍配置导入后替换）")]
        [SerializeField]
        private BoardLayoutConfig testLayout = new BoardLayoutConfig(
            new[] { new Vector2Int(3, 3), new Vector2Int(6, 6), new Vector2Int(3, 6), new Vector2Int(6, 3) },
            new Vector2Int(0, 0), new Vector2Int(9, 9));
        [Header("外观")]
        [Tooltip("棋子图占一格的比例。1 = 铺满整格")]
        [Range(0.3f, 1f)] public float pieceFillRatio = 0.86f;

        // ═══════════════════════════════════════════════════════
        //  五个阶段
        //  steps 留 -1 就用关卡表的 init_steps；填 0 或正数就覆盖
        // ═══════════════════════════════════════════════════════
        [Header("阶段配置（暂定 5 个）")]
        [SerializeField]
        private BoardStageSlot[] stages = new[]
        {
            new BoardStageSlot { name = "阶段 1" },
            new BoardStageSlot { name = "阶段 2" },
            new BoardStageSlot { name = "阶段 3" },
            new BoardStageSlot { name = "阶段 4" },
            new BoardStageSlot { name = "阶段 5" },
        };

        [Serializable]
        public sealed class BoardStageConfig
        {
            [Tooltip("阶段名，只用于显示和日志")]
            public string name = "阶段";

            [Tooltip("这个阶段的步数。小于 0 表示沿用关卡表里的 init_steps")]
            public int steps = -1;

            [Tooltip("关掉则跳过这个阶段")]
            public bool enabled = true;

            // ══ 刷新权重。四个都填 -1 表示沿用关卡表；填了任意一个就整盘按这四个来 ══
            [Header("刷新权重（-1 = 用关卡表）")]
            public int weightFire = -1;
            public int weightWater = -1;
            public int weightWind = -1;
            public int weightGround = -1;
        }
        private AudioSource _sfx;

        [Tooltip("关掉后步数用完不会自动推进，需要外部手动调用 AdvanceStage()")]
        [SerializeField] private bool autoAdvanceStages = true;

        [Header("阶段事件（不用改代码就能挂钩）")]
        [Tooltip("每进入一个阶段时触发，参数是阶段下标（0 起）")]
        public StageIndexEvent onStageBegin;

        [Header("阶段结束时的对话（按阶段顺序，留空则该阶段不播）")]
        [Tooltip("用来播放对话的 IntroDialogue")]
        public IntroDialogue stageDialoguePlayer;

        [Tooltip("五个阶段全部走完时触发")]
        public UnityEvent onAllStagesCleared;

        [Header("可选正式棋子美术；未指定时用颜色和文字")]
        [SerializeField] private Sprite fireSprite;
        [SerializeField] private Sprite waterSprite;
        [SerializeField] private Sprite windSprite;
        [SerializeField] private Sprite groundSprite;

        [Header("特殊棋子美术（留空则用普通图 + 「特」字）")]
        [SerializeField] private Sprite fireSpecialSprite;
        [SerializeField] private Sprite waterSpecialSprite;
        [SerializeField] private Sprite windSpecialSprite;
        [SerializeField] private Sprite groundSpecialSprite;

        [Header("合成音效（留空则不播）")]
        [SerializeField] private AudioClip fireSynthesisClip;
        [SerializeField] private AudioClip waterSynthesisClip;
        [SerializeField] private AudioClip windSynthesisClip;
        [SerializeField] private AudioClip groundSynthesisClip;

        [Range(0f, 1f)]
        [SerializeField] private float synthesisVolume = 0.8f;

        [Header("棋面美术")]
        [Tooltip("普通状态。选中棋子后，走不到的格子用这张")]
        public Sprite cellNormalSprite;

        [Header("棋面：A / B 交叉显示")]
        [Tooltip("A 面 · 高亮（正常状态）")]
        [SerializeField] private Sprite cellASprite;

        [Tooltip("A 面 · 暗淡（选中棋子后走不到的格子）")]
        [SerializeField] private Sprite cellADimSprite;

        [Tooltip("B 面 · 高亮")]
        [SerializeField] private Sprite cellBSprite;

        [Tooltip("B 面 · 暗淡")]
        [SerializeField] private Sprite cellBDimSprite;

        [Header("状态染色（乘在棋面图上，纯白 = 原色）")]
        [SerializeField] private Color cellSelectedTint = new Color(1f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color cellBlockedTint = new Color(0.35f, 0.38f, 0.45f, 1f);

        [Tooltip("有障碍 / 不可操作的格子。留空则用普通图")]
        public Sprite cellBlockedSprite;

        [Header("第二棋盘 → 第一棋盘障碍")]
        [Tooltip("留空则不启用这个规则")]
        public Gridmap worldMap;

        [Header("保留棋子留下的 × 的颜色")]
        public Color fireCrossColor = new Color(1f, 0.35f, 0.30f);
        public Color waterCrossColor = new Color(0.35f, 0.68f, 1f);
        public Color windCrossColor = new Color(0.42f, 0.95f, 0.72f);
        public Color groundCrossColor = new Color(1f, 0.78f, 0.35f);

        readonly Dictionary<Vector2Int, SpecialElement> _retainedAt =
       new Dictionary<Vector2Int, SpecialElement>();

        private sealed class CellView
        {
            public GameObject Root;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image Piece;
            public TMP_Text Label;
            public bool Odd;
            public Vector2Int Cell;
        }

        private readonly Dictionary<GridCoord, CellView> views = new Dictionary<GridCoord, CellView>();
        private UnityEngine.UI.GridLayoutGroup layout;
        private GameEventCenter events;
        private LevelConfig currentLevel;
        private int currentStageIndex;
        private bool hasSelection;
        private GridCoord selected;
        private bool processing;
        private bool previousInput;
        private Vector2 previousSize = new Vector2(-1, -1);

        public BoardStageSession Session { get; private set; }
        public BoardState Board { get { return Session == null ? null : Session.Board; } }
        public bool HasSelection { get { return hasSelection; } }
        public GridCoord SelectedCoord { get { return selected; } }
        public int CurrentStageIndex { get { return currentStageIndex; } }
        public int StageCount { get { return stages == null ? 0 : stages.Length; } }

        public bool CanInteract
        {
            get
            {
                return isActiveAndEnabled && !processing && Session != null &&
                       Session.Phase == BoardStagePhase.Operating && GameManager.Instance != null &&
                       GameManager.Instance.CurrentState == GameState.Playing;
            }
        }

        public event Action<BoardTurnResult> TurnCompleted;
        public event Action<BoardSettlementResult> StageSettled;

        private void Awake()
        {
            if (gridRoot == null || levelIdText == null || stepsText == null)
            {
                Debug.LogError("BoardSceneBridge 缺少现有棋盘容器或文本引用。", this);
                enabled = false;
                return;
            }
            layout = gridRoot.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (layout == null) layout = gridRoot.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        }

        private void OnEnable()
        {
            events = GameEventCenter.Instance;
            if (events != null)
            {
                events.LevelChanged += OnLevelChanged;
                events.GameStateChanged += OnGameStateChanged;
            }
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
                OnLevelChanged(LevelManager.Instance.CurrentLevel);
        }

        private void Start()
        {
            if (Session == null && LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
                OnLevelChanged(LevelManager.Instance.CurrentLevel);
        }

        private void OnDisable()
        {
            if (events != null)
            {
                events.LevelChanged -= OnLevelChanged;
                events.GameStateChanged -= OnGameStateChanged;
            }
            hasSelection = false;
            RefreshViews();
        }

        private void LateUpdate()
        {
            if (Session == null) return;
            if (gridRoot.rect.size != previousSize) FitGrid();
            if (CanInteract != previousInput) RefreshViews();

            // 兜底：阶段已经结束但还没结算（例如某个阶段步数填了 0）
            if (Session.Phase == BoardStagePhase.Ended) CompleteEndedStage();
        }

        // ─────────────────────────────────────────────
        //  关卡 / 阶段构建
        // ─────────────────────────────────────────────
        private void OnLevelChanged(LevelConfig level)
        {
            if (level == currentLevel && Session != null) { RefreshViews(); return; }
            InitializeLevel(level);
        }

        private void InitializeLevel(LevelConfig level)
        {
            if (processing || level == null) return;
            processing = true;
            try
            {
                currentLevel = level;
                currentStageIndex = FirstEnabledStage();
                BuildStage();

            }
            catch (Exception ex)
            {
                Session = null;
                currentLevel = null;
                ClearOwnedViews();
                stepsText.text = "棋盘加载失败";
                Debug.LogException(ex, this);
            }
            finally { processing = false; RefreshViews(); }
        }

        public void RunStage(int index)
        {
            if (stages == null || stages.Length == 0)
            {
                return;
            }
            if (index < 0 || index >= stages.Length)
            {
                return;
            }

            // 可能还没初始化过，补一次
            if (currentLevel == null && LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
                InitializeLevel(LevelManager.Instance.CurrentLevel);

            if (currentLevel == null)
            {
                return;
            }

            currentStageIndex = index;
            BuildStage();
            RefreshViews();
        }

        /// <summary>按当前阶段重建棋盘。步数来自 stages[currentStageIndex]。</summary>
        private void BuildStage()
        {
            if (currentLevel == null) return;

            BoardStageSlot cfg = GetStageConfig(currentStageIndex);
            bool custom = cfg != null && cfg.useCustomWeights;

            CollectRetainedSpecials();
            BoardLayoutConfig layout = BuildLayoutWithRetained();

            BoardStageSession candidate;

            try
            {
                candidate = BoardLevelFactory.Create(
                    currentLevel, layout,
                    StageSteps(currentStageIndex),
                    custom ? cfg.weightFire : -1,
                    custom ? cfg.weightWater : -1,
                    custom ? cfg.weightWind : -1,
                    custom ? cfg.weightGround : -1);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"BoardSceneBridge: 合并保留棋子后建板失败（{ex.Message}），" +
                    $"本次改用关卡原始障碍，不带 × 效果", this);

                candidate = BoardLevelFactory.Create(
                    currentLevel, testLayout,
                    StageSteps(currentStageIndex),
                    custom ? cfg.weightFire : -1,
                    custom ? cfg.weightWater : -1,
                    custom ? cfg.weightWind : -1,
                    custom ? cfg.weightGround : -1);
            }

            ClearOwnedViews();
            Session = candidate;
            hasSelection = false;
            BuildViews();

            RunStageBegin(currentStageIndex);
        }
        void CollectRetainedSpecials()
        {
            _retainedAt.Clear();                    // ① 每次都重新读，不累积

            if (worldMap == null) return;           // ② 没拖就不启用这个规则

            for (int y = 0; y < worldMap.height; y++)
                for (int x = 0; x < worldMap.width; x++)
                {
                    SpecialElement e = worldMap.GetSpecial(new Vector2Int(x, y));
                    if (e == SpecialElement.None) continue;     // ③ 空格跳过

                    if (x >= Board.Width || y >= Board.Height) continue;   // ④ 超出第一棋盘范围就跳过

                    // 只有土和灰烬会在第一棋盘上变成不可通行
                    if (e != SpecialElement.Ground && e != SpecialElement.Ash) continue;

                    _retainedAt[new Vector2Int(x, y)] = e;
                }

            if (_retainedAt.Count > 0)
                Debug.Log($"BoardSceneBridge: 第二棋盘保留了 {_retainedAt.Count} 个棋子，" +
                          $"它们在第一棋盘上会变成障碍", this);
        }

        BoardLayoutConfig BuildLayoutWithRetained()
        {
            if (testLayout == null) return testLayout;

            var obstacles = new List<Vector2Int>();

            // 关卡的固定障碍原样保留
            obstacles.AddRange(testLayout.ObstaclePositions);

            Vector2Int start = testLayout.AnimalStart;
            Vector2Int end = testLayout.AnimalEnd;

            foreach (Vector2Int c in _retainedAt.Keys)
            {
                // 坑 1：压在起点/终点上会被 Validate 抛异常
                if (c == start || c == end) continue;

                if (!obstacles.Contains(c)) obstacles.Add(c);
            }

            // 没有新增就不必造新对象
            if (obstacles.Count == testLayout.ObstaclePositions.Count)
                return testLayout;

            return new BoardLayoutConfig(obstacles.ToArray(), start, end);
        }

        Color CrossColor(SpecialElement e)
        {
            switch (e)
            {
                case SpecialElement.Fire: return fireCrossColor;
                case SpecialElement.Water: return waterCrossColor;
                case SpecialElement.Wind: return windCrossColor;
                case SpecialElement.Ground: return groundCrossColor;
                case SpecialElement.Ash: return new Color(0.45f, 0.42f, 0.40f);
                default: return new Color(0.08f, 0.09f, 0.12f);
            }
        }

        private int StageSteps(int index)
        {
            if (stages != null && index >= 0 && index < stages.Length && stages[index].steps >= 0)
                return stages[index].steps;

            return currentLevel != null ? currentLevel.InitSteps : 0;
        }
        BoardStageSlot GetStageConfig(int index)
        {
            if (stages == null || index < 0 || index >= stages.Length) return null;
            return stages[index];
        }

        private int FirstEnabledStage()
        {
            int index = FindNextEnabledStage(0);
            if (index < 0)
            {
                Debug.LogWarning("BoardSceneBridge: 没有任何启用的阶段，按第 1 个处理。", this);
                return 0;
            }
            return index;
        }

        private int FindNextEnabledStage(int from)
        {
            if (stages == null) return -1;
            for (int i = Mathf.Max(0, from); i < stages.Length; i++)
                if (stages[i] != null && stages[i].enabled) return i;
            return -1;
        }

        // ─────────────────────────────────────────────
        //  阶段推进
        // ─────────────────────────────────────────────
        private void CompleteEndedStage()
        {
            if (Session == null || Session.Phase != BoardStagePhase.Ended) return;

            BoardSettlementResult settlement;
            if (!Session.TrySettle(out settlement)) return;

            hasSelection = false;
            Debug.Log("阶段 " + (currentStageIndex + 1) + " 结算：" + settlement.EndReason +
                      "，用 " + settlement.UsedSteps + " 步，剩余 " + settlement.RemainingSteps +
                      "，保留特种棋子 " + settlement.EnvironmentEntries.Count, this);
            Notify(StageSettled, settlement);

            AdvanceStage();
        }

        /// <summary>
        /// 推进到下一个启用的阶段。全部走完则触发 onAllStagesCleared。
        /// 外部也可以手动调用（把 Auto Advance Stages 关掉之后）。
        /// </summary>
        public void AdvanceStage()
        {
            DoAdvanceStage();
        }

        private void DoAdvanceStage()
        {
            int next = FindNextEnabledStage(currentStageIndex + 1);

            if (next < 0)
            {
                Debug.Log("BoardSceneBridge: 第 " + (currentStageIndex + 1) +
                          " 阶段结束，无限重复这个阶段", this);

                BuildStage();
                RefreshViews();
                return;
            }

            currentStageIndex = next;
            Debug.Log("BoardSceneBridge: 进入阶段 " + (next + 1) + " —— " + StageName(next), this);
            BuildStage();
            RefreshViews();
        }

        [ContextMenu("测试/强制进入下一阶段")]
        public void AdvanceStageManually()
        {
            bool backup = autoAdvanceStages;
            autoAdvanceStages = true;
            AdvanceStage();
            autoAdvanceStages = backup;
        }

        private string StageName(int index)
        {
            if (stages == null || index < 0 || index >= stages.Length || stages[index] == null) return "阶段";
            return stages[index].name;
        }

        // ═══════════════════════════════════════════════════════
        //  ▼▼▼  留空：每个阶段开始时要做的事  ▼▼▼
        //
        //  index 从 0 开始。五个阶段各自的行为写在这里。
        //  例如：
        //      if (index == 0) { 显示教学提示; }
        //      if (index == 1) { 给棋盘加一层障碍; }
        //      if (index >= 2) { 提升刷新权重; }
        //
        //  注意：这时 Session 已经建好、格子也已经画出来了，
        //  想改棋盘内容要走 Board / Session 的接口，不要直接改 views。
        // ═══════════════════════════════════════════════════════
        private void RunStageBegin(int index)
        {
            if (onStageBegin != null) onStageBegin.Invoke(index);
        }
        // ═══════════════════════════════════════════════════════
        //  ▲▲▲  留空结束  ▲▲▲
        // ═══════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════
        //  ▼▼▼  留空：五个阶段全部走完时要做的事  ▼▼▼
        //
        //  这里就是"第一棋盘整体结束"的信号。
        //  下一步通常是通知外部切换到第二棋盘，例如：
        //      startButtonUI.EndStageOne();
        // ═══════════════════════════════════════════════════════
        private void RunAllStagesCleared()
        {
            if (onAllStagesCleared != null) onAllStagesCleared.Invoke();
        }
        // ═══════════════════════════════════════════════════════
        //  ▲▲▲  留空结束  ▲▲▲
        // ═══════════════════════════════════════════════════════

        // ─────────────────────────────────────────────
        //  交互
        // ─────────────────────────────────────────────
        /// <summary>与 UI Button 共用的操作入口；失败不扣步、不清除合法选中状态。</summary>
        public bool ClickCell(GridCoord coord)
        {
            if (!CanInteract || !Board.IsInside(coord)) return false;
            CellState cell = Board.GetCell(coord);
            if (hasSelection && coord == selected)
            {
                hasSelection = false;
                RefreshViews();
                return true;
            }
            if (cell.HasPiece && cell.CanOperate && !cell.HasObstacle)
            {
                selected = coord;
                hasSelection = true;
                RefreshViews();
                return true;
            }
            if (!hasSelection) return false;
            processing = true;
            try
            {
                BoardTurnResult result;
                if (!Session.TryMove(selected, coord, out result)) return false;
                hasSelection = false;
                PlaySynthesisSound(result);
                CompleteEndedStage();
                Notify(TurnCompleted, result);
                return true;
            }
            finally { processing = false; RefreshViews(); }
        }

        private void OnGameStateChanged(GameState state)
        {
            if (state != GameState.Playing) hasSelection = false;
            RefreshViews();
        }

        // 保留旧规则按钮回调名称，但回调目标改为本组件；不改 RulePanelUI。
        public void OnClickShowRule()
        {
            if (GameEventCenter.Instance != null) GameEventCenter.Instance.RequestPauseGame();
        }

        [ContextMenu("测试/重置当前棋盘")]
        public void ResetBoard()
        {
            if (LevelManager.Instance != null) InitializeLevel(LevelManager.Instance.CurrentLevel);
        }

        [ContextMenu("测试/手动结束当前阶段")]
        public void EndStageManually()
        {
            if (!CanInteract) return;
            if (Session.TryEndManually()) CompleteEndedStage();
            RefreshViews();
        }
        static Sprite Pick(Sprite primary, Sprite fallback)
        {
            return primary != null ? primary : fallback;
        }
        // ─────────────────────────────────────────────
        //  显示
        // ─────────────────────────────────────────────
        private void RefreshViews()
        {
            previousInput = CanInteract;
            if (Session == null) return;

            levelIdText.text = currentLevel != null
                ? "关卡：" + currentLevel.LevelId + "  " + StageName(currentStageIndex) +
                  "（" + (currentStageIndex + 1) + "/" + StageCount + "）"
                : "";

            stepsText.text = "剩余步数：" + Session.RemainingSteps + " / " + Session.InitialSteps +
                             (Session.Phase == BoardStagePhase.Settled ? "（已结束）" : "");

            foreach (var pair in views)
            {
                CellState cell = Board.GetCell(pair.Key);
                CellView view = pair.Value;

                bool special = cell.HasPiece && cell.Piece.Kind == PieceKind.Special;
                bool blocked = cell.HasObstacle || !cell.CanOperate;
                bool isSelected = hasSelection && pair.Key == selected;
                bool reachable = hasSelection && Board.CanTransferPiece(selected, pair.Key);

                // ── 先决定这一格用哪张棋面 ──
                // ── 棋面：A/B 交叉 + 高亮/暗淡 ──
                Sprite bright = view.Odd ? cellBSprite : cellASprite;
                Sprite dim = view.Odd ? cellBDimSprite : cellADimSprite;

                Sprite face;
                Color tint;

                if (blocked)
                {
                    face = Pick(dim, bright);
                    tint = cellBlockedTint;
                }
                else if (isSelected)
                {
                    face = Pick(bright, cellASprite);
                    tint = cellSelectedTint;
                }
                else if (hasSelection && !reachable)
                {
                    // 走不到 → 换成暗淡的那张
                    face = Pick(dim, bright);
                    tint = Color.white;
                }
                else
                {
                    // 平时 / 走得到 → 高亮
                    face = Pick(bright, cellASprite);
                    tint = Color.white;
                }

                if (face != null)
                {
                    if (view.Background.sprite != face) view.Background.sprite = face;
                    view.Background.color = tint;
                }
                else
                {
                    // 四张棋面图都没配 → 退回原来的纯色逻辑
                    if (blocked) view.Background.color = new Color(0.22f, 0.25f, 0.29f);
                    else if (special) view.Background.color = new Color(0.8f, 0.56f, 0.95f);
                    else view.Background.color = new Color(0.72f, 0.75f, 0.79f);

                    if (isSelected) view.Background.color = new Color(1f, 0.9f, 0.32f);
                    else if (reachable) view.Background.color = new Color(0.5f, 0.87f, 0.55f);
                }
                view.Button.interactable = previousInput && cell.CanOperate && !cell.HasObstacle;
                view.Piece.gameObject.SetActive(cell.HasPiece);
                if (cell.HasObstacle)
                {
                    // 障碍格：显示 ×，颜色按"是谁留下的"来定
                    view.Label.text = "×";
                    view.Label.color = _retainedAt.TryGetValue(view.Cell, out SpecialElement crossElement)
                        ? CrossColor(crossElement)
                        : new Color(0.08f, 0.09f, 0.12f);
                }
                else
                {
                    view.Label.text = "";
                    view.Label.color = Color.white;
                }

                if (!cell.HasPiece) continue;

                Sprite normalArt = ElementSprite(cell.Piece.Element);
                Sprite specialArt = special ? SpecialSprite(cell.Piece.Element) : null;

                // 特殊棋子优先用专用图，没配才退回普通图
                Sprite sprite = special ? Pick(specialArt, normalArt) : normalArt;

                view.Piece.sprite = sprite;
                view.Piece.preserveAspect = sprite != null;
                view.Piece.color = sprite != null ? Color.white : ElementColor(cell.Piece.Element);

                // 文字只在"没有专用图"的时候兜底
                if (sprite == null)
                    view.Label.text = ElementName(cell.Piece.Element) + (special ? "·特" : "");
                else if (special && specialArt == null)
                    view.Label.text = "特";      // 用的是普通图，靠这个字区分
                else
                    view.Label.text = "";        // 有专用图了，不再写字

                view.Label.color = new Color(0.08f, 0.09f, 0.12f);
            }
        }

        private void FitGrid()
        {
            previousSize = gridRoot.rect.size;
            float width = previousSize.x - layout.padding.horizontal - layout.spacing.x * (Board.Width - 1);
            float height = previousSize.y - layout.padding.vertical - layout.spacing.y * (Board.Height - 1);
            float size = Mathf.Max(1, Mathf.Min(width / Board.Width, height / Board.Height));
            layout.cellSize = new Vector2(size, size);
            foreach (CellView view in views.Values) view.Label.fontSize = Mathf.Max(1, size * 0.42f);
            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(gridRoot);
        }

        // ─────────────────────────────────────────────
        //  视图构建
        // ─────────────────────────────────────────────
        private void BuildViews()
        {
            layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Board.Width;
            layout.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.LowerLeft;
            layout.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.MiddleCenter;
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    GridCoord coord = new GridCoord(x, y);
                    GameObject root = cellPrefab != null ? Instantiate(cellPrefab, gridRoot, false) :
                        new GameObject("Cell", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    root.transform.SetParent(gridRoot, false);
                    root.transform.localScale = Vector3.one;
                    root.name = "Cell_" + x + "_" + y;
                    var background = root.GetComponent<UnityEngine.UI.Image>();
                    if (background == null) background = root.AddComponent<UnityEngine.UI.Image>();
                    background.raycastTarget = true;
                    var button = root.GetComponent<UnityEngine.UI.Button>();
                    if (button == null) button = root.AddComponent<UnityEngine.UI.Button>();
                    button.targetGraphic = background;
                    button.transition = UnityEngine.UI.Selectable.Transition.None;
                    var navigation = button.navigation;
                    navigation.mode = UnityEngine.UI.Navigation.Mode.None;
                    button.navigation = navigation;
                    button.onClick.AddListener(() => ClickCell(coord));
                    float pieceInset = (1f - pieceFillRatio) * 0.5f;
                    var piece = CreateImage("Piece", root.transform,
                                            new Vector2(pieceInset, pieceInset),
                                            new Vector2(1f - pieceInset, 1f - pieceInset));
                    GameObject labelObject = new GameObject("ElementLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                    labelObject.transform.SetParent(root.transform, false);
                    Stretch((RectTransform)labelObject.transform, Vector2.zero, Vector2.one);
                    TMP_Text label = labelObject.GetComponent<TMP_Text>();
                    label.font = levelIdText.font;
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontStyle = FontStyles.Bold;
                    label.raycastTarget = false;
                    views.Add(coord, new CellView
                    {
                        Root = root,
                        Background = background,
                        Button = button,
                        Piece = piece,
                        Label = label,
                        Odd = ((x + y) & 1) == 1,
                        Cell = new Vector2Int(x, y)          
                    });
                }
            FitGrid();
        }

        private void ClearOwnedViews()
        {
            foreach (CellView view in views.Values)
            {
                if (view.Root == null) continue;
                view.Root.SetActive(false);
                Destroy(view.Root);
            }
            views.Clear();
        }

        private static UnityEngine.UI.Image CreateImage(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private Sprite ElementSprite(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fireSprite;
                case ElementType.Water: return waterSprite;
                case ElementType.Wind: return windSprite;
                case ElementType.ground: return groundSprite;
                default: return null;
            }
        }

        private Sprite SpecialSprite(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fireSpecialSprite;
                case ElementType.Water: return waterSpecialSprite;
                case ElementType.Wind: return windSpecialSprite;
                case ElementType.ground: return groundSpecialSprite;
                default: return null;
            }
        }

        // ── 合成音效 ──
        private void PlaySynthesisSound(BoardTurnResult result)
        {
            if (result == null || result.SpecialPiece == null) return;

            AudioClip clip = SynthesisClip(result.SpecialPiece.Element);
            if (clip == null) return;

            EnsureSfx();
            _sfx.PlayOneShot(clip, synthesisVolume);
        }

        private AudioClip SynthesisClip(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fireSynthesisClip;
                case ElementType.Water: return waterSynthesisClip;
                case ElementType.Wind: return windSynthesisClip;
                case ElementType.ground: return groundSynthesisClip;
                default: return null;
            }
        }

        private void EnsureSfx()
        {
            if (_sfx != null) return;

            _sfx = GetComponent<AudioSource>();
            if (_sfx == null) _sfx = gameObject.AddComponent<AudioSource>();

            _sfx.playOnAwake = false;
            _sfx.loop = false;
            _sfx.spatialBlend = 0f;        // 0 = 2D，不受距离影响
        }

        private static string ElementName(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "火";
                case ElementType.Water: return "水";
                case ElementType.Wind: return "风";
                case ElementType.ground: return "土";
                default: return "?";
            }
        }

        private static Color ElementColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return new Color(1f, 0.4f, 0.35f);
                case ElementType.Water: return new Color(0.35f, 0.7f, 1f);
                case ElementType.Wind: return new Color(0.4f, 0.95f, 0.72f);
                case ElementType.ground: return new Color(1f, 0.78f, 0.35f);
                default: return Color.white;
            }
        }

        private void Notify<T>(Action<T> listeners, T result)
        {
            if (listeners == null) return;
            foreach (Delegate listener in listeners.GetInvocationList())
            {
                try { ((Action<T>)listener)(result); }
                catch (Exception ex) { Debug.LogException(ex, this); }
            }
        }
    }
}

