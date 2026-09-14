using System.Collections.Generic;
using UnityEngine;

namespace BeastBeat
{
    public class BangbooActor3D : MonoBehaviour
    {
        public int bangbooId=11, level=1;
        public Transform visualRoot;
        public Transform opponent;
        public Material[] generatedMaterials;
        Vector3 restPosition;
        float phase,attack,hit,arrival;
        bool initialized;
        public void Configure(int id,int lv)
        {
            if(visualRoot!=null && bangbooId==id && level==lv){InitializePose();return;}
            if(visualRoot!=null){visualRoot.gameObject.SetActive(false);if(Application.isPlaying)Destroy(visualRoot.gameObject);else DestroyImmediate(visualRoot.gameObject);}
            if(Application.isPlaying&&generatedMaterials!=null)foreach(var m in generatedMaterials)if(m!=null&&m.name=="Actor material")Destroy(m);
            bangbooId=id;level=lv;
            var materials=new List<Material>();
            ColorUtility.TryParseHtmlString("#"+Elements.Hex(id/10),out var color);
            var coat=Material(color,materials);var dark=Material(new Color(.025f,.045f,.07f),materials);var white=Material(new Color(.84f,.91f,.94f),materials);var eye=Material(new Color(.85f,1,.35f),materials,true);
            var face=Material(new Color(.008f,.012f,.02f),materials);
            visualRoot=new GameObject("Visual · "+id).transform;visualRoot.SetParent(transform,false);
            Part("Body",id==11?PrimitiveType.Cube:PrimitiveType.Capsule,new Vector3(0,1.12f,0),new Vector3(1.08f,.76f,.77f),coat);
            Part("Face screen",PrimitiveType.Sphere,new Vector3(0,1.35f,-.37f),new Vector3(.9f,.59f,.23f),face);
            for(int s=-1;s<=1;s+=2){
                Part("Ear",PrimitiveType.Capsule,new Vector3(s*.32f,2.04f,0),new Vector3(.23f,.43f,.23f),coat,s*-13);
                Part("Ear inset",PrimitiveType.Capsule,new Vector3(s*.32f,2.08f,-.115f),new Vector3(.10f,.28f,.045f),white,s*-13);
                Part("Eye",PrimitiveType.Capsule,new Vector3(s*.23f,1.38f,-.50f),new Vector3(.10f,.12f,.04f),eye);
                Part("Arm",PrimitiveType.Capsule,new Vector3(s*.60f,.97f,0),new Vector3(.25f,.32f,.25f),dark,s*22);
                Part("Foot",PrimitiveType.Capsule,new Vector3(s*.29f,.40f,-.03f),new Vector3(.29f,.26f,.40f),dark,s*-8);
            }
            Part("Belly",PrimitiveType.Sphere,new Vector3(0,.88f,-.35f),new Vector3(.46f,.34f,.16f),white);
            Part("Core",PrimitiveType.Cube,new Vector3(0,.88f,-.45f),new Vector3(.18f,.18f,.03f),eye,45);
            if(id%2==0)Part("Visor",PrimitiveType.Cube,new Vector3(0,1.7f,-.37f),new Vector3(.85f,.10f,.12f),dark);
            if(id/10==4)Part("Antenna",PrimitiveType.Cube,new Vector3(.4f,2.0f,-.08f),new Vector3(.40f,.12f,.12f),eye,-30);
            if(id/10==5)for(int s=-1;s<=1;s+=2)Part("Wing",PrimitiveType.Cube,new Vector3(s*.68f,1.04f,.26f),new Vector3(.4f,.50f,.09f),coat,s*35);
            if(lv>=10)for(int i=-1;i<=1;i++)Part("Evolved crown",PrimitiveType.Cube,new Vector3(i*.19f,1.86f,-.25f),new Vector3(.12f,.18f,.12f),eye,45);
            generatedMaterials=materials.ToArray();arrival=1;InitializePose();
        }
        void InitializePose(){if(initialized)return;restPosition=transform.localPosition;initialized=true;phase=bangbooId*.27f;}
        static Material Material(Color c,List<Material> list,bool emission=false)
        {
            var template=Resources.Load<Material>("BeastBeat/AvatarMaterial");var m=template?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name="Actor material";m.color=c;m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.35f);
            if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*2);}list.Add(m);return m;
        }
        void Part(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,float tilt=0)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(visualRoot,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=Quaternion.Euler(0,0,tilt);go.GetComponent<Renderer>().sharedMaterial=material;
            var collider=go.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
        }
        public void Attack(){attack=1;}
        public void Hit(){hit=1;}
        void OnDestroy(){if(Application.isPlaying&&generatedMaterials!=null)foreach(var m in generatedMaterials)if(m!=null&&m.name=="Actor material")Destroy(m);}
        void Update()
        {
            if(!initialized||!visualRoot||((BeastBeatApp.Instance&&BeastBeatApp.Instance.IsPaused)||(BattleSceneActions.Instance&&BattleSceneActions.Instance.IsPaused)))return;
            float dt=Time.unscaledDeltaTime;phase+=dt*2.4f;attack=Mathf.Max(0,attack-dt*1.8f);hit=Mathf.Max(0,hit-dt*2.4f);arrival=Mathf.Max(0,arrival-dt*2);
            Vector3 direction=opponent?(opponent.position-transform.position).normalized:Vector3.zero;direction.y=0;
            transform.localPosition=restPosition+direction*Mathf.Sin(attack*Mathf.PI)*1.1f+Vector3.up*(Mathf.Sin(phase)*.035f+Mathf.Sin(arrival*Mathf.PI)*.45f);
            visualRoot.localRotation=Quaternion.Euler(Mathf.Sin(hit*Mathf.PI)*-14,0,Mathf.Sin(hit*Mathf.PI*4)*6);
        }
    }
}

