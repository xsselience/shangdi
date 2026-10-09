using System;
using UnityEngine;

public enum MoveMode { Fastest }        // 现在只有一个模式

[Serializable]
public class UnitIntent
{
    public Vector2Int target;
    public MoveMode mode = MoveMode.Fastest;
    public bool locked;                 // 开战后 true：路径只读 + 开始执行
}
