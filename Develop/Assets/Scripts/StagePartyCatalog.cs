using System;
using System.Collections.Generic;
using System.Linq;

namespace BeastBeat
{
    public static class StagePartyCatalog
    {
        // stage_id로 스테이지를 찾고 각 슬롯의 방부 ID와 레벨을 같은 순서로 저장합니다.
        // 아직 행이 없는 스테이지는 기존 적 편성을 유지하며 입력된 행의 오류는 숨기지 않습니다.
        public static void Apply(GameData data, GameWorkbook book)
        {
            if (!book.sheets.Any(s => s.name == "stage_party")) return;
            var rows = EventCatalog.ReadRows(book.ReadSheet("stage_party"))
                .Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v))).ToArray();
            if (rows.Length == 0) throw new FormatException("stage_party 헤더 누락");
            var header = rows[0].Select(v => v.Trim()).ToArray();
            if (header.Distinct().Count() != header.Length || new[]{"id","stage_id","b1_id","b1_lv"}.Any(k => !header.Contains(k)))
                throw new FormatException("stage_party: id, stage_id, b1_id, b1_lv 컬럼 필요");
            var ids = new HashSet<int>(); var stages = new HashSet<int>();
            foreach (var row in rows.Skip(1))
            {
                int Number(string key)
                {
                    int index = Array.IndexOf(header, key);
                    string value = index < 0 || index >= row.Count ? "" : row[index].Trim();
                    if (value.Length == 0) return 0;
                    if (!int.TryParse(value, out int n)) throw new FormatException("stage_party " + key + ": 정수 필요");
                    return n;
                }
                int id = Number("id"), stageId = Number("stage_id");
                if (id <= 0 || !ids.Add(id) || !stages.Add(stageId)) throw new FormatException("stage_party: 중복 id/stage_id");
                var stage = data.stage_list.FirstOrDefault(s => s.id == stageId);
                if (stage == null) throw new FormatException("stage_party: 없는 stage_id " + stageId);
                var members = new List<int>(); var levels = new List<int>(); bool empty = false;
                for (int slot = 1; slot <= 3; slot++)
                {
                    int bid = Number("b" + slot + "_id"), lv = Number("b" + slot + "_lv");
                    if (bid == 0 && lv == 0) { empty = true; continue; }
                    if (empty || !data.bangboo.Any(b => b.id == bid && b.id < 10000) || lv < 1 || lv > data.balance.maxLevel)
                        throw new FormatException("stage_party " + stageId + ": 슬롯 " + slot + "의 방부 ID·레벨·빈 슬롯 순서 확인");
                    members.Add(bid); levels.Add(lv);
                }
                if (members.Count == 0) throw new FormatException("stage_party " + stageId + ": 적 방부가 최소 1마리 필요합니다.");
                stage.enemies = members.ToArray(); stage.enemyLevels = levels.ToArray();
                stage.level = levels.Max();
            }
        }
    }
}
