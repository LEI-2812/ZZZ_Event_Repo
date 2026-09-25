using System;
using System.Collections.Generic;
using System.Linq;
namespace BeastBeat {
 public sealed class BangbooInfo { public int id,type,hp,atk,def,release_lv; public string name; }
 public sealed class BangbooSkillInfo { public int id,bid,type,max_num,dmg=-1,debuffType=1,debuffProb; public string name; }
 public sealed class BangbooCatalog {
  public BangbooInfo[] entries; public BangbooSkillInfo[] skills;
  public HashSet<int> owned=new HashSet<int>();
  static List<Dictionary<string,string>> Table(GameWorkbook book,string name,params string[] required){
   var rows=EventCatalog.ReadRows(book.ReadSheet(name)).Where(r=>r.Any(v=>!string.IsNullOrWhiteSpace(v))).ToArray();
   if(rows.Length==0||required.Any(k=>!rows[0].Contains(k))||rows[0].Distinct().Count()!=rows[0].Count)throw new FormatException(name+": 컬럼을 확인하세요.");
   var header=rows[0];var result=rows.Skip(1).Select(row=>header.Select((key,i)=>new{key,value=i<row.Count?row[i].Trim():""}).ToDictionary(x=>x.key,x=>x.value)).ToList();
   if(result.Select(r=>N(r,"id")).Distinct().Count()!=result.Count)throw new FormatException(name+": 중복 id");return result;
  }
  static int N(Dictionary<string,string> row,string key){if(!row.TryGetValue(key,out var v)||!int.TryParse(v,out var n))throw new FormatException(key+": 정수 필요");return n;}
  static int DamageValue(Dictionary<string,string> row){if(!row.ContainsKey("dmg"))return -1;int value=N(row,"dmg");if(value<0)throw new FormatException("skills.dmg는 0 이상의 정수여야 합니다.");return value;}
  static int Debuff(Dictionary<string,string> row){
   string key="dbf_type";
   if(!row.TryGetValue(key,out var value))return 1;
   switch(value.Trim().ToUpperInvariant()){
    case "NONE":case "0":case "1":case "":return 1;
    case "PAR":case "2":return 2;case "FRZ":case "3":return 3;
    case "BRN":case "4":return 4;case "CNF":case "6":return 6;
    default:throw new FormatException("skills: 지원하지 않는 상태이상 "+value);
   }
  }
  static bool B(Dictionary<string,string> row,string key){if(!row.TryGetValue(key,out var v))throw new FormatException(key+": 컬럼 누락");if(v=="1"||v.Equals("true",StringComparison.OrdinalIgnoreCase))return true;if(v=="0"||v.Equals("false",StringComparison.OrdinalIgnoreCase))return false;throw new FormatException(key+": True/False 필요");}
  public static BangbooCatalog Load(GameWorkbook book,int uid=1,bool includeUsers=true){
   var c=new BangbooCatalog();
   c.entries=Table(book,"bangboo","id","name","type","hp","atk","def","release_lv").Select(r=>new BangbooInfo{id=N(r,"id"),name=r["name"],type=N(r,"type"),hp=N(r,"hp"),atk=N(r,"atk"),def=N(r,"def"),release_lv=N(r,"release_lv")}).OrderBy(b=>b.id).ToArray();
   c.skills=Table(book,"skills","id","bid","name","type","max_num","dmg","dbf_type","dbf_prob").Select(r=>new BangbooSkillInfo{id=N(r,"id"),bid=N(r,"bid"),name=r["name"],type=N(r,"type"),max_num=N(r,"max_num"),dmg=DamageValue(r),debuffType=Debuff(r),debuffProb=N(r,"dbf_prob")}).OrderBy(s=>s.id).ToArray();
   var user=includeUsers?Table(book,"users","id").SingleOrDefault(r=>N(r,"id")==uid):null;
   if(includeUsers&&user==null)throw new FormatException("users: 현재 유저 없음");
   foreach(var b in c.entries){
    if(b.release_lv<1||b.release_lv>20||b.id<=0||string.IsNullOrWhiteSpace(b.name)||b.type<1||b.type>5||b.hp<=0||b.atk<0||b.def<0||c.skills.Count(s=>s.bid==b.id)!=3)throw new FormatException("bangboo: 능력치 또는 스킬 3개 확인: "+b.id);
    string hasKey="has_bangboo_"+b.id;
    bool has=user!=null&&user.ContainsKey(hasKey)&&B(user,hasKey);
    if(has)c.owned.Add(b.id);
   }
   if(c.skills.Any(s=>!c.entries.Any(b=>b.id==s.bid)||s.id<=0||s.max_num<1||s.dmg < -1||s.debuffProb<0||s.debuffProb>100||s.type<1||s.type>5||string.IsNullOrWhiteSpace(s.name)))throw new FormatException("skills: 방부 참조/속성/횟수 확인");return c;
  }
  public bool Has(ProgressService p,int id)=>p.Owns(id);
  public int Level(ProgressService p,int id)=>p.Save.level;
  public static void Apply(GameData data,GameWorkbook book){
   var c=Load(book,data.rewardCatalog == null ? 1 : data.rewardCatalog.user.id);var original=data.bangboo;var originalSkills=data.skills;
   data.initialOwnedBangbooIds=c.owned.ToArray();
   data.unlockableBangbooIds=c.entries.Select(b=>b.id).ToArray();
   // Runtime IDs distinguish retired definitions from the new workbook IDs.
   data.legacyBangbooIds=new Dictionary<int,int>();
   foreach(var old in original){var same=c.entries.FirstOrDefault(b=>b.name==old.name);data.legacyBangbooIds[old.id]=same==null?10000+old.id:same.id;}
   var boos=new List<BooData>();var skills=new List<SkillData>();
   foreach(var b in c.entries){
    var previous=original.FirstOrDefault(x=>x.name==b.name)??original.First(x=>x.type==b.type);
    var list=c.skills.Where(s=>s.bid==b.id).ToArray();
    boos.Add(new BooData{id=b.id,name=b.name,type=b.type,hp=b.hp,atk=b.atk,def=b.def,unlockLevel=b.release_lv,description=previous.description,skillIds=list.Select(s=>s.id).ToArray()});
    for(int i=0;i<3;i++){var template=originalSkills.First(s=>s.id==previous.skillIds[i]);var copy=UnityEngine.JsonUtility.FromJson<SkillData>(UnityEngine.JsonUtility.ToJson(template));copy.id=list[i].id;copy.bangboo_id=b.id;copy.name=list[i].name;copy.charge_count=list[i].max_num;
     copy.type=list[i].type;copy.dmg=list[i].dmg;copy.status=list[i].debuffType;copy.chance=list[i].debuffProb/100f;
     // 회복 기술을 제외한 2·3번 기술은 속성 기술이며 첫 번째 기술은 일반 공격입니다.
     if(list[i].dmg>=0)copy.effect=list[i].name=="응급수리"?"heal":i==0?"attack":"element";
     skills.Add(copy);}
   }
   foreach(var old in original.Where(x=>data.legacyBangbooIds[x.id]>=10000)){
    int oldId=old.id;var copy=UnityEngine.JsonUtility.FromJson<BooData>(UnityEngine.JsonUtility.ToJson(old));copy.id=data.legacyBangbooIds[oldId];copy.skillIds=old.skillIds.Select(id=>10000+id).ToArray();boos.Add(copy);
    // 옛 적의 표시 ID는 유지하되 기술은 현재 엑셀의 같은 속성 방부에서 가져옵니다.
    // stage_list에 직접 편성된 적은 해당 bid의 기술을 그대로 사용합니다.
    var source=boos.First(b=>b.id<10000&&b.type==old.type);
    for(int i=0;i<old.skillIds.Length;i++){
     var current=skills.First(s=>s.id==source.skillIds[i]);
     var skill=UnityEngine.JsonUtility.FromJson<SkillData>(UnityEngine.JsonUtility.ToJson(current));
     skill.id=copy.skillIds[i];skill.bangboo_id=copy.id;skills.Add(skill);
    }
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
  public static int WorkbookPartyId(GameData data,GameWorkbook book,int id) => id;

  public static int[] Slots(ProgressService p,GameWorkbook book){
   if(p.Save.configuredParty!=null&&p.Save.configuredParty.Length==3)return (int[])p.Save.configuredParty.Clone();
   var rows=BattleCatalog.Load(book).party.Where(r=>r.uid==p.Data.rewardCatalog.user.id).OrderBy(r=>r.id).ToArray();var slots=new int[3];for(int i=0;i<Math.Min(3,rows.Length);i++)slots[i]=WorkbookPartyId(p.Data,book,rows[i].bid);return slots;
  }
  public bool Assign(ProgressService p,GameWorkbook book,int slot,int id,out string message){
   message="";if(slot<0||slot>2||!entries.Any(b=>b.id==id)||!Has(p,id)){message="보유한 방부를 선택하세요.";return false;}
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
