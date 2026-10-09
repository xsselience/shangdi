using System;
using System.Collections.Generic;

namespace Game.Board
{
    /// <summary>
    /// 检查移动落点是否形成五连，并返回本次要消除的五个格子。
    /// 特种棋子固定生成在鼠标最终落点，不再提供其他落点模式。
    /// </summary>
    public static class SynthesisResolver
    {
        private const int RequiredCount = 5;
        private static readonly GridCoord[] Axes =
        {
            new GridCoord(1, 0), new GridCoord(0, 1),
            new GridCoord(1, 1), new GridCoord(1, -1)
        };

        public static bool TryResolveSynthesis(BoardState board, GridCoord landing,
            out List<GridCoord> fiveCells, out GridCoord specialSpawnCoord)
        {
            fiveCells = new List<GridCoord>();
            specialSpawnCoord = landing;
            if (!TryFindFiveLine(board, landing, out fiveCells)) return false;
            return true;
        }

        public static bool HasFiveLine(BoardState board, GridCoord landing)
        {
            List<GridCoord> fiveCells;
            return TryFindFiveLine(board, landing, out fiveCells);
        }

        public static bool TryFindFiveLine(BoardState board, GridCoord landing,
            out List<GridCoord> fiveCells)
        {
            fiveCells = new List<GridCoord>();
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (!board.IsInside(landing)) return false;

            CellState landingCell = board.GetCell(landing);
            if (!landingCell.HasPiece || !landingCell.Piece.CanSynthesize) return false;

            ElementType element = landingCell.Piece.Element;
            foreach (GridCoord axis in Axes)
            {
                int landingIndex;
                List<GridCoord> line = GetContinuousLine(board, landing, element, axis, out landingIndex);
                if (line.Count < RequiredCount) continue;
                fiveCells = SelectNearestFive(line, landingIndex);
                return true;
            }
            return false;
        }

        private static List<GridCoord> GetContinuousLine(BoardState board, GridCoord landing,
            ElementType element, GridCoord axis, out int landingIndex)
        {
            List<GridCoord> negativeCells = CollectDirection(board, landing, element, -axis.X, -axis.Y);
            negativeCells.Reverse();
            List<GridCoord> positiveCells = CollectDirection(board, landing, element, axis.X, axis.Y);
            List<GridCoord> line = new List<GridCoord>();
            line.AddRange(negativeCells);
            landingIndex = line.Count;
            line.Add(landing);
            line.AddRange(positiveCells);
            return line;
        }

        private static List<GridCoord> CollectDirection(BoardState board, GridCoord landing,
            ElementType element, int deltaX, int deltaY)
        {
            List<GridCoord> cells = new List<GridCoord>();
            GridCoord current = landing.Offset(deltaX, deltaY);
            while (board.IsInside(current))
            {
                CellState cell = board.GetCell(current);
                if (!cell.HasPiece) break;
                PieceState piece = cell.Piece;
                if (!piece.CanSynthesize || piece.Element != element) break;
                cells.Add(current);
                current = current.Offset(deltaX, deltaY);
            }
            return cells;
        }

        private static List<GridCoord> SelectNearestFive(List<GridCoord> line, int landingIndex)
        {
            int firstStart = Math.Max(0, landingIndex - RequiredCount + 1);
            int lastStart = Math.Min(landingIndex, line.Count - RequiredCount);
            int bestStart = firstStart;
            int bestDistance = int.MaxValue;
            for (int start = firstStart; start <= lastStart; start++)
            {
                int totalDistance = 0;
                for (int offset = 0; offset < RequiredCount; offset++)
                    totalDistance += Math.Abs(start + offset - landingIndex);
                if (totalDistance < bestDistance)
                {
                    bestDistance = totalDistance;
                    bestStart = start;
                }
            }
            return line.GetRange(bestStart, RequiredCount);
        }
    }
}
