using UnityEngine;

namespace Assets.Scripts.UI
{
    /// <summary>
    /// 存档面板（回溯时选择存档点）
    /// 显示时机由"用户的意图"决定：
    ///   用户请求回溯 → 出现
    ///   用户点关闭   → 消失并请求恢复游戏
    /// 脚本必须挂在常激活的对象上，_panelRoot 才是默认隐藏的面板本体
    /// </summary>
    public class SavePanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;

        private void OnEnable()
        {
            GameEventCenter.Instance.RewindStarted += Show;
        }

        private void OnDisable()
        {
            // 退出 Play 时事件中心可能先被销毁，判空防止 OnDisable 报错
            if (GameEventCenter.Instance == null)
                return;

            GameEventCenter.Instance.RewindStarted -= Show;
        }

        private void Show()
        {
            _panelRoot.SetActive(true);
        }

        private void Hide()
        {
            _panelRoot.SetActive(false);
        }

        public void OnClickClose()
        {
            Hide();
            GameEventCenter.Instance.RequestResumeGame();
        }

        // TODO: 选中某个存档 → 通知 RewindManager 回溯到该存档点
    }
}
