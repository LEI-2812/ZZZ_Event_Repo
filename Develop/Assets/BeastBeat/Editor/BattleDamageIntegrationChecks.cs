using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace BeastBeat.Editor {
 public static class BattleDamageIntegrationChecks {
  public static int Run() {
   int count=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
   bool logging=Debug.unityLogger.logEnabled;
   try {
    Debug.unityLogger.logEnabled=false;
    var data=GameData.Load();
    foreach(var legacy in data.bangboo.Where(b=>b.id>=10000)) {
     var source=data.bangboo.First(b=>b.id<10000&&b.type==legacy.type);
     for(int i=0;i<3;i++)Check(data.Skill(legacy.skillIds[i]).dmg==data.Skill(source.skillIds[i]).dmg,"legacy workbook power");
    }
    var p=new ProgressService(data,Path.Combine(Application.temporaryCachePath,"damage-check-"+Guid.NewGuid()+".json"));
    p.Save.level=20;
    var stage=data.stage_list.OrderBy(s=>s.id).First();stage.enemyLevels=new[]{20};
    BattleEngine New(int bid,int target){stage.enemies=new[]{target};return new BattleEngine(p,stage.id,42,new[]{new PartyEntry{bid=bid,hp=1,max_hp=1,is_on_field=1,state=1}});}
    foreach(var boo in data.bangboo) {
     for(int slot=0;slot<3;slot++) {
      var battle=New(boo.id,boo.id);var skill=data.Skill(boo.skillIds[slot]);
      battle.Enemy.state=Condition.Frozen;battle.Enemy.stateTurns=2;
      battle.Player.hp=1000;int enemyHp=battle.Enemy.hp,pp=battle.Player.pp[slot];
      int expected=skill.effect=="heal"?0:battle.Damage(battle.Player,battle.Enemy,skill);
      battle.Act(slot);
      Check(battle.Player.pp[slot]==pp-1,"player PP "+skill.id);
      Check(battle.Enemy.hp==enemyHp-expected,"player actual HP delta "+skill.id);
      if(skill.effect=="heal")Check(battle.Player.hp==Math.Min(battle.Player.maxHp,1000+skill.dmg),"heal amount");
     }
     var enemyBattle=New(1,boo.id);enemyBattle.Player.state=Condition.Frozen;enemyBattle.Player.stateTurns=2;
     var best=boo.skillIds.Select(id=>data.Skill(id)).Where(s=>s.effect=="attack"||s.effect=="element").Max(s=>enemyBattle.Damage(enemyBattle.Enemy,enemyBattle.Player,s));
     int before=enemyBattle.Player.hp;enemyBattle.Act(0);
     // A burn may add end-of-round damage; require at least the exact selected attack damage.
     Check(enemyBattle.Player.hp<=before-best,"enemy attack must use workbook damage "+boo.id);
    }
    var live=New(1,2);live.Player.hp=700;live.Player.pp[0]--;int spent=live.Player.pp[0];
    var fresh=GameData.Load();fresh.Skill(1).dmg=777;fresh.Skill(1).charge_count+=2;
    live.ReloadSkills(fresh);
    Check(live.Player.hp==700&&live.Player.pp[0]==spent+2,"reload preserves HP and spent PP");
    Check(live.Damage(live.Player,live.Enemy,data.Skill(1))==1944,"reload next damage");
    return count;
   } finally {Debug.unityLogger.logEnabled=logging;}
  }
 }
}
