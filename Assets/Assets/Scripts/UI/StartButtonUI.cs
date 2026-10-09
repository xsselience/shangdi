using UnityEngine;

public class StartButtonUI : MonoBehaviour
{
    public enum Stage { None, One, Two }

    [Header("UI 面板（原有）")]
    [SerializeField] private RectTransform mainScreen;
    [SerializeField] private RectTransform subScreen;

    [Header("第一棋盘")]
    [Tooltip("第一棋盘的根物体。留空则不处理")]
    [SerializeField] private Transform firstBoard;

    [Tooltip("开始状态 / 第一阶段 的缩放")]
    [SerializeField] private float firstScaleStart = 1f;

    [Tooltip("第二阶段 的缩放")]
    [SerializeField] private float firstScaleStageTwo = 0.9f;

    [Header("第二棋盘")]
    [Tooltip("第二棋盘的根物体（我们做的世界棋盘）")]
    [SerializeField] private Transform secondBoard;

    [Tooltip("开始状态 / 第一阶段 的缩放")]
    [SerializeField] private float secondScaleStart = 0.7f;

    [Tooltip("第二阶段 的缩放")]
    [SerializeField] private float secondScaleStageTwo = 1f;

    [Header("流程")]
    [Tooltip("第二阶段的 A / B 行动由它驱动")]
    [SerializeField] private BattleFlow battleFlow;

    [Tooltip("勾上则结算后连地图一起重置（清空营地、地形还原）")]
    [SerializeField] private bool fullResetOnReturn = false;

    [Header("运行时状态")]
    [SerializeField] private Stage stage = Stage.None;

    // ─────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────
    private void OnEnable()
    {
        GameEventCenter.Instance.GameStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        if (GameEventCenter.Instance == null)
            return;

        GameEventCenter.Instance.GameStateChanged -= OnGameStateChanged;
    }

    private void Start()
    {
        var gm = GameManager.Instance;
        bool playing = gm != null && gm.CurrentState == GameState.Playing;

        if (playing) EnterStageOne();
        else ReturnToStart();
    }

    private void OnGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.BeforePlaying:
                ReturnToStart();
                break;

            case GameState.Playing:
                EnterStageOne();
                break;
        }
    }

    // ═════════════════════════════════════════════
    //  开始 / 第一阶段
    // ═════════════════════════════════════════════
    private void EnterStageOne()
    {
        stage = Stage.One;

        mainScreen.localScale = Vector3.one;
        subScreen.localScale = Vector3.one;

        SetBoardScales(firstScaleStart, secondScaleStart);

        // ═══════════════════════════════════════════════════════
        //  ▼▼▼  留空：第一棋盘的环节写在这里  ▼▼▼
        //
        //  现在这里什么都不做，靠"测试按钮"手动结束第一阶段。
        //  以后接上真正的第一棋盘逻辑，结束时调用 EndStageOne()。
        // ═══════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════
        //  ▲▲▲  留空结束  ▲▲▲
        // ═══════════════════════════════════════════════════════

        Debug.Log("StartButtonUI: 进入第一阶段（第一棋盘大 / 第二棋盘小）", this);
    }

    // 测试按钮接这个
    public void OnClickEndStageOne() => EndStageOne();

    public void EndStageOne()
    {
        if (stage != Stage.One)
        {
            Debug.Log($"StartButtonUI: 当前阶段是 {stage}，无法结束第一阶段", this);
            return;
        }

        EnterStageTwo();
    }

    // ═════════════════════════════════════════════
    //  第二阶段：第二棋盘放大，单位开始行动
    // ═════════════════════════════════════════════
    private void EnterStageTwo()
    {
        stage = Stage.Two;

        mainScreen.localScale = Vector3.one * 0.9f;
        subScreen.localScale = Vector3.one * 1.1f;

        SetBoardScales(firstScaleStageTwo, secondScaleStageTwo);

        Debug.Log("StartButtonUI: 进入第二阶段（第一棋盘小 / 第二棋盘大），单位开始行动", this);

        if (battleFlow == null)
        {
            Debug.LogError("StartButtonUI: battleFlow 未指定，A / B 不会开始移动", this);
            return;
        }

        battleFlow.StartBattle();
    }

    // ═════════════════════════════════════════════
    //  结算后回到开始状态
    //  胜利面板和失败面板的按钮都接这个
    // ═════════════════════════════════════════════
    public void ReturnToStart()
    {
        stage = Stage.One;

        mainScreen.localScale = Vector3.one;
        subScreen.localScale = Vector3.one;

        SetBoardScales(firstScaleStart, secondScaleStart);

        if (battleFlow != null)
        {
            if (fullResetOnReturn) battleFlow.ResetEverything();
            else battleFlow.ResetToPreBattle();
        }

        Debug.Log("StartButtonUI: 已回到开始状态（第一棋盘大 / 第二棋盘小）", this);
    }

    // ═════════════════════════════════════════════
    //  开始按钮（原有）
    // ═════════════════════════════════════════════
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

    private void SetBoardScales(float first, float second)
    {
        if (firstBoard != null) firstBoard.localScale = Vector3.one * first;
        if (secondBoard != null) secondBoard.localScale = Vector3.one * second;
    }
}