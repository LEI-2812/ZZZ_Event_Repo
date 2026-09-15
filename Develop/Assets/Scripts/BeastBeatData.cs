using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeastBeat
{
    public enum Element { Physical = 1, Fire, Ice, Electric, Ether }
    public enum Condition { Ready = 1, Paralysis, Frozen, Burn }
    [Serializable] public class BooData
    {
        public int id, type, hp, atk, def, unlockLevel;
        public string name, description;
        public int[] skillIds;
    }
    [Serializable] public class SkillData
    {
        public int id, bangboo_id, charge_count, status;
        public string name, description, effect;
        public float power, chance;
    }
    [Serializable] public class StageData
    {
        public int id, event_list_id = 1, type, npc_type, level, experience;
        public string name, npc, npc_dialogue;
        public int[] enemies;
    }
    [Serializable] public class ItemData { public int id, grade; public string name; }
    [Serializable] public class RewardData { public int id, owner_id, items_id, amount; }
    [Serializable] public class AchievementData { public int id, target; public string name, metric; }
    [Serializable] public class EventData
    {
        public int id = 1, type = 1, minimumAccountLevel = 23, minimumChapter = 3;
        public string name, description;
        public int demoLimitedDays = 14;
    }
    [Serializable] public class BalanceData
    {
        public float strong = 1.5f, weak = .65f, defenseFactor = .012f;
        public int maxLevel = 20, evolutionLevel = 10, xpBase = 100, xpStep = 20;
    }
    [Serializable] public class GameData
    {
        [NonSerialized] public RewardCatalog rewardCatalog;
        public EventData event_list;
        public BalanceData balance;
        public BooData[] bangboo;
        public SkillData[] skills;
        public StageData[] stage_list;
        public ItemData[] items;
        public RewardData[] stage_rewards, level_rewards, achievement_rewards;
        public AchievementData[] achievement;
        public BooData Boo(int id) { return bangboo.First(x => x.id == id); }
        public SkillData Skill(int id) { return skills.First(x => x.id == id); }
        public StageData Stage(int id) { return stage_list.First(x => x.id == id); }
        public int NeedXp(int level) { return balance.xpBase + (level - 1) * balance.xpStep; }
        public static GameData LoadBase() { return JsonUtility.FromJson<GameData>((Resources.Load<TextAsset>("Image/game-data")??Resources.Load<TextAsset>("BeastBeat/game-data")).text); }
        public static GameData Load()
        {
            var data = LoadBase();
            var workbook = GameWorkbook.Load();
            if (workbook) { try { StageCatalog.Apply(data, StageCatalog.Parse(workbook.ReadSheet("stage_list"))); } catch (Exception ex) { Debug.LogError("Stage XLSX: " + ex.Message); } }
            if (workbook) { RewardCatalog.Apply(data, workbook); BangbooCatalog.Apply(data, workbook); }
            return data;
        }
        public StageData ResolveStage(int id) { return stage_list.FirstOrDefault(s => s.id == id) ?? stage_list.OrderBy(s => s.id).FirstOrDefault(); }
        public void Validate()
        {
            if (bangboo.Select(x => x.id).Distinct().Count() != bangboo.Length) throw new Exception("Duplicate bangboo ID");
            foreach (var b in bangboo) { if (b.skillIds.Length != 3 || b.hp <= 0) throw new Exception("Invalid bangboo " + b.id); foreach (int s in b.skillIds) if (Skill(s).bangboo_id != b.id) throw new Exception("Skill FK"); }
            foreach (var s in stage_list) { if (s.enemies.Length < 1 || s.enemies.Length > 3) throw new Exception("Enemy party size"); foreach (int b in s.enemies) Boo(b); }
            foreach (var r in stage_rewards.Concat(level_rewards).Concat(achievement_rewards)) if (!items.Any(x => x.id == r.items_id) || r.amount <= 0) throw new Exception("Reward FK");
        }
    }
    [Serializable] public class OwnedBoo { public int id, level = 1, xp; }
    [Serializable] public class InventoryItem { public int id, amount; }
    [Serializable] public class SaveData
    {
        public int version = 1, level = 1, xp, accountLevel = 30, clearedChapter = 3, lastStage = 1;
        public string birthday = "0704", eventStartedUtc;
        public bool tutorialSeen, muted, liveBackground, masterTitle;
        public List<OwnedBoo> owned = new List<OwnedBoo>();
        public List<int> party = new List<int>();
        public int[] configuredParty;
        public List<int> cleared = new List<int>();
        public List<string> claimed = new List<string>();
        public List<InventoryItem> inventory = new List<InventoryItem>();
    }
    public static class Elements
    {
        public static string Name(int type) { return new[] { "", "물리", "불", "얼음", "전기", "에테르" }[type]; }
        public static string Hex(int type) { return new[] { "", "FFD84C", "FF825C", "69E6D7", "62ADFF", "F47BBD" }[type]; }
        public static bool Strong(int a, int b) { return a == 1 && b == 2 || a == 2 && b == 3 || a == 3 && b == 1 || a == 4 && b == 5 || a == 5 && b == 4; }
        public static float Multiplier(int a, int b, BalanceData balance) { return Strong(a,b) ? balance.strong : (a <= 3 && b <= 3 && Strong(b,a) ? balance.weak : 1f); }
    }
}
