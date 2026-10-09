using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Board;
using System.Collections.Generic;

namespace Assets.Scripts.UI
{
    /// <summary>
    /// 棋盘UI控制器 - 对接B部分棋盘逻辑与UI显示
    /// 负责：棋盘初始化、棋子显示、交互处理、步数更新
    /// </summary>
    public class BoardUIController : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private TMP_Text stepsText;           // 步数显示
        [SerializeField] private RectTransform gridRoot;       // 棋盘容器
        [SerializeField] private GameObject cellPrefab;        // 格子预制体

        [Header("棋盘配置")]
        [SerializeField] private int boardWidth = 10;
        [SerializeField] private int boardHeight = 10;
        [SerializeField] private int initialSteps = 30;

        // 核心组件
        private BoardState board;
        private BoardStageSession session;
        private SpawnRules spawnRules;

        // UI缓存
        private Dictionary<GridCoord, GameObject> cellObjects = new Dictionary<GridCoord, GameObject>();
        private Dictionary<GridCoord, Image> pieceImages = new Dictionary<GridCoord, Image>();

        // 选中状态
        private GridCoord selectedCoord;
        private bool hasSelection = false;

        void Start()
        {
            InitializeBoard();
            UpdateStepsUI();
        }

        /// <summary>
        /// 初始化棋盘
        /// </summary>
        void InitializeBoard()
        {
            // 1. 创建棋盘数据
            board = new BoardState(boardWidth, boardHeight);

            // 2. 配置障碍物（示例：使用关卡1的配置）
            BoardLayoutConfig layoutConfig = BoardLevelExample.CreateLevel1();
            BoardInitialLayoutResult layoutResult;
            string error;

            if (!BoardInitialLayoutGenerator.TryGenerate(
                boardWidth, boardHeight,
                layoutConfig,
                out layoutResult,
                out error))
            {
                Debug.LogError($"棋盘生成失败: {error}");
                return;
            }

            board = layoutResult.Board;

            // 3. 创建刷新规则
            var areas = new SpawnArea[] { new SpawnArea(0, 0, boardWidth, boardHeight) };
            var weights = DefaultElementWeights.Create();
            var allowedElements = new ElementType[]
            {
                ElementType.Fire,
                ElementType.Water,
                ElementType.Wind,
                ElementType.ground
            };
            spawnRules = new SpawnRules(areas, allowedElements, weights, new RegionWeightRule[0]);

            // 4. 生成初始棋子
            int initialPieceCount = 15;
            for (int i = 0; i < initialPieceCount; i++)
            {
                SpawnResult spawn = SpawnResolver.TrySpawnOne(board, spawnRules, SpawnContext.Neutral);
                if (!spawn.Success)
                {
                    Debug.LogWarning($"初始棋子生成失败: {spawn.FailureReason}");
                    break;
                }
            }

            // 5. 创建会话
            session = new BoardStageSession(board, initialSteps, spawnRules, SpawnContext.Neutral);

            // 6. 生成UI
            GenerateBoardUI();

            Debug.Log($"棋盘初始化完成: {boardWidth}x{boardHeight}, {initialSteps}步");
        }

        /// <summary>
        /// 生成棋盘UI
        /// </summary>
        void GenerateBoardUI()
        {
            // 清空旧的
            foreach (Transform child in gridRoot)
            {
                Destroy(child.gameObject);
            }
            cellObjects.Clear();
            pieceImages.Clear();

            // 创建格子
            for (int y = 0; y < boardHeight; y++)
            {
                for (int x = 0; x < boardWidth; x++)
                {
                    GridCoord coord = new GridCoord(x, y);
                    CellState cell = board.GetCell(coord);

                    // 创建格子对象
                    GameObject cellObj = Instantiate(cellPrefab, gridRoot);
                    cellObjects[coord] = cellObj;

                    // 设置格子外观
                    Image cellImage = cellObj.GetComponent<Image>();
                    if (cell.HasObstacle)
                    {
                        cellImage.color = new Color(0.3f, 0.3f, 0.3f);  // 深灰色 - 障碍物
                    }
                    else
                    {
                        cellImage.color = new Color(0.8f, 0.8f, 0.8f);  // 浅灰色 - 空格子
                    }

                    // 显示棋子
                    if (cell.HasPiece)
                    {
                        // 创建棋子Image（在格子内部）
                        GameObject pieceObj = new GameObject("Piece");
                        pieceObj.transform.SetParent(cellObj.transform);
                        RectTransform pieceRect = pieceObj.AddComponent<RectTransform>();
                        pieceRect.anchorMin = new Vector2(0.1f, 0.1f);
                        pieceRect.anchorMax = new Vector2(0.9f, 0.9f);
                        pieceRect.offsetMin = Vector2.zero;
                        pieceRect.offsetMax = Vector2.zero;

                        Image pieceImage = pieceObj.AddComponent<Image>();
                        pieceImage.color = GetElementColor(cell.Piece.Element);
                        pieceImages[coord] = pieceImage;

                        // 特种棋子标记
                        if (cell.Piece.Kind == PieceKind.Special)
                        {
                            pieceImage.color = Color.yellow;  // 特种棋子用黄色
                        }
                    }

                    // 添加点击事件
                    Button btn = cellObj.GetComponent<Button>();
                    if (btn != null)
                    {
                        GridCoord capturedCoord = coord;  // 闭包捕获
                        btn.onClick.AddListener(() => OnCellClick(capturedCoord));
                    }
                }
            }
        }

        /// <summary>
        /// 格子点击事件
        /// </summary>
        void OnCellClick(GridCoord coord)
        {
            if (session.Phase != BoardStagePhase.Operating)
            {
                Debug.Log("游戏已结束");
                return;
            }

            CellState cell = board.GetCell(coord);

            if (!hasSelection)
            {
                // 选择棋子
                if (cell.HasPiece && cell.Piece.Kind == PieceKind.Normal)
                {
                    selectedCoord = coord;
                    hasSelection = true;
                    HighlightCell(coord, true);
                    Debug.Log($"选中棋子: {coord}");
                }
            }
            else
            {
                // 尝试移动
                if (coord == selectedCoord)
                {
                    // 取消选择
                    hasSelection = false;
                    HighlightCell(selectedCoord, false);
                    Debug.Log("取消选择");
                }
                else
                {
                    // 执行移动
                    BoardTurnResult result;
                    if (session.TryMove(selectedCoord, coord, out result))
                    {
                        Debug.Log($"移动成功: {selectedCoord} -> {coord}");

                        // 刷新UI
                        RefreshBoardUI();
                        UpdateStepsUI();

                        // 检查游戏状态
                        if (session.Phase == BoardStagePhase.Ended)
                        {
                            OnGameEnd();
                        }
                    }
                    else
                    {
                        Debug.Log($"移动失败: {result.Failure}");
                    }

                    // 取消选择
                    hasSelection = false;
                    HighlightCell(selectedCoord, false);
                }
            }
        }

        /// <summary>
        /// 高亮格子
        /// </summary>
        void HighlightCell(GridCoord coord, bool highlight)
        {
            if (cellObjects.TryGetValue(coord, out GameObject cellObj))
            {
                Image cellImage = cellObj.GetComponent<Image>();
                if (highlight)
                {
                    cellImage.color = new Color(1f, 1f, 0.5f);  // 黄色高亮
                }
                else
                {
                    CellState cell = board.GetCell(coord);
                    cellImage.color = cell.HasObstacle
                        ? new Color(0.3f, 0.3f, 0.3f)
                        : new Color(0.8f, 0.8f, 0.8f);
                }
            }
        }

        /// <summary>
        /// 刷新棋盘UI
        /// </summary>
        void RefreshBoardUI()
        {
            // 清除所有棋子Image
            foreach (var pieceImage in pieceImages.Values)
            {
                if (pieceImage != null)
                    Destroy(pieceImage.gameObject);
            }
            pieceImages.Clear();

            // 重新生成棋子
            for (int y = 0; y < boardHeight; y++)
            {
                for (int x = 0; x < boardWidth; x++)
                {
                    GridCoord coord = new GridCoord(x, y);
                    CellState cell = board.GetCell(coord);

                    if (cell.HasPiece && cellObjects.TryGetValue(coord, out GameObject cellObj))
                    {
                        // 创建棋子Image
                        GameObject pieceObj = new GameObject("Piece");
                        pieceObj.transform.SetParent(cellObj.transform);
                        RectTransform pieceRect = pieceObj.AddComponent<RectTransform>();
                        pieceRect.anchorMin = new Vector2(0.1f, 0.1f);
                        pieceRect.anchorMax = new Vector2(0.9f, 0.9f);
                        pieceRect.offsetMin = Vector2.zero;
                        pieceRect.offsetMax = Vector2.zero;

                        Image pieceImage = pieceObj.AddComponent<Image>();
                        pieceImage.color = GetElementColor(cell.Piece.Element);
                        pieceImages[coord] = pieceImage;

                        if (cell.Piece.Kind == PieceKind.Special)
                        {
                            pieceImage.color = Color.yellow;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 更新步数UI
        /// </summary>
        void UpdateStepsUI()
        {
            if (stepsText != null)
            {
                stepsText.text = $"剩余步数: {session.RemainingSteps}";
            }

            // 通知事件中心
            // GameEventCenter.Instance.NotifyStepsChanged(session.RemainingSteps);
        }

        /// <summary>
        /// 游戏结束处理
        /// </summary>
        void OnGameEnd()
        {
            Debug.Log($"游戏结束: {session.EndReason}");

            // 执行结算
            BoardSettlementResult settlement;
            if (session.TrySettle(out settlement))
            {
                Debug.Log($"结算完成: 移除{settlement.RemovedNormalPieces.Count}个普通棋子, 保留{settlement.EnvironmentEntries.Count}个特种棋子");
            }

            // 通知UI显示结算面板
            // GameEventCenter.Instance.NotifyGameEnd(session.EndReason);
        }

        /// <summary>
        /// 获取元素颜色
        /// </summary>
        Color GetElementColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:   return new Color(1f, 0.3f, 0.3f);    // 红色
                case ElementType.Water:  return new Color(0.3f, 0.6f, 1f);    // 蓝色
                case ElementType.Wind:   return new Color(0.5f, 1f, 0.8f);    // 青色
                case ElementType.ground: return new Color(1f, 0.8f, 0.3f);    // 黄色
                default: return Color.white;
            }
        }

        /// <summary>
        /// 重置棋盘（调试用）
        /// </summary>
        public void ResetBoard()
        {
            InitializeBoard();
            UpdateStepsUI();
        }
    }
}
