// B部分阶段流程、结果数据和结束判断。
// 原有多个脚本已合并；公开类型名称保持不变。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;


// ===== 原文件：BoardStageSession.cs =====
namespace Game.Board
{
    /// <summary>
    /// 一局棋盘阶段的纯逻辑入口。初始化结束后，玩家移动/结束/结算统一经过这里。
    /// 不绕过本类直接修改 Board；不依赖 UI、场景、动画、单位寻路。
    /// </summary>
    public sealed class BoardStageSession
    {
        private readonly SpawnRules spawnRules;
        private SpawnContext spawnContext;
        private BoardSettlementResult settlement;

        public BoardState Board { get; private set; }
        public int InitialSteps { get; private set; }
        public int RemainingSteps { get; private set; }
        public int UsedSteps { get { return InitialSteps - RemainingSteps; } }
        public BoardStagePhase Phase { get; private set; }
        public BoardEndReason EndReason { get; private set; }
        public BoardSettlementResult Settlement { get { return settlement; } }

        public BoardStageSession(BoardState board, int initialSteps, SpawnRules spawnRules,
            SpawnContext context = null)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (initialSteps < 0) throw new ArgumentOutOfRangeException(nameof(initialSteps));
            if (spawnRules == null) throw new ArgumentNullException(nameof(spawnRules));
            bool hasPlayableCell = false;
            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                CellState cell = board.GetCell(new GridCoord(x, y));
                if (cell.CanOperate && !cell.HasObstacle) hasPlayableCell = true;
            }
            if (!hasPlayableCell) throw new ArgumentException("棋盘至少需要一个可操作、无障碍的格子。", nameof(board));
            Board = board; InitialSteps = initialSteps; RemainingSteps = initialSteps;
            this.spawnRules = spawnRules;
            spawnContext = context ?? SpawnContext.Neutral;
            Phase = BoardStagePhase.Operating;
            UpdateEnding(false);
        }

        public void SetSpawnContext(SpawnContext context)
        {
            if (Phase != BoardStagePhase.Operating)
                throw new InvalidOperationException("棋盘阶段已结束，不能继续更新刷新环境。");
            spawnContext = context ?? SpawnContext.Neutral;
        }

        public bool TryMove(GridCoord from, GridCoord to, out BoardTurnResult result)
        {
            if (Phase != BoardStagePhase.Operating)
            {
                result = Failure(from, to, BoardTurnFailure.StageNotOperating, null);
                return false;
            }
            if (!Board.CanTransferPiece(from, to))
            {
                result = Failure(from, to, BoardTurnFailure.IllegalMove, null);
                return false;
            }

            BoardState.PieceSnapshot before = Board.CapturePieceSnapshot();
            int previousSteps = RemainingSteps;
            BoardStagePhase previousPhase = Phase;
            BoardEndReason previousReason = EndReason;
            try
            {
                PieceState moved = Board.GetCell(from).Piece;
                if (!Board.TryTransferPiece(from, to)) throw new InvalidOperationException("移动状态与检查结果不一致。");
                List<GridCoord> five;
                GridCoord specialCoord;
                List<PlacedPieceSnapshot> removed = new List<PlacedPieceSnapshot>();
                PlacedPieceSnapshot special = null;
                SpawnResult spawn = null;
                if (SynthesisResolver.TryResolveSynthesis(Board, to, out five, out specialCoord))
                {
                    foreach (GridCoord coord in five)
                        removed.Add(new PlacedPieceSnapshot(coord, Board.RemovePieceAt(coord)));
                    special = new PlacedPieceSnapshot(specialCoord,
                        Board.CreatePieceAt(specialCoord, moved.Element, PieceKind.Special));
                }
                else
                {
                    spawn = SpawnResolver.TrySpawnAfterMove(Board, spawnRules, spawnContext);
                    if (!spawn.Success)
                    {
                        Board.RestorePieceSnapshot(before);
                        result = Failure(from, to, BoardTurnFailure.SpawnFailed, spawn);
                        return false;
                    }
                }
                RemainingSteps--;
                UpdateEnding(false);
                result = new BoardTurnResult(true, BoardTurnFailure.None, from, to, moved,
                    removed, special, spawn, RemainingSteps, EndReason);
                return true;
            }
            catch
            {
                Board.RestorePieceSnapshot(before);
                RemainingSteps = previousSteps; Phase = previousPhase; EndReason = previousReason;
                throw;
            }
        }

        public bool TryEndManually()
        {
            if (Phase != BoardStagePhase.Operating) return false;
            UpdateEnding(true);
            return true;
        }

        public bool TrySettle(out BoardSettlementResult result)
        {
            if (Phase == BoardStagePhase.Operating) { result = null; return false; }
            if (settlement != null) { result = settlement; return true; }
            List<PlacedPieceSnapshot> removed = new List<PlacedPieceSnapshot>();
            List<BoardEnvironmentEntry> environment = new List<BoardEnvironmentEntry>();
            for (int x = 0; x < Board.Width; x++)
            for (int y = 0; y < Board.Height; y++)
            {
                GridCoord coord = new GridCoord(x, y);
                PieceState piece = Board.GetCell(coord).Piece;
                if (piece == null) continue;
                if (piece.Kind == PieceKind.Normal) removed.Add(new PlacedPieceSnapshot(coord, piece));
                else environment.Add(new BoardEnvironmentEntry(coord, piece));
            }
            result = new BoardSettlementResult(EndReason, UsedSteps, RemainingSteps, removed, environment);
            BoardState.PieceSnapshot before = Board.CapturePieceSnapshot();
            try
            {
                foreach (PlacedPieceSnapshot entry in removed) Board.RemovePieceAt(entry.Coord);
                settlement = result;
                Phase = BoardStagePhase.Settled;
                return true;
            }
            catch { Board.RestorePieceSnapshot(before); throw; }
        }

        private void UpdateEnding(bool manual)
        {
            EndReason = BoardEndResolver.Evaluate(Board, RemainingSteps, manual);
            if (EndReason != BoardEndReason.None) Phase = BoardStagePhase.Ended;
        }

        private BoardTurnResult Failure(GridCoord from, GridCoord to, BoardTurnFailure failure, SpawnResult spawn)
        {
            return new BoardTurnResult(false, failure, from, to, null,
                new List<PlacedPieceSnapshot>(), null, spawn, RemainingSteps, EndReason);
        }
    }
}

// ===== 原文件：BoardStageTypes.cs =====
namespace Game.Board
{
    public enum BoardStagePhase { Operating = 0, Ended = 1, Settled = 2 }

    // 可同时记录最后一步既用完步数又填满棋盘的情况。
    [Flags]
    public enum BoardEndReason
    {
        None = 0,
        StepsExhausted = 1,
        Manual = 2,
        BoardFilled = 4
    }

    public enum BoardTurnFailure
    {
        None = 0,
        StageNotOperating = 1,
        IllegalMove = 2,
        SpawnFailed = 3
    }

    /// <summary>棋子在某个时刻的位置快照；移动后的实时位置不要从旧快照读取。</summary>
    public sealed class PlacedPieceSnapshot
    {
        public GridCoord Coord { get; private set; }
        public PieceState Piece { get; private set; }
        public int Id { get { return Piece.Id; } }
        public ElementType Element { get { return Piece.Element; } }
        public PieceKind Kind { get { return Piece.Kind; } }

        internal PlacedPieceSnapshot(GridCoord coord, PieceState piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            Coord = coord; Piece = piece;
        }
    }

    public sealed class BoardTurnResult
    {
        public bool Success { get; private set; }
        public BoardTurnFailure Failure { get; private set; }
        public GridCoord From { get; private set; }
        public GridCoord To { get; private set; }
        public PieceState MovedPiece { get; private set; }
        public ReadOnlyCollection<PlacedPieceSnapshot> RemovedPieces { get; private set; }
        public PlacedPieceSnapshot SpecialPiece { get; private set; }
        public SpawnResult Spawn { get; private set; }
        public int RemainingSteps { get; private set; }
        public BoardEndReason EndReason { get; private set; }
        public bool Synthesized { get { return SpecialPiece != null; } }

        internal BoardTurnResult(bool success, BoardTurnFailure failure,
            GridCoord from, GridCoord to, PieceState movedPiece,
            List<PlacedPieceSnapshot> removed, PlacedPieceSnapshot special,
            SpawnResult spawn, int remainingSteps, BoardEndReason endReason)
        {
            Success = success; Failure = failure; From = from; To = to;
            MovedPiece = movedPiece;
            RemovedPieces = new List<PlacedPieceSnapshot>(removed).AsReadOnly();
            SpecialPiece = special; Spawn = spawn;
            RemainingSteps = remainingSteps; EndReason = endReason;
        }
    }

    /// <summary>
    /// 交给环境模块的数据，不执行水火风土效果，不判游戏胜负。
    /// PieceId 只在本棋盘内唯一，跨关卡系统应附加自己的关卡/棋盘标识。
    /// </summary>
    public sealed class BoardEnvironmentEntry
    {
        public GridCoord Coord { get; private set; }
        public int PieceId { get; private set; }
        public ElementType Element { get; private set; }

        internal BoardEnvironmentEntry(GridCoord coord, PieceState piece)
        {
            Coord = coord; PieceId = piece.Id; Element = piece.Element;
        }
    }

    public sealed class BoardSettlementResult
    {
        public BoardEndReason EndReason { get; private set; }
        public int UsedSteps { get; private set; }
        public int RemainingSteps { get; private set; }
        public ReadOnlyCollection<PlacedPieceSnapshot> RemovedNormalPieces { get; private set; }
        public ReadOnlyCollection<BoardEnvironmentEntry> EnvironmentEntries { get; private set; }

        internal BoardSettlementResult(BoardEndReason reason, int usedSteps, int remainingSteps,
            List<PlacedPieceSnapshot> removed, List<BoardEnvironmentEntry> environment)
        {
            EndReason = reason; UsedSteps = usedSteps; RemainingSteps = remainingSteps;
            RemovedNormalPieces = new List<PlacedPieceSnapshot>(removed).AsReadOnly();
            EnvironmentEntries = new List<BoardEnvironmentEntry>(environment).AsReadOnly();
        }
    }
}

// ===== 原文件：BoardEndResolver.cs =====
namespace Game.Board
{
    /// <summary>只判断棋盘阶段结束，不负责游戏失败。</summary>
    public static class BoardEndResolver
    {
        /// <summary>
        /// 可操作且无障碍的格子全有棋子才算填满。
        /// CanSpawn 只限制刷新，不改变棋盘容量；没有任何可操作格不算填满。
        /// 没有合法相邻移动，不是新增的第四种结束条件。
        /// </summary>
        public static bool IsBoardFilled(BoardState board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            bool hasPlayableCell = false;
            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                CellState cell = board.GetCell(new GridCoord(x, y));
                if (!cell.CanOperate || cell.HasObstacle) continue;
                hasPlayableCell = true;
                if (!cell.HasPiece) return false;
            }
            return hasPlayableCell;
        }

        public static BoardEndReason Evaluate(BoardState board, int remainingSteps, bool manual = false)
        {
            if (remainingSteps < 0) throw new ArgumentOutOfRangeException(nameof(remainingSteps));
            BoardEndReason reason = BoardEndReason.None;
            if (remainingSteps == 0) reason |= BoardEndReason.StepsExhausted;
            if (manual) reason |= BoardEndReason.Manual;
            if (IsBoardFilled(board)) reason |= BoardEndReason.BoardFilled;
            return reason;
        }
    }
}
