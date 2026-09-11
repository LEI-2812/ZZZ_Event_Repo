using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public partial class BeastBeatApp
    {
        public void StartBattle(int id)
        {
            if(Busy || !Progress.StageOpen(id) || Progress.Save.party.Count==0 || !Progress.Eligible)return;
            BeastBeatSession.PreviewBattle=false;Battle=new BattleEngine(Progress,id);selectedStage=id;skillsOpen=false;paused=false;battleHistory.Clear();battleLine=Battle.Stage.npc+"가 승부를 걸어왔다!";
            BeastBeatSession.TutorialPending=!Progress.Save.tutorialSeen;Show("Battle");
            if(sceneScreen=="Battle"&&BeastBeatSession.TutorialPending){BeastBeatSession.TutorialPending=false;ShowBattleTutorial();}
        }
        void ShowBattleTutorial()
        {
                var p=OpenModal("처음 만나는 비스트 비트",840,525);
                Label(p,"01   FIGHT!에서 기술을 선택하세요.\n02   물리 → 불 → 얼음 → 물리 순서로 강합니다.\n03   전기와 에테르는 서로에게 강합니다.\n04   교체하면 상대가 공격합니다. 기절 후 교체는 무료!\n05   응급수리로 HP와 상태 이상을 회복할 수 있어요.",38,92,763,258,24,Color.white);
                Label(p,"상대 파티를 모두 쓰러뜨리면 승리합니다.\n체력과 기술 사용 횟수는 배틀마다 새로 채워집니다.",38,358,760,61,20,muted);
                Button(p,"알겠어요, 배틀 시작!",38,446,764,54,()=>{Progress.Save.tutorialSeen=true;Progress.Persist();CloseModal();},yellow);
        }
        void FighterPanel(Fighter f,bool enemySide)
        {
            float x=enemySide?1035:40,y=enemySide?187:604;var b=Data.Boo(f.id);
            Box(view,"Fighter stats",x,y,525,136,new Color(.022f,.035f,.055f,.95f));
            Label(view,b.name,x+23,y+13,350,40,29,Color.white,FontStyle.Bold);Label(view,"LV. "+f.level,x+389,y+14,112,38,23,muted,FontStyle.Bold,TextAnchor.MiddleRight);
            Bar(view,x+23,y+65,479,f.hp/(float)f.maxHp,f.hp>f.maxHp*.3f?new Color(.38f,.9f,.55f):pink,13);
            Label(view,f.hp+" / "+f.maxHp,x+23,y+91,235,30,20,Color.white,FontStyle.Bold);
            Label(view,!f.Alive?"리타이어":f.state==Condition.Ready?Elements.Name(b.type):BattleEngine.StatusName(f.state),x+267,y+89,234,30,19,ElementColor(b.type),FontStyle.Bold,TextAnchor.MiddleRight);
            var team=enemySide?Battle.enemy:Battle.player;int active=enemySide?Battle.enemyIndex:Battle.playerIndex;
            for(int i=0;i<team.Length;i++){Color color=!team[i].Alive?muted:i==active?yellow:team[i].state!=Condition.Ready?cyan:Color.white;Tag(view,(i+1).ToString(),x+i*45,y-43,34,color);}
            if(!enemySide){var owned=Battle.Progress.Owned(f.id);Bar(view,x+23,y+128,479,owned.level==20?1:owned.xp/(float)Data.NeedXp(owned.level),yellow,4);}
        }
        void BattleView()
        {
            if(Battle==null){Show("Stages");return;}
            if(arena!=null)arena.Sync(Battle);
            Header("챔피언 리그  /  "+Battle.Stage.npc,Pause);
            Box(view,"Round bar",538,107,525,52,new Color(.015f,.026f,.048f,.93f));Label(view,"ROUND "+Battle.round.ToString("00")+"     /     "+(Busy&&enemyTurn?"상대 턴":"내 턴"),552,113,495,40,22,Busy&&enemyTurn?pink:yellow,FontStyle.Bold,TextAnchor.MiddleCenter);
            if(BeastBeatSession.PreviewBattle)Tag(view,"씬 직접 실행 · 연습 배틀",565,168,470,cyan);
            FighterPanel(Battle.Player,false);FighterPanel(Battle.Enemy,true);
            if(Busy) {
                Box(view,"Action processing",1175,604,385,226,new Color(.02f,.033f,.055f,.97f));Label(view,"배틀 진행 중",1200,660,333,48,29,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
            } else if(Battle.NeedsSwitch) Button(view,"다음 선수 선택  ›",1175,618,385,78,SwitchPopup,yellow);
            else if(!skillsOpen) {
                Button(view,"FIGHT!",1175,604,385,78,()=>{skillsOpen=true;Show("Battle");},yellow,true,38);
                Button(view,"선수 교체",1175,697,385,63,SwitchPopup,cyan,true,25);
                Button(view,"도망간다",1175,773,385,62,EscapePopup,null,true,24);
            } else {
                for(int i=0;i<3;i++){int index=i;var skill=Data.Skill(Data.Boo(Battle.Player.id).skillIds[i]);Button(view,skill.name+"    "+Battle.Player.pp[i]+" / "+skill.charge_count,1148,588+i*73,412,64,()=>UseSkill(index),i==1?ElementColor(Data.Boo(Battle.Player.id).type):(Color?)null,Battle.Player.pp[i]>0,23);}
                if(Battle.Player.pp.All(x=>x==0))Button(view,"발버둥 · 반동 피해",1148,807,276,52,()=>UseSkill(-1),pink,true,18);
                Button(view,"‹",1440,807,120,52,()=>{skillsOpen=false;Show("Battle");},null,true,28);
            }
            Box(view,"Battle commentary",40,772,1058,80,new Color(.014f,.022f,.04f,.97f));Label(view,battleLine,62,780,1014,63,23,Color.white,FontStyle.Bold);
            Button(view,"기록",975,692,123,47,LogPopup,null,true,19);
            Button(view,"Ⅱ  일시정지",40,106,202,49,Pause,null,true,20);
        }
        public void UseSkill(int index) { if(Busy || modal!=null || Battle==null || Battle.Finished)return;var messages=Battle.Act(index);if(messages.Count>0)StartCoroutine(PlayRound(messages)); }
        IEnumerator PlayRound(List<BattleMessage> messages)
        {
            Busy=true;skillsOpen=false;
            foreach(var message in messages) {
                while(paused)yield return null;
                enemyTurn=message.enemy;battleLine=(message.enemy?"상대  /  ":"우리  /  ")+message.text;battleHistory.Add(battleLine);Show("Battle");Sound(hitClip);
                if(arena!=null)arena.PlayAction(message.enemy,message.text);
                float timer=0;while(timer<.65f){if(!paused)timer+=Time.unscaledDeltaTime;yield return null;}
            }
            while(paused)yield return null;
            Busy=false;
            if(Battle.Finished) {
                resultNotes=BeastBeatSession.PreviewBattle?new List<string>{"씬 직접 실행 연습 배틀입니다. 저장 데이터와 보상은 변경되지 않습니다."}:Battle.Won?Progress.Victory(Battle.Stage.id,Battle.player.Select(x=>x.id)):new List<string>{"파티를 다시 편성하고 상성을 확인해보세요.","실패한 배틀의 경험치와 보상은 지급되지 않습니다."};
                if(Battle.Won)Sound(winClip);Show("Result");
            } else Show("Battle");
        }
        void SwitchPopup()
        {
            if(Busy)return;
            var p=OpenModal("어떤 Bangboo로 교체할까?",990,440);
            Label(p,Battle.NeedsSwitch?"리타이어한 방부를 대신할 선수를 선택하세요. 이 교체는 턴을 소모하지 않습니다.":"교체하면 상대가 먼저 공격합니다.",35,82,920,58,20,muted);
            for(int i=0;i<Battle.player.Length;i++) {
                int index=i;var f=Battle.player[i];float x=30+i*321;Portrait(p,f.id,f.level,x+45,138,190);
                Label(p,Data.Boo(f.id).name+"  LV."+f.level,x,299,302,36,22,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
                Bar(p,x+17,342,267,f.hp/(float)f.maxHp,cyan,8);
                Button(p,i==Battle.playerIndex?"배틀 중!":!f.Alive?"리타이어":f.hp+" / "+f.maxHp+" · 교체",x+7,364,288,52,()=>{CloseModal();StartCoroutine(PlayRound(Battle.Switch(index)));},i==Battle.playerIndex?(Color?)null:cyan,f.Alive&&i!=Battle.playerIndex,19);
            }
        }
        void Pause()
        {
            if(CurrentScreen!="Battle")return;
            var p=OpenModal("챔피언 리그 상세  /  일시정지",930,625);paused=true;
            Label(p,"도전 목표",38,91,850,35,23,cyan,FontStyle.Bold);Label(p,Battle.Stage.npc+"와의 배틀에서 승리하기",38,138,850,46,29,Color.white,FontStyle.Bold);
            Label(p,"“"+Battle.Stage.npc_dialogue+"”",38,193,850,51,22,muted);
            Label(p,"주특기 간 상성 메커니즘",38,265,850,43,25,Color.white,FontStyle.Bold);
            Label(p,"<color=#FFD84C>물리</color>  →  <color=#FF825C>불</color>  →  <color=#69E6D7>얼음</color>  →  <color=#FFD84C>물리</color>\n\n<color=#62ADFF>전기</color>  ↔  <color=#F47BBD>에테르</color>  (서로에게 강함)",38,321,850,106,26,Color.white,FontStyle.Bold);
            Label(p,"유리한 속성 공격 1.5배 · 불리한 순환 상성 0.65배\n응급수리는 체력 30%와 상태 이상을 회복합니다.",38,444,850,63,21,muted);
            Button(p,"계속하기",38,545,272,56,CloseModal,cyan);
            Button(p,"재시작",330,545,272,56,()=>{StopAllCoroutines();Busy=false;CloseModal();if(BeastBeatSession.PreviewBattle){PrepareBattlePreview();Show("Battle");}else StartBattle(selectedStage);});
            Button(p,"경기 나가기",622,545,272,56,()=>{CloseModal();EscapePopup();});
        }
        void EscapePopup()
        {
            var p=OpenModal("배틀을 포기할까?",700,330);paused=true;
            Label(p,"이번 배틀의 진행 상황은 저장되지 않습니다.\n그만두시겠습니까?",35,97,625,102,24);
            Button(p,"예 · 경기 나가기",35,238,300,56,()=>{StopAllCoroutines();Busy=false;Battle=null;Show("Stages");},yellow);
            Button(p,"아니오",355,238,310,56,CloseModal);
        }
        void LogPopup()
        {
            if(battleHistory.Count>512)battleHistory.RemoveRange(0,battleHistory.Count-512);var p=OpenModal("배틀 기록",1060,650);paused=true;var list=Scroll(p,30,85,1000,520,Mathf.Max(520,battleHistory.Count*61));
            for(int i=0;i<battleHistory.Count;i++)Label(list,battleHistory[i],10,i*61,970,58,21,muted);
        }
        void Results()
        {
            if(Battle==null){Header("도전 결과",()=>Show("Stages"));Label(view,"아직 완료한 배틀이 없습니다.",250,300,1100,100,42,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);Button(view,"챔피언 리그로",570,500,460,70,()=>Show("Stages"),cyan);return;}
            bool win=Battle!=null&&Battle.Won;Header("도전 결과",()=>Show("Stages"));
            Label(view,win?"VICTORY!":"TRY AGAIN",40,116,1520,91,73,win?yellow:pink,FontStyle.Bold,TextAnchor.MiddleCenter);
            Label(view,win?"‘"+Battle.Stage.npc+"’와의 배틀에서 승리했다!":"아쉽지만, 이번 배틀은 여기까지!",40,214,1520,52,32,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
            for(int i=0;i<Battle.player.Length;i++) {
                var f=Battle.player[i];int lv=Progress.Owns(f.id)?Progress.Owned(f.id).level:f.level;float x=160+i*430;Box(view,"Result member",x,303,400,276,panel);Portrait(view,f.id,lv,x+104,300,207);
                Label(view,Data.Boo(f.id).name+"  LV."+lv,x+20,507,360,46,27,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
                if(lv>f.level)Tag(view,"LV UP!",x+239,320,137,yellow);
            }
            var scroll=Scroll(view,175,612,1250,155,resultNotes.Count*39);for(int i=0;i<resultNotes.Count;i++)Label(scroll,resultNotes[i],15,i*39,1210,36,22,muted,FontStyle.Normal,TextAnchor.MiddleCenter);
            Button(view,"파티 확인",350,802,420,59,()=>Show("Collection"));Button(view,"확인  ›",800,802,450,59,()=>Show("Stages"),cyan);
        }
        void PrepareBattlePreview()
        {
            var training=new ProgressService(Data,System.IO.Path.Combine(Application.temporaryCachePath,"beastbeat-preview-"+Guid.NewGuid().ToString("N")+".json"));
            training.Save.owned.Add(new OwnedBoo{id=11});training.Save.owned.Add(new OwnedBoo{id=21});training.Save.owned.Add(new OwnedBoo{id=31});training.Save.party.AddRange(new[]{11,21,31});
            Battle=new BattleEngine(training,1,123);selectedStage=1;BeastBeatSession.PreviewBattle=true;battleLine="연습 배틀 · 파티와 보상은 저장되지 않습니다.";
        }
    }
}
