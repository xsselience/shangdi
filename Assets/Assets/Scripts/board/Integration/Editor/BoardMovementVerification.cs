using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Board;

/// <summary>全棋盘直接移动规则的确定性回归测试，不修改场景或 UI。</summary>
public static class BoardMovementVerification
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Board/Verify Free Movement")]
    public static void Run()
    {
        foreach (string check in Verify()) UnityEngine.Debug.Log("BOARD_MOVE_VERIFY PASS: " + check);
    }
#endif

    public static List<string> Verify()
    {
        var checks = new List<string>();
        VerifyAllDestinations();
        checks.Add("合法目标覆盖全棋盘，允许非直线远距离移动和跨越棋子/障碍");
        VerifyInvalidMoves();
        checks.Add("占用、障碍、不可操作、越界和原地目标均被拒绝，失败不改变棋盘");
        VerifySpecialPieceAndFullBoard();
        checks.Add("特种棋子同样可以全图移动，满棋盘没有合法目标");
        VerifySessionAndSynthesis();
        checks.Add("远距离移动只扣一步，非法移动不扣步，五连合成和结束判断保持有效");
        VerifyRollback();
        checks.Add("远距离移动后刷新失败仍回滚棋子和步数");
        return checks;
    }

    private static void VerifyAllDestinations()
    {
        var board = new BoardState(6, 5);
        var from = new GridCoord(1, 1);
        // 封住起点的四个邻格：无需通路，仍可飞到任意其他合法空格。
        board.ConfigureEmptyCell(new GridCoord(0, 1), true, true, true);
        board.ConfigureEmptyCell(new GridCoord(2, 1), true, true, true);
        board.ConfigureEmptyCell(new GridCoord(1, 0), true, true, true);
        board.CreatePieceAt(new GridCoord(1, 2), ElementType.Water, PieceKind.Normal);
        board.ConfigureEmptyCell(new GridCoord(4, 3), false, true, false);
        var to = new GridCoord(5, 4);
        board.ConfigureEmptyCell(to, true, false, false); // 禁止刷新不等于禁止移动。
        var moving = board.CreatePieceAt(from, ElementType.Fire, PieceKind.Normal);
        var moves = board.GetAvailableMoves(from);
        var uniqueMoves = new HashSet<GridCoord>(moves);
        Check(moves.Count == 24 && uniqueMoves.Count == moves.Count, "合法目标数错误或重复");
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            var target = new GridCoord(x, y);
            var cell = board.GetCell(target);
            bool expected = cell.CanOperate && !cell.HasObstacle && !cell.HasPiece;
            Check(board.CanTransferPiece(from, target) == expected && uniqueMoves.Contains(target) == expected,
                  "目标列表与全棋盘移动检查不一致：" + target);
        }
        Check(Transfer(board, from, to), "被包围的棋子不能飞到非直线远距离空格");
        Check(!board.GetCell(from).HasPiece && ReferenceEquals(board.GetCell(to).Piece, moving),
              "移动没有保留棋子身份或没有清空原格");
    }

    private static void VerifyInvalidMoves()
    {
        var board = new BoardState(4, 4);
        var from = new GridCoord(0, 0);
        var moving = board.CreatePieceAt(from, ElementType.Fire, PieceKind.Normal);
        var occupied = new GridCoord(3, 3);
        var other = board.CreatePieceAt(occupied, ElementType.Water, PieceKind.Normal);
        var obstacle = new GridCoord(2, 2);
        var locked = new GridCoord(3, 2);
        board.ConfigureEmptyCell(obstacle, true, true, true);
        board.ConfigureEmptyCell(locked, false, true, false);
        foreach (var to in new[] { from, occupied, obstacle, locked, new GridCoord(-1, 0), new GridCoord(4, 4) })
            Check(!board.CanTransferPiece(from, to) && !Transfer(board, from, to), "非法目标被允许：" + to);
        Check(ReferenceEquals(board.GetCell(from).Piece, moving) && ReferenceEquals(board.GetCell(occupied).Piece, other),
              "非法移动改变了棋子");
        Check(board.GetAvailableMoves(new GridCoord(-1, 0)).Count == 0 &&
              board.GetAvailableMoves(new GridCoord(1, 1)).Count == 0, "无效或空的起点产生了合法移动");
        Check(!board.CanTransferPiece(new GridCoord(1, 1), new GridCoord(2, 3)), "空起点允许移动");
        board.CreatePieceAt(locked, ElementType.Wind, PieceKind.Normal);
        Check(board.GetAvailableMoves(locked).Count == 0 && !board.CanTransferPiece(locked, new GridCoord(1, 1)),
              "不可操作的起点允许移动");
    }

    private static void VerifySpecialPieceAndFullBoard()
    {
        var board = new BoardState(3, 2);
        var from = new GridCoord(0, 0);
        board.CreatePieceAt(from, ElementType.Wind, PieceKind.Special);
        Check(board.GetAvailableMoves(from).Count == 5 && Transfer(board, from, new GridCoord(2, 1)),
              "特种棋子未使用全棋盘移动规则");
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            var coord = new GridCoord(x, y);
            if (!board.GetCell(coord).HasPiece) board.CreatePieceAt(coord, ElementType.Fire, PieceKind.Normal);
        }
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
            Check(board.GetAvailableMoves(new GridCoord(x, y)).Count == 0, "满棋盘仍存在合法移动");
        Check(BoardEndResolver.IsBoardFilled(board), "满棋盘结束判断失效");
    }

    private static void VerifySessionAndSynthesis()
    {
        var board = new BoardState(6, 5);
        // 合成回合无需随机刷新，测试可脱离 Editor 运行且结果确定。
        for (int x = 0; x < board.Width; x++)
            board.ConfigureEmptyCell(new GridCoord(x, 2), true, true, true);
        for (int x = 0; x < 4; x++)
            board.CreatePieceAt(new GridCoord(x, 0), ElementType.Fire, PieceKind.Normal);
        var from = new GridCoord(5, 4);
        var to = new GridCoord(4, 0);
        board.ConfigureEmptyCell(to, true, false, false);
        var moving = board.CreatePieceAt(from, ElementType.Fire, PieceKind.Normal);
        var session = new BoardStageSession(board, 3, Rules(board));
        BoardTurnResult result;
        Check(!session.TryMove(from, new GridCoord(0, 0), out result) && result.Failure == BoardTurnFailure.IllegalMove &&
              session.RemainingSteps == 3 && ReferenceEquals(board.GetCell(from).Piece, moving),
              "非法移动扣步或改变棋子");
        Check(session.TryMove(from, to, out result) && result.Success && result.Synthesized &&
              result.RemovedPieces.Count == 5 && ReferenceEquals(result.MovedPiece, moving) &&
              result.From == from && result.To == to && session.RemainingSteps == 2 && session.UsedSteps == 1 &&
              board.GetCell(to).Piece.Kind == PieceKind.Special && !board.GetCell(from).HasPiece,
              "跨障碍远距离回合未正确扣一步或触发五连合成");
        Check(session.Phase == BoardStagePhase.Operating && session.EndReason == BoardEndReason.None,
              "远距离移动错误结束阶段");
    }

    private static void VerifyRollback()
    {
        var board = new BoardState(3, 3);
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
            board.ConfigureEmptyCell(new GridCoord(x, y), true, false, false);
        var from = new GridCoord(0, 0);
        var to = new GridCoord(2, 2);
        var moving = board.CreatePieceAt(from, ElementType.Fire, PieceKind.Normal);
        var session = new BoardStageSession(board, 2, Rules(board));
        BoardTurnResult result;
        Check(board.CanTransferPiece(from, to), "禁止刷新被错误用于限制移动");
        Check(!session.TryMove(from, to, out result) && result.Failure == BoardTurnFailure.SpawnFailed &&
              session.RemainingSteps == 2 && ReferenceEquals(board.GetCell(from).Piece, moving) && !board.GetCell(to).HasPiece,
              "远距离移动刷新失败没有完整回滚");
    }

    private static SpawnRules Rules(BoardState board)
    {
        return new SpawnRules(new[] { new SpawnArea(0, 0, board.Width, board.Height) },
            new[] { ElementType.Fire }, new ElementWeights(1, 0, 0, 0), new RegionWeightRule[0]);
    }

    // Editor 测试程序集不能直接调用 runtime 的 internal 方法，保留原有封装边界。
    private static bool Transfer(BoardState board, GridCoord from, GridCoord to)
    {
        var method = typeof(BoardState).GetMethod("TryTransferPiece", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("底层移动方法不存在。");
        return (bool)method.Invoke(board, new object[] { from, to });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
