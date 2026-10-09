using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>命令行：-executeMethod BoardIntegrationVerification.Run；也可从菜单运行。测试不保存运行时场景。</summary>
[InitializeOnLoad]
public static class BoardIntegrationVerification
{
    private const string Key = "BoardIntegrationVerification.Active";
    private static int stage;
    private static int frames;
    private static double deadline;
    private static readonly List<string> checks = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static BoardSceneBridge bridge;
    private static GridCoord from;
    private static GridCoord to;
    private static int steps;
    private static int pieceId;
    private static bool finishing;

    static BoardIntegrationVerification()
    {
        if (SessionState.GetBool(Key, false)) Subscribe();
    }

    [MenuItem("Tools/Board/Verify Existing UI Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play Mode。");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/StartScreen.unity");
        Subscribe();
        EditorApplication.EnterPlaymode();
    }

    private static void Subscribe()
    {
        deadline = EditorApplication.timeSinceStartup + 150;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            errors.Add(message + "\n" + trace);
    }

    private static void Tick()
    {
        if (finishing) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "测试超时，stage=" + stage); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        if (++frames < 25) return;
        frames = 0;
        try
        {
            switch (stage)
            {
                case 0:
                    Require(SceneManager.GetActiveScene().name == "StartScreen", "测试从原有 StartScreen 开始");
                    AssertNoMissingScripts();
                    var start = FindButton("StartButton");
                    Require(start.onClick.GetPersistentEventCount() > 0 && start.onClick.GetPersistentTarget(0) != null,
                            "开始页面按钮的原有脚本引用可用");
                    start.onClick.Invoke();
                    break;
                case 1:
                    Require(SceneManager.GetActiveScene().name == "LevelScene", "原有开始按钮进入 LevelScene");
                    AssertNoMissingScripts();
                    bridge = UnityEngine.Object.FindObjectOfType<BoardSceneBridge>();
                    Require(bridge != null && bridge.Session != null, "现有 ChessBoard 上的桥接组件完成初始化");
                    Require(bridge.Board.Width == 10 && bridge.Board.Height == 10, "加载现有 10×10 棋盘配置");
                    Require(CountPieces() == 8 && bridge.Session.RemainingSteps == 25, "初始棋子 8、步数 25 来自现有关卡数据");
                    Require(CellButtons().Count() == 100, "现有 Grid 内有 100 个可点击测试格子");
                    Require(!bridge.CanInteract && GameManager.Instance.CurrentState == GameState.BeforePlaying, "进入关卡但尚未开始时禁止操作");
                    FindMove(out from, out to);
                    steps = bridge.Session.RemainingSteps;
                    Require(!bridge.ClickCell(from) && bridge.Session.RemainingSteps == steps, "未开始点击不会选中或扣步");
                    VerifyLayoutAndRaycast();
                    var rules = BoardLevelFactory.CreateSpawnRules(1, 10, 10);
                    Require(rules.GetWeight(new GridCoord(0, 5), ElementType.Fire) == 0 &&
                            rules.GetWeight(new GridCoord(0, 5), ElementType.Water) == 5 &&
                            rules.GetWeight(new GridCoord(5, 5), ElementType.Water) == 3,
                            "刷新区域正确按优先级覆盖，而不是相乘");
                    FindButton("StartButton").onClick.Invoke();
                    break;
                case 2:
                    Require(bridge.CanInteract && GameManager.Instance.CurrentState == GameState.Playing, "原有下屏播放按钮启用棋盘");
                    FindMove(out from, out to);
                    pieceId = bridge.Board.GetCell(from).Piece.Id;
                    steps = bridge.Session.RemainingSteps;
                    PressCell(from);
                    Require(bridge.HasSelection && bridge.SelectedCoord == from, "真实格子 Button 点击选中棋子");
                    var invalid = new GridCoord((from.X + 3) % 10, (from.Y + 3) % 10);
                    Require(!bridge.ClickCell(invalid) && bridge.Session.RemainingSteps == steps, "非法移动不扣步数");
                    PressCell(from); // 如果非法目标上有棋子，可能切换选中；重新确定选择。
                    if (!bridge.HasSelection || bridge.SelectedCoord != from) PressCell(from);
                    PressCell(to);
                    Require(bridge.Board.GetCell(to).HasPiece && bridge.Board.GetCell(to).Piece.Id == pieceId &&
                            bridge.Session.RemainingSteps == steps - 1 && CountPieces() == 9,
                            "UI 点击完成合法移动，扣一步，未合成时刷新一颗棋子");
                    Require(FindText("StepsCountText").text.Contains((steps - 1).ToString()), "原有剩余步数文本同步实际回合");
                    FindButton("RuleButton").onClick.Invoke();
                    break;
                case 3:
                    Require(GameManager.Instance.CurrentState == GameState.Paused && !bridge.CanInteract, "原有规则按钮暂停游戏并禁止棋盘输入");
                    Require(FindObject("RulePanel").activeInHierarchy, "现有规则面板弹出");
                    steps = bridge.Session.RemainingSteps;
                    Require(!bridge.ClickCell(to) && bridge.Session.RemainingSteps == steps, "暂停期间操作被拒绝");
                    UnityEngine.Object.FindObjectOfType<Assets.Scripts.UI.RulePanelUI>().OnClickClose();
                    Require(bridge.CanInteract && GameManager.Instance.CurrentState == GameState.Playing, "关闭原有规则面板恢复操作");
                    FindButton("BackTrackButtton").onClick.Invoke();
                    break;
                case 4:
                    Require(!bridge.CanInteract && FindObject("SavePanel").activeInHierarchy, "原有回溯面板打开时禁止棋盘操作");
                    UnityEngine.Object.FindObjectOfType<Assets.Scripts.UI.SavePanelUI>().OnClickClose();
                    Require(bridge.CanInteract, "关闭原有回溯面板恢复操作");
                    bridge.ResetBoard();
                    Require(CountPieces() == 8 && bridge.Session.RemainingSteps == 25, "调试重置从关卡配置重新生成，无重复初始化");
                    // 以下只为验证已有合成规则布置测试局面，不是正式关卡初始化代码。
                    for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++)
                        bridge.Board.RemovePieceAt(new GridCoord(x, y));
                    for (int x = 1; x <= 4; x++)
                        bridge.Board.CreatePieceAt(new GridCoord(x, 1), ElementType.Fire, PieceKind.Normal);
                    bridge.Board.CreatePieceAt(new GridCoord(0, 0), ElementType.Fire, PieceKind.Normal);
                    Require(bridge.ClickCell(new GridCoord(0, 0)) && bridge.ClickCell(new GridCoord(0, 1)), "五连测试移动经过同一 UI 入口");
                    Require(CountPieces() == 1 && bridge.Session.RemainingSteps == 24 &&
                            bridge.Board.GetCell(new GridCoord(0, 1)).Piece.Kind == PieceKind.Special,
                            "同元素五连合成一颗特种棋子，不额外刷新棋子");
                    Require(bridge.ClickCell(new GridCoord(0, 1)) && bridge.ClickCell(new GridCoord(0, 2)),
                            "特种棋子的可移动性遵循 BoardState，不被 UI 擅自限制");
                    bridge.EndStageManually();
                    Require(bridge.Session.Phase == BoardStagePhase.Settled && CountPieces() == 1 &&
                            bridge.Session.Settlement.EnvironmentEntries.Count == 1 && !bridge.CanInteract,
                            "手动结束结算移除普通棋子、保留特种棋子并锁定输入");
                    Require(VisiblePieces() == 1, "结算后表现刷新，不残留普通棋子的图像");
                    Require(FindText("StepsCountText").text.Contains("已结束"), "结算状态显示在原有步数文本");
                    LevelManager.Instance.LoadLevel(2);
                    break;
                case 5:
                    Require(bridge.Session.RemainingSteps == 25 && CountPieces() == 8 && CellButtons().Count() == 100,
                            "LevelChanged 重建棋盘，数量与引用正确");
                    Require(FindText("LevelText").text.Contains("2"), "现有关卡文本跟随 LevelChanged");
                    VerifyLayoutAndRaycast();
                    // 尺寸变化适配：在运行时改变棋盘容器大小，再恢复（不保存场景）。
                    var grid = (RectTransform)bridge.transform.Find("Grid");
                    SessionState.SetFloat("BoardVerify.SizeX", grid.sizeDelta.x);
                    SessionState.SetFloat("BoardVerify.SizeY", grid.sizeDelta.y);
                    grid.sizeDelta = new Vector2(grid.sizeDelta.x - 40, grid.sizeDelta.y - 30);
                    break;
                case 6:
                    VerifyLayoutAndRaycast();
                    var rect = (RectTransform)bridge.transform.Find("Grid");
                    rect.sizeDelta = new Vector2(SessionState.GetFloat("BoardVerify.SizeX", 0), SessionState.GetFloat("BoardVerify.SizeY", 0));
                    LevelManager.Instance.LoadLevel(1);
                    break;
                case 7:
                    VerifyLayoutAndRaycast();
                    int count = 0;
                    while (bridge.Session.Phase == BoardStagePhase.Operating && count < 100)
                    {
                        FindMove(out from, out to);
                        steps = bridge.Session.RemainingSteps;
                        RequireSilent(bridge.ClickCell(from) && bridge.ClickCell(to), "自动回合失败");
                        RequireSilent(bridge.Session.RemainingSteps == steps - 1, "自动回合步数错误");
                        count++;
                    }
                    Require(count == 25 && bridge.Session.Phase == BoardStagePhase.Settled && bridge.Session.RemainingSteps == 0,
                            "连续 25 回合耗尽步数自动结束并结算");
                    Require(bridge.Board.GetCell(to).Piece == null || bridge.Board.GetCell(to).Piece.Kind == PieceKind.Special,
                            "自动结算完成后棋盘普通棋子被移除");
                    Require(VisiblePieces() == CountPieces() && !bridge.CanInteract, "自动结算同步显示并禁止继续操作");
                    bridge.ResetBoard();
                    ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "../Logs/board-integration-preview.png"));
                    break;
                case 8:
                    Require(Time.frameCount > 150, "Play Mode 持续推进，不是冻结的第一帧");
                    Require(errors.Count == 0, "整个真实场景流程没有运行时 Error/Exception");
                    Finish(true, "全部集成验证通过");
                    return;
            }
            stage++;
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }

    private static void VerifyLayoutAndRaycast()
    {
        Canvas.ForceUpdateCanvases();
        var grid = (RectTransform)bridge.transform.Find("Grid");
        var lower = (RectTransform)grid.Find("Cell_0_0");
        var upper = (RectTransform)grid.Find("Cell_0_9");
        RequireSilent(lower.position.y < upper.position.y, "显示坐标 Y 方向颠倒");
        foreach (var button in CellButtons())
        {
            var rect = (RectTransform)button.transform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = grid.InverseTransformPoint(corner);
                RequireSilent(local.x >= grid.rect.xMin - 0.1f && local.x <= grid.rect.xMax + 0.1f &&
                              local.y >= grid.rect.yMin - 0.1f && local.y <= grid.rect.yMax + 0.1f,
                              "格子溢出现有棋盘容器");
            }
        }
        FindMove(out from, out to);
        var target = grid.Find("Cell_" + from.X + "_" + from.Y);
        var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, target.position) };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        Require(results.Count > 0 && results[0].gameObject == target.gameObject,
                "现有 Canvas/EventSystem 射线能命中棋盘格子，装饰不遮挡点击");
        Require(lower.rect.width > 1 && Mathf.Abs(lower.rect.width - lower.rect.height) < 0.1f,
                "格子适配现有容器，保持非零正方形尺寸和正确坐标方向");
    }

    private static void FindMove(out GridCoord source, out GridCoord target)
    {
        for (int y = 0; y < bridge.Board.Height; y++) for (int x = 0; x < bridge.Board.Width; x++)
        {
            source = new GridCoord(x, y);
            var moves = bridge.Board.GetAvailableMoves(source);
            if (moves.Count > 0) { target = moves[0]; return; }
        }
        throw new InvalidOperationException("找不到合法测试移动。");
    }
    private static int CountPieces()
    {
        int result = 0;
        for (int y = 0; y < bridge.Board.Height; y++) for (int x = 0; x < bridge.Board.Width; x++)
            if (bridge.Board.GetCell(new GridCoord(x, y)).HasPiece) result++;
        return result;
    }
    private static int VisiblePieces()
    {
        return bridge.transform.Find("Grid").GetComponentsInChildren<UnityEngine.UI.Image>()
            .Count(image => image.name == "Piece" && image.gameObject.activeInHierarchy);
    }
    private static IEnumerable<UnityEngine.UI.Button> CellButtons()
    {
        return bridge.transform.Find("Grid").GetComponentsInChildren<UnityEngine.UI.Button>();
    }
    private static void PressCell(GridCoord coord)
    {
        var button = bridge.transform.Find("Grid/Cell_" + coord.X + "_" + coord.Y).GetComponent<UnityEngine.UI.Button>();
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    }
    private static GameObject FindObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>().First(go => go.name == name && go.scene.IsValid() && go.scene.isLoaded);
    }
    private static UnityEngine.UI.Button FindButton(string name)
    {
        return FindObject(name).GetComponent<UnityEngine.UI.Button>();
    }
    private static TMPro.TMP_Text FindText(string name) { return FindObject(name).GetComponent<TMPro.TMP_Text>(); }
    private static void AssertNoMissingScripts()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            RequireSilent(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                          "场景存在缺失脚本：" + child.name);
        Require(true, SceneManager.GetActiveScene().name + " 无 Missing Script");
    }
    private static void RequireSilent(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Require(bool condition, string message)
    {
        RequireSilent(condition, message);
        checks.Add("PASS: " + message);
        Debug.Log("BOARD_VERIFY PASS: " + message);
    }
    private static void Finish(bool success, string message)
    {
        finishing = true;
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= Tick;
        string report = "Result: " + (success ? "PASS" : "FAIL") + "\n" + message + "\n" +
                        string.Join("\n", checks) + "\n\nRuntime errors:\n" + string.Join("\n", errors);
        File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/board-integration-verification.txt"), report);
        Debug.Log("BOARD_VERIFY " + (success ? "PASS" : "FAIL") + ": " + message);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
