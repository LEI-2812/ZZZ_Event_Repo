using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BeastBeat.Editor
{
    public static class BattleGrowthChecks
    {
        public static int Run()
        {
            int count=0;
            void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
            bool logging=Debug.unityLogger.logEnabled;
            try {
                Debug.unityLogger.logEnabled=false;
                var data=GameData.Load();
                var p=new ProgressService(data,Path.Combine(Application.temporaryCachePath,"Growth-"+Guid.NewGuid().ToString("N"),"save.json"));
                var stage=data.stage_list.OrderBy(s=>s.id).First();
                int physical=data.bangboo.First(b=>b.id<10000&&b.type==1).id;
                int fire=data.bangboo.First(b=>b.id<10000&&b.type==2).id;
                int electric=data.bangboo.First(b=>b.id<10000&&b.type==4).id;
                stage.enemies=new[]{fire};stage.enemyLevels=new[]{10};
                var boo=data.Boo(physical);
                Check(p.Stat(boo,1,"hp")==800&&p.Stat(boo,1,"atk")==50&&p.Stat(boo,1,"def")==50,"level 1 stats");
                Check(p.Stat(boo,10,"hp")==1745&&p.Stat(boo,10,"atk")==125,"level 10 stats");
                Check(p.Stat(boo,20,"hp")==2795&&p.Stat(boo,20,"atk")==210&&p.Stat(boo,20,"def")==210,"level 20 stats");
                BattleEngine New(int seed=7,int level=10){p.Save.level=level;return new BattleEngine(p,stage.id,seed,new[]{new PartyEntry{bid=physical,hp=808,max_hp=808,is_on_field=1,state=1}});}
                foreach(var b in data.bangboo)foreach(int id in b.skillIds){var s=data.Skill(id);s.effect="guard";s.status=1;s.chance=0;}
                var battle=New();
                Check(battle.Player.maxHp==1745&&battle.Player.hp==1745&&battle.Player.atk==125&&battle.Player.def==125,"party old HP must not override formula");
                Check(battle.Enemy.maxHp==1745&&battle.Enemy.level==10,"enemy level stats");
                var skill=data.Skill(boo.skillIds[0]);skill.dmg=20;skill.type=1;skill.effect="attack";
                battle.Enemy.def=50;
                Check(battle.Damage(battle.Player,battle.Enemy,skill)==77,"float division and floor formula");
                skill.effect="element";Check(battle.Damage(battle.Player,battle.Enemy,skill)==100,"advantage after base formula");
                skill.type=3;Check(battle.Damage(battle.Player,battle.Enemy,skill)==54,"disadvantage after base formula");
                skill.type=4;Check(battle.Damage(battle.Player,battle.Enemy,skill)==77,"neutral affinity");
                skill.effect="attack";skill.type=1;battle=New(level:1);
                Check(battle.Damage(battle.Player,battle.Enemy,skill)==6,"low level fractional divisions");
                // 행동 자체가 피해를 주지 않는 guard로 상태이상의 효과를 분리합니다.
                skill.effect="guard";
                battle=New(level:20);battle.Player.state=Condition.Burn;battle.Player.stateTurns=2;
                battle.Act(0);Check(battle.Player.hp==2795-174,"burn max HP / 16");
                battle=New();battle.Player.state=Condition.Frozen;battle.Player.stateTurns=2;
                int pp=battle.Player.pp[0];battle.Act(0);
                Check(battle.Player.state==Condition.Frozen&&battle.Player.stateTurns==1&&battle.Player.pp[0]==pp,"freeze first blocked action");
                battle.Act(0);Check(battle.Player.state==Condition.Ready&&battle.Player.pp[0]==pp,"freeze second blocked action");
                battle.Act(0);Check(battle.Player.pp[0]==pp-1,"thawed action resumes");
                int par=0,confused=0;
                for(int seed=0;seed<1000;seed++){
                    battle=New(seed);battle.Player.state=Condition.Paralysis;battle.Player.stateTurns=2;
                    pp=battle.Player.pp[0];battle.Act(0);if(battle.Player.pp[0]==pp)par++;
                    battle=New(seed);battle.Player.state=Condition.Confused;battle.Player.stateTurns=2;
                    pp=battle.Player.pp[0];int hp=battle.Player.hp;battle.Act(0);
                    if(battle.Player.pp[0]==pp){confused++;if(battle.Player.hp!=hp-40)throw new Exception("confusion self damage must be 40");}
                }
                Check(par>=130&&par<=170,"paralysis 15% distribution: "+par);
                Check(confused>=310&&confused<=355,"confusion 33.3% distribution: "+confused);
                skill.effect="attack";skill.dmg=1;skill.status=2;skill.chance=1;
                stage.enemies=new[]{electric};battle=New();battle.Act(0);
                Check(battle.Enemy.state==Condition.Ready,"electric paralysis immunity");
                stage.enemies=new[]{fire};skill.status=4;skill.chance=0;battle=New();
                Check(!battle.Act(0).Any(m=>m.text.Contains("화상 발생")),"zero proc chance");
                skill.chance=1;battle=New();Check(battle.Act(0).Any(m=>m.text.Contains("화상 발생")),"100% proc chance");
                skill.status=3;bool one=false,two=false;
                for(int seed=0;seed<30;seed++){
                    battle=New(seed);int enemyPP=battle.Enemy.pp.Sum();battle.Act(0);
                    Check(battle.Enemy.pp.Sum()==enemyPP,"freeze blocks enemy's immediate action");
                    if(battle.Enemy.state==Condition.Ready)one=true;
                    if(battle.Enemy.state==Condition.Frozen&&battle.Enemy.stateTurns==1)two=true;
                }
                Check(one&&two,"freeze randomly chooses 1 or 2 blocked actions");
                return count;
            } finally { Debug.unityLogger.logEnabled=logging; }
        }
    }
}
