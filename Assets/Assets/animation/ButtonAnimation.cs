using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonAnimation : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空则自动取自己身上的 Animation")]
    public Animation animationTarget;

    [Header("片段")]
    [Tooltip("留空 = 播放 Animation 上的默认片段。填名字则播指定片段")]
    public string clipName = "";

    Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();

        if (animationTarget == null) animationTarget = GetComponent<Animation>();

        if (animationTarget == null)
        {
            Debug.LogError($"{name}: 找不到 Animation 组件", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (_button != null) _button.onClick.AddListener(Play);
    }

    void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(Play);
    }

    [ContextMenu("测试播放")]
    public void Play()
    {
        if (animationTarget == null) return;

        string clip = string.IsNullOrEmpty(clipName)
            ? (animationTarget.clip != null ? animationTarget.clip.name : "")
            : clipName;

        if (string.IsNullOrEmpty(clip))
        {
            Debug.LogWarning($"{name}: Animation 上没有默认片段，也没填 Clip Name", this);
            return;
        }

        if (animationTarget.GetClip(clip) == null)
        {
            Debug.LogWarning($"{name}: Animation 上找不到片段「{clip}」", this);
            return;
        }

        // 显式回到第 0 秒，保证"每次点击都从头播"
        AnimationState state = animationTarget[clip];
        if (state != null)
        {
            state.time = 0f;
            state.speed = 1f;
        }

        animationTarget.Play(clip);
    }
}
