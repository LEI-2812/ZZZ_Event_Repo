using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat.Editor
{
    public static class BeastBeatUIAuthoring
    {
        [MenuItem("Beast Beat/UI/현재 씬 UI 최초 생성")]
        public static void AuthorCurrent()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var app=UnityEngine.Object.FindFirstObjectByType<BeastBeatApp>();
            if(!app)throw new InvalidOperationException("Open a Beast Beat scene first.");
            if(app.screenRoot)throw new InvalidOperationException("This scene already has editable UI. Edit its objects directly; regeneration is intentionally disabled to protect your edits.");
            Author(app);EditorSceneManager.SaveScene(app.gameObject.scene);
        }
        public static void AuthorAll(bool includeExisting=false)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            foreach(string name in BeastBeatSceneSetup.Names){
                var scene=EditorSceneManager.OpenScene("Assets/BeastBeat/Scenes/"+name+".unity");
                var app=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BeastBeatApp>(true)).First();
                if(app.screenRoot&&!includeExisting)continue;
                Author(app);EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene("Assets/BeastBeat/Scenes/battle.unity");
            Selection.activeGameObject=UnityEngine.Object.FindFirstObjectByType<BeastBeatApp>().screenRoot.gameObject;
            Debug.Log("Eight scenes now contain editable UI objects. No player build performed.");
        }
        static void Author(BeastBeatApp app)
        {
            const string folder="Assets/BeastBeat/Generated/UI";
            Directory.CreateDirectory(folder);
            string fontPath=folder+"/MalgunGothic.ttf";
            if(!File.Exists(fontPath)){File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"malgun.ttf"),fontPath);AssetDatabase.ImportAsset(fontPath,ImportAssetOptions.ForceSynchronousImport);}
            app.sceneFont=AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            var progress=new ProgressService(GameData.Load(),Path.Combine(Application.temporaryCachePath,"bb-ui-authoring-"+Guid.NewGuid().ToString("N")+".json"));
            progress.Save.owned.AddRange(new[]{new OwnedBoo{id=11},new OwnedBoo{id=21},new OwnedBoo{id=31}});progress.Save.party.AddRange(new[]{11,21,31});progress.Save.tutorialSeen=true;
            BeastBeatSession.Progress=progress;BeastBeatSession.Battle=null;BeastBeatSession.PreviewBattle=false;BeastBeatSession.SelectedStage=1;BeastBeatSession.StageGroup=1;BeastBeatSession.SelectedBoo=11;BeastBeatSession.RewardGroup=0;BeastBeatSession.TutorialPending=false;BeastBeatSession.ResultNotes=new System.Collections.Generic.List<string>();BeastBeatSession.History.Clear();
            if(app.sceneScreen=="Battle"||app.sceneScreen=="Result"){
                var battle=new BattleEngine(progress,1,123);BeastBeatSession.Battle=battle;
                BeastBeatSession.BattleLine=battle.Stage.npc+"가 승부를 걸어왔다!";
                if(app.sceneScreen=="Result"){foreach(var e in battle.enemy)e.hp=1;battle.Act(0);BeastBeatSession.ResultNotes.Add("경험치와 클리어 보상이 이 영역에 표시됩니다.");}
            }
            app.AuthorScreen();app.AuthorVariants();
            int index=0;
            foreach(var image in app.GetComponentsInChildren<RawImage>(true)){
                var texture=image.texture as Texture2D;if(!texture||AssetDatabase.Contains(texture))continue;
                string path=folder+"/"+app.sceneScreen+"-portrait-"+(index++)+".png";
                File.WriteAllBytes(path,texture.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                image.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            foreach(var node in app.GetComponentsInChildren<EditableUINode>(true))node.SaveDefaults();
            EditorUtility.SetDirty(app);AssetDatabase.SaveAssets();
            Debug.Log(app.sceneScreen+": "+app.GetComponentsInChildren<EditableUINode>(true).Length+" editable UI objects authored.");
        }
    }
}
