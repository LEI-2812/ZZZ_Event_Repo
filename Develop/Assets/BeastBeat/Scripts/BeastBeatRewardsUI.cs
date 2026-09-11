using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public partial class BeastBeatApp
    {
        string RewardText(IEnumerable<RewardData> rows) { return string.Join("   ·   ",rows.Select(r=>Data.items.First(x=>x.id==r.items_id).name+" ×"+r.amount)); }
        void Rewards(bool levels)
        {
            Header(levels?"레벨 보상":"업적 · 기간 한정 보상",()=>Show(previousScreen));
            Label(view,levels?"모든 레벨의 보상을 모아 타이틀을 획득하세요.":"기본 업적 보상은 상시 수령할 수 있습니다.",40,113,1080,49,25,Color.white,FontStyle.Bold);
            Button(view,"보유 보상 확인",1273,114,285,48,InventoryPopup,null,true,20);
            if(levels) {
                Button(view,"레벨 1–10",40,197,300,70,()=>{rewardGroup=0;Show("Levels");},rewardGroup==0?cyan:(Color?)null);
                Button(view,"레벨 11–20",40,287,300,70,()=>{rewardGroup=1;Show("Levels");},rewardGroup==1?cyan:(Color?)null);
            } else {
                Button(view,"리그 배틀",40,197,300,70,()=>{rewardGroup=0;Show("Rewards");},rewardGroup==0?cyan:(Color?)null);
                Button(view,"방부 수집 · 진화",40,287,300,70,()=>{rewardGroup=1;Show("Rewards");},rewardGroup==1?cyan:(Color?)null);
            }
            var ids=levels?Enumerable.Range(rewardGroup==0?1:11,10).ToArray():Data.achievement.Where(a=>rewardGroup==0?a.metric=="stage":a.metric!="stage").Select(a=>a.id).ToArray();
            var scroll=Scroll(view,369,197,1189,455,ids.Length*111);
            for(int i=0;i<ids.Length;i++) {
                int id=ids[i];string key=(levels?"l":"a")+id;bool done=Progress.Save.claimed.Contains(key),can=Progress.CanClaim(key);
                string title=levels?"LV. "+id.ToString("00")+" 보상":Data.achievement.First(a=>a.id==id).name;
                string progress=levels?"플레이어 LV."+Progress.Save.level:Math.Min(Progress.Metric(Data.achievement.First(a=>a.id==id)),Data.achievement.First(a=>a.id==id).target)+" / "+Data.achievement.First(a=>a.id==id).target;
                Box(scroll,"Reward row",0,i*111,1175,97,panel);Label(scroll,title+"   <color=#91A2B8>"+progress+"</color>",20,i*111+6,900,38,23,Color.white,FontStyle.Bold);
                Label(scroll,RewardText((levels?Data.level_rewards:Data.achievement_rewards).Where(x=>x.owner_id==id)),20,i*111+49,916,38,18,muted);
                Button(scroll,done?"수령 완료":can?"수령하기":"진행 중",964,i*111+18,191,60,()=>ClaimReward(key),can?yellow:(Color?)null,can,21);
            }
            string special=levels?"maxlevel":"special";bool claimed=Progress.Save.claimed.Contains(special);bool ready=Progress.CanClaim(special);
            Box(view,"Special reward",369,693,1189,155,panel);Tag(view,levels?"레벨 완료 보상":"이벤트 특별 보상",391,710,250,levels?cyan:pink);
            Label(view,levels?"비스트 마스터 타이틀":"한정 훈장 · 챔피언 라이브 배경",392,755,890,38,25,Color.white,FontStyle.Bold);
            Label(view,levels?"레벨 1–20의 모든 보상 수령 시 지급":"모든 업적 보상 수령 시 지급  ·  "+(Progress.LimitedActive?"체험 이벤트 남은 기간 "+Progress.DaysLeft+"일":"한정 보상 기간 종료"),392,803,900,29,18,muted);
            Button(view,claimed?"수령 완료":ready?"수령하기":"진행 중",1333,741,203,60,()=>ClaimReward(special),ready?yellow:(Color?)null,ready,22);
            Label(view,levels?"현재 레벨\nLV. "+Progress.Save.level:"도감 수집\n"+Progress.Save.owned.Count+" / 10",42,426,280,130,30,muted,FontStyle.Bold);
        }
        void ClaimReward(string key)
        {
            if(!Progress.Claim(key))return;
            Show(CurrentScreen);var p=OpenModal("보상 획득",800,340);
            string text=key=="special"?"비스트 비트 한정 훈장 ×1\n챔피언 라이브 배경 ×1":key=="maxlevel"?"비스트 마스터 타이틀 ×1":RewardText((key[0]=='l'?Data.level_rewards:Data.achievement_rewards).Where(r=>r.owner_id==int.Parse(key.Substring(1))));
            Label(p,text,35,100,730,105,26,yellow,FontStyle.Bold,TextAnchor.MiddleCenter);Button(p,"확인",35,255,730,50,CloseModal,cyan);Sound(winClip);
        }
        void InventoryPopup()
        {
            var p=OpenModal("획득한 보상",1050,650);var rows=Progress.Save.inventory.Where(x=>x.amount>0).ToArray();var scroll=Scroll(p,30,85,990,520,Mathf.Max(520,rows.Length*70));
            if(rows.Length==0)Label(scroll,"아직 획득한 보상이 없습니다.",20,20,950,60,25,muted);
            for(int i=0;i<rows.Length;i++){var item=Data.items.First(x=>x.id==rows[i].id);Color color=item.grade==1?yellow:item.grade==2?pink:item.grade==3?cyan:new Color(.65f,.85f,.67f);Tag(scroll,new[]{"","S","A","B","C"}[item.grade],15,i*70+9,42,color);Label(scroll,item.name,78,i*70,530,50,23);Label(scroll,"× "+rows[i].amount,825,i*70,140,50,24,yellow,FontStyle.Bold,TextAnchor.MiddleRight);
                if(item.id==11||item.id==12){int cosmetic=item.id;bool on=cosmetic==11?Progress.Save.liveBackground:Progress.Save.masterTitle;Button(scroll,on?"적용 중":"적용",635,i*70,160,50,()=>{if(cosmetic==11)Progress.Save.liveBackground=!Progress.Save.liveBackground;else Progress.Save.masterTitle=!Progress.Save.masterTitle;Progress.Persist();CloseModal();InventoryPopup();},on?(Color?)null:cyan,true,19);}
            }
        }
        RectTransform OpenModal(string title,float width,float height,[System.Runtime.CompilerServices.CallerMemberName]string owner="")
        {
            CloseModal();if(view!=null){var group=view.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;}
            if(!popupRoot){var go=new GameObject("Popups · Editable UI",typeof(RectTransform));go.transform.SetParent(canvas,false);popupRoot=(RectTransform)go.transform;popupRoot.anchorMin=Vector2.zero;popupRoot.anchorMax=Vector2.one;popupRoot.offsetMin=popupRoot.offsetMax=Vector2.zero;}
            string key=owner+" · "+title;modal=null;foreach(Transform child in popupRoot){var binding=child.GetComponent<EditableUINode>();if(binding&&binding.bindingKey=="Popup/"+key){modal=(RectTransform)child;break;}}
            if(!modal){var go=new GameObject(key,typeof(RectTransform),typeof(Image),typeof(EditableUINode));go.transform.SetParent(popupRoot,false);modal=(RectTransform)go.transform;modal.anchorMin=Vector2.zero;modal.anchorMax=Vector2.one;modal.offsetMin=modal.offsetMax=Vector2.zero;go.GetComponent<Image>().color=new Color(0,0,0,.78f);go.GetComponent<EditableUINode>().bindingKey="Popup/"+key;}
            BeginUI(modal);modal.gameObject.SetActive(true);
            var p=Box(modal,"Modal",0,0,width,height,panel);if(!p.GetComponent<EditableUINode>().authored){p.anchorMin=p.anchorMax=new Vector2(.5f,.5f);p.pivot=new Vector2(.5f,.5f);p.anchoredPosition=Vector2.zero;}
            Box(p,"Accent",0,0,width,5,cyan);Label(p,title,30,18,width-100,50,27,Color.white,FontStyle.Bold);Button(p,"×",width-65,17,45,45,CloseModal,null,true,27);
            return p;
        }
        void CloseModal() { paused=false;if(view!=null){var group=view.GetComponent<CanvasGroup>();if(group){group.interactable=true;group.blocksRaycasts=true;}}if(modal!=null){modal.gameObject.SetActive(false);modal=null;} }
        void Toast(string message)
        {
            var p=OpenModal("안내",750,280);Label(p,message,30,91,690,86,25,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);Button(p,"확인",30,199,690,50,CloseModal,cyan);
        }
        void OnApplicationPause(bool pausedApp) { if(pausedApp){Progress?.Persist();if(CurrentScreen=="Battle")Pause();} }
        void OnApplicationQuit() { Progress?.Persist(); }
    }
}
