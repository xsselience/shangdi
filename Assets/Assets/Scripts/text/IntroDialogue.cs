using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public enum BoardFocus { None, First, Second }

[System.Serializable]
public class DialogueStep
{
    [TextArea(2, 5)]
    public string text;

    [Tooltip("这一句显示时把哪块棋盘放大")]
    public BoardFocus focus = BoardFocus.None;

    [Tooltip("文字出现后，等多少秒再切棋盘。0 = 同步（和文字同一帧）")]
    public float focusDelay = 0f;

    [Tooltip("自动等多少秒进下一句。0 = 等玩家按键")]
    public float autoAdvanceDelay = 0f;
}

/// 进入游戏场景后播放的一段说明对话。
/// 每次运行只播一次；任意键推进；播完交还操作权。
public class IntroDialogue : MonoBehaviour
{
    static bool _playedThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _playedThisRun = false; }

    [Header("界面")]
    [Tooltip("整块对话框（含全屏遮罩）。播完会隐藏它")]
    public GameObject root;

    public TMP_Text bodyText;

    [Tooltip("「按任意键继续」的提示物体")]
    public GameObject continueHint;

    [Header("内容")]
    public DialogueStep[] steps;

    [Header("流程")]
    [Tooltip("用来切换棋盘大小")]
    public StartButtonUI startButtonUI;

    public bool playOnStart = true;

    [Tooltip("每句开始后的多少秒内忽略按键，防止连跳")]
    public float inputCooldown = 0.2f;

    [Header("结束后")]
    public UnityEvent onFinished;

    public bool IsPlaying { get; private set; }

    int _index;
    bool _advanceRequested;
    Coroutine _routine;

    void Start()
    {
        if (root != null) root.SetActive(false);

        if (_playedThisRun)
        {
            Debug.Log("IntroDialogue: 本次运行已播过，跳过", this);
            if (onFinished != null) onFinished.Invoke();
            return;
        }

        if (playOnStart) Begin();
    }

    [ContextMenu("播放对话（测试用）")]
    public void Begin()
    {
        if (IsPlaying) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Run());
    }

    void OnDisable()
    {
        // 对话被意外关掉时，别把锁留在里面
        if (startButtonUI != null) startButtonUI.blockBoardFocus = false;
    }
    void Update()
    {
        if (!IsPlaying) return;
        if (SkipPressed()) _advanceRequested = true;
    }

    IEnumerator Run()
    {
        IsPlaying = true;
        _playedThisRun = true;

        // 对话期间禁掉玩家的棋盘聚焦
        if (startButtonUI != null) startButtonUI.blockBoardFocus = true;
        Debug.Log($"IntroDialogue: 上锁，startButtonUI=" +
                  $"{(startButtonUI != null ? startButtonUI.name : "【未指定】")}", this);
        if (root != null) root.SetActive(true);

        for (_index = 0; _index < steps.Length; _index++)
        {
            DialogueStep step = steps[_index];

            // ① 文字先出来
            if (bodyText != null) bodyText.text = step.text;
            if (continueHint != null) continueHint.SetActive(step.autoAdvanceDelay <= 0f);

            // ② 棋盘聚焦。focusDelay = 0 时和文字同一帧，视觉上是同步的
            if (step.focus != BoardFocus.None)
            {
                if (step.focusDelay > 0f)
                    yield return new WaitForSecondsRealtime(step.focusDelay);

                ApplyFocus(step.focus);
            }

            // ③ 等待推进
            _advanceRequested = false;

            if (step.autoAdvanceDelay > 0f)
            {
                float t = 0f;
                while (t < step.autoAdvanceDelay && !_advanceRequested)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                // 冷却：吃掉"进场景那一下"或者"上一句的按键"
                if (inputCooldown > 0f)
                    yield return new WaitForSecondsRealtime(inputCooldown);

                _advanceRequested = false;

                while (!_advanceRequested) yield return null;
            }
        }

        // ④ 结束
        if (root != null) root.SetActive(false);

        IsPlaying = false;
        _routine = null;

        // 交还操作权
        if (startButtonUI != null) startButtonUI.blockBoardFocus = false;

        Debug.Log("IntroDialogue: 对话结束，交还操作权", this);
        if (onFinished != null) onFinished.Invoke();
    }

    void ApplyFocus(BoardFocus focus)
    {
        if (startButtonUI == null) return;

        switch (focus)
        {
            case BoardFocus.First: startButtonUI.ForceFocusFirstBoard(); break;
            case BoardFocus.Second: startButtonUI.ForceFocusSecondBoard(); break;
        }
    }

    // 任意键 / 任意鼠标键
    bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame)) return true;

        return false;
#else
        return Input.anyKeyDown;
#endif
    }
}