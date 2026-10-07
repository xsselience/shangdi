using UnityEngine;

/// <summary>
/// 关卡管理器
/// 负责"当前是第几关"这个全局状态，并按 id 加载对应 LevelConfig
/// UI 想读当前关卡数据：LevelManager.Instance.CurrentLevel
/// 想响应换关：订阅 GameEventCenter.Instance.LevelChanged
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField] private int startLevelId = 1;   // 启动时默认第一关，Inspector 可改

    /// <summary>
    /// 当前关卡 id
    /// </summary>
    public int CurrentLevelId { get; private set; }

    /// <summary>
    /// 当前关卡配置（所有 UI 从这里读数据）
    /// </summary>
    public LevelConfig CurrentLevel { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 启动时加载起始关卡（在 Start 里做，保证事件中心已就绪）
        LoadLevel(startLevelId);
    }

    /// <summary>
    /// 切换关卡：加载配置并广播，所有订阅 LevelChanged 的 UI 自动刷新
    /// </summary>
    public void LoadLevel(int levelId)
    {
        // 配置资产在 Resources/Configs/Levels/ 下（由导表工具生成）
        var level = Resources.Load<LevelConfig>($"Configs/Levels/LevelConfig_{levelId}");

        if (level == null)
        {
            Debug.LogError($"找不到关卡配置：LevelConfig_{levelId}，检查是否已导表");
            return;
        }

        CurrentLevelId = levelId;
        CurrentLevel = level;

        // 广播"换关了"，UI 各自去读自己要的数据
        GameEventCenter.Instance.NotifyLevelChanged(level);
    }
}
