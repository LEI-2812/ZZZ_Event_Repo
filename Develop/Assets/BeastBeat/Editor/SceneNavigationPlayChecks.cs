using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeastBeat.Editor
{
    // 실제 버튼 UnityEvent를 실행하는 Play Mode 검증입니다. 임시 저장 경로만 사용합니다.
    [InitializeOnLoad]
    public static class SceneNavigationPlayChecks
    {
        const string Key = "BeastBeat.NavigationCheck";
        static double next;
        static readonly string[] expected = { Move_Scene.Entry, Move_Scene.Home, Move_Scene.Stages, Move_Scene.Home,
            Move_Scene.Levels, Move_Scene.Home, Move_Scene.Rewards, Move_Scene.Home, Move_Scene.Collection,
            Move_Scene.Home, Move_Scene.Stages, Move_Scene.Battle, Move_Scene.Stages, Move_Scene.Battle,
            Move_Scene.Result, Move_Scene.Stages, Move_Scene.Home, Move_Scene.Entry };
        static readonly string[] actions = { "OpenMainScene", "OpenLeague", "GoBack", "OpenPlayerLevel", "GoBack",
            "OpenLimitedRewards", "GoBack", "OpenBangboo", "GoBack", "OpenLeague", "StartBattle", "ExitBattle",
            "StartBattle", "RESULT", "Confirm", "GoBack", "GoBack" };
        static SceneNavigationPlayChecks() { EditorApplication.update += Tick; }
        public static string Status => SessionState.GetString(Key+"Result", "Not started");
        public static void Start()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before checks.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene before checks.");
            SessionState.SetString(Key+"Original", SceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene(Move_Scene.Entry);
            SessionState.SetInt(Key+"Step",0); SessionState.SetString(Key+"Result","Running");
            SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
        }
        static T Find<T>() where T:Behaviour => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).First(x=>x.enabled);
        static void Tick()
        {
            if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < next) return;
            next=EditorApplication.timeSinceStartup+.6;
            if (Move_Scene.IsLoading) return;
            try
            {
                int step=SessionState.GetInt(Key+"Step",0);
                string path=SceneManager.GetActiveScene().path;
                if(path!=expected[step]) throw new Exception("Step "+step+": expected "+expected[step]+", got "+path);
                if(step==0) {
                    var data=GameData.Load();
                    var progress=new ProgressService(data,Path.Combine(Application.temporaryCachePath,"NavCheck-"+Guid.NewGuid().ToString("N"),"save.json"));
                    progress.Save.configuredParty=new[]{1,2,3};progress.Save.party=new System.Collections.Generic.List<int>{1,2,3};
                    foreach(int id in progress.Save.party)if(!progress.Owns(id))progress.Save.owned.Add(new OwnedBoo{id=id,level=1});
                    BeastBeatSession.Progress=progress;
                    var entry=Find<EventEntryActions>();entry.ShowPermanent();entry.SelectEvent(1);
                }
                if(step==11) { var ui=Find<BattleSceneActions>(); if(ui.Battle==null)throw new Exception("Battle not initialized"); ui.Pause(); }
                if(step==14 && Find<ClearSceneActions>().Outcome==null)throw new Exception("Result snapshot missing");
                if(step==actions.Length){Finish("PASS: 17 button/result transitions, duplicate click blocked, battle/result data initialized");return;}
                if(actions[step]=="RESULT") {
                    var battle=BeastBeatSession.Battle;battle.Finished=battle.Won=true;
                    BattleOutcome.Complete(battle,true);Move_Scene.For(Find<BattleSceneActions>()).OpenResult();
                } else {
                    var button=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>
                        Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentTarget(i) is Move_Scene && b.onClick.GetPersistentMethodName(i)==actions[step]));
                    if(!button.interactable)throw new Exception("Button disabled: "+button.name);
                    button.onClick.Invoke();
                    if(step==3)Move_Scene.For(Find<MainSceneActions>()).OpenLimitedRewards();
                }
                SessionState.SetInt(Key+"Step",step+1);
            }
            catch(Exception ex){Finish("FAIL: "+ex.Message);}
        }
        static void Finish(string result)
        {
            SessionState.SetString(Key+"Result",result);SessionState.SetBool(Key,false);
            Debug.Log("[NavigationCheck] "+result);EditorApplication.ExitPlaymode();
        }
    }
}
