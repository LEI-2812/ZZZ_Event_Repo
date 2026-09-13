using System;
using System.Linq;
using UnityEngine;

namespace BeastBeat
{
    // The XLSX asset itself imports into this runtime data object. No generated CSV files.
    public sealed class GameWorkbook : ScriptableObject
    {
        [Serializable] public sealed class Sheet { public string name; [TextArea] public string records; }
        public string revision;
        [TextArea] public string importError;
        public Sheet[] sheets = Array.Empty<Sheet>();
        public static GameWorkbook Load() => Resources.Load<GameWorkbook>("Data/game_data");
        public string ReadSheet(string name)
        {
            if (!string.IsNullOrEmpty(importError)) throw new FormatException(importError);
            var sheet = sheets.FirstOrDefault(s => s.name == name);
            if (sheet == null) throw new FormatException("game_data.xlsx에 시트가 없습니다: " + name);
            return sheet.records;
        }
    }
}
