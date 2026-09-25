using System;
using System.Collections.Generic;
using System.Linq;

namespace BeastBeat
{
    public static class StagePartyCatalog
    {
        // stage_list에서 각 스테이지의 방부 ID와 공통 레벨을 읽습니다.
        // 편성이 누락되면 초기 데이터로 대체하지 않고 오류를 표시합니다.
        public static void Apply(GameData data, GameWorkbook book)
        {
            // 삭제된 별도 편성 시트는 참조하지 않습니다.
            var stageRows=EventCatalog.ReadRows(book.ReadSheet("stage_list"));
            var stageHeader=stageRows[0].Select(v=>v.Trim().TrimStart('\uFEFF')).ToArray();
            var partyKeys=new[]{"b1_id","b2_id","b3_id","b_lv"};
            if(partyKeys.Any(stageHeader.Contains)) {
                if(!partyKeys.All(stageHeader.Contains))throw new FormatException("stage_list: b1_id, b2_id, b3_id, b_lv 컬럼 필요");
                foreach(var row in stageRows.Skip(1).Where(r=>r.Any(v=>!string.IsNullOrWhiteSpace(v)))) {
                    int N(string key){int i=Array.IndexOf(stageHeader,key);string v=i<row.Count?row[i].Trim():"";if(v=="")return 0;if(!int.TryParse(v,out int n))throw new FormatException("stage_list "+key+": 정수 필요");return n;}
                    int id=N(stageHeader.Contains("s_id")?"s_id":"id"),level=N("b_lv");var stage=data.stage_list.Single(s=>s.id==id);
                    var members=new List<int>();bool empty=false;
                    foreach(var key in partyKeys.Take(3)) {
                        int bid=N(key);if(bid==0){empty=true;continue;}
                        if(empty||!data.bangboo.Any(b=>b.id==bid&&b.id<10000))throw new FormatException("stage_list "+id+": "+key+" 방부 ID 또는 빈 슬롯 순서 확인");
                        members.Add(bid);
                    }
                    if(members.Count==0||level<1||level>data.balance.maxLevel)throw new FormatException("stage_list "+id+": 적 편성과 b_lv 확인");
                    stage.enemies=members.ToArray();stage.enemyLevels=members.Select(_=>level).ToArray();stage.level=level;
                }
                return;
            }
            throw new FormatException("stage_list: b1_id, b2_id, b3_id, b_lv 컬럼 필요");
        }
    }
}
