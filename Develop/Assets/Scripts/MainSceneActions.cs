using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeastBeat
{
    // Only navigation and game state. The scene owns all UI objects and styling.
    public sealed class MainSceneActions : MonoBehaviour
    {
        [Header("Scene destinations")]
        public string entryScene="Assets/Scenes/New/Event List Scene.unity";
        public string levelScene="Assets/Scenes/rw_level.unity";
        public string leagueScene="Assets/Scenes/New/Stage List Scene.unity";
        public string bangbooScene="Assets/Scenes/bangboo_list.unity";
        public string rewardScene="Assets/Scenes/reward_list.unity";
        bool navigating;

        void Awake()
        {
            if (!enabled) return;
            BeastBeatSession.HomeScenePath=gameObject.scene.path;
            EnsureProgress();
        }
        void EnsureProgress()
        {
            if(BeastBeatSession.Progress!=null)return;
            var progress=new ProgressService(GameData.Load());
            BeastBeatSession.Progress=progress;
            var stage=progress.Data.ResolveStage(progress.Save.lastStage);
            BeastBeatSession.SelectedStage=stage==null?0:stage.id;
            BeastBeatSession.StageGroup=stage==null?1:stage.type;
        }
        public void GoBack(){Open(entryScene);}
        public void OpenPlayerLevel()
        {
            BeastBeatSession.PreviousScreen="Home";
            BeastBeatSession.RewardGroup=0;
            Open(levelScene);
        }
        public void OpenLeague()
        {
            EnsureProgress();
            BeastBeatSession.StageReturnScenePath=gameObject.scene.path;
            // Match the original main: adopt a partner before entering the league.
            if(BeastBeatSession.Progress.Save.party.Count==0){BeastBeatSession.SelectedBoo=11;Open(bangbooScene);}
            else Open(leagueScene);
        }
        public void OpenBangboo(){Open(bangbooScene);}
        public void OpenLimitedRewards()
        {
            BeastBeatSession.PreviousScreen="Home";
            BeastBeatSession.RewardGroup=0;
            Open(rewardScene);
        }
        void Open(string path)
        {
            if(navigating)return;
            if(!Application.CanStreamedLevelBeLoaded(path)){Debug.LogError("Scene is not registered: "+path,this);return;}
            navigating=true;SceneManager.LoadScene(path);
        }
    }
}
