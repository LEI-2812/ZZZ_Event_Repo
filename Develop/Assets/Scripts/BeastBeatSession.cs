using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeastBeat
{
    // Data survives scene changes; presentation objects never do.
    public static class BeastBeatSession
    {
        public static string HomeScenePath="Assets/Scenes/New/Main Scene.unity";
        public static string StageReturnScenePath="Assets/Scenes/New/Main Scene.unity";
        public static ProgressService Progress;
        public static BattleEngine Battle;
        public static int StageGroup=1, SelectedStage=1, SelectedBoo=11, RewardGroup;
        public static string BattleLine="", PreviousScreen="Home";
        public static List<string> History=new List<string>(), ResultNotes=new List<string>();
        public static bool TutorialPending;
        public static bool PreviewBattle;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            HomeScenePath="Assets/Scenes/New/Main Scene.unity";StageReturnScenePath="Assets/Scenes/New/Main Scene.unity";Progress=null;Battle=null;StageGroup=1;SelectedStage=1;SelectedBoo=11;RewardGroup=0;
            BattleLine="";PreviousScreen="Home";History=new List<string>();ResultNotes=new List<string>();TutorialPending=false;PreviewBattle=false;
        }
        public static string SceneName(string screen)
        {
            switch(screen){case "Entry":return "New/Event List Scene";case "Home":return "New/Main Scene";case "Stages":return "New/Stage List Scene";case "Battle":return "battle";case "Result":return "clear";case "Rewards":return "reward_list";case "Collection":return "bangboo_list";case "Levels":return "rw_level";default:throw new ArgumentException(screen);}
        }
        public static string ScreenName(string scene)
        {
            switch(scene){case "Event List Scene":return "Entry";case "Main Scene":return "Home";case "Stage List Scene":return "Stages";case "battle":return "Battle";case "clear":return "Result";case "reward_list":return "Rewards";case "bangboo_list":return "Collection";case "rw_level":return "Levels";default:return "Entry";}
        }
    }
}
