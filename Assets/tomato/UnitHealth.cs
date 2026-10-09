using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class UnitHealth : MonoBehaviour
{
    [Header("引用")]
    public PathAgent agent;

    [Header("血量")]
    public int maxHp = 100;

    [Tooltip("每踩中一格营地扣多少血")]
    public int campDamage = 20;

    [Header("当前血量")]
    [Tooltip("运行时实时更新。也可以在 Inspector 里直接改它来做测试")]
    [SerializeField] int currentHp = 100;

    public int Hp => currentHp;
    public bool IsDead => _dead;

    public event Action<int, int> OnHealthChanged;   // (当前, 上限)
    public event Action<int> OnDamaged;              // 本次扣了多少
    public event Action OnDied;

    bool _dead;

    void Awake()
    {
        // 只在空着的时候补一个初始值，不覆盖你手填的数字
        if (currentHp <= 0 && !_dead)
            currentHp = Mathf.Max(1, maxHp);
    }

    void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    void Start()
    {
        if (agent == null)
        {
            Debug.LogError($"{name}: UnitHealth.agent 未指定 —— 不会扣血", this);
            return;
        }
        OnHealthChanged?.Invoke(currentHp, maxHp);
    }

    // ── 由 BattleFlow 在"A 行动之后"调用 ──
    public void ResolveGroundEffect()
    {
        if (_dead || agent == null || agent.map == null) return;

        if (agent.map.HasCamp(agent.GridPos))
            TakeDamage(campDamage, "敌方营地");
    }

    public void TakeDamage(int amount, string reason = null)
    {
        if (_dead || amount <= 0) return;

        currentHp = Mathf.Max(0, currentHp - amount);
        OnDamaged?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHp, maxHp);

        Debug.Log($"{name}: -{amount} HP" +
                  (reason != null ? $"（{reason}）" : "") +
                  $"  剩余 {currentHp}/{maxHp}", this);

        if (currentHp <= 0)
        {
            _dead = true;
            Debug.Log($"{name}: 血量归零", this);
            OnDied?.Invoke();
        }
    }

    public void ResetToFull()
    {
        _dead = false;
        currentHp = Mathf.Max(1, maxHp);
        OnHealthChanged?.Invoke(currentHp, maxHp);
        Debug.Log($"{name}: 血量回满 {currentHp}/{maxHp}", this);
    }
}
