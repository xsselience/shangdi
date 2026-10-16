using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// 开场动画。
/// · 每次启动游戏只播一次；切回开始场景不会再播
/// · 第一次按任意键 → 弹出呼吸提示
/// · 再按一次     → 直接跳到动画结束帧
public class IntroSequence : MonoBehaviour
{
    static bool _playedThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _playedThisRun = false; }

    [Header("引用")]
    [Tooltip("开场画面。播完后会被隐藏。脚本不要挂在这个物体上")]
    public GameObject introRoot;

    [Tooltip("播放片段的 Animation 组件")]
    public Animation animationTarget;

    [Tooltip("片段名。留空 = 播 Animation 上的默认片段")]
    public string clipName = "";

    [Tooltip("第一次按键后才显示的提示物体。需要挂 CanvasGroup")]
    public GameObject skipHint;

    [Header("播放")]
    public bool playOnStart = true;

    [Tooltip("片段播完之后再停留多少秒")]
    public float holdAfterClip = 0.5f;

    [Tooltip("最短显示时间。算出来的时长低于它就用它兜底")]
    public float minDuration = 2f;

    [Tooltip("完全没配片段时用这个时长")]
    public float fallbackDuration = 2f;

    [Header("输入")]
    [Tooltip("开场后多少秒内不接受按键。用来吃掉「点 Play」那一下")]
    public float inputLockTime = 0.4f;

    [Header("呼吸提示")]
    public float hintPeriod = 1.2f;

    [Range(0f, 1f)] public float hintMinAlpha = 0.3f;

    [Header("结束后")]
    public UnityEvent onFinished;

    public bool IsPlaying { get; private set; }

    CanvasGroup _hintGroup;

    void Start()
    {
        if (introRoot == null)
        {
            Debug.LogWarning("IntroSequence: introRoot 未指定", this);
            enabled = false;
            return;
        }

        introRoot.SetActive(false);

        if (skipHint != null)
        {
            skipHint.SetActive(false);

            _hintGroup = skipHint.GetComponent<CanvasGroup>();
            if (_hintGroup == null)
                Debug.LogWarning("IntroSequence: skipHint 上没有 CanvasGroup，呼吸不会生效", skipHint);
        }

        if (_playedThisRun)
        {
            Debug.Log("IntroSequence: 本次运行已播过，跳过开场动画", this);
            if (onFinished != null) onFinished.Invoke();
            return;
        }

        if (playOnStart) Begin();
    }

    [ContextMenu("重新播放（测试用）")]
    public void Begin()
    {
        if (IsPlaying) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        IsPlaying = true;
        _playedThisRun = true;

        introRoot.SetActive(true);

        // ── 决定播多久 ──
        string clip = ResolveClip();
        AnimationState state = null;

        float duration = Mathf.Max(minDuration, fallbackDuration);

        if (animationTarget != null && !string.IsNullOrEmpty(clip) &&
            animationTarget.GetClip(clip) != null)
        {
            state = animationTarget[clip];
            state.time = 0f;
            state.speed = 1f;
            animationTarget.Play(clip);

            duration = Mathf.Max(minDuration, state.length);
        }

        Debug.Log($"IntroSequence: 开始播放 | 片段「{clip}」 时长 {duration:F2}s " +
                  $"输入锁定 {inputLockTime:F2}s 结束停留 {holdAfterClip:F2}s", this);

        // ── 播放 ──
        bool hintShown = false;
        bool jumpedToEnd = false;
        float t = 0f;
        float total = duration + Mathf.Max(0f, holdAfterClip);

        while (t < total)
        {
            // 开头那一小段时间不收输入，避免把「点 Play」的鼠标算进去
            if (t >= inputLockTime && SkipPressed())
            {
                if (!hintShown)
                {
                    hintShown = true;
                    if (skipHint != null) skipHint.SetActive(true);
                    Debug.Log("IntroSequence: 第一次按键 → 显示跳过提示", this);
                }
                else
                {
                    if (state != null)
                    {
                        state.speed = 0f;
                        state.time = state.length;
                        animationTarget.Sample();
                    }

                    if (skipHint != null) skipHint.SetActive(false);

                    Debug.Log("IntroSequence: 第二次按键 → 跳到结束帧", this);
                    jumpedToEnd = true;
                    break;
                }
            }

            if (hintShown && _hintGroup != null)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f /
                                        Mathf.Max(0.05f, hintPeriod)) + 1f) * 0.5f;
                _hintGroup.alpha = Mathf.Lerp(hintMinAlpha, 1f, wave);
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (jumpedToEnd)
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, holdAfterClip));

        if (animationTarget != null) animationTarget.Stop();

        introRoot.SetActive(false);
        if (skipHint != null) skipHint.SetActive(false);

        IsPlaying = false;

        Debug.Log("IntroSequence: 开场动画结束", this);
        if (onFinished != null) onFinished.Invoke();
    }

    string ResolveClip()
    {
        if (animationTarget == null) return "";
        if (!string.IsNullOrEmpty(clipName)) return clipName;
        return animationTarget.clip != null ? animationTarget.clip.name : "";
    }

    bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame)) return true;

        return false;
#else
        return Input.anyKeyDown;
#endif
    }
}
