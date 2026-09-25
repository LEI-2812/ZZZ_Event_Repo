using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class ClearSceneActions:MonoBehaviour
    {
        public GameWorkbook workbook;
        public Image[] partyImages;
        public TMP_Text[] levelLabels, levelUpLabels;
        public TMP_Text infoText, titleText;
        public Button okButton;

        public ResultPartyMember[] DisplayedParty {get;private set;}
        public BattleOutcome Outcome {get;private set;}
        public ProgressService Progress {get;private set;}
        readonly Dictionary<int,Sprite> portraitsById=new Dictionary<int,Sprite>();
        BangbooPortraits portraits;
        string revision;
        string workbookWarning;
        float nextRefresh;

        void Awake()
        {
            if(!enabled)return;
            try{
                if(BeastBeatSession.Progress==null)BeastBeatSession.Progress=new ProgressService(GameData.Load());
                var battle=BeastBeatSession.Battle;
                Outcome=battle==null?null:battle.Outcome;
                Progress=Outcome==null?BeastBeatSession.Progress:battle.Progress;
                portraits=new BangbooPortraits();Refresh();
            }catch(Exception ex){infoText.text="데이터 확인: "+ex.Message;Debug.LogError("Clear Scene: "+ex.Message,this);}
        }
        void Update()
        {
            // A completed battle keeps its participant snapshot. Direct scene preview follows workbook edits.
            if (Outcome != null) {
                if (workbookWarning != Progress.WorkbookStorageWarning) Refresh();
                return;
            }
            if(Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.5f;var book=workbook?workbook:GameWorkbook.Load();
            if(book&&book.revision!=revision){try{Refresh();}catch(Exception ex){infoText.text="데이터 확인: "+ex.Message;}}
        }
        public void Refresh()
        {
            workbookWarning = Progress.WorkbookStorageWarning;
            var book=workbook?workbook:GameWorkbook.Load();revision=book.revision;
            DisplayedParty=Outcome!=null?Outcome.Party:BattleCatalog.Load(book).ForUser(Progress).Select(p=>new ResultPartyMember{bid=p.bid,lv=Progress.Save.level,leveledUp=false}).ToArray();
            titleText.text="도전 결과";
            if(Outcome==null)infoText.text="파티 미리보기 · 완료된 배틀이 없습니다.";
            else{
                var npc=Progress.Data.Stage(Outcome.StageId).npc;
                if(npc.Length>4)npc=npc.Substring(0,4);
                infoText.text=Outcome.Won?"‘"+npc+"’과의 배틀에서 승리했다!":"‘"+npc+"’과의 배틀에서 패배했다.";
                if(Outcome.Preview)infoText.text+="\n연습 배틀 · 저장 및 보상 지급 없음";
                if (!string.IsNullOrEmpty(Progress.WorkbookStorageWarning)) infoText.text += "\n" + Progress.WorkbookStorageWarning;
            }
            for(int i=0;i<partyImages.Length;i++){
                bool filled=i<DisplayedParty.Length;partyImages[i].gameObject.SetActive(filled);
                if(levelUpLabels!=null&&i<levelUpLabels.Length&&levelUpLabels[i])levelUpLabels[i].gameObject.SetActive(filled&&DisplayedParty[i].leveledUp);
                if(!filled)continue;
                var member=DisplayedParty[i];levelLabels[i].text="Lv. "+member.lv;
                int key=member.bid+(member.lv>=Progress.Data.balance.evolutionLevel?1000:0);
                if(!portraitsById.TryGetValue(key,out var sprite)){
                    var texture=portraits.Get(Progress.Data.Boo(member.bid),member.lv>=Progress.Data.balance.evolutionLevel);
                    sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
                    sprite.name="Result portrait "+member.bid;portraitsById.Add(key,sprite);
                }
                partyImages[i].sprite=sprite;
            }
        }
        public void Confirm() => Move_Scene.For(this).Confirm();
        public bool TryPrepareReturn(out string message)
        {
            message = "";
            if (Outcome != null) {
                if (Outcome.Won && !Outcome.Preview && !Progress.TryPersist()) { message = Progress.StorageWarning; return false; }
                BeastBeatSession.SelectedStage = Outcome.StageId;
                BeastBeatSession.StageGroup = Progress.Data.Stage(Outcome.StageId).type;
            }
            BeastBeatSession.Battle = null;
            return true;
        }
        void OnDestroy(){foreach(var sprite in portraitsById.Values)if(sprite)Destroy(sprite);portraits?.Dispose();}
    }
}
