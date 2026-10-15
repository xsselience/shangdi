using UnityEngine;
using UnityEngine.SceneManagement;

public class StartPageBtn : MonoBehaviour
{
    public void OnClickStartGame()
    {
        SceneManager.LoadScene("LevelScene");
    }

    public void OnClickQuitGame()
    {
        Debug.Log("StartPageBtn: 退出游戏", this);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 编辑器里没有"退出"，改成停止播放
#else
        Application.Quit();
#endif
    }
}