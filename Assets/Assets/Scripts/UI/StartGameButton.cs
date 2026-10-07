using UnityEngine;
using UnityEngine.SceneManagement;

public class StartPageBtn : MonoBehaviour
{
    public void OnClickStartGame()
    {
        SceneManager.LoadScene("LevelScene");
    }
}