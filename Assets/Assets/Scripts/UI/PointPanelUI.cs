using UnityEngine;
using TMPro;

namespace Assets.Scripts.UI
{
    public class PointPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text pointText;

        private float _elapsed;             // 已经过去多少秒
        private float _limit = 600f;        // 关卡时限。到了就触发 OnTimeUp
        private float _point = 0;
        private bool isCounting;

        private void OnEnable()
        {
            GameEventCenter.Instance.GameStateChanged += OnGameStateChanged;
            GameEventCenter.Instance.LevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            if (GameEventCenter.Instance == null)
                return;

            GameEventCenter.Instance.GameStateChanged -= OnGameStateChanged;
            GameEventCenter.Instance.LevelChanged -= OnLevelChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            isCounting = state == GameState.Playing;
        }

        /// <summary>换关时重新开始计时</summary>
        private void OnLevelChanged(LevelConfig level)
        {
            if (level == null) return;

            _limit = level.TimeLimit;
            _elapsed = 0f;
            UpdateTimeText();
        }

        private void Start()
        {
            // LevelManager 启动时已经广播过 LevelChanged，这里兜底读一次，
            // 防止本面板比广播晚激活而错过
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
                _limit = LevelManager.Instance.CurrentLevel.TimeLimit;

            _elapsed = 0f;

            //TODO:这里要做得分丢分逻辑
            pointText.text = $"{_point}";

            isCounting = GameManager.Instance.CurrentState == GameState.Playing;
            UpdateTimeText();
        }

        private void Update()
        {
            if (!isCounting) return;

            //TODO：时间到底时会结束游戏，这里要做逻辑
            _elapsed += Time.deltaTime;

            if (_elapsed >= _limit)
            {
                _elapsed = _limit;
                isCounting = false;
                OnTimeUp();
            }

            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            int totalSeconds = Mathf.FloorToInt(_elapsed);   // 往上计时用 Floor
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            timeText.text = $"{minutes:00}:{seconds:00}";
        }

        private void OnTimeUp()
        {
            // TODO:时间到：先留空或打个日志，以后接失败流程
            Debug.Log("时间到");
        }
    }
}