using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 将 BoardController 的逻辑状态同步到场景中的棋子和障碍物预制体。
    /// 当前版本直接同步位置，不负责移动动画；后续动画系统可订阅 BoardController 事件替换此实现。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private BoardController controller;
        [SerializeField] private BoardWorldMapper mapper;
        [Header("棋子预制体")]
        [SerializeField] private GameObject normalPiecePrefab;
        [SerializeField] private GameObject specialPiecePrefab;
        [Header("障碍预制体")]
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private Transform pieceRoot;
        [SerializeField] private Transform obstacleRoot;
        [SerializeField] private bool rebuildOnEnable = true;

        private readonly Dictionary<int, GameObject> pieceViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<GridCoord, GameObject> obstacleViews = new Dictionary<GridCoord, GameObject>();
        private bool subscribed;

        private void Reset()
        {
            controller = GetComponent<BoardController>();
            mapper = GetComponent<BoardWorldMapper>();
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<BoardController>();
            if (mapper == null) mapper = GetComponent<BoardWorldMapper>();
            if (pieceRoot == null) pieceRoot = transform;
            if (obstacleRoot == null) obstacleRoot = transform;
        }

        private void OnEnable()
        {
            Subscribe();
            if (rebuildOnEnable) TryRebuild();
        }

        private void Start()
        {
            TryRebuild();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Rebuild()
        {
            if (!TryRebuild())
                throw new InvalidOperationException("BoardView 当前无法重建：请检查 BoardController、BoardWorldMapper 和阶段初始化状态。");
        }

        private bool TryRebuild()
        {
            if (controller == null || mapper == null || controller.Board == null) return false;
            SyncObstacles(controller.Board);
            SyncPieces(controller.Board);
            return true;
        }

        private void Subscribe()
        {
            if (subscribed || controller == null) return;
            controller.BoardChanged += OnBoardChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || controller == null) return;
            controller.BoardChanged -= OnBoardChanged;
            subscribed = false;
        }

        private void OnBoardChanged(BoardState board)
        {
            SyncObstacles(board);
            SyncPieces(board);
        }

        private void SyncObstacles(BoardState board)
        {
            HashSet<GridCoord> live = new HashSet<GridCoord>();
            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                GridCoord coord = new GridCoord(x, y);
                if (!board.GetCell(coord).HasObstacle) continue;
                live.Add(coord);
                if (obstacleViews.ContainsKey(coord))
                {
                    obstacleViews[coord].transform.position = mapper.GridToWorld(coord);
                    continue;
                }
                if (obstaclePrefab == null) continue;
                GameObject view = Instantiate(obstaclePrefab, mapper.GridToWorld(coord), obstaclePrefab.transform.rotation, obstacleRoot);
                obstacleViews.Add(coord, view);
            }

            List<GridCoord> stale = new List<GridCoord>();
            foreach (KeyValuePair<GridCoord, GameObject> pair in obstacleViews)
            {
                if (live.Contains(pair.Key)) continue;
                if (pair.Value != null) Destroy(pair.Value);
                stale.Add(pair.Key);
            }
            foreach (GridCoord coord in stale) obstacleViews.Remove(coord);
        }

        private void SyncPieces(BoardState board)
        {
            HashSet<int> live = new HashSet<int>();
            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                GridCoord coord = new GridCoord(x, y);
                PieceState piece = board.GetCell(coord).Piece;
                if (piece == null) continue;
                live.Add(piece.Id);

                GameObject view;
                if (!pieceViews.TryGetValue(piece.Id, out view) || view == null)
                {
                    GameObject prefab = piece.Kind == PieceKind.Special ? specialPiecePrefab : normalPiecePrefab;
                    if (prefab == null) continue;
                    view = Instantiate(prefab, mapper.GridToWorld(coord), prefab.transform.rotation, pieceRoot);
                    pieceViews[piece.Id] = view;
                }
                else
                {
                    view.transform.position = mapper.GridToWorld(coord);
                }
            }

            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, GameObject> pair in pieceViews)
            {
                if (live.Contains(pair.Key)) continue;
                if (pair.Value != null) Destroy(pair.Value);
                stale.Add(pair.Key);
            }
            foreach (int id in stale) pieceViews.Remove(id);
        }
    }
}
