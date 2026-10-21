using UnityEngine;
using TMPro;

/// 显示第二棋盘的回合数。
public class TurnCounterUI : MonoBehaviour
{
    [Header("引用")]
    public BattleFlow battleFlow;
    public TMP_Text text;

    [Header("显示")]
    public string prefix = "回合 ";

    int _shown = -1;

    void Start()
    {
        Refresh(true);
    }

    void Update()
    {
        Refresh(false);
    }

    void Refresh(bool force)
    {
        if (battleFlow == null || text == null) return;

        if (!force && battleFlow.TurnCount == _shown) return;

        _shown = battleFlow.TurnCount;
        text.text = prefix + _shown;
    }
}
