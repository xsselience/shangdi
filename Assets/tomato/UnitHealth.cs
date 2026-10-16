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

    [Header("攻击力")]
    [Tooltip("这个单位造成的伤害。撞击别人、以及它留下的营地，都用这个值")]
    public int attackPower = 30;

    [Header("当前血量")]
    [Tooltip("运行时实时更新。也可以在 Inspector 里直接改它来做测试")]
    [SerializeField] int currentHp = 100;

    public int Hp => currentHp;
    public bool IsDead => _dead;

    public event Action<int, int> OnHealthChanged;
    public event Action<int> OnDamaged;
    public event Action<UnitHealth> OnDied;

    bool _dead;

    void Awake()
    {
        if (currentHp <= 0 && !_dead)
            currentHp = Mathf.Max(1, maxHp);
    }

    void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        attackPower = Mathf.Max(0, attackPower);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    void Start()
    {
        if (agent == null)
            Debug.LogError($"{name}: UnitHealth.agent 未指定 —— 不会扣血", this);
        else
            OnHealthChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>由 BattleFlow 在"A 行动之后"调用。伤害从营地自己身上读。</summary>
    public void ResolveGroundEffect()
    {
        if (_dead || agent == null || agent.map == null) return;

        int campDamage = agent.map.GetCampDamage(agent.GridPos);
        if (campDamage > 0)
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
            OnDied?.Invoke(this);
        }
    }

    public void Heal(int amount)
    {
        if (_dead || amount <= 0) return;

        int before = currentHp;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        if (currentHp == before) return;

        OnHealthChanged?.Invoke(currentHp, maxHp);
    }

    public void ResetToFull()
    {
        _dead = false;
        currentHp = Mathf.Max(1, maxHp);
        OnHealthChanged?.Invoke(currentHp, maxHp);
    }
}
