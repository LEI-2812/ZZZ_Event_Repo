using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeastBeat
{
    // Data survives scene changes; presentation objects never do.
    public static class BeastBeatSession
    {
        public static string BangbooReturnScenePath="Assets/Scenes/Main Scene.unity";
        public static string HomeScenePath="Assets/Scenes/Main Scene.unity";
        public static string StageReturnScenePath="Assets/Scenes/Main Scene.unity";
        public static string RewardReturnScenePath="Assets/Scenes/Main Scene.unity";
        public static ProgressService Progress;
        public static BattleEngine Battle;
        public static int SelectedEvent;
        public static int StageGroup=1, SelectedStage=1, SelectedBoo=11, RewardGroup;
        public static string BattleLine="", PreviousScreen="Home";
        public static List<string> History=new List<string>(), ResultNotes=new List<string>();
        public static bool TutorialPending;
        public static bool PreviewBattle;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            BangbooReturnScenePath="Assets/Scenes/Main Scene.unity";HomeScenePath="Assets/Scenes/Main Scene.unity";StageReturnScenePath="Assets/Scenes/Main Scene.unity";RewardReturnScenePath="Assets/Scenes/Main Scene.unity";Progress=null;Battle=null;SelectedEvent=0;StageGroup=1;SelectedStage=1;SelectedBoo=11;RewardGroup=0;
            BattleLine="";PreviousScreen="Home";History=new List<string>();ResultNotes=new List<string>();TutorialPending=false;PreviewBattle=false;
        }
        // 씬 이동 전에 공통 데이터를 한 번 검증하고 기존 저장 상태에 연결합니다.
        public static ProgressService EnsureProgress(bool refresh = false)
        {
            if (Progress == null) Progress = new ProgressService(GameData.Load());
            else if (refresh) Progress.ReloadData(GameData.Load());
            return Progress;
        }
        public static string SceneName(string screen)
        {
            switch(screen){case "Entry":return "Event List Scene";case "Home":return "Main Scene";case "Stages":return "Stage List Scene";case "Battle":return "Battle Scene";case "Result":return "Clear Scene";case "Rewards":return "Reward List Scene";case "Collection":return "Bangboo List Scene";case "Levels":return "Level Complete List Scene";default:throw new ArgumentException(screen);}
        }
        public static string ScreenName(string scene)
        {
            switch(scene){case "Event List Scene":return "Entry";case "Main Scene":return "Home";case "Stage List Scene":return "Stages";case "Battle Scene":return "Battle";case "Clear Scene":return "Result";case "Reward List Scene":return "Rewards";case "Bangboo List Scene":return "Collection";case "Level Complete List Scene":return "Levels";default:return "Entry";}
        }
    }
}
