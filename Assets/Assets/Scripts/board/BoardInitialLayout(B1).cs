// B部分初始障碍配置和路线验证。
// 障碍物现在使用固定配置，不再随机生成。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;


// ===== 障碍物配置 =====
namespace Game.Board
{
    /// <summary>
    /// 初始棋盘布局配置。
    /// 障碍物使用固定坐标列表，与关卡设计保持一致。
    /// </summary>
    [Serializable]
    public sealed class BoardLayoutConfig
    {
        [Tooltip("固定的障碍物坐标列表。格式：(x, y)")]
        [SerializeField] private Vector2Int[] obstaclePositions = new Vector2Int[0];

        [Tooltip("动物直通路线起点，使用棋盘逻辑坐标。")]
        [SerializeField] private Vector2Int animalStart = new Vector2Int(0, 0);

        [Tooltip("动物直通路线终点，使用棋盘逻辑坐标。")]
        [SerializeField] private Vector2Int animalEnd = new Vector2Int(9, 9);

        public ReadOnlyCollection<Vector2Int> ObstaclePositions
        {
            get { return Array.AsReadOnly(obstaclePositions); }
        }

        public Vector2Int AnimalStart { get { return animalStart; } }
        public Vector2Int AnimalEnd { get { return animalEnd; } }

        public GridCoord StartCoord
        {
            get { return new GridCoord(animalStart.x, animalStart.y); }
        }

        public GridCoord EndCoord
        {
            get { return new GridCoord(animalEnd.x, animalEnd.y); }
        }

        /// <summary>
        /// 快速创建配置（代码中使用）
        /// </summary>
        public BoardLayoutConfig(Vector2Int[] obstacles, Vector2Int start, Vector2Int end)
        {
            this.obstaclePositions = obstacles ?? new Vector2Int[0];
            this.animalStart = start;
            this.animalEnd = end;
        }

        /// <summary>
        /// 默认构造函数（Unity序列化需要）
        /// </summary>
        public BoardLayoutConfig()
        {
            this.obstaclePositions = new Vector2Int[0];
            this.animalStart = new Vector2Int(0, 0);
            this.animalEnd = new Vector2Int(9, 9);
        }

        internal void Validate(int boardWidth, int boardHeight)
        {
            if (boardWidth <= 0 || boardHeight <= 0)
                throw new ArgumentException("棋盘宽高必须大于 0。");

            ValidateInside(StartCoord, boardWidth, boardHeight, "动物起点");
            ValidateInside(EndCoord, boardWidth, boardHeight, "动物终点");

            if (StartCoord == EndCoord)
                throw new ArgumentException("动物起点和终点不能是同一个格子。");

            // 验证所有障碍物坐标合法
            foreach (Vector2Int pos in obstaclePositions)
            {
                GridCoord coord = new GridCoord(pos.x, pos.y);
                ValidateInside(coord, boardWidth, boardHeight, "障碍物");

                if (coord == StartCoord)
                    throw new ArgumentException($"障碍物不能放在起点位置：{pos}");
                if (coord == EndCoord)
                    throw new ArgumentException($"障碍物不能放在终点位置：{pos}");
            }
        }

        private static void ValidateInside(GridCoord coord, int width, int height, string label)
        {
            if (coord.X < 0 || coord.X >= width || coord.Y < 0 || coord.Y >= height)
                throw new ArgumentException(
                    $"{label}超出棋盘范围：{coord}。棋盘大小为 {width}×{height}。");
        }
    }
}

// ===== 路线验证 =====
namespace Game.Board
{
    /// <summary>
    /// 验证四方向直通路线（BFS广度优先搜索）。
    /// 用于检查障碍物配置是否堵死了动物的通路。
    /// </summary>
    public static class BoardRouteValidator
    {
        public static bool CanReach(BoardState board, GridCoord start, GridCoord end)
        {
            List<GridCoord> path;
            return TryFindPath(board, start, end, out path);
        }

        public static bool TryFindPath(
            BoardState board,
            GridCoord start,
            GridCoord end,
            out List<GridCoord> path)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            path = new List<GridCoord>();
            if (!board.IsInside(start) || !board.IsInside(end)) return false;
            if (!IsPassable(board, start) || !IsPassable(board, end)) return false;

            Queue<GridCoord> pending = new Queue<GridCoord>();
            HashSet<GridCoord> visited = new HashSet<GridCoord>();
            Dictionary<GridCoord, GridCoord> previous = new Dictionary<GridCoord, GridCoord>();

            pending.Enqueue(start);
            visited.Add(start);

            while (pending.Count > 0)
            {
                GridCoord current = pending.Dequeue();
                if (current == end)
                {
                    path = RebuildPath(previous, start, end);
                    return true;
                }

                foreach (GridCoord next in Neighbors(current))
                {
                    if (!board.IsInside(next) || visited.Contains(next) || !IsPassable(board, next))
                        continue;

                    visited.Add(next);
                    previous[next] = current;
                    pending.Enqueue(next);
                }
            }

            return false;
        }

        private static bool IsPassable(BoardState board, GridCoord coord)
        {
            CellState cell = board.GetCell(coord);
            return cell.CanOperate && !cell.HasObstacle;
        }

        private static IEnumerable<GridCoord> Neighbors(GridCoord coord)
        {
            yield return coord.Offset(1, 0);   // 右
            yield return coord.Offset(-1, 0);  // 左
            yield return coord.Offset(0, 1);   // 上
            yield return coord.Offset(0, -1);  // 下
        }

        private static List<GridCoord> RebuildPath(
            Dictionary<GridCoord, GridCoord> previous,
            GridCoord start,
            GridCoord end)
        {
            List<GridCoord> reversed = new List<GridCoord>();
            GridCoord current = end;
            reversed.Add(current);

            while (current != start)
            {
                current = previous[current];
                reversed.Add(current);
            }

            reversed.Reverse();
            return reversed;
        }
    }
}

// ===== 布局生成器 =====
namespace Game.Board
{
    /// <summary>初始布局生成结果。</summary>
    public sealed class BoardInitialLayoutResult
    {
        public BoardState Board { get; private set; }
        public int ObstacleCount { get; private set; }
        public ReadOnlyCollection<GridCoord> AnimalRoute { get; private set; }

        internal BoardInitialLayoutResult(
            BoardState board,
            int obstacleCount,
            List<GridCoord> animalRoute)
        {
            Board = board;
            ObstacleCount = obstacleCount;
            AnimalRoute = new List<GridCoord>(animalRoute).AsReadOnly();
        }
    }

    /// <summary>
    /// 使用固定坐标配置生成初始障碍布局。
    /// 障碍物位置由关卡配置决定，不再随机生成。
    /// </summary>
    public static class BoardInitialLayoutGenerator
    {
        public static bool TryGenerate(
            int width,
            int height,
            BoardLayoutConfig config,
            out BoardInitialLayoutResult result,
            out string error)
        {
            result = null;
            error = null;

            if (config == null)
            {
                error = "初始布局配置不能为 null。";
                return false;
            }

            try
            {
                config.Validate(width, height);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            // 创建空棋盘
            BoardState board = new BoardState(width, height);
            GridCoord start = config.StartCoord;
            GridCoord end = config.EndCoord;

            // 检查空棋盘是否连通
            if (!BoardRouteValidator.CanReach(board, start, end))
            {
                error = "空棋盘上的动物起点和终点无法连通。";
                return false;
            }

            // 按配置放置障碍物
            foreach (Vector2Int pos in config.ObstaclePositions)
            {
                GridCoord coord = new GridCoord(pos.x, pos.y);
                board.ConfigureEmptyCell(coord, false, false, true);
            }

            // 验证障碍物放置后是否还能连通
            List<GridCoord> route;
            if (!BoardRouteValidator.TryFindPath(board, start, end, out route))
            {
                error = $"障碍物配置堵死了路径！起点{start}无法到达终点{end}。请调整障碍物位置。";
                return false;
            }

            result = new BoardInitialLayoutResult(
                board,
                config.ObstaclePositions.Count,
                route
            );
            return true;
        }
    }
}
