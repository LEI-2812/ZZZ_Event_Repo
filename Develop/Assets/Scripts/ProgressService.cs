using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BeastBeat
{
    public class ProgressService
    {
        public readonly GameData Data;
        public SaveData Save;
        public string StorageWarning = "";
        readonly string path;
        public ProgressService(GameData data, string file = null)
        {
            Data = data; Data.Validate(); path = file ?? Path.Combine(Application.persistentDataPath, "beast-beat-v1.json");
            Save = Load() ?? new SaveData { eventStartedUtc = DateTime.UtcNow.ToString("o"), bangbooIdSchema = 1 };
        }
        SaveData Load()
        {
            foreach (var p in new[] { path, path + ".bak" })
            {
                if (!File.Exists(p)) continue;
                try {
                    var s = JsonUtility.FromJson<SaveData>(File.ReadAllText(p));
                    if (s == null || s.version != 1 || s.level < 1 || s.level > 20 || s.owned == null || s.party == null || s.claimed == null || s.inventory == null || s.cleared == null) throw new Exception("Invalid save");
                    BangbooCatalog.UpgradeSave(Data,s);
                    DateTime.Parse(s.eventStartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
                    if (s.owned.Any(x => !Data.bangboo.Any(b => b.id == x.id) || x.level < 1 || x.level > s.level) || s.party.Count > 3 || s.party.Distinct().Count() != s.party.Count || s.party.Any(x => !s.owned.Any(o => o.id == x))) throw new Exception("Invalid party");
                    if (p.EndsWith(".bak")) StorageWarning = "백업 저장 데이터로 복구했습니다.";
                    return s;
                } catch (Exception e) { StorageWarning = "저장 파일을 읽지 못했습니다. 기존 파일은 백업으로 보존됩니다."; Debug.LogWarning("BeastBeat save: " + e.Message); }
            }
            // Do not silently destroy a corrupt save: keep a separate recovery copy.
            if (File.Exists(path)) try { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, false); } catch { }
            return null;
        }
        public void Persist() { TryPersist(); }
        public bool TryPersist()
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(Save, true));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
                else File.Move(path + ".tmp", path);
                StorageWarning = ""; return true;
            } catch (Exception e) { StorageWarning = "자동 저장 실패: 저장 폴더의 여유 공간과 권한을 확인해주세요."; Debug.LogWarning("BeastBeat save failed: " + e.Message); return false; }
        }
        public bool Eligible { get { return Save.accountLevel >= Data.event_list.minimumAccountLevel && Save.clearedChapter >= Data.event_list.minimumChapter; } }
        public bool LimitedActive { get { return DateTime.UtcNow < DateTime.Parse(Save.eventStartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).AddDays(Data.event_list.demoLimitedDays); } }
        public int DaysLeft { get { return Math.Max(0, (int)Math.Ceiling((DateTime.Parse(Save.eventStartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).AddDays(Data.event_list.demoLimitedDays) - DateTime.UtcNow).TotalDays)); } }
        public bool Owns(int id) { return Save.owned.Any(x => x.id == id); }
        public OwnedBoo Owned(int id) { return Save.owned.First(x => x.id == id); }
        public bool CanAdopt(int id) { return !Owns(id) && Save.level >= Data.Boo(id).unlockLevel; }
        public bool Adopt(int id)
        {
            if (!CanAdopt(id)) return false;
            Save.owned.Add(new OwnedBoo { id = id, level = Save.level });
            if (Save.party.Count < 3) Save.party.Add(id);
            Persist(); return true;
        }
        public bool SetParty(IEnumerable<int> ids)
        {
            var list = ids.ToList();
            if (list.Count < 1 || list.Count > 3 || list.Distinct().Count() != list.Count || list.Any(x => !Owns(x))) return false;
            Save.party = list; Persist(); return true;
        }
        public bool StageOpen(int id)
        {
            var stage = Data.stage_list.FirstOrDefault(s => s.id == id);
            return stage != null && Data.stage_list.Where(x => x.event_list_id == stage.event_list_id && x.id < id).All(x => Save.cleared.Contains(x.id));
        }
        public void Grant(RewardData[] rewards)
        {
            foreach (var r in rewards) {
                var item = Save.inventory.FirstOrDefault(x => x.id == r.items_id);
                if (item == null) { item = new InventoryItem { id = r.items_id }; Save.inventory.Add(item); }
                item.amount += r.amount;
            }
        }
        void Grow(ref int level, ref int xp, int amount, int cap)
        {
            xp += amount;
            while (level < cap && xp >= Data.NeedXp(level)) { xp -= Data.NeedXp(level); level++; }
            if (level >= Data.balance.maxLevel) xp = 0;
            // At player cap, retain at most one level's experience until the player grows.
            else if (level >= cap) xp = Math.Min(xp, Data.NeedXp(level) - 1);
        }
        public List<string> Victory(int stageId, IEnumerable<int> participating)
        {
            var s = Data.Stage(stageId); var notes = new List<string>();
            int before = Save.level;
            Grow(ref Save.level, ref Save.xp, s.experience, Data.balance.maxLevel);
            notes.Add("플레이어 경험치 +" + s.experience);
            if (Save.level > before) notes.Add("플레이어 LV. " + before + " → " + Save.level);
            foreach (int id in participating.Distinct()) {
                var b = Owned(id); int old = b.level;
                Grow(ref b.level, ref b.xp, s.experience, Save.level);
                if (old != b.level) notes.Add(Data.Boo(id).name + " LV. " + old + " → " + b.level);
                if (old < Data.balance.evolutionLevel && b.level >= Data.balance.evolutionLevel) notes.Add(Data.Boo(id).name + " 진화! 코어가 각성했습니다.");
            }
            if (!Save.cleared.Contains(stageId)) {
                Save.cleared.Add(stageId); Grant(Data.stage_rewards.Where(x => x.owner_id == stageId).ToArray());
                notes.Add("첫 클리어 보상을 획득했습니다.");
            } else notes.Add("재도전 경험치를 획득했습니다. 첫 클리어 보상은 이미 수령했습니다.");
            Save.lastStage = stageId; Persist(); return notes;
        }
        public int Metric(AchievementData a)
        {
            if (a.metric == "collection") return Save.owned.Count;
            if (a.metric == "evolution") return Save.owned.Count(x => x.level >= Data.balance.evolutionLevel);
            if (a.metric == "stage") return Save.cleared.Count;
            return 0;
        }
        public bool CanClaim(string key)
        {
            if (Data.rewardCatalog != null) return RewardClaims.CanClaim(this,key);
            if (Save.claimed.Contains(key)) return false;
            if (key == "special") return LimitedActive && Data.achievement.All(a => Save.claimed.Contains("a" + a.id));
            if (key == "maxlevel") return Enumerable.Range(1, 20).All(l => Save.claimed.Contains("l" + l));
            int id; if (key.Length < 2 || !int.TryParse(key.Substring(1), out id)) return false;
            if (key[0] == 'l') return id >= 1 && id <= 20 && Save.level >= id;
            var a = Data.achievement.FirstOrDefault(x => x.id == id);
            return key[0] == 'a' && a != null && Metric(a) >= a.target;
        }
        public bool Claim(string key)
        {
            if (Data.rewardCatalog != null) return RewardClaims.Claim(this,key);
            if (!CanClaim(key)) return false;
            RewardData[] rows;
            if (key == "special") rows = new[] { new RewardData { items_id = 10, amount = 1 }, new RewardData { items_id = 11, amount = 1 } };
            else if (key == "maxlevel") rows = new[] { new RewardData { items_id = 12, amount = 1 } };
            else { int id = int.Parse(key.Substring(1)); rows = (key[0] == 'l' ? Data.level_rewards : Data.achievement_rewards).Where(x => x.owner_id == id).ToArray(); }
            Grant(rows); Save.claimed.Add(key); Persist(); return true;
        }
        public bool HasRewards { get { return Enumerable.Range(1,20).Any(x => CanClaim("l" + x)) || Data.achievement.Any(x => CanClaim("a" + x.id)) || CanClaim("special") || CanClaim("maxlevel"); } }
        public int Stat(BooData b, int level, string stat)
        {
            if (stat == "hp") return Math.Min(999, b.hp + (level - 1) * 4 + (level >= Data.balance.evolutionLevel ? 25 : 0));
            return (stat == "atk" ? b.atk : b.def) + (level - 1) * 4 + (level >= Data.balance.evolutionLevel ? 12 : 0);
        }
    }
}
