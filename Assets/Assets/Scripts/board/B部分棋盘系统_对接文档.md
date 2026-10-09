# B部分棋盘系统对接文档



## 系统概述




### B部分范围


- 棋盘数据结构
- 棋子生成、移动、合成
- 五连检测
- 步数管理
- 关卡结算
- 坐标转换（逻辑↔世界）



---

## 核心文件功能说明

###  数据层

#### `BoardData.cs` 
**功能**：定义所有基础数据结构
- `GridCoord`: 格子坐标 (x, y)
- `ElementType`: 元素类型（火/水/风/土/无）
- `PieceKind`: 棋子类型（普通/特种）
- `PieceState`: 棋子状态（ID、元素、类型）
- `CellState`: 格子状态（棋子、障碍物、操作权限）

#### `BoardState.cs`
**功能**：棋盘状态管理
- 维护整个棋盘的数据（宽×高）
- 创建/移动/删除棋子
- 查询格子状态
- 配置格子属性（是否可操作、是否有障碍）



#### `BoardStageSession.cs` 
**功能**：关卡会话管理（核心逻辑）
- 管理一个关卡的完整生命周期
- 处理玩家移动
- 触发五连检测和合成
- 管理步数
- 判断关卡结束
- 提供结算数据



/


#### `SynthesisResolver.cs` 
**功能**：五连检测和合成判断
- 检测四个方向（横/竖/两斜）
- 找到五个连续相同元素的普通棋子
- 选择最接近落点的5个
- 特种棋子不参与合成

#### `SpawnProfile.cs` 
**功能**：棋子生成系统
- 定义刷新区域
- 定义元素权重
- 移动后自动刷新一个新棋子

---

#### `BoardInitialLayout.cs` (308行)
**功能**：初始棋盘生成
- 随机生成障碍物（确保路径通畅）
- 随机生成初始棋子
- 验证棋盘可玩性



---

#### `BoardStageConfig.cs` + `SpawnProfile.cs`
**功能**：关卡配置（ScriptableObject）
- 棋盘尺寸
- 步数限制
- 初始棋子数量
- 障碍物数量
- 刷新规则

*
---

#### `BoardWorldMapper.cs` 
**功能**：坐标转换
- 逻辑坐标 ↔ Unity世界坐标
- 支持自定义原点、格子大小、方向



以下脚本是辅助开发用的，**不是核心系统**，可以删除：

-
- ✅ `BoardSimpleTest2D.cs`: 当前2D测试脚本（测试用）
- `PieceVisualizer.cs`: 自动设置棋子颜色
- `BoardGameManager.cs`: 示例游戏管理器
- `BoardSelectionHighlight.cs`: 选中高亮
- `BoardDebugger.cs`: 调试信息显示
- `Editor/BoardControllerEditor.cs`: Inspector扩展
- `Editor/BoardQuickSetup.cs`: 快速设置工具



---

修改内容    │         文件位置          │  行数   │
├───────────────┼───────────────────────────┼─────────┤
│ 障碍物数量    │ BoardInitialLayout(B1).cs │ 22      │
├───────────────┼───────────────────────────┼─────────┤
│ 起点/终点坐标 │ BoardInitialLayout(B1).cs │ 25, 28  │
├───────────────┼───────────────────────────┼─────────┤
│ 初始棋子数量  │ 测试脚本或你的游戏控制器  │ -       │
├───────────────┼───────────────────────────┼─────────┤
│ 元素类型权重  │ 测试脚本或你的游戏控制器  │ -       │
├───────────────┼───────────────────────────┼─────────┤
│ 限制特定区域  │ 自定义候选列表逻辑        │ 280-296 │

修改障碍物数量
文件：BoardInitialLayout(B1).cs:22
[SerializeField, Min(0)] private int obstacleCount = 15;  // 改这里

修改起点/终点位置
文件：BoardInitialLayout(B1).cs:25-28
// 动物直通路线起点
[SerializeField] private Vector2Int animalStart = new Vector2Int(0, 0);  // 左下角
// 动物直通路线终点
[SerializeField] private Vector2Int animalEnd = new Vector2Int(9, 9);    // 右上角

修改元素权重
修改位置：SpawnProfile(B2).cs:9-38

打开 SpawnProfile(B2).cs 文件，找到这段代码：

public static class DefaultElementWeights
{
    // ===== 默认配置：四元素均匀分布（各25%） =====
    public const float Fire = 1.0f;    // 火属性权重（修改这里改变火元素生成概率）
    public const float Water = 1.0f;   // 水属性权重（修改这里改变水元素生成概率）
    public const float Wind = 1.0f;    // 风属性权重（修改这里改变风元素生成概率）
    public const float Ground = 1.0f;  // 土属性权重（修改这里改变土元素生成概率）
}

---

修改示例

示例1：火焰关卡（火元素占60%）

public const float Fire = 6.0f;    // 火60%
public const float Water = 2.0f;   // 水20%
public const float Wind = 1.0f;    // 风10%
public const float Ground = 1.0f;  // 土10%
```

---

