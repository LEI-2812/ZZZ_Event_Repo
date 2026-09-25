using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeastBeat.Editor
{
    public static class SceneNavigationSetup
    {
        public static readonly string[] Paths = { Move_Scene.Entry, Move_Scene.Home, Move_Scene.Stages,
            Move_Scene.Battle, Move_Scene.Result, Move_Scene.Rewards, Move_Scene.Levels, Move_Scene.Collection };

        // 기존 버튼의 이동 이벤트만 교체합니다. 스킬/탭/보상 수령 이벤트는 건드리지 않습니다.
        [MenuItem("Beast Beat/설정/씬 이동 버튼 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료하세요.");
            var active = SceneManager.GetActiveScene();
            foreach (string path in Paths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                // 열린 씬의 편집 내용은 유지한 채 이동 버튼 연결만 변경하고 저장합니다.
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var button in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Button>(true)))
                    {
                        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        {
                            var target = button.onClick.GetPersistentTarget(i) as MonoBehaviour;
                            if (!target || !target.enabled) continue;
                            string method = button.onClick.GetPersistentMethodName(i);
                            bool move = target is EventEntryActions && new[]{"OpenMainScene","OpenLevelRewards"}.Contains(method) ||
                                target is MainSceneActions && new[]{"GoBack","OpenPlayerLevel","OpenLeague","OpenBangboo","OpenLimitedRewards"}.Contains(method) ||
                                target is StageListActions && new[]{"GoBack","StartBattle"}.Contains(method) ||
                                target is BattleSceneActions && method == "ExitBattle" || target is ClearSceneActions && method == "Confirm" ||
                                (target is BangbooSceneActions || target is RewardScreenActions) && method == "GoBack";
                            if (!move) continue;
                            var nav = target.GetComponent<Move_Scene>();
                            if (!nav) nav = Undo.AddComponent<Move_Scene>(target.gameObject);
                            Undo.RecordObject(nav, "Connect navigation status");
                            nav.errorText = target is EventEntryActions e ? e.statusText : target is StageListActions st ? st.statusText :
                                target is BattleSceneActions b ? b.partyDetails : target is ClearSceneActions c ? c.infoText :
                                target is RewardScreenActions rw ? rw.statusText : target is BangbooSceneActions bc ? bc.statusText : null;
                            UnityAction call;
                            switch (method) {
                                case "OpenMainScene": call = nav.OpenMainScene; break;
                                case "OpenLevelRewards": call = nav.OpenLevelRewards; break;
                                case "OpenPlayerLevel": call = nav.OpenPlayerLevel; break;
                                case "OpenLeague": call = nav.OpenLeague; break;
                                case "OpenBangboo": call = nav.OpenBangboo; break;
                                case "OpenLimitedRewards": call = nav.OpenLimitedRewards; break;
                                case "StartBattle": call = nav.StartBattle; break;
                                case "ExitBattle": call = nav.ExitBattle; break;
                                case "Confirm": call = nav.Confirm; break;
                                default: call = nav.GoBack; break;
                            }
                            Undo.RecordObject(button, "Connect Move_Scene");
                            UnityEventTools.RegisterVoidPersistentListener(button.onClick, i, call);
                            EditorUtility.SetDirty(button); EditorUtility.SetDirty(nav);
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬 저장 실패: " + path);
                }
                finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
        }
    }
}
