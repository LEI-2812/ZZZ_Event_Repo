using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace BeastBeat {
 public sealed class StageCatalogEntry {
  public int id,eventId,group,element,levelGain=1;
  public string npc,dialogue,team;
  public int[] rewardIds=Array.Empty<int>(),rewardAmounts=Array.Empty<int>();
 }
 public static class StageCatalog {
  public static readonly string[] Headers={"id","e_name","s_num","npc_name","npc_con","npc_type"};
  public static string GroupName(int group)=>new[]{"","워밍업","예선전","본선","결승전"}[group];
  public static List<StageCatalogEntry> Parse(string csv) {
   var rows=EventCatalog.ReadRows(csv);
   if(rows.Count==0)throw new FormatException("stage_list 헤더가 없습니다.");
   var header=rows[0].Select(s=>s.Trim().TrimStart('\uFEFF')).ToArray();
   header=header.Select(key=>key=="s_id"?"id":key).ToArray();
   if(header.Distinct().Count()!=header.Length||Headers.Any(key=>!header.Contains(key)))throw new FormatException("stage_list 필수 컬럼: "+string.Join(",",Headers));
   if(header.Contains("reward_ids")!=header.Contains("reward_amounts"))throw new FormatException("reward_ids와 reward_amounts는 함께 필요합니다.");
   var ids=new HashSet<int>();var result=new List<StageCatalogEntry>();
   for(int i=1;i<rows.Count;i++) {
    var row=rows[i];if(row.All(string.IsNullOrWhiteSpace))continue;
    try {
     if(row.Count!=header.Length)throw new FormatException("헤더와 데이터의 컬럼 개수가 다릅니다.");
     string Get(string key){int index=Array.IndexOf(header,key);return index<0?"":row[index].Trim();}
     int Number(string key)=>int.Parse(Get(key),CultureInfo.InvariantCulture);
     int[] Numbers(string key)=>string.IsNullOrWhiteSpace(Get(key))?Array.Empty<int>():Get(key).Split(';').Select(s=>int.Parse(s.Trim(),CultureInfo.InvariantCulture)).ToArray();
     var e=new StageCatalogEntry{id=Number("id"),eventId=Number("e_name"),group=Number("s_num"),npc=Get("npc_name"),dialogue=Get("npc_con").Replace("\\n","\n"),element=Number("npc_type"),team=Get("s_name"),levelGain=header.Contains("level_gain")?Number("level_gain"):1,rewardIds=Numbers("reward_ids"),rewardAmounts=Numbers("reward_amounts")};
     if(e.id<=0||!ids.Add(e.id))throw new FormatException("아이디는 중복 없는 양수여야 합니다.");
     if(e.eventId<=0||e.group<1||e.group>4||e.element<1||e.element>5)throw new FormatException("이벤트 아이디·종류·npc 속성을 확인하세요.");
     if(e.npc.Length==0)throw new FormatException("등장 npc가 비어 있습니다.");
     if(e.levelGain<0)throw new FormatException("level_gain은 0 이상의 정수여야 합니다.");
     if(e.rewardIds.Length!=e.rewardAmounts.Length||e.rewardIds.Any(id=>id<=0)||e.rewardAmounts.Any(n=>n<=0))throw new FormatException("보상 ID·수량은 같은 개수의 양수 목록이어야 합니다. 여러 값은 ;로 구분합니다.");
     if(e.rewardIds.Distinct().Count()!=e.rewardIds.Length)throw new FormatException("보상 아이템 ID가 중복되었습니다.");
     result.Add(e);
    }catch(Exception ex){throw new FormatException("stage_list "+(i+1)+"행: "+ex.Message,ex);}
   }
   return result.OrderBy(e=>e.id).ToList();
  }
  public static void Apply(GameData data,List<StageCatalogEntry> entries) {
   var originals=data.stage_list;var stages=new List<StageData>();var rewards=new List<RewardData>();
   foreach(var e in entries) {
    var original=originals.FirstOrDefault(s=>s.id==e.id);
    var template=original!=null&&original.type==e.group?original:originals.FirstOrDefault(s=>s.type==e.group)??originals.FirstOrDefault();
    int ordinal=entries.Count(x=>x.eventId==e.eventId&&x.group==e.group&&x.id<=e.id);
    int[] enemies=template==null?Array.Empty<int>():template.enemies.ToArray();
    if(template==null||template.npc_type!=e.element) {
     var candidates=data.bangboo.Where(b=>b.type==e.element).OrderBy(b=>b.id).Select(b=>b.id).ToArray();
     if(candidates.Length==0)throw new FormatException("해당 속성의 전투 방부가 없습니다: "+e.element);
     int count=template==null?Math.Min(3,e.group):template.enemies.Length;
     enemies=Enumerable.Range(0,count).Select(i=>candidates[i%candidates.Length]).ToArray();
    }
    stages.Add(new StageData{id=e.id,event_list_id=e.eventId,type=e.group,npc_type=e.element,name=string.IsNullOrWhiteSpace(e.team)?GroupName(e.group)+" 배틀 "+ordinal.ToString("00"):e.team,npc=e.npc,npc_dialogue=e.dialogue,level=template==null?1:template.level,level_gain=e.levelGain,enemies=enemies});
    for(int i=0;i<e.rewardIds.Length;i++) {
     int itemId=e.rewardIds[i];if(!data.items.Any(item=>item.id==itemId))throw new FormatException("stage_list "+e.id+": items에 없는 아이템 ID "+itemId);
     rewards.Add(new RewardData{id=rewards.Count+1,owner_id=e.id,items_id=itemId,amount=e.rewardAmounts[i]});
    }
   }
   data.stage_list=stages.ToArray();data.stage_rewards=rewards.ToArray();
  }
 }
}
