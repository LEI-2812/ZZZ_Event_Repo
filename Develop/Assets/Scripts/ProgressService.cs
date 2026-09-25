using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BeastBeat
{
    public class ProgressService
    {
        public GameData Data { get; private set; }
        public SaveData Save;
        public string StorageWarning = "";
        public string WorkbookStorageWarning { get; private set; } = "";
        readonly string workbookPath;
        readonly string path;
        public ProgressService(GameData data, string file = null, string ownershipWorkbookPath = null)
        {
            Data = data; Data.Validate(); path = file ?? Path.Combine(Application.persistentDataPath, "beast-beat-v1.json");
            // 임시 저장 경로를 쓰는 연습/테스트는 원본 엑셀을 수정하지 않습니다.
            workbookPath = ownershipWorkbookPath ?? (file == null ? Path.Combine(Application.dataPath, "Resources/Data/game_data.xlsx") : null);
            Save = Load() ?? new SaveData { eventStartedUtc = DateTime.UtcNow.ToString("o"), bangbooIdSchema = 1 };
            if (Save.pendingWorkbookUnlocks == null) Save.pendingWorkbookUnlocks = new List<int>();
            Save.level = Save.pendingWorkbookLevel > 0 ? Save.pendingWorkbookLevel : Data.rewardCatalog?.user.level ?? Save.level;
            SyncPartyLevels();
            QueueWorkbookUnlocks();
        }
        // 저장된 진행도는 유지하고 검증된 데이터 정의만 갱신합니다.
        public void ReloadData(GameData data)
        {
            data.Validate();
            Data = data;
            Save.level = Save.pendingWorkbookLevel > 0 ? Save.pendingWorkbookLevel : Data.rewardCatalog?.user.level ?? Save.level;
            SyncPartyLevels();
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
                    if (s.party.Count > 3 || s.party.Distinct().Count() != s.party.Count) throw new Exception("Invalid party");
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
            if(workbookPath != null && Data.rewardCatalog != null && Save.level != Data.rewardCatalog.user.level) Save.pendingWorkbookLevel=Save.level;
            SyncPartyLevels();
            QueueWorkbookUnlocks();
            if (!WriteProgress()) return false;
            // 진행도와 재시도 목록부터 보존합니다. 엑셀 잠금 때문에 보상/레벨을 되돌리지 않습니다.
            FlushWorkbookUnlocks();
            return true;
        }
        bool WriteProgress()
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(Save, true));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
                else File.Move(path + ".tmp", path);
                StorageWarning = ""; return true;
            } catch (Exception e) { StorageWarning = "자동 저장 실패: 저장 폴더의 여유 공간과 권한을 확인해주세요."; Debug.LogWarning("BeastBeat save failed: " + e.Message); return false; }
        }
        void QueueWorkbookUnlocks()
        {
            if (workbookPath == null || Data.unlockableBangbooIds == null) return;
            foreach (int id in Data.unlockableBangbooIds)
                if (Save.level >= Data.Boo(id).unlockLevel && !Data.initialOwnedBangbooIds.Contains(id) && !Save.pendingWorkbookUnlocks.Contains(id))
                    Save.pendingWorkbookUnlocks.Add(id);
        }
        public bool RetryWorkbookUnlocks()
        {
            QueueWorkbookUnlocks();
            if (workbookPath == null || Save.pendingWorkbookUnlocks.Count == 0 && Save.pendingWorkbookLevel == 0) return true;
            if (!WriteProgress()) return false;
            return FlushWorkbookUnlocks();
        }
        bool FlushWorkbookUnlocks()
        {
            if (workbookPath == null || Save.pendingWorkbookUnlocks.Count == 0 && Save.pendingWorkbookLevel == 0) return true;
            try {
                int[] ids = Save.pendingWorkbookUnlocks.ToArray();
                WorkbookOwnershipStore.SaveUnlocked(workbookPath, Data.rewardCatalog == null ? 1 : Data.rewardCatalog.user.id, ids, Save.pendingWorkbookLevel > 0 ? (int?)Save.pendingWorkbookLevel : null);
                Data.initialOwnedBangbooIds = Data.initialOwnedBangbooIds.Union(ids).ToArray();
                if(Save.pendingWorkbookLevel > 0 && Data.rewardCatalog != null) Data.rewardCatalog.user.level=Save.pendingWorkbookLevel;
                Save.pendingWorkbookLevel=0;
                Save.pendingWorkbookUnlocks.Clear();
                WorkbookStorageWarning = "";
                // 여기서 저장이 중단되어도 이전 파일의 대기 목록을 다시 적용하는 것은 안전합니다.
                WriteProgress();
                #if UNITY_EDITOR
                if (Path.GetFullPath(workbookPath) == Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/Data/game_data.xlsx")))
                    UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/Data/game_data.xlsx");
                #endif
                return true;
            } catch (Exception e) {
                string action = e is FileNotFoundException || e is DirectoryNotFoundException ? "game_data.xlsx 원본 경로를 확인해 주세요." :
                    e is IOException || e is UnauthorizedAccessException ? "Excel에서 game_data.xlsx를 저장하고 닫고, 쓰기 권한을 확인해 주세요." : "users 시트의 컬럼과 사용자 ID를 확인해 주세요.";
                string message = "플레이어 레벨·방부 해금의 엑셀 저장 대기 중입니다. " + action + " (" + e.Message + ")";
                if (WorkbookStorageWarning != message) Debug.LogWarning(message);
                WorkbookStorageWarning = message;
                return false;
            }
        }
        public bool Eligible { get { return Save.accountLevel >= Data.event_list.minimumAccountLevel && Save.clearedChapter >= Data.event_list.minimumChapter; } }
        public bool LimitedActive { get { return DateTime.UtcNow < DateTime.Parse(Save.eventStartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).AddDays(Data.event_list.demoLimitedDays); } }
        public int DaysLeft { get { return Math.Max(0, (int)Math.Ceiling((DateTime.Parse(Save.eventStartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).AddDays(Data.event_list.demoLimitedDays) - DateTime.UtcNow).TotalDays)); } }
        public bool Owns(int id) { return Data.unlockableBangbooIds == null ? Save.owned.Any(x => x.id == id) : Data.unlockableBangbooIds.Contains(id) && (Data.initialOwnedBangbooIds.Contains(id) || Save.level >= Data.Boo(id).unlockLevel); }
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
        // Individual levels/XP remain in old saves for compatibility only.
        public void SyncPartyLevels()
        {
            // users의 명시 보유 + 현재 레벨의 자동 해금만 인정합니다. 이전 owned는 판정 근거가 아닙니다.
            if (Data.unlockableBangbooIds != null)
            {
                Save.owned = Data.unlockableBangbooIds.Where(Owns).Select(id => new OwnedBoo { id = id, level = Save.level }).ToList();
                Save.party = Save.party.Where(Owns).ToList();
                if (Save.configuredParty != null)
                    Save.configuredParty = Save.configuredParty.Select(id => id != 0 && Owns(id) ? id : 0).ToArray();
            }
            Save.xp = 0;
            foreach (var b in Save.owned) { b.level = Save.level; b.xp = 0; }
        }
        public List<string> Victory(int stageId, IEnumerable<int> participating)
        {
            var stage = Data.Stage(stageId); var notes = new List<string>();
            if(stage.level_gain < 0) throw new InvalidOperationException("level_gain은 0 이상의 정수여야 합니다.");
            int before = Save.level;
            var ownedBefore = Data.bangboo.Where(b => Owns(b.id)).Select(b => b.id).ToHashSet();
            if (!Save.cleared.Contains(stageId)) {
                Save.level += Math.Min(stage.level_gain, Math.Max(0, Data.balance.maxLevel - Save.level));
                SyncPartyLevels();
                foreach (var boo in Data.bangboo.Where(b => Owns(b.id) && !ownedBefore.Contains(b.id))) notes.Add("방부 해금: " + boo.name);
                if (Save.level > before) notes.Add("플레이어·방부 LV. " + before + " → " + Save.level);
                else notes.Add(Save.level >= Data.balance.maxLevel ? "플레이어·방부가 최고 레벨입니다." : "이 스테이지는 레벨 상승 보상이 없습니다.");
                if (before < Data.balance.evolutionLevel && Save.level >= Data.balance.evolutionLevel)
                    notes.Add("모든 보유 방부가 진화 레벨에 도달했습니다.");
                Save.cleared.Add(stageId); Grant(Data.stage_rewards.Where(x => x.owner_id == stageId).ToArray());
                notes.Add("첫 클리어 보상을 획득했습니다.");
            } else notes.Add("이미 클리어한 스테이지입니다. 레벨과 첫 클리어 보상은 추가 지급되지 않습니다.");
            Save.lastStage = stageId; Persist();
            if (!string.IsNullOrEmpty(WorkbookStorageWarning)) notes.Add(WorkbookStorageWarning);
            return notes;
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
            return false; // 업적 조건 미구현
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
            if (level < 1 || level > Data.balance.maxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            // 엑셀의 방부 정의에 현재 레벨 공식을 적용합니다. 원본 능력치 셀은 덮어쓰지 않습니다.
            if (stat == "hp") return (int)Math.Floor(((level - 1) / 19.0) * 1995) + 800;
            if (stat == "atk" || stat == "def") return (int)Math.Floor(((level - 1) / 19.0) * 160) + 50;
            throw new ArgumentException("지원하지 않는 능력치: " + stat);
        }
    }
}
