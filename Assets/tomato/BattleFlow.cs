using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public enum UnitSpawnTiming
{
    AtStart,            // 开局就在场
    AfterTicks,         // 开战经过 N 个回合后
    AfterPlayerSteps,   // A 累计走了 N 步后
}
[Serializable]
public sealed class UnitSetup
{
    [Tooltip("场景里的单位")]
    public PathAgent unit;

    [Tooltip("这一关的出发格")]
    public Vector2Int cell;

    [Header("目标点")]
    [Tooltip("勾上则用下面这个目标；不勾则沿用关卡上填的统一目标")]
    public bool useOwnGoal;

    [Tooltip("这个单位自己的目标格（Use Own Goal 勾上时才生效）")]
    public Vector2Int ownGoal;

    [Header("出场时机")]
    public UnitSpawnTiming timing = UnitSpawnTiming.AtStart;

    [Tooltip("AfterTicks：开战经过多少个回合后出场")]
    public int spawnAfterTicks = 8;

    [Tooltip("AfterPlayerSteps：A 累计走多少步后出场")]
    public int spawnAfterPlayerSteps = 12;

    [Header("可选覆盖。小于等于 0 表示不改，沿用单位自己 Inspector 里的值")]
    public int maxHpOverride = -1;
    public int attackPowerOverride = -1;
    public double stepsPerTickOverride = 0;

    [NonSerialized] public bool spawned;   // 运行时用，别在 Inspector 里动
}

[Serializable]
public sealed class SecondBoardLevel
{
    public string levelName = "关卡";

    [Tooltip("A 和 B 共同的目标格")]
    public Vector2Int goal;

    public List<UnitSetup> playerUnits = new List<UnitSetup>();
    public List<UnitSetup> enemyUnits = new List<UnitSetup>();
}
public class BattleFlow : MonoBehaviour
{
    public enum Phase { PreBattle, StageIntro, Battle, Resolved }
    public enum RoundResult { None, Success, Fail }
    public enum MapResetMode { KeepCurrent, RestoreSnapshot }

    [Header("引用")]
    public Gridmap map;

    [Header("第二棋盘关卡（按章节顺序）")]
    public List<SecondBoardLevel> levels = new List<SecondBoardLevel>();

    [Header("没配 levels 时的兜底单位")]
    public PathAgent playerAgent;
    public PathAgent enemyAgent;

    [Header("交战")]
    [Tooltip("敌人身上没挂 UnitHealth 时用的兜底伤害")]
    public int enemyContactDamage = 30;

    [Header("结算界面")]
    public GameObject successPanel;
    public GameObject failPanel;

    [Header("回合节奏")]
    public float stepInterval = 0.25f;

    [Header("调试")]
    public MapResetMode mapResetMode = MapResetMode.KeepCurrent;

    public Phase Current { get; private set; } = Phase.PreBattle;
    public RoundResult LastResult { get; private set; } = RoundResult.None;
    public int RoundIndex { get; private set; }
    public int LevelIndex { get; private set; }

    public bool InPreBattle => Current == Phase.PreBattle;
    public bool InBattle => Current == Phase.Battle;
    public bool IsResolved => Current == Phase.Resolved;
    public bool MapUnlocked => Current == Phase.PreBattle || Current == Phase.StageIntro;

    public event Action<RoundResult> OnRoundResolved;

    [Header("运行时状态")]
    [Tooltip("开战时在 Console 里清点一遍场景里所有单位")]
    [SerializeField] bool logUnitsOnStart = true;
    [SerializeField] int _ticks;          // 开战以来的回合数
    [SerializeField] int _playerSteps;    // 开战以来 A 累计走的步数
    // 单位的原始数值。关卡里的"覆盖"是临时的，每次换关卡先还原到这里
    readonly Dictionary<PathAgent, double> _baseSteps = new Dictionary<PathAgent, double>();
    readonly Dictionary<UnitHealth, int> _baseMaxHp = new Dictionary<UnitHealth, int>();
    readonly Dictionary<UnitHealth, int> _baseAttack = new Dictionary<UnitHealth, int>();

    TileType[] _battleStartSnapshot;
    float _tickTimer;

    readonly List<PathAgent> _allPlayers = new List<PathAgent>();
    readonly List<PathAgent> _allEnemies = new List<PathAgent>();

    // ─────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────
    void OnEnable() { if (map != null) map.OnChanged += RecomputeAll; }
    void OnDisable() { if (map != null) map.OnChanged -= RecomputeAll; }

    void Start()
    {
        if (levels == null) levels = new List<SecondBoardLevel>();

        CollectAllUnits();

        if (levels.Count == 0)
        {
            Debug.LogWarning("BattleFlow: 没有配任何关卡，用兜底的单 A 单 B 建一个", this);
            BuildFallbackLevel();
            CollectAllUnits();
        }

        CaptureBaseStats();
        SubscribeDeaths();

        if (logUnitsOnStart) LogAllSceneUnits();

        if (map != null) _battleStartSnapshot = map.CaptureSnapshot();

        Current = Phase.PreBattle;
        LastResult = RoundResult.None;
        RoundIndex = 0;

        ApplyLevel(0);
        HideResultPanels();
        LogSummary();
    }

    void OnDestroy() { UnsubscribeDeaths(); }

    void Update()
    {
        SyncResultPanels();

        if (Current != Phase.Battle) return;

        _tickTimer += Time.deltaTime;

        float interval = Mathf.Max(0.01f, stepInterval);

        while (Current == Phase.Battle && _tickTimer >= interval)
        {
            _tickTimer -= interval;
            _ticks++;
            RunTick();
        }

        // 增援出场检查放在回合外面，出场后要等下一回合才行动
        TryActivatePending();
    }

    // ─────────────────────────────────────────────
    //  关卡
    // ─────────────────────────────────────────────
    public SecondBoardLevel CurrentLevel =>
        levels != null && LevelIndex >= 0 && LevelIndex < levels.Count ? levels[LevelIndex] : null;

    /// <summary>载入第 index 个关卡。StartButtonUI 在每章开始时调它。</summary>
    public void ApplyLevel(int index)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("BattleFlow: 没有任何关卡可以载入", this);
            return;
        }

        LevelIndex = Mathf.Clamp(index, 0, levels.Count - 1);

        SecondBoardLevel lv = CurrentLevel;
        WarnDuplicates(lv);
        if (lv == null)
        {
            Debug.LogError($"BattleFlow: 关卡下标 {LevelIndex} 取不到内容", this);
            return;
        }

        // 先把这个场景里所有单位藏起来
        foreach (var u in _allPlayers) if (u != null) u.gameObject.SetActive(false);
        foreach (var u in _allEnemies) if (u != null) u.gameObject.SetActive(false);

        // 再按关卡摆出来
        ApplySetups(lv.playerUnits, lv.goal, true);
        ApplySetups(lv.enemyUnits, lv.goal, false);

        Current = Phase.PreBattle;
        if (map != null) map.editable = true;

        Debug.Log($"BattleFlow: 载入关卡 {LevelIndex + 1}「{lv.levelName}」  A {lv.playerUnits.Count} 个 / " +
                  $"B {lv.enemyUnits.Count} 个  目标 {lv.goal}", this);

        RecomputeAll();
    }

    void ApplySetups(List<UnitSetup> setups, Vector2Int goal, bool isPlayer)
    {
        if (setups == null) return;

        foreach (UnitSetup s in setups)
        {
            if (s == null || s.unit == null)
            {
                Debug.LogWarning("BattleFlow: 关卡里有一个空的单位槽位，已跳过", this);
                continue;
            }

            PathAgent u = s.unit;

            // 起点 + 目标
            u.SetStartPos(s.cell);

            if (isPlayer)
            {
                // A：用关卡填的目标
                u.intent.target = s.useOwnGoal ? s.ownGoal : goal;
            }
            else
            {
                // B：目标由追击逻辑实时决定。
                // 这里先把目标设成它自己的出发格，赛前算出来就是空路径，
                // 免得画面上留一条用不上的路线
                u.intent.target = s.cell;
            }

            // 可选覆盖
            if (_baseSteps.TryGetValue(u, out double baseSteps))
                u.stepsPerTick = baseSteps;

            if (s.stepsPerTickOverride > 0)
                u.stepsPerTick = s.stepsPerTickOverride;

            UnitHealth hp = u.GetComponent<UnitHealth>();
            if (hp != null)
            {
                if (_baseMaxHp.TryGetValue(hp, out int baseMaxHp)) hp.maxHp = baseMaxHp;
                if (_baseAttack.TryGetValue(hp, out int baseAtk)) hp.attackPower = baseAtk;

                if (s.maxHpOverride > 0) hp.maxHp = s.maxHpOverride;
                if (s.attackPowerOverride >= 0) hp.attackPower = s.attackPowerOverride;

                hp.ResetToFull();
            }

            // 出场时机：AtStart 的直接摆出来，其余先藏好等条件
            s.spawned = s.timing == UnitSpawnTiming.AtStart;

            if (s.spawned)
            {
                u.gameObject.SetActive(true);
                u.ResetToStart();
            }
            else
            {
                u.gameObject.SetActive(false);
            }
            Debug.Log($"BattleFlow: 关卡 {LevelIndex + 1} 配置 {u.name} → 起点 {s.cell} " +
                    $"目标 {u.intent.target} 行动力 {u.stepsPerTick:F2} 出场 {s.timing}", this);
        }
    }

    void CollectAllUnits()
    {
        _allPlayers.Clear();
        _allEnemies.Clear();

        foreach (SecondBoardLevel lv in levels)
        {
            if (lv == null) continue;

            foreach (UnitSetup s in lv.playerUnits)
                if (s != null && s.unit != null && !_allPlayers.Contains(s.unit)) _allPlayers.Add(s.unit);

            foreach (UnitSetup s in lv.enemyUnits)
                if (s != null && s.unit != null && !_allEnemies.Contains(s.unit)) _allEnemies.Add(s.unit);
        }

        if (playerAgent != null && !_allPlayers.Contains(playerAgent)) _allPlayers.Add(playerAgent);
        if (enemyAgent != null && !_allEnemies.Contains(enemyAgent)) _allEnemies.Add(enemyAgent);
    }
    void WarnDuplicates(SecondBoardLevel lv)
    {
        if (lv == null) return;

        var seen = new HashSet<PathAgent>();
        WarnDupList(lv.playerUnits, seen, lv, "A");
        seen.Clear();
        WarnDupList(lv.enemyUnits, seen, lv, "B");

        // 同时出现在两边
        var players = new HashSet<PathAgent>();
        foreach (UnitSetup s in lv.playerUnits)
            if (s != null && s.unit != null) players.Add(s.unit);

        foreach (UnitSetup s in lv.enemyUnits)
        {
            if (s == null || s.unit == null) continue;
            if (!players.Contains(s.unit)) continue;

            Debug.LogError($"BattleFlow: {s.unit.name} 同时出现在关卡「{lv.levelName}」的 A 和 B 列表里" +
                           $" —— 它每回合会行动两次", s.unit);
        }
    }

    void WarnDupList(List<UnitSetup> setups, HashSet<PathAgent> seen,
                     SecondBoardLevel lv, string side)
    {
        if (setups == null) return;

        foreach (UnitSetup s in setups)
        {
            if (s == null || s.unit == null) continue;

            if (!seen.Add(s.unit))
                Debug.LogError($"BattleFlow: {s.unit.name} 在关卡「{lv.levelName}」的 {side} 列表里" +
                               $"出现了两次 —— 它每回合会行动两次", s.unit);
        }
    }

    void CaptureBaseStats()
    {
        foreach (PathAgent u in AllPoolUnits())
        {
            if (u == null) continue;

            if (!_baseSteps.ContainsKey(u)) _baseSteps[u] = u.stepsPerTick;

            UnitHealth hp = u.GetComponent<UnitHealth>();
            if (hp == null) continue;

            if (!_baseMaxHp.ContainsKey(hp))
            {
                _baseMaxHp[hp] = hp.maxHp;
                _baseAttack[hp] = hp.attackPower;
            }
        }
    }

    IEnumerable<PathAgent> AllPoolUnits()
    {
        foreach (PathAgent u in _allPlayers) yield return u;
        foreach (PathAgent u in _allEnemies) yield return u;
    }
    void BuildFallbackLevel()
    {
        var lv = new SecondBoardLevel { levelName = "默认关卡" };

        if (playerAgent != null)
        {
            lv.goal = playerAgent.intent.target;
            lv.playerUnits.Add(new UnitSetup { unit = playerAgent, cell = playerAgent.StartPos });
        }
        if (enemyAgent != null)
            lv.enemyUnits.Add(new UnitSetup { unit = enemyAgent, cell = enemyAgent.StartPos });

        if (lv.playerUnits.Count + lv.enemyUnits.Count > 0) levels.Add(lv);
    }

    // ─────────────────────────────────────────────
    //  当前关卡的成员
    // ─────────────────────────────────────────────
    IEnumerable<PathAgent> LevelPlayers()
    {
        SecondBoardLevel lv = CurrentLevel;
        if (lv == null || lv.playerUnits == null) yield break;

        foreach (UnitSetup s in lv.playerUnits)
            if (s != null && s.unit != null) yield return s.unit;
    }

    public List<PathAgent> GetPlayerUnits()
    {
        var list = new List<PathAgent>();

        SecondBoardLevel lv = CurrentLevel;
        if (lv == null || lv.playerUnits == null) return list;

        foreach (UnitSetup s in lv.playerUnits)
        {
            if (s == null || s.unit == null) continue;
            if (!s.spawned) continue;                  // 还没出场的不算
            if (list.Contains(s.unit)) continue;       // 去重

            list.Add(s.unit);
        }

        return list;
    }

    IEnumerable<PathAgent> LevelEnemies()
    {
        SecondBoardLevel lv = CurrentLevel;
        if (lv == null || lv.enemyUnits == null) yield break;

        foreach (UnitSetup s in lv.enemyUnits)
            if (s != null && s.unit != null) yield return s.unit;
    }

    bool IsPlayerUnit(PathAgent u) => u != null && _allPlayers.Contains(u);

    UnitHealth HealthOf(PathAgent u) => u != null ? u.GetComponent<UnitHealth>() : null;

    static bool AtGoal(PathAgent a) => a.GridPos == a.intent.target;

    // ─────────────────────────────────────────────
    //  赛前 → 交战
    // ─────────────────────────────────────────────
    [ContextMenu("开战")]
    public void StartBattle()
    {
        if (Current == Phase.StageIntro)
        {
            Debug.Log("BattleFlow: 阶段衔接还没结束", this);
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
        _ticks = 0;
        _playerSteps = 0;

        if (map != null) map.editable = false;

        foreach (PathAgent a in LevelPlayers()) a.intent.locked = true;
        foreach (PathAgent b in LevelEnemies()) b.intent.locked = true;

        SecondBoardLevel lv = CurrentLevel;
        Debug.Log($"BattleFlow: 关卡「{(lv != null ? lv.levelName : "?")}」开战 —— A 先行", this);
    }

    // ─────────────────────────────────────────────
    //  交战推进
    // ─────────────────────────────────────────────
    void RunTick()
    {
        // 起手判一次：可能开局就全都在终点了
        if (AllPlayersAtGoal()) { ResolveRound(RoundResult.Success); return; }

        // ══ 所有 A 依次行动 ══
        foreach (PathAgent a in LevelPlayers())
        {
            if (a == null || !a.IsActive) continue;
            if (AtGoal(a)) continue;                    // 已经在终点的跳过

            int steps = a.GetStepsThisTick();

            for (int i = 0; i < steps; i++)
            {
                if (!a.TryStep()) break;

                _playerSteps++;

                HealthOf(a)?.ResolveGroundEffect();
                if (Current != Phase.Battle) return;

                a.ResolveElementEffect();
                if (Current != Phase.Battle) return;

                if (HandleContact(a)) return;
            }
        }

        // A 是不是全部到终点了
        if (AllPlayersAtGoal()) { ResolveRound(RoundResult.Success); return; }

        // ══ 所有 B 依次行动：每一步都朝当时最近的 A 走 ══
        foreach (PathAgent b in LevelEnemies())
        {
            if (b == null || !b.IsActive) continue;

            int steps = b.GetStepsThisTick();

            for (int i = 0; i < steps; i++)
            {
                PathAgent target = FindNearestPlayer(b);
                if (target == null) break;                  // 场上没有 A 可追

                b.RetargetTo(target.GridPos);

                if (!b.TryStep()) break;

                if (HandleContact(b)) return;
            }
        }

        // ══ 兜底：全都停下了，但 A 没有全部到达 ══
        if (!AnyUnitHasPath())
        {
            Debug.Log("BattleFlow: 双方都无法继续前进，而 A 没有全部到达终点", this);
            ResolveRound(RoundResult.Fail);
        }
    }

    bool AnyUnitHasPath()
    {
        foreach (PathAgent a in LevelPlayers()) if (a != null && a.IsActive && a.HasPath) return true;
        foreach (PathAgent b in LevelEnemies()) if (b != null && b.IsActive && b.HasPath) return true;
        return false;
    }

    /// <summary>找出离 from 最近的、还在场的 A。用的是格子距离。</summary>
    PathAgent FindNearestPlayer(PathAgent from)
    {
        if (from == null) return null;

        PathAgent best = null;
        int bestDist = int.MaxValue;

        foreach (PathAgent a in LevelPlayers())
        {
            if (a == null || !a.IsActive) continue;

            int d = Mathf.Abs(a.GridPos.x - from.GridPos.x) +
                    Mathf.Abs(a.GridPos.y - from.GridPos.y);

            if (d < bestDist)
            {
                bestDist = d;
                best = a;
            }
        }

        return best;
    }
    bool AllPlayersAtGoal()
    {
        bool anyActive = false;

        foreach (PathAgent a in LevelPlayers())
        {
            if (a == null || !a.IsActive) continue;     // 退场的不算拦路

            anyActive = true;
            if (!AtGoal(a)) return false;
        }

        return anyActive;
    }

    /// <summary>检查 unit 有没有和敌对单位撞上。返回 true 表示本 tick 结束。</summary>
    bool HandleContact(PathAgent unit)
    {
        if (unit == null || !unit.IsActive) return false;

        bool unitIsPlayer = IsPlayerUnit(unit);

        IEnumerable<PathAgent> opponents = unitIsPlayer ? LevelEnemies() : LevelPlayers();

        foreach (PathAgent other in opponents)
        {
            if (other == null || !other.IsActive) continue;
            if (other.GridPos != unit.GridPos) continue;

            // 不管谁撞谁，都由 B 造成伤害
            PathAgent attacker = unitIsPlayer ? other : unit;
            PathAgent victim = unitIsPlayer ? unit : other;

            UnitHealth attackerHp = HealthOf(attacker);
            int damage = attackerHp != null ? attackerHp.attackPower : enemyContactDamage;

            Debug.Log($"BattleFlow: {attacker.name} 撞上 {victim.name}，造成 {damage} 点伤害", this);

            HealthOf(victim)?.TakeDamage(damage, "被敌方单位撞击");

            attacker.Despawn();
            Debug.Log($"BattleFlow: {attacker.name} 撞击后消失", this);

            return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────
    //  阵亡
    // ─────────────────────────────────────────────
    void SubscribeDeaths()
    {
        foreach (PathAgent u in _allPlayers)
        {
            UnitHealth hp = HealthOf(u);
            if (hp != null) hp.OnDied += HandleUnitDied;
        }
        foreach (PathAgent u in _allEnemies)
        {
            UnitHealth hp = HealthOf(u);
            if (hp != null) hp.OnDied += HandleUnitDied;
        }
    }

    void UnsubscribeDeaths()
    {
        foreach (PathAgent u in _allPlayers)
        {
            UnitHealth hp = HealthOf(u);
            if (hp != null) hp.OnDied -= HandleUnitDied;
        }
        foreach (PathAgent u in _allEnemies)
        {
            UnitHealth hp = HealthOf(u);
            if (hp != null) hp.OnDied -= HandleUnitDied;
        }
    }

    void HandleUnitDied(UnitHealth hp)
    {
        if (Current != Phase.Battle) return;

        PathAgent unit = hp != null ? hp.agent : null;
        if (unit != null) unit.Despawn();

        // A 全灭 = 失败
        if (IsPlayerUnit(unit) && !AnyPlayerAlive())
        {
            Debug.Log("BattleFlow: A 已全部阵亡", this);
            ResolveRound(RoundResult.Fail);
        }
    }

    bool AnyPlayerAlive()
    {
        bool any = false;

        foreach (PathAgent a in LevelPlayers())
        {
            if (a == null) continue;
            any = true;

            UnitHealth hp = HealthOf(a);
            if (hp == null || !hp.IsDead) return true;
        }

        return !any;   // 一个 A 都没配的话，不算全灭
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

    void ShowResult(RoundResult r)
    {
        Debug.Log(r == RoundResult.Success
            ? $"BattleFlow: 结算 —— 成功（A 先到终点）"
            : $"BattleFlow: 结算 —— 失败", this);
    }

    void SyncResultPanels()
    {
        bool showSuccess = Current == Phase.Resolved && LastResult == RoundResult.Success;
        bool showFail = Current == Phase.Resolved && LastResult == RoundResult.Fail;

        if (successPanel != null && successPanel.activeSelf != showSuccess)
            successPanel.SetActive(showSuccess);

        if (failPanel != null && failPanel.activeSelf != showFail)
            failPanel.SetActive(showFail);
    }

    void HideResultPanels()
    {
        if (successPanel != null) successPanel.SetActive(false);
        if (failPanel != null) failPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  结算 → 下一关
    // ─────────────────────────────────────────────
    [ContextMenu("继续（下一关）")]
    public void ContinueToNextStage()
    {
        if (Current != Phase.Resolved) return;
        if (LastResult != RoundResult.Success) return;

        HideResultPanels();
        LastResult = RoundResult.None;

        // 每个"活着走完这一局"的 B，在它停下的位置留一个营地
        int built = 0;

        foreach (PathAgent b in LevelEnemies())
        {
            if (b == null || !b.IsActive) continue;

            Vector2Int c = b.GridPos;
            UnitHealth hp = HealthOf(b);
            int damage = hp != null ? hp.attackPower : (map != null ? map.defaultCampDamage : 20);

            if (map != null && map.AddCamp(c, damage))
            {
                built++;
                Debug.Log($"BattleFlow: {b.name} 在 {c} 留下营地，伤害 {damage}", this);
            }
        }

        if (built > 0 && map != null)
            Debug.Log($"BattleFlow: 本回合共留下 {built} 个营地（场上共 {map.CampCount()} 个）", this);

        RoundIndex++;

        // 全部归位（退场的会重新出现）
        foreach (PathAgent u in _allPlayers) if (u != null) u.ResetToStart();
        foreach (PathAgent u in _allEnemies) if (u != null) u.ResetToStart();

        // 血量回满
        foreach (PathAgent u in _allPlayers)
        {
            UnitHealth hp = HealthOf(u);
            if (hp != null) hp.ResetToFull();
        }
        foreach (PathAgent u in _allEnemies)
        {
            UnitHealth hp = HealthOf(u);
            if (hp != null) hp.ResetToFull();
        }

        Current = Phase.StageIntro;
        if (map != null) map.editable = true;
        RecomputeAll();

        Debug.Log($"BattleFlow: 进入第 {RoundIndex + 1} 章", this);
    }

    [ContextMenu("结束阶段衔接")]
    public void ProceedToPreBattle()
    {
        if (Current != Phase.StageIntro)
        {
            Debug.Log($"BattleFlow: 当前是 {Current}，不在阶段衔接中", this);
            return;
        }

        Current = Phase.PreBattle;
        if (map != null) map.editable = true;
        RecomputeAll();

        Debug.Log("BattleFlow: 阶段就绪，可以规划并开战", this);
    }

    // ─────────────────────────────────────────────
    //  重置
    // ─────────────────────────────────────────────
    [ContextMenu("重置本关")]
    public void ResetThisLevel()
    {
        HideResultPanels();
        LastResult = RoundResult.None;

        ApplyLevel(LevelIndex);

        Debug.Log("BattleFlow: 已重置本关（营地保留）", this);
    }

    [ContextMenu("完全重置（回到最初）")]
    public void ResetEverything()
    {
        HideResultPanels();
        LastResult = RoundResult.None;
        RoundIndex = 0;

        if (map != null)
        {
            map.ClearCamps();
            map.ClearSpecials();
            map.editable = true;
            map.RestoreSnapshot(_battleStartSnapshot);
        }

        LevelIndex = 0;
        ApplyLevel(0);

        Debug.Log("BattleFlow: 已完全重置回最初状态", this);
    }

    // ─────────────────────────────────────────────
    //  内部
    // ─────────────────────────────────────────────
    // ─────────────────────────────────────────────
    //  增援出场
    // ─────────────────────────────────────────────
    void TryActivatePending()
    {
        if (Current != Phase.Battle) return;

        SecondBoardLevel lv = CurrentLevel;
        if (lv == null) return;

        TryActivateList(lv.playerUnits);
        TryActivateList(lv.enemyUnits);
    }

    void TryActivateList(List<UnitSetup> setups)
    {
        if (setups == null) return;

        foreach (UnitSetup s in setups)
        {
            if (s == null || s.unit == null) continue;
            if (s.spawned) continue;                 // 已经出过场了
            if (!IsDue(s)) continue;

            ActivateUnit(s);
        }
    }

    bool IsDue(UnitSetup s)
    {
        switch (s.timing)
        {
            case UnitSpawnTiming.AfterTicks:
                return _ticks >= s.spawnAfterTicks;

            case UnitSpawnTiming.AfterPlayerSteps:
                return _playerSteps >= s.spawnAfterPlayerSteps;

            default:
                return false;                        // AtStart 的由 ApplySetups 处理
        }
    }

    void ActivateUnit(UnitSetup s)
    {
        PathAgent u = s.unit;

        s.spawned = true;

        u.gameObject.SetActive(true);
        u.ResetToStart();          // 回到出发格、解锁、清路径
        u.Recompute();             // 用当前地图重算路径
        u.intent.locked = true;    // 立刻锁住，之后不再变

        Debug.Log($"BattleFlow: 增援出场 —— {u.name} 在 {s.cell}" +
                  $"（回合 {_ticks}，A 步数 {_playerSteps}）", this);
    }
    void RecomputeAll()
    {
        if (!MapUnlocked) return;

        // 同一目标的单位先算的先占位；现在单位互不阻挡，顺序不影响结果
        foreach (PathAgent a in LevelPlayers()) a?.Recompute();
        foreach (PathAgent b in LevelEnemies()) b?.Recompute();
    }

    [ContextMenu("自检")]
    public void Validate()
    {
        if (map == null) Debug.LogError("BattleFlow: map 未指定", this);

        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("BattleFlow: levels 是空的，会用兜底单位建一个默认关卡", this);
        }

        if (successPanel == null && failPanel == null)
            Debug.LogWarning("BattleFlow: Success Panel / Fail Panel 都没指定 —— 结算时不会有按钮出现", this);

        foreach (PathAgent u in _allPlayers)
        {
            if (u == null) continue;
            if (u.map == null) Debug.LogError($"BattleFlow: {u.name} 的 PathAgent.map 未指定", u);
            if (u.GetComponent<UnitView>() == null) Debug.LogWarning($"BattleFlow: {u.name} 没有 UnitView", u);
            if (u.GetComponent<PathRenderer>() == null) Debug.LogWarning($"BattleFlow: {u.name} 没有 PathRenderer", u);
        }
    }

    [ContextMenu("输出摘要")]
    public void LogSummary()
    {
        if (map == null) { Debug.Log("map 未指定", this); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"[自检] 阶段={Current}  关卡={LevelIndex + 1}/{levels.Count}  营地={map.CampCount()}个");

        SecondBoardLevel lv = CurrentLevel;
        if (lv != null)
        {
            sb.AppendLine($"        关卡名={lv.levelName}  统一目标={lv.goal}");

            foreach (UnitSetup s in lv.playerUnits)
                if (s != null && s.unit != null)
                    sb.AppendLine($"        A {s.unit.name}: 起点={s.cell} " +
                                  $"目标={(s.useOwnGoal ? s.ownGoal.ToString() : "（统一）")} " +
                                  $"HP={HpText(s.unit)}");

            foreach (UnitSetup s in lv.enemyUnits)
                if (s != null && s.unit != null)
                    sb.AppendLine($"        B {s.unit.name}: 起点={s.cell} " +
                                  $"目标={(s.useOwnGoal ? s.ownGoal.ToString() : "（统一）")} " +
                                  $"HP={HpText(s.unit)}");
        }

        Debug.Log(sb.ToString(), this);
    }

    [ContextMenu("清点场景里的单位")]
    public void LogAllSceneUnits()
    {
        PathAgent[] all = FindObjectsOfType<PathAgent>(true);

        var sb = new StringBuilder();
        sb.AppendLine($"[场景单位] 一共找到 {all.Length} 个 PathAgent");
        sb.AppendLine($"           已收集进单位池：A {_allPlayers.Count} 个 / B {_allEnemies.Count} 个");

        foreach (PathAgent u in all)
        {
            bool inPool = _allPlayers.Contains(u) || _allEnemies.Contains(u);
            sb.AppendLine($"   {u.name}   在单位池里={inPool}   当前激活={u.gameObject.activeSelf}");
        }

        foreach (PathAgent u in all)
        {
            if (_allPlayers.Contains(u) || _allEnemies.Contains(u)) continue;
            sb.AppendLine($"   ⚠ {u.name} 不在任何关卡的列表里 —— 它永远不会出场");
        }

        Debug.Log(sb.ToString(), this);
    }

    static string HpText(PathAgent u)
    {
        UnitHealth hp = u != null ? u.GetComponent<UnitHealth>() : null;
        return hp != null ? $"{hp.Hp}/{hp.maxHp} 攻击 {hp.attackPower}" : "-";
    }
}
