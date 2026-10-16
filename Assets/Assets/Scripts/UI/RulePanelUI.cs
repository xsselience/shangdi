using UnityEngine;

namespace Assets.Scripts.UI
{
    /// <summary>
    /// 规则面板。显示 / 隐藏由外部直接调用，不再依赖事件中心。
    /// </summary>
    public class RulePanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;

        [Header("初始")]
        [Tooltip("开局先把面板藏起来")]
        [SerializeField] private bool hideOnStart = true;

        [Header("顺带的游戏状态")]
        [Tooltip("显示面板时是否顺便请求暂停。默认不请求 —— 由调用方决定")]
        [SerializeField] private bool pauseGameWhenShown = false;

        [Tooltip("关闭面板时是否顺便请求恢复游戏")]
        [SerializeField] private bool resumeGameWhenClosed = true;

        void Start()
        {
            if (hideOnStart && _panelRoot != null)
                _panelRoot.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  直接调用的入口
        // ─────────────────────────────────────────────
        /// <summary>显示规则面板。</summary>
        public void Show()
        {
            if (_panelRoot == null)
            {
                Debug.LogWarning("RulePanelUI: _panelRoot 未指定，面板不会显示", this);
                return;
            }

            _panelRoot.SetActive(true);

            if (pauseGameWhenShown && GameEventCenter.Instance != null)
                GameEventCenter.Instance.RequestPauseGame();
        }

        /// <summary>隐藏规则面板。</summary>
        public void Hide()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);

            if (resumeGameWhenClosed && GameEventCenter.Instance != null)
                GameEventCenter.Instance.RequestResumeGame();
        }

        /// <summary>已显示就隐藏，已隐藏就显示。</summary>
        public void Toggle()
        {
            if (_panelRoot == null) return;

            if (_panelRoot.activeSelf) Hide();
            else Show();
        }

        /// <summary>面板上的关闭按钮挂这个。</summary>
        public void OnClickClose()
        {
            Hide();
        }

        public bool IsShowing => _panelRoot != null && _panelRoot.activeSelf;
    }
}
