using System;
using System.Collections.Generic;
using System.Linq;
namespace BeastBeat {
 public sealed class BangbooInfo { public int id,type,hp,atk,def,release_lv; public string name; }
 public sealed class BangbooSkillInfo { public int id,bid,type,max_num; public string name; }
 public sealed class BangbooCatalog {
  public bool legacyUsers;
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
  public static BangbooCatalog Load(GameWorkbook book,int uid=1,bool includeUsers=true){
   var c=new BangbooCatalog();
   c.entries=Table(book,"bangboo","id","name","type","hp","atk","def","release_lv").Select(r=>new BangbooInfo{id=N(r,"id"),name=r["name"],type=N(r,"type"),hp=N(r,"hp"),atk=N(r,"atk"),def=N(r,"def"),release_lv=N(r,"release_lv")}).OrderBy(b=>b.id).ToArray();
   c.skills=Table(book,"skills","id","bid","name","type","max_num").Select(r=>new BangbooSkillInfo{id=N(r,"id"),bid=N(r,"bid"),name=r["name"],type=N(r,"type"),max_num=N(r,"max_num")}).OrderBy(s=>s.id).ToArray();
   var user=includeUsers?Table(book,"users","id").SingleOrDefault(r=>N(r,"id")==uid):null;
   if(includeUsers&&user==null)throw new FormatException("users: 현재 유저 없음");
   c.legacyUsers=user!=null&&!user.ContainsKey("has_bangboo_1")&&(user.ContainsKey("has_bangboo_21")||user.ContainsKey("has_bangboo_31"));
   var oldBoos=GameData.LoadBase().bangboo;
   foreach(var b in c.entries){
    if(b.release_lv<1||b.release_lv>20||b.id<=0||string.IsNullOrWhiteSpace(b.name)||b.type<1||b.type>5||b.hp<=0||b.atk<0||b.def<0||c.skills.Count(s=>s.bid==b.id)!=3)throw new FormatException("bangboo: 능력치 또는 스킬 3개 확인: "+b.id);
    int userId=b.id;if(c.legacyUsers){var previous=oldBoos.FirstOrDefault(x=>x.name==b.name);userId=previous==null?-1:previous.id;}
    string hasKey="has_bangboo_"+userId,lvKey="bangboo_"+userId+"_lv";
    bool has=user!=null&&user.ContainsKey(hasKey)&&B(user,hasKey);int level=user!=null&&user.ContainsKey(lvKey)?N(user,lvKey):0;
    if(level<0||level>20||(has&&level<1))throw new FormatException("users: 보유 방부 레벨은 1~20입니다.");
    c.levels[b.id]=level;if(has)c.owned.Add(b.id);
   }
   if(c.skills.Any(s=>!c.entries.Any(b=>b.id==s.bid)||s.id<=0||s.max_num<1||s.type<1||s.type>5||string.IsNullOrWhiteSpace(s.name)))throw new FormatException("skills: 방부 참조/속성/횟수 확인");return c;
  }
  public bool Has(ProgressService p,int id)=>owned.Contains(id)||p.Owns(id);
  public int Level(ProgressService p,int id)=>Math.Max(levels.TryGetValue(id,out var lv)?lv:0,p.Owns(id)?p.Owned(id).level:0);
  public static void Apply(GameData data,GameWorkbook book){
   var c=Load(book,1,false);var original=data.bangboo;var originalSkills=data.skills;
   // Runtime IDs distinguish retired definitions from the new workbook IDs.
   data.legacyBangbooIds=new Dictionary<int,int>();
   foreach(var old in original){var same=c.entries.FirstOrDefault(b=>b.name==old.name);data.legacyBangbooIds[old.id]=same==null?10000+old.id:same.id;}
   var boos=new List<BooData>();var skills=new List<SkillData>();
   foreach(var b in c.entries){
    var previous=original.FirstOrDefault(x=>x.name==b.name)??original.First(x=>x.type==b.type);
    var list=c.skills.Where(s=>s.bid==b.id).ToArray();
    boos.Add(new BooData{id=b.id,name=b.name,type=b.type,hp=b.hp,atk=b.atk,def=b.def,unlockLevel=b.release_lv,description=previous.description,skillIds=list.Select(s=>s.id).ToArray()});
    for(int i=0;i<3;i++){var template=originalSkills.First(s=>s.id==previous.skillIds[i]);var copy=UnityEngine.JsonUtility.FromJson<SkillData>(UnityEngine.JsonUtility.ToJson(template));copy.id=list[i].id;copy.bangboo_id=b.id;copy.name=list[i].name;copy.charge_count=list[i].max_num;skills.Add(copy);}
   }
   foreach(var old in original.Where(x=>data.legacyBangbooIds[x.id]>=10000)){
    int oldId=old.id;var copy=UnityEngine.JsonUtility.FromJson<BooData>(UnityEngine.JsonUtility.ToJson(old));copy.id=data.legacyBangbooIds[oldId];copy.skillIds=old.skillIds.Select(id=>10000+id).ToArray();boos.Add(copy);
    foreach(int id in old.skillIds){var skill=UnityEngine.JsonUtility.FromJson<SkillData>(UnityEngine.JsonUtility.ToJson(originalSkills.First(s=>s.id==id)));skill.id+=10000;skill.bangboo_id=copy.id;skills.Add(skill);}
   }
   // Existing stage enemy IDs still refer to the original definitions.
   foreach(var stage in data.stage_list)stage.enemies=stage.enemies.Select(id=>data.legacyBangbooIds.TryGetValue(id,out var mapped)?mapped:id).ToArray();
   data.bangboo=boos.ToArray();data.skills=skills.ToArray();
  }
  public static void UpgradeSave(GameData data,SaveData save){
   if(save.bangbooIdSchema>=1||data.legacyBangbooIds==null)return;
   foreach(var b in save.owned)if(data.legacyBangbooIds.TryGetValue(b.id,out var id))b.id=id;
   save.party=save.party.Select(id=>data.legacyBangbooIds.TryGetValue(id,out var mapped)?mapped:id).ToList();
   if(save.configuredParty!=null)save.configuredParty=save.configuredParty.Select(id=>data.legacyBangbooIds.TryGetValue(id,out var mapped)?mapped:id).ToArray();
   save.bangbooIdSchema=1;
  }
  public static int WorkbookPartyId(GameData data,GameWorkbook book,int id){
   var header=EventCatalog.ReadRows(book.ReadSheet("users"))[0];
   bool legacy=!header.Contains("has_bangboo_1")&&(header.Contains("has_bangboo_21")||header.Contains("has_bangboo_31"));
   return legacy&&data.legacyBangbooIds!=null&&data.legacyBangbooIds.TryGetValue(id,out var mapped)?mapped:id;
  }

  public static int[] Slots(ProgressService p,GameWorkbook book){
   if(p.Save.configuredParty!=null&&p.Save.configuredParty.Length==3)return (int[])p.Save.configuredParty.Clone();
   var rows=BattleCatalog.Load(book).party.Where(r=>r.uid==p.Data.rewardCatalog.user.id).OrderBy(r=>r.id).ToArray();var slots=new int[3];for(int i=0;i<Math.Min(3,rows.Length);i++)slots[i]=WorkbookPartyId(p.Data,book,rows[i].bid);return slots;
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
