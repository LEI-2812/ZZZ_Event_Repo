using UnityEngine;
namespace BeastBeat
{
    public class PortraitMotion : MonoBehaviour
    {
        RectTransform rect;Vector2 origin;float phase,punch,direction;
        void Start(){rect=(RectTransform)transform;origin=rect.anchoredPosition;phase=origin.x*.01f;}
        public void Punch(float sign){punch=1;direction=sign;}
        void Update(){if(!rect||(BeastBeatApp.Instance!=null&&BeastBeatApp.Instance.IsPaused))return;phase+=Time.unscaledDeltaTime*2;punch=Mathf.Max(0,punch-Time.unscaledDeltaTime*3);rect.anchoredPosition=origin+new Vector2(Mathf.Sin(punch*Mathf.PI)*24*direction,Mathf.Sin(phase)*3);}
    }
}
