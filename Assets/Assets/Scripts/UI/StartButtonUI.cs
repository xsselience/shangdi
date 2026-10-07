using UnityEngine;

public class StartButtonUI : MonoBehaviour
{
    [SerializeField]
    private RectTransform mainScreen;

    [SerializeField]
    private RectTransform subScreen;

    private void OnEnable()
    {
        GameEventCenter.Instance.GameStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        if (GameEventCenter.Instance == null)
            return;

        GameEventCenter.Instance.GameStateChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.BeforePlaying:
                ShowMainMenu();
                break;

            case GameState.Playing:
                ShowPlaying();
                break;
        }
    }


    private void ShowMainMenu()
    {
        mainScreen.localScale = Vector3.one;
        subScreen.localScale = Vector3.one;
    }


    private void ShowPlaying()
    {
        mainScreen.localScale = Vector3.one * 0.9f;
        subScreen.localScale = Vector3.one * 1.1f;
    }
    
    public void OnClickStartLevel()
    {
        GameState state = GameManager.Instance.CurrentState;
        switch (state)
        {
            case GameState.BeforePlaying:
                GameEventCenter.Instance.RequestStartGame();
                break;
            case GameState.Playing:
                GameEventCenter.Instance.RequestPauseGame();
                break;
        }
    }
}