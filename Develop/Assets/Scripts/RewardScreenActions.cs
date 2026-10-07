using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class RewardScreenActions:MonoBehaviour
    {
        public bool levelMode;
        public GameWorkbook workbook;
        public ScrollRect nameScroll,infoScroll;
        public RewardListRow groupPrefab,infoPrefab;
        public Button specialButton;
        public TMP_Text specialButtonText,specialCount,specialCondition,specialItems,timeText,statusText;

        [SerializeField] int selectedGroup;
        string revision,saveState;
        float nextRefresh;
        bool valid;
        ProgressService progress;
        public GameData Data=>progress.Data;
        public ProgressService Progress=>progress;
        public int SelectedGroup=>selectedGroup;
        void Awake(){if(!enabled)return;if(BeastBeatSession.Progress==null)BeastBeatSession.Progress=new ProgressService(GameData.Load());progress=BeastBeatSession.Progress;selectedGroup=0;ReloadWorkbook();}
        void Update(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.5f;var source=workbook?workbook:GameWorkbook.Load();if((source?source.revision:"")!=revision)ReloadWorkbook();else if(valid&&JsonUtility.ToJson(progress.Save)!=saveState)Refresh();else if(valid)UpdateTime();}
        [ContextMenu("Reload Workbook / Update Scene Preview")]
        public void ReloadWorkbook()
        {
            var source=workbook?workbook:GameWorkbook.Load();revision=source?source.revision:"";
            try{
                if(!source)throw new FormatException("game_data.xlsx 누락");var fresh=GameData.Load(source);fresh.Validate();
                if(progress==null)progress=Application.isPlaying?BeastBeatSession.Progress:new ProgressService(fresh);
                if(progress==null)progress=new ProgressService(fresh);
                progress.Data.items=fresh.items;progress.Data.stage_list=fresh.stage_list;progress.Data.stage_rewards=fresh.stage_rewards;progress.Data.achievement=fresh.achievement;progress.Data.achievement_rewards=fresh.achievement_rewards;progress.Data.level_rewards=fresh.level_rewards;progress.Data.rewardCatalog=fresh.rewardCatalog;
                valid=true;statusText.text="";Refresh();
            }catch(Exception ex){valid=false;specialButton.interactable=false;foreach(var row in infoScroll.content.GetComponentsInChildren<RewardListRow>(true))row.actionButton.interactable=false;statusText.text="데이터 확인: "+ex.Message;Debug.LogError("Reward XLSX: "+ex.Message,this);}
        }
        RewardListRow[] Pool(ScrollRect scroll,RewardListRow prefab,int count)
        {
            var rows=scroll.content.GetComponentsInChildren<RewardListRow>(true).ToList();
            while(rows.Count<count){RewardListRow row;
                #if UNITY_EDITOR
                if(!Application.isPlaying){var go=(GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab.gameObject,scroll.content);UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Add reward row");row=go.GetComponent<RewardListRow>();}else
                #endif
                row=Instantiate(prefab,scroll.content);rows.Add(row);
            }
            for(int i=0;i<rows.Count;i++)rows[i].gameObject.SetActive(i<count);
            return rows.Take(count).ToArray();
        }
        public void SelectGroup(int id){if(!valid)return;selectedGroup=id;Refresh();infoScroll.StopMovement();infoScroll.verticalNormalizedPosition=1;}
        void Refresh()
        {
            int[] groups=levelMode?Data.level_rewards.Select(r=>(r.owner_id-1)/10).Distinct().OrderBy(x=>x).ToArray():Data.achievement.Select(a=>a.id).OrderBy(x=>x).ToArray();
            if(!groups.Contains(selectedGroup))selectedGroup=groups.Length==0?-1:groups[0];
            var groupRows=Pool(nameScroll,groupPrefab,groups.Length);
            for(int i=0;i<groups.Length;i++){
                int group=groups[i],count,total;string title;
                if(levelMode){var levels=Data.level_rewards.Where(r=>(r.owner_id-1)/10==group).Select(r=>r.owner_id).Distinct().ToArray();count=levels.Count(l=>RewardClaims.Claimed(progress,"l"+l));total=levels.Length;title="레벨 "+(group*10+1)+" - "+Math.Min(group*10+10,Data.balance.maxLevel);}
                else {var rewards=Data.rewardCatalog.rows.Where(r=>r.a_id==group).ToArray();count=rewards.Count(r=>RewardClaims.Claimed(progress,"ar"+r.id));total=rewards.Length;title=Data.achievement.First(a=>a.id==group).name;}
                groupRows[i].Group(this,group,title,count+" / "+total,selectedGroup==group);
            }
            if(levelMode){var levels=Data.level_rewards.Where(r=>(r.owner_id-1)/10==selectedGroup).GroupBy(r=>r.owner_id).OrderBy(g=>g.Key).ToArray();var rows=Pool(infoScroll,infoPrefab,levels.Length);
                for(int i=0;i<levels.Length;i++){var l=levels[i];var key="l"+l.Key;rows[i].Reward(this,key,"LV. "+l.Key.ToString("00")+" 보상",l.ToArray(),"현재 LV. "+progress.Save.level,progress.CanClaim(key),RewardClaims.Claimed(progress,key));}
            }else {var rewards=Data.rewardCatalog.rows.Where(r=>r.a_id==selectedGroup).OrderBy(r=>r.id).ToArray();var rows=Pool(infoScroll,infoPrefab,rewards.Length);
                for(int i=0;i<rewards.Length;i++){var reward=rewards[i];var a=Data.achievement.First(x=>x.id==reward.a_id);var key="ar"+reward.id;rows[i].Reward(this,key,reward.info,reward.Items,"조건 준비 중",progress.CanClaim(key),RewardClaims.Claimed(progress,key));}
            }
            string specialKey=levelMode?"maxlevel":"special";bool claimed=RewardClaims.Claimed(progress,specialKey),complete=RewardClaims.Complete(progress,levelMode);
            specialButton.interactable=progress.CanClaim(specialKey);specialButtonText.text=claimed?"수령 완료":specialButton.interactable?"수령하기":"진행 중";
            specialCount.text=(complete||claimed?"1":"0")+" / 1";specialCondition.text=complete||claimed?(levelMode?"모든 레벨 완료":"모든 배틀 완료"):"";
            specialItems.richText=true;
            specialItems.text=string.Join("\n\n",Data.rewardCatalog.special[specialKey].Select(r=>{var item=Data.items.First(i=>i.id==r.items_id);return "<color=#"+ColorUtility.ToHtmlStringRGB(ItemCatalog.GradeColor(item.grade))+">"+ItemCatalog.DisplayName(item)+" ×"+r.amount+"</color>";}));
            if(groups.Length==0)statusText.text="등록된 보상이 없습니다.";
            LayoutRebuilder.ForceRebuildLayoutImmediate(nameScroll.content);LayoutRebuilder.ForceRebuildLayoutImmediate(infoScroll.content);saveState=JsonUtility.ToJson(progress.Save);UpdateTime();
            #if UNITY_EDITOR
            if(!Application.isPlaying)UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            #endif
        }
        void UpdateTime(){var end=DateTime.Parse(progress.Save.eventStartedUtc,null, System.Globalization.DateTimeStyles.RoundtripKind).AddDays(Data.event_list.demoLimitedDays);var remaining=end-DateTime.UtcNow;timeText.text=Data.rewardCatalog.user.name+"  ·  "+(remaining.TotalSeconds<=0?"기간 종료":"남은 시간 "+remaining.Days+"일 "+remaining.Hours+"시간");}
        public void ClaimRow(string key){if(!Application.isPlaying||!valid)return;bool claimed=progress.Claim(key);statusText.text=claimed?"보상을 수령했습니다.":string.IsNullOrEmpty(progress.StorageWarning)?"이미 받았거나 수령 조건을 충족하지 않았습니다.":progress.StorageWarning;Refresh();}
        public void GetSpecialReward(){ClaimRow(levelMode?"maxlevel":"special");}
    }
}
