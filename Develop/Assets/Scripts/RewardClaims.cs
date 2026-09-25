using System;
using System.Linq;
using UnityEngine;

namespace BeastBeat
{
    public static class RewardClaims
    {
        public static bool Claimed(ProgressService p,string key)
        {
            if(p.Save.claimed.Contains(key))return true;
            if(key=="special")return p.Data.rewardCatalog.user.is_get_lm_reward;
            if(key=="maxlevel")return p.Data.rewardCatalog.user.is_get_lv_reward;
            if(key.StartsWith("ar")&&int.TryParse(key.Substring(2),out int id)){
                var row=p.Data.rewardCatalog.rows.FirstOrDefault(r=>r.id==id);
                return row!=null&&p.Save.claimed.Contains("a"+row.a_id); // previous versions claimed a whole group
            }
            return false;
        }
        public static bool Complete(ProgressService p,bool level)
        {
            if(level){var levels=p.Data.level_rewards.Select(r=>r.owner_id).Distinct().ToArray();return levels.Length>0&&p.Save.level>=p.Data.balance.maxLevel&&levels.All(l=>Claimed(p,"l"+l));}
            var rows=p.Data.rewardCatalog.rows;var stages=p.Data.stage_list.Where(s=>s.event_list_id==p.Data.event_list.id).ToArray();
            return rows.Length>0&&stages.Length>0&&stages.All(s=>p.Save.cleared.Contains(s.id))&&rows.All(r=>Claimed(p,"ar"+r.id));
        }
        public static bool CanClaim(ProgressService p,string key)
        {
            if(string.IsNullOrEmpty(key)||Claimed(p,key))return false;
            if(key=="special")return p.LimitedActive&&Complete(p,false);
            if(key=="maxlevel")return Complete(p,true);
            if(key.StartsWith("ar")&&int.TryParse(key.Substring(2),out int rewardId)){
                var row=p.Data.rewardCatalog.rows.FirstOrDefault(r=>r.id==rewardId);var a=row==null?null:p.Data.achievement.FirstOrDefault(x=>x.id==row.a_id);
                return false; // 업적 수령 조건은 아직 정의되지 않았습니다.
            }
            if(key[0]=='l'&&int.TryParse(key.Substring(1),out int level))return p.Data.level_rewards.Any(r=>r.owner_id==level)&&p.Save.level>=level;
            if(key[0]=='a'&&int.TryParse(key.Substring(1),out int group))return p.Data.rewardCatalog.rows.Any(r=>r.a_id==group&&CanClaim(p,"ar"+r.id));
            return false;
        }
        public static bool Claim(ProgressService p,string key)
        {
            if(!CanClaim(p,key))return false;
            string[] keys=new[]{key}; RewardData[] rewards;
            if(key=="special"||key=="maxlevel")rewards=p.Data.rewardCatalog.special[key];
            else if(key.StartsWith("ar")) {int id=int.Parse(key.Substring(2));rewards=p.Data.achievement_rewards.Where(r=>r.id==id).ToArray();}
            else if(key[0]=='a') {int group=int.Parse(key.Substring(1));var rows=p.Data.rewardCatalog.rows.Where(r=>r.a_id==group&&CanClaim(p,"ar"+r.id)).ToArray();keys=rows.Select(r=>"ar"+r.id).ToArray();rewards=p.Data.achievement_rewards.Where(r=>rows.Any(x=>x.id==r.id)).ToArray();}
            else {int level=int.Parse(key.Substring(1));rewards=p.Data.level_rewards.Where(r=>r.owner_id==level).ToArray();}
            string before=JsonUtility.ToJson(p.Save);p.Grant(rewards);p.Save.claimed.AddRange(keys);
            if(p.TryPersist())return true;
            p.Save=JsonUtility.FromJson<SaveData>(before);return false;
        }
    }
}
