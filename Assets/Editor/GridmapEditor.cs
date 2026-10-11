 using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Gridmap))]
public sealed class GridmapEditor : UnityEditor.Editor
{
    private TileType brush = TileType.Wall;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var map = (Gridmap)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("格子绘制", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "左键点格子 → 刷成笔刷类型。\n" +
            "右键点格子 → 擦成地面。\n" +
            "改动直接写进场景数据，不需要运行游戏。",
            MessageType.Info);

        brush = (TileType)EditorGUILayout.EnumPopup("笔刷", brush);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全填地面")) FillAll(map, TileType.Ground);
        if (GUILayout.Button("全填墙壁")) FillAll(map, TileType.Wall);
        if (GUILayout.Button("围一圈边框")) Border(map);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        float cell = Mathf.Clamp(
            (EditorGUIUtility.currentViewWidth - 40f) / Mathf.Max(1, map.width), 8f, 26f);

        // 屏幕上方 = 地图上方（y 大的在上面）
        for (int y = map.height - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int x = 0; x < map.width; x++)
            {
                var c = new Vector2Int(x, y);
                Rect r = GUILayoutUtility.GetRect(cell, cell);

                TileType t = map.Get(c);
                Color fill = t == TileType.Wall
                    ? new Color(0.40f, 0.43f, 0.50f)
                    : new Color(0.80f, 0.84f, 0.90f);

                EditorGUI.DrawRect(r, fill);
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), new Color(0.25f, 0.27f, 0.32f));
                EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), new Color(0.25f, 0.27f, 0.32f));

                if (Event.current.type == EventType.MouseDown &&
                    r.Contains(Event.current.mousePosition))
                {
                    Paint(map, c, Event.current.button == 1 ? TileType.Ground : brush);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void Paint(Gridmap map, Vector2Int c, TileType t)
    {
        Undo.RecordObject(map, "绘制格子");
        map.EditorSetTile(c, t);
        EditorUtility.SetDirty(map);
    }

    private static void FillAll(Gridmap map, TileType t)
    {
        Undo.RecordObject(map, "填充格子");
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                map.EditorSetTile(new Vector2Int(x, y), t);
        EditorUtility.SetDirty(map);
    }

    private static void Border(Gridmap map)
    {
        Undo.RecordObject(map, "围边框");
        for (int x = 0; x < map.width; x++)
        {
            map.EditorSetTile(new Vector2Int(x, 0), TileType.Wall);
            map.EditorSetTile(new Vector2Int(x, map.height - 1), TileType.Wall);
        }
        for (int y = 0; y < map.height; y++)
        {
            map.EditorSetTile(new Vector2Int(0, y), TileType.Wall);
            map.EditorSetTile(new Vector2Int(map.width - 1, y), TileType.Wall);
        }
        EditorUtility.SetDirty(map);
    }
}
