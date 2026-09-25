using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
namespace BeastBeat.Editor {
 public static class WorkbookV2Checks {
  public static int Run(){
   int count=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);count++;}
   var original=File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/Data/game_data.xlsx"));
   var folder=Path.Combine(Application.temporaryCachePath,"WorkbookV2-"+Guid.NewGuid());Directory.CreateDirectory(folder);
   string copy=Path.Combine(folder,"game_data.xlsx"),save=Path.Combine(folder,"save.json");File.WriteAllBytes(copy,original);
   var book=ScriptableObject.CreateInstance<GameWorkbook>();bool logging=Debug.unityLogger.logEnabled;
   try {
    Debug.unityLogger.logEnabled=false;
    GameData Load(){book.sheets=GameWorkbookImporter.Read(File.ReadAllBytes(copy));return GameData.Load(book);}
    var data=Load();data.Validate();
    foreach(var sheet in book.sheets){var rows=EventCatalog.ReadRows(sheet.records);Check(rows[0][0]=="id","second row header "+sheet.name);Check(rows.Count==1||int.TryParse(rows[1][0],out _),"fourth row starts data "+sheet.name);}
    Check(!book.sheets.Any(s=>s.name=="stage_party"),"removed sheet");
    Check(data.rewardCatalog.user.level==3,"users level read");
    Check(data.Stage(1).enemies.SequenceEqual(new[]{1,2,7})&&data.Stage(1).enemyLevels.SequenceEqual(new[]{1,1,1}),"stage party columns");
    var events=EventCatalog.Parse(book.ReadSheet("event_list"));Check(events.First(e=>e.id==1).rewardIds.Length==6&&events.First(e=>e.id==1).achievementIds.Length==3,"singular reward IDs");
    var p=new ProgressService(data,save,copy);
    Check(p.Save.level==3,"level source");Check(!p.CanClaim("ar1")&&!p.CanClaim("a1"),"undefined achievement disabled");
    var party=BattleCatalog.Load(book).ForUser(p);Check(party.All(r=>r.max_hp==data.Boo(r.bid).hp),"max HP from bangboo");
    // 파일 잠금 중의 레벨·해금을 JSON에 보존하고 재시작 후 다시 저장합니다.
    using(var held=new FileStream(copy,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
     p.Victory(1,party.Select(r=>r.bid));
     Check(p.Save.level==4&&p.Save.pendingWorkbookLevel==4,"pending level survives lock");
     Check(File.ReadAllText(save).Contains("pendingWorkbookLevel"),"pending durable JSON");
    }
    var pending=new ProgressService(Load(),save,copy);Check(pending.Save.level==4,"restart keeps pending level");
    Check(pending.RetryWorkbookUnlocks()&&pending.Save.pendingWorkbookLevel==0,"retry succeeds");
    var after=Load();Check(after.rewardCatalog.user.level==4,"xlsx level persisted");
    Check(after.unlockableBangbooIds.Where(id=>after.Boo(id).unlockLevel<=4).All(id=>after.initialOwnedBangbooIds.Contains(id)),"unlocks persisted");
    // Only the users XML entry may change; all other sheets/styles/table definitions must stay byte-identical.
    using(var beforeZip=new ZipArchive(new MemoryStream(original),ZipArchiveMode.Read))
    using(var afterZip=new ZipArchive(File.OpenRead(copy),ZipArchiveMode.Read)){
     int changed=0;foreach(var entry in beforeZip.Entries){using(var a=entry.Open())using(var b=afterZip.GetEntry(entry.FullName).Open())using(var ma=new MemoryStream())using(var mb=new MemoryStream()){a.CopyTo(ma);b.CopyTo(mb);if(!ma.ToArray().SequenceEqual(mb.ToArray())){changed++;var x=XDocument.Load(new MemoryStream(mb.ToArray()));XNamespace n="http://schemas.openxmlformats.org/spreadsheetml/2006/main";var prior=XDocument.Load(new MemoryStream(ma.ToArray()));foreach(int r in new[]{1,2,3})Check(XNode.DeepEquals(prior.Descendants(n+"row").Single(row=>(int)row.Attribute("r")==r),x.Descendants(n+"row").Single(row=>(int)row.Attribute("r")==r)),"description/header/type preserved");}}}Check(changed==1,"only users XML changed");
    }
    Check(File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/Data/game_data.xlsx")).SequenceEqual(original),"source workbook unchanged");
    return count;
   }finally{Debug.unityLogger.logEnabled=logging;UnityEngine.Object.DestroyImmediate(book);}
  }
 }
}
