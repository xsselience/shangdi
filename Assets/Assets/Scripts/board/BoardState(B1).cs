using System;
using System.Collections.Generic;

namespace Game.Board
{
    /// <summary>
    /// 棋盘逻辑数据的核心类。
    /// </summary>
    public class BoardState
    {
        private readonly int width;
        private readonly int height;
        private readonly CellState[,] cells;
        private int nextPieceId;

        public int Width { get { return width; } }
        public int Height { get { return height; } }

        /// <summary>
        /// 创建一个指定大小的空棋盘。
        /// 当前版本所有格子均可操作、可刷新、没有障碍。
        /// </summary>
        public BoardState(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "棋盘宽度必须大于 0。");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "棋盘高度必须大于 0。");

            this.width = width;
            this.height = height;
            nextPieceId = 1;

            cells = new CellState[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    GridCoord coord = new GridCoord(x, y);
                    cells[x, y] = new CellState(coord, true, true, false);
                }
            }
        }

        /// <summary>
        /// 初始化空格权限和障碍。关卡生成器使用，程序员也可用。
        /// 有棋子的格子不能调用此方法，否则丢失棋子。
        /// </summary>
        public void ConfigureEmptyCell(GridCoord coord, bool canOperate, bool canSpawn, bool hasObstacle)
        {
            CellState oldCell = GetCell(coord);
            if (oldCell.HasPiece)
                throw new InvalidOperationException("有棋子的格子不能重新配置。");
            cells[coord.X, coord.Y] = new CellState(coord, canOperate, canSpawn, hasObstacle);
        }

        /// <summary>
        /// 判断坐标是否在棋盘范围内。
        /// 坐标不合法返回 false，不抛出异常。
        /// </summary>
        public bool IsInside(GridCoord coord)
        {
            return coord.X >= 0 && coord.X < width && coord.Y >= 0 && coord.Y < height;
        }

        /// <summary>
        /// 获取指定位置的格子。
        /// 提供坐标超出范围会抛出异常。
        /// </summary>
        public CellState GetCell(GridCoord coord)
        {
            if (!IsInside(coord))
                throw new ArgumentOutOfRangeException(nameof(coord), "坐标不在棋盘范围内：" + coord);
            return cells[coord.X, coord.Y];
        }

        /// <summary>
        /// 创建一个新棋子，并放到指定格子。
        /// 这是底层数据操作，不负责刷新或完整移动。
        /// 棋子编号会自动递增。
        /// </summary>
        public PieceState CreatePieceAt(GridCoord coord, ElementType element, PieceKind kind)
        {
            CellState cell = GetCell(coord);
            PieceState newPiece = new PieceState(nextPieceId, element, kind);
            cell.PlacePiece(newPiece);
            nextPieceId++;
            return newPiece;
        }

        /// <summary>
        /// 从指定格子取出棋子，并清空该格。
        /// 如果原本没有棋子，返回 null。
        /// 这是底层数据操作，不负责完整移动流程。
        /// </summary>
        public PieceState RemovePieceAt(GridCoord coord)
        {
            CellState cell = GetCell(coord);
            return cell.RemovePiece();
        }

        /// <summary>
        /// 判断能否将棋子移动到指定位置。
        /// 允许直接移动到全棋盘任意可操作、无障碍的空格，不检查距离或路径。
        /// 不检查步数、不改变棋盘状态。
        /// </summary>
        public bool CanTransferPiece(GridCoord from, GridCoord to)
        {
            if (!IsInside(from) || !IsInside(to)) return false;

            CellState fromCell = GetCell(from);
            CellState toCell = GetCell(to);
            if (!fromCell.CanOperate || !toCell.CanOperate) return false;
            if (fromCell.HasObstacle || toCell.HasObstacle) return false;
            if (!fromCell.HasPiece || toCell.HasPiece) return false;
            return true;
        }

        /// <summary>
        /// 获取棋子当前所有合法的移动目标。
        /// 返回全棋盘内所有合法目标的坐标，不受距离、方向或途中阻挡影响。
        /// 不移动棋子，不改变棋盘。
        /// 没有合法目标时返回空列表。
        /// </summary>
        public List<GridCoord> GetAvailableMoves(GridCoord from)
        {
            List<GridCoord> availableMoves = new List<GridCoord>();
            if (!IsInside(from)) return availableMoves;

            CellState fromCell = GetCell(from);
            if (!fromCell.CanOperate || fromCell.HasObstacle || !fromCell.HasPiece)
                return availableMoves;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridCoord target = new GridCoord(x, y);
                    if (CanTransferPiece(from, target))
                        availableMoves.Add(target);
                }
            }
            return availableMoves;
        }

        /// <summary>
        /// 尝试将棋子直接移动到全棋盘任意合法空格。
        /// 成功返回 true，检查失败返回 false。
        /// 本方法只负责转移，不触发合成和刷新。
        /// </summary>
        internal bool TryTransferPiece(GridCoord from, GridCoord to)
        {
            if (!CanTransferPiece(from, to)) return false;
            CellState fromCell = GetCell(from);
            CellState toCell = GetCell(to);
            PieceState movingPiece = fromCell.RemovePiece();
            toCell.PlacePiece(movingPiece);
            return true;
        }

        /// <summary>
        /// 快照类：仅内部回滚使用。棋子不可变，故快照只需记录引用即可，不复制格子对象。
        /// 只在同一移动/合成/刷新/回滚内使用，不是存档或编辑接口。
        /// </summary>
        internal sealed class PieceSnapshot
        {
            internal readonly BoardState Owner;
            internal readonly PieceState[,] Pieces;
            internal readonly int NextPieceId;

            internal PieceSnapshot(BoardState owner, PieceState[,] pieces, int nextId)
            {
                Owner = owner;
                Pieces = pieces;
                NextPieceId = nextId;
            }
        }

        internal PieceSnapshot CapturePieceSnapshot()
        {
            PieceState[,] pieces = new PieceState[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    pieces[x, y] = cells[x, y].Piece;
            return new PieceSnapshot(this, pieces, nextPieceId);
        }

        internal void RestorePieceSnapshot(PieceSnapshot snapshot)
        {
            if (snapshot == null || !ReferenceEquals(snapshot.Owner, this))
                throw new ArgumentException("快照不属于当前棋盘。", nameof(snapshot));
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    CellState cell = cells[x, y];
                    if (ReferenceEquals(cell.Piece, snapshot.Pieces[x, y])) continue;
                    cell.RemovePiece();
                    if (snapshot.Pieces[x, y] != null)
                        cell.PlacePiece(snapshot.Pieces[x, y]);
                }
            nextPieceId = snapshot.NextPieceId;
        }
    }
}
