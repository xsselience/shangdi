using UnityEngine;

/// <summary>
/// 格子类型配置
/// 由导表工具从 Tables/棋盘系统表.xlsx（格子类型表）自动生成
/// 表字段：cell_type_id / cell_type_name / cell_type_key / operable / remark
/// </summary>
[CreateAssetMenu(fileName = "CellTypeConfig_", menuName = "ShangDi/CellTypeConfig")]
public class CellTypeConfig : ScriptableObject
{
    [SerializeField] private int cellTypeId;
    [SerializeField] private string cellTypeName;
    [SerializeField] private string cellTypeKey;   // Blocked / Normal / Special
    [SerializeField] private bool operable;        // 表里 0/1
    [SerializeField] private string remark;

    public int CellTypeId => cellTypeId;
    public string CellTypeName => cellTypeName;
    public string CellTypeKey => cellTypeKey;
    public bool Operable => operable;
    public string Remark => remark;

    /// <summary>
    /// 由导表工具写入数据，运行时不要调用
    /// </summary>
    public void SetData(int cellTypeId, string cellTypeName, string cellTypeKey, bool operable, string remark)
    {
        this.cellTypeId = cellTypeId;
        this.cellTypeName = cellTypeName;
        this.cellTypeKey = cellTypeKey;
        this.operable = operable;
        this.remark = remark;
    }
}
