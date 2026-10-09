using System;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 挂到一个空物体上即可托管棋盘阶段。不自动生成美术棋盘或处理鼠标点击。
    /// 输入/表现脚本调用 TryMove；环境模块订阅 StageSettled。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardController : MonoBehaviour
    {
        [SerializeField] private BoardStageConfig config;
        [SerializeField] private bool initializeOnStart = true;
        [Tooltip("默认结束后立即结算。关闭可为将来的结束动画保留 Ended 状态，再显式调用 TrySettle。")]
        [SerializeField] private bool autoSettle = true;
        [SerializeField] private bool logResults = true;
        [Header("临时逻辑调试，不是美术坐标")]
        [SerializeField] private Vector2Int debugFrom = new Vector2Int(0, 0);
        [SerializeField] private Vector2Int debugTo = new Vector2Int(1, 0);

        private bool executing;
        public BoardInitialLayoutResult InitialLayout { get; private set; }
        public BoardStageSession Session { get; private set; }
        public BoardState Board { get { return Session == null ? null : Session.Board; } }

        // 通知在逻辑提交/自动结算之后发出；TurnCompleted 含结算前回合快照，BoardChanged 是最终棋盘。
        // 所有事件均在 Unity 主线程触发。一个监听器抛异常不影响其他监听器或已提交的回合。
        public event Action<BoardTurnResult> TurnCompleted;
        public event Action<int, int> StepsChanged; // remaining, used
        public event Action<BoardEndReason> StageEnded;
        public event Action<BoardSettlementResult> StageSettled;
        public event Action<BoardState> BoardChanged;

        private void Start()
        {
            if (!initializeOnStart || Session != null) return;
            string error;
            if (!TryInitialize(out error)) Debug.LogError(error, this);
        }

        /// <summary>生成测试空棋盘和初始普通棋子。失败不安装部分初始化的棋盘。</summary>
        public bool TryInitialize(out string error)
        {
            return InitializeCore(null, SpawnContext.Neutral, out error);
        }

        /// <summary>
        /// 关卡生成器使用此入口传入已配置禁区、障碍和初始棋子的棋盘。
        /// 不再另加初始棋子。动物直通路线应在上游生成器中验证。
        /// 成功后棋盘交给 Session 管理，不再从外部直接改动它。
        /// </summary>
        public bool TryInitializeWithBoard(BoardState preparedBoard, SpawnContext context, out string error)
        {
            if (preparedBoard == null) { error = "准备好的棋盘不能为 null。"; return false; }
            return InitializeCore(preparedBoard, context ?? SpawnContext.Neutral, out error);
        }

        private bool InitializeCore(BoardState preparedBoard, SpawnContext context, out string error)
        {
            error = null;
            if (executing || Session != null) { error = "正在处理操作，或本控制器已初始化；不能重复初始化。"; return false; }
            executing = true;
            try
            {
                if (config == null) throw new ArgumentException("请给 BoardController 指定 BoardStageConfig 资源。");
                config.Validate();
                SpawnRules rules = config.SpawnProfile.CreateRules();
                BoardState board = preparedBoard;
                if (board == null)
                {
                    BoardInitialLayoutResult layoutResult;
                    string layoutError;
                    if (!BoardInitialLayoutGenerator.TryGenerate(
                        config.Width,
                        config.Height,
                        config.Layout,
                        out layoutResult,
                        out layoutError))
                    {
                        throw new InvalidOperationException("初始棋盘布局生成失败：" + layoutError);
                    }

                    board = layoutResult.Board;
                    InitialLayout = layoutResult;
                    // 生成初始棋子（使用Unity Random）
                    for (int i = 0; i < config.InitialPieceCount; i++)
                    {
                        SpawnResult spawn = SpawnResolver.TrySpawnOne(board, rules, context);
                        if (!spawn.Success)
                        {
                            throw new InvalidOperationException(
                                "初始棋子生成失败（第 " + (i + 1) + " 颗）：" +
                                spawn.FailureReason + "。请检查范围、权限和属性配置。"
                            );
                        }
                    }
                }
                else if (board.Width != config.Width || board.Height != config.Height)
                    throw new ArgumentException("准备好的棋盘尺寸与阶段配置尺寸不一致。");

                BoardStageSession candidate = new BoardStageSession(board, config.StepLimit, rules, context);
                bool ended = candidate.Phase == BoardStagePhase.Ended;
                BoardSettlementResult settled = null;
                if (ended && autoSettle) candidate.TrySettle(out settled);
                Session = candidate;
                Notify(StepsChanged, Session.RemainingSteps, Session.UsedSteps);
                if (ended) Notify(StageEnded, Session.EndReason);
                if (settled != null) Notify(StageSettled, settled);
                Notify(BoardChanged, Board);
                if (logResults) Debug.Log("棋盘阶段初始化：" + Board.Width + "×" + Board.Height + "，剩余步数=" + Session.RemainingSteps + "，状态=" + Session.Phase, this);
                return true;
            }
            catch (Exception ex) { error = "棋盘初始化失败：" + ex.Message; return false; }
            finally { executing = false; }
        }

        public bool TryMove(GridCoord from, GridCoord to, out BoardTurnResult result)
        {
            result = null;
            if (executing || Session == null) return false;
            executing = true;
            try
            {
                bool success = Session.TryMove(from, to, out result);
                if (!success)
                {
                    if (logResults) Debug.LogWarning("移动未执行：" + result.Failure + (result.Spawn == null ? "" : " / " + result.Spawn.FailureReason), this);
                    return false;
                }
                bool ended = Session.Phase == BoardStagePhase.Ended;
                BoardSettlementResult settled = null;
                if (ended && autoSettle) Session.TrySettle(out settled);
                Notify(TurnCompleted, result);
                Notify(StepsChanged, Session.RemainingSteps, Session.UsedSteps);
                if (ended) Notify(StageEnded, Session.EndReason);
                if (settled != null) Notify(StageSettled, settled);
                Notify(BoardChanged, Board);
                if (logResults) Debug.Log("移动完成：" + from + " → " + to + "，合成=" + result.Synthesized + "，兜底刷新=" + (result.Spawn != null && result.Spawn.UsedFallback) + "，剩余步数=" + Session.RemainingSteps + "，结束原因=" + Session.EndReason, this);
                return true;
            }
            finally { executing = false; }
        }

        public bool TryEndManually()
        {
            if (executing || Session == null) return false;
            executing = true;
            try
            {
                if (!Session.TryEndManually()) return false;
                BoardSettlementResult settled = null;
                if (autoSettle) Session.TrySettle(out settled);
                Notify(StageEnded, Session.EndReason);
                if (settled != null) Notify(StageSettled, settled);
                Notify(BoardChanged, Board);
                if (logResults) Debug.Log("棋盘阶段已手动结束。", this);
                return true;
            }
            finally { executing = false; }
        }

        public bool TrySettle(out BoardSettlementResult result)
        {
            result = null;
            if (executing || Session == null) return false;
            executing = true;
            try
            {
                bool alreadySettled = Session.Phase == BoardStagePhase.Settled;
                if (!Session.TrySettle(out result)) return false;
                if (!alreadySettled) { Notify(StageSettled, result); Notify(BoardChanged, Board); }
                return true;
            }
            finally { executing = false; }
        }

        public bool TrySetSpawnContext(SpawnContext context)
        {
            if (executing || Session == null || Session.Phase != BoardStagePhase.Operating) return false;
            Session.SetSpawnContext(context);
            return true;
        }

        [ContextMenu("调试/初始化棋盘")]
        private void DebugInitialize() { string error; if (!TryInitialize(out error)) Debug.LogWarning(error, this); }
        [ContextMenu("调试/执行指定移动")]
        private void DebugMove()
        {
            BoardTurnResult result;
            if (!TryMove(new GridCoord(debugFrom.x, debugFrom.y), new GridCoord(debugTo.x, debugTo.y), out result) && result == null)
                Debug.LogWarning("控制器未初始化或正在处理操作。", this);
        }
        [ContextMenu("调试/手动结束棋盘阶段")]
        private void DebugEnd() { if (!TryEndManually()) Debug.LogWarning("当前不能手动结束。", this); }
        [ContextMenu("调试/执行阶段结算")]
        private void DebugSettle() { BoardSettlementResult result; if (!TrySettle(out result)) Debug.LogWarning("阶段尚未结束，或控制器未初始化。", this); }

        private void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Delegate listener in handlers.GetInvocationList())
            {
                try { ((Action<T>)listener)(value); }
                catch (Exception ex) { Debug.LogException(ex, this); }
            }
        }
        private void Notify<T1, T2>(Action<T1, T2> handlers, T1 first, T2 second)
        {
            if (handlers == null) return;
            foreach (Delegate listener in handlers.GetInvocationList())
            {
                try { ((Action<T1, T2>)listener)(first, second); }
                catch (Exception ex) { Debug.LogException(ex, this); }
            }
        }
    }
}




