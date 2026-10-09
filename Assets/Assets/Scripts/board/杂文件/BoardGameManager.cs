using UnityEngine;
using UnityEngine.UI;

namespace Game.Board
{
    /// <summary>
    /// 棋盘游戏管理器示例。
    /// 监听棋盘事件，显示UI信息，提供测试功能。
    /// </summary>
    public class BoardGameManager : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private BoardController boardController;

        [Header("UI显示（可选）")]
        [SerializeField] private Text stepsText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text messageText;

        [Header("测试功能")]
        [SerializeField] private bool enableKeyboardShortcuts = true;

        private void Awake()
        {
            if (boardController == null)
                boardController = GetComponent<BoardController>();
        }

        private void OnEnable()
        {
            if (boardController != null)
            {
                boardController.TurnCompleted += OnTurnCompleted;
                boardController.StepsChanged += OnStepsChanged;
                boardController.StageEnded += OnStageEnded;
                boardController.StageSettled += OnStageSettled;
            }
        }

        private void OnDisable()
        {
            if (boardController != null)
            {
                boardController.TurnCompleted -= OnTurnCompleted;
                boardController.StepsChanged -= OnStepsChanged;
                boardController.StageEnded -= OnStageEnded;
                boardController.StageSettled -= OnStageSettled;
            }
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts) return;

            // R键：重新初始化
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartBoard();
            }

            // 空格键：手动结束阶段
            if (Input.GetKeyDown(KeyCode.Space))
            {
                EndStageManually();
            }

            // T键：执行随机移动（测试用）
            if (Input.GetKeyDown(KeyCode.T))
            {
                MakeRandomMove();
            }
        }

        private void OnTurnCompleted(BoardTurnResult result)
        {
            if (!result.Success)
            {
                ShowMessage($"移动失败：{result.Failure}", Color.red);
                return;
            }

            string message = $"移动：{result.From} → {result.To}";

            if (result.Synthesized)
            {
                message += $"\n✨ 合成成功！";
                message += $"\n生成特种棋子：{result.SpecialPiece.Element}";
                message += $"\n消除 {result.RemovedPieces.Count} 个普通棋子";
                ShowMessage(message, Color.yellow);
            }
            else if (result.Spawn != null && result.Spawn.Success)
            {
                message += $"\n刷新新棋子：{result.Spawn.Piece.Element}";
                ShowMessage(message, Color.green);
            }
            else
            {
                ShowMessage(message, Color.white);
            }
        }

        private void OnStepsChanged(int remaining, int used)
        {
            if (stepsText != null)
            {
                stepsText.text = $"步数：{remaining}/{remaining + used}";
            }

            Debug.Log($"剩余步数：{remaining}");
        }

        private void OnStageEnded(BoardEndReason reason)
        {
            string message = "阶段结束！\n原因：";

            if ((reason & BoardEndReason.StepsExhausted) != 0)
                message += "\n- 步数用完";
            if ((reason & BoardEndReason.Manual) != 0)
                message += "\n- 手动结束";
            if ((reason & BoardEndReason.BoardFilled) != 0)
                message += "\n- 棋盘填满";

            if (statusText != null)
                statusText.text = "阶段已结束";

            ShowMessage(message, Color.cyan);
        }

        private void OnStageSettled(BoardSettlementResult result)
        {
            string message = $"结算完成\n";
            message += $"使用步数：{result.UsedSteps}\n";
            message += $"剩余步数：{result.RemainingSteps}\n";
            message += $"删除普通棋子：{result.RemovedNormalPieces.Count} 个\n";
            message += $"保留特种棋子：{result.EnvironmentEntries.Count} 个";

            if (result.EnvironmentEntries.Count > 0)
            {
                message += "\n\n特种棋子详情：";
                foreach (var entry in result.EnvironmentEntries)
                {
                    message += $"\n- {entry.Element} @ {entry.Coord}";
                }
            }

            ShowMessage(message, Color.magenta);
        }

        private void ShowMessage(string message, Color color)
        {
            Debug.Log(message);

            if (messageText != null)
            {
                messageText.text = message;
                messageText.color = color;
            }
        }

        // 公开方法：供UI按钮调用

        public void RestartBoard()
        {
            if (boardController == null) return;

            string error;
            if (boardController.TryInitialize(out error))
            {
                ShowMessage("棋盘已重新初始化", Color.green);
            }
            else
            {
                ShowMessage($"初始化失败：{error}", Color.red);
            }
        }

        public void EndStageManually()
        {
            if (boardController == null) return;

            if (boardController.TryEndManually())
            {
                ShowMessage("手动结束阶段", Color.cyan);
            }
            else
            {
                ShowMessage("当前无法手动结束", Color.yellow);
            }
        }

        public void MakeRandomMove()
        {
            if (boardController == null || boardController.Board == null) return;

            BoardState board = boardController.Board;

            // 遍历所有格子，找到第一个可移动的棋子
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    GridCoord from = new GridCoord(x, y);
                    CellState cell = board.GetCell(from);

                    if (!cell.HasPiece) continue;

                    var moves = board.GetAvailableMoves(from);
                    if (moves.Count > 0)
                    {
                        GridCoord to = moves[Random.Range(0, moves.Count)];
                        BoardTurnResult result;
                        boardController.TryMove(from, to, out result);
                        return;
                    }
                }
            }

            ShowMessage("没有可移动的棋子", Color.yellow);
        }
    }
}
