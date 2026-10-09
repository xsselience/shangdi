using UnityEngine;
using Game.Board;

/// <summary>
/// 关卡障碍物配置示例。
/// 展示如何轻松设定障碍物位置。
/// </summary>
public class BoardLevelExample : MonoBehaviour
{
    /// <summary>
    /// 示例1：简单关卡（8个障碍物）
    /// </summary>
    public static BoardLayoutConfig CreateLevel1()
    {
        // 直接列出障碍物坐标，非常直观
        Vector2Int[] obstacles = new Vector2Int[]
        {
            new Vector2Int(2, 2),
            new Vector2Int(3, 3),
            new Vector2Int(4, 4),
            new Vector2Int(5, 5),
            new Vector2Int(6, 6),
            new Vector2Int(7, 7),
            new Vector2Int(2, 7),
            new Vector2Int(7, 2)
        };

        return new BoardLayoutConfig(
            obstacles,
            new Vector2Int(0, 0),  // 起点
            new Vector2Int(9, 9)   // 终点
        );
    }

    /// <summary>
    /// 示例2：中等难度关卡（迷宫风格）
    /// </summary>
    public static BoardLayoutConfig CreateLevel2()
    {
        Vector2Int[] obstacles = new Vector2Int[]
        {
            // 上方墙
            new Vector2Int(1, 7), new Vector2Int(2, 7), new Vector2Int(3, 7),
            new Vector2Int(4, 7), new Vector2Int(5, 7),

            // 左侧墙
            new Vector2Int(1, 3), new Vector2Int(1, 4), new Vector2Int(1, 5),

            // 中央障碍
            new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(6, 4),

            // 右侧墙
            new Vector2Int(8, 2), new Vector2Int(8, 3), new Vector2Int(8, 4)
        };

        return new BoardLayoutConfig(
            obstacles,
            new Vector2Int(0, 0),
            new Vector2Int(9, 9)
        );
    }

    /// <summary>
    /// 示例3：困难关卡（复杂路径）
    /// </summary>
    public static BoardLayoutConfig CreateLevel3()
    {
        Vector2Int[] obstacles = new Vector2Int[]
        {
            // S型路径障碍
            new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3),
            new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3),
            new Vector2Int(5, 4), new Vector2Int(5, 5), new Vector2Int(5, 6),
            new Vector2Int(6, 6), new Vector2Int(7, 6), new Vector2Int(8, 6),
            new Vector2Int(8, 7), new Vector2Int(8, 8),

            // 额外干扰障碍
            new Vector2Int(0, 5), new Vector2Int(1, 7), new Vector2Int(3, 8),
            new Vector2Int(6, 2), new Vector2Int(7, 0)
        };

        return new BoardLayoutConfig(
            obstacles,
            new Vector2Int(0, 0),
            new Vector2Int(9, 9)
        );
    }

    /// <summary>
    /// 示例4：空旷关卡（只有少量障碍）
    /// </summary>
    public static BoardLayoutConfig CreateLevel4()
    {
        Vector2Int[] obstacles = new Vector2Int[]
        {
            new Vector2Int(4, 4),
            new Vector2Int(5, 5),
            new Vector2Int(4, 5),
            new Vector2Int(5, 4)
        };

        return new BoardLayoutConfig(
            obstacles,
            new Vector2Int(0, 0),
            new Vector2Int(9, 9)
        );
    }

    /// <summary>
    /// 示例5：自定义起点终点
    /// </summary>
    public static BoardLayoutConfig CreateLevel5()
    {
        Vector2Int[] obstacles = new Vector2Int[]
        {
            new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3),
            new Vector2Int(3, 6), new Vector2Int(4, 6), new Vector2Int(5, 6)
        };

        return new BoardLayoutConfig(
            obstacles,
            new Vector2Int(1, 1),  // 起点改为(1,1)
            new Vector2Int(8, 8)   // 终点改为(8,8)
        );
    }

    // ======================
    // 使用方法示例
    // ======================

    void Start()
    {
        // 创建关卡配置
        BoardLayoutConfig levelConfig = CreateLevel1();

        // 生成棋盘
        BoardInitialLayoutResult result;
        string error;
        if (BoardInitialLayoutGenerator.TryGenerate(
            10, 10,  // 棋盘大小
            levelConfig,
            out result,
            out error))
        {
            Debug.Log("关卡生成成功！");
            Debug.Log($"障碍物数量: {result.ObstacleCount}");
            Debug.Log($"路径长度: {result.AnimalRoute.Count}");
        }
        else
        {
            Debug.LogError($"关卡生成失败: {error}");
        }
    }
}
