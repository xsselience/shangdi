using UnityEngine;


/// <summary>
/// 游戏状态,后期可以扩展游戏状态
/// </summary>
public enum GameState
{
    BeforePlaying,   // 游戏开始前
    Playing,    // 游戏中
    Paused      // 暂停
}
/// <summary>
/// 游戏状态管理器
/// 负责控制游戏当前处于什么状态
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>
    /// 当前游戏状态
    /// </summary>
    public GameState CurrentState { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        // 游戏启动时默认在开局前状态，全局暂停，等待点击开始
        CurrentState = GameState.BeforePlaying;

        Time.timeScale = 0f;
    }


    private void OnEnable()
    {
        // 订阅事件
        if (GameEventCenter.Instance != null)
        {
            GameEventCenter.Instance.StartGameRequested += OnStartGameRequested;
            GameEventCenter.Instance.PauseGameRequested += OnPauseGameRequested;
            GameEventCenter.Instance.ResumeGameRequested += OnResumeGameRequested;
            GameEventCenter.Instance.RewindRequested += OnRewindRequested;
        }
    }


    private void OnDisable()
    {
        // 取消订阅
        if (GameEventCenter.Instance == null)
            return;

        GameEventCenter.Instance.StartGameRequested -= OnStartGameRequested;
        GameEventCenter.Instance.PauseGameRequested -= OnPauseGameRequested;
        GameEventCenter.Instance.ResumeGameRequested -= OnResumeGameRequested;
        GameEventCenter.Instance.RewindRequested -= OnRewindRequested;
    }


    /// <summary>
    /// 收到回溯请求：校验通过才暂停游戏并广播"回溯已生效"
    /// 请求被拒（非游戏中状态）时什么都不发生，面板不会乱动
    /// </summary>
    private void OnRewindRequested()
    {
        if (CurrentState != GameState.Playing)
            return;

        PauseGame();
        GameEventCenter.Instance.NotifyRewindStarted();
    }


    /// <summary>
    /// 收到开始游戏请求
    /// </summary>
    private void OnStartGameRequested()
    {
        StartGame();
    }


    /// <summary>
    /// 收到暂停请求
    /// </summary>
    private void OnPauseGameRequested()
    {
        PauseGame();
    }


    /// <summary>
    /// 收到继续请求
    /// </summary>
    private void OnResumeGameRequested()
    {
        ResumeGame();
    }


    /// <summary>
    /// 开始游戏
    /// </summary>
    private void StartGame()
    {
        if (CurrentState != GameState.BeforePlaying)
            return;

        Time.timeScale = 1f;

        SetGameState(GameState.Playing);
    }


    /// <summary>
    /// 暂停游戏
    /// </summary>
    private void PauseGame()
    {
        // 防止重复暂停
        if (CurrentState != GameState.Playing)
            return;

        Time.timeScale = 0f;

        SetGameState(GameState.Paused);

        // 广播"暂停已生效"，面板显示听这个
        GameEventCenter.Instance.NotifyGamePauseStarted();
    }


    /// <summary>
    /// 继续游戏
    /// </summary>
    private void ResumeGame()
    {
        // 只有暂停状态才能继续
        if (CurrentState != GameState.Paused)
            return;

        Time.timeScale = 1f;

        SetGameState(GameState.Playing);
    }


    /// <summary>
    /// 修改游戏状态
    /// </summary>
    private void SetGameState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;

        // 通知所有监听者
        GameEventCenter.Instance.NotifyGameStateChanged(newState);
    }
}