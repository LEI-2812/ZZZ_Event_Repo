using UnityEngine;

namespace BeastBeat
{
    // Data survives scene changes; presentation objects never do.
    public static class BeastBeatSession
    {
        public static string BangbooReturnScenePath="Assets/Scenes/Main Scene.unity";
        public static string StageReturnScenePath="Assets/Scenes/Main Scene.unity";
        public static string RewardReturnScenePath="Assets/Scenes/Main Scene.unity";
        public static ProgressService Progress;
        public static BattleEngine Battle;
        public static int SelectedEvent;
        public static int StageGroup=1, SelectedStage=1, SelectedBoo=11;
        public static bool PreviewBattle;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            BangbooReturnScenePath="Assets/Scenes/Main Scene.unity";StageReturnScenePath="Assets/Scenes/Main Scene.unity";RewardReturnScenePath="Assets/Scenes/Main Scene.unity";Progress=null;Battle=null;SelectedEvent=0;StageGroup=1;SelectedStage=1;SelectedBoo=11;
            PreviewBattle=false;
        }
        // 씬 이동 전에 공통 데이터를 한 번 검증하고 기존 저장 상태에 연결합니다.
        public static ProgressService EnsureProgress(bool refresh = false)
        {
            if (Progress == null) Progress = new ProgressService(GameData.Load());
            else if (refresh) Progress.ReloadData(GameData.Load());
            return Progress;
        }
    }
}
