# 棋盘系统（B部分）使用指南

## 📁 文件结构

```
board/
├── 核心数据
│   ├── BoardData.cs           # 基础数据类型（坐标、元素、棋子、格子）
│   └── BoardState.cs          # 棋盘状态管理
│
├── 初始化
│   └── BoardInitialLayout.cs  # 初始布局生成（障碍物、路线验证）
│
├── 阶段管理
│   ├── BoardStageSession.cs   # 阶段流程控制
│   └── BoardStageConfig.cs    # 阶段配置资源
│
├── 游戏逻辑
│   ├── SynthesisResolver.cs   # 五连合成判断
│   └── SpawnProfile.cs        # 刷新系统
│
├── 辅助系统
│   ├── BoardRandom.cs         # 随机数系统
│   ├── BoardWorldMapper.cs    # 坐标转换
│   ├── BoardController.cs     # 主控制器
│   ├── BoardInputController.cs # 输入处理
│   └── BoardView.cs           # 视图同步
│
├── 扩展功能（新增）
│   ├── BoardGameManager.cs    # 游戏管理器示例
│   ├── BoardDebugger.cs       # 调试工具
│   ├── BoardSelectionHighlight.cs # 选中高亮
│   └── PieceVisualizer.cs     # 棋子颜色管理
│
└── Editor/
    └── BoardControllerEditor.cs # 编辑器扩展
```

## 🚀 快速开始

### 步骤1：创建棋盘对象

1. 在 Hierarchy 中创建空对象：`BoardManager`
2. 添加以下组件：
   - `BoardController`
   - `BoardWorldMapper`
   - `BoardInputController`
   - `BoardView`
   - `BoardGameManager` (可选，用于显示信息)
   - `BoardSelectionHighlight` (可选，用于高亮)
   - `BoardDebugger` (可选，用于调试)

### 步骤2：创建配置资源

**创建阶段配置：**
```
右键 → Create → Game/Board/Stage Config
命名：BoardStageConfig_Default
```

**创建刷新配置：**
```
右键 → Create → Game/Board/Spawn Profile
命名：SpawnProfile_Default
```

### 步骤3：配置参数

**BoardStageConfig_Default：**
- Width: 10
- Height: 10
- Initial Piece Count: 10
- Step Limit: 20
- Seed: 12345
- Layout:
  - Generate Obstacles: ✓
  - Obstacle Count: 15
  - Animal Start: (0, 0)
  - Animal End: (9, 9)
- Spawn Profile: 拖入 SpawnProfile_Default

**SpawnProfile_Default：**
- Areas: X=0, Y=0, Width=10, Height=10
- Allowed Elements: Fire, Water, Wind, Ground 全选
- Base Weights: 全部设为 1

**BoardManager 组件配置：**
- BoardController → Config: 拖入 BoardStageConfig_Default
- BoardWorldMapper → Local Step X: (1, 0, 0)
- BoardWorldMapper → Local Step Y: (0, 0, 1)
- BoardInputController → Input Camera: 拖入 Main Camera

### 步骤4：创建简单预制体

**临时测试预制体（快速开始）：**

1. **普通棋子：**
   - 创建 Cube，缩放 (0.8, 0.3, 0.8)
   - 保存为 Prefab: `NormalPiece`

2. **特种棋子：**
   - 创建 Sphere，缩放 (0.9, 0.9, 0.9)
   - 保存为 Prefab: `SpecialPiece`

3. **障碍物：**
   - 创建 Cube，缩放 (1, 0.5, 1)
   - 保存为 Prefab: `Obstacle`

4. 在 BoardView 中拖入这些预制体

## 🎮 运行测试

### 方法1：Play模式测试

1. 点击 Unity Play 按钮
2. 棋盘会自动初始化
3. 点击棋子选中，点击相邻空格移动
4. 查看 Console 的日志输出

### 方法2：键盘快捷键（需要 BoardGameManager）

- **R键**：重新初始化棋盘
- **空格**：手动结束阶段
- **T键**：执行随机移动（测试用）

### 方法3：Inspector调试

1. 选中 BoardManager
2. 右键点击 BoardController 组件
3. 选择：
   - `调试/初始化棋盘`
   - `调试/执行指定移动`
   - `调试/手动结束棋盘阶段`

## 📊 功能清单

### ✅ 已实现的16项B部分需求

| 序号 | 功能 | 实现文件 | 状态 |
|-----|------|---------|------|
| 1 | 棋盘数据 | BoardState.cs | ✅ |
| 2 | 格子状态 | BoardData.cs | ✅ |
| 3 | 障碍格 | BoardInitialLayout.cs | ✅ |
| 4 | 棋子数据 | BoardData.cs | ✅ |
| 5 | 初始生成 | BoardInitialLayout.cs | ✅ |
| 6 | 棋子刷新 | SpawnProfile.cs | ✅ |
| 7 | 棋子移动 | BoardStageSession.cs | ✅ |
| 8 | 上下左右移动 | BoardState.cs | ✅ |
| 9 | 五连判断 | SynthesisResolver.cs | ✅ |
| 10 | 合成处理 | BoardStageSession.cs | ✅ |
| 11 | 特种棋子 | SynthesisResolver.cs | ✅ |
| 12 | 步数管理 | BoardStageSession.cs | ✅ |
| 13 | 阶段结束 | BoardStageSession.cs | ✅ |
| 14 | 阶段结算 | BoardStageSession.cs | ✅ |
| 15 | 坐标转换 | BoardWorldMapper.cs | ✅ |
| 16 | 随机种子 | BoardRandom.cs | ✅ |

### 🎨 新增扩展功能

- **BoardGameManager**：游戏管理器，事件监听和UI显示
- **BoardSelectionHighlight**：选中棋子的高亮显示
- **BoardDebugger**：调试信息显示工具
- **PieceVisualizer**：根据属性自动设置棋子颜色
- **BoardControllerEditor**：编辑器扩展工具

## 🔧 代码使用示例

### 监听游戏事件

```csharp
using Game.Board;

public class MyGameLogic : MonoBehaviour
{
    [SerializeField] private BoardController boardController;

    private void Start()
    {
        boardController.TurnCompleted += OnMove;
        boardController.StageEnded += OnEnd;
        boardController.StageSettled += OnSettled;
    }

    private void OnMove(BoardTurnResult result)
    {
        if (result.Synthesized)
        {
            Debug.Log("合成特种棋子！");
        }
    }

    private void OnEnd(BoardEndReason reason)
    {
        Debug.Log($"阶段结束：{reason}");
    }

    private void OnSettled(BoardSettlementResult result)
    {
        // 获取特种棋子传递给环境系统
        foreach (var entry in result.EnvironmentEntries)
        {
            Debug.Log($"特种棋子：{entry.Element} @ {entry.Coord}");
        }
    }
}
```

### 程序化控制

```csharp
// 查询棋盘状态
BoardState board = boardController.Board;
GridCoord coord = new GridCoord(5, 5);
CellState cell = board.GetCell(coord);

if (cell.HasPiece)
{
    PieceState piece = cell.Piece;
    Debug.Log($"元素：{piece.Element}");
    Debug.Log($"类型：{piece.Kind}");
}

// 手动移动
GridCoord from = new GridCoord(0, 0);
GridCoord to = new GridCoord(0, 1);
BoardTurnResult result;
if (boardController.TryMove(from, to, out result))
{
    Debug.Log("移动成功");
}
```

## 🐛 调试技巧

### 1. 使用 BoardDebugger

- 添加 BoardDebugger 组件到 BoardManager
- 运行时会在左上角显示棋盘状态
- Scene视图会显示坐标和棋子信息

### 2. 查看日志

- BoardController.logResults = true （默认开启）
- 每次移动、合成、刷新都会输出详细日志

### 3. Scene视图辅助线

- BoardWorldMapper 的 Draw Gizmos = true
- 选中 BoardManager 后在 Scene 视图看到格子位置

### 4. Inspector实时监控

- 运行时选中 BoardManager
- 可以看到 Session 的实时状态
- 可以执行调试命令

## ❓ 常见问题

### Q: 棋盘初始化失败？
A: 检查：
1. BoardStageConfig 是否正确配置
2. SpawnProfile 是否关联
3. 障碍数量是否太多导致无法生成直通路线

### Q: 点击没有反应？
A: 检查：
1. BoardInputController 的 Input Camera 是否设置
2. 是否有 Collider 在棋盘上方遮挡
3. BoardWorldMapper 的坐标配置是否正确

### Q: 棋子显示不出来？
A: 检查：
1. BoardView 的预制体是否正确拖入
2. 预制体是否在正确的位置（Y坐标）
3. 查看 Console 是否有错误

### Q: 如何修改棋盘大小？
A: 修改 BoardStageConfig_Default 的 Width 和 Height

### Q: 如何调整刷新规则？
A: 修改 SpawnProfile_Default 的：
- Areas：刷新区域
- Allowed Elements：允许的属性
- Base Weights：属性权重

## 📝 下一步开发建议

### 美术相关
1. 替换临时预制体为正式美术资源
2. 添加棋子移动动画
3. 添加合成特效

### 玩法扩展
1. 实现环境系统（水火风土效果）
2. 实现动物移动系统
3. 添加关卡系统

### UI优化
1. 添加步数显示UI
2. 添加结算面板
3. 添加操作提示

## 📞 技术支持

如有问题，可以：
1. 查看各脚本的代码注释
2. 使用 BoardDebugger 查看运行状态
3. 在 Console 查看详细日志输出

---

**版本**: B部分完整版
**日期**: 2026-10-07
**代码行数**: 2348行（核心）+ 额外扩展
**脚本数量**: 16个文件
