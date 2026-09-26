using UnityEngine;
using TMPro;
using System.Linq;

namespace BeastBeat
{
    // 메인 화면의 진행 상태를 초기화합니다. 이동은 Move_Scene에 위임합니다.
    public sealed class MainSceneActions : MonoBehaviour
    {



        TMP_Text levelLabel;
        string levelTemplate;
        int displayedLevel = -1;

        // 씬의 글자 스타일과 LV. 접두사는 유지하고 99 자리만 실제 레벨로 바꿉니다.
        public void RefreshLevel()
        {
            if (!levelLabel)
            {
                levelLabel = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                    .FirstOrDefault(text => text.name == "Txt_Lv");
                if (!levelLabel) return;
                levelTemplate = levelLabel.text;
            }
            var progress = BeastBeatSession.Progress;
            if (progress == null || displayedLevel == progress.Save.level) return;
            displayedLevel = progress.Save.level;
            levelLabel.text = levelTemplate.Replace("99", displayedLevel.ToString("D2"));
        }
        void Update() => RefreshLevel();

        void Awake()
        {
            if (!enabled) return;
            EnsureProgress();
            RefreshLevel();
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
        public void GoBack() => Move_Scene.For(this).GoBack();
        public void OpenPlayerLevel() => Move_Scene.For(this).OpenPlayerLevel();
        public void OpenLeague() => Move_Scene.For(this).OpenLeague();
        public void OpenBangboo() => Move_Scene.For(this).OpenBangboo();
        public void OpenLimitedRewards() => Move_Scene.For(this).OpenLimitedRewards();
    }
}
