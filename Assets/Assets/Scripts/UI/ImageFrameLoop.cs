using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageFrameLoop : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空则用自己身上的 Image")]
    public Image targetImage;

    [Header("帧")]
    [Tooltip("按顺序排好，会一直循环")]
    public Sprite[] frames;

    [Tooltip("每帧停留多少秒")]
    public float frameDuration = 0.12f;

    void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<Image>();
    }

    void OnEnable()
    {
        if (targetImage == null)
        {
            Debug.LogWarning($"{name}: ImageFrameLoop 找不到 Image", this);
            enabled = false;
            return;
        }

        if (frames == null || frames.Length == 0) return;

        StartCoroutine(Play());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator Play()
    {
        int i = 0;
        float delay = Mathf.Max(0.02f, frameDuration);

        while (true)
        {
            if (frames[i] != null) targetImage.sprite = frames[i];

            i = (i + 1) % frames.Length;      // 到末尾自动回到第 0 张

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / delay;
                yield return null;
            }
        }
    }
}
