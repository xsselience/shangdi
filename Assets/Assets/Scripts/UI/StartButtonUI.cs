using UnityEngine;
using UnityEngine.Events;
using Game.Board;
using Assets.Scripts.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

public class StartButtonUI : MonoBehaviour
{
    public enum Stage { None, FirstBoard, SecondBoard }

    [Header("UI 面板")]
    [SerializeField] private RectTransform mainScreen;
    [SerializeField] private RectTransform subScreen;

    [Tooltip("规则面板。开始游戏之后按这个按钮会把它打开")]
    [SerializeField] private RulePanelUI rulePanel;

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

    [Header("全部通关后")]
    [Tooltip("第五章第二棋盘打完之后要播的对话")]
    [SerializeField] private IntroDialogue finalDialogue;

    [Tooltip("点击结束后要去的场景索引。0 = 开始场景")]
    [SerializeField] private int endingSceneIndex = 0;

    [Header("开始按钮提示")]
    [Tooltip("点了开始之后要隐藏并停掉的那个提示动画")]
    [SerializeField] private StartHintAnimator startHint;

    [Header("缩放过渡")]
    [Tooltip("棋盘放大/缩小的过渡时长（秒）。0 = 瞬间切换")]
    [SerializeField] private float boardScaleDuration = 0.35f;

    [Tooltip("过渡曲线")]
    [SerializeField]
    private AnimationCurve boardScaleEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("输入锁")]
    [Tooltip("勾上时，FocusFirstBoard / FocusSecondBoard 会被忽略。对话期间用")]
    public bool blockBoardFocus;
    Coroutine _scaleRoutine;


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

        // ★ 先让第二棋盘载入这一章的关卡（这一步同时会把它自己隐藏/显示好）
        if (battleFlow != null) battleFlow.ApplyLevel(currentChapter - 1);
        else Debug.LogWarning("StartButtonUI: battleFlow 未指定", this);

        RunChapterBegin(currentChapter);

        if (boardBridge != null) boardBridge.RunStage(currentChapter - 1);
        else Debug.LogWarning("StartButtonUI: boardBridge 未指定，第一棋盘不会启动", this);

        Debug.Log($"StartButtonUI: 第 {currentChapter} 章开始", this);
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

        Debug.Log($"StartButtonUI: 第 {currentChapter} 章失败，重来本关", this);

        if (battleFlow != null)
        {
            if (fullResetOnFailure) battleFlow.ResetEverything();
            else battleFlow.ResetThisLevel();   // ← 改这一行
        }

        BeginChapter(currentChapter);
    }

    /// <summary>成功面板的按钮接这个。</summary>
    public void OnClickRaceSucceeded()
    {
        if (stage != Stage.SecondBoard)
        {
            Debug.Log("StartButtonUI: 不是第二棋盘，忽略", this);
            return;
        }

        if (battleFlow != null) battleFlow.ContinueToNextStage();

        if (currentChapter >= maxChapter)
        {
            Debug.Log("StartButtonUI: " + maxChapter + " 章全部完成", this);
            RunAllChaptersCleared();
            return;
        }

        // ★ 这里插对话：刚打完的这一章，对应下标 currentChapter - 1
        int finishedStage = currentChapter - 1;

        DialogueSequence seq = finalDialogue != null
            ? finalDialogue.StageSequence(finishedStage)
            : null;

        if (seq != null)
        {
            Debug.Log($"StartButtonUI: 第 {currentChapter} 章结束，播放对话「{seq.name}」", this);

            int nextChapter = currentChapter + 1;

            UnityAction onDone = null;
            onDone = () =>
            {
                seq.onFinished.RemoveListener(onDone);
                BeginChapter(nextChapter);
            };

            seq.onFinished.AddListener(onDone);
            finalDialogue.Begin(seq);
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
                if (startHint != null) startHint.Hide();
                GameEventCenter.Instance.RequestStartGame();
                break;
            case GameState.Playing:
                // 游戏已经开始了，这个按钮改成"打开规则"
                GameEventCenter.Instance.RequestPauseGame();

                if (rulePanel != null) rulePanel.Show();
                else Debug.LogWarning("StartButtonUI: rulePanel 未指定，规则面板不会打开", this);
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
        // 目标缩放
        Vector3 targetMain = secondBoardActive ? Vector3.one * 0.9f : Vector3.one;
        Vector3 targetSub = secondBoardActive ? Vector3.one * 1.1f : Vector3.one;
        Vector3 targetFirst = Vector3.one *
            (secondBoardActive ? firstScaleOnSecondBoard : firstScaleOnFirstBoard);
        Vector3 targetSecond = Vector3.one *
            (secondBoardActive ? secondScaleOnSecondBoard : secondScaleOnFirstBoard);

        if (_scaleRoutine != null)
        {
            StopCoroutine(_scaleRoutine);
            _scaleRoutine = null;
        }

        if (boardScaleDuration <= 0f || !isActiveAndEnabled)
        {
            SetScales(targetMain, targetSub, targetFirst, targetSecond);
            if (battleFlow != null) battleFlow.SnapAllViews();
            return;
        }

        _scaleRoutine = StartCoroutine(ScaleRoutine(targetMain, targetSub, targetFirst, targetSecond));
    }

    private void SetScales(Vector3 main, Vector3 sub, Vector3 first, Vector3 second)
    {
        if (mainScreen != null) mainScreen.localScale = main;
        if (subScreen != null) subScreen.localScale = sub;
        if (firstBoard != null) firstBoard.localScale = first;
        if (secondBoard != null) secondBoard.localScale = second;
    }

    private System.Collections.IEnumerator ScaleRoutine(
        Vector3 main, Vector3 sub, Vector3 first, Vector3 second)
    {
        Vector3 fromMain = mainScreen != null ? mainScreen.localScale : Vector3.one;
        Vector3 fromSub = subScreen != null ? subScreen.localScale : Vector3.one;
        Vector3 fromFirst = firstBoard != null ? firstBoard.localScale : Vector3.one;
        Vector3 fromSecond = secondBoard != null ? secondBoard.localScale : Vector3.one;

        float duration = Mathf.Max(0.01f, boardScaleDuration);
        float t = 0f;

        while (t < 1f)
        {
            // 用 unscaledDeltaTime，暂停时也能正常播完
            t += Time.unscaledDeltaTime / duration;

            float k = (boardScaleEase != null && boardScaleEase.length > 0)
                ? boardScaleEase.Evaluate(Mathf.Clamp01(t))
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            SetScales(Vector3.LerpUnclamped(fromMain, main, k),
                      Vector3.LerpUnclamped(fromSub, sub, k),
                      Vector3.LerpUnclamped(fromFirst, first, k),
                      Vector3.LerpUnclamped(fromSecond, second, k));

            // 棋盘在变，棋子每一帧都要重新贴住格子
            if (battleFlow != null) battleFlow.SnapAllViews();

            yield return null;
        }

        SetScales(main, sub, first, second);
        if (battleFlow != null) battleFlow.SnapAllViews();

        _scaleRoutine = null;
    }
    // ─────────────────────────────────────────────
    //  手动切换聚焦（点棋盘触发）
    // ─────────────────────────────────────────────
    /// <summary>点上方棋盘 / 第一棋盘时调这个</summary>
    

    // ── 玩家点击用的入口：受 blockBoardFocus 限制 ──
    [ContextMenu("聚焦第一棋盘")]
    public void FocusFirstBoard()
    {
        Debug.Log($"StartButtonUI: FocusFirstBoard 被调用，blockBoardFocus={blockBoardFocus}", this);
        if (blockBoardFocus)
        {
            Debug.Log("StartButtonUI: 对话进行中，忽略聚焦第一棋盘", this);
            return;
        }

        ForceFocusFirstBoard();
    }

    [ContextMenu("聚焦第二棋盘")]
    public void FocusSecondBoard()
    {
        if (blockBoardFocus)
        {
            Debug.Log("StartButtonUI: 对话进行中，忽略聚焦第二棋盘", this);
            return;
        }

        ForceFocusSecondBoard();
    }

    // ── 脚本用的入口：无视输入锁 ──
    /// <summary>脚本专用，不受 blockBoardFocus 限制</summary>
    public void ForceFocusFirstBoard() => ApplyBoardScales(false);

    /// <summary>脚本专用，不受 blockBoardFocus 限制</summary>
    public void ForceFocusSecondBoard() => ApplyBoardScales(true);

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
        Debug.Log("StartButtonUI: 全部章节通关", this);

        if (finalDialogue == null)
        {
            Debug.LogWarning("StartButtonUI: finalDialogue 未指定，通关后不会出现对话", this);
            return;
        }

        finalDialogue.BeginEnding();       // ← 是 BeginEnding，不是 Begin
    }

    /// <summary>结束按钮接这个：回到开始场景</summary>
    public void OnClickEndingButton()
    {
        Debug.Log($"StartButtonUI: 返回场景 {endingSceneIndex}", this);

        // 保险：万一暂停那套改过 timeScale，回场景前先还原，
        // 否则新场景一进去就是静止的
        Time.timeScale = 1f;

        SceneManager.LoadScene(endingSceneIndex);
    }
}