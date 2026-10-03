using System;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    public sealed partial class TacticalDirector
    {
        private string jadeRewardChoice;
        [Serializable] private sealed class JadePortraitLayout { public JadePortraitFrame[] frames=null; }
        [Serializable] private sealed class JadePortraitFrame { public int x=0,y=0,width=0,height=0; }

        // The same reviewed first-frame rectangle is used by the battlefield sprite.
        // UV selection leaves the original character sheet unchanged.
        private static RawImage JadeUnitPicture(string name,Transform parent,string hero,Vector2 pos,Vector2 size,bool face=false)
        {
            var texture=Resources.Load<Texture2D>("Tactics/Sprites/"+hero+"-sheet-v2");
            var layout=Resources.Load<TextAsset>("Tactics/Sprites/"+hero+"-layout-v2");
            if(!texture||!layout)return null;
            var metadata=JsonUtility.FromJson<JadePortraitLayout>(layout.text);
            if(metadata?.frames==null||metadata.frames.Length==0)return null;
            var frame=metadata.frames[0];
            float x=frame.x,y=frame.y,width=frame.width,height=frame.height;
            if(face){x+=width*.12f;width*=.76f;height*=.66f;}
            float scale=Mathf.Min(size.x/width,size.y/height);
            var image=Ui.Picture(name,parent,texture,Center,pos,new Vector2(width,height)*scale,Color.white);
            image.uvRect=new Rect(x/texture.width,(texture.height-y-height)/texture.height,width/texture.width,height/texture.height);
            return image;
        }

        private void Reward()=>JadeReward();
        private void AscentReward()=>JadeReward();

        private static RawImage JadeTreasurePicture(string name,Transform parent,int index,Vector2 pos,float size)
        {
            var atlas=Resources.Load<Texture2D>("Tactics/Jade/treasure-atlas");
            if(!atlas)return null;
            var image=Ui.Picture(name,parent,atlas,Center,pos,Vector2.one*size,Color.white);
            image.uvRect=new Rect(index/3f,0,1f/3f,1);
            return image;
        }

        private static void JadeRewardReadingBacking(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            // Reuse the quiet centre of the authored ink-jade material, without its heavy frame.
            // Text remains independent of the workshop's bright paving and engraved display stands.
            var texture=Resources.Load<Texture2D>("Tactics/JadeCraft/inset-panel");
            if(!texture){Ui.Panel(name,parent,Center,pos,size,TacticalMenuArt.Ink);return;}
            var image=Ui.Picture(name,parent,texture,Center,pos,size,Color.white);
            image.uvRect=new Rect(.30f,.36f,.40f,.28f);
        }

        private void JadeRewardArt(TacticalReward reward,Transform parent,int index)
        {
            if(reward.kind=="skill")
            {
                JadeTreasurePicture("Reward illustration "+index,parent,2,new Vector2(0,172),304);
                TacticalArt.SkillSymbol("Reward skill seal "+index,parent,reward.skillId,new Vector2(108,50),76);
            }
            else if(reward.kind=="gold")
                JadeTreasurePicture("Reward illustration "+index,parent,1,new Vector2(0,172),306);
            else if(reward.kind=="relic"&&reward.relicId=="lamp")
                JadeTreasurePicture("Reward illustration "+index,parent,0,new Vector2(0,172),316);
            else if(reward.kind=="relic"&&(reward.relicId=="pearl"||reward.relicId=="seal"))
                JadeTreasurePicture("Reward illustration "+index,parent,1,new Vector2(0,172),306);
            else
                TacticalArt.Symbol("Reward illustration "+index,parent,reward.kind=="relic"?"relic-"+reward.relicId:"relic-flask",new Vector2(0,172),Vector2.one*254);
        }

        private void JadeReward()
        {
            Audio.SetTheme("reward");
            var treasureBackdrop=TacticalMenuArt.PlateBackdrop("Jade workshop",page,"treasure-plate",.12f);
            menuMotion.Bind(treasureBackdrop,"treasure-loop.mp4",glassBackdrop:true);
            JadeRewardReadingBacking("Victory heading ink",page,new Vector2(0,469),new Vector2(376,102));
            TacticalMenuArt.Decoration("Victory cloud ornament",page,"cloud",new Vector2(0,468),new Vector2(760,136));
            TacticalMenuArt.Heading("Page heading",page,"战斗胜利",new Vector2(0,470),new Vector2(356,94),64,true).alignment=TextAnchor.MiddleCenter;
            JadeRewardReadingBacking("Victory subtitle ink",page,new Vector2(0,362),new Vector2(420,46));
            Label("Page subtitle",page,"选择一件战利品",0,362,390,42,28,TacticalMenuArt.Text,TextAnchor.MiddleCenter);
            TacticalMenuArt.TextButton("Reward settings",page,"设置",new Vector2(674,476),new Vector2(140,54),Settings,fontSize:24);
            TacticalMenuArt.TextButton("Reward title",page,"标题",new Vector2(844,476),new Vector2(140,54),Title,fontSize:24);
            TacticalMenuArt.TextButton("Reward party details",page,"队伍详情",new Vector2(-816,476),new Vector2(180,54),PartyDetails,fontSize:24);
            TacticalMenuArt.TextButton("Reward inventory",page,"行囊",new Vector2(-608,476),new Vector2(160,54),Inventory,fontSize:24);
            if(!State.rewards.Any(r=>r.id==jadeRewardChoice))jadeRewardChoice=State.rewards.FirstOrDefault()?.id;
            for(int i=0;i<State.rewards.Count;i++)
            {
                var reward=TacticalRules.EffectiveReward(State,State.rewards[i]);bool chosen=reward.id==jadeRewardChoice;
                var frame=TacticalMenuArt.QuietPanel("Reward "+reward.id,page,new Vector2((i-(State.rewards.Count-1)*.5f)*540,-2),new Vector2(500,666)).transform;
                if(chosen)TacticalArt.Outline("Chosen reward rim",frame,Vector2.zero,new Vector2(474,640),new Color(.53f,.86f,.71f,.7f),4);
                var definition=reward.kind=="skill"?TacticalContent.GetSkill(reward.skillId):null;
                var relic=reward.kind=="relic"?TacticalContent.GetRelic(reward.relicId):null;
                var recipient=definition==null?null:SkillDisplayRecipient(definition);
                if(definition!=null)Medallion("Reward skill seal "+i,frame,definition.id,new Vector2(-165,247),88);
                else if(relic!=null)TacticalArt.Symbol("Reward illustration "+i,frame,RelicArtwork(relic.id),new Vector2(-165,247),Vector2.one*104);
                else JadeTreasurePicture("Reward illustration "+i,frame,1,new Vector2(-165,247),116);
                string name=definition?.name??relic?.name??reward.name;
                Label("Reward name "+i,frame,name,57,267,300,62,34,TacticalMenuArt.Gold);
                string category=definition!=null?recipient.name+" · "+(string.IsNullOrEmpty(definition.heroId)?"通用":"专属"):relic!=null?TacticalContent.RelicTag(relic.rarity):"同行补给";
                Label("Reward category "+i,frame,category,57,211,300,40,24,TacticalMenuArt.Muted);
                if(definition!=null)
                {
                    SkillParameterChips("Reward "+i,frame,recipient,definition,145,422);
                    RuleBody("Reward effect "+i,frame,OfferedSkillEffect(recipient,definition),new Vector2(0,23),new Vector2(422,174),24);
                    var upgrade=InspectionPanel("Reward upgrade panel "+i,frame,new Vector2(0,-151),new Vector2(422,112));
                    Label("Reward upgrade "+i,upgrade,InspectedUpgradeSummary(recipient,definition),0,0,394,104,22,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
                }
                else
                {
                    if(relic!=null){Label("Reward trigger label "+i,frame,"触发",-157,131,104,40,24,TacticalMenuArt.Muted);RuleBody("Reward trigger "+i,frame,RelicTrigger(relic.id),new Vector2(50,116),new Vector2(322,82),25);}
                    RuleBody("Reward effect "+i,frame,ReadableEffect(relic?.text??reward.text),new Vector2(0,-49),new Vector2(422,256),27);
                }
                var request=new TacticalRequest{type="reward",choice=reward.id};var availability=TacticalRules.Preview(State,request);
                TacticalMenuArt.TextButton("Reward choice "+reward.id,frame,chosen?"已选中":"选择",new Vector2(0,-251),new Vector2(410,54),()=>{jadeRewardChoice=reward.id;Render();},chosen,chosen,25);
                Label("Reward availability "+i,frame,availability.ok?"":availability.reason,0,-301,414,32,21,TacticalTheme.Danger,TextAnchor.MiddleCenter);
            }
            JadeRewardReadingBacking("Reward footer ink",page,new Vector2(0,-450),new Vector2(1808,154));
            Label("Reward gold",page,State.journeyMode=="ascent"?"随行金币  "+State.gold:"旅途行囊",-615,-417,476,46,28,TacticalMenuArt.Gold);
            Label("Reward learning reminder",page,"领取后继续旅程",-615,-471,476,40,24,TacticalMenuArt.Muted);
            var saved=State.rewards.FirstOrDefault(r=>r.id==jadeRewardChoice);
            var chosenReward=TacticalRules.EffectiveReward(State,saved);
            if(chosenReward!=null)
            {
                var recipient=chosenReward.kind=="skill"?TacticalRules.SkillRecipient(State,chosenReward.skillId):null;
                Label("Reward recipient heading",page,recipient==null?"收入行囊":recipient.name+" 自动领悟",0,-423,500,46,27,TacticalMenuArt.Accent,TextAnchor.MiddleCenter);
                Label("Reward selected name",page,chosenReward.name,0,-473,500,40,24,TacticalMenuArt.Text,TextAnchor.MiddleCenter);
                var request=new TacticalRequest{type="reward",choice=chosenReward.id};var check=TacticalRules.Preview(State,request);
                TacticalMenuArt.TextButton("Claim reward",page,"领取战利品",new Vector2(625,-450),new Vector2(438,82),()=>Request(request),true,fontSize:30).interactable=check.ok;
            }
        }
    }
}
