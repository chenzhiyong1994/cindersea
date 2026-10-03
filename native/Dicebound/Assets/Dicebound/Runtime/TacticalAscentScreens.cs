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
        private string inspectedNode;
        private int displayedMapFloor = -99;
        private static readonly Dictionary<string, Sprite> ascentEmblems = new Dictionary<string, Sprite>();
        private static Material inactiveRouteMaterial;
        private static Material routeBackdropMaterial;
        private const string RoutePainting="route-city-reference";
        // Source rectangles centre the painted silhouettes, rather than the atlas cells' uneven padding.
        private static readonly Rect[] ascentSourceRects={
            new Rect(58,842,339,339),new Rect(443,841,367,367),new Rect(880,847,333,333),
            new Rect(69,460,328,328),new Rect(439,440,375,375),new Rect(891,461,311,311),
            new Rect(45,57,358,358),new Rect(424,34,404,404),new Rect(852,59,356,356)
        };
        private static string NodeKind(string kind) => kind == "elite" ? "幕门守卫" : kind == "shop" ? "行商" : kind == "event" ? "沿途机遇" : kind == "boss" ? "天阙决战" : kind == "camp" ? "营火歇脚" : "普通战斗";
        private string NodeAdvice(TacticalMapNode node)
        {
            int floor=node.floor;bool depth=State!=null&&State.journeyMode=="ascent"&&State.ascentRevision>=2;
            switch(node.kind)
            {
                case "elite":return depth?(floor>=9?"强敌驻守。胜利获得 60 金币与战利品。":"强敌驻守。胜利获得 55 金币与战利品。"):"强敌驻守。胜利获得 45 金币与一项战利品。";
                case "shop":return "购买旧物、领悟技能，或为全队补给。可以随时离开。";
                case "event":return "与沿途的人们交谈，衡量回报与代价后作出选择。";
                case "camp":return depth?"交班灯下休整：恢复 35% 生命、研习技能或守夜戒备。":"短暂停留，在两份帮助中选择一项。";
                case "boss":return "击败顶层守敌，与同行者一同守住曜京。";
                default:
                    int gold=!depth?30:floor>=9?40:floor>=5?35:30;
                    return $"击败全部敌人。胜利获得 {gold} 金币与一项战利品。";
            }
        }
        private void AscentBackdrop(string title, string subtitle)
        {
            // 行商与营火同为室内灯下场景，用偏暗的工坊底板保证标题可读；野外城景仍用路线底板。
            TacticalMenuArt.PlateBackdrop("Ascent city painting",page,title.Contains("行商")||title.Contains("营火")?"treasure-plate":"route-plate",.15f);
            Ui.Panel("Ascent atmosphere", page, Center, Vector2.zero, PaperViewport.DesignSize, new Color(.015f,.028f,.038f,.35f));
            TacticalMenuArt.Heading("Page heading",page,title,new Vector2(-525,458),new Vector2(740,62),44,true).alignment=TextAnchor.MiddleLeft;
            TacticalArt.Plate("Ascent subtitle backing",page,new Vector2(-360,405),new Vector2(1094,46),new Color(.02f,.045f,.05f,.9f),3);
            TacticalMenuArt.Label("Page subtitle",page,subtitle,new Vector2(-360,405),new Vector2(1070,36),22);
            TacticalMenuArt.TextButton("Preferences",page,"设置",new Vector2(700,461),new Vector2(110,48),Settings);
            TacticalMenuArt.TextButton("Title",page,"标题",new Vector2(832,461),new Vector2(142,48),Title);
            TacticalMenuArt.TextButton("Ascent party details",page,"队伍",new Vector2(405,461),new Vector2(110,48),PartyDetails,fontSize:24);
            TacticalMenuArt.TextButton("Ascent inventory",page,"行囊",new Vector2(551,461),new Vector2(110,48),Inventory,fontSize:24);
            TacticalMenuArt.Rule("Ascent header rule",page,new Vector2(0,375),1800);
        }
        private static Sprite AscentCraftSprite(int index)
        {
            string key="craft-"+index;
            if(ascentEmblems.TryGetValue(key,out var cached))return cached;
            var atlas=Resources.Load<Texture2D>("Tactics/JadeCraft/route-nodes-v2");
            if(!atlas)return null;
            var sprite=Visuals.Own(Sprite.Create(atlas,ascentSourceRects[index],Center,100));
            ascentEmblems[key]=sprite;return sprite;
        }
        private static Image AscentEmblem(string name,Transform parent,string kind,Vector2 pos,float size,Color? tint=null)
        {
            if(kind=="camp")
            {
                // The jade node atlas has no campfire cell; the drawn route icon reads as a rest stop
                // instead of borrowing the event question mark.
                var campfire=TacticalArt.Symbol(name,parent,"route-camp",pos,Vector2.one*size);
                if(tint.HasValue)campfire.color=tint.Value;
                return campfire;
            }
            int index=kind=="elite"?1:kind=="shop"?2:kind=="event"?3:kind=="boss"?4:kind=="complete"?5:0;
            var image=Ui.Panel(name,parent,Center,pos,Vector2.one*size,tint??Color.white);
            image.sprite=AscentCraftSprite(index);image.preserveAspect=true;
            if(!image.sprite){image.color=Color.clear;TacticalArt.Symbol(name+" fallback",parent,kind=="shop"?"relic-flask":kind=="event"?"route-event":"route-battle",pos,Vector2.one*size);}
            return image;
        }
        private static Material InactiveRouteMaterial()
        {
            if(inactiveRouteMaterial)return inactiveRouteMaterial;
            var shader=Resources.Load<Shader>("Shaders/Tactics/RouteInactive");
            if(!shader)return null;
            inactiveRouteMaterial=Visuals.Own(new Material(shader){name="Future route antique bronze"});
            inactiveRouteMaterial.SetColor("_Tone",new Color(.89f,.77f,.55f));
            return inactiveRouteMaterial;
        }
        private static Material RouteBackdropMaterial()
        {
            if(routeBackdropMaterial)return routeBackdropMaterial;
            var shader=Resources.Load<Shader>("Shaders/Tactics/RouteBackdrop");
            if(!shader)return null;
            routeBackdropMaterial=Visuals.Own(new Material(shader){name="City detail softened behind route"});
            routeBackdropMaterial.SetFloat("_BlurPixels",3.5f);
            routeBackdropMaterial.SetFloat("_DetailContrast",.90f);
            routeBackdropMaterial.SetFloat("_Saturation",.94f);
            return routeBackdropMaterial;
        }
        private static Image AscentNodePlate(string name,Transform parent,Vector2 pos,float size,int state,bool raycast=false)
        {
            var image=Ui.Panel(name,parent,Center,pos,Vector2.one*size,Color.white,raycast);
            image.sprite=AscentCraftSprite(6+state);image.preserveAspect=true;return image;
        }
        private Vector2 MapPosition(TacticalMapNode node)
        {
            int count=State.mapNodes.Count(n=>n.floor==node.floor);
            float spacing=count==2?292:240;
            float sway=Mathf.Sin(node.floor*.9f)*12;
            return new Vector2((node.lane-(count-1)*.5f)*spacing+sway,MapFloorHeight(node.floor));
        }
        private float MapFloorHeight(int floor)
        {
            // Leave space at the gate and around the immediate choice without losing the twelve-floor overview.
            int focus=Mathf.Max(0,State.floor);
            return 102+floor*71+(floor>focus?22:0);
        }
        private HashSet<string> RemainingMapNodes(IEnumerable<TacticalMapNode> available)
        {
            var remaining=new HashSet<string>();var frontier=new Queue<TacticalMapNode>(available);
            while(frontier.Count>0)
            {
                var node=frontier.Dequeue();if(!remaining.Add(node.id))continue;
                foreach(var id in node.next)
                {
                    var next=State.mapNodes.FirstOrDefault(n=>n.id==id);
                    if(next!=null)frontier.Enqueue(next);
                }
            }
            return remaining;
        }
        private void AscentMap()
        {
            Ui.Panel("Ascent page ground",page,Center,Vector2.zero,PaperViewport.DesignSize,new Color(.025f,.044f,.052f));
            // The approved city is a near-square composition between the sidebars, not a full-screen panorama.
            // Keep its foreground gate, middle terraces and summit together in the actual map aperture.
            var city=Ui.Picture("Jade ascent city",page,Resources.Load<Texture2D>("Tactics/Jade/"+RoutePainting),Center,new Vector2(-44,0),new Vector2(1060,1080),Color.white);
            Ui.Crop(city);
            city.material=RouteBackdropMaterial();
            Ui.Panel("Map reading veil",page,Center,new Vector2(-44,0),new Vector2(1060,1080),new Color(.028f,.063f,.077f,.06f));
            var available=TacticalRules.AvailableNodes(State);
            var remaining=RemainingMapNodes(available);
            if(displayedMapFloor!=State.floor||!State.mapNodes.Any(n=>n.id==inspectedNode))
            {displayedMapFloor=State.floor;inspectedNode=available.FirstOrDefault()?.id??State.currentNodeId;}
            var jade=new Color(.43f,.9f,.76f);var gold=new Color(1,.86f,.55f);
            var side=TacticalMenuArt.QuietPanel("Expedition status",page,new Vector2(-758,0),new Vector2(380,1044)).transform;
            TacticalMenuArt.Decoration("Ascent cloud title",side,"cloud",new Vector2(0,451),new Vector2(366,102));
            TacticalMenuArt.Heading("Page heading",side,"登阙之路",new Vector2(0,451),new Vector2(274,72),50,true).alignment=TextAnchor.MiddleCenter;
            TacticalMenuArt.Rule("Ascent title separator",side,new Vector2(0,393),324);
            var heroes=State.units.Where(u=>u.team=="hero").OrderBy(u=>State.squad.IndexOf(u.heroId)).ToArray();
            for(int i=0;i<heroes.Length;i++)
            {
                var hero=heroes[i];float y=299-i*151;
                JadeUnitPicture("Map portrait "+hero.id,side,hero.heroId,new Vector2(-108,y),new Vector2(100,124),true);
                TacticalMenuArt.Heading("Map hero "+hero.id,side,hero.name,new Vector2(53,y+38),new Vector2(178,42),28);
                TacticalArt.Plate("Map health backing "+hero.id,side,new Vector2(51,y-4),new Vector2(178,10),new Color(.015f,.045f,.04f),4);
                float health=178*Mathf.Clamp01((float)hero.hp/Mathf.Max(1,hero.maxHp));
                TacticalArt.Plate("Map health fill "+hero.id,side,new Vector2(51-(178-health)*.5f,y-4),new Vector2(health,8),jade,3);
                TacticalMenuArt.Label("Map health "+hero.id,side,$"HP  {hero.hp} / {hero.maxHp}",new Vector2(53,y-38),new Vector2(178,36),22);
            }
            TacticalMenuArt.Rule("Journey resource separator",side,new Vector2(0,-91),324);
            TacticalMenuArt.TextButton("Map party details",side,"队伍详情",new Vector2(-82,-137),new Vector2(150,48),PartyDetails,fontSize:22);
            TacticalMenuArt.TextButton("Map inventory",side,"行囊",new Vector2(82,-137),new Vector2(150,48),Inventory,fontSize:22);
            string[] resourceNames={"金币","攀登进度","生还连胜"};string[] resourceValues={State.gold.ToString(),Mathf.Max(0,State.floor+1)+" / 12",State.flawlessStreak.ToString()};
            for(int i=0;i<3;i++){float y=-214-i*58;Label("Journey resource label "+i,side,resourceNames[i],-81,y,148,44,25,TacticalMenuArt.Muted);Label(i==0?"Journey gold":i==1?"Ascent progress":"Ascent streak",side,resourceValues[i],86,y,136,44,29,i==0?TacticalMenuArt.Gold:jade,TextAnchor.MiddleRight);}
            TacticalMenuArt.TextButton("Map last outcome",side,"收获记录",new Vector2(0,-406),new Vector2(306,46),OutcomeDetails,fontSize:23);
            TacticalMenuArt.TextButton("Title",side,"返回标题",new Vector2(0,-482),new Vector2(306,56),Title);

            // All twelve floors stay visible; every edge below comes from the saved graph.
            var viewport=Ui.Rect("Ascent map viewport",page,Center,new Vector2(-44,0),new Vector2(1040,980));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Ui.Rect("Ascent map content",viewport,new Vector2(.5f,0),Vector2.zero,new Vector2(1040,980),new Vector2(.5f,0));
            var lines=Ui.Rect("Ascent route connections",content,new Vector2(.5f,0),Vector2.zero,content.sizeDelta,new Vector2(.5f,0)).gameObject.AddComponent<TacticalMapLines>();
            lines.raycastTarget=false;
            // Keep every saved branch, but reserve the luminous ribbons for the player's actual journey.
            // Future bronze paths sit behind jade history and the next gold choice, as in the approved painting.
            for(int layer=0;layer<3;layer++)
            foreach(var node in State.mapNodes)
            foreach(string next in node.next)
            {
                var target=State.mapNodes.First(n=>n.id==next);
                bool traversed=node.completed&&target.visited;
                bool open=node.id==State.currentNodeId&&available.Any(n=>n.id==next);
                bool abandoned=!traversed&&!open&&!remaining.Contains(target.id);
                if((open?2:traversed?1:0)!=layer)continue;
                Vector2 a=MapPosition(node),b=MapPosition(target);
                float distance=Mathf.Max(0,node.floor-State.floor);
                float futureAlpha=abandoned?.23f:distance<=2?.85f:.66f;
                float stroke=open?6:traversed?4.5f:2.8f;
                // A soft displaced shadow and narrow upper highlight lift each ribbon off the painting.
                Vector2 shadowOffset=new Vector2(3,-4);
                lines.AddCurve(a+shadowOffset,b+shadowOffset,new Color(.006f,.015f,.018f,(open||traversed?.52f:.36f*futureAlpha)),stroke+5,3);
                lines.AddCurve(a,b,new Color(.025f,.048f,.052f,open||traversed?.95f:futureAlpha),stroke+2.4f);
                lines.AddCurve(a,b,open?gold:traversed?new Color(.50f,.91f,.77f):new Color(.64f,.55f,.40f,futureAlpha),stroke);
                Vector2 bevelOffset=new Vector2(0,.75f);
                lines.AddCurve(a+bevelOffset,b+bevelOffset,open?new Color(1,.97f,.81f):traversed?new Color(.79f,1,.94f,.8f):new Color(.95f,.82f,.59f,.48f*futureAlpha),open?1.5f:1);
            }
            var floorTrack=TacticalMenuArt.CraftImage("Floor index backing",content,"inset-panel",new Vector2(-454,488),new Vector2(70,924),sliced:true);
            floorTrack.pixelsPerUnitMultiplier=8;
            floorTrack.rectTransform.anchorMin=floorTrack.rectTransform.anchorMax=new Vector2(.5f,0);
            for(int floor=0;floor<12;floor++)
            {
                bool nextFloor=available.Any(n=>n.floor==floor);
                var label=TacticalMenuArt.Label("Map floor "+floor,content,(floor+1).ToString("00"),new Vector2(-454,MapFloorHeight(floor)),new Vector2(48,40),23,floor==State.floor?jade:nextFloor?gold:TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,0);
            }
            foreach(var node in State.mapNodes)
            {
                var captured=node;bool reachable=available.Any(n=>n.id==node.id);bool current=node.id==State.currentNodeId;
                Vector2 position=MapPosition(node);
                var root=Ui.Rect("Map node "+node.id,content,new Vector2(.5f,0),position,new Vector2(88,78));
                int plate=current||node.completed?2:reachable?1:0;
                bool summit=node.kind=="boss";
                float nodeSize=summit?76:current?72:reachable?70:60;
                var softShadow=AscentNodePlate("Node soft shadow",root,new Vector2(4,-5),nodeSize+9,0);
                softShadow.color=new Color(0,.015f,.02f,.20f);
                var contactShadow=AscentNodePlate("Node contact shadow",root,new Vector2(3,-4),nodeSize+2,0);
                contactShadow.color=new Color(0,.008f,.01f,.55f);
                var hit=AscentNodePlate("Node target",root,Vector2.zero,nodeSize,plate,true);
                bool future=!current&&!node.completed&&!reachable;
                bool abandoned=!current&&!node.completed&&!remaining.Contains(node.id);
                hit.color=abandoned?new Color(.65f,.65f,.65f,1):Color.white;
                var symbol=AscentEmblem("Node emblem "+node.id,root,node.kind,Vector2.zero,summit?47:current||reachable?40:35,current||node.completed?new Color(.65f,1,.89f):abandoned?new Color(.52f,.52f,.52f):Color.white);
                if(future){hit.material=InactiveRouteMaterial();symbol.material=InactiveRouteMaterial();}
                if(reachable)
                {
                    var glow=hit.gameObject.AddComponent<Outline>();glow.effectColor=new Color(1,.72f,.26f,.75f);glow.effectDistance=new Vector2(1,-1);
                }
                var button=root.gameObject.AddComponent<Button>();button.targetGraphic=hit;
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=Color.white*1.18f;colors.pressedColor=Color.white*.8f;button.colors=colors;
                button.onClick.AddListener(()=>{inspectedNode=captured.id;Render();});
                if(node.id==inspectedNode&&!current)
                {
                    var focus=AscentNodePlate("Inspected node rim",root,Vector2.zero,nodeSize+5,plate);
                    focus.color=new Color(.95f,.86f,.65f,.28f);focus.transform.SetAsFirstSibling();
                }
                if(current)
                {
                    var glow=hit.gameObject.AddComponent<Outline>();glow.effectColor=new Color(.30f,1,.81f,.52f);glow.effectDistance=new Vector2(3,-3);
                    // Follow the saved squad's first slot, independent of the inspected portrait or preview default.
                    JadeUnitPicture("Current travelling companion",root,State.squad[0],new Vector2(-55,18),new Vector2(53,66));
                }
            }

            var info=TacticalMenuArt.QuietPanel("Map destination",page,new Vector2(713,0),new Vector2(470,1044)).transform;
            TacticalMenuArt.QuietPanel("Map legend shelf",info,new Vector2(0,438),new Vector2(420,139));
            string[] kinds={"battle","elite","shop","event","camp","boss"};string[] names={"战斗","幕门","商店","事件","营火","决战"};
            for(int i=0;i<kinds.Length;i++)
            {
                float x=(i-2.5f)*80;AscentEmblem("Map legend "+kinds[i],info,kinds[i],new Vector2(x,459),46);
                TacticalMenuArt.Label("Map legend label "+kinds[i],info,names[i],new Vector2(x,413),new Vector2(76,34),22,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
            }
            var inspected=State.mapNodes.FirstOrDefault(n=>n.id==inspectedNode)??State.mapNodes.First();
            string district=inspected.kind=="shop"?"treasure-plate":inspected.kind=="event"?"party-plate":RoutePainting;
            var art=Resources.Load<Texture2D>("Tactics/Jade/"+district);
            if(art)
            {
                TacticalMenuArt.QuietPanel("Destination picture frame",info,new Vector2(0,235),new Vector2(420,240));
                var picture=Ui.Picture("Destination scene painting",info,art,Center,new Vector2(0,235),new Vector2(388,208),Color.white);
                picture.material=RouteBackdropMaterial();
                // A district close-up is easier to read than a second miniature of the entire city.
                float cropWidth=district==RoutePainting?.46f:.56f;
                float cropHeight=cropWidth*art.width/art.height/(388f/208f);
                float offset=district==RoutePainting?Mathf.Lerp(.03f,1-cropHeight-.03f,Mathf.Clamp01(inspected.chapter/4f)):.36f;
                picture.uvRect=new Rect(district=="treasure-plate"?.025f:(1-cropWidth)*.5f,offset,cropWidth,cropHeight);
            }
            AscentEmblem("Destination emblem",info,inspected.kind,new Vector2(0,60),75);
            TacticalMenuArt.Heading("Destination category",info,$"第 {inspected.floor+1} 层 · "+NodeKind(inspected.kind),new Vector2(0,-22),new Vector2(406,64),34,true).alignment=TextAnchor.MiddleCenter;
            TacticalMenuArt.Rule("Destination separator",info,new Vector2(0,-57),366);
            TacticalMenuArt.Heading("Destination name",info,inspected.name.Split('·')[0].Trim(),new Vector2(0,-114),new Vector2(390,54),29).alignment=TextAnchor.MiddleCenter;
            TacticalMenuArt.Label("Destination description",info,NodeAdvice(inspected),new Vector2(0,-213),new Vector2(362,88),23,TacticalMenuArt.Text,TextAnchor.MiddleCenter);
            var request=new TacticalRequest{type="node",choice=inspected.id};bool canEnter=TacticalRules.Preview(State,request).ok;
            string entryLabel=canEnter?"前往"+(inspected.kind=="shop"?"商店":inspected.kind=="event"?"机遇":inspected.kind=="boss"?"决战":inspected.kind=="elite"?"精英战":"战斗"):inspected.completed?"已经通过":inspected.floor<=State.floor?"此路已错过":"尚未解锁";
            TacticalMenuArt.TextButton("Enter map node",info,entryLabel,new Vector2(0,-307),new Vector2(402,88),()=>Request(request),true,fontSize:33).interactable=canEnter;
            string nextHint=string.IsNullOrEmpty(State.currentNodeId)?"选择起点，开启旅程":"沿金色路线前往";
            TacticalMenuArt.Label("Destination availability",info,canEnter?nextHint:inspected.completed?"已完成这一站":"可查看详情 · 暂不可前往",new Vector2(0,-370),new Vector2(374,36),22,canEnter?jade:TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
            TacticalMenuArt.Decoration("Destination workshop ornament",info,"workshop",new Vector2(132,-440),new Vector2(168,152));
        }
        private void AscentShop()
        {
            AscentBackdrop("行商 · 灯下补给","本队招式，自动归属对应同行者。重复获得可升级。");
            TacticalMenuArt.Heading("Shop gold",page,State.gold+" 金币",new Vector2(738,401),new Vector2(318,45),30,true);
            var skills=State.shopOffers.Where(o=>o.kind=="skill").ToArray();
            var supplies=State.shopOffers.Where(o=>o.kind!="skill").ToArray();
            for(int i=0;i<skills.Length;i++)
            {
                var offer=skills[i];var definition=TacticalContent.GetSkill(offer.skillId);var recipient=SkillDisplayRecipient(definition);
                int level=recipient.skills.Contains(offer.skillId)?TacticalRules.SkillLevel(recipient,offer.skillId):0;
                var frame=TacticalMenuArt.QuietPanel("Shop offer "+offer.id,page,new Vector2((i-(skills.Length-1)*.5f)*540,56),new Vector2(500,584)).transform;
                if(!string.IsNullOrEmpty(definition.heroId)&&!State.squad.Contains(definition.heroId))
                {
                    Label("Shop unavailable name "+offer.id,frame,"暂无可习练招式",0,133,420,58,32,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
                    RuleBody("Shop unavailable explanation "+offer.id,frame,"当前队伍没有这项招式的习练者。\n\n重随后将抽取本队同行者的技能。",new Vector2(0,-17),new Vector2(422,170),26);
                    Label("Shop unavailable hint "+offer.id,frame,offer.sold?"已售罄":"可重随货架，或继续前进。",0,-221,420,60,24,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
                    continue;
                }
                Medallion("Shop art "+offer.id,frame,offer.skillId,new Vector2(-164,230),76);
                string source=string.IsNullOrEmpty(definition.heroId)?"通用 · "+recipient.name:TacticalContent.GetHero(definition.heroId)?.name+" · 专属";
                TacticalMenuArt.Label("Shop source "+offer.id,frame,source,new Vector2(46,234),new Vector2(300,42),25,TacticalMenuArt.Muted);
                TacticalMenuArt.Heading("Shop name "+offer.id,frame,definition.name,new Vector2(0,161),new Vector2(420,58),36,true);
                SkillParameterChips("Shop "+offer.id,frame,recipient,definition,115,422);
                RuleBody("Shop effect "+offer.id,frame,OfferedSkillEffect(recipient,definition),new Vector2(0,9),new Vector2(422,156),24);
                var request=new TacticalRequest{type="buy",choice=offer.id};var check=TacticalRules.Preview(State,request);
                var upgrade=InspectionPanel("Shop upgrade panel "+offer.id,frame,new Vector2(0,-126),new Vector2(422,92));
                Label("Shop skill upgrade "+offer.id,upgrade,InspectedUpgradeSummary(recipient,definition),0,0,394,84,22,TacticalMenuArt.Gold,TextAnchor.MiddleCenter);
                TacticalMenuArt.TextButton("Shop buy "+offer.id,frame,offer.sold?"已售罄":offer.price+" 金币 · "+(level>0?"升级":"领悟"),new Vector2(0,-210),new Vector2(410,52),()=>Request(request),true,fontSize:25).interactable=check.ok;
                string availability=offer.sold?"已归入 "+recipient.name:check.ok?"":State.gold<offer.price?"金币不足 · 还差 "+(offer.price-State.gold)+" 金币":check.reason;
                Label("Shop availability "+offer.id,frame,availability,0,-255,414,30,20,offer.sold?TacticalMenuArt.Muted:TacticalTheme.Danger,TextAnchor.MiddleCenter);
            }
            for(int i=0;i<supplies.Length;i++)
            {
                var offer=supplies[i];float x=(i-(supplies.Length-1)*.5f)*810;
                var frame=TacticalMenuArt.QuietPanel("Shop supply "+offer.id,page,new Vector2(x,-332),new Vector2(760,156)).transform;
                var relic=offer.kind=="relic"?TacticalContent.GetRelic(offer.relicId):null;
                if(relic!=null&&!TacticalRules.RelicAllowedForParty(State,relic.id))
                {
                    Label("Shop unavailable supply "+offer.id,frame,"此处暂无适用旧物",-98,25,500,40,24,TacticalMenuArt.Gold);
                    Label("Shop unavailable supply reason "+offer.id,frame,"本队未携带对应同行者，可继续前进。",-98,-23,500,48,22,TacticalMenuArt.Muted);
                    continue;
                }
                string relicTag=relic==null?"":"["+TacticalContent.RelicTag(relic.rarity)+"] ";
                Label("Shop supply name "+offer.id,frame,relicTag+offer.name+" · "+offer.price+" 金币",-98,42,512,38,24,TacticalMenuArt.Gold);
                RuleBody("Shop supply effect "+offer.id,frame,relic==null?offer.text:ReadableEffect(relic.text),new Vector2(-98,-23),new Vector2(512,74),22);
                var request=new TacticalRequest{type="buy",choice=offer.id};
                TacticalMenuArt.TextButton("Shop buy "+offer.id,frame,offer.sold?"售罄":"购买",new Vector2(279,0),new Vector2(154,54),()=>Request(request)).interactable=TacticalRules.Preview(State,request).ok;
            }
            var reroll=new TacticalRequest{type="rerollShop"};
            TacticalMenuArt.TextButton("Reroll shop",page,"重随技能 · "+TacticalRules.ShopRerollCost(State)+" 金币",new Vector2(-330,-461),new Vector2(520,62),()=>Request(reroll),true).interactable=TacticalRules.Preview(State,reroll).ok;
            TacticalMenuArt.TextButton("Leave shop",page,"返回路线",new Vector2(330,-461),new Vector2(520,62),()=>Request(new TacticalRequest{type="leaveShop"}),true);
        }
        private void AscentEvent()
        {
            var e=TacticalRules.CurrentEvent(State);
            AscentBackdrop("沿途机遇",TacticalStory.Stage(State.chapter).location);
            var frame=TacticalMenuArt.QuietPanel("Ascent encounter",page,new Vector2(0,-27),new Vector2(1470,718)).transform;
            AscentEmblem("Encounter seal",frame,"event",new Vector2(-560,141),188);
            TacticalMenuArt.Heading("Encounter title",frame,e.title,new Vector2(95,226),new Vector2(950,66),46,true).alignment=TextAnchor.MiddleLeft;
            TacticalMenuArt.Label("Encounter story",frame,e.body,new Vector2(98,104),new Vector2(950,158),28,TacticalMenuArt.Text);
            TacticalMenuArt.Rule("Encounter separator",frame,new Vector2(0,-36),1320);
            var positions=Ui.Row(1340,24,e.choices.Count,out float width);
            for(int i=0;i<e.choices.Count;i++)
            {
                var choice=e.choices[i];float x=positions[i];
                TacticalArt.Plate("Event choice backing "+choice.id,frame,new Vector2(x,-178),new Vector2(width,264),new Color(.025f,.065f,.069f,.92f),6);
                RuleBody("Event consequence "+choice.id,frame,ReadableEffect(choice.description)+(choice.available?"":"\n\n不可选择 · "+choice.reason),new Vector2(x,-135),new Vector2(width-40,166),23);
                var button=TacticalMenuArt.TextButton("Event "+choice.id,frame,choice.title,new Vector2(x,-263),new Vector2(width-28,60),()=>Request(new TacticalRequest{type="event",choice=choice.id}),i==0,fontSize:24);
                button.interactable=choice.available;
            }
        }
        private void AscentCamp()
        {
            AscentBackdrop("营火 · 交班灯下","整理装备，恢复体力，再一起出发。");
            var frame=TacticalMenuArt.QuietPanel("Camp rest panel",page,new Vector2(0,-27),new Vector2(1470,640)).transform;
            AscentEmblem("Camp seal",frame,"camp",new Vector2(-560,161),188);
            TacticalMenuArt.Heading("Camp title",frame,"交班灯下",new Vector2(95,236),new Vector2(950,66),46,true).alignment=TextAnchor.MiddleLeft;
            TacticalMenuArt.Label("Camp story",frame,"一盏暖灯照亮了歇脚处。热汤、修好的器物与轮值的守夜人都在——\n\n大家商量好下一步，再一起上路。",new Vector2(98,120),new Vector2(950,160),28,TacticalMenuArt.Text);
            TacticalMenuArt.Rule("Camp separator",frame,new Vector2(0,16),1320);
            var rest=new TacticalRequest{type="camp",choice="rest"};
            var study=new TacticalRequest{type="camp",choice="study"};
            var ward=new TacticalRequest{type="camp",choice="ward"};
            var columns=Ui.Row(1400,24,3,out float width);
            TacticalMenuArt.TextButton("Camp rest",frame,"休整包扎 · 恢复 35% 生命",new Vector2(columns[0],-140),new Vector2(width,120),()=>Request(rest),true,fontSize:26);
            var studyButton=TacticalMenuArt.TextButton("Camp study",frame,"研习技艺 · 随机领悟或升级",new Vector2(columns[1],-140),new Vector2(width,120),()=>Request(study),fontSize:26);
            studyButton.interactable=TacticalRules.Preview(State,study).ok;
            TacticalMenuArt.TextButton("Camp ward",frame,"守夜戒备 · 下一战全员 +5 护盾",new Vector2(columns[2],-140),new Vector2(width,120),()=>Request(ward),fontSize:26);
        }
        private void AscentEnding()
        {
            bool won=State.phase=="victory";Audio.SetTheme(won?"victory":"defeat");AscentBackdrop(won?"登阙 · 曙光再临":"整装 · 再次出发",won?"顶层敌障散去。伙伴们并肩而立，曜京重归安宁。":"换一种阵容与路线，再与同伴踏上前路。");
            Ui.Panel("Ending city veil",page,Center,Vector2.zero,PaperViewport.DesignSize,new Color(.006f,.018f,.024f,.52f));
            var frame=TacticalMenuArt.QuietPanel("Ascent ending panel",page,new Vector2(0,-30),new Vector2(1420,784)).transform;
            AscentEmblem("Ending seal",frame,won?"boss":"event",new Vector2(0,239),138);
            TacticalMenuArt.Heading("Ascent result",frame,won?"十二重天阙，与你并肩。":"此路未尽，来日再会。",new Vector2(0,98),new Vector2(1210,92),52,true).alignment=TextAnchor.MiddleCenter;
            TacticalMenuArt.Rule("Ending engraved rule",frame,new Vector2(0,35),1160);
            TacticalMenuArt.Label("Expedition record",frame,$"抵达第 {State.floor+1} 层  ·  金币 {State.gold}  ·  旧物 {State.relics.Count} 件\n同行者  "+string.Join("、",State.squad.Select(id=>TacticalContent.GetHero(id).name)),new Vector2(0,-47),new Vector2(1160,100),28,TacticalMenuArt.Text,TextAnchor.MiddleCenter);
            var relics=InspectionScroll("Ending relic list",frame,new Vector2(0,-155),new Vector2(1160,76),76);
            var relicText=Label("Ending relics",relics,State.relics.Count==0?"伙伴并肩，旅途留痕。":"此行旧物："+string.Join("、",State.relics.Select(id=>TacticalContent.GetRelic(id).name)),-7,0,1132,76,24,TacticalMenuArt.Muted,TextAnchor.UpperCenter);
            ResizeInspectionText(relics,relicText,76);
            TacticalMenuArt.TextButton("Ending inventory",frame,"查看此行行囊",new Vector2(0,-226),new Vector2(300,42),Inventory,fontSize:23);
            TacticalMenuArt.TextButton("Restart",frame,"重新组队 · 再次出发",new Vector2(-310,-294),new Vector2(530,72),SelectParty,true);
            TacticalMenuArt.TextButton("Return title",frame,"返回标题",new Vector2(310,-294),new Vector2(530,72),Title);
        }
    }

    // Canvas geometry obeys RectMask2D; connections are built from the persisted graph, never decorative links.
    internal sealed class TacticalMapLines : MaskableGraphic
    {
        private struct Edge { public Vector2[] points;public Color color;public float width,feather; }
        private readonly List<Edge> edges=new List<Edge>();
        public void AddCurve(Vector2 a,Vector2 b,Color tint,float width,float feather=1)
        {
            const int segments=18;var points=new Vector2[segments+1];
            float rise=(b.y-a.y)*.52f;
            Vector2 p=a+Vector2.up*rise,q=b-Vector2.up*rise;
            for(int i=0;i<=segments;i++)
            {
                float t=(float)i/segments,u=1-t;
                points[i]=u*u*u*a+3*u*u*t*p+3*u*t*t*q+t*t*t*b;
            }
            edges.Add(new Edge{points=points,color=tint,width=width,feather=feather});SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            foreach(var edge in edges)
            {
                int start=helper.currentVertCount;
                var feather=edge.color;feather.a=0;
                for(int i=0;i<edge.points.Length;i++)
                {
                    Vector2 delta=edge.points[Mathf.Min(i+1,edge.points.Length-1)]-edge.points[Mathf.Max(0,i-1)];
                    Vector2 normal=new Vector2(-delta.y,delta.x).normalized;
                    float half=edge.width*.5f;var point=edge.points[i];
                    helper.AddVert(point-normal*(half+edge.feather),feather,Vector2.zero);
                    helper.AddVert(point-normal*half,edge.color,Vector2.zero);
                    helper.AddVert(point+normal*half,edge.color,Vector2.zero);
                    helper.AddVert(point+normal*(half+edge.feather),feather,Vector2.zero);
                    if(i==0)continue;
                    for(int strip=0;strip<3;strip++)
                    {
                        int before=start+(i-1)*4+strip,after=start+i*4+strip;
                        helper.AddTriangle(before,before+1,after+1);helper.AddTriangle(before,after+1,after);
                    }
                }
            }
        }
    }
}
