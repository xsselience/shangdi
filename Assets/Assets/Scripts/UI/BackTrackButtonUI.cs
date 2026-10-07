using UnityEngine;

namespace Assets.Scripts.UI
{
    /// <summary>
    /// 回溯按钮
    /// 只负责发"我要回溯"的请求，不关心谁会响应：
    ///   GameManager  → 暂停游戏
    ///   RulePanelUI  → 收起自己
    ///   SavePanelUI  → 弹出存档列表
    /// </summary>
    public class BackTrackButtonUI : MonoBehaviour
    {
        /// <summary>
        /// 绑到 Button 的 OnClick 上
        /// </summary>
        public void OnClickBackTrack()
        {
            GameEventCenter.Instance.RequestRewind();
        }
    }
}
