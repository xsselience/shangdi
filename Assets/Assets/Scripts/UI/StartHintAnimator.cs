using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartHintAnimator : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("要隐藏的那个物体。留空则用自己")]
    public GameObject hintRoot;

    [Tooltip("显示帧动画的 Image。留空则用 hintRoot 上的")]
    public Image targetImage;

    [Header("帧")]
    [Tooltip("按顺序排好，会一直循环")]
    public Sprite[] frames;

    [Tooltip("每帧停留多少秒")]
    public float frameDuration = 0.12f;

    [Header("没有帧时的兜底呼吸")]
    public bool fallbackPulse = true;
    public float pulsePeriod = 1.2f;

    [Range(0f, 1f)] public float minAlpha = 0.25f;

    Sprite _firstSprite;
    CanvasGroup _group;
    Coroutine _anim;
    bool _hidden;

    void Awake()
    {
        if (hintRoot == null) hintRoot = gameObject;
        if (targetImage == null) targetImage = hintRoot.GetComponent<Image>();

        _group = hintRoot.GetComponent<CanvasGroup>();
        _firstSprite = targetImage != null ? targetImage.sprite : null;
    }

    void OnEnable()
    {
        _hidden = false;

        if (hintRoot != null) hintRoot.SetActive(true);

        if (frames != null && frames.Length > 0)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(PlayFrames());
        }
    }

    void OnDisable()
    {
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }

        if (targetImage != null && _firstSprite != null)
            targetImage.sprite = _firstSprite;
    }

    void Update()
    {
        if (_hidden) return;
        if (frames != null && frames.Length > 0) return;   // 有帧就不用呼吸
        if (!fallbackPulse) return;

        float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f /
                                Mathf.Max(0.05f, pulsePeriod)) + 1f) * 0.5f;
        float a = Mathf.Lerp(minAlpha, 1f, wave);

        if (_group != null) _group.alpha = a;
        else if (targetImage != null)
        {
            Color c = targetImage.color;
            c.a = a;
            targetImage.color = c;
        }
    }

    /// <summary>隐藏提示并停止动画。点了「开始」之后调这个。</summary>
    public void Hide()
    {
        _hidden = true;

        if (_anim != null) { StopCoroutine(_anim); _anim = null; }

        if (_group != null) _group.alpha = 1f;
        if (targetImage != null)
        {
            Color c = targetImage.color;
            c.a = 1f;
            targetImage.color = c;
        }

        if (hintRoot != null) hintRoot.SetActive(false);
    }

    IEnumerator PlayFrames()
    {
        int i = 0;
        float delay = Mathf.Max(0.02f, frameDuration);

        while (true)
        {
            if (targetImage != null) targetImage.sprite = frames[i];

            i = (i + 1) % frames.Length;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / delay;
                yield return null;
            }
        }
    }
}
