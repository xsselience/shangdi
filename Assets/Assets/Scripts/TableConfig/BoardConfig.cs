using UnityEngine;

/// <summary>
/// 棋盘配置
/// 由导表工具从 Tables/棋盘系统表.xlsx 自动生成，一般不手动改
/// 表字段：board_id / board_name / width / height / remark
/// </summary>
[CreateAssetMenu(fileName = "BoardConfig_", menuName = "ShangDi/BoardConfig")]
public class BoardConfig : ScriptableObject
{
    [SerializeField] private int boardId;
    [SerializeField] private string boardName;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private string remark;

    public int BoardId => boardId;
    public string BoardName => boardName;
    public int Width => width;
    public int Height => height;
    public string Remark => remark;

    /// <summary>
    /// 由导表工具写入数据，运行时不要调用
    /// </summary>
    public void SetData(int boardId, string boardName, int width, int height, string remark)
    {
        this.boardId = boardId;
        this.boardName = boardName;
        this.width = width;
        this.height = height;
        this.remark = remark;
    }
}
