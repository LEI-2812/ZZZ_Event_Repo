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
            playerActor.Configure(battle.Progress.Data.Boo(battle.Player.id),battle.Player.level);enemyActor.Configure(battle.Progress.Data.Boo(battle.Enemy.id),battle.Enemy.level);
            playerActor.opponent=enemyActor.transform;enemyActor.opponent=playerActor.transform;
        }
        public void PlayAction(bool enemy,string message)
        {
            if(!message.Contains("피해"))return;
            (enemy?enemyActor:playerActor).Attack();(enemy?playerActor:enemyActor).Hit();
            impactLight.transform.position=(enemy?playerActor:enemyActor).transform.position+Vector3.up*1.6f;impactLight.color=enemy?new Color(1,.35f,.2f):new Color(.2f,.8f,1);flash=1;
        }
        void Update(){if(BattleSceneActions.Instance&&BattleSceneActions.Instance.IsPaused)return;flash=Mathf.Max(0,flash-Time.unscaledDeltaTime*3);if(impactLight)impactLight.intensity=flash*5;}
    }
}
