using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Board
{
    /// <summary>
    /// 快速配置工具。一键创建完整的棋盘系统。
    /// </summary>
    public class BoardQuickSetup : MonoBehaviour
    {
#if UNITY_EDITOR
        [MenuItem("GameObject/Board/Create Board System", false, 10)]
        private static void CreateBoardSystem()
        {
            // 创建主对象
            GameObject boardManager = new GameObject("BoardManager");
            boardManager.transform.position = Vector3.zero;

            // 添加核心组件
            var controller = boardManager.AddComponent<BoardController>();
            var mapper = boardManager.AddComponent<BoardWorldMapper>();
            var inputController = boardManager.AddComponent<BoardInputController>();
            var view = boardManager.AddComponent<BoardView>();

            // 添加扩展组件
            boardManager.AddComponent<BoardGameManager>();
            boardManager.AddComponent<BoardSelectionHighlight>();
            boardManager.AddComponent<BoardDebugger>();

            // 创建子对象用于组织
            GameObject pieceRoot = new GameObject("Pieces");
            pieceRoot.transform.SetParent(boardManager.transform);
            pieceRoot.transform.localPosition = Vector3.zero;

            GameObject obstacleRoot = new GameObject("Obstacles");
            obstacleRoot.transform.SetParent(boardManager.transform);
            obstacleRoot.transform.localPosition = Vector3.zero;

            // 查找或创建配置资源
            string configPath = "Assets/BoardStageConfig_Default.asset";
            string profilePath = "Assets/SpawnProfile_Default.asset";

            BoardStageConfig config = AssetDatabase.LoadAssetAtPath<BoardStageConfig>(configPath);
            SpawnProfile profile = AssetDatabase.LoadAssetAtPath<SpawnProfile>(profilePath);

            // 如果没有配置资源，创建默认的
            if (config == null)
            {
                Debug.LogWarning("未找到 BoardStageConfig_Default，请手动创建：右键 → Create → Game/Board/Stage Config");
            }

            if (profile == null)
            {
                Debug.LogWarning("未找到 SpawnProfile_Default，请手动创建：右键 → Create → Game/Board/Spawn Profile");
            }

            // 选中创建的对象
            Selection.activeGameObject = boardManager;

            // 标记场景为已修改
            EditorUtility.SetDirty(boardManager);

            Debug.Log("棋盘系统已创建！请配置以下内容：\n" +
                     "1. 创建 BoardStageConfig_Default 和 SpawnProfile_Default 资源\n" +
                     "2. 在 BoardController 中关联配置资源\n" +
                     "3. 创建并关联棋子和障碍物预制体\n" +
                     "4. 设置 Main Camera");
        }

        [MenuItem("GameObject/Board/Create Simple Piece Prefab", false, 11)]
        private static void CreateSimplePiecePrefab()
        {
            // 创建简单的立方体棋子
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "SimplePiece";
            piece.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);

            // 添加可视化组件
            piece.AddComponent<PieceVisualizer>();

            // 保存为预制体
            string path = "Assets/SimplePiece.prefab";
            PrefabUtility.SaveAsPrefabAsset(piece, path);
            DestroyImmediate(piece);

            Debug.Log($"简单棋子预制体已创建：{path}");

            // 选中预制体
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        [MenuItem("GameObject/Board/Create Simple Obstacle Prefab", false, 12)]
        private static void CreateSimpleObstaclePrefab()
        {
            // 创建简单的立方体障碍
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "SimpleObstacle";
            obstacle.transform.localScale = new Vector3(1f, 0.5f, 1f);

            // 设置深色材质
            var renderer = obstacle.GetComponent<Renderer>();
            renderer.material.color = new Color(0.3f, 0.3f, 0.3f);

            // 保存为预制体
            string path = "Assets/SimpleObstacle.prefab";
            PrefabUtility.SaveAsPrefabAsset(obstacle, path);
            DestroyImmediate(obstacle);

            Debug.Log($"简单障碍物预制体已创建：{path}");

            // 选中预制体
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
    }
}
