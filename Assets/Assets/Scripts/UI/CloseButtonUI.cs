using UnityEngine;

namespace Assets.Scripts.UI
{
    public class CloseButtonUI : MonoBehaviour
    {
        public void OnClickCloseGame()
        {
#if UNITY_EDITOR
            // 编辑器里 Play 模式没有"退出应用"，改为停止播放
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
