using System;
using System.Collections.Generic;
using System.Linq;
namespace BeastBeat {
 public sealed class BangbooInfo { public int id,type,hp,atk,def; public string name; }
 public sealed class BangbooSkillInfo { public int id,bid,type,max_num; public string name; }
 public sealed class BangbooCatalog {
  public BangbooInfo[] entries; public BangbooSkillInfo[] skills;
  public Dictionary<int,int> levels=new Dictionary<int,int>();
  public HashSet<int> owned=new HashSet<int>();
  static List<Dictionary<string,string>> Table(GameWorkbook book,string name,params string[] required){
   var rows=EventCatalog.ReadRows(book.ReadSheet(name)).Where(r=>r.Any(v=>!string.IsNullOrWhiteSpace(v))).ToArray();
   if(rows.Length==0||required.Any(k=>!rows[0].Contains(k))||rows[0].Distinct().Count()!=rows[0].Count)throw new FormatException(name+": 컬럼을 확인하세요.");
   var header=rows[0];var result=rows.Skip(1).Select(row=>header.Select((key,i)=>new{key,value=i<row.Count?row[i].Trim():""}).ToDictionary(x=>x.key,x=>x.value)).ToList();
   if(result.Select(r=>N(r,"id")).Distinct().Count()!=result.Count)throw new FormatException(name+": 중복 id");return result;
  }
  static int N(Dictionary<string,string> row,string key){if(!row.TryGetValue(key,out var v)||!int.TryParse(v,out var n))throw new FormatException(key+": 정수 필요");return n;}
  static bool B(Dictionary<string,string> row,string key){if(!row.TryGetValue(key,out var v))throw new FormatException(key+": 컬럼 누락");if(v=="1"||v.Equals("true",StringComparison.OrdinalIgnoreCase))return true;if(v=="0"||v.Equals("false",StringComparison.OrdinalIgnoreCase))return false;throw new FormatException(key+": True/False 필요");}
  public static BangbooCatalog Load(GameWorkbook book,int uid=1){
   var c=new BangbooCatalog();
   c.entries=Table(book,"bangboo","id","name","type","hp","atk","def").Select(r=>new BangbooInfo{id=N(r,"id"),name=r["name"],type=N(r,"type"),hp=N(r,"hp"),atk=N(r,"atk"),def=N(r,"def")}).OrderBy(b=>b.id).ToArray();
   c.skills=Table(book,"skills","id","bid","name","type","max_num").Select(r=>new BangbooSkillInfo{id=N(r,"id"),bid=N(r,"bid"),name=r["name"],type=N(r,"type"),max_num=N(r,"max_num")}).OrderBy(s=>s.id).ToArray();
   var user=Table(book,"users","id").SingleOrDefault(r=>N(r,"id")==uid)??throw new FormatException("users: 현재 유저 없음");
   foreach(var b in c.entries){
    if(b.id<=0||string.IsNullOrWhiteSpace(b.name)||b.type<1||b.type>5||b.hp<=0||b.atk<0||b.def<0||c.skills.Count(s=>s.bid==b.id)!=3)throw new FormatException("bangboo: 능력치 또는 스킬 3개 확인: "+b.id);
    bool has=B(user,"has_bangboo_"+b.id);int level=N(user,"bangboo_"+b.id+"_lv");
    if(level<0||level>20||(has&&level<1))throw new FormatException("users: 보유 방부 레벨은 1~20입니다.");
    c.levels[b.id]=level;if(has)c.owned.Add(b.id);
   }
   if(c.skills.Any(s=>!c.entries.Any(b=>b.id==s.bid)||s.id<=0||s.max_num<1||s.type<1||s.type>5||string.IsNullOrWhiteSpace(s.name)))throw new FormatException("skills: 방부 참조/속성/횟수 확인");return c;
  }
  public bool Has(ProgressService p,int id)=>owned.Contains(id)||p.Owns(id);
  public int Level(ProgressService p,int id)=>Math.Max(levels[id],p.Owns(id)?p.Owned(id).level:0);
  public static void Apply(GameData data,GameWorkbook book){
   var c=Load(book);foreach(var b in c.entries){
    var target=data.bangboo.FirstOrDefault(x=>x.id==b.id);if(target==null)throw new FormatException("bangboo: 모델 정보가 없는 id "+b.id);
    target.name=b.name;target.type=b.type;target.hp=b.hp;target.atk=b.atk;target.def=b.def;
    var list=c.skills.Where(s=>s.bid==b.id).ToArray();
    // Keep the existing battle effects while sourcing names and charges from the workbook.
    for(int i=0;i<3;i++){var s=list[i];var original=data.Skill(target.skillIds[i]);original.name=s.name;original.charge_count=s.max_num;}
   }
  }
  public static int[] Slots(ProgressService p,GameWorkbook book){
   if(p.Save.configuredParty!=null&&p.Save.configuredParty.Length==3)return (int[])p.Save.configuredParty.Clone();
   var rows=BattleCatalog.Load(book).party.Where(r=>r.uid==p.Data.rewardCatalog.user.id).OrderBy(r=>r.id).ToArray();var slots=new int[3];for(int i=0;i<Math.Min(3,rows.Length);i++)slots[i]=rows[i].bid;return slots;
  }
  public bool Assign(ProgressService p,GameWorkbook book,int slot,int id,out string message){
   message="";if(slot<0||slot>2||!entries.Any(b=>b.id==id)||!Has(p,id)){message="보유한 방부를 선택하세요.";return false;}
   if(Level(p,id)>p.Save.level){message="방부 레벨이 플레이어 레벨보다 높습니다.";return false;}
   var old=UnityEngine.JsonUtility.ToJson(p.Save);var slots=Slots(p,book);int existing=Array.IndexOf(slots,id);int replaced=slots[slot];
   if(existing>=0&&existing!=slot)slots[existing]=replaced;slots[slot]=id;
   foreach(int bid in slots.Where(b=>b!=0)){if(!Has(p,bid)){message="파티의 미보유 방부를 확인하세요.";return false;}}
   foreach(int bid in slots.Where(b=>b!=0)){if(!p.Owns(bid))p.Save.owned.Add(new OwnedBoo{id=bid,level=Level(p,bid)});}
   p.Save.configuredParty=slots;p.Save.party=slots.Where(b=>b!=0).ToList();
   if(!p.TryPersist()){message=p.StorageWarning;p.Save=UnityEngine.JsonUtility.FromJson<SaveData>(old);return false;}
   message=(slot+1)+"번 파티에 "+entries.First(b=>b.id==id).name+" 배치 완료";return true;
  }
 }
}
