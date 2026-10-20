using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CenterBoardInParent : MonoBehaviour
{
    public Gridmap map;

    [ContextMenu("只把棋盘的 z 归零")]
    public void ZeroZ()
    {
        if (map == null)
        {
            Debug.LogWarning("map 未指定", this);
            return;
        }

        Vector3 p = map.transform.localPosition;
        float oldZ = p.z;

        p.z = 0f;
        map.transform.localPosition = p;

        Debug.Log($"已把 {map.name} 的 localPosition.z 从 {oldZ:F2} 改成 0。" +
                  $"棋盘的世界 z 现在固定等于外壳的 z，不会再跟着缩放摆动", this);
    }
}
