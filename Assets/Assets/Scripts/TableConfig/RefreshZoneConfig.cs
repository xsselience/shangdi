using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 刷新区域的元素权重（刷新区域_元素权重表的一行）
/// </summary>
[Serializable]
public class ZoneElemWeightEntry
{
    [SerializeField] private int elemId;
    [SerializeField] private int weight;

    public int ElemId => elemId;
    public int Weight => weight;

    public void SetData(int elemId, int weight)
    {
        this.elemId = elemId;
        this.weight = weight;
    }
}

/// <summary>
/// 刷新区域配置
/// 由导表工具从 Tables/棋盘系统表.xlsx（刷新区域表）自动生成，
/// 元素权重在导入时按 zone_id 合并进 Weights，运行时不用再关联
/// </summary>
[CreateAssetMenu(fileName = "RefreshZoneConfig_", menuName = "ShangDi/RefreshZoneConfig")]
public class RefreshZoneConfig : ScriptableObject
{
    [SerializeField] private int zoneId;
    [SerializeField] private int levelId;
    [SerializeField] private string zoneName;
    [SerializeField] private int rectX1;
    [SerializeField] private int rectY1;
    [SerializeField] private int rectX2;
    [SerializeField] private int rectY2;
    [SerializeField] private int priority;
    [SerializeField] private string stateCondition;   // 例如 "Humidity<2"，空表示无条件
    [SerializeField] private string remark;
    [SerializeField] private List<ZoneElemWeightEntry> weights = new List<ZoneElemWeightEntry>();

    public int ZoneId => zoneId;
    public int LevelId => levelId;
    public string ZoneName => zoneName;
    public int RectX1 => rectX1;
    public int RectY1 => rectY1;
    public int RectX2 => rectX2;
    public int RectY2 => rectY2;
    public int Priority => priority;
    public string StateCondition => stateCondition;
    public string Remark => remark;
    public List<ZoneElemWeightEntry> Weights => weights;

    /// <summary>
    /// 由导表工具写入数据，运行时不要调用
    /// </summary>
    public void SetData(int zoneId, int levelId, string zoneName,
        int rectX1, int rectY1, int rectX2, int rectY2,
        int priority, string stateCondition, string remark,
        List<ZoneElemWeightEntry> weights)
    {
        this.zoneId = zoneId;
        this.levelId = levelId;
        this.zoneName = zoneName;
        this.rectX1 = rectX1;
        this.rectY1 = rectY1;
        this.rectX2 = rectX2;
        this.rectY2 = rectY2;
        this.priority = priority;
        this.stateCondition = stateCondition;
        this.remark = remark;
        this.weights = weights;
    }
}
