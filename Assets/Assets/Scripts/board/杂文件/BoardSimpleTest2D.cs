using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 2D棋盘简单测试。完全2D显示，正俯视角度。
    /// </summary>
    public class BoardSimpleTest2D : MonoBehaviour
    {
        [Header("棋盘设置")]
        [SerializeField] private int boardWidth = 8;
        [SerializeField] private int boardHeight = 8;
        [SerializeField] private int initialPieces = 12;
        [SerializeField] private int stepLimit = 30;
        [SerializeField] private int obstacleCount = 8;

        [Header("显示设置")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float pieceScale = 0.8f;

        // 核心组件
        private BoardState board;
        private BoardStageSession session;
        private SpawnRules spawnRules;

        // 选中状态
        private GridCoord selectedCoord;
        private bool hasSelection = false;

        // 视觉对象
        private SpriteRenderer[,] pieceSprites;
        private SpriteRenderer[,] obstacleSprites;
        private SpriteRenderer selectionHighlight;

        // 统计
        private int remainingSteps;
        private int normalPieceCount;
        private int specialPieceCount;

        void Start()
        {
            SetupCamera();
            InitializeBoard();
            CreateVisuals();

            Debug.Log("=== 2D棋盘测试启动 ===");
            Debug.Log("点击棋子选中（变黄），点击相邻空格移动");
            Debug.Log("按 R 键重新开始");
        }

        void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }

            // 2D正交摄像机，正俯视
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(boardWidth, boardHeight) * cellSize * 0.6f;

            float centerX = (boardWidth - 1) * cellSize * 0.5f;
            float centerY = (boardHeight - 1) * cellSize * 0.5f;
            cam.transform.position = new Vector3(centerX, centerY, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);

            Debug.Log("2D摄像机已设置");
        }

        void InitializeBoard()
        {
            // 创建棋盘
            board = new BoardState(boardWidth, boardHeight);

            // 生成障碍物（简单随机）
            int obstaclesPlaced = 0;
            int maxAttempts = obstacleCount * 3;
            int attempts = 0;

            while (obstaclesPlaced < obstacleCount && attempts < maxAttempts)
            {
                int x = Random.Range(1, boardWidth - 1);
                int y = Random.Range(1, boardHeight - 1);
                GridCoord coord = new GridCoord(x, y);

                CellState cell = board.GetCell(coord);
                if (!cell.HasObstacle && !cell.HasPiece)
                {
                    board.ConfigureEmptyCell(coord, false, false, true);
                    obstaclesPlaced++;
                }
                attempts++;
            }

            // 生成初始棋子
            ElementType[] elements = { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.ground };
            int piecesPlaced = 0;
            int maxPieceAttempts = initialPieces * 3;
            attempts = 0;

            while (piecesPlaced < initialPieces && attempts < maxPieceAttempts)
            {
                int x = Random.Range(0, boardWidth);
                int y = Random.Range(0, boardHeight);
                GridCoord coord = new GridCoord(x, y);

                CellState cell = board.GetCell(coord);
                if (!cell.HasObstacle && !cell.HasPiece)
                {
                    ElementType element = elements[Random.Range(0, elements.Length)];
                    board.CreatePieceAt(coord, element, PieceKind.Normal);
                    piecesPlaced++;
                }
                attempts++;
            }

            // 创建刷新规则（用于移动后刷新）
            var areas = new SpawnArea[] { new SpawnArea(0, 0, boardWidth, boardHeight) };
            var weights = DefaultElementWeights.Create();  // 使用默认权重配置
            spawnRules = new SpawnRules(areas, elements, weights, new RegionWeightRule[0]);

            // 创建会话（不再需要随机种子）
            session = new BoardStageSession(board, stepLimit, spawnRules, SpawnContext.Neutral);
            remainingSteps = stepLimit;

            Debug.Log($"棋盘初始化完成: {boardWidth}x{boardHeight}, {piecesPlaced}个棋子, {obstaclesPlaced}个障碍");
        }

        void CreateVisuals()
        {
            pieceSprites = new SpriteRenderer[boardWidth, boardHeight];
            obstacleSprites = new SpriteRenderer[boardWidth, boardHeight];

            // 创建选中高亮
            GameObject highlightObj = new GameObject("SelectionHighlight");
            highlightObj.transform.SetParent(transform);
            selectionHighlight = highlightObj.AddComponent<SpriteRenderer>();
            selectionHighlight.sprite = CreateSquareSprite();
            selectionHighlight.color = new Color(1f, 1f, 0f, 0.5f);
            selectionHighlight.sortingOrder = 5;
            selectionHighlight.gameObject.SetActive(false);

            // 创建棋盘格背景
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    Vector3 pos = new Vector3(x * cellSize, y * cellSize, 0);

                    // 棋盘格背景
                    GameObject bgObj = new GameObject($"BG_{x}_{y}");
                    bgObj.transform.position = pos;
                    bgObj.transform.SetParent(transform);
                    SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
                    bgRenderer.sprite = CreateSquareSprite();
                    bgRenderer.color = (x + y) % 2 == 0 ? new Color(0.3f, 0.3f, 0.35f) : new Color(0.25f, 0.25f, 0.3f);
                    bgRenderer.sortingOrder = 0;

                    GridCoord coord = new GridCoord(x, y);
                    CellState cell = board.GetCell(coord);

                    // 障碍物
                    if (cell.HasObstacle)
                    {
                        GameObject obstacleObj = new GameObject($"Obstacle_{x}_{y}");
                        obstacleObj.transform.position = pos;
                        obstacleObj.transform.SetParent(transform);
                        SpriteRenderer obstacleRenderer = obstacleObj.AddComponent<SpriteRenderer>();
                        obstacleRenderer.sprite = CreateSquareSprite();
                        obstacleRenderer.color = new Color(0.2f, 0.2f, 0.2f);
                        obstacleRenderer.sortingOrder = 1;
                        obstacleRenderer.transform.localScale = Vector3.one * 0.9f * cellSize;
                        obstacleSprites[x, y] = obstacleRenderer;
                    }

                    // 棋子
                    if (cell.HasPiece)
                    {
                        CreatePieceSprite(coord, cell.Piece);
                    }
                }
            }

            UpdateStats();
        }

        void CreatePieceSprite(GridCoord coord, PieceState piece)
        {
            Vector3 pos = new Vector3(coord.X * cellSize, coord.Y * cellSize, 0);
            GameObject pieceObj = new GameObject($"Piece_{piece.Id}");
            pieceObj.transform.position = pos;
            pieceObj.transform.SetParent(transform);

            SpriteRenderer renderer = pieceObj.AddComponent<SpriteRenderer>();
            renderer.sprite = piece.Kind == PieceKind.Special ? CreateCircleSprite() : CreateSquareSprite();
            renderer.color = GetElementColor(piece.Element);
            renderer.sortingOrder = 2;
            renderer.transform.localScale = Vector3.one * pieceScale * cellSize;

            pieceSprites[coord.X, coord.Y] = renderer;
        }

        Sprite CreateSquareSprite()
        {
            Texture2D tex = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64);
        }

        Sprite CreateCircleSprite()
        {
            Texture2D tex = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];
            Vector2 center = new Vector2(32, 32);
            float radius = 30;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * 64 + x] = dist <= radius ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64);
        }

        Color GetElementColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return new Color(1f, 0.3f, 0.1f);
                case ElementType.Water: return new Color(0.2f, 0.6f, 1f);
                case ElementType.Wind: return new Color(0.9f, 0.9f, 0.9f);
                case ElementType.ground: return new Color(0.9f, 0.7f, 0.2f);
                default: return Color.white;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartBoard();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                int x = Mathf.RoundToInt(mousePos.x / cellSize);
                int y = Mathf.RoundToInt(mousePos.y / cellSize);

                if (x >= 0 && x < boardWidth && y >= 0 && y < boardHeight)
                {
                    HandleCellClick(new GridCoord(x, y));
                }
            }

            UpdateHighlight();
        }

        void HandleCellClick(GridCoord clicked)
        {
            CellState cell = board.GetCell(clicked);

            if (!hasSelection)
            {
                if (cell.HasPiece && cell.CanOperate && !cell.HasObstacle)
                {
                    selectedCoord = clicked;
                    hasSelection = true;
                    Debug.Log($"选中: {clicked} [{cell.Piece.Element}]");
                }
            }
            else
            {
                if (clicked == selectedCoord)
                {
                    hasSelection = false;
                    Debug.Log("取消选中");
                }
                else if (cell.HasPiece)
                {
                    selectedCoord = clicked;
                    Debug.Log($"选中: {clicked} [{cell.Piece.Element}]");
                }
                else
                {
                    TryMove(selectedCoord, clicked);
                }
            }
        }

        void TryMove(GridCoord from, GridCoord to)
        {
            BoardTurnResult result;
            if (session.TryMove(from, to, out result))
            {
                hasSelection = false;
                remainingSteps = session.RemainingSteps;

                if (result.Synthesized)
                {
                    Debug.Log($"✨ 合成！{result.SpecialPiece.Element} @ {result.SpecialPiece.Coord}");
                }
                else
                {
                    Debug.Log($"移动: {from} → {to}");
                }

                RefreshVisuals();
                UpdateStats();

                if (session.Phase == BoardStagePhase.Ended)
                {
                    Debug.Log($"=== 结束 === 原因: {session.EndReason}");
                }
            }
            else
            {
                Debug.Log("无法移动到此位置");
            }
        }

        void RefreshVisuals()
        {
            // 清空旧棋子
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    if (pieceSprites[x, y] != null)
                    {
                        Destroy(pieceSprites[x, y].gameObject);
                        pieceSprites[x, y] = null;
                    }
                }
            }

            // 重新创建
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    GridCoord coord = new GridCoord(x, y);
                    CellState cell = board.GetCell(coord);
                    if (cell.HasPiece)
                    {
                        CreatePieceSprite(coord, cell.Piece);
                    }
                }
            }
        }

        void UpdateHighlight()
        {
            if (hasSelection)
            {
                selectionHighlight.gameObject.SetActive(true);
                Vector3 pos = new Vector3(selectedCoord.X * cellSize, selectedCoord.Y * cellSize, 0);
                selectionHighlight.transform.position = pos;
                selectionHighlight.transform.localScale = Vector3.one * cellSize;
            }
            else
            {
                selectionHighlight.gameObject.SetActive(false);
            }
        }

        void UpdateStats()
        {
            normalPieceCount = 0;
            specialPieceCount = 0;

            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    CellState cell = board.GetCell(new GridCoord(x, y));
                    if (cell.HasPiece)
                    {
                        if (cell.Piece.Kind == PieceKind.Normal) normalPieceCount++;
                        else specialPieceCount++;
                    }
                }
            }
        }

        void RestartBoard()
        {
            Debug.Log("重新开始...");

            // 清理
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            hasSelection = false;
            pieceSprites = null;
            obstacleSprites = null;

            // 重新初始化
            InitializeBoard();
            CreateVisuals();
        }

        void OnGUI()
        {
            // 简单UI，右上角
            float width = 200;
            float height = 150;
            float x = Screen.width - width - 10;
            float y = 10;

            GUI.Box(new Rect(x, y, width, height), "");

            GUILayout.BeginArea(new Rect(x + 10, y + 10, width - 20, height - 20));

            GUILayout.Label("2D棋盘测试");
            GUILayout.Space(5);

            if (session != null)
            {
                GUILayout.Label($"步数: {remainingSteps}/{stepLimit}");
                GUILayout.Label($"普通: {normalPieceCount}");
                GUILayout.Label($"特种: {specialPieceCount}");
                GUILayout.Label($"状态: {session.Phase}");
            }

            GUILayout.Space(10);
            GUILayout.Label("R - 重新开始");

            GUILayout.EndArea();
        }
    }
}
