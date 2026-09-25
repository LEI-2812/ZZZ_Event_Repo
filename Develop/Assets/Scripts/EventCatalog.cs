using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BeastBeat
{
    [Serializable]
    public sealed class EventCatalogEntry
    {
        public int id;
        public string category, title, description, background;
        public DateTime updatedAt;
        public int[] rewardIds, achievementIds;
    }

    // CSV is data only: scene destinations and callbacks remain in the scene.
    public static class EventCatalog
    {
        public static List<EventCatalogEntry> Parse(string csv)
        {
            var rows = ReadRows(csv);
            if (rows.Count == 0) throw new FormatException("이벤트 CSV가 비어 있습니다.");
            var headers = rows[0].Select(s => s.Trim().TrimStart('\uFEFF')).ToArray();
            if (headers.Distinct().Count() != headers.Length) throw new FormatException("중복된 CSV 열 이름입니다.");
            foreach (var key in new[] { "id", "category", "updated_at", "title", "description" })
                if (!headers.Contains(key)) throw new FormatException("필수 CSV 열 누락: " + key);
            var entries = new List<EventCatalogEntry>();
            var ids = new HashSet<int>();
            for (int i = 1; i < rows.Count; i++)
            {
                var cells = rows[i];
                if (cells.All(string.IsNullOrWhiteSpace)) continue;
                if (cells.Count != headers.Length) throw new FormatException($"CSV {i + 1}행: 열 개수가 다릅니다.");
                string Get(string key) { int index = Array.IndexOf(headers, key); return index < 0 ? "" : cells[index].Trim(); }
                try
                {
                    var entry = new EventCatalogEntry {
                        id = int.Parse(Get("id"), CultureInfo.InvariantCulture),
                        category = Get("category"), title = Get("title"), description = Get("description").Replace("\\n", "\n"),
                        updatedAt = DateTime.ParseExact(Get("updated_at"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                        background = Get("background"), rewardIds = Ids(Get("reward_ids")), achievementIds = Ids(Get("achievement_ids"))
                    };
                    if (entry.id <= 0 || !ids.Add(entry.id)) throw new FormatException("ID는 중복 없는 양수여야 합니다.");
                    if (entry.category != "permanent" && entry.category != "limited") throw new FormatException("category는 permanent 또는 limited여야 합니다.");
                    if (entry.title.Length == 0) throw new FormatException("title이 비어 있습니다.");
                    if (entry.rewardIds.Length > 6 || entry.achievementIds.Length > 3) throw new FormatException("보상은 최대 6개, 업적은 최대 3개입니다.");
                    entries.Add(entry);
                }
                catch (Exception ex) { throw new FormatException($"CSV {i + 1}행: {ex.Message}", ex); }
            }
            return entries.OrderBy(e => e.updatedAt).ThenBy(e => e.id).ToList();
        }
        static int[] Ids(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<int>();
            var values = value.Split(';').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            if (values.Any(n => n <= 0)) throw new FormatException("참조 ID는 양수여야 합니다.");
            return values;
        }
        public static List<List<string>> ReadRows(string value)
        {
            var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder();
            bool quoted = false, closed = false;
            value = (value ?? "").TrimStart('\uFEFF');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (quoted)
                {
                    if (c == '"') { if (i + 1 < value.Length && value[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; closed = true; } }
                    else field.Append(c);
                }
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); closed = false; }
                else if (c == '\r' || c == '\n') { row.Add(field.ToString()); rows.Add(row); row = new List<string>(); field.Clear(); closed = false; if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n') i++; }
                else if (c == '"' && field.Length == 0 && !closed) quoted = true;
                else if (closed || c == '"') throw new FormatException("CSV 따옴표 형식이 잘못되었습니다.");
                else field.Append(c);
            }
            if (quoted) throw new FormatException("CSV 따옴표가 닫히지 않았습니다.");
            if (field.Length > 0 || row.Count > 0 || closed) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
