using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 配置表导入器：一次导入 xlsx 里的全部 sheet
///
/// 表文件：Assets/Assets/Tables/棋盘系统表.xlsx
/// 输出目录：Assets/Assets/Configs/
///   Boards/     BoardConfig_棋盘ID       （棋盘表，每行一个资产）
///   CellTypes/  CellTypeConfig_类型ID    （格子类型表，每行一个资产）
///   Cells/      BoardCellTable           （棋盘格子表，整表一个资产）
///   Levels/     LevelConfig_关卡ID       （关卡表，每行一个资产）
///   Zones/      RefreshZoneConfig_区域ID （刷新区域表，每行一个资产，
///                                          元素权重表按 zone_id 合并进 Weights）
///
/// 每张 sheet 的格式约定：
///   第1行 中文说明（跳过）
///   第2行 字段名（board_id, board_name, ...）
///   第3行 类型（跳过）
///   第4行起 数据
///
/// 用法：Unity 菜单 ShangDi → 导入全部配置表
/// </summary>
public static class GameTableImporter
{
    private const string TablePath = "Assets/Assets/Tables/棋盘系统表.xlsx";
    // 放在 Resources 下，运行时才能用 Resources.Load 按关卡 id 加载
    private const string OutputRoot = "Assets/Assets/Resources/Configs";

    [MenuItem("ShangDi/导入全部配置表")]
    public static void Import()
    {
        if (!File.Exists(TablePath))
        {
            Debug.LogError($"找不到表文件：{TablePath}");
            return;
        }

        var sharedStrings = new List<string>();
        // sheet序号 → (行数据, 字段名→列字母)
        var sheets = new Dictionary<int, SheetData>();

        // xlsx 本质是 zip 包，解出 XML 直接读，不需要第三方库
        using (var zip = ZipFile.OpenRead(TablePath))
        {
            sharedStrings = ReadSharedStrings(zip);

            for (int i = 1; i <= 6; i++)
            {
                sheets[i] = ReadSheet(zip, $"xl/worksheets/sheet{i}.xml", sharedStrings);
            }
        }

        Directory.CreateDirectory(OutputRoot);

        ImportBoards(sheets[1]);
        ImportCellTypes(sheets[2]);
        ImportBoardCells(sheets[3]);
        ImportLevels(sheets[4]);
        ImportZones(sheets[5], sheets[6]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"配置表全部导入完成 → {OutputRoot}");
    }

    // =========================
    // 各表导入
    // =========================

    private static void ImportBoards(SheetData sheet)
    {
        string dir = $"{OutputRoot}/Boards";
        Directory.CreateDirectory(dir);

        foreach (var row in sheet.DataRows)
        {
            string id = Get(row, sheet, "board_id");
            if (string.IsNullOrEmpty(id))
                continue;

            var config = LoadOrCreate<BoardConfig>($"{dir}/BoardConfig_{id}.asset");

            config.SetData(
                boardId: GetInt(row, sheet, "board_id"),
                boardName: Get(row, sheet, "board_name"),
                width: GetInt(row, sheet, "width"),
                height: GetInt(row, sheet, "height"),
                remark: Get(row, sheet, "remark"));

            Save(config);
        }
    }

    private static void ImportCellTypes(SheetData sheet)
    {
        string dir = $"{OutputRoot}/CellTypes";
        Directory.CreateDirectory(dir);

        foreach (var row in sheet.DataRows)
        {
            string id = Get(row, sheet, "cell_type_id");
            if (string.IsNullOrEmpty(id))
                continue;

            var config = LoadOrCreate<CellTypeConfig>($"{dir}/CellTypeConfig_{id}.asset");

            config.SetData(
                cellTypeId: int.Parse(id),
                cellTypeName: Get(row, sheet, "cell_type_name"),
                cellTypeKey: Get(row, sheet, "cell_type_key"),
                operable: Get(row, sheet, "operable") == "1",
                remark: Get(row, sheet, "remark"));

            Save(config);
        }
    }

    private static void ImportBoardCells(SheetData sheet)
    {
        string dir = $"{OutputRoot}/Cells";
        Directory.CreateDirectory(dir);

        // 棋盘格子有上百行，每行一个资产太碎，整表装进一个资产
        var cells = new List<BoardCellEntry>();
        foreach (var row in sheet.DataRows)
        {
            string boardId = Get(row, sheet, "board_id");
            if (string.IsNullOrEmpty(boardId))
                continue;

            var entry = new BoardCellEntry();
            entry.SetData(
                boardId: int.Parse(boardId),
                posX: GetInt(row, sheet, "pos_x"),
                posY: GetInt(row, sheet, "pos_y"),
                cellType: GetInt(row, sheet, "cell_type"));
            cells.Add(entry);
        }

        var config = LoadOrCreate<BoardCellTableConfig>($"{dir}/BoardCellTable.asset");
        config.SetData(cells);
        Save(config);
        Debug.Log($"棋盘格子表导入 {cells.Count} 格");
    }

    private static void ImportLevels(SheetData sheet)
    {
        string dir = $"{OutputRoot}/Levels";
        Directory.CreateDirectory(dir);

        foreach (var row in sheet.DataRows)
        {
            string id = Get(row, sheet, "level_id");
            if (string.IsNullOrEmpty(id))
                continue;

            var config = LoadOrCreate<LevelConfig>($"{dir}/LevelConfig_{id}.asset");

            config.SetData(
                levelId: int.Parse(id),
                levelOrder: GetInt(row, sheet, "level_order"),
                levelName: Get(row, sheet, "level_name"),
                boardId: GetInt(row, sheet, "board_id"),
                initSteps: GetInt(row, sheet, "init_steps"),
                initPieceCount: GetInt(row, sheet, "init_piece_count"),
                taskType: Get(row, sheet, "task_type"),
                taskDesc: Get(row, sheet, "task_desc"),
                taskPreview: Get(row, sheet, "task_preview"),
                remark: Get(row, sheet, "remark"));

            Save(config);
        }
    }

    private static void ImportZones(SheetData zoneSheet, SheetData weightSheet)
    {
        string dir = $"{OutputRoot}/Zones";
        Directory.CreateDirectory(dir);

        // 先把权重表按 zone_id 分组
        var weightsByZone = new Dictionary<int, List<ZoneElemWeightEntry>>();
        foreach (var row in weightSheet.DataRows)
        {
            string zoneId = Get(row, weightSheet, "zone_id");
            if (string.IsNullOrEmpty(zoneId))
                continue;

            var entry = new ZoneElemWeightEntry();
            entry.SetData(
                elemId: GetInt(row, weightSheet, "elem_id"),
                weight: GetInt(row, weightSheet, "weight"));

            int key = int.Parse(zoneId);
            if (!weightsByZone.TryGetValue(key, out var list))
            {
                list = new List<ZoneElemWeightEntry>();
                weightsByZone[key] = list;
            }
            list.Add(entry);
        }

        foreach (var row in zoneSheet.DataRows)
        {
            string id = Get(row, zoneSheet, "zone_id");
            if (string.IsNullOrEmpty(id))
                continue;

            int zoneId = int.Parse(id);
            var config = LoadOrCreate<RefreshZoneConfig>($"{dir}/RefreshZoneConfig_{id}.asset");

            config.SetData(
                zoneId: zoneId,
                levelId: GetInt(row, zoneSheet, "level_id"),
                zoneName: Get(row, zoneSheet, "zone_name"),
                rectX1: GetInt(row, zoneSheet, "rect_x1"),
                rectY1: GetInt(row, zoneSheet, "rect_y1"),
                rectX2: GetInt(row, zoneSheet, "rect_x2"),
                rectY2: GetInt(row, zoneSheet, "rect_y2"),
                priority: GetInt(row, zoneSheet, "priority"),
                stateCondition: Get(row, zoneSheet, "state_condition"),
                remark: Get(row, zoneSheet, "remark"),
                weights: weightsByZone.TryGetValue(zoneId, out var weights)
                    ? weights
                    : new List<ZoneElemWeightEntry>());

            Save(config);
        }
    }

    // =========================
    // 资产读写小工具
    // =========================

    /// <summary>有就加载，没有就新建——反复导表不会产生重复资产</summary>
    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        var config = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(config, assetPath);
        }
        return config;
    }

    private static void Save(ScriptableObject config)
    {
        EditorUtility.SetDirty(config);
    }

    // =========================
    // xlsx 解析（zip + XML）
    // =========================

    private class SheetData
    {
        // 字段名 → 列字母（第2行解析而来）
        public Dictionary<string, string> Headers = new Dictionary<string, string>();
        // 第4行起，每行：列字母 → 值
        public List<Dictionary<string, string>> DataRows = new List<Dictionary<string, string>>();
    }

    /// <summary>
    /// 读 xl/sharedStrings.xml：所有字符串单元格共享一张表，按索引取
    /// </summary>
    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var result = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return result;

        using (var stream = entry.Open())
        {
            var doc = new XmlDocument();
            doc.Load(stream);

            // xlsx 的 XML 带默认命名空间，XPath 必须挂命名空间管理器才能匹配到节点
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

            // 每个 <si> 里可能有多个 <t>（富文本分片），拼起来
            foreach (XmlNode si in doc.SelectNodes("//x:si", ns))
            {
                var sb = new StringBuilder();
                foreach (XmlNode t in si.SelectNodes(".//x:t", ns))
                    sb.Append(t.InnerText);
                result.Add(sb.ToString());
            }
        }

        return result;
    }

    /// <summary>
    /// 读一张工作表
    /// 第2行解析成字段名→列字母的映射，第4行起收进 DataRows
    /// </summary>
    private static SheetData ReadSheet(ZipArchive zip, string entryPath, List<string> sharedStrings)
    {
        var sheet = new SheetData();
        var entry = zip.GetEntry(entryPath);
        if (entry == null)
        {
            Debug.LogError($"xlsx 里找不到 {entryPath}");
            return sheet;
        }

        using (var stream = entry.Open())
        {
            var doc = new XmlDocument();
            doc.Load(stream);

            // xlsx 的 XML 带默认命名空间，XPath 必须挂命名空间管理器才能匹配到节点
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

            foreach (XmlNode rowNode in doc.SelectNodes("//x:row", ns))
            {
                int rowNumber = int.Parse(rowNode.Attributes["r"].Value);

                // 单元格 c 的 r 属性形如 "B4"：字母是列，数字是行
                var cells = new Dictionary<string, string>();
                foreach (XmlNode c in rowNode.SelectNodes("x:c", ns))
                {
                    string refName = c.Attributes["r"].Value;
                    string colLetters = new string(refName.TakeWhile(char.IsLetter).ToArray());
                    string type = c.Attributes["t"]?.Value ?? "";   // s=共享字符串，空=数字

                    var vNode = c.SelectSingleNode("x:v", ns);
                    if (vNode == null)
                        continue;

                    string value = vNode.InnerText;
                    if (type == "s")
                    {
                        int index = int.Parse(value);
                        value = index < sharedStrings.Count ? sharedStrings[index] : "";
                    }

                    cells[colLetters] = value;
                }

                if (rowNumber == 2)
                {
                    foreach (var kv in cells)
                        sheet.Headers[kv.Value] = kv.Key;
                }
                else if (rowNumber >= 4)
                {
                    sheet.DataRows.Add(cells);
                }
            }
        }

        return sheet;
    }

    /// <summary>按字段名取一行里的值，空单元格返回空字符串</summary>
    private static string Get(Dictionary<string, string> row, SheetData sheet, string fieldName)
    {
        return sheet.Headers.TryGetValue(fieldName, out string col) && row.TryGetValue(col, out string value)
            ? value
            : "";
    }

    /// <summary>按字段名取整数，空或填错时返回 0 并警告，不中断整个导入</summary>
    private static int GetInt(Dictionary<string, string> row, SheetData sheet, string fieldName)
    {
        string raw = Get(row, sheet, fieldName);
        if (int.TryParse(raw, out int value))
            return value;

        Debug.LogWarning($"字段 {fieldName} 的值 \"{raw}\" 不是数字，按 0 处理");
        return 0;
    }
}
