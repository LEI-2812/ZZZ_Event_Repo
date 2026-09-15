using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BeastBeat {
 public sealed class BangbooCollectionRow:MonoBehaviour {
  public BangbooSceneActions owner;public Button button;public TMP_Text label;public int bid;
  public void Select(){if(bid!=0&&owner)owner.SelectBangboo(bid);}
 }
}
