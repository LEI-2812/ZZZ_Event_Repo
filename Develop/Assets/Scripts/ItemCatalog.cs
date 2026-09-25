using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace BeastBeat
{
    public static class ItemCatalog
    {
        public static void Apply(GameData data, GameWorkbook book)
        {
            var rows = EventCatalog.ReadRows(book.ReadSheet("items")).Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v))).ToArray();
            if(rows.Length == 0) throw new FormatException("items 시트가 비어 있습니다.");
            var header = rows[0].Select(s => s.Trim().TrimStart('\uFEFF')).ToArray();
            if(header.Distinct().Count() != header.Length || new[]{"id","name","grade"}.Any(k => !header.Contains(k)))
                throw new FormatException("items: id, name, grade 컬럼을 확인하세요.");
            var items = rows.Skip(1).Select((row, index) => {
                string Get(string key) { int i = Array.IndexOf(header, key); return i < row.Count ? row[i].Trim() : ""; }
                if(!int.TryParse(Get("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id <= 0)
                    throw new FormatException("items " + (index + 2) + "행: id는 양수여야 합니다.");
                string value = Get("grade"); int grade = 0;
                if(value.Length > 0 && (!int.TryParse(value, out grade) || grade < 1 || grade > 4))
                    throw new FormatException("items " + id + ": grade는 1~4 또는 빈칸이어야 합니다.");
                return new ItemData { id = id, name = Get("name"), grade = grade };
            }).ToArray();
            if(items.Select(i => i.id).Distinct().Count() != items.Length) throw new FormatException("items: 중복 id");
            data.items = items;
        }
        public static string DisplayName(ItemData item) => string.IsNullOrWhiteSpace(item.name) ? "아이템 " + item.id + " · 이름 미입력" : item.name;
        public static Color GradeColor(int grade)
        {
            switch(grade) {
                case 1: return new Color32(0x7D,0xA8,0x9B,0xFF);
                case 2: return new Color32(0x00,0xAA,0xFF,0xFF);
                case 3: return new Color32(0xE9,0x00,0xFF,0xFF);
                case 4: return new Color32(0xFF,0xB5,0x00,0xFF);
                default: return new Color(.4f,.42f,.45f);
            }
        }
    }
}
