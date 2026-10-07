using System;
using UnityEngine;

/// <summary>
/// 游戏事件中心
/// 负责模块之间的消息传递
/// 不负责具体业务逻辑
/// </summary>
public class GameEventCenter : MonoBehaviour
{
    private static GameEventCenter _instance;

    public static GameEventCenter Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameEventCenter>();
            }

            return _instance;
        }
    }

    // =========================
    // 请求事件
    // =========================

    /// <summary>
    /// 请求开始游戏
    /// </summary>
    public event Action StartGameRequested;

    /// <summary>
    /// 请求暂停游戏
    /// </summary>
    public event Action PauseGameRequested;

    /// <summary>
    /// 请求继续游戏
    /// </summary>
    public event Action ResumeGameRequested;

    /// <summary>
    /// 请求存档
    /// </summary>
    public event Action SaveRequested;

    /// <summary>
    /// 请求回溯
    /// </summary>
    public event Action RewindRequested;

    /// <summary>
    /// 请求修改音量
    /// </summary>
    public event Action<float> VolumeChanged;


    // =========================
    // 状态事件
    // =========================

    /// <summary>
    /// 游戏状态发生变化
    /// </summary>
    public event Action<GameState> GameStateChanged;


    // =========================
    // 数据事件
    // =========================

    /// <summary>
    /// 当前关卡发生变化（换关/开局时由 LevelManager 广播）
    /// 参数是新的关卡配置，UI 直接从里面读数据
    /// </summary>
    public event Action<LevelConfig> LevelChanged;

    /// <summary>
    /// 暂停已生效（GameManager 校验通过后才广播）
    /// 面板显示听这个，不要听 PauseGameRequested——请求可能被拒
    /// </summary>
    public event Action GamePauseStarted;

    /// <summary>
    /// 回溯已生效（GameManager 校验通过后才广播）
    /// </summary>
    public event Action RewindStarted;


    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // 如果以后切场景，事件中心可以继续存在
        DontDestroyOnLoad(gameObject);
    }


    // =========================
    // 请求方法
    // =========================

    public void RequestStartGame()
    {
        StartGameRequested?.Invoke();
    }

    public void RequestPauseGame()
    {
        PauseGameRequested?.Invoke();
    }

    public void RequestResumeGame()
    {
        ResumeGameRequested?.Invoke();
    }

    public void RequestSave()
    {
        SaveRequested?.Invoke();
    }

    public void RequestRewind()
    {
        RewindRequested?.Invoke();
    }

    public void RequestVolumeChange(float value)
    {
        VolumeChanged?.Invoke(value);
    }


    // =========================
    // 状态通知
    // =========================

    public void NotifyGameStateChanged(GameState state)
    {
        GameStateChanged?.Invoke(state);
    }

    /// <summary>
    /// 通知当前关卡变化
    /// </summary>
    public void NotifyLevelChanged(LevelConfig level)
    {
        LevelChanged?.Invoke(level);
    }

    /// <summary>
    /// 通知暂停已生效
    /// </summary>
    public void NotifyGamePauseStarted()
    {
        GamePauseStarted?.Invoke();
    }

    /// <summary>
    /// 通知回溯已生效
    /// </summary>
    public void NotifyRewindStarted()
    {
        RewindStarted?.Invoke();
    }
}