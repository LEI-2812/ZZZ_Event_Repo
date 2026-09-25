using System.Collections.Generic;
using System.Linq;
namespace BeastBeat
{
    public sealed class ResultPartyMember
    {
        public int bid, lv;
        public bool leveledUp;
    }
    public sealed class BattleOutcome
    {
        public bool Won, Preview;
        public int StageId;
        public ResultPartyMember[] Party;
        public List<string> Notes;
        // Finalize once per battle. Reopening the result or clicking OK never grants rewards again.
        public static BattleOutcome Complete(BattleEngine battle,bool preview)
        {
            if(battle.Outcome!=null)return battle.Outcome;
            if(!battle.Finished)throw new System.InvalidOperationException("배틀이 아직 끝나지 않았습니다.");
            var p=battle.Progress;
            var before=battle.player.ToDictionary(f=>f.id,f=>f.level);
            var notes=preview?new List<string>{"연습 배틀입니다. 저장 데이터와 보상은 변경되지 않습니다."}:battle.Won?p.Victory(battle.Stage.id,battle.player.Select(f=>f.id)):new List<string>{"패배 시 레벨 상승과 보상은 없습니다."};
            battle.Outcome=new BattleOutcome{
                Won=battle.Won,Preview=preview,StageId=battle.Stage.id,Notes=notes,
                Party=battle.player.Select(f=>new ResultPartyMember{
                    bid=f.id,lv=!preview&&battle.Won?p.Save.level:f.level,
                    leveledUp=!preview&&battle.Won&&p.Save.level>before[f.id]
                }).ToArray()
            };
            return battle.Outcome;
        }
    }
}
