# 代号：上帝

Unity 项目。本文档为交接文档，重点说明**事件订阅系统**（模块间通信的核心机制）和配置表流程。

## 项目结构

```
Assets/Assets/Scripts/
├── Core/
│   └── GameEventCenter.cs     # 事件中心（全局唯一，消息中转站）
├── Manager/
│   ├── GameManager.cs         # 游戏状态机：BeforePlaying / Playing / Paused
│   ├── LevelManager.cs        # 当前关卡：加载 LevelConfig 并广播
│   ├── RewindManager.cs       # 回溯（待实现）
│   └── SaveManager.cs         # 存档（待实现）
├── TableConfig/               # 配置类（对应 Excel 表结构）
│   └── Editor/
│       └── GameTableImporter.cs   # 导表工具（编辑器专用）
└── UI/                        # 所有界面脚本
```

## 一、事件订阅系统（核心机制）

### 总览

所有模块**互不直接引用**，通过 `GameEventCenter` 中转：

```
┌─────────┐   ①请求事件    ┌──────────────────┐   ③通知事件    ┌─────────┐
│   UI    │ ────────────▶ │  GameEventCenter │ ────────────▶ │   UI    │
│ (按钮点击)│   "我想做某事"  │    (只转发消息)    │  "某事已生效"   │ (更新显示)│
└─────────┘                └──────▲───────────┘                └─────────┘
                                  │ ②订阅请求，校验后决策
                               GameManager / LevelManager
```

以"点开始按钮"为例的完整链路：

1. `StartButtonUI.OnClickStartLevel()` → `GameEventCenter.Instance.RequestStartGame()`
2. 事件中心转发 `StartGameRequested`（不做任何业务判断）
3. `GameManager` 订阅了该事件 → 校验状态（只有 `BeforePlaying` 才放行）→ 改状态 → 广播通知
4. 所有订阅了 `GameStateChanged` 的 UI 收到通知，各自更新表现

### 事件的两个方向（最重要的规矩）

| 类型 | 命名 | 方向 | 含义 | 谁能订阅 |
|---|---|---|---|---|
| **请求事件** | `XxxRequested` | UI → Manager | "我想做某事"，**可能被拒** | 只有 Manager |
| **通知事件** | `XxxStarted` / `XxxChanged` | Manager → UI | "某事已生效"，**事实公告** | 任何 UI |

**为什么 UI 不能互相订阅请求事件**：请求只要发出就会广播，Manager 可能拒绝（状态不对直接 return），但订阅者已经响应了——出现过"游戏没暂停、存档面板却弹出来"的 bug。修复方式就是引入通知事件：`GameManager.PauseGame()` 校验**通过后**才 `NotifyGamePauseStarted()`。UI 只听盖章的公告。

### 现有事件清单（GameEventCenter.cs）

**请求（UI 发，Manager 收）：**
`StartGameRequested` `PauseGameRequested` `ResumeGameRequested` `SaveRequested` `RewindRequested` `VolumeChanged`

**通知（Manager 发，UI 收）：**
`GameStateChanged(GameState)` —— 游戏状态变化
`LevelChanged(LevelConfig)` —— 换关（LevelManager 广播）
`GamePauseStarted` —— 暂停已生效
`RewindStarted` —— 回溯已生效

### UI 脚本的标准写法（照抄这个模板）

```csharp
public class XxxUI : MonoBehaviour
{
    private void OnEnable()
    {
        GameEventCenter.Instance.某通知事件 += On某通知;
    }

    private void OnDisable()
    {
        // 判空必须有：退出 Play 时事件中心可能先被销毁
        if (GameEventCenter.Instance == null)
            return;

        GameEventCenter.Instance.某通知事件 -= On某通知;
    }

    // Button 的 OnClick 绑这类方法 → 只发请求，不写业务
    public void OnClickXxx()
    {
        GameEventCenter.Instance.RequestXxx();
    }

    // 收到通知 → 只改自己的显示，不做业务判断
    private void On某通知() { /* 更新文本/显隐/动画 */ }
}
```

### 加新功能的步骤

**加一个新按钮**：
1. 按钮脚本写 `OnClick` 方法发请求（已有请求直接用）
2. 若是新请求：`GameEventCenter` 加 `event Action XxxRequested` + `RequestXxx()` 方法
3. 对应 Manager 订阅、校验、执行，需要别人知道就加**通知事件**并在生效后广播
4. UI 订阅通知刷新显示

**加一个新面板**：
1. 脚本挂**常激活**的对象上（见下方坑 2），`_panelRoot` 拖默认隐藏的面板本体
2. 订阅对应的**通知事件**决定显隐（不要订阅请求事件）
3. 关闭按钮：`Hide()` + `RequestResumeGame()`（如果开面板时暂停了）

**加一张新配置表**：见下方"配置表"一节

### 已知限制 / 待办

- 面板关闭统一 `RequestResumeGame`，如果以后允许面板叠加，需要一个 PanelManager 记录"还有谁开着"
- `RewindManager` / `SaveManager` 还是空壳，回溯/存档流程待实现
- 棋盘格子类型外观（`BoardCellTableConfig` 已有数据，`ChessBoardUI.BuildBoard` 里有 TODO 钩子）
- 时间到 (`OnTimeUp`)、得分丢分逻辑待接

## 二、必须遵守的坑（都是踩过的）

1. **OnEnable/OnDisable 必须配对，且 OnDisable 开头判空** `GameEventCenter.Instance == null`——退出 Play 时销毁顺序不定，不判空必报 NRE。
2. **控制别人的脚本必须挂在常激活的对象上**。挂在默认 `SetActive(false)` 的面板上的脚本，`OnEnable` 永远不会执行 → 订阅不上 → 收不到任何事件。面板本体只放展示内容。
3. **UI 事件处理器只管"长什么样"，不管"该不该"**。状态校验全部留在 Manager（`GameManager` 各方法的入口 `if` 就是拦截层）。按钮"点了没反应"是正常防御，不是 bug。
4. **倒计时/动画用 `Time.deltaTime`**，不要用 `unscaledDeltaTime`——暂停（`timeScale=0`）时前者自动停，后者是给暂停菜单自己的动画用的。
5. **改 Excel 后导表前先保存并关闭文件**（否则 `IOException: Sharing violation`）。WPS 有后台驻留，窗口关了进程还锁文件。

## 三、游戏状态机（GameManager）

```
BeforePlaying ──点击开始──▶ Playing ───暂停/回溯────▶ Paused
     （timeScale=0）          ▲ └─────点关闭面板──────┘
                              └────────恢复──────────
```

- 状态是"游戏循环阶段"，**不是**"哪个面板开着"。面板显隐听通知事件，不要为面板加状态。
- 每个状态变化只从 `SetGameState` 一个出口广播 `GameStateChanged`。
- 开局前只有 StartButton / CloseButton 有效——其他按钮的请求会被各 Manager 入口校验拦下，无需额外代码。

## 四、配置表流程

**表**：`Assets/Assets/Tables/棋盘系统表.xlsx`，六张 sheet（棋盘/格子类型/棋盘格子/关卡/刷新区域/元素权重）。
每张 sheet 格式：第 1 行中文说明、第 2 行英文字段名、第 3 行类型、第 4 行起数据。**主键列不能留空**（空行会被跳过）。

**导表**：Unity 菜单 **ShangDi → 导入全部配置表** → 生成到 `Assets/Assets/Resources/Configs/`：

```
Boards/     BoardConfig_{board_id}
CellTypes/  CellTypeConfig_{cell_type_id}
Cells/      BoardCellTable          （全部格子，含 100 行棋盘数据）
Levels/     LevelConfig_{level_id}
Zones/      RefreshZoneConfig_{zone_id}   （元素权重已按 zone_id 合并进 Weights）
```

要点：
- 导表工具是 `TableConfig/Editor/GameTableImporter.cs`，纯 .NET 解析 xlsx（zip+XML），无第三方依赖，不进打包
- **反复导表原地更新资产，不会重复**；删除表里的行不会删已有资产，要手动删
- `LevelConfig.timeLimit`（倒计时秒数）是**表外手动字段**，Excel 里没有这列，导表不覆盖，在 Inspector 里按关卡填
- 改表结构（加列）需要同步改：配置类字段 + 导入器的 `SetData` 调用

**运行时读配置**：一律通过 `LevelManager`，不要在 UI 上拖配置引用：

```csharp
// 当前关卡配置（LevelManager 启动时自动加载第一关并广播）
LevelManager.Instance.CurrentLevel

// 换关（一行代码，所有订阅 LevelChanged 的 UI 自动刷新）
LevelManager.Instance.LoadLevel(3);

// 其他表按 id 加载
Resources.Load<BoardConfig>($"Configs/Boards/BoardConfig_{level.BoardId}");
```

## 五、场景搭建速查（LevelScene）

必须存在的对象：

- `GameEventCenter`（事件中心）
- `GameManager` / `LevelManager`（可设 Start Level Id）
- 各 UI 脚本挂**常激活**对象，`_panelRoot` 拖默认隐藏的面板
- 棋盘容器挂 `GridLayoutGroup`（Cell Size/Spacing 手调，列数代码会按配置设）
- 按钮 OnClick 绑定各脚本的 `OnClickXxx` 公有方法
