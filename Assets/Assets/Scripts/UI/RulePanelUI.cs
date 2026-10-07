using UnityEngine;

namespace Assets.Scripts.UI
{
    /// <summary>
    /// 规则面板
    /// </summary>
    public class RulePanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;

        private void OnEnable()
        {
            GameEventCenter.Instance.GamePauseStarted += Show;
            GameEventCenter.Instance.RewindStarted += Hide;
        }

        private void OnDisable()
        {
            // 退出 Play 时事件中心可能先被销毁，判空防止 OnDisable 报错
            if (GameEventCenter.Instance == null)
                return;

            GameEventCenter.Instance.GamePauseStarted -= Show;
            GameEventCenter.Instance.RewindStarted -= Hide;
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
    }
}
