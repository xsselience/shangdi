using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]

public class BgmPlayer : MonoBehaviour
{
    public static BgmPlayer Instance { get; private set; }

    [Header("音乐")]
    [Tooltip("要循环播放的曲子")]
    public AudioClip clip;

    [Range(0f, 1f)] public float volume = 0.6f;

    [Tooltip("单曲循环。关掉则播完就停")]
    public bool loop = true;

    [Tooltip("切换场景时不断。关掉则换场景就停")]
    public bool persistAcrossScenes = true;

    [Header("淡入")]
    [Tooltip("开场淡入秒数。0 = 直接开始")]
    public float fadeInDuration = 1.5f;

    AudioSource _source;

    void Awake()
    {
        // 已经有实例了，就销毁自己，避免换场景时叠成两首
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        _source = GetComponent<AudioSource>();
        _source.clip = clip;
        _source.loop = loop;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;     // 0 = 2D，不受距离影响
        _source.volume = fadeInDuration > 0f ? 0f : volume;

        if (clip == null)
        {
            Debug.LogWarning("BgmPlayer: 没有指定 AudioClip，不会播任何东西", this);
            return;
        }

        _source.Play();

        if (fadeInDuration > 0f) StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInDuration);
            _source.volume = Mathf.Lerp(0f, volume, Mathf.Clamp01(t));
            yield return null;
        }

        _source.volume = volume;
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (_source != null) _source.volume = volume;
    }

    public void Stop()
    {
        if (_source != null) _source.Stop();
    }

    public void Play()
    {
        if (_source != null && _source.clip != null) _source.Play();
    }
}
