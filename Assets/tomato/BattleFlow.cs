using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public class BattleFlow : MonoBehaviour
{
    public enum Phase
    {
        PreBattle,    // 赛前：可编辑地图、可设路线、可开战
        StageIntro,   // 阶段衔接：刚胜利完，等你插入环节（此时不能开战）
        Battle,       // 交战中：A 先行动，B 后行动
        Resolved      // 已结算：等按钮
    }

    public enum RoundResult { None, Success, Fail }
    public enum MapResetMode { KeepCurrent, RestoreSnapshot }

    [Header("引用")]
    public Gridmap map;

    [Tooltip("A 单位。先到终点 = 成功")]
    public PathAgent playerAgent;

    [Tooltip("B 单位。先到终点 = 失败；撞到 A 造成伤害后消失")]
    public PathAgent enemyAgent;

    [Header("交战")]
    [Tooltip("B 撞到 A 时造成的伤害")]
    public int enemyContactDamage = 30;

    [Header("结算界面")]
    [Tooltip("胜利面板，里面的按钮接 ContinueToNextStage")]
    public GameObject successPanel;

    [Tooltip("失败面板，里面的按钮接 ResetEverything")]
    public GameObject failPanel;

    [Header("阶段衔接")]
    [Tooltip("进入阶段衔接时触发。可以在这里挂现成的调用，不用改代码")]
    public UnityEvent onStageIntro;

    [Header("回合节奏")]
    [Tooltip("相邻两次行动之间的间隔（秒）")]
    public float stepInterval = 0.25f;

    [Header("调试")]
    [Tooltip("只影响 ResetToPreBattle（右键菜单里的撤销本回合）")]
    public MapResetMode mapResetMode = MapResetMode.KeepCurrent;

    public Phase Current { get; private set; } = Phase.PreBattle;
    public RoundResult LastResult { get; private set; } = RoundResult.None;
    public int RoundIndex { get; private set; }

    public bool InPreBattle => Current == Phase.PreBattle;
    public bool InBattle => Current == Phase.Battle;
    public bool IsResolved => Current == Phase.Resolved;
    public bool InStageIntro => Current == Phase.StageIntro;

    // 这两个状态下地图可改、路径会重算
    public bool MapUnlocked => Current == Phase.PreBattle || Current == Phase.StageIntro;

    public event Action<RoundResult> OnRoundResolved;

    TileType[] _initialSnapshot;        // 场景最初的地形，供"完全重置"使用
    TileType[] _battleStartSnapshot;    // 每次开战前的地形
    float _tickTimer;

    IEnumerable<PathAgent> AllAgents
    {
        get
        {
            if (playerAgent != null) yield return playerAgent;
            if (enemyAgent != null) yield return enemyAgent;
        }
    }

    IEnumerable<PathAgent> OrderedAgents => AllAgents.OrderBy(a => a.priority);

    void OnEnable() { if (map != null) map.OnChanged += RecomputeAll; }
    void OnDisable() { if (map != null) map.OnChanged -= RecomputeAll; }

    void Start()
    {
        Validate();

        var hp = PlayerHealth();
        if (hp != null) hp.OnDied += HandlePlayerDied;

        // 场景最初的地形。PresetWalls 的执行顺序比这里更早，所以它已经生效了
        if (map != null) _initialSnapshot = map.CaptureSnapshot();

        Current = Phase.PreBattle;
        LastResult = RoundResult.None;
        RoundIndex = 0;
        HideResultPanels();

        RecomputeAll();
        LogSummary();
    }

    void OnDestroy()
    {
        var hp = PlayerHealth();
        if (hp != null) hp.OnDied -= HandlePlayerDied;
    }

    // ─────────────────────────────────────────────
    //  交战推进
    // ─────────────────────────────────────────────
    void Update()
    {
        if (Current != Phase.Battle) return;

        _tickTimer += Time.deltaTime;

        float interval = Mathf.Max(0.01f, stepInterval);

        while (Current == Phase.Battle && _tickTimer >= interval)
        {
            _tickTimer -= interval;
            RunTick();
        }
    }

    void RunTick()
    {
        if (playerAgent == null || enemyAgent == null) return;

        if (AtGoal(playerAgent)) { ResolveRound(RoundResult.Success); return; }

        // ── A 行动 ──
        int aSteps = Mathf.Max(1, playerAgent.stepsPerTick);
        for (int i = 0; i < aSteps; i++)
        {
            if (!playerAgent.TryStep()) break;

            PlayerHealth()?.ResolveGroundEffect();
            if (Current != Phase.Battle) return;

            if (HandleContact()) return;

            if (AtGoal(playerAgent)) { ResolveRound(RoundResult.Success); return; }
        }

        // ── B 行动 ──
        int bSteps = Mathf.Max(1, enemyAgent.stepsPerTick);
        for (int i = 0; i < bSteps; i++)
        {
            if (!enemyAgent.TryStep()) break;

            if (HandleContact()) return;

            if (AtGoal(enemyAgent)) { ResolveRound(RoundResult.Fail); return; }
        }

        if (!playerAgent.HasPath && !enemyAgent.HasPath)
        {
            Debug.Log("BattleFlow: 双方都无法继续前进，且都没到终点", this);
            ResolveRound(RoundResult.Fail);
        }
    }

    bool HandleContact()
    {
        if (playerAgent == null || enemyAgent == null) return false;
        if (!enemyAgent.IsActive) return false;
        if (enemyAgent.GridPos != playerAgent.GridPos) return false;

        Debug.Log($"BattleFlow: {enemyAgent.name} 撞上 {playerAgent.name}，" +
                  $"造成 {enemyContactDamage} 点伤害", this);

        PlayerHealth()?.TakeDamage(enemyContactDamage, "被敌方单位撞击");

        enemyAgent.Despawn();
        Debug.Log($"BattleFlow: {enemyAgent.name} 撞击后消失（本回合不再出现）", this);

        return true;
    }

    // ─────────────────────────────────────────────
    //  赛前 → 交战
    // ─────────────────────────────────────────────
    [ContextMenu("开战")]
    public void StartBattle()
    {
        if (Current == Phase.StageIntro)
        {
            Debug.Log("BattleFlow: 阶段衔接还没结束，先调用 ProceedToPreBattle()", this);
            return;
        }

        if (Current != Phase.PreBattle)
        {
            Debug.Log($"BattleFlow: 当前是 {Current}，不能开战", this);
            return;
        }

        _battleStartSnapshot = map != null ? map.CaptureSnapshot() : null;

        Current = Phase.Battle;
        _tickTimer = 0f;

        if (map != null) map.editable = false;

        foreach (var a in AllAgents) a.intent.locked = true;

        Debug.Log($"BattleFlow: 第 {RoundIndex + 1} 回合开战 —— A 先行", this);
    }

    // ─────────────────────────────────────────────
    //  结算
    // ─────────────────────────────────────────────
    void ResolveRound(RoundResult result)
    {
        if (Current != Phase.Battle) return;

        Current = Phase.Resolved;
        LastResult = result;

        ShowResult(result);
        OnRoundResolved?.Invoke(result);
    }

    void HandlePlayerDied()
    {
        if (Current != Phase.Battle) return;
        Debug.Log("BattleFlow: 玩家单位阵亡", this);
        ResolveRound(RoundResult.Fail);
    }

    void ShowResult(RoundResult r)
    {
        if (successPanel != null) successPanel.SetActive(r == RoundResult.Success);
        if (failPanel != null) failPanel.SetActive(r == RoundResult.Fail);

        Debug.Log(r == RoundResult.Success
            ? $"BattleFlow: 第 {RoundIndex + 1} 回合结算 —— 成功（A 先到）"
            : $"BattleFlow: 第 {RoundIndex + 1} 回合结算 —— 失败（B 先到，或 A 阵亡）", this);
    }

    void HideResultPanels()
    {
        if (successPanel != null) successPanel.SetActive(false);
        if (failPanel != null) failPanel.SetActive(false);
    }

    // ═════════════════════════════════════════════
    //  胜利按钮 → 进入下一阶段
    // ═════════════════════════════════════════════
    [ContextMenu("继续（下一阶段）")]
    public void ContinueToNextStage()
    {
        if (Current != Phase.Resolved)
        {
            Debug.Log($"BattleFlow: 当前是 {Current}，没有可继续的结算", this);
            return;
        }
        if (LastResult != RoundResult.Success)
        {
            Debug.Log("BattleFlow: 本回合不是胜利，不能进入下一阶段", this);
            return;
        }

        HideResultPanels();
        LastResult = RoundResult.None;

        // 1. 地图保留不动，在 B 停下的位置建一个营地
        if (map != null && enemyAgent != null)
        {
            Vector2Int c = enemyAgent.GridPos;
            if (map.AddCamp(c))
                Debug.Log($"BattleFlow: 在 {c} 建立敌方营地（共 {map.CampCount()} 个）", this);
        }

        RoundIndex++;

        // 2. 双方归位（退场的 B 会在这里重新出现）
        foreach (var a in AllAgents) a.ResetToStart();

        // 3. 血量回满
        ResetPlayerHealth();

        // 4. 进入阶段衔接：先重算路径，让画面立刻反映出新营地
        Current = Phase.StageIntro;
        if (map != null) map.editable = true;
        RecomputeAll();

        Debug.Log($"BattleFlow: 进入第 {RoundIndex + 1} 阶段衔接", this);

        // 5. 给你留的挂钩（Inspector 里可以挂现成的调用）
        onStageIntro?.Invoke();

        // 6. 给你留的空位
        RunStageIntro();
    }

    // ═══════════════════════════════════════════════════════════
    //  ▼▼▼  留空：第二阶段开始前的其它环节写在这里  ▼▼▼
    //
    //  进入这个方法时，状态是这样的：
    //    · 地图保留，B 原位已经生成了一个营地
    //    · A、B 都已回到出发格，血量已回满
    //    · 路径已重算，画面处于赛前状态
    //    · 但还不能开战 —— 这是刻意的，保证你的环节不会被跳过
    //
    //  你的环节做完之后，调用 ProceedToPreBattle() 交还控制权。
    //  这里什么都不写的话会立刻交还，等同于没有中间环节。
    //
    //  需要等待的话可以直接开协程：
    //      StartCoroutine(MyIntro());
    //      IEnumerator MyIntro()
    //      {
    //          yield return new WaitForSeconds(2f);
    //          ProceedToPreBattle();
    //      }
    //
    // ═══════════════════════════════════════════════════════════
    void RunStageIntro()
    {
        ProceedToPreBattle();
    }
    // ═══════════════════════════════════════════════════════════
    //  ▲▲▲  留空结束  ▲▲▲
    // ═══════════════════════════════════════════════════════════

    // 结束阶段衔接，交还给玩家规划
    [ContextMenu("结束阶段衔接")]
    public void ProceedToPreBattle()
    {
        if (Current != Phase.StageIntro)
        {
            Debug.Log($"BattleFlow: 当前是 {Current}，不在阶段衔接中，忽略", this);
            return;
        }

        Current = Phase.PreBattle;
        if (map != null) map.editable = true;
        RecomputeAll();

        Debug.Log($"BattleFlow: 第 {RoundIndex + 1} 阶段就绪，可以规划并开战", this);
    }

    // ═════════════════════════════════════════════
    //  失败按钮 → 完全重置回最初
    // ═════════════════════════════════════════════
    [ContextMenu("完全重置（回到最初）")]
    public void ResetEverything()
    {
        HideResultPanels();
        LastResult = RoundResult.None;
        RoundIndex = 0;

        // 1. 清空所有营地
        if (map != null) map.ClearCamps();

        // 2. 地形回到场景最初的样子
        if (map != null)
        {
            map.editable = true;
            map.RestoreSnapshot(_initialSnapshot);
        }

        // 3. 单位归位 + 满血
        foreach (var a in AllAgents) a.ResetToStart();
        ResetPlayerHealth();

        Current = Phase.PreBattle;
        RecomputeAll();

        Debug.Log("BattleFlow: 已完全重置回最初状态（营地清空，地形还原）", this);
    }

    // ═════════════════════════════════════════════
    //  调试：撤销本回合（不接按钮，只走右键菜单）
    // ═════════════════════════════════════════════
    [ContextMenu("撤销本回合")]
    public void ResetToPreBattle()
    {
        if (Current == Phase.PreBattle)
        {
            Debug.Log("BattleFlow: 已经是赛前状态", this);
            return;
        }

        Current = Phase.PreBattle;
        LastResult = RoundResult.None;
        HideResultPanels();

        if (map != null) map.editable = true;

        foreach (var a in AllAgents) a.ResetToStart();

        if (mapResetMode == MapResetMode.RestoreSnapshot && map != null)
            map.RestoreSnapshot(_battleStartSnapshot);

        ResetPlayerHealth();
        RecomputeAll();

        Debug.Log("BattleFlow: 已撤销本回合（营地保留）", this);
    }

    // ─────────────────────────────────────────────
    //  内部
    // ─────────────────────────────────────────────
    static bool AtGoal(PathAgent a) => a.GridPos == a.intent.target;

    UnitHealth PlayerHealth() =>
        playerAgent != null ? playerAgent.GetComponent<UnitHealth>() : null;

    void ResetPlayerHealth() => PlayerHealth()?.ResetToFull();

    void RecomputeAll()
    {
        if (!MapUnlocked) return;

        foreach (var a in OrderedAgents) a.Recompute();
    }

    [ContextMenu("自检")]
    public void Validate()
    {
        if (map == null) Debug.LogError("BattleFlow: map 未指定", this);
        if (playerAgent == null) Debug.LogError("BattleFlow: playerAgent（A）未指定", this);
        if (enemyAgent == null) Debug.LogError("BattleFlow: enemyAgent（B）未指定", this);

        foreach (var a in AllAgents)
        {
            if (a.map == null)
                Debug.LogError($"BattleFlow: {a.name} 的 PathAgent.map 未指定", a);
            else if (a.map != map)
                Debug.LogWarning($"BattleFlow: {a.name} 用的 map 与本脚本不同", a);

            if (a.GetComponent<UnitView>() == null)
                Debug.LogWarning($"BattleFlow: {a.name} 没有 UnitView —— 看不见棋子", a);

            if (a.GetComponent<PathRenderer>() == null)
                Debug.LogWarning($"BattleFlow: {a.name} 没有 PathRenderer —— 看不见路径", a);
        }

        if (PlayerHealth() == null)
            Debug.LogWarning("BattleFlow: playerAgent 上没有 UnitHealth —— 不会扣血", playerAgent);
    }

    [ContextMenu("输出摘要")]
    public void LogSummary()
    {
        if (map == null) { Debug.Log("map 未指定", this); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"[自检] map={map.name} {map.width}x{map.height} cellSize={map.cellSize}");
        sb.AppendLine($"        阶段={Current}  回合={RoundIndex + 1}  营地={map.CampCount()}个  " +
                      $"地图可改={MapUnlocked}");

        foreach (var a in AllAgents)
        {
            var hp = a.GetComponent<UnitHealth>();
            sb.AppendLine($"        {a.name}: 出发={a.StartPos} 当前={a.GridPos} " +
                          $"终点={a.intent.target} path={a.Path.Count} " +
                          $"在场={a.IsActive} HP={(hp != null ? $"{hp.Hp}/{hp.maxHp}" : "-")}");
        }

        Debug.Log(sb.ToString(), this);
    }
}
