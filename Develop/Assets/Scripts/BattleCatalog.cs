using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace BeastBeat {
 [Serializable] public sealed class PartyEntry { public int id,uid,bid,is_on_field,hp,state,max_hp; }
 [Serializable] public sealed class PauseEntry { public int id; public string name,info1,info2,info3,info4,info5,info6; }
 public sealed class BattleCatalog {
  GameWorkbook workbook;
  public PartyEntry[] party; public PauseEntry[] pause;
  static List<Dictionary<string,string>> Rows(GameWorkbook b,string name,params string[] required){
   var rows=EventCatalog.ReadRows(b.ReadSheet(name)).Where(r=>r.Any(v=>!string.IsNullOrWhiteSpace(v))).ToArray();
   if(rows.Length==0||required.Any(k=>!rows[0].Contains(k)))throw new FormatException(name+" 필수 컬럼 누락");
   var header=rows[0];if(header.Distinct().Count()!=header.Count)throw new FormatException(name+" 중복 컬럼");
   var result=rows.Skip(1).Select(row=>header.Select((key,i)=>new{key,value=i<row.Count?row[i]:""}).ToDictionary(x=>x.key,x=>x.value)).ToList();
   if(result.Select(r=>N(r,"id")).Distinct().Count()!=result.Count)throw new FormatException(name+" 중복 id");return result;
  }
  static int N(Dictionary<string,string> r,string k){if(!r.TryGetValue(k,out var s))return 0;if(!int.TryParse(s,out var n))throw new FormatException(k+" 정수 필요: "+s);return n;}
  public static BattleCatalog Load(GameWorkbook book=null){
   book=book?book:GameWorkbook.Load();if(!book)throw new FormatException("game_data.xlsx 누락");
   var c=new BattleCatalog{workbook=book};
   c.party=Rows(book,"party","id","uid","bid","is_on_field","hp","state").Select(r=>new PartyEntry{id=N(r,"id"),uid=N(r,"uid"),bid=N(r,"bid"),is_on_field=N(r,"is_on_field"),hp=N(r,"hp"),state=N(r,"state")}).OrderBy(r=>r.id).ToArray();
   c.pause=Rows(book,"pause_list","id","name","info1","info2","info3","info4","info5","info6").Select(r=>new PauseEntry{id=N(r,"id"),name=r["name"],info1=r["info1"],info2=r["info2"],info3=r["info3"],info4=r["info4"],info5=r["info5"],info6=r["info6"]}).OrderBy(r=>r.id).ToArray();return c;
  }
  public PartyEntry[] ForUser(ProgressService p){var rows=party.Where(r=>r.uid==p.Data.rewardCatalog.user.id).Select(r=>UnityEngine.JsonUtility.FromJson<PartyEntry>(UnityEngine.JsonUtility.ToJson(r))).ToArray();
   foreach(var row in rows){row.bid=BangbooCatalog.WorkbookPartyId(p.Data,workbook,row.bid);row.max_hp=p.Data.Boo(row.bid).hp;}
   if(p.Save.configuredParty!=null&&p.Save.configuredParty.Length==3){
    var ids=p.Save.configuredParty.Where(id=>id!=0).ToArray();
    if(ids.Any(id=>!p.Owns(id)))throw new FormatException("party: 보유하지 않은 방부");
    rows=ids.Select((id,i)=>{int hp=p.Stat(p.Data.Boo(id),p.Save.level,"hp");return new PartyEntry{id=i+1,uid=p.Data.rewardCatalog.user.id,bid=id,is_on_field=i==0?1:0,hp=hp,max_hp=hp,state=1};}).ToArray();
   }
   if(rows.Length<1||rows.Length>3||rows.Select(r=>r.bid).Distinct().Count()!=rows.Length)throw new FormatException("party: 현재 유저의 서로 다른 방부 1~3마리가 필요합니다.");
   if(rows.Count(r=>r.is_on_field==1)!=1||rows.Any(r=>r.is_on_field<0||r.is_on_field>1||p.Save.level<1||p.Save.level>p.Data.balance.maxLevel||r.hp<0||r.state<1||r.state>6||!p.Owns(r.bid)))throw new FormatException("party: 보유 방부·출전 표시·체력·상태와 플레이어 레벨을 확인하세요.");
   if(!rows.Any(r=>r.hp>0&&r.state!=5))throw new FormatException("party: 출전 가능한 방부가 없습니다.");return rows;}
  public static BattleEngine CreateBattle(ProgressService p,int stage){return new BattleEngine(p,stage,-1,Load().ForUser(p));}
 }
}
