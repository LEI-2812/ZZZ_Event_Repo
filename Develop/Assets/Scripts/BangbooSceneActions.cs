using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
namespace BeastBeat {
 public sealed class BangbooSceneActions:MonoBehaviour {
  public GameWorkbook workbook;
  public Image portrait;
  public TMP_Text portraitName,infoName,levelText,typeText,hpText,atkText,defText,statusText;
  public TMP_Text[] skillNames,skillCounts,partyLabels;
  public Button backButton;public Button[] partyButtons;
  public BangbooCollectionRow[] rows;
  public string previousScene="Assets/Scenes/Main Scene.unity";
  public ProgressService Progress{get;private set;}public BangbooCatalog Catalog{get;private set;}
  public int SelectedId{get;private set;}
  BangbooPortraits portraits;readonly Dictionary<int,Sprite> sprites=new Dictionary<int,Sprite>();string revision;float nextRefresh;bool navigating;
  void Awake(){if(!enabled)return;try{if(BeastBeatSession.Progress==null)BeastBeatSession.Progress=new ProgressService(GameData.Load());Progress=BeastBeatSession.Progress;previousScene=BeastBeatSession.BangbooReturnScenePath;portraits=new BangbooPortraits();Reload();}catch(Exception e){ShowError(e);}}
  void Update(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.5f;if(workbook&&workbook.revision!=revision)try{Reload();}catch(Exception e){ShowError(e);}}
  void ShowError(Exception e){statusText.text="데이터 확인: "+e.Message;foreach(var b in partyButtons)b.interactable=false;Debug.LogError("Bangboo Scene: "+e.Message,this);}
  public void Reload(){
   workbook=workbook?workbook:GameWorkbook.Load();Catalog=BangbooCatalog.Load(workbook,Progress.Data.rewardCatalog.user.id);revision=workbook.revision;
   int wanted=SelectedId!=0?SelectedId:BeastBeatSession.SelectedBoo;SelectedId=Catalog.entries.Any(b=>b.id==wanted&&Catalog.Has(Progress,b.id))?wanted:Catalog.entries.Where(b=>Catalog.Has(Progress,b.id)).Select(b=>b.id).FirstOrDefault();
   Refresh();
  }
  public void SelectBangboo(int id){if(!Catalog.entries.Any(b=>b.id==id)||!Catalog.Has(Progress,id))return;SelectedId=id;BeastBeatSession.SelectedBoo=id;Refresh();}
  public void Refresh(){
   var slots=BangbooCatalog.Slots(Progress,workbook);
   for(int i=0;i<rows.Length;i++){var entry=i<Catalog.entries.Length?Catalog.entries[i]:null;rows[i].bid=entry==null?0:entry.id;rows[i].button.interactable=entry!=null&&Catalog.Has(Progress,entry.id);rows[i].label.text=entry==null?"—":entry.name+(Catalog.Has(Progress,entry.id)?"\nLv. "+Catalog.Level(Progress,entry.id):"\n미보유");}
   for(int i=0;i<3;i++){partyLabels[i].font=portraitName.font;partyLabels[i].text=(i+1)+"번\n"+(slots[i]==0?"비어 있음":Progress.Data.Boo(slots[i]).name);partyButtons[i].interactable=SelectedId!=0;}
   if(SelectedId==0){portraitName.text=infoName.text="보유 방부 없음";levelText.text="Lv. —";typeText.text=hpText.text=atkText.text=defText.text="—";foreach(var t in skillNames)t.text="—";foreach(var t in skillCounts)t.text="—";portrait.enabled=false;statusText.text="보유한 방부가 없습니다.";return;}
   var b=Catalog.entries.First(x=>x.id==SelectedId);int lv=Catalog.Level(Progress,b.id);
   portraitName.text=b.name;infoName.text=b.id.ToString("00")+" | "+b.name;levelText.text="Lv. "+lv;typeText.text=Elements.Name(b.type)+" 속성";hpText.text=b.hp.ToString();atkText.text=b.atk.ToString();defText.text=b.def.ToString();
   var skills=Catalog.skills.Where(s=>s.bid==b.id).ToArray();for(int i=0;i<3;i++){skillNames[i].text=skills[i].name;skillCounts[i].text=skills[i].max_num+" / "+skills[i].max_num;}
   int key=b.id+(lv>=Progress.Data.balance.evolutionLevel?1000:0);if(!sprites.TryGetValue(key,out var sprite)){var model=new BooData{id=b.id,name=b.name,type=b.type};var texture=portraits.Get(model,lv>=Progress.Data.balance.evolutionLevel);sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));sprites.Add(key,sprite);}portrait.sprite=sprite;portrait.enabled=true;
   statusText.text="방부 선택 후 위치 선택";
  }
  public void AssignFirst(){Assign(0);} public void AssignSecond(){Assign(1);} public void AssignThird(){Assign(2);}
  public void Assign(int slot){if(Catalog==null)return;bool success=Catalog.Assign(Progress,workbook,slot,SelectedId,out var message);if(success)Refresh();statusText.text=message;}
  public void GoBack(){if(navigating)return;if(!Application.CanStreamedLevelBeLoaded(previousScene)){statusText.text="이전 화면을 찾을 수 없습니다.";return;}navigating=true;SceneManager.LoadScene(previousScene);}
  void OnDestroy(){foreach(var sprite in sprites.Values)if(sprite)Destroy(sprite);portraits?.Dispose();}
 }
}
