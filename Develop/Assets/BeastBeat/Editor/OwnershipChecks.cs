using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BeastBeat.Editor
{
    public static class OwnershipChecks
    {
        public static int Run()
        {
            int count=0;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
            var source=GameWorkbook.Load();
            var book=ScriptableObject.CreateInstance<GameWorkbook>();
            book.sheets=source.sheets.Select(s=>new GameWorkbook.Sheet{name=s.name,records=s.records}).ToArray();
            try {
                var users=book.sheets.First(s=>s.name=="users");
                var rows=EventCatalog.ReadRows(users.records);
                for(int i=0;i<rows[0].Count;i++)if(rows[0][i].StartsWith("has_bangboo_"))rows[1][i]=rows[0][i]=="has_bangboo_1"?"True":"False";
                users.records=string.Join("\n",rows.Select(row=>string.Join(",",row.Select(v=>"\""+v.Replace("\"","\"\"")+"\""))));
                var data=GameData.Load(book);
                foreach(var boo in data.bangboo)boo.unlockLevel=20;
                data.Boo(2).unlockLevel=3;
                string path=Path.Combine(Application.temporaryCachePath,"Ownership-"+Guid.NewGuid().ToString("N"),"save.json");
                var p=new ProgressService(data,path);
                Check(p.Owns(1)&&!p.Owns(2)&&p.Save.owned.Count==1,"users True/False initial ownership");
                p.Save.owned.Add(new OwnedBoo{id=2,level=1});
                Check(!p.Owns(2),"stale saved ownership must not override users");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                p.Save.inventory.Add(new InventoryItem{id=1,amount=123});
                File.WriteAllText(path,JsonUtility.ToJson(p.Save));
                p=new ProgressService(data,path);
                Check(!p.Owns(2)&&p.Save.owned.All(b=>b.id!=2)&&p.Save.inventory.Single().amount==123,"legacy save migration preserves progress");
                var stage=data.stage_list.OrderBy(s=>s.id).First();stage.level_gain=2;
                var notes=p.Victory(stage.id,new[]{1});
                Check(p.Save.level==3&&p.Owns(2)&&p.Save.owned.Any(b=>b.id==2&&b.level==3),"level gain auto unlock");
                Check(notes.Any(n=>n.Contains("방부 해금")&&n.Contains(data.Boo(2).name)),"unlock result notification");
                p=new ProgressService(data,path);
                Check(p.Owns(2),"unlock survives restart from saved player level");
                int owned=p.Save.owned.Count;
                notes=p.Victory(stage.id,new[]{1});
                Check(p.Save.owned.Count==owned&&!notes.Any(n=>n.Contains("방부 해금")),"reclear does not duplicate unlock");
                data.initialOwnedBangbooIds=new[]{1,3};p.ReloadData(data);
                Check(p.Owns(3),"users can grant before release level");
                p.Save.configuredParty=new[]{1,3,0};p.Save.party=new System.Collections.Generic.List<int>{1,3};
                data.initialOwnedBangbooIds=new[]{1};p.ReloadData(data);
                Check(!p.Owns(3)&&p.Save.configuredParty[1]==0&&!p.Save.party.Contains(3),"users revocation removes stale ownership and invalid saved slot");
                Check(p.Owns(2),"level unlock remains when workbook flag is False");
                Check(data.bangboo.Where(b=>b.id>=10000).All(b=>!p.Owns(b.id)),"legacy enemy definitions never unlock");
                return count;
            } finally { UnityEngine.Object.DestroyImmediate(book); }
        }
    }
}
