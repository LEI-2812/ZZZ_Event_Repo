using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace BeastBeat.Editor
{
    public static class WorkbookOwnershipChecks
    {
        static byte[] Bytes(ZipArchiveEntry entry) { using(var s=entry.Open())using(var b=new MemoryStream()){s.CopyTo(b);return b.ToArray();} }
        public static int Run()
        {
            int count=0;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
            string source=Path.Combine(Application.dataPath,"Resources/Data/game_data.xlsx");
            byte[] original=File.ReadAllBytes(source);
            string dir=Path.Combine(Application.temporaryCachePath,"WorkbookUnlock-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string copy=Path.Combine(dir,"game_data.xlsx"),save=Path.Combine(dir,"save.json");File.WriteAllBytes(copy,original);
            var book=ScriptableObject.CreateInstance<GameWorkbook>();
            try {
                book.sheets=GameWorkbookImporter.Read(original);
                var data=GameData.Load(book);
                int[] ids=data.unlockableBangbooIds.Except(data.initialOwnedBangbooIds).Take(2).ToArray();
                if(ids.Length!=2)throw new Exception("Test needs two initially locked Bangboo.");
                foreach(int id in data.unlockableBangbooIds)data.Boo(id).unlockLevel=20;
                data.Boo(ids[0]).unlockLevel=2;data.Boo(ids[1]).unlockLevel=3;
                var stages=data.stage_list.OrderBy(s=>s.id).Take(2).ToArray();foreach(var stage in stages)stage.level_gain=1;
                var p=new ProgressService(data,save,copy);
                p.Victory(stages[0].id,new[]{1});
                Check(p.Save.level==2&&p.Save.pendingWorkbookUnlocks.Count==0&&p.WorkbookStorageWarning=="","level-up save did not complete");
                book.sheets=GameWorkbookImporter.Read(File.ReadAllBytes(copy));
                Check(BangbooCatalog.Load(book).owned.Contains(ids[0]),"saved XLSX flag is not True");
                var reloaded=GameData.Load(book);var restart=new ProgressService(reloaded,Path.Combine(dir,"fresh.json"));
                Check(restart.Save.level==1&&restart.Owns(ids[0]),"ownership was not restored from persisted True flag");
                byte[] first=File.ReadAllBytes(copy);
                using(var a=new ZipArchive(new MemoryStream(original),ZipArchiveMode.Read))
                using(var b=new ZipArchive(new MemoryStream(first),ZipArchiveMode.Read)) {
                    Check(a.Entries.Select(e=>e.FullName).SequenceEqual(b.Entries.Select(e=>e.FullName)),"ZIP entries/sheets changed");
                    var changed=a.Entries.Where(e=>!Bytes(e).SequenceEqual(Bytes(b.GetEntry(e.FullName)))).ToArray();
                    Check(changed.Length==1&&changed[0].FullName.StartsWith("xl/worksheets/"),"unrelated workbook structures changed");
                    var before=XDocument.Parse(System.Text.Encoding.UTF8.GetString(Bytes(changed[0])).TrimStart('\uFEFF'));
                    var after=XDocument.Parse(System.Text.Encoding.UTF8.GetString(Bytes(b.GetEntry(changed[0].FullName))).TrimStart('\uFEFF'));
                    XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var oldCells=before.Descendants(ns+"c").ToArray();var newCells=after.Descendants(ns+"c").ToArray();
                    Check(oldCells.Length==newCells.Length,"cell count changed");
                    int differences=0;
                    for(int i=0;i<oldCells.Length;i++)if(!XNode.DeepEquals(oldCells[i],newCells[i])) {
                        differences++;
                        Check((string)oldCells[i].Attribute("r")== (string)newCells[i].Attribute("r")&&(string)oldCells[i].Attribute("s")== (string)newCells[i].Attribute("s"),"cell address/style changed");
                        Check((string)newCells[i].Attribute("t")=="b"&&(string)newCells[i].Element(ns+"v")=="1","not a boolean True");
                    }
                    Check(differences==1,"unrelated cells changed");
                }
                WorkbookOwnershipStore.SaveUnlocked(copy,1,new[]{ids[0]});
                Check(first.SequenceEqual(File.ReadAllBytes(copy)),"duplicate unlock rewrote file");
                using(var locked=new FileStream(copy,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
                    p.Victory(stages[1].id,new[]{1});
                    Check(p.Save.level==3&&p.Save.pendingWorkbookUnlocks.Contains(ids[1])&&p.WorkbookStorageWarning.Length>0,"lock did not retain pending unlock");
                    var disk=JsonUtility.FromJson<SaveData>(File.ReadAllText(save));
                    Check(disk.level==3&&disk.pendingWorkbookUnlocks.Contains(ids[1]),"pending unlock not durable");
                }
                Check(first.SequenceEqual(File.ReadAllBytes(copy)),"locked workbook was changed");
                int rewardCount=p.Save.inventory.Sum(i=>i.amount);
                p=new ProgressService(data,save,copy);
                Check(p.RetryWorkbookUnlocks()&&p.Save.pendingWorkbookUnlocks.Count==0&&p.WorkbookStorageWarning=="","retry after restart failed");
                book.sheets=GameWorkbookImporter.Read(File.ReadAllBytes(copy));
                Check(BangbooCatalog.Load(book).owned.Contains(ids[1])&&p.Save.inventory.Sum(i=>i.amount)==rewardCount,"retry duplicated rewards or lost flag");
                byte[] final=File.ReadAllBytes(copy);bool rejected=false;
                try{WorkbookOwnershipStore.SaveUnlocked(copy,1,new[]{99999});}catch(FormatException){rejected=true;}
                Check(rejected&&final.SequenceEqual(File.ReadAllBytes(copy)),"invalid column damaged workbook");
                Check(original.SequenceEqual(File.ReadAllBytes(source)),"test changed live workbook");
                Debug.Log("Workbook ownership test copy: "+copy);
                return count;
            } finally { UnityEngine.Object.DestroyImmediate(book); }
        }
    }
}
