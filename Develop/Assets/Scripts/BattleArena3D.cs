using System.Collections.Generic;
using UnityEngine;

namespace BeastBeat
{
    public class BattleArena3D : MonoBehaviour
    {
        public Camera spectatorCamera;
        public BangbooActor3D playerActor, enemyActor;
        public Transform playerSpawn,enemySpawn;
        public Light impactLight;
        float flash;
        public void Sync(BattleEngine battle)
        {
            playerActor.Configure(battle.Player.id,battle.Player.level);enemyActor.Configure(battle.Enemy.id,battle.Enemy.level);
            playerActor.opponent=enemyActor.transform;enemyActor.opponent=playerActor.transform;
        }
        public void PlayAction(bool enemy,string message)
        {
            if(!message.Contains("피해"))return;
            (enemy?enemyActor:playerActor).Attack();(enemy?playerActor:enemyActor).Hit();
            impactLight.transform.position=(enemy?playerActor:enemyActor).transform.position+Vector3.up*1.6f;impactLight.color=enemy?new Color(1,.35f,.2f):new Color(.2f,.8f,1);flash=1;
        }
        void Update(){if(BeastBeatApp.Instance&&BeastBeatApp.Instance.IsPaused)return;flash=Mathf.Max(0,flash-Time.unscaledDeltaTime*3);if(impactLight)impactLight.intensity=flash*5;}
    }
    // Editor calls this once to author real scene geometry. Runtime uses the saved objects.
    public static class BattleArenaGeometry
    {
        public static BattleArena3D Create()
        {
            var root=new GameObject("Dome Arena");var arena=root.AddComponent<BattleArena3D>();
            var structure=Group(root.transform,"01 Dome shell and ribs");var stage=Group(root.transform,"02 Circular raised stage");var stands=Group(root.transform,"03 Audience terraces");var lights=Group(root.transform,"04 Arena lighting");var actors=Group(root.transform,"05 Fighters");
            var navy=Mat("Dome",new Color(.025f,.06f,.11f));var metal=Mat("Ribs",new Color(.12f,.21f,.29f));var floor=Mat("Stage",new Color(.13f,.25f,.29f));var edge=Mat("Stage fascia",new Color(.045f,.095f,.14f));var cyan=Mat("Cyan emissive",new Color(.12f,.85f,1),true);var gold=Mat("Gold emissive",new Color(1,.74f,.16f),true);var seats=Mat("Seating",new Color(.12f,.19f,.27f));
            MeshObject(structure,"Hemisphere interior",Dome(19,15,64,20),navy,Vector3.zero);
            structure.Find("Hemisphere interior").GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            for(int rib=0;rib<16;rib++){
                float a=rib*Mathf.PI*2/16;Vector3 previous=new Vector3(Mathf.Cos(a)*18.85f,0,Mathf.Sin(a)*18.85f);
                for(int j=1;j<=12;j++){float t=j*Mathf.PI*.5f/12;var next=new Vector3(Mathf.Cos(a)*Mathf.Cos(t)*18.85f,Mathf.Sin(t)*14.9f,Mathf.Sin(a)*Mathf.Cos(t)*18.85f);Beam(structure,"Dome structural rib",previous,next,.095f,metal);previous=next;}
            }
            foreach(float y in new[]{4f,8f,11.5f}){float radius=19*Mathf.Sqrt(1-y*y/(15*15))-.18f;MeshObject(structure,"Ceiling light ring",Ring(radius,.045f),cyan,new Vector3(0,y,0));}
            Shape(stage,"Platform base",PrimitiveType.Cylinder,new Vector3(0,-.32f,0),new Vector3(17,.32f,17),edge);
            // A shallow domed platform top, distinct from the surrounding hemisphere roof.
            MeshObject(stage,"Convex playing surface",Platform(8.25f,.24f),floor,Vector3.zero);
            MeshObject(stage,"Outer neon boundary",Ring(8.1f,.07f),cyan,new Vector3(0,.025f,0));
            MeshObject(stage,"Inner field marking",Ring(6.8f,.035f),gold,new Vector3(0,.09f,0));
            MeshObject(stage,"Center circle",Ring(1.2f,.035f),cyan,new Vector3(0,.245f,0));
            Shape(stage,"Center line",PrimitiveType.Cube,new Vector3(0,.255f,0),new Vector3(.045f,.015f,12.8f),cyan);
            foreach(int side in new[]{-1,1}){MeshObject(stage,"Fighter position ring",Ring(1.3f,.04f),side<0?cyan:gold,new Vector3(side*3.1f,.22f,0));}
            for(int row=0;row<3;row++){
                float radius=11.2f+row*1.45f;
                MeshObject(stands,"Terrace boundary",Ring(radius,.18f),metal,new Vector3(0,.3f+row*.6f,0));
                for(int i=0;i<44;i++){
                    float a=i*Mathf.PI*2/44;var pos=new Vector3(Mathf.Cos(a)*radius,.65f+row*.6f,Mathf.Sin(a)*radius);
                    // Keep an open near-side entrance aisle for the spectator camera.
                    if(pos.z<-6)continue;
                    var chair=Shape(stands,"Audience seat",PrimitiveType.Cube,pos,new Vector3(.64f,.40f,.64f),seats);chair.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
                    Shape(stands,"Seat back",PrimitiveType.Cube,pos+new Vector3(Mathf.Cos(a)*.23f,.35f,Mathf.Sin(a)*.23f),new Vector3(.58f,.48f,.14f),metal).transform.rotation=Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0);
                }
            }
            for(int i=0;i<8;i++){float a=i*Mathf.PI*2/8;var pos=new Vector3(Mathf.Cos(a)*10,5,Mathf.Sin(a)*10);var light=Light(lights,"Arena spotlight",LightType.Spot,pos,i%2==0?new Color(.42f,.75f,1):new Color(1,.8f,.5f),14);light.spotAngle=65;light.range=22;light.transform.LookAt(new Vector3(0,1,0));light.shadows=i%2==0?LightShadows.Soft:LightShadows.None;}
            var key=Light(lights,"Key light",LightType.Directional,new Vector3(0,10,-8),new Color(.86f,.93f,1),1.3f);key.transform.rotation=Quaternion.Euler(45,-25,0);key.shadows=LightShadows.Soft;
            var fill=Light(lights,"Fill light",LightType.Directional,new Vector3(0,8,5),new Color(.35f,.6f,1),.6f);fill.transform.rotation=Quaternion.Euler(25,150,0);
            arena.impactLight=Light(lights,"Skill impact light",LightType.Point,new Vector3(0,2,0),Color.cyan,0);arena.impactLight.range=4;
            arena.playerSpawn=Group(actors,"Player spawn");arena.playerSpawn.localPosition=new Vector3(-3.1f,.12f,0);
            arena.enemySpawn=Group(actors,"Opponent spawn");arena.enemySpawn.localPosition=new Vector3(3.1f,.12f,0);
            arena.playerActor=Actor(actors,"Player Bangboo",arena.playerSpawn.localPosition,-90,11);arena.enemyActor=Actor(actors,"Opponent Bangboo",arena.enemySpawn.localPosition,90,21);
            arena.playerActor.opponent=arena.enemyActor.transform;arena.enemyActor.opponent=arena.playerActor.transform;
            var cameraGo=new GameObject("Spectator Camera");cameraGo.tag="MainCamera";cameraGo.transform.SetParent(root.transform);cameraGo.transform.position=Quaternion.Euler(0,45,0)*new Vector3(0,6,-15);cameraGo.transform.LookAt(new Vector3(0,2.15f,0));
            var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=43;camera.nearClipPlane=.1f;camera.farClipPlane=65;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.01f,.025f,.05f);camera.cullingMask=~(1<<30);cameraGo.AddComponent<AudioListener>();arena.spectatorCamera=camera;
            return arena;
        }
        static BangbooActor3D Actor(Transform parent,string name,Vector3 position,float yaw,int id){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*1.3f;var actor=go.AddComponent<BangbooActor3D>();actor.Configure(id,1);return actor;}
        static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Material Mat(string name,Color color,bool emission=false){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name="BB_"+name;m.color=color;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}return m;}
        static GameObject Shape(Transform p,string n,PrimitiveType type,Vector3 position,Vector3 size,Material material){var go=GameObject.CreatePrimitive(type);go.name=n;go.transform.SetParent(p,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,Material m){var go=Shape(p,n,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(width,(b-a).magnitude*.5f,width),m);go.transform.up=(b-a).normalized;}
        static void MeshObject(Transform p,string n,Mesh mesh,Material m,Vector3 pos){var go=new GameObject(n,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(p,false);go.transform.localPosition=pos;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=m;}
        static Light Light(Transform p,string n,LightType type,Vector3 pos,Color color,float intensity){var go=new GameObject(n);go.transform.SetParent(p,false);go.transform.localPosition=pos;var light=go.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;return light;}
        static Mesh Dome(float radius,float height,int segments,int rings)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();for(int j=0;j<=rings;j++){float latitude=j*Mathf.PI*.5f/rings;for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vertices.Add(new Vector3(Mathf.Cos(a)*Mathf.Cos(latitude)*radius,Mathf.Sin(latitude)*height,Mathf.Sin(a)*Mathf.Cos(latitude)*radius));}}
            for(int j=0;j<rings;j++)for(int i=0;i<segments;i++){int a=j*(segments+1)+i,b=a+segments+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            // Double-sided shell: inside is visible from the spectator's position.
            int count=triangles.Count;for(int i=0;i<count;i+=3)triangles.AddRange(new[]{triangles[i+2],triangles[i+1],triangles[i]});
            var mesh=new Mesh{name="BB_DomeMesh"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);var normals=new List<Vector3>();foreach(var p in vertices)normals.Add(-new Vector3(p.x/(radius*radius),p.y/(height*height),p.z/(radius*radius)).normalized);mesh.SetNormals(normals);mesh.RecalculateBounds();return mesh;
        }
        static Mesh Platform(float radius,float rise)
        {
            const int segments=96,rings=12;var vertices=new List<Vector3>();var tris=new List<int>();
            for(int r=0;r<=rings;r++){float t=r/(float)rings;for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vertices.Add(new Vector3(Mathf.Cos(a)*radius*t,rise*(1-t*t),Mathf.Sin(a)*radius*t));}}
            for(int r=0;r<rings;r++)for(int i=0;i<segments;i++){int a=r*(segments+1)+i,b=a+segments+1;tris.AddRange(new[]{a,a+1,b,a+1,b+1,b});}var mesh=new Mesh{name="BB_PlatformMesh"};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Ring(float radius,float tube)
        {
            const int segments=96,sides=6;var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;for(int j=0;j<=sides;j++){float b=j*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube,Mathf.Sin(a)*(radius+Mathf.Cos(b)*tube)));}}
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;t.AddRange(new[]{a,a+1,b,a+1,b+1,b});}var mesh=new Mesh{name="BB_RingMesh"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}


