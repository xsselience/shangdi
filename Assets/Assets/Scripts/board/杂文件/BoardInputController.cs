using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 使用鼠标完成最小可用的棋盘输入：点击棋子，再点击相邻空格。
    /// 不负责高亮和 UI；表现层可以读取 SelectedCoord 做自己的选中显示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardInputController : MonoBehaviour
    {
        [SerializeField] private BoardController controller;
        [SerializeField] private BoardWorldMapper mapper;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private bool inputEnabled = true;

        private bool hasSelection;
        private GridCoord selectedCoord;

        public bool InputEnabled
        {
            get { return inputEnabled; }
            set { inputEnabled = value; }
        }

        public bool HasSelection { get { return hasSelection; } }
        public GridCoord SelectedCoord { get { return selectedCoord; } }

        private void Reset()
        {
            controller = GetComponent<BoardController>();
            mapper = GetComponent<BoardWorldMapper>();
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<BoardController>();
            if (mapper == null) mapper = GetComponent<BoardWorldMapper>();
            if (inputCamera == null) inputCamera = Camera.main;
        }

        private void Update()
        {
            if (!inputEnabled || controller == null || mapper == null || controller.Board == null) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (inputCamera == null) inputCamera = Camera.main;
            if (inputCamera == null) return;

            GridCoord clicked;
            Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            if (!mapper.TryRayToGrid(ray, controller.Board.Width, controller.Board.Height, out clicked))
                return;

            HandleGridClick(clicked);
        }

        public void ClearSelection()
        {
            hasSelection = false;
        }

        private void HandleGridClick(GridCoord clicked)
        {
            CellState cell = controller.Board.GetCell(clicked);
            if (!hasSelection)
            {
                if (cell.HasPiece && cell.CanOperate && !cell.HasObstacle)
                {
                    selectedCoord = clicked;
                    hasSelection = true;
                }
                return;
            }

            if (clicked == selectedCoord)
            {
                hasSelection = false;
                return;
            }

            if (cell.HasPiece && cell.CanOperate && !cell.HasObstacle)
            {
                selectedCoord = clicked;
                return;
            }

            BoardTurnResult result;
            if (controller.TryMove(selectedCoord, clicked, out result))
            {
                hasSelection = false;
            }
        }
    }
}
