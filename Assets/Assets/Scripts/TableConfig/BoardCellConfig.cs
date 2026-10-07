using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋盘格子数据（棋盘格子表的一行）
/// </summary>
[Serializable]
public class BoardCellEntry
{
    [SerializeField] private int boardId;
    [SerializeField] private int posX;
    [SerializeField] private int posY;
    [SerializeField] private int cellType;   // 对应 CellTypeConfig.cellTypeId

    public int BoardId => boardId;
    public int PosX => posX;
    public int PosY => posY;
    public int CellType => cellType;

    public void SetData(int boardId, int posX, int posY, int cellType)
    {
        this.boardId = boardId;
        this.posX = posX;
        this.posY = posY;
        this.cellType = cellType;
    }
}

/// <summary>
/// 棋盘格子表整表配置
/// 一张资产装所有格子行，运行时按 boardId + 坐标查
/// </summary>
[CreateAssetMenu(fileName = "BoardCellTable", menuName = "ShangDi/BoardCellTable")]
public class BoardCellTableConfig : ScriptableObject
{
    [SerializeField] private List<BoardCellEntry> cells = new List<BoardCellEntry>();

    public List<BoardCellEntry> Cells => cells;

    /// <summary>
    /// 由导表工具写入数据（整表覆盖），运行时不要调用
    /// </summary>
    public void SetData(List<BoardCellEntry> cells)
    {
        this.cells = cells;
    }
}
