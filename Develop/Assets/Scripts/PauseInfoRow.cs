using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BeastBeat {
 public sealed class PauseInfoRow:MonoBehaviour {
  public Button button; public TMP_Text label; public int Id {get;private set;}
  BattleSceneActions owner;
  public void Bind(BattleSceneActions screen,PauseEntry entry){owner=screen;Id=entry.id;label.text=entry.name.Length>10?entry.name.Substring(0,10):entry.name;}
  public void Select(){if(owner)owner.SelectInfo(Id);}
  public void SetSelected(bool value){button.image.color=value?new Color(1,.86f,.1f):new Color(.13f,.15f,.18f);label.color=value?Color.black:Color.white;}
 }
}
