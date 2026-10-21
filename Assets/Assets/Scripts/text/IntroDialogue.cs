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

    [Tooltip("文字出现后，等多少秒再切棋盘。0 = 同步")]
    public float focusDelay = 0f;

    [Tooltip("自动等多少秒进下一句。0 = 等玩家按键")]
    public float autoAdvanceDelay = 0f;
}

[System.Serializable]
public class DialogueSequence
{
    public string name = "对话";

    public DialogueStep[] steps;

    [Tooltip("播到最后一句时显示的物体（比如结束按钮）。留空则不显示")]
    public GameObject revealOnLastStep;

    public UnityEvent onFinished;
}

/// 共用一个对话框，但分「开局」和「结尾」两套内容。
public class IntroDialogue : MonoBehaviour
{
    static bool _playedOpeningThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _playedOpeningThisRun = false; }

    [Header("界面")]
    public GameObject root;
    public TMP_Text bodyText;
    public GameObject continueHint;

    [Header("流程")]
    public StartButtonUI startButtonUI;

    [Tooltip("每句开始后的多少秒内忽略按键，防止连跳")]
    public float inputCooldown = 0.2f;

    [Header("开局对话（每次运行只播一次）")]
    public bool playOpeningOnStart = true;
    public DialogueSequence opening = new DialogueSequence();

    [Header("通关后的对话（不限次数）")]
    public DialogueSequence ending = new DialogueSequence();

    [Header("各阶段结束时的对话（下标 = 阶段下标，留空则该阶段不播）")]
    [Tooltip("第 N 项 = 第 N 阶段结束时播的对话。数组长度可以比阶段数少")]
    public DialogueSequence[] stageEndDialogues;

    public bool IsPlaying { get; private set; }

    DialogueSequence _current;
    int _index;
    bool _advanceRequested;
    Coroutine _routine;
    CanvasGroup _group;

    void Awake()
    {
        if (root == null) root = gameObject;

        if (root == gameObject)
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Start()
    {
        SetVisible(false);
        HideReveal(opening);
        HideReveal(ending);
        if (stageEndDialogues != null)
        {
            for (int i = 0; i < stageEndDialogues.Length; i++)
                HideReveal(stageEndDialogues[i]);
        }

        if (_playedOpeningThisRun)
        {
            Debug.Log("IntroDialogue: 本次运行已播过开局对话，跳过", this);
            if (opening != null && opening.onFinished != null) opening.onFinished.Invoke();
            return;
        }

        if (playOpeningOnStart) BeginOpening();
    }

    void Update()
    {
        if (!IsPlaying) return;
        if (SkipPressed()) _advanceRequested = true;
    }

    void OnDisable()
    {
        if (startButtonUI != null) startButtonUI.blockBoardFocus = false;
    }

    // ─────────────────────────────────────────────
    //  入口
    // ─────────────────────────────────────────────
    [ContextMenu("播开局对话（测试用）")]
    public void BeginOpening()
    {
        _playedOpeningThisRun = true;
        Begin(opening);
    }

    [ContextMenu("播结尾对话（测试用）")]
    public void BeginEnding()
    {
        Begin(ending);
    }


    /// <summary>播某个阶段结束时的对话。没有可播的就返回 false</summary>
    public bool BeginStageEnd(int stageIndex)
    {
        DialogueSequence seq = StageSequence(stageIndex);
        if (seq == null) return false;

        Begin(seq);
        return true;
    }

    /// <summary>取某个阶段的对话，没有就返回 null</summary>
    public DialogueSequence StageSequence(int stageIndex)
    {
        if (stageEndDialogues == null) return null;
        if (stageIndex < 0 || stageIndex >= stageEndDialogues.Length) return null;

        DialogueSequence seq = stageEndDialogues[stageIndex];
        if (seq == null || seq.steps == null || seq.steps.Length == 0) return null;

        return seq;
    }
    public void Begin(DialogueSequence sequence)
    {
        if (sequence == null || sequence.steps == null || sequence.steps.Length == 0)
        {
            Debug.LogWarning($"IntroDialogue: 序列「{(sequence != null ? sequence.name : "null")}」没有步骤", this);
            return;
        }

        if (IsPlaying)
        {
            Debug.LogWarning("IntroDialogue: 正在播另一段对话，忽略这次调用", this);
            return;
        }

        _current = sequence;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Run(sequence));
    }

    // ─────────────────────────────────────────────
    //  播放
    // ─────────────────────────────────────────────
    IEnumerator Run(DialogueSequence sequence)
    {
        IsPlaying = true;

        HideReveal(sequence);
        SetVisible(true);

        if (startButtonUI != null) startButtonUI.blockBoardFocus = true;

        for (_index = 0; _index < sequence.steps.Length; _index++)
        {
            DialogueStep step = sequence.steps[_index];

            // 最后一句 → 亮出按钮
            if (sequence.revealOnLastStep != null && _index == sequence.steps.Length - 1)
                sequence.revealOnLastStep.SetActive(true);

            if (bodyText != null) bodyText.text = step.text;
            if (continueHint != null) continueHint.SetActive(step.autoAdvanceDelay <= 0f);

            if (step.focus != BoardFocus.None)
            {
                if (step.focusDelay > 0f)
                    yield return new WaitForSecondsRealtime(step.focusDelay);

                ApplyFocus(step.focus);
            }

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
                if (inputCooldown > 0f)
                    yield return new WaitForSecondsRealtime(inputCooldown);

                _advanceRequested = false;
                while (!_advanceRequested) yield return null;
            }
        }

        SetVisible(false);

        IsPlaying = false;
        _routine = null;
        _current = null;

        if (startButtonUI != null) startButtonUI.blockBoardFocus = false;

        Debug.Log($"IntroDialogue: 序列「{sequence.name}」结束", this);

        if (sequence.onFinished != null) sequence.onFinished.Invoke();
    }

    // ─────────────────────────────────────────────
    //  内部
    // ─────────────────────────────────────────────
    void HideReveal(DialogueSequence sequence)
    {
        if (sequence != null && sequence.revealOnLastStep != null)
            sequence.revealOnLastStep.SetActive(false);
    }

    void SetVisible(bool visible)
    {
        if (_group != null)
        {
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
            return;
        }

        if (root != null) root.SetActive(visible);
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