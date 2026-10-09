using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Board
{
    /// <summary>
    /// 棋盘编辑器扩展工具。提供一键设置功能。
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(BoardController))]
    public class BoardControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BoardController controller = (BoardController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);

            if (GUILayout.Button("初始化棋盘"))
            {
                if (Application.isPlaying)
                {
                    string error;
                    if (controller.TryInitialize(out error))
                    {
                        Debug.Log("棋盘初始化成功");
                    }
                    else
                    {
                        Debug.LogError(error);
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请在运行时使用此功能", "确定");
                }
            }

            if (GUILayout.Button("手动结束阶段"))
            {
                if (Application.isPlaying)
                {
                    if (controller.TryEndManually())
                    {
                        Debug.Log("阶段已手动结束");
                    }
                    else
                    {
                        Debug.LogWarning("当前无法手动结束");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请在运行时使用此功能", "确定");
                }
            }

            if (Application.isPlaying && controller.Board != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("棋盘状态", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"尺寸: {controller.Board.Width} × {controller.Board.Height}");

                if (controller.Session != null)
                {
                    EditorGUILayout.LabelField($"阶段: {controller.Session.Phase}");
                    EditorGUILayout.LabelField($"步数: {controller.Session.RemainingSteps}/{controller.Session.InitialSteps}");
                }
            }
        }
    }
#endif
}
