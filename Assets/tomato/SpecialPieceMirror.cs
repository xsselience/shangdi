using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Board;

public class SpecialPieceMirror : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("第一棋盘")]
    public BoardSceneBridge boardBridge;

    [Tooltip("第二棋盘的世界格子")]
    public Gridmap worldMap;

    [Header("调试")]
    public bool logChanges = true;

    bool _warnedSize;

    void OnEnable()
    {
        if (boardBridge != null) boardBridge.TurnCompleted += OnTurnCompleted;
    }

    void OnDisable()
    {
        if (boardBridge != null) boardBridge.TurnCompleted -= OnTurnCompleted;
    }

    void Start()
    {
        if (boardBridge == null) Debug.LogError("SpecialPieceMirror: boardBridge 未指定", this);
        if (worldMap == null) Debug.LogError("SpecialPieceMirror: worldMap 未指定", this);
    }

    // 每一次成功落子之后同步一次
    void OnTurnCompleted(BoardTurnResult result)
    {
        if (result == null || !result.Success) return;
        if (worldMap == null || boardBridge == null || boardBridge.Board == null) return;

        CheckSize(boardBridge.Board);

        // ① 特殊棋子被移动了：先搬走原位置，再放到新位置
        if (result.MovedPiece != null && result.MovedPiece.Kind == PieceKind.Special)
            MoveSpecial(result.From, result.To, result.MovedPiece.Element);

        // ② 这一步合成了新特殊棋子
        if (result.SpecialPiece != null)
        {
            Vector2Int coord = ToWorld(result.SpecialPiece.Coord);
            SpecialElement element = ToSpecial(result.SpecialPiece.Element);

            if (worldMap.SetSpecial(coord, element) && logChanges)
                Debug.Log($"SpecialPieceMirror: 合成 —— 在 {coord} 生成特殊{element}", this);
        }
    }

    void MoveSpecial(GridCoord from, GridCoord to, ElementType element)
    {
        Vector2Int f = ToWorld(from);
        Vector2Int t = ToWorld(to);
        SpecialElement e = ToSpecial(element);

        worldMap.SetSpecial(f, SpecialElement.None);
        worldMap.SetSpecial(t, e);

        if (logChanges) Debug.Log($"SpecialPieceMirror: 移动 —— 特殊{e} {f} → {t}", this);
    }

    [ContextMenu("清空第二棋盘的特殊棋子")]
    public void ClearWorldSpecials()
    {
        if (worldMap != null) worldMap.ClearSpecials();
    }

    void CheckSize(BoardState board)
    {
        if (_warnedSize) return;
        if (board.Width == worldMap.width && board.Height == worldMap.height) return;

        _warnedSize = true;
        Debug.LogWarning("SpecialPieceMirror: 两个棋盘尺寸不一致 —— 第一棋盘 " +
                         board.Width + "×" + board.Height +
                         "，第二棋盘 " + worldMap.width + "×" + worldMap.height +
                         "。镜像按同坐标直接对应，尺寸不同会错位。", this);
    }

    static Vector2Int ToWorld(GridCoord c) => new Vector2Int(c.X, c.Y);

    static SpecialElement ToSpecial(ElementType e)
    {
        switch (e)
        {
            case ElementType.Fire: return SpecialElement.Fire;
            case ElementType.Water: return SpecialElement.Water;
            case ElementType.Wind: return SpecialElement.Wind;
            case ElementType.ground: return SpecialElement.Ground;
            default: return SpecialElement.None;
        }
    }
}
