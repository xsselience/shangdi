using UnityEngine;

/// <summary>
/// 关卡配置
/// 由导表工具从 Tables/棋盘系统表.xlsx（关卡表）自动生成
/// 表字段：level_id / level_order / level_name / board_id / init_steps /
///         init_piece_count / task_type / task_desc / task_preview / remark
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig_", menuName = "ShangDi/LevelConfig")]
public class LevelConfig : ScriptableObject
{
    [SerializeField] private int levelId;
    [SerializeField] private int levelOrder;
    [SerializeField] private string levelName;
    [SerializeField] private int boardId;
    [SerializeField] private int initSteps;
    [SerializeField] private int initPieceCount;
    [SerializeField] private string taskType;     // Escort / Intercept / Mixed
    [SerializeField] private string taskDesc;
    [SerializeField] private string taskPreview;
    [SerializeField] private string remark;

    // 表里没有的字段，导表不会覆盖，需要时手动在 Inspector 里填
    [SerializeField] private float timeLimit = 600f;   // 倒计时（秒）

    public int LevelId => levelId;
    public int LevelOrder => levelOrder;
    public string LevelName => levelName;
    public int BoardId => boardId;
    public int InitSteps => initSteps;
    public int InitPieceCount => initPieceCount;
    public string TaskType => taskType;
    public string TaskDesc => taskDesc;
    public string TaskPreview => taskPreview;
    public string Remark => remark;
    public float TimeLimit => timeLimit;

    /// <summary>
    /// 由导表工具写入数据，运行时不要调用
    /// </summary>
    public void SetData(int levelId, int levelOrder, string levelName, int boardId,
        int initSteps, int initPieceCount, string taskType, string taskDesc,
        string taskPreview, string remark)
    {
        this.levelId = levelId;
        this.levelOrder = levelOrder;
        this.levelName = levelName;
        this.boardId = boardId;
        this.initSteps = initSteps;
        this.initPieceCount = initPieceCount;
        this.taskType = taskType;
        this.taskDesc = taskDesc;
        this.taskPreview = taskPreview;
        this.remark = remark;
    }
}
