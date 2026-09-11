using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public partial class BeastBeatApp
    {
        [Header("Editable scene UI")]
        public RectTransform screenRoot;
        public RectTransform popupRoot;
        public Font sceneFont;
        [System.NonSerialized] public bool authoringUI;
        readonly Dictionary<string,int> uiOccurrences=new Dictionary<string,int>();

        // The method + IL call site is independent of visible/localized text and data values.
        // Stop at the view builder so editor authoring and runtime binding use identical IDs.
        string UIKey(Transform parent)
        {
            var frames=new StackTrace(2,false).GetFrames();var parts=new List<string>();
            foreach(var frame in frames){var method=frame.GetMethod();if(method.DeclaringType!=typeof(BeastBeatApp))break;
                if(method.Name=="RenderScreen"||method.Name=="OpenModal")break;
                parts.Add(method.Name+":"+frame.GetILOffset());
                if(method.Name.EndsWith("Popup")||method.Name=="Pause"||method.Name=="Results"||method.Name=="PartyOrAdopt"||method.Name=="ShowBattleTutorial"||method.Name=="Toast"||method.Name=="ClaimReward")break;
            }
            var parentNode=parent.GetComponent<EditableUINode>();string prefix=(parentNode?parentNode.bindingKey:parent.name)+"/"+string.Join("/",parts);
            uiOccurrences.TryGetValue(prefix,out int count);uiOccurrences[prefix]=count+1;return prefix+"#"+count;
        }
        RectTransform FindUI(Transform parent,string key)
        {
            // Search the whole screen to allow designers to regroup children in the Hierarchy.
            var root=parent==canvas?canvas:parent.GetComponentInParent<Canvas>()?.transform;
            if(!root)return null;
            foreach(var node in root.GetComponentsInChildren<EditableUINode>(true))if(node.bindingKey==key){node.touched=true;node.gameObject.SetActive(true);return (RectTransform)node.transform;}
            return null;
        }
        void BeginUI(Transform root)
        {
            uiOccurrences.Clear();if(!root)return;
            foreach(var node in root.GetComponentsInChildren<EditableUINode>(true))node.touched=false;
        }
        void EndUI(Transform root)
        {
            if(!root)return;
            foreach(var node in root.GetComponentsInChildren<EditableUINode>(true))if(node.transform!=root&&!node.touched)node.gameObject.SetActive(false);
        }
        static T UIComponent<T>(GameObject go) where T:Component {var component=go.GetComponent<T>();return component?component:go.AddComponent<T>();}
        void LateUpdate(){if(modal)EndUI(modal);}

        public void AuthorScreen()
        {
            authoringUI=true;Awake();
        }
        public void AuthorVariants()
        {
            Toast("안내 문구");EndUI(modal);CloseModal();
            Progress.StorageWarning="저장 안내";RenderScreen(sceneScreen);Progress.StorageWarning="";
            if(sceneScreen=="Home"){
                var party=Progress.Save.party.ToArray();Progress.Save.party.Clear();RenderScreen(sceneScreen);Progress.Save.party.AddRange(party);
                Progress.Save.masterTitle=true;Progress.Save.claimed.Add("maxlevel");RenderScreen(sceneScreen);Progress.Save.masterTitle=false;Progress.Save.claimed.Remove("maxlevel");
            }
            if(sceneScreen=="Battle"){
                BeastBeatSession.PreviewBattle=true;RenderScreen("Battle");BeastBeatSession.PreviewBattle=false;
                Busy=true;RenderScreen("Battle");Busy=false;
                int hp=Battle.Player.hp;Battle.Player.hp=0;RenderScreen("Battle");Battle.Player.hp=hp;
                skillsOpen=true;RenderScreen("Battle");skillsOpen=false;
                var pp=(int[])Battle.Player.pp.Clone();Battle.Player.pp=new[]{0,0,0};skillsOpen=true;RenderScreen("Battle");skillsOpen=false;Battle.Player.pp=pp;
                var old=Battle;var ids=Data.Stage(1).enemies;Data.Stage(1).enemies=new[]{21,31,41};Battle=new BattleEngine(Progress,1,123);RenderScreen("Battle");Battle=old;Data.Stage(1).enemies=ids;
                Pause();EndUI(modal);CloseModal();SwitchPopup();EndUI(modal);CloseModal();
                EscapePopup();EndUI(modal);CloseModal();ShowBattleTutorial();EndUI(modal);CloseModal();LogPopup();EndUI(modal);CloseModal();
                battleHistory.AddRange(Enumerable.Repeat("배틀 기록",512));LogPopup();EndUI(modal);CloseModal();battleHistory.Clear();
            }
            if(sceneScreen=="Entry"){AccountPopup();EndUI(modal);CloseModal();}
            if(sceneScreen=="Collection"){
                PartyPopup();EndUI(modal);CloseModal();PartyOrAdopt(11);EndUI(modal);CloseModal();
                var boo=Progress.Owned(11);Progress.Save.owned.Remove(boo);RenderScreen(sceneScreen);PartyOrAdopt(11);EndUI(modal);CloseModal();Progress.Save.owned.Add(boo);
                Progress.Save.owned.Add(new OwnedBoo{id=12});PartyOrAdopt(12);EndUI(modal);CloseModal();Progress.Save.owned.RemoveAll(b=>b.id==12);
                var party=Progress.Save.party.ToArray();Progress.Save.party.Clear();PartyPopup();EndUI(modal);CloseModal();Progress.Save.party.AddRange(party);
            }
            if(sceneScreen=="Rewards"||sceneScreen=="Levels"){
                rewardGroup=1;RenderScreen(sceneScreen);rewardGroup=0;InventoryPopup();EndUI(modal);CloseModal();
                Progress.Save.inventory.AddRange(Data.items.Select(i=>new InventoryItem{id=i.id,amount=1}));InventoryPopup();EndUI(modal);CloseModal();Progress.Save.inventory.Clear();
                ClaimReward("l1");EndUI(modal);CloseModal();Progress.Save.claimed.Clear();Progress.Save.inventory.Clear();
            }
            if(sceneScreen=="Stages"){
                for(int g=2;g<=4;g++){stageGroup=g;selectedStage=Data.stage_list.First(s=>s.type==g).id;RenderScreen(sceneScreen);}stageGroup=1;selectedStage=1;
            }
            if(sceneScreen=="Result"){
                var battle=Battle;Battle=null;RenderScreen(sceneScreen);Battle=battle;
                var notes=resultNotes;resultNotes=Enumerable.Repeat("결과 안내",64).ToList();foreach(var owned in Progress.Save.owned)owned.level=2;RenderScreen(sceneScreen);foreach(var owned in Progress.Save.owned)owned.level=1;resultNotes=notes;
            }
            RenderScreen(sceneScreen);
        }
    }
}
