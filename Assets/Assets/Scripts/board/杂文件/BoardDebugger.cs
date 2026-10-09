using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 棋盘调试工具。在Scene视图和Game视图中显示调试信息。
    /// </summary>
    public class BoardDebugger : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private BoardController controller;
        [SerializeField] private BoardWorldMapper mapper;

        [Header("显示设置")]
        [SerializeField] private bool showCoordinates = true;
        [SerializeField] private bool showPieceInfo = true;
        [SerializeField] private bool showObstacles = true;
        [SerializeField] private bool showCanOperate = false;
        [SerializeField] private float textHeight = 0.5f;
        [SerializeField] private Color coordinateColor = Color.white;
        [SerializeField] private Color pieceInfoColor = Color.yellow;
        [SerializeField] private Color obstacleColor = Color.red;

        private GUIStyle labelStyle;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<BoardController>();
            if (mapper == null) mapper = GetComponent<BoardWorldMapper>();
        }

        private void OnDrawGizmos()
        {
            if (controller == null || mapper == null || controller.Board == null) return;

            BoardState board = controller.Board;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    GridCoord coord = new GridCoord(x, y);
                    CellState cell = board.GetCell(coord);
                    Vector3 worldPos = mapper.GridToWorld(coord);

                    // 绘制障碍物
                    if (showObstacles && cell.HasObstacle)
                    {
                        Gizmos.color = obstacleColor;
                        Gizmos.DrawWireCube(worldPos, Vector3.one * 0.8f);
                    }

                    // 绘制不可操作区域
                    if (showCanOperate && !cell.CanOperate)
                    {
                        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                        Gizmos.DrawCube(worldPos, Vector3.one * 0.9f);
                    }

                    // 绘制坐标
                    if (showCoordinates)
                    {
                        DrawLabel(worldPos + Vector3.up * textHeight,
                            $"({x},{y})", coordinateColor);
                    }

                    // 绘制棋子信息
                    if (showPieceInfo && cell.HasPiece)
                    {
                        PieceState piece = cell.Piece;
                        string info = $"{piece.Element}\n{piece.Kind}\nID:{piece.Id}";
                        DrawLabel(worldPos + Vector3.up * (textHeight + 0.3f),
                            info, pieceInfoColor);
                    }
                }
            }
        }

        private void DrawLabel(Vector3 position, string text, Color color)
        {
#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = color;
            style.fontSize = 10;
            style.alignment = TextAnchor.MiddleCenter;
            UnityEditor.Handles.Label(position, text, style);
#endif
        }

        private void OnGUI()
        {
            if (controller == null || controller.Session == null) return;

            // 显示阶段信息
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Box("棋盘调试信息");

            GUILayout.Label($"阶段状态: {controller.Session.Phase}");
            GUILayout.Label($"剩余步数: {controller.Session.RemainingSteps}/{controller.Session.InitialSteps}");

            if (controller.Session.Phase == BoardStagePhase.Ended)
            {
                GUILayout.Label($"结束原因: {controller.Session.EndReason}");
            }

            if (controller.Board != null)
            {
                int totalPieces = 0;
                int normalPieces = 0;
                int specialPieces = 0;
                int obstacles = 0;

                for (int x = 0; x < controller.Board.Width; x++)
                {
                    for (int y = 0; y < controller.Board.Height; y++)
                    {
                        CellState cell = controller.Board.GetCell(new GridCoord(x, y));
                        if (cell.HasObstacle) obstacles++;
                        if (cell.HasPiece)
                        {
                            totalPieces++;
                            if (cell.Piece.Kind == PieceKind.Normal) normalPieces++;
                            else specialPieces++;
                        }
                    }
                }

                GUILayout.Label($"棋子总数: {totalPieces}");
                GUILayout.Label($"  普通: {normalPieces}");
                GUILayout.Label($"  特种: {specialPieces}");
                GUILayout.Label($"障碍物: {obstacles}");
            }

            GUILayout.EndArea();
        }
    }
}
