using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;

public class ScoreBoard : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("用来判断胜负的战斗流程")]
    public BattleFlow battleFlow;

    [Tooltip("要计分的单位。通常是 A")]
    public UnitHealth playerHealth;

    [Tooltip("显示积分的文本")]
    public TMP_Text scoreText;

    [Header("显示")]
    public string prefix = "积分：";
    public bool logChanges = true;

    [Header("初始值")]
    public int startingScore = 0;

    public int TotalScore { get; private set; }

    /// <summary>参数：(本次增加, 总分)</summary>
    public event Action<int, int> OnScoreChanged;

    void Awake()
    {
        TotalScore = startingScore;
    }

    void OnEnable()
    {
        if (battleFlow != null) battleFlow.OnRoundResolved += OnRoundResolved;

        if (GameEventCenter.Instance != null)
            GameEventCenter.Instance.GameStateChanged += OnGameStateChanged;
    }

    void OnDisable()
    {
        if (battleFlow != null) battleFlow.OnRoundResolved -= OnRoundResolved;

        if (GameEventCenter.Instance != null)
            GameEventCenter.Instance.GameStateChanged -= OnGameStateChanged;
    }

    void Start()
    {
        if (battleFlow == null) Debug.LogWarning("ScoreBoard: battleFlow 未指定，不会计分", this);
        if (playerHealth == null) Debug.LogWarning("ScoreBoard: playerHealth 未指定，不会计分", this);
        if (scoreText == null) Debug.LogWarning("ScoreBoard: scoreText 未指定，分数不会显示", this);

        RefreshText();
    }

    // 结算那一刻触发，早于任何血量重置
    void OnRoundResolved(BattleFlow.RoundResult result)
    {
        if (result != BattleFlow.RoundResult.Success) return;   // 失败 / 阵亡不计分

        int gained = playerHealth != null ? Mathf.Max(0, playerHealth.Hp) : 0;
        AddScore(gained, "到达终点");
    }

    void OnGameStateChanged(GameState state)
    {
        if (state == GameState.BeforePlaying) ResetScore();
    }

    public void AddScore(int amount, string reason = null)
    {
        if (amount <= 0)
        {
            RefreshText();
            return;
        }

        TotalScore += amount;

        if (logChanges)
            Debug.Log($"ScoreBoard: +{amount}" +
                      (reason != null ? $"（{reason}）" : "") +
                      $"  总分 {TotalScore}", this);

        RefreshText();
        if (OnScoreChanged != null) OnScoreChanged.Invoke(amount, TotalScore);
    }

    [ContextMenu("重置积分")]
    public void ResetScore()
    {
        TotalScore = startingScore;
        RefreshText();
        if (OnScoreChanged != null) OnScoreChanged.Invoke(0, TotalScore);
        if (logChanges) Debug.Log($"ScoreBoard: 积分已重置为 {TotalScore}", this);
    }

    void RefreshText()
    {
        if (scoreText == null) return;
        scoreText.text = prefix + TotalScore;
    }
}
