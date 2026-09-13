using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BeastBeat
{
    public sealed class StageCatalogEntry
    {
        public int id, eventId, group, element;
        public string npc, dialogue;
    }

    public static class StageCatalog
    {
        public static readonly string[] Headers = { "아이디", "이벤트 아이디", "종류", "등장 npc", "npc 대사", "npc 속성" };
        public static string GroupName(int group) => new[] { "", "워밍업", "예선전", "본선", "결승전" }[group];
        public static List<StageCatalogEntry> Parse(string csv)
        {
            var rows = EventCatalog.ReadRows(csv);
            if (rows.Count == 0) throw new FormatException("스테이지 CSV 헤더가 없습니다.");
            var header = rows[0].Select(s => s.Trim().TrimStart('\uFEFF')).ToArray();
            if (header.Length != 6 || !header.SequenceEqual(Headers)) throw new FormatException("CSV 컬럼: " + string.Join(",", Headers));
            var ids = new HashSet<int>(); var result = new List<StageCatalogEntry>();
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                try
                {
                    if (row.Count != 6) throw new FormatException("6개 컬럼이 필요합니다.");
                    int Number(int index) => int.Parse(row[index].Trim(), CultureInfo.InvariantCulture);
                    var entry = new StageCatalogEntry { id = Number(0), eventId = Number(1), group = Number(2), npc = row[3].Trim(), dialogue = row[4].Trim().Replace("\\n", "\n"), element = Number(5) };
                    if (entry.id <= 0 || !ids.Add(entry.id)) throw new FormatException("아이디는 중복 없는 양수여야 합니다.");
                    if (entry.eventId <= 0) throw new FormatException("이벤트 아이디는 양수여야 합니다.");
                    if (entry.group < 1 || entry.group > 4) throw new FormatException("종류는 1~4입니다.");
                    if (entry.element < 1 || entry.element > 5) throw new FormatException("npc 속성은 1~5입니다.");
                    if (entry.npc.Length == 0) throw new FormatException("등장 npc가 비어 있습니다.");
                    result.Add(entry);
                }
                catch (Exception ex) { throw new FormatException($"CSV {i + 1}행: {ex.Message}", ex); }
            }
            return result.OrderBy(e => e.id).ToList();
        }

        // Six-column catalog owns stage identity and NPC data. Existing JSON owns battle balance.
        // New IDs inherit a same-group battle/reward template, so adding a CSV row is playable.
        public static void Apply(GameData data, List<StageCatalogEntry> entries)
        {
            var originals = data.stage_list;
            var rewards = data.stage_rewards.ToList();
            int rewardId = rewards.Count == 0 ? 1 : rewards.Max(r => r.id) + 1;
            var stages = new List<StageData>();
            foreach (var entry in entries)
            {
                var original = originals.FirstOrDefault(s => s.id == entry.id);
                var template = original ?? originals.FirstOrDefault(s => s.type == entry.group) ?? originals.FirstOrDefault();
                int ordinal = entries.Count(e => e.eventId == entry.eventId && e.group == entry.group && e.id <= entry.id);
                int[] enemies = template == null ? Array.Empty<int>() : template.enemies.ToArray();
                if (original == null || original.npc_type != entry.element)
                {
                    var candidates = data.bangboo.Where(b => b.type == entry.element).OrderBy(b => b.id).Select(b => b.id).ToArray();
                    if (candidates.Length == 0) throw new FormatException("해당 속성의 전투 방부가 없습니다: " + entry.element);
                    int count = template == null ? Math.Min(3, entry.group) : template.enemies.Length;
                    enemies = Enumerable.Range(0, count).Select(i => candidates[i % candidates.Length]).ToArray();
                }
                stages.Add(new StageData { id = entry.id, event_list_id = entry.eventId, type = entry.group, npc_type = entry.element,
                    name = GroupName(entry.group) + " 배틀 " + ordinal.ToString("00"), npc = entry.npc, npc_dialogue = entry.dialogue,
                    level = template == null ? 1 : template.level, experience = template == null ? 280 : template.experience, enemies = enemies });
                if (original == null && template != null)
                    foreach (var reward in data.stage_rewards.Where(r => r.owner_id == template.id))
                        rewards.Add(new RewardData { id = rewardId++, owner_id = entry.id, items_id = reward.items_id, amount = reward.amount });
            }
            data.stage_list = stages.ToArray();
            data.stage_rewards = rewards.Where(r => entries.Any(e => e.id == r.owner_id)).ToArray();
        }
    }
}
