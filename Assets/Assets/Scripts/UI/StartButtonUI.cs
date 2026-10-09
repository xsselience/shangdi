using UnityEngine;
using Game.Board;

public class StartButtonUI : MonoBehaviour
{
    public enum Stage { None, FirstBoard, SecondBoard }

    [Header("UI 面板")]
    [SerializeField] private RectTransform mainScreen;
    [SerializeField] private RectTransform subScreen;

    [Header("第一棋盘")]
    [SerializeField] private Transform firstBoard;
    [SerializeField] private float firstScaleOnFirstBoard = 1f;
    [SerializeField] private float firstScaleOnSecondBoard = 0.9f;

    [Header("第二棋盘")]
    [SerializeField] private Transform secondBoard;
    [SerializeField] private float secondScaleOnFirstBoard = 0.7f;
    [SerializeField] private float secondScaleOnSecondBoard = 1f;

    [Header("流程")]
    [SerializeField] private BoardSceneBridge boardBridge;
    [SerializeField] private BattleFlow battleFlow;

    [Header("章节")]
    [Tooltip("一共几章。每一章 = 第一棋盘一个阶段 + 第二棋盘一轮")]
    [SerializeField] private int maxChapter = 5;

    [Tooltip("失败重来时是否连地图一起完全重置（默认否：保留已经建好的营地）")]
    [SerializeField] private bool fullResetOnFailure = false;

    [Header("运行时状态")]
    [SerializeField] private Stage stage = Stage.None;

    [Tooltip("1 起。0 表示还没开始")]
    [SerializeField] private int currentChapter;

    public Stage CurrentStage { get { return stage; } }
    public int CurrentChapter { get { return currentChapter; } }

    // ─────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────
    private void OnEnable()
    {
        GameEventCenter.Instance.GameStateChanged += OnGameStateChanged;
        if (boardBridge != null) boardBridge.StageSettled += OnFirstBoardSettled;
    }

    private void OnDisable()
    {
        if (GameEventCenter.Instance != null)
            GameEventCenter.Instance.GameStateChanged -= OnGameStateChanged;
        if (boardBridge != null) boardBridge.StageSettled -= OnFirstBoardSettled;
    }

    private void Start()
    {
        var gm = GameManager.Instance;
        bool playing = gm != null && gm.CurrentState == GameState.Playing;

        if (playing) BeginChapter(1);
        else ApplyBoardScales(false);       // 还没开始，只摆视觉，不动流程
    }

    private void OnGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.BeforePlaying:
                ReturnToStart();
                break;

            case GameState.Playing:
                // 只在还没开始的时候启动，避免运行中被打断重来
                if (stage == Stage.None) BeginChapter(1);
                break;
        }
    }

    // ─────────────────────────────────────────────
    //  章节循环
    // ─────────────────────────────────────────────
    /// <summary>开始（或重来）第 chapter 章。第一棋盘会被重置到这个阶段的初始状态。</summary>
    public void BeginChapter(int chapter)
    {
        currentChapter = Mathf.Clamp(chapter, 1, maxChapter);
        stage = Stage.FirstBoard;

        ApplyBoardScales(false);

        RunChapterBegin(currentChapter);

        if (boardBridge != null) boardBridge.RunStage(currentChapter - 1);
        else Debug.LogWarning("StartButtonUI: boardBridge 未指定，第一棋盘不会启动", this);

        Debug.Log("StartButtonUI: 第 " + currentChapter + " 章开始（第一棋盘阶段 " + currentChapter + "）", this);
    }

    // 第一棋盘当前阶段跑完
    private void OnFirstBoardSettled(BoardSettlementResult settlement)
    {
        if (stage != Stage.FirstBoard) return;

        Debug.Log("StartButtonUI: 第 " + currentChapter + " 章 —— 步数耗尽，用 " + settlement.UsedSteps + " 步", this);
        EnterSecondBoard();
    }

    public void EnterSecondBoard()
    {
        stage = Stage.SecondBoard;

        ApplyBoardScales(true);

        Debug.Log("StartButtonUI: 第 " + currentChapter + " 章 —— 进入第二棋盘，A / B 开始行动", this);

        if (battleFlow == null)
        {
            Debug.LogError("StartButtonUI: battleFlow 未指定，A / B 不会开始移动", this);
            return;
        }

        battleFlow.StartBattle();
    }

    // ─────────────────────────────────────────────
    //  第二棋盘结算
    // ─────────────────────────────────────────────
    /// <summary>失败面板的按钮接这个。</summary>
    public void OnClickRaceFailed()
    {
        if (stage != Stage.SecondBoard)
        {
            Debug.Log("StartButtonUI: 当前不在第二棋盘，忽略失败结算", this);
            return;
        }

        Debug.Log("StartButtonUI: 第 " + currentChapter + " 章失败，重来本阶段", this);

        if (battleFlow != null)
        {
            if (fullResetOnFailure) battleFlow.ResetEverything();
            else battleFlow.ResetToPreBattle();   // 地图和营地保留，A / B 归位
        }

        BeginChapter(currentChapter);       // 重来同一章
    }

    /// <summary>成功面板的按钮接这个。</summary>
    public void OnClickRaceSucceeded()
    {
        if (stage != Stage.SecondBoard)
        {
            Debug.Log("StartButtonUI: 当前不在第二棋盘，忽略成功结算", this);
            return;
        }

        // B 原地建营地 + A / B 归位，地图与已有营地保留
        if (battleFlow != null) battleFlow.ContinueToNextStage();

        if (currentChapter >= maxChapter)
        {
            Debug.Log("StartButtonUI: " + maxChapter + " 章全部完成", this);
            RunAllChaptersCleared();
            return;
        }

        BeginChapter(currentChapter + 1);
    }

    // ─────────────────────────────────────────────
    //  开始按钮
    // ─────────────────────────────────────────────
    public void OnClickStartLevel()
    {
        GameState state = GameManager.Instance.CurrentState;
        switch (state)
        {
            case GameState.BeforePlaying:
                GameEventCenter.Instance.RequestStartGame();
                break;
            case GameState.Playing:
                GameEventCenter.Instance.RequestPauseGame();
                break;
        }
    }

    // ─────────────────────────────────────────────
    //  回到开始状态
    // ─────────────────────────────────────────────
    public void ReturnToStart()
    {
        stage = Stage.None;
        currentChapter = 0;

        ApplyBoardScales(false);

        if (battleFlow != null) battleFlow.ResetEverything();
        if (boardBridge != null) boardBridge.ResetBoard();

        Debug.Log("StartButtonUI: 已回到开始状态（第一棋盘大 / 第二棋盘小）", this);
    }

    // ─────────────────────────────────────────────
    //  视觉
    // ─────────────────────────────────────────────
    private void ApplyBoardScales(bool secondBoardActive)
    {
        mainScreen.localScale = secondBoardActive ? Vector3.one * 0.9f : Vector3.one;
        subScreen.localScale = secondBoardActive ? Vector3.one * 1.1f : Vector3.one;

        if (firstBoard != null)
            firstBoard.localScale = Vector3.one *
                (secondBoardActive ? firstScaleOnSecondBoard : firstScaleOnFirstBoard);

        if (secondBoard != null)
            secondBoard.localScale = Vector3.one *
                (secondBoardActive ? secondScaleOnSecondBoard : secondScaleOnFirstBoard);
    }

    // ═══════════════════════════════════════════════════════
    //  ▼▼▼  留空：每一章开始时要做的事  ▼▼▼
    //
    //  chapter 从 1 开始。每章各自的演出 / 提示写在这里。
    //  注意这时第一棋盘还没重建，需要它先就绪的话
    //  把你的逻辑挂到 boardBridge.On Stage Begin 上。
    // ═══════════════════════════════════════════════════════
    private void RunChapterBegin(int chapter)
    {
    }
    // ═══════════════════════════════════════════════════════
    //  ▲▲▲  留空结束  ▲▲▲
    // ═══════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════
    //  ▼▼▼  留空：全部章节完成时要做的事  ▼▼▼
    // ═══════════════════════════════════════════════════════
    private void RunAllChaptersCleared()
    {
    }
    // ═══════════════════════════════════════════════════════
    //  ▲▲▲  留空结束  ▲▲▲
    // ═══════════════════════════════════════════════════════
}