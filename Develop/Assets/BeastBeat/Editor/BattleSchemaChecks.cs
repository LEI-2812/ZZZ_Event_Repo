using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BeastBeat.Editor
{
    public static class BattleSchemaChecks
    {
        [MenuItem("Beast Beat/검증/스킬·적 파티 데이터 테스트")]
        public static int Run()
        {
            int count = 0;
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
            var source = GameWorkbook.Load();
            var book = ScriptableObject.CreateInstance<GameWorkbook>();
            book.sheets = source.sheets.Select(s => new GameWorkbook.Sheet { name=s.name, records=s.records }).ToArray();
            try
            {
                // 사용자가 조정한 실제 밸런스와 무관하게 파서 입력을 고정합니다.
                var fixture=book.sheets.First(s=>s.name=="skills");
                var cells=EventCatalog.ReadRows(fixture.records);
                var header=cells[0];var row=cells.Skip(1).First(r=>r[header.IndexOf("id")]=="18");
                row[header.IndexOf("dmg")]="6";
                row[header.IndexOf(header.Contains("dbf_type")?"dbf_type":"debuff_type")]="CNF";
                row[header.IndexOf(header.Contains("dbf_prob")?"dbf_prob":"debuff_prob")]="35";
                fixture.records=string.Join("\n",cells.Select(r=>string.Join(",",r.Select(v=>"\""+v.Replace("\"","\"\"")+"\""))));
                var data = GameData.Load(book);
                Check(data.Skill(18).dmg == 6 && data.Skill(18).status == 6 && Mathf.Approximately(data.Skill(18).chance,.35f), "dbf columns not loaded");
                var sheet = book.sheets.First(s => s.name == "skills");
                sheet.records = sheet.records.Replace("dbf_type", "debuff_type").Replace("dbf_prob", "debuff_prob");
                var alias = GameData.Load(book);
                Check(alias.Skill(18).status == 6 && alias.Skill(18).chance == data.Skill(18).chance, "debuff aliases differ");
                var stageSheet = book.sheets.First(s => s.name == "stage_party");
                stageSheet.records = "id,stage_id,b1_id,b1_lv,b2_id,b2_lv,b3_id,b3_lv\n1,1,2,3,3,7,4,11";
                data = GameData.Load(book);
                Check(data.Stage(1).enemies.SequenceEqual(new[]{2,3,4}) && data.Stage(1).enemyLevels.SequenceEqual(new[]{3,7,11}), "stage party ids/levels");
                var p = new ProgressService(data, Path.Combine(Application.temporaryCachePath, "BattleSchema-"+Guid.NewGuid().ToString("N")+".json"));
                BattleEngine Battle()
                {
                    var b = new BattleEngine(p,1,4,new[]{new PartyEntry{bid=1,is_on_field=1,hp=10000,max_hp=10000,state=1}});
                    b.Player.atk=50;b.Player.def=50;b.Enemy.hp=b.Enemy.maxHp=10000;b.Enemy.def=50;
                    return b;
                }
                var battle = Battle();
                Check(battle.enemy.Select(f => f.level).SequenceEqual(new[]{3,7,11}), "individual enemy levels in engine");
                var skill = data.Skill(data.Boo(1).skillIds[0]); skill.dmg=100;skill.type=1;skill.effect="element";
                Check(battle.Damage(battle.Player,battle.Enemy,skill)==81, "damage uses level and attack-defense ratio");
                skill.type=3;Check(battle.Damage(battle.Player,battle.Enemy,skill)==43,"unfavorable skill type");
                skill.type=4;Check(battle.Damage(battle.Player,battle.Enemy,skill)==62,"neutral skill type");
                skill.type=1;skill.effect="attack";Check(battle.Damage(battle.Player,battle.Enemy,skill)==62,"normal attack affinity");
                battle.Enemy.def=100;Check(battle.Damage(battle.Player,battle.Enemy,skill)==32,"defense reduction");
                skill.status=6;skill.chance=1;
                var messages=Battle().Act(0);Check(messages.Any(m=>m.text.Contains("혼란 발생")),"100% debuff");
                skill.chance=0;messages=Battle().Act(0);Check(!messages.Any(m=>m.text.Contains("혼란 발생")),"0% debuff");
                bool cancelled=false;
                for(int seed=0;seed<30&&!cancelled;seed++)
                {
                    battle=new BattleEngine(p,1,seed,new[]{new PartyEntry{bid=1,is_on_field=1,hp=1000,max_hp=1000,state=1}});
                    battle.Player.state=Condition.Confused;battle.Player.stateTurns=2;battle.Player.def=50;
                    battle.Enemy.state=Condition.Frozen;
                    int pp=battle.Player.pp[0], hp=battle.Enemy.hp;
                    messages=battle.Act(0);
                    if(messages.Any(m=>m.text.Contains("기술이 취소"))) {
                        Check(battle.Player.hp==760 && battle.Player.pp[0]==pp && battle.Enemy.hp==hp,"confusion cancels cast and self hits 40");
                        cancelled=true;
                    }
                }
                Check(cancelled,"confusion branch not exercised");
                stageSheet.records="id,stage_id,b1_id,b1_lv\n1,1,999,3";
                bool rejected=false;try{GameData.Load(book);}catch(FormatException){rejected=true;}
                Check(rejected,"invalid enemy reference accepted");
                return count;
            }
            finally { UnityEngine.Object.DestroyImmediate(book); }
        }
    }
}
