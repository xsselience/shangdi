using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// 点这个世界物体时触发一个 UnityEvent。
/// 需要同物体上有 Collider2D。挂在 Gridmap 物体上最合适。
[RequireComponent(typeof(Collider2D))]
public class WorldClickRelay : MonoBehaviour
{
    [Header("点击")]
    [Tooltip("点中时触发。把 StartButtonUI.FocusSecondBoard 挂进来")]
    public UnityEvent onClick;

    [Tooltip("鼠标停在 UI 上时不响应（避免隔着一整块 Canvas 还能点到）")]
    public bool ignoreWhenOverUI = true;

    [Header("自动贴合棋盘")]
    [Tooltip("勾上则自动把 BoxCollider2D 调成覆盖整个棋盘")]
    public bool autoFitToBoard = true;

    public Gridmap board;

    void LateUpdate()
    {
        if (!autoFitToBoard || board == null) return;

        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;

        box.offset = board.BoardCenterLocal;
        box.size = new Vector2(board.width * board.cellSize,
                               board.height * board.cellSize);
    }

    void OnMouseDown()
    {
        if (ignoreWhenOverUI && EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        Debug.Log($"WorldClickRelay: 点中 {name}", this);

        if (onClick != null) onClick.Invoke();
    }
}
