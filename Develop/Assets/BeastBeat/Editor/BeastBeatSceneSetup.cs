using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeastBeat.Editor
{
    public static class BeastBeatSceneSetup
    {
        public static readonly string[] Names={"eventlist","main","stage_list","battle","clear","reward_list","bangboo_list","rw_level"};
        [MenuItem("Beast Beat/8개 화면 씬 구성")]
        public static void CreateScenes()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play mode first.");
            Directory.CreateDirectory("Assets/BeastBeat/Scenes");Directory.CreateDirectory("Assets/BeastBeat/Generated/Arena");AssetDatabase.Refresh();
            var previous=SceneManager.GetActiveScene();
            foreach(string name in Names){
                string path="Assets/BeastBeat/Scenes/"+name+".unity";
                if(File.Exists(path))continue; // Preserve edits when this utility is rerun.
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
                var root=new GameObject(name+" · Screen Controller");var app=root.AddComponent<BeastBeatApp>();app.sceneScreen=BeastBeatSession.ScreenName(name);
                var canvasGO=new GameObject(name+" · UI Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGO.transform.SetParent(root.transform,false);
                var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;app.sceneCanvas=canvas;
                var scaler=canvasGO.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
                if(name=="battle"){
                    app.arena=BattleArenaGeometry.Create();
                    RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.14f,.19f,.26f);RenderSettings.fog=false;
                    PersistGeometry(app.arena.gameObject);
                }else{
                    var cameraGO=new GameObject(name+" · UI Camera");cameraGO.tag="MainCamera";var camera=cameraGO.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.033f,.055f);camera.cullingMask=0;cameraGO.AddComponent<AudioListener>();
                }
                EditorSceneManager.SaveScene(scene,path);EditorSceneManager.CloseScene(scene,true);
            }
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            var own=new HashSet<string>(Names.Select(n=>"Assets/BeastBeat/Scenes/"+n+".unity"));
            var other=EditorBuildSettings.scenes.Where(s=>!own.Contains(s.path)&&s.path!="Assets/BeastBeat/Scenes/BeastBeatStation.unity").ToArray();
            EditorBuildSettings.scenes=Names.Select(n=>new EditorBuildSettingsScene("Assets/BeastBeat/Scenes/"+n+".unity",true)).Concat(other).ToArray();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("8 Beast Beat scenes configured. No player build performed.");
        }
        static void PersistGeometry(GameObject root)
        {
            var materials=new HashSet<Material>();var meshes=new HashSet<Mesh>();int index=0;
            foreach(var r in root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)if(m!=null&&materials.Add(m)&&!AssetDatabase.Contains(m)){m.name="BB_Asset_"+index;AssetDatabase.CreateAsset(m,"Assets/BeastBeat/Generated/Arena/material-"+(index++)+".mat");}
            index=0;foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&f.sharedMesh.name.StartsWith("BB_")&&meshes.Add(f.sharedMesh)&&!AssetDatabase.Contains(f.sharedMesh))AssetDatabase.CreateAsset(f.sharedMesh,"Assets/BeastBeat/Generated/Arena/mesh-"+(index++)+".asset");
        }
        [MenuItem("Beast Beat/씬 열기/eventlist")]
        public static void OpenEntry(){Open("eventlist");}
        [MenuItem("Beast Beat/씬 열기/battle")]
        public static void OpenBattle(){Open("battle");}
        static void Open(string name){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene("Assets/BeastBeat/Scenes/"+name+".unity");}
    }
}
