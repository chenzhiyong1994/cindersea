using System;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    public sealed partial class TacticalDirector
    {
        private const float JadeAllyHeight=220,JadeAllyGap=12,JadeSkillWidth=182,JadeSkillHeight=220,JadeSkillGap=10,JadeSkillIcon=58;
        private const int BattleSkillsPerPage=5;
        private Text BattleLabel(string name,Transform parent,string text,float x,float y,float width,float height,int size=TacticalTheme.FontButton,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var label=TacticalUi.FieldLabel(name,parent,text,new Vector2(x,y),new Vector2(width,Mathf.Max(height,Mathf.Ceil(size*1.55f))),size,color??TacticalTheme.OnDark,align);
            if(name=="Battle chapter"||name=="Battle turn"||name.StartsWith("Ally name")||name.StartsWith("Skill title")||name=="Move label")label.font=TacticalMenuArt.Brush;
            return label;
        }
        private Transform Hud(string name,float x,float y,float width,float height)
        {
            return TacticalArt.FieldSurface(name,page,new Vector2(x,y),new Vector2(width,height),raycast:true).transform;
        }

        private void Battle()
        {
            var chapter=TacticalStory.Stage(State.chapter);Stage.Synchronize(State);
            var header=Ui.Rect("Battle header",page,Center,new Vector2(0,492),new Vector2(1872,68));int enemies=State.units.Count(u=>u.team=="enemy"&&u.hp>0);
            TacticalArt.FieldSurface("Battle chapter plate",header,new Vector2(-738,0),new Vector2(396,56));
            var node=TacticalRules.CurrentNode(State);
            string title=State.journeyMode=="ascent"?"第 "+(State.floor+1)+" 层 · "+(node?.name.Split('·')[0].Trim()??chapter.location):(State.sideBattle?"支路 · ":"")+chapter.title;
            BattleLabel("Battle chapter",header,title,-738,0,360,38,TacticalTheme.FontEmphasis,TacticalTheme.PaperLight);
            TacticalArt.FieldSurface("Round inset",header,new Vector2(-8,0),new Vector2(314,48));
            BattleLabel("Battle turn",header,$"第 {State.round} 回合 · 我方行动",-8,0,286,36,TacticalTheme.FontButton,TacticalTheme.Gold,TextAnchor.MiddleCenter);
            TacticalUi.FieldButton("Ground view",header,"查看地面",new Vector2(248,0),new Vector2(172,46),()=>Stage.SetGroundView(!Stage.GroundViewActive),fontSize:TacticalTheme.FontSecondary);
            HeaderTools(header,true);
            var allies=State.units.Where(u=>u.team=="hero").ToArray();if(selected==null||!allies.Any(u=>u.id==selected&&u.hp>0))selected=allies.FirstOrDefault(u=>u.hp>0&&!u.turnEnded)?.id??allies.FirstOrDefault(u=>u.hp>0)?.id;
            for(int i=0;i<allies.Length;i++)AllyCard(allies[i],-796,336-i*(JadeAllyHeight+JadeAllyGap),i);
            var objective=Hud("Battle objective plate",798,393,276,48);
            BattleLabel("Objective",objective,"清敌 · 剩余 "+enemies+" 名敌人",0,0,244,36,TacticalTheme.FontButton,TacticalTheme.Gold,TextAnchor.MiddleCenter);
            var commands=Hud("Tactical commands",798,138,276,316);
            previewPanel=(RectTransform)commands;
            previewHeading=BattleLabel("Preview heading",commands,"行动预览",0,127,244,30,TacticalTheme.FontSecondary,TacticalTheme.Gold);
            // Live hover summaries must be readable while the pointer stays on the board.
            // Long rules belong in the fixed inspector, never in a hover-only scroll view.
            preview=Ui.Label("Action preview",commands,"",Center,new Vector2(0,-22),new Vector2(244,250),20,TacticalTheme.OnDark,TextAnchor.UpperLeft);
            preview.lineSpacing=1f;
            var terrain=Hud("Tile information",798,-143,276,222);
            BattleLabel("Tile heading",terrain,"地块信息",0,84,244,30,TacticalTheme.FontSecondary,TacticalTheme.Gold);
            tilePreview=Ui.Label("Tile preview",terrain,"",Center,new Vector2(0,-22),new Vector2(244,160),19,TacticalTheme.OnDark,TextAnchor.UpperLeft);
            tilePreview.lineSpacing=1.05f;
            commit=TacticalUi.FieldButton("Commit action",page,"确认行动 · Space",new Vector2(798,-377),new Vector2(276,60),Commit,true,icon:"confirm");commit.interactable=false;
            var active=TacticalRules.FindUnit(State,selected);
            TacticalUi.FieldButton("End round",page,"结束"+(active?.name??"角色")+"行动 · E",new Vector2(798,-452),new Vector2(276,60),EndSelectedTurn,icon:"endTurn").interactable=active!=null&&!active.turnEnded;
            TacticalUi.FieldButton("Inspect target",page,"地块 / 目标详情",new Vector2(798,-303),new Vector2(276,44),InspectTarget,fontSize:TacticalTheme.FontSecondary);
            TacticalUi.FieldButton("Battle party details",page,"队伍详情",new Vector2(-796,-360),new Vector2(280,60),PartyDetails,icon:"help",fontSize:TacticalTheme.FontButton);
            TacticalUi.FieldButton("Battle inventory",page,"行囊",new Vector2(-796,-438),new Vector2(280,60),Inventory,fontSize:TacticalTheme.FontButton);
            TacticalUi.FieldButton("Cancel action",page,"取消 · 右键 / Esc",new Vector2(798,-509),new Vector2(276,44),()=>CancelSelection(),fontSize:TacticalTheme.FontSecondary);
            var intents=TacticalRules.EnemyIntents(State).ToArray();int attacks=intents.Count(i=>i.attacks);
            TacticalUi.FieldButton("Enemy intentions",page,attacks>0?"敌人动向 · "+attacks+" 准备攻击":"敌人动向 · 正在接近",new Vector2(798,326),new Vector2(276,44),EnemyIntentions,fontSize:TacticalTheme.FontBody);
            DrawSkills();RefreshPreview();DrawSelection();
        }
        public void RefreshGroundViewControl()
        {
            if(!page)return;var control=page.Find("Battle header/Ground view");if(!control)return;
            bool active=Stage.GroundViewActive;
            control.GetComponentInChildren<Text>().text=active?"恢复人物":"查看地面";
            control.GetComponent<Image>().color=Color.clear;
            control.GetComponent<TacticalBattleFrame>().Configure(active,false,true);
        }

        private void AllyCard(TacticalUnit unit,float x,float y,int order)
        {
            var cardSize=new Vector2(280,JadeAllyHeight);
            bool active=selected==unit.id;var panel=TacticalArt.FieldSurface("Ally "+unit.id,page,new Vector2(x,y),cardSize,rim:active?TacticalTheme.HudSelectedRule:TacticalTheme.HudRule).transform;
            TacticalUi.Portrait("Ally portrait "+unit.id,panel,Picture("Heroes",unit.heroId),unit.heroId,new Vector2(-87,1),new Vector2(88,144),true,0,radius:1);
            TacticalArt.Plate("Shortcut shade",panel,new Vector2(-114,68),new Vector2(28,32),TacticalTheme.BadgeBackdrop,2);
            BattleLabel("Ally shortcut "+unit.id,panel,"F"+(order+1),-114,68,28,32,TacticalTheme.FontSecondary,TacticalTheme.PaperLight,TextAnchor.MiddleCenter);
            bool danger=unit.hp<=unit.maxHp*.3f;
            BattleLabel("Ally name "+unit.id,panel,unit.name,53,72,148,40,TacticalTheme.FontEmphasis,danger?TacticalTheme.Danger:active?TacticalTheme.Gold:TacticalTheme.PaperLight);
            BattleLabel("Ally health "+unit.id,panel,$"{unit.hp}/{unit.maxHp}",unit.block>0?20:53,31,unit.block>0?82:148,30,21,TacticalTheme.PaperLight);
            if(unit.block>0)BattleLabel("Ally shield "+unit.id,panel,"盾 "+unit.block,93,31,66,30,20,TacticalArt.ShieldLabelColor,TextAnchor.MiddleRight);
            TacticalArt.HealthShieldMeter("Health "+unit.id,panel,new Vector2(53,9),new Vector2(146,10),unit.hp,unit.maxHp,unit.block,danger?TacticalTheme.Danger:TacticalTheme.Ally);
            BattleLabel("AP label "+unit.id,panel,"AP",-23,-11,32,28,18,TacticalTheme.OnDarkSub);TacticalUi.Pips("AP "+unit.id,panel,0,-11,unit.ap,unit.maxAp,TacticalTheme.Gold,icon:"ap",width:122);
            BattleLabel("SP label "+unit.id,panel,"SP",-23,-40,32,28,18,TacticalTheme.OnDarkSub);TacticalUi.Pips("SP "+unit.id,panel,0,-40,unit.sp,unit.maxSp,TacticalTheme.SpBlue,icon:"sp",width:122);
            BattleLabel("Charge amount "+unit.id,panel,unit.turnEnded?"已结束":"蓄势 "+unit.charge,26,-67,98,25,16,unit.turnEnded?TacticalTheme.Gold:TacticalTheme.OnDarkSub);
            TacticalUi.Meter("Charge "+unit.id,panel,100,-67,46,5,unit.charge/100f,JinhaiArt.Accent(unit.heroId),border:1,field:true);
            var hit=Ui.Panel("Select "+unit.id,panel,Center,Vector2.zero,cardSize,Color.clear,true);var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=panel.GetComponent<Image>();button.transition=Selectable.Transition.None;var colors=button.colors;colors.normalColor=TacticalTheme.ButtonNormal;colors.highlightedColor=TacticalTheme.ButtonHover;colors.pressedColor=TacticalTheme.ButtonPressed;colors.disabledColor=TacticalTheme.HudButtonDisabled;button.colors=colors;button.interactable=unit.hp>0;button.onClick.AddListener(()=>Select(unit.id));
            // A separate hit target keeps status inspection distinct from selecting the actor.
            var status=TacticalArt.Plate("Ally status "+unit.id,panel,new Vector2(0,-95),new Vector2(246,27),new Color(.08f,.18f,.18f,.92f),4,true);
            var inspect=status.gameObject.AddComponent<Button>();inspect.targetGraphic=status;inspect.onClick.AddListener(()=>InspectStatuses(unit.id));
            BattleLabel("Ally status summary "+unit.id,status.transform,TacticalBattleCopy.StatusSummary(unit),0,0,230,27,17,TacticalTheme.PaperLight,TextAnchor.MiddleCenter);
        }

        private void DrawSkills()
        {
            var unit=TacticalRules.FindUnit(State,selected);int pages=Mathf.Max(1,Mathf.CeilToInt((unit?.skills.Count??0)/(float)BattleSkillsPerPage));skillPage=Mathf.Clamp(skillPage,0,pages-1);
            var visible=unit?.skills.Skip(skillPage*BattleSkillsPerPage).Take(BattleSkillsPerPage).ToArray()??Array.Empty<string>();int columns=visible.Length+1,rowCount=1;
            float captionHeight=TacticalTheme.BattleToolbarHeight,captionGap=12;
            float width=JadeSkillWidth,totalWidth=columns*width+(columns-1)*JadeSkillGap,height=captionHeight+captionGap+rowCount*JadeSkillHeight+(rowCount-1)*TacticalTheme.BattleSkillRowGap,top=-522+height;
            float hintWidth=Mathf.Max(680,totalWidth);
            var panel=Ui.Rect("Skill tray",page,Center,new Vector2(0,-516+height*.5f),new Vector2(hintWidth,height));
            var safe=TacticalTheme.BoardSafeRect;safe.yMin=Mathf.Max(safe.yMin,top+26);Stage.SetBoardSafeRect(safe);
            float headingY=height*.5f-captionHeight*.5f;
            if(unit==null)return;
            if(skill!="throw")
            {
                TacticalArt.FieldSurface("Battle navigation plate",panel,new Vector2(0,headingY),new Vector2(hintWidth,captionHeight),TacticalTheme.HudInset);
                BattleLabel("Battle navigation",panel,"点目标 → Space 确认 · 右键取消"+(pages>1?"  ·  "+(skillPage+1)+" / "+pages:""),0,headingY,hintWidth-(pages>1?124:32),28,21,TacticalTheme.OnDarkSub,TextAnchor.MiddleCenter);
            }
            if(skill=="throw")
            {
                int pickupRange=State.relics.Contains("wrench")?2:1;
                var objects=State.objects.Where(o=>o.hp>0&&Math.Abs(o.x-unit.x)+Math.Abs(o.y-unit.y)<=pickupRange).OrderBy(o=>o.id).ToArray();if(!objects.Any(o=>o.id==throwObject))throwObject=objects.FirstOrDefault()?.id;
                if(objects.Length>0){var item=objects.First(o=>o.id==throwObject);var selector=TacticalUi.FieldButton("Throw source",panel,"投掷："+(item.kind=="valve"?"蒸汽阀":item.kind=="canister"?"热罐":"木箱")+" "+GridName(item.x,item.y)+(objects.Length>1?" · 切换":""),new Vector2(0,headingY),new Vector2(hintWidth-(pages>1?104:0),captionHeight),()=>{throwObject=objects[(Array.IndexOf(objects,item)+1)%objects.Length].id;pending=null;hovered=null;Render();},fontSize:TacticalTheme.FontSecondary);selector.interactable=objects.Length>1;}
                else{TacticalArt.FieldSurface("Battle navigation plate",panel,new Vector2(0,headingY),new Vector2(hintWidth,captionHeight),TacticalTheme.HudInset);BattleLabel("Battle navigation",panel,"身旁没有可投掷物件",0,headingY,hintWidth-32,28,TacticalTheme.FontSecondary,TacticalTheme.OnDarkSub,TextAnchor.MiddleCenter);}
            }
            if(pages>1){TacticalUi.FieldButton("Previous skill page",panel,"‹",new Vector2(-hintWidth*.5f+24,headingY),new Vector2(44,captionHeight),()=>{skillPage=(skillPage+pages-1)%pages;Render();});TacticalUi.FieldButton("Next skill page",panel,"›",new Vector2(hintWidth*.5f-24,headingY),new Vector2(44,captionHeight),()=>{skillPage=(skillPage+1)%pages;Render();});}
            var positions=Ui.Row(totalWidth,JadeSkillGap,columns,out width);var rows=Ui.Column(height*.5f-captionHeight-captionGap,TacticalTheme.BattleSkillRowGap,Enumerable.Repeat(JadeSkillHeight,rowCount).ToArray());
            var move=TacticalUi.FieldButton("Move mode",panel,"",new Vector2(positions[0],rows[0]),new Vector2(width,JadeSkillHeight),()=>ChooseSkill(null),skill==null,subtle:true);
            var moveAvailability=TacticalRules.SkillAvailability(State,unit.id,null);
            TacticalArt.SkillSymbol("Move illustration",move.transform,"move",new Vector2(0,76),JadeSkillIcon);
            BattleLabel("Move label",move.transform,"移动",0,28,width-20,32,22,TacticalTheme.PaperLight,TextAnchor.MiddleCenter);
            BattleLabel("Move purpose",move.transform,"调整站位\n接敌或避开危险",0,-45,width-20,52,18,TacticalTheme.OnDarkSub,TextAnchor.MiddleCenter);
            BattleLabel("Move cost",move.transform,unit.turnEnded?"已结束":unit.rooted?"已定身":unit.ap<1?"1 AP · 不足":"1 AP · "+TacticalRules.MoveBudget(State)+"步",0,-88,width-12,28,18,moveAvailability.ok?TacticalTheme.OnDarkSub:TacticalTheme.Danger,TextAnchor.MiddleCenter);
            SkillSelectionBadge(move,skill==null);
            TacticalUi.HoverHint(move,()=>{if(pending==null&&!Busy&&!Modal)SetMovePreview();},RefreshPreview);
            for(int i=0;i<visible.Length;i++)
            {
                var definition=TacticalContent.GetSkill(visible[i]);int cooldown=unit.cooldowns.TryGetValue(definition.id,out var remaining)?remaining:0;string cost=definition.resource=="charge"?"100 蓄势":definition.cost+" "+(definition.resource=="sp"?"SP":"AP");
                var button=TacticalUi.FieldButton("Skill "+definition.id,panel,"",new Vector2(positions[(i+1)%columns],rows[(i+1)/columns]),new Vector2(width,JadeSkillHeight),()=>ChooseSkill(definition.id),skill==definition.id,subtle:true);
                int resource=definition.resource=="sp"?unit.sp:definition.resource=="charge"?unit.charge:unit.ap;
                var availability=TacticalRules.SkillAvailability(State,unit.id,definition.id);bool unavailable=definition.kind!="passive"&&!availability.ok;
                string caption=definition.kind=="passive"?"常驻生效":unit.turnEnded?"已结束":cooldown>0?"冷却 "+cooldown+" 轮":resource<definition.cost?cost+" · 不足":!availability.ok?cost+" · "+ShortSkillRequirement(definition,unit):cost;
                TacticalArt.SkillSymbol("Skill illustration "+definition.id,button.transform,definition.id,new Vector2(0,76),JadeSkillIcon);
                BattleLabel("Skill title "+definition.id,button.transform,definition.name,0,28,width-12,30,22,TacticalTheme.PaperLight,TextAnchor.MiddleCenter);
                BattleLabel("Skill level "+definition.id,button.transform,"Lv."+TacticalRules.SkillLevel(unit,definition.id),0,-4,width-12,23,17,TacticalTheme.Gold,TextAnchor.MiddleCenter);
                BattleLabel("Skill purpose "+definition.id,button.transform,TacticalBattleCopy.SkillPurpose(State,unit,definition),0,-45,width-20,52,18,TacticalTheme.OnDarkSub,TextAnchor.MiddleCenter);
                BattleLabel("Skill cost "+definition.id,button.transform,caption,0,-88,width-12,28,18,unavailable?TacticalTheme.Danger:TacticalTheme.OnDarkSub,TextAnchor.MiddleCenter);button.interactable=unit.hp>0;
                SkillSelectionBadge(button,skill==definition.id);
                TacticalUi.HoverHint(button,()=>{if(pending==null&&!Busy&&!Modal)SetSkillPreview(definition,unit);},RefreshPreview);
            }
        }
        private void SkillSelectionBadge(Button button,bool active)
        {
            if(!active)return;
            var rect=(RectTransform)button.transform;rect.anchoredPosition+=Vector2.up*6;
            for(int i=3;i>=1;i--){var glow=TacticalArt.Outline("Selected skill soft glow "+i,button.transform,Vector2.zero,rect.sizeDelta+Vector2.one*(i*4),new Color(1,.82f,.44f,.18f/i),6+i);glow.transform.SetAsFirstSibling();}
            var wash=TacticalArt.Plate("Selected skill warm light",button.transform,Vector2.zero,rect.sizeDelta-Vector2.one*10,new Color(1,.77f,.34f,.08f),4);wash.transform.SetSiblingIndex(4);
            var rim=button.transform.Find("Authored metal rim");
            if(rim){var glow=rim.gameObject.AddComponent<Outline>();glow.effectColor=new Color(1,.83f,.48f,.64f);glow.effectDistance=new Vector2(1,-1);}
            TacticalArt.Plate("Selected skill gold",button.transform,new Vector2(0,-104),new Vector2(96,3),new Color(.94f,.77f,.39f),1);
            float sealX=rect.sizeDelta.x*.5f-21,sealY=rect.sizeDelta.y*.5f-21;
            TacticalArt.Plate("Selected skill seal",button.transform,new Vector2(sealX,sealY),new Vector2(26,26),new Color(.16f,.17f,.11f),13);
            BattleLabel("Selected skill badge",button.transform,"✓",sealX,sealY,24,26,20,TacticalTheme.Gold,TextAnchor.MiddleCenter);
        }
        private string ShortSkillRequirement(TacticalSkill definition,TacticalUnit unit)
        {
            if(definition.condition=="mist")return "需水雾";
            if(definition.condition=="shadow")return "需阴影";
            if(definition.kind=="step"&&unit.rooted)return "已定身";
            if(definition.kind=="throw")return "缺少物件";
            return "不可用";
        }
    }
}
