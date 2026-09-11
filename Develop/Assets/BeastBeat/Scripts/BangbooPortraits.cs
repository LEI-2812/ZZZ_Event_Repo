using System.Collections.Generic;
using UnityEngine;

namespace BeastBeat
{
    // Replaceable prototype portraits. No original game meshes or downloaded assets.
    public class BangbooPortraits : System.IDisposable
    {
        readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();
        readonly List<Material> materials = new List<Material>();
        Material Mat(Color color, bool glow = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var template=Resources.Load<Material>("BeastBeat/AvatarMaterial");
            var m = template!=null?new Material(template):new Material(shader); m.color=color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",.55f);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*1.6f); }
            materials.Add(m);return m;
        }
        GameObject Shape(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, float angle=0)
        {
            var go=GameObject.CreatePrimitive(type); go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.transform.localRotation=Quaternion.Euler(0,0,angle);go.GetComponent<Renderer>().sharedMaterial=mat;go.layer=30;
            Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        public Texture2D Get(BooData b, bool evolved)
        {
            int key=b.id+(evolved?1000:0); if(cache.TryGetValue(key,out var texture))return texture;
            var root=new GameObject("Portrait rig");root.transform.position=new Vector3(0,-1000,0);
            ColorUtility.TryParseHtmlString("#"+Elements.Hex(b.type),out Color accent);
            var coat=Mat(accent);var dark=Mat(new Color(.035f,.048f,.085f));var face=Mat(new Color(.012f,.016f,.024f));var white=Mat(new Color(.87f,.91f,.94f));var eye=Mat(new Color(.9f,1,.35f),true);
            var model=new GameObject("Bangboo " + b.name);model.transform.SetParent(root.transform,false);
            Shape(model.transform,b.id==11?PrimitiveType.Cube:PrimitiveType.Capsule,new Vector3(0,1.15f,0),new Vector3(1.05f,.78f,.75f),coat);
            Shape(model.transform,PrimitiveType.Sphere,new Vector3(0,1.36f,-.36f),new Vector3(.86f,.59f,.22f),face);
            for(int s=-1;s<=1;s+=2) {
                Shape(model.transform,PrimitiveType.Capsule,new Vector3(s*.32f,2.04f,.02f),new Vector3(.23f,.43f,.22f),b.id%2==0?dark:coat,s*-13);
                Shape(model.transform,PrimitiveType.Capsule,new Vector3(s*.32f,2.09f,-.11f),new Vector3(.10f,.26f,.06f),white,s*-13);
                Shape(model.transform,PrimitiveType.Capsule,new Vector3(s*.22f,1.4f,-.49f),new Vector3(.09f,.115f,.035f),eye);
                Shape(model.transform,PrimitiveType.Capsule,new Vector3(s*.59f,1.01f,0),new Vector3(.23f,.32f,.25f),dark,s*24);
                Shape(model.transform,PrimitiveType.Capsule,new Vector3(s*.28f,.48f,-.02f),new Vector3(.29f,.25f,.36f),dark,s*-8);
            }
            Shape(model.transform,PrimitiveType.Sphere,new Vector3(0,.91f,-.35f),new Vector3(.42f,.32f,.16f),white);
            Shape(model.transform,PrimitiveType.Cube,new Vector3(0,.92f,-.44f),new Vector3(.16f,.16f,.025f),coat,45);
            if(b.type==4) Shape(model.transform,PrimitiveType.Cube,new Vector3(.45f,1.88f,-.06f),new Vector3(.4f,.12f,.18f),eye,-35);
            if(b.type==5) {
                Shape(model.transform,PrimitiveType.Cube,new Vector3(-.62f,1.08f,.2f),new Vector3(.35f,.5f,.08f),coat,-40);
                Shape(model.transform,PrimitiveType.Cube,new Vector3(.62f,1.08f,.2f),new Vector3(.35f,.5f,.08f),coat,40);
            }
            if(evolved) for(int i=-1;i<=1;i++) Shape(model.transform,PrimitiveType.Cube,new Vector3(i*.19f,1.83f,-.28f),new Vector3(.13f,.19f,.13f),eye,45);
            model.transform.localRotation=Quaternion.Euler(0,-17,0);
            var lamp=new GameObject("Key light");lamp.transform.SetParent(root.transform,false);var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.2f;light.cullingMask=1<<30;lamp.transform.rotation=Quaternion.Euler(35,-30,0);
            var fill=new GameObject("Fill light");fill.transform.SetParent(root.transform,false);var l2=fill.AddComponent<Light>();l2.type=LightType.Directional;l2.intensity=.8f;l2.cullingMask=1<<30;fill.transform.rotation=Quaternion.Euler(15,150,0);
            var cameraGo=new GameObject("Portrait camera");cameraGo.transform.SetParent(root.transform,false);var cam=cameraGo.AddComponent<Camera>();
            cam.transform.localPosition=new Vector3(0,1.3f,-5);cam.orthographic=true;cam.orthographicSize=1.35f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.cullingMask=1<<30;cam.enabled=false;
            var rt=RenderTexture.GetTemporary(384,384,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;cam.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;texture=new Texture2D(384,384,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,384,384),0,0);texture.Apply();RenderTexture.active=previous;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(root);cache.Add(key,texture);return texture;
        }
        public void Dispose() { foreach(var t in cache.Values){if(Application.isPlaying)Object.Destroy(t);else Object.DestroyImmediate(t);}foreach(var m in materials){if(Application.isPlaying)Object.Destroy(m);else Object.DestroyImmediate(m);}cache.Clear();materials.Clear(); }
    }
}
