using UnityEngine;
using TMPro;

namespace Assets.Scripts.UI
{
    public class PointPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text pointText;

        private float _remainTime;
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

        /// <summary>
        /// 换关时自动重读当前关卡配置
        /// </summary>
        private void OnLevelChanged(LevelConfig level)
        {
            _remainTime = level.TimeLimit;
            UpdateTimeText();
        }

        private void Start()
        {
            // LevelManager 启动时已经广播过 LevelChanged，这里兜底读一次，
            // 防止本面板比广播晚激活而错过
            if (LevelManager.Instance.CurrentLevel != null)
            {
                _remainTime = LevelManager.Instance.CurrentLevel.TimeLimit;
            }

            //TODO:这里要做得分丢分逻辑
            pointText.text = $"{_point}";
            isCounting = GameManager.Instance.CurrentState == GameState.Playing;
            UpdateTimeText();
        }

        private void Update()
        {
            if (!isCounting)
                return;
            
            //TODO：时间到底时会结束游戏，这里要做逻辑
            _remainTime -= Time.deltaTime;  

            if (_remainTime <= 0f)
            {
                _remainTime = 0f;
                isCounting = false;
                OnTimeUp();
            }

            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            int totalSeconds = Mathf.CeilToInt(_remainTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            // "xx:xx"，不足两位补零
            timeText.text = $"{minutes:00}:{seconds:00}";
        }

        private void OnTimeUp()
        {
            // TODO:时间到：先留空或打个日志，以后接失败流程
            Debug.Log("时间到");
        }
    }
}
