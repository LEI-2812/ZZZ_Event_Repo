using UnityEngine;
using UnityEngine.UI;
namespace BeastBeat
{
    // Cosmetic reward: a gently moving, color-cycling arcade backdrop.
    public class ChampionBackdrop : MonoBehaviour
    {
        RawImage image;
        void Awake(){image=GetComponent<RawImage>();}
        void Update(){float t=Time.unscaledTime;image.uvRect=new Rect(.02f+Mathf.Sin(t*.15f)*.015f,.02f+Mathf.Cos(t*.12f)*.015f,.96f,.96f);image.color=Color.Lerp(new Color(.45f,.48f,.8f),new Color(.72f,.45f,.6f),(Mathf.Sin(t*.5f)+1)*.5f);}
    }
}
