using UnityEngine;
using Game.Board;

/// 按下之后：清空第二棋盘的特殊棋子和营地，然后进入最后一个阶段。
public class EndlessStageButton : MonoBehaviour
{
    [Header("引用")]
    public BoardSceneBridge boardBridge;
    public Gridmap worldMap;

    [Header("行为")]
    [Tooltip("清空之后要进入的阶段下标。-1 = 最后一个阶段")]
    public int targetStage = -1;

    public void OnClick()
    {
        if (boardBridge == null)
        {
            Debug.LogWarning("EndlessStageButton: boardBridge 未指定", this);
            return;
        }

        // ① 清空第二棋盘的运行时内容
        if (worldMap != null)
        {
            worldMap.ClearCamps();
            worldMap.ClearSpecials();
        }

        // ② 进最后一个阶段
        int index = targetStage >= 0 ? targetStage : boardBridge.StageCount - 1;

        boardBridge.RunStage(index);
        gameObject.SetActive(false);

        Debug.Log($"EndlessStageButton: 已清空第二棋盘，进入第 {index + 1} 阶段", this);
    }
}