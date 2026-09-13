using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace BeastBeat
{
    public partial class BeastBeatApp : MonoBehaviour
    {
        public static BeastBeatApp Instance { get; private set; }
        public ProgressService Progress { get; private set; }
        public BattleEngine Battle { get; private set; }
        public string CurrentScreen { get; private set; }
        public bool Busy { get; private set; }
        public bool IsPaused { get { return paused; } }
        [Header("This scene owns this screen and its canvas")]
        public string sceneScreen="Entry";
        public Canvas sceneCanvas;
        public BattleArena3D arena;
        public GameData Data { get { return Progress.Data; } }
        RectTransform canvas, view, modal;
        Font font;
        BangbooPortraits portraits;
        AudioSource audioSource;
        AudioClip clickClip, hitClip, winClip;
        int stageGroup=1,selectedStage=1,selectedBoo=11,rewardGroup=0;
        bool skillsOpen, paused, enemyTurn;
        string battleLine="",previousScreen="Home";
        readonly List<string> battleHistory=new List<string>();
        List<string> resultNotes=new List<string>();
        Color ink=new Color(.025f,.033f,.055f), panel=new Color(.055f,.075f,.12f), muted=new Color(.60f,.67f,.76f);
        Color cyan=new Color(.22f,.81f,.98f), yellow=new Color(1,.87f,.22f), pink=new Color(1,.32f,.61f);
        const float W=1600,H=900;

        void Awake()
        {
            Instance=this;Application.runInBackground=true;
            if(BeastBeatSession.Progress==null){BeastBeatSession.Progress=new ProgressService(GameData.Load());var initialStage=BeastBeatSession.Progress.Data.ResolveStage(BeastBeatSession.Progress.Save.lastStage);BeastBeatSession.SelectedStage=initialStage==null?0:initialStage.id;BeastBeatSession.StageGroup=initialStage==null?1:initialStage.type;}
            Progress=BeastBeatSession.Progress;Battle=BeastBeatSession.Battle;portraits=new BangbooPortraits();
            selectedStage=BeastBeatSession.SelectedStage;stageGroup=BeastBeatSession.StageGroup;selectedBoo=BeastBeatSession.SelectedBoo;rewardGroup=BeastBeatSession.RewardGroup;
            previousScreen=BeastBeatSession.PreviousScreen;battleLine=BeastBeatSession.BattleLine;battleHistory.AddRange(BeastBeatSession.History);resultNotes=new List<string>(BeastBeatSession.ResultNotes);
            font=sceneFont?sceneFont:Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Noto Sans CJK KR","Arial"},24);
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go=sceneCanvas!=null?sceneCanvas.gameObject:new GameObject("Beast Beat Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            if(sceneCanvas==null)go.transform.SetParent(transform,false);canvas=go.GetComponent<RectTransform>();go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(W,H);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            if(GetComponentInChildren<EventSystem>()==null){var eventGo=new GameObject("Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));eventGo.transform.SetParent(transform,false);}
            audioSource=UIComponent<AudioSource>(gameObject);audioSource.playOnAwake=false;
            clickClip=Tone(650,.055f);hitClip=Tone(145,.15f);winClip=Tone(880,.45f);
            if(sceneScreen=="Battle"&&Battle==null)PrepareBattlePreview();
            RenderScreen(sceneScreen);
            if(sceneScreen=="Battle"&&BeastBeatSession.TutorialPending){BeastBeatSession.TutorialPending=false;ShowBattleTutorial();}
        }
        void OnDestroy() { if(Instance==this)Instance=null;portraits?.Dispose();if(clickClip){if(Application.isPlaying)Destroy(clickClip);else DestroyImmediate(clickClip);}if(hitClip){if(Application.isPlaying)Destroy(hitClip);else DestroyImmediate(hitClip);}if(winClip){if(Application.isPlaying)Destroy(winClip);else DestroyImmediate(winClip);} }
        AudioClip Tone(float frequency,float length)
        {
            int count=(int)(22050*length);float[] samples=new float[count];for(int i=0;i<count;i++)samples[i]=Mathf.Sin(i*frequency*2*Mathf.PI/22050)*.15f*(1-i/(float)count);
            var clip=AudioClip.Create("BeastBeat tone",count,1,22050,false);clip.SetData(samples,0);return clip;
        }
        void Sound(AudioClip clip) { if(!Progress.Save.muted)audioSource.PlayOneShot(clip); }
        void Update()
        {
            if(Keyboard.current==null)return;
            if(Keyboard.current.escapeKey.wasPressedThisFrame) { if(modal!=null)CloseModal();else if(CurrentScreen=="Battle")Pause();else if(CurrentScreen!="Entry")Show("Home"); }
        }
        RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color? color=null)
        {
            string key=UIKey(parent);var existing=FindUI(parent,key);
            if(existing){var binding=existing.GetComponent<EditableUINode>();if(color.HasValue){UIComponent<Image>(existing.gameObject);binding.ColorValue(color.Value);}if(name=="Bar fill"||name=="Content")binding.DynamicSize(new Vector2(w,h));return existing;}
            if(Application.isPlaying||!authoringUI)throw new System.InvalidOperationException("Missing authored UI object: "+name+" ("+key+")");
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
            var node=go.AddComponent<EditableUINode>();node.bindingKey=key;node.touched=true;
            if(color.HasValue){var image=go.AddComponent<Image>();image.color=color.Value;}return r;
        }
        Text Label(Transform parent,string value,float x,float y,float w,float h,int size=22,Color? color=null,FontStyle style=FontStyle.Normal,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var r=Box(parent,"Text "+value.Split('\n')[0],x,y,w,h);var t=r.GetComponent<Text>();bool fresh=t==null;t=UIComponent<Text>(r.gameObject);
            if(fresh){t.font=font;t.fontSize=size;t.fontStyle=style;t.alignment=alignment;t.supportRichText=true;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;}
            var node=r.GetComponent<EditableUINode>();node.TextValue(value);node.ColorValue(color??Color.white);return t;
        }
        Button Button(Transform parent,string text,float x,float y,float w,float h,Action action,Color? color=null,bool enabled=true,int size=23)
        {
            var r=Box(parent,"Button "+text,x,y,w,h,color??panel);var b=r.GetComponent<Button>();if(!b){b=r.gameObject.AddComponent<Button>();var colors=b.colors;colors.highlightedColor=new Color(1.15f,1.15f,1.15f);colors.pressedColor=new Color(.7f,.7f,.7f);colors.disabledColor=new Color(.45f,.45f,.45f,.75f);b.colors=colors;}b.interactable=enabled;
            Label(r,text,16,0,w-32,h,size,color.HasValue?ink:Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
            r.GetComponent<EditableUINode>().Bind(b,()=>{Sound(clickClip);action();});return b;
        }
        void Picture(Transform parent,Texture texture,float x,float y,float w,float h,Color? tint=null)
        { if(texture==null)return;var r=Box(parent,"Art",x,y,w,h);var image=UIComponent<RawImage>(r.gameObject);var node=r.GetComponent<EditableUINode>();if(!node.overrideTexture)image.texture=texture;node.ColorValue(tint??Color.white);image.raycastTarget=false; }
        void Portrait(Transform parent,int id,int level,float x,float y,float size,bool flip=false)
        {
            var b=Data.Boo(id);var texture=portraits.Get(b,level>=Data.balance.evolutionLevel);
            var r=Box(parent,"Portrait "+b.name,x,y,size,size);var image=UIComponent<RawImage>(r.gameObject);if(!r.GetComponent<EditableUINode>().overrideTexture)image.texture=texture;image.raycastTarget=false;if(flip)image.uvRect=new Rect(1,0,-1,1);
            UIComponent<PortraitMotion>(r.gameObject);
        }
        void Bar(Transform parent,float x,float y,float width,float value,Color color,float height=10)
        { Box(parent,"Bar track",x,y,width,height,new Color(.13f,.17f,.21f));Box(parent,"Bar fill",x,y,width*Mathf.Clamp01(value),height,color); }
        Color ElementColor(int type) { ColorUtility.TryParseHtmlString("#"+Elements.Hex(type),out Color c);return c; }
        void Tag(Transform parent,string text,float x,float y,float width,Color color) { Box(parent,"Tag",x,y,width,32,color);Label(parent,text,x+8,y,width-16,32,16,ink,FontStyle.Bold,TextAnchor.MiddleCenter); }
        RectTransform Scroll(Transform parent,float x,float y,float w,float h,float contentHeight)
        {
            var outer=Box(parent,"Scroll view",x,y,w,h);var scroll=UIComponent<ScrollRect>(outer.gameObject);scroll.horizontal=false;scroll.scrollSensitivity=40;
            var viewport=Box(outer,"Viewport",0,0,w,h);UIComponent<RectMask2D>(viewport.gameObject);
            var content=Box(viewport,"Content",0,0,w,Mathf.Max(h,contentHeight));scroll.viewport=viewport;scroll.content=content;
            // Transparent graphic receives drag/wheel events even in empty list space.
            var hit=UIComponent<Image>(viewport.gameObject);hit.color=Color.clear;return content;
        }
        void ClearView()
        {
            CloseModal();view=screenRoot;
            if(!view){view=Box(canvas,"Screen · Editable UI",0,0,W,H,ink);view.anchorMin=view.anchorMax=new Vector2(.5f,.5f);view.pivot=new Vector2(.5f,.5f);view.anchoredPosition=Vector2.zero;UIComponent<CanvasGroup>(view.gameObject);screenRoot=view;}
            BeginUI(view);view.gameObject.SetActive(true);
        }
        public void Show(string screen)
        {
            if(Busy && screen!="Battle")return;
            if(screen!=sceneScreen){CaptureSession();SceneManager.LoadScene(screen=="Home"?BeastBeatSession.HomeScenePath:"Assets/Scenes/"+BeastBeatSession.SceneName(screen)+".unity");return;}
            RenderScreen(screen);
        }
        public void CaptureSession()
        {
            BeastBeatSession.Battle=Battle;BeastBeatSession.StageGroup=stageGroup;BeastBeatSession.SelectedStage=selectedStage;BeastBeatSession.SelectedBoo=selectedBoo;BeastBeatSession.RewardGroup=rewardGroup;
            BeastBeatSession.PreviousScreen=previousScreen;BeastBeatSession.BattleLine=battleLine;BeastBeatSession.History=new List<string>(battleHistory);BeastBeatSession.ResultNotes=new List<string>(resultNotes);
        }
        void RenderScreen(string screen)
        {
            ClearView();CurrentScreen=screen;
            if(screen=="Battle")view.GetComponent<Image>().color=Color.clear;
            switch(screen) {
                case "Entry":Entry();break;case "Home":Home();break;case "Stages":Stages();break;case "Collection":Collection();break;
                case "Battle":BattleView();break;case "Result":Results();break;case "Rewards":Rewards(false);break;case "Levels":Rewards(true);break;
            }
            if(!string.IsNullOrEmpty(Progress.StorageWarning))Label(view,Progress.StorageWarning,40,863,1520,30,16,pink);
            EndUI(view);
        }
        void Header(string title,Action back=null)
        {
            Box(view,"Header",0,0,W,86,new Color(.014f,.018f,.03f));
            if(back!=null)Button(view,"‹",30,19,55,48,back,null,true,34);
            Label(view,title,back==null?40:110,16,850,56,27,Color.white,FontStyle.Bold);
            Label(view,"BEAST / BEAT     STATION",1130,22,330,42,17,muted,FontStyle.Bold,TextAnchor.MiddleRight);
            Button(view,Progress.Save.muted?"음향 OFF":"음향 ON",1470,22,105,42,()=>{Progress.Save.muted=!Progress.Save.muted;Progress.Persist();Show(CurrentScreen);},null,true,15);
        }
        void Entry()
        {
            Header("리두 기록");Picture(view,Resources.Load<Texture2D>("BeastBeat/arcade"),0,86,1600,814,new Color(.42f,.44f,.60f));
            Box(view,"Entry panel",80,154,760,637,new Color(.022f,.032f,.064f,.97f));Tag(view,"상시 이벤트",118,191,150,cyan);
            Label(view,"비스트-비트\n스테이션",118,252,670,157,64,Color.white,FontStyle.Bold);
            Label(view,Data.event_list.description,120,446,620,122,23,new Color(.78f,.83f,.89f));
            Label(view,"참여 조건  ·  계정 LV.23 / 시즌 1 제3장 완료",120,579,650,34,18,muted);
            Tag(view,"로컬 체험 계정  LV."+Progress.Save.accountLevel,120,632,265,yellow);
            Button(view,"이벤트 이동  ›",120,697,395,60,()=>Show("Home"),cyan,Progress.Eligible,25);
            Button(view,"계정 정보",542,697,218,60,AccountPopup,null,true,20);
            Label(view,"당신의 파트너와\n챔피언에 도전하세요.",947,246,550,116,34,Color.white,FontStyle.Bold);
            Portrait(view,11,1,955,345,380);Tag(view,"BEAST BEAT / 01",1060,717,210,yellow);
            Label(view,"보상은 이 프로젝트 안에서만 지급됩니다.",935,814,570,30,17,muted);
        }
        void AccountPopup()
        {
            var p=OpenModal("로컬 체험 계정",620,400);
            Label(p,"계정 조건 검증용 설정입니다.\n실제 젠레스 존 제로 계정과 연동되지 않습니다.",35,89,550,65,20,muted);
            Button(p,"계정 LV.22",35,178,170,55,()=>{Progress.Save.accountLevel=22;Progress.Persist();Show("Entry");});
            Button(p,"계정 LV.30",219,178,170,55,()=>{Progress.Save.accountLevel=30;Progress.Persist();Show("Entry");});
            Button(p,"3장 "+(Progress.Save.clearedChapter>=3?"완료":"미완료"),403,178,180,55,()=>{Progress.Save.clearedChapter=Progress.Save.clearedChapter>=3?2:3;Progress.Persist();Show("Entry");});
            Label(p,"현재 조건: "+(Progress.Eligible?"참여 가능":"참여 불가"),35,254,550,42,23,Progress.Eligible?cyan:pink);
            Button(p,"확인",35,323,550,50,CloseModal,cyan);
        }
        void Home()
        {
            Header(Data.event_list.name,()=>Show("Entry"));Picture(view,Resources.Load<Texture2D>("BeastBeat/arcade"),0,86,1600,814,new Color(.55f,.55f,.7f));
            if(Progress.Save.liveBackground&&Progress.Save.claimed.Contains("special")){var art=view.GetComponentsInChildren<RawImage>().Last();art.gameObject.AddComponent<ChampionBackdrop>();}
            Box(view,"Title plate",60,139,615,162,new Color(.015f,.022f,.048f,.90f));
            Label(view,"BEAST BEAT",86,152,560,62,52,yellow,FontStyle.Bold);Label(view,"방부 챔피언 리그",90,219,550,49,28,Color.white,FontStyle.Bold);
            var s=Progress.Save;
            Box(view,"Profile",850,140,680,154,pink);Label(view,"Player_24"+s.birthday,888,155,410,47,27,ink,FontStyle.Bold);
            Label(view,"LV. "+s.level.ToString("00"),885,209,210,55,36,ink,FontStyle.Bold);
            if(s.masterTitle&&s.claimed.Contains("maxlevel"))Label(view,"BEAST MASTER",1248,157,244, forty,18,ink,FontStyle.Bold,TextAnchor.MiddleRight);
            Label(view,s.level==20?"MAX LEVEL":s.xp+" / "+Data.NeedXp(s.level),1180,220,305,30,20,ink,FontStyle.Bold,TextAnchor.MiddleRight);
            Bar(view,1100,266,385,s.level==20?1:s.xp/(float)Data.NeedXp(s.level),ink,7);
            Button(view,"챔피언 리그     ›",850,316,680,126,()=>{if(s.party.Count==0){selectedBoo=11;Show("Collection");Toast("먼저 파트너 방부를 입양해주세요.");}else Show("Stages");},cyan,true,37);
            Button(view,"방부 도감 / 입양     ›",850,464,680,117,()=>Show("Collection"),yellow,true,33);
            Button(view,"레벨 보상"+(Enumerable.Range(1,20).Any(i=>Progress.CanClaim("l"+i))?"  ●":""),850,604,328,72,()=>{previousScreen="Home";Show("Levels");},null,true,24);
            Button(view,"업적 · 한정 보상"+(Progress.HasRewards?"  ●":""),1198,604,332,72,()=>{previousScreen="Home";Show("Rewards");},null,true,23);
            Box(view,"Party strip",850,703,680,117,new Color(.025f,.04f,.075f,.94f));
            Label(view,"출전 파티",875,715,125,33,18,muted);
            if(s.party.Count==0)Label(view,"첫 번째 파트너를 기다리고 있어요.",875,756,600,38,23,Color.white);
            for(int i=0;i<s.party.Count;i++){int id=s.party[i];Portrait(view,id,Progress.Owned(id).level,1010+i*160,700,103);Label(view,Data.Boo(id).name,1015+i*160,790,120,27,16,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);}
            Box(view,"Progress",60,726,615,94,new Color(.018f,.029f,.06f,.95f));Label(view,"리그 진행  "+s.cleared.Count+" / "+Data.stage_list.Length,85,740,430,34,23,Color.white,FontStyle.Bold);Bar(view,85,791,555,s.cleared.Count/(float)Data.stage_list.Length,cyan,7);
        }
        string GroupName(int i){return new[]{"","워밍업","예선전","본선","결승전"}[i];}
        const int forty=40;
        void Stages()
        {
            Header("챔피언 리그",()=>Show("Home"));
            for(int g=1;g<=4;g++){int group=g;Button(view,GroupName(g),40+(g-1)*390,112,367,59,()=>{stageGroup=group;selectedStage=Data.stage_list.First(x=>x.type==group).id;Show("Stages");},stageGroup==g?cyan:(Color?)null);}
            var list=Data.stage_list.Where(x=>x.type==stageGroup).ToArray();var scroll=Scroll(view,40,197,475,574,list.Length*100);
            for(int i=0;i<list.Length;i++){var st=list[i];bool open=Progress.StageOpen(st.id);Button(scroll,(Progress.Save.cleared.Contains(st.id)?"✓  ":open?"▶  ":"잠김  ")+st.name,0,i*100,463,82,()=>{selectedStage=st.id;Progress.Save.lastStage=st.id;Progress.Persist();Show("Stages");},selectedStage==st.id?yellow:(Color?)null,true,22);}
            var s=Data.Stage(selectedStage);Box(view,"Stage details",548,197,1012,574,panel);Tag(view,GroupName(s.type)+"  /  "+s.id.ToString("00"),582,224,260,cyan);
            Label(view,s.npc+"와의 배틀",582,281,910,63,42,Color.white,FontStyle.Bold);Label(view,"“"+s.npc_dialogue+"”",584,351,920,55,25,muted);
            Label(view,"주특기  "+Elements.Name(s.npc_type)+"   ·   상대 LV."+s.level,584,435,780,36,23,ElementColor(s.npc_type),FontStyle.Bold);
            string good=string.Join(" / ",Enumerable.Range(1,5).Where(t=>Elements.Strong(t,s.npc_type)).Select(Elements.Name));
            string bad=string.Join(" / ",Enumerable.Range(1,5).Where(t=>Elements.Strong(s.npc_type,t)).Select(Elements.Name));
            Label(view,"유리한 상성  "+good+"\n주의할 상성  "+bad,584,486,850,72,21,muted);
            Label(view,"첫 클리어 보상"+(Progress.Save.cleared.Contains(s.id)?"  ·  수령 완료":""),584,589,870,35,20,Color.white,FontStyle.Bold);
            Label(view,RewardText(Data.stage_rewards.Where(x=>x.owner_id==s.id)),584,634,895,59,21,yellow);
            Label(view,"경험치 +"+s.experience+"  ·  재도전 시에도 획득",584,706,850,29,18,muted);
            Button(view,"파티 편성",40,798,475,62,()=>Show("Collection"));
            Label(view,Progress.StageOpen(s.id)?"준비한 방부로 상대의 약점을 공략하세요.":"앞선 스테이지를 모두 클리어하면 열립니다.",550,799,625,62,20,muted);
            Button(view,"배틀 시작  ›",1200,794,360,67,()=>StartBattle(s.id),cyan,Progress.StageOpen(s.id)&&Progress.Save.party.Count>0);
        }
        void Collection()
        {
            Header("방부 도감",()=>Show("Home"));var b=Data.Boo(selectedBoo);bool owned=Progress.Owns(b.id);int level=owned?Progress.Owned(b.id).level:1;
            Box(view,"Portrait panel",40,119,445,580,panel);Tag(view,"NO. "+b.id+"  /  "+(owned?"보유":"미보유"),70,144,240,owned?yellow:muted);
            Portrait(view,b.id,level,73,215,375);Label(view,b.name,70,577,380,52,39,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
            Label(view,owned?"LV. "+level.ToString("00")+(level>=10?"  ·  진화":"") : "입양 해금 LV. "+b.unlockLevel,65,644,390,32,21,muted,FontStyle.Bold,TextAnchor.MiddleCenter);
            Label(view,b.id+"  |  "+b.name,527,118,700,56,36,Color.white,FontStyle.Bold);Tag(view,Elements.Name(b.type)+" 속성",1340,132,220,ElementColor(b.type));
            Label(view,b.description,528,179,1020,44,23,muted);
            var stats=new[]{"HP","공격력","방어력"};var values=new[]{Progress.Stat(b,level,"hp"),Progress.Stat(b,level,"atk"),Progress.Stat(b,level,"def")};
            for(int i=0;i<3;i++){Box(view,"Stat",529+i*343,239,317,94,panel);Label(view,stats[i],547+i*343,250,265,27,18,muted);Label(view,values[i].ToString(),547+i*343,281,265,37,29,Color.white,FontStyle.Bold);}
            Label(view,"스킬",529,353,900,34,24,Color.white,FontStyle.Bold);
            for(int i=0;i<3;i++){var skill=Data.Skill(b.skillIds[i]);Box(view,"Skill",529,401+i*69,1031,59,panel);Label(view,skill.name,547,406+i*69,190,46,22,ElementColor(b.type),FontStyle.Bold);Label(view,skill.description,751,407+i*69,674,45,19,muted);Label(view,skill.charge_count+" / "+skill.charge_count,1430,407+i*69,112,45,19,Color.white,FontStyle.Bold,TextAnchor.MiddleRight);}
            Button(view,owned?Progress.Save.party.Contains(b.id)?"파티에서 해제":"파티에 편성":Progress.CanAdopt(b.id)?"파트너로 입양":"플레이어 LV."+b.unlockLevel+"에 해금",529,629,493,62,()=>PartyOrAdopt(b.id),owned?cyan:yellow,owned||Progress.CanAdopt(b.id));
            Button(view,"출전 파티  "+Progress.Save.party.Count+" / 3",1045,629,515,62,PartyPopup,null,true,22);
            Label(view,"수집  "+Progress.Save.owned.Count+" / 10",40,719,500,31,22,Color.white,FontStyle.Bold);
            for(int i=0;i<Data.bangboo.Length;i++){var boo=Data.bangboo[i];float x=40+i*153;bool has=Progress.Owns(boo.id);var button=Button(view,"",x,762,143,90,()=>{selectedBoo=boo.id;Show("Collection");},boo.id==selectedBoo?ElementColor(boo.type):(Color?)null);Portrait(button.transform,boo.id,has?Progress.Owned(boo.id).level:1,-1,-7,84);Label(button.transform,boo.name,69,11,69,35,17,boo.id==selectedBoo?ink:Color.white,FontStyle.Bold);Label(button.transform,has?"보유":"LV."+boo.unlockLevel,69,49,70,26,15,boo.id==selectedBoo?ink:muted);}
        }
        void PartyOrAdopt(int id)
        {
            if(!Progress.Owns(id)){var p=OpenModal("파트너 입양",610,330);Label(p,Data.Boo(id).name+"를 입양할까요?\n비용 없이 입양하고 도감에 등록합니다.",35,91,545,94,24,Color.white);Button(p,"입양하기",35,224,258, sixty,()=>{Progress.Adopt(id);Show("Collection");Toast(Data.Boo(id).name+"가 동료가 되었습니다!");},yellow);Button(p,"취소",313,224,260,sixty,CloseModal);return;}
            if(Progress.Save.party.Contains(id)) {
                if(Progress.Save.party.Count==1){Toast("출전 파티에는 최소 1명의 방부가 필요합니다.");return;}
                var p=OpenModal("파티 편성 해제",610,300);Label(p,Data.Boo(id).name+"를 파티에서 해제할까요?",35,94,540,68,24);Button(p,"해제",35,200,260,60,()=>{Progress.SetParty(Progress.Save.party.Where(x=>x!=id));Show("Collection");},yellow);Button(p,"취소",315,200,260,60,CloseModal);
            } else if(Progress.Save.party.Count<3){Progress.SetParty(Progress.Save.party.Concat(new[]{id}));Show("Collection");}
            else {var p=OpenModal("교체할 파티원을 선택하세요",800,360);for(int i=0;i<3;i++){int slot=i;int old=Progress.Save.party[i];Portrait(p,old,Progress.Owned(old).level,40+i*249,78,155);Button(p,Data.Boo(old).name,30+i*250,255,230,60,()=>{var list=Progress.Save.party.ToList();list[slot]=id;Progress.SetParty(list);Show("Collection");},cyan);}}
        }
        const int sixty=60;
        void PartyPopup()
        {
            var p=OpenModal("출전 파티 · 첫 번째 방부가 선봉입니다",900,410);
            if(Progress.Save.party.Count==0){Label(p,"아직 편성한 방부가 없습니다.",35,110,830,60,25);return;}
            for(int i=0;i<Progress.Save.party.Count;i++){int slot=i;int id=Progress.Save.party[i];Portrait(p,id,Progress.Owned(id).level,35+i*287,87,180);Label(p,Data.Boo(id).name+"  LV."+Progress.Owned(id).level,30+i*287,265,265,36,22,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);Button(p,i==0?"현재 선봉":"선봉으로",35+i*287,321,254,55,()=>{var list=Progress.Save.party.ToList();list.RemoveAt(slot);list.Insert(0,id);Progress.SetParty(list);Show("Collection");},i==0?(Color?)null:cyan,i!=0,20);}
        }
    }
}

