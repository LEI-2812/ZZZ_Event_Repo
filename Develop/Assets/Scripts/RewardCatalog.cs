using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BeastBeat
{
    [Serializable] public sealed class AchievementRewardEntry { public int id, items_id, a_id, amount; public string info; }
    [Serializable] public sealed class RewardUser { public int id, level; public int[] clearedStages=Array.Empty<int>(); public string name; public bool is_get_lm_reward, is_get_lv_reward; }
    public sealed class RewardCatalog
    {
        public AchievementRewardEntry[] rows;
        public RewardUser user;
        public Dictionary<string, RewardData[]> special;
        static List<Dictionary<string,string>> Table(GameWorkbook book, string sheet, params string[] columns)
        {
            var rows = EventCatalog.ReadRows(book.ReadSheet(sheet)).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
            if (rows.Count == 0 || columns.Any(c=>!rows[0].Contains(c))) throw new FormatException(sheet + " 컬럼: " + string.Join(",", columns));
            columns=rows[0].ToArray();
            var output = new List<Dictionary<string,string>>(); var ids = new HashSet<int>();
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Count != columns.Length) throw new FormatException(sheet + " " + (i+1) + "행 컬럼 수 오류");
                var row = columns.Select((c,j) => new { c, value = rows[i][j].Trim() }).ToDictionary(x => x.c, x => x.value);
                if (!ids.Add(Number(row,"id"))) throw new FormatException(sheet + " 중복 id: " + row["id"]);
                output.Add(row);
            }
            return output;
        }
        static int Number(Dictionary<string,string> row, string key)
        {
            if (!int.TryParse(row[key],NumberStyles.Integer,CultureInfo.InvariantCulture,out int value) || value <= 0) throw new FormatException(key + "는 양수입니다: " + row[key]);
            return value;
        }
        static bool Flag(string value)
        {
            if(value=="0"||value.Equals("false",StringComparison.OrdinalIgnoreCase))return false;
            if(value=="1"||value.Equals("true",StringComparison.OrdinalIgnoreCase))return true;
            throw new FormatException("획득 여부는 0 또는 1입니다.");
        }
        public static void Apply(GameData data, GameWorkbook book)
        {
            var names=Table(book,"achievement","id","name");
            var achievements=names.Select(n=>{
                int id=Number(n,"id");
                if(string.IsNullOrWhiteSpace(n["name"]))throw new FormatException("업적 이름 누락: "+id);
                return new AchievementData{id=id,name=n["name"]};
            }).OrderBy(a=>a.id).ToArray();
            var rewards=Table(book,"achievement_reward","id","items_id","achievement_info","a_id","amount").Select(r=>new AchievementRewardEntry{id=Number(r,"id"),items_id=Number(r,"items_id"),info=r["achievement_info"],a_id=Number(r,"a_id"),amount=Number(r,"amount")}).OrderBy(r=>r.id).ToArray();
            foreach(var row in rewards)if(!achievements.Any(a=>a.id==row.a_id)||!data.items.Any(i=>i.id==row.items_id)||string.IsNullOrWhiteSpace(row.info))throw new FormatException("achievement_reward 참조/제목 오류: "+row.id);
            var levels=Table(book,"level_reward","id","lv_num","items_id","amount").Select(r=>new RewardData{id=Number(r,"id"),owner_id=Number(r,"lv_num"),items_id=Number(r,"items_id"),amount=Number(r,"amount")}).OrderBy(r=>r.id).ToArray();
            foreach(var row in levels)if(row.owner_id>data.balance.maxLevel||!data.items.Any(i=>i.id==row.items_id))throw new FormatException("level_reward 참조 오류: "+row.id);
            var users=Table(book,"users","id","category","level","is_get_lm_reward","is_get_lv_reward").Select(r=>new RewardUser{id=Number(r,"id"),name=r["category"],level=Number(r,"level"),clearedStages=data.stage_list.Where(stage=>{string key=r.ContainsKey("is_st"+stage.id+"_clear")?"is_st"+stage.id+"_clear":"is_str"+stage.id+"_clear";if(!r.ContainsKey(key))throw new FormatException("users 컬럼 누락: "+key);return Flag(r[key]);}).Select(stage=>stage.id).ToArray(),is_get_lm_reward=Flag(r["is_get_lm_reward"]),is_get_lv_reward=Flag(r["is_get_lv_reward"])}).ToArray();
            var user=users.SingleOrDefault(u=>u.id==1)??throw new FormatException("users 시트에 현재 사용자 id=1이 필요합니다.");
            if(user.level>data.balance.maxLevel)throw new FormatException("users.level 범위 초과");
            var specials=Table(book,"special_reward","id","type","items_id","amount");
            foreach(var row in specials)if(!new[]{"special","maxlevel"}.Contains(row["type"])||!data.items.Any(i=>i.id==Number(row,"items_id")))throw new FormatException("특별 보상 참조 오류");
            var mapped=specials.GroupBy(r=>r["type"]).ToDictionary(g=>g.Key,g=>g.Select(r=>new RewardData{id=Number(r,"id"),items_id=Number(r,"items_id"),amount=Number(r,"amount")}).ToArray());
            if(!mapped.ContainsKey("special")||!mapped.ContainsKey("maxlevel"))throw new FormatException("special/maxlevel 지급 아이템이 필요합니다.");
            data.achievement=achievements;data.achievement_rewards=rewards.Select(r=>new RewardData{id=r.id,owner_id=r.a_id,items_id=r.items_id,amount=r.amount}).ToArray();data.level_rewards=levels;
            data.rewardCatalog=new RewardCatalog{rows=rewards,user=user,special=mapped};
        }
    }
}
