using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeastBeat
{
    // Presentation binds to the user's existing scene. Only pause list rows are generated.
    public sealed class BattleSceneActions : MonoBehaviour
    {
        public static BattleSceneActions Instance { get; private set; }
        public GameWorkbook workbook;
        public BattleArena3D arena;
        public GameObject pausePopup, changePopup, retirePopup;
        public Button pauseButton, fightButton, changeButton, retireButton, skillsBack;
        public Button[] partyButtons;
        public Slider[] partyHp;
        public Transform myStatus, enemyStatus, myParty, enemyParty;
        public TMP_Text myTurn, enemyTurn, partyDetails;
        public TMP_Text[] infoLabels;
        public ScrollRect infoScroll;
        public PauseInfoRow infoRowPrefab;

        public BattleEngine Battle { get; private set; }
        public ProgressService Progress => Battle == null ? BeastBeatSession.Progress : Battle.Progress;
        public bool Busy { get; private set; }
        public bool IsPaused => pausePopup.activeSelf || retirePopup.activeSelf || changePopup.activeSelf;
        public bool SkillsOpen { get; private set; }
        public int SelectedInfo { get; private set; }
        readonly List<PauseInfoRow> rows = new List<PauseInfoRow>();
        TMP_Text mySkillText, enemySkillText;
        BattleCatalog catalog;
        string revision, skillRevision;
        float nextCheck;
        bool enemyActing, navigating;
        string error;

        void Awake()
        {
            if (!enabled) return;
            Instance = this;
            ClearSkillMessages();
            pausePopup.SetActive(false); changePopup.SetActive(false); retirePopup.SetActive(false);
            try
            {
                if (BeastBeatSession.Progress == null) BeastBeatSession.Progress = new ProgressService(GameData.Load());
                Battle = BeastBeatSession.Battle;
                if (Battle == null)
                {
                    var original = BeastBeatSession.Progress;
                    var practice = new ProgressService(original.Data, System.IO.Path.Combine(Application.temporaryCachePath, "beastbeat-practice-" + Guid.NewGuid().ToString("N") + ".json"));
                    practice.Save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original.Save));
                    var stage = original.Data.ResolveStage(BeastBeatSession.SelectedStage);
                    if (stage == null || !practice.StageOpen(stage.id)) stage = original.Data.stage_list.OrderBy(s => s.id).First();
                    Battle = BattleCatalog.CreateBattle(practice, stage.id);
                    BeastBeatSession.PreviewBattle = true;
                    BeastBeatSession.SelectedStage = stage.id; BeastBeatSession.StageGroup = stage.type;
                }
                BeastBeatSession.Battle = Battle;
                ReloadInfo(); skillRevision = (workbook ? workbook : GameWorkbook.Load()).revision; Refresh();
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;

        }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsPaused) ClosePopup(); else if (SkillsOpen) CloseSkills(); else Pause();
            }
            if (Busy || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .5f;
            var book = workbook ? workbook : GameWorkbook.Load();
            if (book && book.revision != skillRevision)
            {
                try { Battle?.ReloadSkills(GameData.Load(book)); skillRevision=book.revision; ReloadInfo(); Refresh(); } catch (Exception ex) { ShowError(ex.Message); }
            }
        }
        void ShowError(string message)
        {
            error = message; Debug.LogError("Battle Scene: " + message);
            if (partyDetails) partyDetails.text = message;
            fightButton.interactable = changeButton.interactable = false;
            pausePopup.SetActive(true);
            if (infoLabels.Length > 2) infoLabels[2].text = message;
        }
        public void ReloadInfo()
        {
            var book = workbook ? workbook : GameWorkbook.Load();
            catalog = BattleCatalog.Load(book); revision = book.revision;
            Canvas.ForceUpdateCanvases();
            infoScroll.viewport.ForceUpdateRectTransforms();
            float viewportHeight = infoScroll.viewport.rect.height;
            if(viewportHeight<=0) viewportHeight=((RectTransform)infoScroll.transform).rect.height;
            float height = Mathf.Max(1, viewportHeight / 7.2f);
            for (int i = 0; i < catalog.pause.Length; i++)
            {
                if (i >= rows.Count)
                {
                    var row = Instantiate(infoRowPrefab, infoScroll.content);
                    row.name = "PauseInfo_" + catalog.pause[i].id; rows.Add(row);
                }
                var rt = (RectTransform)rows[i].transform;
                rt.anchorMin = new Vector2(0,1); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f,1);
                rt.anchoredPosition = new Vector2(0,-i*height); rt.sizeDelta = new Vector2(0,height);
                rows[i].Bind(this,catalog.pause[i]); rows[i].gameObject.SetActive(true);
            }
            for(int i=catalog.pause.Length;i<rows.Count;i++) rows[i].gameObject.SetActive(false);
            infoScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewportHeight,catalog.pause.Length*height));
            if (!catalog.pause.Any(p=>p.id==SelectedInfo)) SelectedInfo = catalog.pause.Length==0 ? 0 : catalog.pause[0].id;
            SelectInfo(SelectedInfo);
        }
        public void SelectInfo(int id)
        {
            SelectedInfo = id;
            foreach(var row in rows) row.SetSelected(row.Id==id);
            var entry = catalog.pause.FirstOrDefault(p=>p.id==id);
            var values = entry == null ? new string[6] : new[]{entry.info1,entry.info2,entry.info3,entry.info4,entry.info5,entry.info6};
            for(int i=0;i<infoLabels.Length;i++) infoLabels[i].text = Expand(values[i]??"");
        }
        string Expand(string value) => value.Replace("{npc}",Battle==null?"":Battle.Stage.npc).Replace("{npc_dialogue}",Battle==null?"":Battle.Stage.npc_dialogue);
        public void Pause() { if(navigating)return; pausePopup.SetActive(true); changePopup.SetActive(false); retirePopup.SetActive(false); try { ReloadInfo(); } catch(Exception ex) { ShowError(ex.Message); } }
        public void ClosePopup() { pausePopup.SetActive(false);changePopup.SetActive(false);retirePopup.SetActive(false);if(Battle!=null)Refresh(); }
        public void CloseSkills() { SkillsOpen=false;Refresh(); }
        public void Fight()
        {
            if(SkillsOpen){UseSkill(0);return;}
            if(!CanAct())return;
            if(Battle.NeedsSwitch){OpenChange();return;}
            SkillsOpen=true;
            Refresh();
            // 같은 버튼을 스킬 선택에 재사용하므로 이전 클릭의 선택 상태를 해제합니다.
            // 포인터 위치에 따른 Highlighted Color는 그대로 유지됩니다.
            if(EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
        public void Change() { if(SkillsOpen)UseSkill(1);else OpenChange(); }
        public void Retire() { if(SkillsOpen){UseSkill(2);return;}if(navigating)return;retirePopup.SetActive(true); }
        public void OpenChange() { if(!CanAct())return;changePopup.SetActive(true);Refresh(); }
        bool CanAct() => Battle!=null&&!Busy&&!navigating&&!IsPaused&&!Battle.Finished&&string.IsNullOrEmpty(error);
        public void ChangeFirst() => Switch(0);
        public void ChangeSecond() => Switch(1);
        public void ChangeThird() => Switch(2);
        public void Switch(int index)
        {
            if(Battle==null||Busy||!changePopup.activeSelf||pausePopup.activeSelf||retirePopup.activeSelf)return;
            var messages=Battle.Switch(index);if(messages.Count==0)return;
            changePopup.SetActive(false);StartCoroutine(Play(messages));
        }
        void UseSkill(int index)
        {
            if(!CanAct())return;
            var ordered=Progress.Data.Boo(Battle.Player.id).skillIds.Select((id,i)=>new{id,i}).OrderBy(x=>x.id).ToArray();
            int skill=Battle.Player.pp.All(pp=>pp==0)?-1:ordered[index].i;
            var messages=Battle.Act(skill);if(messages.Count>0)StartCoroutine(Play(messages));
        }
        void BindSkillMessages()
        {
            var nodes=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true));
            if(!mySkillText) mySkillText=nodes.First(t=>t.name=="Img_MySkill").GetComponentInChildren<TMP_Text>(true);
            if(!enemySkillText) enemySkillText=nodes.First(t=>t.name=="Img_YourSkill").GetComponentInChildren<TMP_Text>(true);
        }
        void ClearSkillMessages()
        {
            BindSkillMessages();mySkillText.text="";enemySkillText.text="";
        }
        // 공격·회복·방어를 실제 사용한 경우에만 해당 진영의 최근 기술을 표시합니다.
        public void ShowSkillMessage(BattleMessage message)
        {
            if(string.IsNullOrEmpty(message.skillText)) return;
            BindSkillMessages();
            var label=message.enemy?enemySkillText:mySkillText;
            label.text=message.skillText;
        }
        IEnumerator Play(List<BattleMessage> messages)
        {
            Busy=true;SkillsOpen=false;
            foreach(var message in messages)
            {
                while(IsPaused)yield return null;
                enemyActing=message.enemy;BeastBeatSession.BattleLine=message.text;BeastBeatSession.History.Add(message.text);
                ShowSkillMessage(message);
                if(arena)arena.PlayAction(message.enemy,message.text);
                Refresh();
                float time=0;while(time<.65f){if(!IsPaused)time+=Time.unscaledDeltaTime;yield return null;}
            }
            while(IsPaused)yield return null;
            Busy=false;enemyActing=false;Refresh();
            if(Battle.Finished)
            {
                BeastBeatSession.ResultNotes=BattleOutcome.Complete(Battle,BeastBeatSession.PreviewBattle).Notes;
                BeastBeatSession.Battle=Battle;
                if (Move_Scene.For(this).OpenResult()) navigating = true;
            }
            else if(Battle.NeedsSwitch) { changePopup.SetActive(true);Refresh(); }
        }
        public void Restart()
        {
            if(navigating)return;
            try
            {
                Progress.ReloadData(GameData.Load(workbook ? workbook : GameWorkbook.Load()));
                var next=BattleCatalog.CreateBattle(Progress,Battle==null?BeastBeatSession.SelectedStage:Battle.Stage.id);
                StopAllCoroutines();Battle=next;BeastBeatSession.Battle=next;Busy=false;enemyActing=false;SkillsOpen=false;error=null;
                BeastBeatSession.History.Clear();ClearSkillMessages();ClosePopup();ReloadInfo();Refresh();
            }
            catch(Exception ex){ShowError(ex.Message);}
        }
        public void ExitBattle() => Move_Scene.For(this).ExitBattle();
        public void PrepareExit() { StopAllCoroutines(); Busy = false; }
        static T Child<T>(Transform parent,string name) where T:Component => parent.GetComponentsInChildren<T>(true).First(x=>x.name==name);
        void Label(Button b,string value)
        {
            var t=b.GetComponentInChildren<TMP_Text>(true);if(!t)return;
            // 동적 폰트를 Instantiate하면 글자 테이블은 복제되지만 atlas는 공유됩니다.
            // 원본 폰트에서 글자를 추가해야 다른 UI의 글자 이미지가 덮어써지지 않습니다.
            if(t.font && !t.font.HasCharacters(value, out uint[] _, true, true))
            {
                var korean = infoLabels.Length > 0 && infoLabels[0] ? infoLabels[0].font : null;
                if(korean && korean.HasCharacters(value, out uint[] _, true, true)) t.font=korean;
            }
            t.text=value;
        }
        // 교체·상태 회복 때도 현재 출전 방부의 상태만 표시합니다.
        public static void RefreshConditionIcon(Image icon, Fighter fighter)
        {
            bool visible = fighter.Alive && (fighter.state == Condition.Burn ||
                fighter.state == Condition.Paralysis || fighter.state == Condition.Frozen ||
                fighter.state == Condition.Confused);
            icon.gameObject.SetActive(visible);
            if (!visible) return;
            switch (fighter.state)
            {
                case Condition.Burn: icon.color = Color.red; break;
                case Condition.Paralysis: icon.color = Color.yellow; break;
                case Condition.Frozen: icon.color = new Color32(105, 230, 215, 255); break;
                case Condition.Confused: icon.color = Color.gray; break;
            }
        }
        void Status(Transform root,Fighter f,bool enemy)
        {
            RefreshConditionIcon(Child<Image>(root,"Img_status"), f);
            Child<TMP_Text>(root,"Txt_Name").text=Progress.Data.Boo(f.id).name;
            Child<TMP_Text>(root,"Txt_Hpnum").text=f.hp+" / "+f.maxHp;
            Child<TMP_Text>(root,"Txt_Lvnum").text="Lv. "+f.level;
            var hp=Child<Slider>(root,"Slider_Hp");hp.minValue=0;hp.maxValue=f.maxHp;hp.value=f.hp;hp.interactable=false;
        }
        public void Refresh()
        {
            if(Battle==null)return;
            Status(myStatus,Battle.Player,false);Status(enemyStatus,Battle.Enemy,true);
            myTurn.text=Busy&&!enemyActing?"내 턴 ▶":"내 턴";enemyTurn.text=Busy&&enemyActing?"상대 턴 ▶":"상대 턴";
            myTurn.alpha=enemyActing?.45f:1;enemyTurn.alpha=enemyActing?1:.45f;
            if(arena)arena.Sync(Battle);
            var buttons=new[]{fightButton,changeButton,retireButton};
            if(SkillsOpen)
            {
                var skills=Progress.Data.Boo(Battle.Player.id).skillIds.Select((id,i)=>new{id,i}).OrderBy(x=>x.id).ToArray();bool empty=Battle.Player.pp.All(n=>n==0);
                for(int i=0;i<3;i++){var skill=Progress.Data.Skill(skills[i].id);Label(buttons[i],empty?(i==0?"발버둥":"사용 횟수 소진"):skill.name+"  "+Battle.Player.pp[skills[i].i]+" / "+skill.charge_count);buttons[i].interactable=!Busy&&!IsPaused&&(empty?i==0:Battle.Player.pp[skills[i].i]>0);}
            }
            else
            {
                Label(fightButton,Battle.NeedsSwitch?"다음 선수 선택":"FIGHT!");Label(changeButton,"선수 교체");Label(retireButton,"도망간다");
                fightButton.interactable=changeButton.interactable=!Busy&&!IsPaused&&!Battle.Finished;retireButton.interactable=!IsPaused&&!navigating;
            }
            skillsBack.gameObject.SetActive(SkillsOpen);pauseButton.interactable=!navigating;
            for(int i=0;i<3;i++)
            {
                var f=i<Battle.player.Length?Battle.player[i]:null;
                Label(partyButtons[i],f==null?"빈 슬롯":Progress.Data.Boo(f.id).name+"\nLv. "+f.level+"\n"+(i==Battle.playerIndex?"배틀 중!":!f.Alive?"기절":BattleEngine.StatusName(f.state)));
                partyButtons[i].interactable=f!=null&&f.Alive&&i!=Battle.playerIndex&&!Busy;
                partyHp[i].minValue=0;partyHp[i].maxValue=f==null?1:f.maxHp;partyHp[i].value=f==null?0:f.hp;partyHp[i].interactable=false;
                Mark(myParty,i,Battle.player,Battle.playerIndex);Mark(enemyParty,i,Battle.enemy,Battle.enemyIndex);
            }
            var current=Battle.Player;var b=Progress.Data.Boo(current.id);
            partyDetails.text="상태: "+(!current.Alive?"기절":BattleEngine.StatusName(current.state))+"\n주특기: "+Elements.Name(b.type)+"\n"+b.description;
        }
        void Mark(Transform parent,int index,Fighter[] team,int active)
        {
            var img=Child<Image>(parent,"Img_B"+(index+1));
            img.color=index>=team.Length||!team[index].Alive?Color.gray:index==active?Color.yellow:team[index].state!=Condition.Ready?Color.cyan:Color.white;
        }
    }
}
