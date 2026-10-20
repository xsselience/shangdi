using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class KeepBoardZ : MonoBehaviour
{
    public Gridmap map;

    [Tooltip("每帧检查一次。关掉则只在 Start 跑一次")]
    public bool keepEveryFrame = true;

    void Start() => Apply();

    void LateUpdate()
    {
        if (keepEveryFrame) Apply();
    }

    [ContextMenu("立刻归零")]
    public void Apply()
    {
        if (map == null) return;

        Vector3 p = map.transform.localPosition;
        if (Mathf.Abs(p.z) < 0.0001f) return;      // 已经是 0 就不动，省开销

        p.z = 0f;
        map.transform.localPosition = p;
    }
}
