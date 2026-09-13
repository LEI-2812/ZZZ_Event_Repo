using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeastBeat
{
    public sealed class RewardListRow:MonoBehaviour
    {
        public TMP_Text titleLabel,progressLabel,buttonLabel;
        public Button actionButton;
        public Image background;
        public GameObject selectedMark;
        public Image[] itemImages;
        public TMP_Text[] itemLabels;
        [SerializeField] RewardScreenActions owner;
        [SerializeField] int groupId;
        [SerializeField] string claimKey;
        public string ClaimKey=>claimKey;
        public int GroupId=>groupId;
        public void Group(RewardScreenActions screen,int id,string title,string count,bool selected)
        {
            owner=screen;groupId=id;claimKey="";titleLabel.text=title;progressLabel.text=count;
            background.color=selected?new Color(.9f,.88f,.32f):new Color(.075f,.12f,.2f);
            titleLabel.color=progressLabel.color=selected?new Color(.05f,.09f,.15f):Color.white;
            if(selectedMark)selectedMark.SetActive(selected);actionButton.interactable=true;
        }
        public void Reward(RewardScreenActions screen,string key,string title,RewardData[] items,string progress,bool enabled,bool claimed)
        {
            owner=screen;claimKey=key;titleLabel.text=title;progressLabel.text=progress;actionButton.interactable=enabled;
            buttonLabel.text=claimed?"수령 완료":enabled?"수령하기":"진행 중";
            for(int i=0;i<itemImages.Length;i++){
                if(i>=items.Length){itemLabels[i].text="";itemImages[i].color=Color.clear;continue;}
                var item=screen.Data.items;var definition=System.Array.Find(item,d=>d.id==items[i].items_id);
                itemLabels[i].text=definition.name+"\n×"+items[i].amount;
                itemImages[i].color=definition.grade==1?new Color(.62f,.46f,.16f):definition.grade==2?new Color(.4f,.28f,.56f):new Color(.2f,.4f,.5f);
            }
        }
        public void InvokeAction(){if(!owner)return;if(string.IsNullOrEmpty(claimKey))owner.SelectGroup(groupId);else owner.ClaimRow(claimKey);}
    }
}
