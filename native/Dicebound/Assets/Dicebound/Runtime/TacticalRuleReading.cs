using System;
using System.Collections.Generic;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    public sealed partial class TacticalDirector
    {
        private Transform ruleOverlay;
        private static readonly Dictionary<string,string> RuleDefinitions=new Dictionary<string,string>{
            {"溃散","同一玩家回合内，三位不同同行者都用技能命中过同一敌人。\n\n全队结束行动时，该敌人受到 6 点真实伤害；持有回声螺时提高至 12 点。真实伤害直接扣除生命，护盾不能抵消。"},
            {"同行连携","同一回合，由不同同行者用技能命中同一敌人。\n\n已有两人命中过：后续命中伤害 +2。\n已有三人命中过：后续命中伤害 +4。\n哨绳使上述加成再增加 1。\n\n三人都命中后，在玩家阶段结束时触发溃散。"},
            {"连携","同一回合，先后命中过同一敌人的同行者达到两人时，后续命中伤害 +2；达到三人时 +4。哨绳使加成再增加 1。\n\n三人都命中后，玩家阶段结束时触发溃散。"},
            {"淬水","附着于敌人的水系状态，通常持续 2 轮。\n\n带有淬水的敌人再被施加余焰时，会触发蒸汽爆散，并清除其淬水和余焰。淬水与地块的水雾是两种不同效果。"},
            {"蒸汽爆散","淬水目标被施加余焰时立即触发。\n\n目标受到「已有余焰＋本次余焰＋3」点伤害；邻格敌人受到 4 点伤害，存活者获得 2 轮淬水。清除原目标的淬水与余焰。\n\n赤心刀镡让上述伤害提高一半，结果向下取整。"},
            {"余焰","敌方行动前结算的延迟伤害，结算后清除。多次施加可叠加，通常上限 12；烬芯炉提高至 18。\n\n遇到淬水会立即转为蒸汽爆散。"},
            {"定身","无法移动或借影换位，仍可对范围内目标施放其他技能。\n\n同行者持续到自己下一次结束行动，也可提前净化；敌人于该次行动结束时清除。"},
            {"削弱","攻击伤害降低 3，最低为 0。持续轮数会在对应队伍的回合结算时递减。"},
            {"易伤","受到的攻击伤害增加 3。持续轮数会在对应队伍的回合结算时递减。"},
            {"守势","近身受击时，对相邻攻击者造成状态标注的反击伤害。下一玩家回合开始清除。护盾与守势可以同时存在。"},
            {"反击","持有守势的单位近身受击时，对相邻攻击者反击。伤害由技能与旧物决定，可被攻击者护盾吸收。"},
            {"续息","下一玩家回合开始时恢复标注数值的生命，随后清除。治疗仍受旧物修正及生命上限约束。"},
            {"护盾","先于生命承受普通伤害，最高 40 点。穿盾攻击与真实伤害可绕过护盾。\n\n超过上限的新增护盾有一半转为续息，向下取整。"},
            {"鼓舞","本轮攻击伤害增加状态标注的数值；下一玩家回合开始清除。"},
            {"影遁","敌人优先选择其他未影遁的存活同行者；若全员影遁，仍会选择目标。自己出手或下一玩家回合开始时解除。"},
            {"喝令","受影响的敌人下次行动只追击指定同行者；如果无法在射程内攻击，会尝试接近。该次敌方行动结束后解除。"},
            {"在案","敌人带有定身、削弱、易伤或淬水中的任意一种，即视为在案。断案笔、天衡玉印会为全队攻击这类敌人提供增伤。"},
            {"击退","把目标沿远离施术者的方向推出。\n\n路径被墙体、单位或物件阻挡时，被推目标受到 4 点基础碰撞伤害；撞到的单位或物件也会受到伤害。技能与旧物可增加推移距离和碰撞伤害。"},
            {"水雾","地块类型，每格通常消耗 1 步。注明「需要水雾」的技能要求施术者站在这里；部分治疗与旧物也会获得额外效果。"},
            {"阴影","地块类型，每格通常消耗 1 步。部分晏烛影技能必须站在阴影中，部分只获得额外伤害；借影允许起点或终点其中一处为阴影。"},
            {"热管","每格消耗 2 步。敌方行动结束后，仍站在上面的单位受到 2 点伤害；旧物可以改变该结算。"},
            {"碎石","可以通行，每格消耗 2 步。"},
            {"掩体","站在此处时，受到的攻击伤害减半，向上取整。"},
            {"高台","从高台攻击未站在高台的敌人时，伤害增加 2。"},
            {"蓄势","达到 100 时可释放角色奥义，消耗全部 100 点。\n\n释放非奥义技能：+20（借影除外）。\n技能每击杀一名敌人：+15。\n实际受到敌方攻击：+10。\n\n部分技能与旧物可以额外增加蓄势，上限 100。"},
            {"真实伤害","直接扣除生命，不被护盾抵消。溃散属于真实伤害。"},
            {"镀膜","每轮开始获得 6 点护盾，最多保留 40 点。"},
            {"疾走","移动上限增加 1 步，已计入敌方详情中的移动属性。"},
            {"过载","最大生命减少 5，基础攻击增加 3，已计入敌方属性。"},
            {"坚壳","每轮首次受到的伤害减半，结果向上取整。"},
            {"蔓延","攻击同时波及目标的相邻格。"},
            {"锈坏","倒下后，所在格变为每格消耗 2 步的碎石。"},
            {"庇邻","相邻的其他敌障受到的伤害减少 2。"},
            {"缠缚","每第 3 次攻击使目标定身。敌方详情会显示距离下一次触发还需多少次攻击。"}
        };
        private static string[] readingKeywords;
        private readonly TextGenerator readingMeasure=new TextGenerator();
        private static string[] ReadingKeywords()=>readingKeywords??(readingKeywords=RuleDefinitions.Keys.Concat(TacticalContent.Skills.Select(s=>s.name)).Distinct().OrderByDescending(s=>s.Length).ToArray());
        private void CloseRulePopover(){if(ruleOverlay){ruleOverlay.gameObject.SetActive(false);Destroy(ruleOverlay.gameObject);ruleOverlay=null;}}
        private void OpenRulePopover(string word,TacticalUnit context=null)
        {
            CloseRulePopover();
            var shade=Ui.Panel("Rule popover shade",Canvas.transform,Center,Vector2.zero,PaperViewport.DesignSize,new Color(0,.015f,.02f,.62f),true);ruleOverlay=shade.transform;
            var outside=shade.gameObject.AddComponent<Button>();outside.transition=Selectable.Transition.None;outside.onClick.AddListener(CloseRulePopover);
            var panel=TacticalMenuArt.QuietPanel("Rule popover",ruleOverlay,Vector2.zero,new Vector2(950,640),true).transform;
            var stop=panel.gameObject.AddComponent<Button>();stop.transition=Selectable.Transition.None;
            Label("Rule title",panel,word,0,240,800,60,38,TacticalMenuArt.Gold);
            string body;
            if(!RuleDefinitions.TryGetValue(word,out body))
            {
                var skillDefinition=TacticalContent.Skills.FirstOrDefault(s=>s.name==word);
                var unit=skillDefinition==null?null:context!=null&&context.skills.Contains(skillDefinition.id)?context:State?.units.FirstOrDefault(u=>u.team=="hero"&&u.skills.Contains(skillDefinition.id)&&u.id==selected)??State?.units.FirstOrDefault(u=>u.team=="hero"&&u.skills.Contains(skillDefinition.id)&&u.heroId==skillDefinition.heroId)??State?.units.FirstOrDefault(u=>u.team=="hero"&&u.skills.Contains(skillDefinition.id));
                body=skillDefinition==null?"暂无说明。":(unit==null?"Lv.1 基准效果\n\n":unit.name+" · 当前技能\n\n")+TacticalRules.SkillDescription(State,unit,skillDefinition.id);
            }
            RuleBody("Rule explanation",panel,body,new Vector2(0,-6),new Vector2(814,354),27,false);
            TacticalMenuArt.TextButton("Close rule",panel,"知道了",new Vector2(0,-242),new Vector2(300,54),CloseRulePopover,fontSize:25);
        }
        // Lay out real text runs so the colored keyword itself is a click target,
        // without mapping rich-text source indices back to generated glyph indices.
        private RectTransform RuleBody(string name,Transform parent,string value,Vector2 pos,Vector2 size,int fontSize=25,bool links=true,TacticalUnit context=null)
        {
            value=value??"";
            // Short explanations use the full available width, without a redundant scroll gutter.
            float naturalHeight=FlowRuleText(name,null,value,size.x-4,fontSize,links,context);
            if(naturalHeight<=size.y)
            {
                var body=Ui.Rect(name+" body",parent,Center,pos,size);
                FlowRuleText(name,body,value,size.x-4,fontSize,links,context);return body;
            }
            var content=InspectionScroll(name+" scroll",parent,pos,size,size.y);
            float height=FlowRuleText(name,content,value,size.x-28,fontSize,links,context);
            content.sizeDelta=new Vector2(size.x,Mathf.Max(size.y,height));return content;
        }
        private float FlowRuleText(string name,RectTransform content,string value,float width,int size,bool links,TacticalUnit context=null)
        {
            var keywords=links?ReadingKeywords():Array.Empty<string>();
            var settings=new TextGenerationSettings{font=Ui.Font,fontSize=size,fontStyle=FontStyle.Normal,scaleFactor=1,lineSpacing=1,richText=false,textAnchor=TextAnchor.UpperLeft,horizontalOverflow=HorizontalWrapMode.Overflow,verticalOverflow=VerticalWrapMode.Overflow,generationExtents=new Vector2(width,1000),color=Color.white};
            var measure=readingMeasure;float x=0,y=2,lineHeight=size*1.5f;int sequence=0;
            string run="",runKeyword=null;float runX=0,runWidth=0;
            Action flush=()=>{
                if(run.Length==0)return;
                if(content)
                {
                    var text=Ui.Label(name+" line "+sequence++,content,run,new Vector2(0,1),new Vector2(runX,-y),new Vector2(runWidth+3,lineHeight),size,runKeyword==null?TacticalMenuArt.Text:TacticalMenuArt.Accent,TextAnchor.UpperLeft);
                    text.rectTransform.pivot=new Vector2(0,1);text.horizontalOverflow=HorizontalWrapMode.Overflow;
                    if(runKeyword!=null){string word=runKeyword;text.raycastTarget=true;var button=text.gameObject.AddComponent<Button>();button.targetGraphic=text;button.onClick.AddListener(()=>OpenRulePopover(word,context));Ui.Panel("Keyword underline",text.transform,new Vector2(0,1),new Vector2(runWidth*.5f,-size*1.20f),new Vector2(runWidth,1),new Color(.4f,.8f,.72f,.6f));}
                }
                run="";runWidth=0;runKeyword=null;
            };
            for(int i=0;i<value.Length;)
            {
                if(value[i]=='\r'){i++;continue;}
                if(value[i]=='\n'){flush();x=0;y+=lineHeight;if(i+1<value.Length&&value[i+1]=='\n'){y+=8;i++;}i++;continue;}
                string word=keywords.FirstOrDefault(k=>i+k.Length<=value.Length&&string.CompareOrdinal(value,i,k,0,k.Length)==0);
                string textSegment=word??value[i].ToString();
                if(word==null&&value[i]<128&&char.IsLetterOrDigit(value[i]))
                {
                    int end=i+1;while(end<value.Length&&value[end]<128&&(char.IsLetterOrDigit(value[end])||"./%".IndexOf(value[end])>=0))end++;
                    textSegment=value.Substring(i,end-i);
                }
                const string closingPunctuation="，。；：！？、）】」”";
                if(word==null)while(i+textSegment.Length<value.Length&&closingPunctuation.IndexOf(value[i+textSegment.Length])>=0)textSegment+=value[i+textSegment.Length];
                float tokenWidth=measure.GetPreferredWidth(textSegment,settings);
                if(textSegment==" ")tokenWidth=Mathf.Max(tokenWidth,size*.28f);
                float wrappingWidth=tokenWidth;
                // Keep a keyword with its following punctuation, without extending its click target.
                if(word!=null){int end=i+textSegment.Length;while(end<value.Length&&closingPunctuation.IndexOf(value[end])>=0)end++;if(end>i+textSegment.Length)wrappingWidth+=measure.GetPreferredWidth(value.Substring(i+textSegment.Length,end-i-textSegment.Length),settings);}
                if(x+wrappingWidth>width&&x>0){flush();x=0;y+=lineHeight;}
                if(run.Length>0&&runKeyword!=word)flush();
                if(run.Length==0){runX=x;runKeyword=word;}
                run+=textSegment;runWidth+=tokenWidth;x+=tokenWidth;i+=textSegment.Length;
                if(word!=null)flush();
            }
            flush();return y+lineHeight+4;
        }
        private static string ReadableEffect(string text)=>text.Replace("；","；\n").Replace("。","。\n").TrimEnd('\n');
        public void InspectStatuses(string unitId)
        {
            var unit=TacticalRules.FindUnit(State,unitId);if(unit==null)return;
            var panel=InspectionFrame(unit.name+" · 当前状态",1120,820);
            Label("Status health",panel,"生命 "+unit.hp+" / "+unit.maxHp+"    护盾 "+unit.block,0,206,950,48,29,TacticalMenuArt.Text);
            InspectionBar("Status health shield bar",panel,new Vector2(0,164),new Vector2(944,18),unit.hp,unit.maxHp,unit.block);
            string description=TacticalRules.UnitStatusDescription(unit);
            if(!string.IsNullOrEmpty(unit.tauntedBy)){var target=TacticalRules.FindUnit(State,unit.tauntedBy);if(target!=null)description=description.Replace(unit.tauntedBy,target.name);}
            if(unit.block>0)description="护盾：优先抵消 "+unit.block+" 点普通伤害"+(description.Length>0?" · "+description:"");
            description=string.IsNullOrEmpty(description)?"当前没有额外状态。":description.Replace(" · ","\n\n");
            RuleBody("Current status effects",panel,description,new Vector2(0,-76),new Vector2(960,406),27,context:unit);
        }
        private string SkillEffectText(TacticalUnit unit,TacticalSkill definition,bool battleContext=true)
        {
            string[] full=TacticalRules.SkillDescription(battleContext?State:null,unit,definition.id).Split('\n');
            string value=full[0];int split=value.IndexOf('·');if(split>=0)value=value.Substring(split+1).Trim();
            string extra=full.Length>3?string.Join("\n",full.Skip(1).Take(full.Length-3)):"";
            return value+"\n\n"+ReadableEffect(definition.kind=="passive"?full[1]:definition.text)+(extra.Length==0?"":"\n\n"+extra);
        }
        private static string TrialName(int trial)=>trial<=0?"初行":trial==1?"试锋":trial==2?"踏岚":trial==3?"破障":trial==4?"逆潮":"登阙";
        private void TrialDetails()
        {
            var panel=InspectionFrame("登阙难度",1180,780);
            Label("Trial introduction",panel,"后一个难度保留前一个难度的规则。",0,196,1010,46,25,TacticalMenuArt.Muted);
            string[] descriptions={"默认 · 敌人生命 +10%，基础攻击 +1。","敌方词缀提前出现，前段也需应对特殊能力。","商店基础价格再提高 20%。","普通战再增加一名敌人，最多五名。"};
            for(int i=0;i<4;i++){float y=108-i*105;var row=InspectionPanel("Trial detail "+(i+2),panel,new Vector2(0,y),new Vector2(1010,90),selectedTrial==i+2);Label("Trial tier "+(i+2),row,TrialName(i+2),-389,0,170,48,32,TacticalMenuArt.Gold);Label("Trial effect "+(i+2),row,descriptions[i],117,0,708,68,25,TacticalMenuArt.Text);}
        }
    }
}
