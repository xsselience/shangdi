using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 现有 SubScreen/ChessBoard 的连接层。只生成 Grid 内的测试格子，不新建 Canvas/掌机界面。
    /// 所有玩家回合经过原有 BoardStageSession；不使用世界坐标射线或另一套测试棋盘。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardSceneBridge : MonoBehaviour
    {
        // 字段名保持旧棋盘组件的序列化名称，保留预制体现有引用。
        [Header("现有 UI 引用")]
        [SerializeField] private TMP_Text levelIdText;
        [SerializeField] private TMP_Text stepsText;
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private RectTransform gridRoot;
        [Header("临时测试布局（正式关卡障碍配置导入后替换）")]
        [SerializeField] private BoardLayoutConfig testLayout = new BoardLayoutConfig(
            new[] { new Vector2Int(3, 3), new Vector2Int(6, 6), new Vector2Int(3, 6), new Vector2Int(6, 3) },
            new Vector2Int(0, 0), new Vector2Int(9, 9));
        [Header("可选正式棋子美术；未指定时用颜色和文字")]
        [SerializeField] private Sprite fireSprite;
        [SerializeField] private Sprite waterSprite;
        [SerializeField] private Sprite windSprite;
        [SerializeField] private Sprite groundSprite;

        private sealed class CellView
        {
            public GameObject Root;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image Piece;
            public TMP_Text Label;
        }

        private readonly Dictionary<GridCoord, CellView> views = new Dictionary<GridCoord, CellView>();
        private UnityEngine.UI.GridLayoutGroup layout;
        private GameEventCenter events;
        private LevelConfig currentLevel;
        private bool hasSelection;
        private GridCoord selected;
        private bool processing;
        private bool previousInput;
        private Vector2 previousSize = new Vector2(-1, -1);

        public BoardStageSession Session { get; private set; }
        public BoardState Board { get { return Session == null ? null : Session.Board; } }
        public bool HasSelection { get { return hasSelection; } }
        public GridCoord SelectedCoord { get { return selected; } }
        public bool CanInteract
        {
            get { return isActiveAndEnabled && !processing && Session != null &&
                         Session.Phase == BoardStagePhase.Operating && GameManager.Instance != null &&
                         GameManager.Instance.CurrentState == GameState.Playing; }
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
            // 重新激活时可能已错过广播，但普通 Start 先等待 LevelManager 的初始化。
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
            // 同时兜底全局状态变化，不能靠 timeScale 暂停鼠标 UI 事件。
            if (CanInteract != previousInput) RefreshViews();
        }

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
                BoardStageSession candidate = BoardLevelFactory.Create(level, testLayout);
                ClearOwnedViews();
                Session = candidate;
                currentLevel = level;
                hasSelection = false;
                BuildViews();
                CompleteEndedStage();
                Debug.Log("现有下屏棋盘已连接：关卡 " + level.LevelId + "，" + Board.Width + "×" + Board.Height +
                          "，初始棋子 " + level.InitPieceCount + "，步数 " + level.InitSteps, this);
            }
            catch (Exception ex)
            {
                // 错误时禁止操作，不留下可交互的旧关卡。
                Session = null;
                currentLevel = null;
                ClearOwnedViews();
                stepsText.text = "棋盘加载失败";
                Debug.LogException(ex, this);
            }
            finally { processing = false; RefreshViews(); }
        }

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
                var piece = CreateImage("Piece", root.transform, new Vector2(0.13f, 0.13f), new Vector2(0.87f, 0.87f));
                GameObject labelObject = new GameObject("ElementLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(root.transform, false);
                Stretch((RectTransform)labelObject.transform, Vector2.zero, Vector2.one);
                TMP_Text label = labelObject.GetComponent<TMP_Text>();
                label.font = levelIdText.font;
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.raycastTarget = false;
                views.Add(coord, new CellView { Root = root, Background = background, Button = button, Piece = piece, Label = label });
            }
            FitGrid();
        }

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
            // 不自行禁止特种棋子移动：遵循 BoardState 的实际可操作规则。
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
                CompleteEndedStage();
                Notify(TurnCompleted, result);
                return true;
            }
            finally { processing = false; RefreshViews(); }
        }

        private void CompleteEndedStage()
        {
            if (Session.Phase != BoardStagePhase.Ended) return;
            BoardSettlementResult settlement;
            if (!Session.TrySettle(out settlement)) return;
            hasSelection = false;
            Debug.Log("棋盘阶段已结算：" + settlement.EndReason + "，保留特种棋子 " + settlement.EnvironmentEntries.Count, this);
            Notify(StageSettled, settlement);
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

        [ContextMenu("测试/手动结束棋盘阶段")]
        public void EndStageManually()
        {
            if (!CanInteract) return;
            if (Session.TryEndManually()) CompleteEndedStage();
            RefreshViews();
        }

        private void RefreshViews()
        {
            previousInput = CanInteract;
            if (Session == null) return;
            levelIdText.text = "关卡：" + currentLevel.LevelId;
            stepsText.text = "剩余步数：" + Session.RemainingSteps +
                             (Session.Phase == BoardStagePhase.Settled ? "（已结束）" : "");
            foreach (var pair in views)
            {
                CellState cell = Board.GetCell(pair.Key);
                CellView view = pair.Value;
                bool special = cell.HasPiece && cell.Piece.Kind == PieceKind.Special;
                view.Background.color = cell.HasObstacle || !cell.CanOperate ? new Color(0.22f, 0.25f, 0.29f) :
                    special ? new Color(0.8f, 0.56f, 0.95f) : new Color(0.72f, 0.75f, 0.79f);
                if (hasSelection && pair.Key == selected) view.Background.color = new Color(1f, 0.9f, 0.32f);
                else if (hasSelection && Board.CanTransferPiece(selected, pair.Key))
                    view.Background.color = new Color(0.5f, 0.87f, 0.55f);
                view.Button.interactable = previousInput && cell.CanOperate && !cell.HasObstacle;
                view.Piece.gameObject.SetActive(cell.HasPiece);
                view.Label.text = cell.HasObstacle ? "×" : "";
                view.Label.color = Color.white;
                if (!cell.HasPiece) continue;
                Sprite sprite = ElementSprite(cell.Piece.Element);
                view.Piece.sprite = sprite;
                view.Piece.preserveAspect = sprite != null;
                view.Piece.color = sprite != null ? Color.white : ElementColor(cell.Piece.Element);
                view.Label.text = sprite != null ? (special ? "特" : "") : ElementName(cell.Piece.Element) + (special ? "·特" : "");
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

        private void ClearOwnedViews()
        {
            foreach (CellView view in views.Values)
            {
                if (view.Root == null) continue;
                view.Root.SetActive(false); // Destroy 延后执行，先退出布局和射线检测。
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
