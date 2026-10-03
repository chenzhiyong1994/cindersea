using System;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    public sealed partial class TacticalDirector
    {
        private int skillPage;
        private string detailHero;
        private string detailSkill,detailRelic;
        private float detailSkillScroll=1,detailRelicScroll=1;
        private static string UnitStatuses(TacticalUnit unit)
        {
            string value=TacticalRules.UnitStatusDescription(unit);return string.IsNullOrEmpty(value)?"无异常状态":value;
        }
        private string OfferedSkillEffect(TacticalUnit unit,TacticalSkill definition)
        {
            bool known=unit.skills.Contains(definition.id);var after=unit.Clone();
            int level=known?Math.Min(3,TacticalRules.SkillLevel(unit,definition.id)+1):1;
            if(!known)after.skills.Add(definition.id);after.skillLevels[definition.id]=level;
            return SkillEffectText(after,definition);
        }
        private TacticalUnit SkillDisplayRecipient(TacticalSkill definition)=>TacticalRules.SkillRecipient(State,definition.id)??State.units.FirstOrDefault(u=>u.team=="hero"&&u.heroId==definition.heroId)??State.units.First(u=>u.team=="hero");
        private void SkillParameterChips(string name,Transform parent,TacticalUnit unit,TacticalSkill definition,float y,float width)
        {
            string[] values={SkillCost(definition),"射程 "+TacticalRules.EffectiveSkillRange(State,unit,definition)+" 格","冷却 "+TacticalRules.SkillCooldown(State,definition)+" 轮"};
            if(definition.kind=="passive")values=new[]{"被动","自身","常驻"};
            float cell=(width-16)/3;
            for(int i=0;i<3;i++){var chip=InspectionPanel(name+" parameter "+i,parent,new Vector2((i-1)*(cell+8),y),new Vector2(cell,44));Label(name+" parameter value "+i,chip,values[i],0,0,cell-8,38,22,TacticalMenuArt.Accent,TextAnchor.MiddleCenter);}
        }
        private string InspectedUpgradeSummary(TacticalUnit unit,TacticalSkill definition)
        {
            bool known=unit.skills.Contains(definition.id);int level=known?TacticalRules.SkillLevel(unit,definition.id):0;
            var after=unit.Clone();after.skillLevels[definition.id]=Math.Min(3,level+1);
            string type=SkillValueName(definition);
            int current=definition.kind=="step"?TacticalRules.SkillRange(unit,definition):TacticalRules.SkillPower(unit,definition),next=definition.kind=="step"?TacticalRules.SkillRange(after,definition):TacticalRules.SkillPower(after,definition);
            string levelText=level==0?"领悟 Lv.1":level>=3?"已达 Lv.3":"Lv."+level+" → Lv."+(level+1);
            bool upgrading=known&&level<3;
            if(definition.id=="bastion")
            {
                var before=unit.Clone();before.block=100;after.block=100;
                return levelText+"\n护盾转为反击 "+(upgrading?TacticalRules.BastionRetaliationBonus(before)+"% → ":"")+TacticalRules.BastionRetaliationBonus(after)+"%";
            }
            string summary=levelText+(current==0&&next==0?"":" · "+type+" "+(upgrading?current+" → "+next:next.ToString()));
            int secondary=TacticalRules.SkillSecondaryPower(unit,definition),nextSecondary=TacticalRules.SkillSecondaryPower(after,definition);
            if(secondary>0||nextSecondary>0)summary+="\n"+SkillSecondaryValueName(definition)+" "+(upgrading?secondary+" → "+nextSecondary:nextSecondary.ToString());
            return summary;
        }
        private string CellSummary(int x,int y)
        {
            string terrain=TacticalRules.TerrainAt(State,x,y);
            var unit=State.units.FirstOrDefault(u=>u.hp>0&&u.x==x&&u.y==y);
            return TerrainName(terrain)+"\n"+TerrainDescription(terrain)+(unit==null?"":"\n\n"+unit.name+"  生命 "+unit.hp+"/"+unit.maxHp+"\n护盾 "+unit.block);
        }
        private void InspectTarget()
        {
            var target=pending??hovered;var selectedUnit=TacticalRules.FindUnit(State,selected);
            int x=target?.x??selectedUnit?.x??0,y=target?.y??selectedUnit?.y??0;
            var unit=State.units.FirstOrDefault(u=>u.hp>0&&u.x==x&&u.y==y);
            if(unit!=null){InspectUnit(unit.id);return;}
            string terrain=TacticalRules.TerrainAt(State,x,y);var item=TacticalRules.ObjectAt(State,x,y);var frame=InspectionFrame("地块信息",1000,650);
            InspectionMetric("Cell coordinate",frame,"坐标",GridName(x,y),new Vector2(-290,134),new Vector2(244,104));
            InspectionMetric("Cell terrain",frame,"地形",TerrainName(terrain),new Vector2(0,134),new Vector2(288,104));
            InspectionMetric("Cell movement",frame,"移动消耗",!TacticalRules.Walkable(State,x,y)?"不可通行":item!=null?"物件占据":TacticalRules.MoveCost(State,x,y)+" 步 / 格",new Vector2(290,134),new Vector2(244,104));
            var panel=InspectionPanel("Cell details panel",frame,new Vector2(0,-36),new Vector2(860,180));
            Label("Cell effect heading",panel,"地形效果",0,item==null?46:54,808,38,27,TacticalMenuArt.Gold);
            RuleBody("Cell details",panel,ReadableEffect(TerrainDescription(terrain)),new Vector2(0,item==null?-18:-4),new Vector2(808,76),26);
            if(item!=null)Label("Cell object details",panel,(item.kind=="valve"?"蒸汽阀":item.kind=="canister"?"热罐":"木箱")+" · 耐久 "+item.hp+" · 击破或移走后可进入",0,-62,808,38,25,TacticalMenuArt.Accent);
            Label("Cell inspection hint",frame,"行动的目标、路径与消耗会单独显示在行动预览中。",0,-174,850,54,23,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
        }
        public void InspectUnit(string id)
        {
            var unit=TacticalRules.FindUnit(State,id);if(unit==null)return;
            if(unit.team=="hero"){if(detailHero!=id){detailSkill=null;detailSkillScroll=1;}detailHero=id;PartyDetails();return;}
            var frame=InspectionFrame("敌方详情",1440,940);
            var identity=InspectionPanel("Enemy identity",frame,new Vector2(-471,-10),new Vector2(382,648));
            var definition=TacticalStory.GetEnemy(unit.heroId);string rank=definition?.rank??unit.enemyType;
            Label("Enemy name",identity,unit.name,0,264,326,64,34,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
            Label("Enemy rank",identity,(rank=="boss"?"首领":rank=="elite"?"精英":"普通")+" · "+(TacticalRules.EnemyAttackRange(State,unit)>1?"远攻":"近战"),0,216,322,40,25,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
            JadeUnitPicture("Enemy detail artwork",identity,unit.heroId,new Vector2(0,57),new Vector2(244,256));
            InspectionChip("Enemy position",identity,GridName(unit.x,unit.y)+" · "+TerrainName(TacticalRules.TerrainAt(State,unit.x,unit.y)),new Vector2(0,-91),new Vector2(300,48));
            Label("Enemy health",identity,"生命 "+unit.hp+" / "+unit.maxHp,0,-148,310,44,28,TacticalMenuArt.Text);
            InspectionBar("Enemy health bar",identity,new Vector2(0,-182),new Vector2(310,14),unit.hp,unit.maxHp,unit.block);
            Label("Enemy shield",identity,"护盾 "+unit.block,0,-225,310,42,27,TacticalMenuArt.Text);
            RuleBody("Enemy status",identity,UnitStatuses(unit).Replace(" · ","\n"),new Vector2(0,-276),new Vector2(310,60),22);
            int power=TacticalRules.EnemyAttackPower(State,unit),range=TacticalRules.EnemyAttackRange(State,unit);
            InspectionMetric("Enemy attack",frame,"基础攻击",power.ToString(),new Vector2(-107,268),new Vector2(286,94));
            InspectionMetric("Enemy range",frame,"攻击射程",range+" 格",new Vector2(206,268),new Vector2(286,94));
            InspectionMetric("Enemy movement",frame,"移动上限",TacticalRules.EnemyMoveBudget(State,unit)+" 步",new Vector2(519,268),new Vector2(286,94));
            var intent=TacticalRules.EnemyIntents(State).FirstOrDefault(i=>i.unitId==id);
            var nextPanel=InspectionPanel("Enemy intent panel",frame,new Vector2(206,108),new Vector2(912,190),true);
            Label("Enemy next heading",nextPanel,"下回行动",0,60,864,42,28,TacticalMenuArt.Accent);
            var recipient=intent==null?null:TacticalRules.FindUnit(State,intent.targetId);
            string action=intent==null?"无法行动":intent.attacks?TacticalRules.EnemyActionName(State,unit).Split('·')[0].Trim():recipient==null?"无法行动":"接近";
            if(action=="构装攻击"&&definition!=null&&definition.text.Contains("："))action=definition.text.Split('：')[0];
            Label("Enemy next action",nextPanel,action,-228,3,400,44,29,TacticalMenuArt.Gold);
            string route=intent==null?GridName(unit.x,unit.y):GridName(unit.x,unit.y)+" → "+GridName(intent.x,intent.y);
            Label("Enemy intent route",nextPanel,route+"\n"+(recipient==null?intent?.text??"当前没有可执行行动。":"目标："+recipient.name),-228,-47,400,67,23,TacticalMenuArt.Text,TextAnchor.UpperLeft);
            Label("Enemy predicted damage",nextPanel,intent!=null&&intent.attacks?"预计生命损失 "+intent.damage:"本次不会攻击",220,1,408,44,28,intent!=null&&intent.attacks?TacticalTheme.Danger:TacticalMenuArt.Text);
            Label("Enemy damage explanation",nextPanel,intent!=null&&intent.attacks?"已计入目标护盾、掩体及当前状态。":"以当前站位推演；我方行动后会更新。",220,-47,408,66,22,TacticalMenuArt.Muted,TextAnchor.UpperLeft);
            var abilities=InspectionPanel("Enemy abilities panel",frame,new Vector2(206,-121),new Vector2(912,238));
            Label("Enemy abilities heading",abilities,"技能与行为",-79,83,700,42,28,TacticalMenuArt.Gold);
            InspectionChip("Enemy skill damage",abilities,"基础伤害 "+power,new Vector2(328,79),new Vector2(205,46));
            string scope=TacticalRules.EnemySplashRadius(State,unit)>0?"目标与邻格":"单体";
            Label("Enemy skill parameters",abilities,"射程 "+range+" 格 · "+scope+EnemyEffectText(TacticalRules.EnemyInflicts(State,unit)),0,30,860,40,23,TacticalMenuArt.Accent);
            string explanation=TacticalStory.EnemyDescription(unit.heroId);
            if(!string.IsNullOrEmpty(unit.affixes))explanation+="\n\n词缀效果\n"+string.Join("\n",unit.affixes.Split('+').Select(a=>TacticalContent.AffixName(a)+" · "+EnemyAffixDescription(unit,a)));
            RuleBody("Enemy abilities",abilities,ReadableEffect(explanation),new Vector2(0,-47),new Vector2(860,122),24);
            var terrainPanel=InspectionPanel("Enemy terrain panel",frame,new Vector2(206,-298),new Vector2(912,90));
            Label("Enemy terrain heading",terrainPanel,"地块效果",-340,0,180,42,25,TacticalMenuArt.Gold);
            RuleBody("Enemy terrain",terrainPanel,TerrainDescription(TacticalRules.TerrainAt(State,unit.x,unit.y)),new Vector2(107,0),new Vector2(636,70),23);
        }
        private RectTransform InspectionScroll(string name,Transform parent,Vector2 pos,Vector2 size,float contentHeight)
        {
            var viewport=Ui.Rect(name,parent,Center,pos,size);viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=new Color(0,0,0,.015f);hit.raycastTarget=true;
            var content=Ui.Rect(name+" content",viewport,new Vector2(.5f,1),Vector2.zero,new Vector2(size.x,Mathf.Max(size.y,contentHeight)));
            content.pivot=new Vector2(.5f,1);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=42;
            var track=Ui.Panel(name+" scrollbar",viewport,Center,new Vector2(size.x*.5f-10,0),new Vector2(20,size.y),Color.clear,true);
            Ui.Panel("Track line",track.transform,Center,new Vector2(6,0),new Vector2(5,size.y),new Color(.3f,.4f,.37f,.15f));
            var thumb=Ui.Panel("Handle",track.transform,Center,Vector2.zero,new Vector2(20,40),Color.clear,true);
            thumb.rectTransform.sizeDelta=Vector2.zero;
            var thumbLine=Ui.Panel("Handle line",thumb.transform,Center,new Vector2(6,0),new Vector2(5,40),TacticalMenuArt.Accent);
            thumbLine.rectTransform.anchorMin=new Vector2(.5f,0);thumbLine.rectTransform.anchorMax=new Vector2(.5f,1);thumbLine.rectTransform.sizeDelta=new Vector2(5,0);
            var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=thumb.rectTransform;scrollbar.targetGraphic=thumbLine;scrollbar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
        public void PartyDetails()
        {
            if(State==null)return;var heroes=State.units.Where(u=>u.team=="hero").ToArray();if(heroes.Length==0)return;
            var unit=heroes.FirstOrDefault(u=>u.id==detailHero)??heroes.FirstOrDefault(u=>u.id==selected)??heroes[0];detailHero=unit.id;
            var frame=InspectionFrame("同行者 · 属性与技能",1700,980);
            for(int i=0;i<heroes.Length;i++)
            {
                var hero=heroes[i];float x=(i-(heroes.Length-1)*.5f)*312;
                TacticalMenuArt.TextButton("Inspect companion "+hero.id,frame,hero.name,new Vector2(x,327),new Vector2(286,58),()=>{detailHero=hero.id;detailSkill=null;detailSkillScroll=1;PartyDetails();},selected:hero.id==unit.id,fontSize:27);
            }
            var identity=InspectionPanel("Companion identity",frame,new Vector2(-630,-39),new Vector2(330,626));
            Label("Companion detail name",identity,unit.name,0,277,280,46,34,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
            Label("Companion detail role",identity,TacticalContent.GetHero(unit.heroId)?.title??"同行者",0,235,280,38,24,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
            JadeUnitPicture("Companion detail artwork",identity,unit.heroId,new Vector2(0,108),new Vector2(240,218));
            Label("Companion health",identity,"生命 "+unit.hp+" / "+unit.maxHp,0,-41,280,44,27,TacticalMenuArt.Text);
            InspectionBar("Companion health bar",identity,new Vector2(0,-74),new Vector2(280,14),unit.hp,unit.maxHp,unit.block);
            string[] keys={"护盾","AP","SP","蓄势"};string[] values={unit.block.ToString(),unit.ap+" / "+unit.maxAp,unit.sp+" / "+unit.maxSp,unit.charge+" / 100"};
            var stats=Ui.Rect("Companion detail stats",identity,Center,Vector2.zero,Vector2.zero);
            for(int i=0;i<keys.Length;i++){Label("Companion stat "+keys[i],stats,keys[i],-84,-114-i*37,112,39,24,TacticalMenuArt.Muted);Label("Companion value "+keys[i],stats,values[i],58,-114-i*37,160,39,25,TacticalMenuArt.Text,TextAnchor.MiddleRight);}
            InspectionChip("Companion turn state",identity,unit.hp<=0?"已倒下":unit.turnEnded?"本回合已结束":"本回合可行动",new Vector2(0,-276),new Vector2(282,46),!unit.turnEnded&&unit.hp>0);
            var known=unit.skills.Select(TacticalContent.GetSkill).Where(s=>s!=null).ToArray();
            var learnable=TacticalRules.AvailableSkillCatalogue(State).Where(s=>!unit.skills.Contains(s.id)&&s.heroId==unit.heroId).ToArray();
            var all=known.Concat(learnable).ToArray();
            if(!all.Any(s=>s.id==detailSkill))detailSkill=known.FirstOrDefault()?.id??all.FirstOrDefault()?.id;
            var list=InspectionPanel("Companion skills panel",frame,new Vector2(-205,-39),new Vector2(480,626));
            Label("Companion skill list heading",list,"已掌握 "+known.Length+" · 待领悟 "+learnable.Length,0,270,430,44,25,TacticalMenuArt.Gold);
            var content=InspectionScroll("Companion skill list",list,new Vector2(0,-30),new Vector2(446,546),all.Length*92+(learnable.Length>0?50:8));
            var scroll=content.parent.GetComponent<ScrollRect>();
            for(int i=0;i<all.Length;i++)
            {
                var d=all[i];bool learned=unit.skills.Contains(d.id);float y=content.sizeDelta.y*.5f-45-i*92-(learned?0:42);
                if(i==known.Length)Label("Unlearned skills heading",content,"本角色招式 · 尚未获得",-6,y+63,418,38,22,TacticalMenuArt.Muted);
                var row=InspectionRow("Inspect skill "+d.id,content,new Vector2(-6,y),new Vector2(424,82),d.id==detailSkill,()=>{detailSkill=d.id;detailSkillScroll=scroll.verticalNormalizedPosition;PartyDetails();});
                var icon=TacticalArt.SkillSymbol("Inspected skill icon "+d.id,row,d.id,new Vector2(-164,0),60);if(!learned)icon.color=new Color(.73f,.78f,.75f,.7f);
                Label("Inspected skill name "+d.id,row,d.name,41,18,264,36,25,learned?TacticalMenuArt.Text:TacticalMenuArt.Muted);
                Label("Inspected skill level "+d.id,row,(learned?"Lv."+TacticalRules.SkillLevel(unit,d.id):"未领悟")+" · "+SkillCost(d),41,-19,264,34,22,learned?TacticalMenuArt.Accent:TacticalMenuArt.Muted);
            }
            scroll.verticalNormalizedPosition=detailSkillScroll;
            var selectedSkill=all.FirstOrDefault(s=>s.id==detailSkill);if(selectedSkill!=null)DrawInspectedSkill(frame,unit,selectedSkill);
            TacticalMenuArt.TextButton("Companion current statuses",frame,"查看当前状态",new Vector2(-581,-422),new Vector2(350,56),()=>InspectStatuses(unit.id),fontSize:25);
            TacticalMenuArt.TextButton("Open pack from party",frame,"查看行囊",new Vector2(307,-422),new Vector2(286,56),Inventory,fontSize:25);
            MoveInspectionClose(frame,new Vector2(627,-422),new Vector2(286,56));
        }
        public void Inventory()
        {
            if(State==null)return;var frame=InspectionFrame("行囊 · 已获得物品",1480,900);
            InspectionChip("Inventory gold",frame,"金币 "+State.gold,new Vector2(363,301),new Vector2(232,52));
            InspectionChip("Inventory count",frame,"旧物 "+State.relics.Count+" 件",new Vector2(600,301),new Vector2(206,52));
            Label("Inventory subtitle",frame,"旅途中收集的旧物",-389,301,556,48,25,TacticalMenuArt.Muted);
            var list=InspectionPanel("Inventory list panel",frame,new Vector2(-412,-20),new Vector2(520,560));
            Label("Inventory heading",list,"已获得",0,238,464,42,28,TacticalMenuArt.Gold);
            if(State.relics.Count==0)
            {
                detailRelic=null;Label("Empty inventory",list,"尚未获得旧物\n\n战斗奖励、事件与商店\n都可能带来新的收获。",0,-30,432,210,26,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
                var empty=InspectionPanel("Inventory detail panel",frame,new Vector2(272,-20),new Vector2(800,560));
                TacticalArt.Symbol("Empty inventory emblem",empty,"route-event",new Vector2(0,68),new Vector2(118,118));
                Label("Empty inventory detail",empty,"收集旧物，形成自己的构筑。\n获得后可在这里查看触发时机与效果。",0,-90,668,106,28,TacticalMenuArt.Text,TextAnchor.MiddleCenter);
            }
            else
            {
                if(!State.relics.Contains(detailRelic))detailRelic=State.relics[0];
                var content=InspectionScroll("Inventory list",list,new Vector2(0,-36),new Vector2(486,458),State.relics.Count*118+8);var scroll=content.parent.GetComponent<ScrollRect>();
                for(int i=0;i<State.relics.Count;i++)
                {
                    var relic=TacticalContent.GetRelic(State.relics[i]);if(relic==null)continue;float y=content.sizeDelta.y*.5f-58-i*118;
                    var row=InspectionRow("Inspect relic "+relic.id,content,new Vector2(-6,y),new Vector2(464,108),relic.id==detailRelic,()=>{detailRelic=relic.id;detailRelicScroll=scroll.verticalNormalizedPosition;Inventory();});
                    TacticalArt.Symbol("Inventory relic "+relic.id,row,RelicArtwork(relic.id),new Vector2(-178,0),Vector2.one*74);
                    Label("Inventory name "+i,row,relic.name,54,23,310,42,28,TacticalMenuArt.Text);
                    Label("Inventory trigger "+i,row,RelicTrigger(relic.id),54,-22,310,44,22,TacticalMenuArt.Muted);
                }
                scroll.verticalNormalizedPosition=detailRelicScroll;DrawInspectedRelic(frame,TacticalContent.GetRelic(detailRelic));
            }
            Label("Inventory hint",frame,"点击旧物查看效果 · 滚轮浏览",-407,-380,530,46,23,TacticalMenuArt.Muted);
            TacticalMenuArt.TextButton("Open party from pack",frame,"查看同行者",new Vector2(232,-382),new Vector2(286,56),PartyDetails,fontSize:25);
            MoveInspectionClose(frame,new Vector2(550,-382),new Vector2(286,56));
        }

        private void DrawInspectedSkill(Transform frame,TacticalUnit unit,TacticalSkill skillDefinition)
        {
            bool known=unit.skills.Contains(skillDefinition.id);var shown=unit;
            if(!known){shown=unit.Clone();shown.skillLevels[skillDefinition.id]=1;}
            var panel=InspectionPanel("Companion selected skill",frame,new Vector2(425,-39),new Vector2(740,626));
            TacticalArt.SkillSymbol("Selected skill artwork",panel,skillDefinition.id,new Vector2(-290,252),82);
            Label("Selected skill name",panel,skillDefinition.name,47,268,538,50,34,TacticalMenuArt.Gold);
            Label("Selected skill level",panel,(known?"Lv."+TacticalRules.SkillLevel(unit,skillDefinition.id)+" / 3":"未领悟 · Lv.1 效果预览")+" · "+SkillKindName(skillDefinition),47,224,538,40,23,known?TacticalMenuArt.Accent:TacticalMenuArt.Muted);
            InspectionChip("Selected skill cost",panel,skillDefinition.kind=="passive"?"无需消耗":"消耗 "+SkillCost(skillDefinition),new Vector2(-230,163),new Vector2(214,52));
            InspectionChip("Selected skill range",panel,skillDefinition.kind=="passive"?"作用于自身":"范围 "+TacticalRules.EffectiveSkillRange(State,shown,skillDefinition)+" 格",new Vector2(0,163),new Vector2(214,52));
            InspectionChip("Selected skill cooldown",panel,skillDefinition.kind=="passive"?"常驻生效":"冷却 "+TacticalRules.SkillCooldown(State,skillDefinition)+" 轮",new Vector2(230,163),new Vector2(214,52));
            int value=skillDefinition.kind=="step"?TacticalRules.SkillRange(shown,skillDefinition):TacticalRules.SkillPower(shown,skillDefinition);
            string valueText=skillDefinition.kind=="passive"?"持有技能时生效":value==0?"特殊效果":SkillValueName(skillDefinition)+" "+value;
            string[] full=TacticalRules.SkillDescription(known?State:null,shown,skillDefinition.id).Split('\n');
            bool detailedValue=skillDefinition.effect=="depthcleanse";
            if(detailedValue){int separator=full[0].IndexOf('·');if(separator>=0)valueText=full[0].Substring(separator+1).Trim();}
            Label("Selected skill value",panel,valueText,0,detailedValue?86:92,672,detailedValue?76:54,detailedValue?26:34,TacticalMenuArt.Accent);
            string body=ReadableEffect(skillDefinition.kind=="passive"?full[1]:skillDefinition.text);
            if(full.Length>3)body+="\n\n"+string.Join("\n",full.Skip(1).Take(full.Length-3));
            if(known&&unit.cooldowns.TryGetValue(skillDefinition.id,out int cooldown)&&cooldown>0)body+="\n当前还需冷却 "+cooldown+" 轮。";
            if(!known)body+="\n可在商店或奖励中领悟；当前尚不能释放。";
            RuleBody("Inspected skill effect "+skillDefinition.id,panel,body,new Vector2(0,-64),new Vector2(672,208),26,context:shown);
            var upgrade=InspectionPanel("Selected skill upgrade panel",panel,new Vector2(0,-241),new Vector2(672,118));
            Label("Selected skill upgrade",upgrade,InspectedUpgradeSummary(unit,skillDefinition),0,14,624,66,24,TacticalMenuArt.Gold);
            Label("Selected skill upgrade hint",upgrade,known?(TacticalRules.SkillLevel(unit,skillDefinition.id)>=3?"当前技能已达到最高等级。":"重复领悟可提升等级；数值随等级增长。"):"领悟后加入技能组合。",0,-37,624,34,22,TacticalMenuArt.Muted);
        }
        private void DrawInspectedRelic(Transform frame,TacticalRelic relic)
        {
            if(relic==null)return;var panel=InspectionPanel("Inventory detail panel",frame,new Vector2(272,-20),new Vector2(800,560));
            TacticalArt.Symbol("Selected relic artwork",panel,RelicArtwork(relic.id),new Vector2(-266,166),Vector2.one*132);
            Label("Selected relic name",panel,relic.name,99,201,490,62,38,TacticalMenuArt.Gold);
            Label("Selected relic rarity",panel,RelicRarity(relic.rarity)+" · 持续生效",99,144,490,46,25,relic.rarity=="cursed"?TacticalTheme.Danger:TacticalMenuArt.Muted);
            InspectionChip("Selected relic trigger label",panel,"触发时机",new Vector2(-269,48),new Vector2(182,48));
            RuleBody("Selected relic trigger",panel,RelicTrigger(relic.id),new Vector2(97,42),new Vector2(508,56),27);
            Label("Selected relic effect heading",panel,"具体效果",0,-26,716,42,27,TacticalMenuArt.Gold);
            RuleBody("Selected relic effect",panel,ReadableEffect(relic.text),new Vector2(0,-129),new Vector2(716,146),28);
            Label("Selected relic passive hint",panel,"持有时自动生效",0,-239,716,38,24,TacticalMenuArt.Muted);
        }
        private Transform InspectionFrame(string title,float width,float height)
        {
            var frame=Overlay(title,width,height);var rule=frame.Find("Modal engraved rule");if(rule)rule.gameObject.SetActive(false);
            var heading=frame.Find("Modal title") as RectTransform;if(heading)heading.anchoredPosition=new Vector2(0,height*.5f-68);
            Ui.Panel("Inspection quiet lining",frame,Center,new Vector2(0,-17),new Vector2(width-46,height-182),new Color(.021f,.039f,.039f,.90f)).transform.SetSiblingIndex(0);
            return frame;
        }
        private Transform InspectionPanel(string name,Transform parent,Vector2 pos,Vector2 size,bool selectedPanel=false)
        {
            var rim=Ui.Panel(name,parent,Center,pos,size,selectedPanel?new Color(.34f,.64f,.56f,.72f):new Color(.55f,.48f,.30f,.45f));
            Ui.Panel(name+" ink",rim.transform,Center,Vector2.zero,size-Vector2.one*2,selectedPanel?new Color(.035f,.15f,.13f,.98f):new Color(.025f,.055f,.054f,.96f));return rim.transform;
        }
        private Transform InspectionRow(string name,Transform parent,Vector2 pos,Vector2 size,bool chosen,UnityEngine.Events.UnityAction action)
        {
            var panel=InspectionPanel(name,parent,pos,size,chosen);var hit=Ui.Panel("Row hit",panel,Center,Vector2.zero,size,new Color(.09f,.22f,.18f,chosen?.40f:.10f),true);
            var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=hit;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.8f,1.9f,1.6f,1);colors.pressedColor=new Color(.75f,.88f,.77f,1);colors.fadeDuration=.1f;button.colors=colors;button.onClick.AddListener(action);
            if(chosen){Ui.Panel("Selected jade edge",panel,Center,new Vector2(-size.x*.5f+2,0),new Vector2(4,size.y-4),TacticalMenuArt.Accent);Label("Selected check",panel,"✓",size.x*.5f-19,size.y*.5f-20,32,34,24,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);}return panel;
        }
        private void InspectionMetric(string name,Transform parent,string title,string value,Vector2 pos,Vector2 size)
        {var panel=InspectionPanel(name,parent,pos,size);Label(name+" label",panel,title,0,20,size.x-24,34,22,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);Label(name+" value",panel,value,0,-20,size.x-24,42,30,TacticalMenuArt.Text,TextAnchor.MiddleCenter);}
        private void InspectionChip(string name,Transform parent,string text,Vector2 pos,Vector2 size,bool on=false)
        {var panel=InspectionPanel(name+" panel",parent,pos,size,on);Label(name,panel,text,0,0,size.x-18,size.y-2,23,on?TacticalMenuArt.Accent:TacticalMenuArt.Text,TextAnchor.MiddleCenter);}
        private void InspectionBar(string name,Transform parent,Vector2 pos,Vector2 size,int current,int maximum,int shield=0)
        {TacticalArt.HealthShieldMeter(name,parent,pos,size,current,maximum,shield,TacticalMenuArt.Accent);}
        private void MoveInspectionClose(Transform frame,Vector2 pos,Vector2 size)
        {var close=frame.Find("Close modal") as RectTransform;if(!close)return;close.anchoredPosition=pos;close.sizeDelta=size;var label=close.GetComponentInChildren<Text>();if(label)label.text=State?.phase=="battle"?"返回战斗":State?.phase=="reward"?"返回奖励":State?.phase=="map"?"返回路线":"返回旅程";}
        private static void ResizeInspectionText(RectTransform content,Text text,float minimum)
        {float height=Mathf.Max(minimum,text.preferredHeight+8);content.sizeDelta=new Vector2(content.sizeDelta.x,height);text.rectTransform.sizeDelta=new Vector2(text.rectTransform.sizeDelta.x,height);}
        private static string SkillCost(TacticalSkill definition)=>definition.kind=="passive"?"被动":definition.cost+" "+(definition.resource=="charge"?"蓄势":definition.resource.ToUpperInvariant());
        private static string SkillValueName(TacticalSkill definition)=>definition.kind=="heal"?"治疗":definition.kind=="guard"?"护盾":definition.kind=="haste"?"恢复 AP":definition.kind=="restore"?"恢复 SP":definition.kind=="empower"?"增伤":definition.kind=="step"?"移动格数":"基础伤害";
        private static string SkillSecondaryValueName(TacticalSkill definition)=>definition.effect=="depthcleanse"?"每项净化护盾":definition.effect=="apcharge"?"施术者蓄势":definition.effect=="scorch"?"余焰伤害":definition.effect=="retaliate"?"近身反击伤害":definition.effect=="regen"?"下轮治疗":definition.effect=="restoresp"?"额外恢复 SP":"额外护盾";
        private static string SkillKindName(TacticalSkill definition)=>definition.kind=="passive"?"被动":definition.resource=="charge"?"奥义":definition.kind=="guard"?"守护":definition.kind=="heal"?"治疗":definition.kind=="step"?"身法":definition.kind=="haste"||definition.kind=="restore"||definition.kind=="empower"?"辅助":definition.kind=="bind"?"控制":"攻击";
        private static string EnemyEffectText(string effect)=>effect=="rooted"?" · 命中后定身":effect=="weakened"?" · 命中后削弱":effect=="vulnerable"?" · 命中后易伤":"";
        private static string EnemyAffixDescription(TacticalUnit unit,string affix)
        {
            switch(affix){
                case "plated":return "每轮开始获得 6 护盾，护盾上限 40。";
                case "swift":return "移动上限 +1 步，已计入上方属性。";
                case "overloaded":return "最大生命 -5、基础攻击 +3，已计入上方属性。";
                case "shellhard":return "每轮首次受伤减半，向上取整。"+(unit.shellUsed==0?"本轮尚未触发。":"本轮已触发。");
                case "spreading":return "攻击同时波及目标的相邻格。";
                case "rusted":return "倒下后，所在格变为每格消耗 2 步的碎石。";
                case "guarding":return "相邻的其他敌障受到的伤害减少 2。";
                case "binding":return "每第 3 次攻击使目标定身；还需 "+(3-Math.Max(0,Math.Min(2,unit.affixCounter)))+" 次攻击。";
                default:return "特殊战斗效果。";
            }
        }
        private static string RelicRarity(string rarity)=>TacticalContent.RelicTag(rarity)+"旧物";
        private static string RelicArtwork(string id)
        {return TacticalArt.RelicSymbolId(id);}
        private static string RelicTrigger(string id)
        {
            switch(id){
                case "bell":return "战斗首轮";case "needle":case "valvecore":return "击退碰撞";
                case "flask":case "lamp":case "pearl":case "shoulderline":return "玩家回合开始";
                case "seal":return "战斗开始";case "whetstone":return "释放破障";case "medicinebag":case "blanket":return "事件、营火与补给治疗";
                case "whistle":case "echoshell":return "同行连携";case "rubberboots":case "overheatvalve":return "热管地形";
                case "wrench":return "攻击物件与投掷";case "worktag":return "战斗胜利";case "rustwatch":return "商店与战斗胜利";
                case "ruler":return "移动";case "emberfurnace":return "施加余焰";case "doublepen":return "施加削弱与易伤";
                case "brasshorn":return "击杀敌障";case "hotnetmap":return "商店购买";case "brokenscabbard":return "释放处决类技能";
                case "verdictpen":case "jadeimprint":return "攻击在案敌障";case "immunitytalisman":return "每场战斗首次倒下";
                case "sunwheelshard":case "judgeseal":return "释放奥义";case "dreamspool":return "敌障倒下";case "anchorhammer":return "持盾受击";
                case "bladetsuba":return "蒸汽爆散";case "heavywheel":return "获得时与移动";case "moonledger":return "治疗结算";default:return "持有期间";
            }
        }
    }
}
