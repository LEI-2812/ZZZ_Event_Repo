using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeastBeat.Editor
{
    public static class BeastBeatSetup
    {
        public const string ScenePath="Assets/Scenes/Event List Scene.unity";
        [MenuItem("Beast Beat/이벤트 씬 열기")]
        public static void OpenScene()
        {
            if(EditorApplication.isPlaying)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Beast Beat/검증/핵심 규칙 테스트")]
        public static void RunChecks()
        {
            var data=GameData.Load();data.Validate();int count=0;
            Action<bool,string> check=(ok,msg)=>{if(!ok)throw new Exception("FAIL: "+msg);count++;};
            string dir=Path.Combine(Application.temporaryCachePath,"BeastBeatChecks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var p=new ProgressService(data,Path.Combine(dir,"save.json"));
            check(Elements.Strong(1,2)&&Elements.Strong(2,3)&&Elements.Strong(3,1),"circular affinity");
            check(Elements.Multiplier(4,5,data.balance)==1.3f&&Elements.Multiplier(5,4,data.balance)==1.3f,"mutual affinity");
            check(Elements.Multiplier(1,3,data.balance)==.7f&&Elements.Multiplier(1,4,data.balance)==1,"weak and neutral");
            p.Save.accountLevel=22;check(!p.Eligible,"account gate");p.Save.accountLevel=30;p.Save.clearedChapter=2;check(!p.Eligible,"chapter gate");p.Save.clearedChapter=3;
            check(p.StageOpen(1)&&!p.StageOpen(2),"stage lock");
            check(p.Adopt(11)&&!p.Adopt(11)&&!p.Adopt(52),"adoption gate and duplicates");p.Adopt(21);p.Adopt(31);
            check(!p.SetParty(new[]{11,11})&&!p.SetParty(new int[0])&&!p.SetParty(new[]{52}),"party invariants");
            var battle=new BattleEngine(p,1,6);int pp=battle.Player.pp[1];battle.Act(1);check(battle.Player.pp[1]==pp-1,"PP consumed");
            battle.Player.pp[0]=0;int hp=battle.Enemy.hp;check(battle.Act(0).Count==0&&battle.Enemy.hp==hp,"exhausted skill blocked");
            battle=new BattleEngine(p,1,6);int hp2=battle.player[1].hp;battle.Switch(1);check(battle.player[1].hp<hp2,"voluntary switch costs turn");
            battle=new BattleEngine(p,1,6);battle.Player.hp=0;hp2=battle.player[1].hp;battle.Switch(1);check(battle.player[1].hp==hp2,"forced switch free");
            battle=new BattleEngine(p,1,6);battle.Player.state=Condition.Burn;battle.Player.stateTurns=2;battle.Player.hp=400;battle.Act(2);check(battle.Player.state==Condition.Ready,"recovery clears status");
            battle=new BattleEngine(p,1,6);battle.Player.state=Condition.Frozen;battle.Player.stateTurns=2;hp=battle.Enemy.hp;battle.Act(1);check(battle.Enemy.hp==hp&&battle.Player.state==Condition.Ready,"freeze skips action and clears");
            battle=new BattleEngine(p,1,6);battle.Player.pp=new[]{0,0,0};hp=battle.Enemy.hp;check(battle.Act(-1).Count>0&&battle.Enemy.hp<hp,"struggle fallback");
            battle=new BattleEngine(p,1,6);battle.enemy[0].hp=1;battle.Act(1);check(battle.Finished&&battle.Won,"victory");
            battle=new BattleEngine(p,1,6);foreach(var f in battle.player)f.hp=1;battle.player[1].hp=0;battle.player[2].hp=0;battle.Player.pp=new[]{0,0,0};battle.Act(-1);check(battle.Finished&&!battle.Won,"defeat");
            p.Victory(1,new[]{11,21,31});int money=p.Save.inventory.Sum(x=>x.amount);p.Victory(1,new[]{11,21,31});check(p.Save.inventory.Sum(x=>x.amount)==money&&p.StageOpen(2),"first-clear reward idempotency");
            check(p.Claim("l1")&&!p.Claim("l1")&&!p.Claim("l20"),"level claim eligibility and idempotency");
            var reloaded=new ProgressService(data,Path.Combine(dir,"save.json"));check(reloaded.Save.claimed.Contains("l1")&&reloaded.Save.party.SequenceEqual(p.Save.party),"save roundtrip");
            p.Save.eventStartedUtc=DateTime.UtcNow.AddDays(-30).ToString("o");foreach(var a in data.achievement)p.Save.claimed.Add("a"+a.id);check(!p.CanClaim("special"),"expired limited reward blocked");
            p.Save.level=19;p.Save.xp=0;for(int i=0;i<10;i++)p.Victory(11,new[]{11});check(p.Save.level==20&&p.Owned(11).level<=20&&p.Save.xp==0,"level cap");
            File.WriteAllText(Path.Combine(dir,"save.json"),"corrupt");var recovered=new ProgressService(data,Path.Combine(dir,"save.json"));check(recovered.Save.owned.Count==3,"backup recovery");
            Debug.Log("BEAST_BEAT_CHECKS_PASS "+count+" rules");
        }
        [MenuItem("Beast Beat/검증/전체 리그 시뮬레이션")]
        public static void SimulateLeague()
        {
            var data=GameData.Load();var p=new ProgressService(data,Path.Combine(Application.temporaryCachePath,"BeastBeatLeague-"+Guid.NewGuid().ToString("N")+".json"));p.Adopt(11);p.Adopt(21);p.Adopt(31);
            string report="";
            foreach(var s in data.stage_list) {
                // New free adoptions catch up to player level; pick suitable available partners.
                foreach(var b in data.bangboo)if(p.CanAdopt(b.id))p.Adopt(b.id);
                var team=p.Save.owned.OrderByDescending(x=>Elements.Multiplier(data.Boo(x.id).type,s.npc_type,data.balance)*p.Stat(data.Boo(x.id),x.level,"atk")).Take(3).Select(x=>x.id).ToArray();p.SetParty(team);
                bool won=false;
                for(int attempt=0;attempt<12&&!won;attempt++) {
                    var battle=new BattleEngine(p,s.id,100+s.id+attempt);int safety=0;
                    while(!battle.Finished&&++safety<200) {
                        if(battle.NeedsSwitch){battle.Switch(Array.FindIndex(battle.player,x=>x.Alive));continue;}
                        int skill=battle.Player.hp<battle.Player.maxHp*.40&&battle.Player.pp[2]>0?2:battle.Player.pp[1]>0?1:battle.Player.pp[0]>0?0:battle.Player.pp[2]>0?2:-1;
                        battle.Act(skill);
                    }
                    if(battle.Won){p.Victory(s.id,p.Save.party);won=true;report+=s.id+":"+s.npc+" rounds="+safety+" LV="+p.Save.level+" retries="+attempt+"\n";}
                    else if(s.id>1) {
                        // Replay the already-unlocked warmup through the same engine, never grant unearned XP.
                        var replay=new BattleEngine(p,1,attempt);int turns=0;
                        while(!replay.Finished&&++turns<200){if(replay.NeedsSwitch){replay.Switch(Array.FindIndex(replay.player,x=>x.Alive));continue;}int pick=replay.Player.hp<replay.Player.maxHp*.4&&replay.Player.pp[2]>0?2:replay.Player.pp[1]>0?1:replay.Player.pp[0]>0?0:replay.Player.pp[2]>0?2:-1;replay.Act(pick);}
                        if(replay.Won)p.Victory(1,p.Save.party);
                    }
                }
                if(!won)throw new Exception("League blocked at "+s.id+"\n"+report);
            }
            Debug.Log("BEAST_BEAT_LEAGUE_PASS\n"+report);
        }
    }
}
