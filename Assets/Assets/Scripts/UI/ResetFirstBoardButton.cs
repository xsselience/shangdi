using UnityEngine;
using Game.Board;

/// 调试用：把第一棋盘重置回「当前阶段」的开局状态。
public class ResetFirstBoardButton : MonoBehaviour
{
    [Header("引用")]
    public BoardSceneBridge boardBridge;

    [Tooltip("留空则只重置第一棋盘；填上则连第二棋盘的战局一起退回赛前")]
    public BattleFlow battleFlow;

    [Header("界面")]
    [Tooltip("点击之后要隐藏的背景 / 面板")]
    public GameObject backgroundToHide;

    [Header("行为")]
    public bool logReset = true;

    /// <summary>按钮接这个</summary>
    [ContextMenu("重置当前阶段的第一棋盘")]
    public void ResetCurrentStage()
    {
        if (boardBridge == null)
        {
            Debug.LogWarning("ResetFirstBoardButton: boardBridge 未指定", this);
            return;
        }

        int index = boardBridge.CurrentStageIndex;

        // ① 先把战局退回赛前
        if (battleFlow != null && battleFlow.Current != BattleFlow.Phase.PreBattle)
            battleFlow.ResetThisLevel();

        // ② 再按"当前阶段"重建第一棋盘
        boardBridge.RunStage(index);

        // ③ 隐藏背景
        if (backgroundToHide != null)
            backgroundToHide.SetActive(false);

        if (logReset)
            Debug.Log($"ResetFirstBoardButton: 已把第一棋盘重置回第 {index + 1} 阶段的开局", this);
    }
}