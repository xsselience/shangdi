using UnityEngine;
using TMPro;

namespace Assets.Scripts.UI
{
    public class ChessBoardUI : MonoBehaviour
    {
        [Header("关卡信息文本")]
        [SerializeField] private TMP_Text levelIdText;
        [SerializeField] private TMP_Text stepsText;

        [Header("棋盘生成")]
        [SerializeField] private GameObject cellPrefab;       // 格子预制体
        [SerializeField] private RectTransform gridRoot;      // 格子容器（挂 GridLayoutGroup）

        private void OnEnable()
        {
            GameEventCenter.Instance.LevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            // 退出 Play 时事件中心可能先被销毁，判空防止 OnDisable 报错
            if (GameEventCenter.Instance == null)
                return;

            GameEventCenter.Instance.LevelChanged -= OnLevelChanged;
        }

        /// <summary>
        /// 换关时自动刷新：文本 + 棋盘
        /// </summary>
        private void OnLevelChanged(LevelConfig level)
        {
            RefreshLevelInfo(level);
            BuildBoard(level);
        }

        private void Start()
        {
            if (LevelManager.Instance.CurrentLevel != null)
            {
                RefreshLevelInfo(LevelManager.Instance.CurrentLevel);
                BuildBoard(LevelManager.Instance.CurrentLevel);
            }
        }

        /// <summary>
        /// 关卡信息 → 文本
        /// </summary>
        private void RefreshLevelInfo(LevelConfig level)
        {
            levelIdText.text = $"关卡:{level.LevelId}";
            stepsText.text = $"剩余步数:{level.InitSteps}";
        }

        /// <summary>
        /// 按棋盘配置生成格子
        /// 关卡配置里有 boardId → 加载对应 BoardConfig → 按宽高铺格子
        /// </summary>
        private void BuildBoard(LevelConfig level)
        {
            var boardConfig = Resources.Load<BoardConfig>($"Configs/Boards/BoardConfig_{level.BoardId}");
            if (boardConfig == null)
            {
                Debug.LogError($"找不到棋盘配置：BoardConfig_{level.BoardId}，检查是否已导表");
                return;
            }

            // 重新生成前清掉旧格子
            foreach (Transform child in gridRoot)
            {
                Destroy(child.gameObject);
            }

            // 容器上的 GridLayoutGroup 按列数自动排布
            var gridLayout = gridRoot.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            for (int y = 0; y < boardConfig.Height; y++)
            {
                for (int x = 0; x < boardConfig.Width; x++)
                {
                    //TODO：要设计不同属性的棋子prefab，然后根据配置生成
                    Instantiate(cellPrefab, gridRoot);
                }
            }
        }
        
        public void OnClickShowRule()
        {
            GameEventCenter.Instance.RequestPauseGame();
        }
    }
}
