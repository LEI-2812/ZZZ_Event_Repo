using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeastBeat
{
    // 씬 이동 버튼은 이 컴포넌트에 연결합니다. 화면 표시/전투 처리는 각 화면 스크립트가 담당합니다.
    [DisallowMultipleComponent]
    public sealed class Move_Scene : MonoBehaviour
    {
        public const string Entry = "Assets/Scenes/Event List Scene.unity";
        public const string Home = "Assets/Scenes/Main Scene.unity";
        public const string Stages = "Assets/Scenes/Stage List Scene.unity";
        public const string Battle = "Assets/Scenes/Battle Scene.unity";
        public const string Result = "Assets/Scenes/Clear Scene.unity";
        public const string Collection = "Assets/Scenes/Bangboo List Scene.unity";
        public const string Rewards = "Assets/Scenes/Reward List Scene.unity";
        public const string Levels = "Assets/Scenes/Level Complete List Scene.unity";
        public TMP_Text errorText;
        static bool loading;
        float nextWorkbookRetry;
        public static bool IsLoading => loading;

        // 파일 잠금이 풀리면 같은 대기 목록을 다시 저장합니다. 다른 씬으로 이동해도 재시도합니다.
        void Update()
        {
            if (Time.unscaledTime < nextWorkbookRetry) return;
            nextWorkbookRetry = Time.unscaledTime + 3f;
            BeastBeatSession.Progress?.RetryWorkbookUnlocks();
        }

        // 도메인 리로드를 꺼도 이전 실행의 이동 잠금이 남지 않게 초기화합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLoading() { loading = false; }

        public static Move_Scene For(Component owner)
        {
            var navigation = owner.GetComponent<Move_Scene>();
            return navigation ? navigation : owner.gameObject.AddComponent<Move_Scene>();
        }

        void Report(Exception ex)
        {
            if (errorText) errorText.text = "씬 이동 실패: " + ex.Message;
            Debug.LogError("[Move_Scene] " + gameObject.scene.path + " | " + ex.Message, this);
        }

        // 목적지를 먼저 확인하고 데이터를 준비합니다. 실패하면 이동하지 않아 빈 화면 진입을 막습니다.
        // 전역 잠금은 서로 다른 버튼을 연속 클릭해도 첫 요청 하나만 실행되게 합니다.
        public bool Navigate(string path, Action prepare = null, bool refreshData = true)
        {
            if (!Application.isPlaying || loading || gameObject.scene != SceneManager.GetActiveScene()) return false;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Application.CanStreamedLevelBeLoaded(path))
                    throw new InvalidOperationException("Build Settings에 등록되지 않은 씬: " + path);
                loading = true;
                if (refreshData) BeastBeatSession.EnsureProgress(true);
                prepare?.Invoke();
                var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
                if (operation == null) throw new InvalidOperationException("씬 로드를 시작하지 못했습니다: " + path);
                operation.completed += _ => loading = false;
                Debug.Log("[Move_Scene] " + gameObject.scene.path + " → " + path, this);
                return true;
            }
            catch (Exception ex) { loading = false; Report(ex); return false; }
        }

        public void OpenMainScene()
        {
            var entry = GetComponent<EventEntryActions>();
            if (!entry || entry.SelectedId == 0) return;
            Navigate(Home, () => BeastBeatSession.SelectedEvent = entry.SelectedId);
        }
        public void OpenLevelRewards() => OpenPlayerLevel();
        public void OpenPlayerLevel() => Navigate(Levels, RememberRewardReturn);
        public void OpenLimitedRewards() => Navigate(Rewards, RememberRewardReturn);
        void RememberRewardReturn()
        {
            BeastBeatSession.RewardReturnScenePath = gameObject.scene.path;
            BeastBeatSession.PreviousScreen = gameObject.scene.path == Entry ? "Entry" : "Home";
            BeastBeatSession.RewardGroup = 0;
        }
        public void OpenBangboo() => Navigate(Collection, () => BeastBeatSession.BangbooReturnScenePath = gameObject.scene.path);
        // 파티가 비어 있어도 리그 버튼의 목적지는 바꾸지 않습니다. 편성 오류는 배틀 시작 전에 안내합니다.
        public void OpenLeague() => Navigate(Stages, () => BeastBeatSession.StageReturnScenePath = gameObject.scene.path);
        public void StartBattle() => Navigate(Battle, () =>
        {
            var screen = GetComponent<StageListActions>();
            if (!screen) throw new InvalidOperationException("스테이지 화면 연결 누락");
            if (!screen.TryPrepareBattle(out var message)) throw new InvalidOperationException(message);
        });
        public void Confirm() => Navigate(Stages, () =>
        {
            var screen = GetComponent<ClearSceneActions>();
            if (!screen) throw new InvalidOperationException("결과 화면 연결 누락");
            if (!screen.TryPrepareReturn(out var message)) throw new InvalidOperationException(message);
        }, false);
        public void ExitBattle() => Navigate(Stages, () =>
        {
            var screen = GetComponent<BattleSceneActions>();
            if (screen) screen.PrepareExit();
            BeastBeatSession.Battle = null;
        }, false);
        // 완료된 배틀의 스냅샷을 유지하므로 결과 화면 진입 직전에는 엑셀을 다시 덮어쓰지 않습니다.
        public bool OpenResult() => Navigate(Result, null, false);
        public void GoBack()
        {
            string current = gameObject.scene.path;
            string path = current == Home ? Entry : current == Stages ? BeastBeatSession.StageReturnScenePath :
                current == Collection ? BeastBeatSession.BangbooReturnScenePath :
                current == Rewards || current == Levels ? BeastBeatSession.RewardReturnScenePath : Home;
            // 복귀는 데이터 오류가 있어도 허용합니다. 잘못된 복귀 주소는 명시적으로 실패합니다.
            Navigate(path, null, false);
        }
    }
}
