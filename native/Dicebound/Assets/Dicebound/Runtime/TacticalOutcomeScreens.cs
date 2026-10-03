using System.Linq;
using Dicebound.Tactics;
using UnityEngine;

namespace Dicebound.Presentation
{
    public sealed partial class TacticalDirector
    {
        private TacticalOutcome lastOutcome;
        private void ShowOutcome(TacticalOutcome outcome)
        {
            if(outcome==null)return;
            lastOutcome=outcome;
            string changes=string.Join("\n",outcome.entries.Select(e=>e.title+" · "+e.detail));
            string narrative=string.IsNullOrWhiteSpace(outcome.narrative)?"":outcome.narrative+"\n";
            Notify(outcome.title+"\n"+narrative+changes);
            toastUntil=Time.unscaledTime+3;
        }
        private void OutcomeDetails()
        {
            if(lastOutcome==null){Notify("本次接续后尚未领取奖励。已获得的旧物可在行囊中查看。");return;}
            var frame=Overlay(lastOutcome.title,1250,850);
            Label("Outcome narrative",frame,lastOutcome.narrative,0,248,1060,100,26,TacticalMenuArt.Text);
            var content=InspectionScroll("Outcome entries",frame,new Vector2(0,-32),new Vector2(1060,424),424);
            float offset=12;
            for(int i=0;i<lastOutcome.entries.Count;i++)
            {
                var entry=lastOutcome.entries[i];
                var title=Label("Outcome title "+i,content,entry.title,-8,0,1000,40,28,entry.amount<0?TacticalTheme.Danger:TacticalMenuArt.Gold);
                var detail=Label("Outcome detail "+i,content,entry.detail,-8,0,1000,66,23,TacticalMenuArt.Text);
                float detailHeight=Mathf.Max(54,detail.preferredHeight+8);
                foreach(var text in new[]{title,detail}){text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,1);text.rectTransform.pivot=new Vector2(.5f,1);}
                title.rectTransform.anchoredPosition=new Vector2(-8,-offset);
                detail.rectTransform.anchoredPosition=new Vector2(-8,-offset-44);detail.rectTransform.sizeDelta=new Vector2(1000,detailHeight);
                offset+=44+detailHeight+18;
            }
            content.sizeDelta=new Vector2(content.sizeDelta.x,Mathf.Max(424,offset));
        }
    }
}
