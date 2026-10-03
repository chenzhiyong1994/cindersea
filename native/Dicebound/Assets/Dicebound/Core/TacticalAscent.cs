using System;
using System.Collections.Generic;
using System.Linq;
namespace Dicebound.Tactics
{
    public static partial class TacticalRules
    {
        public const int AscentFloors=12;
        // 已发布内容池的顺序也是种子与货架契约，扩充内容只用于 revision 3。
        static readonly string[] PublishedSkillIds={"strike","throw","brace","shove","step","balance","moon","cleave","tide","anchor","stitch","line","mend","burst","hook","haste","ward","oath","jadeguard","lawmark","edict","verdict","lawshare","countermove","sever","sixuan_ultimate","moonveil","nightpin","pursuit","needlesweep","veiledge","duskguard","yanzhuying_ultimate","ember","flamesweep","battlecry","mountainstance","execution","advanceguard","groundbreaker","lingfeng_ultimate","tidemend","tideward","rainrest","cleansingwave","resonance","undertow","deepcurrent","cangling_ultimate","nail","bulwark","counterthrust","quake","ironmarch","earthbind","lastline","shangshuo_ultimate","ruanzhuo_ultimate"};
        static readonly string[] PublishedRelicIds={"bell","needle","flask","lamp","pearl","seal"};
        public static IEnumerable<TacticalSkill> AvailableSkillCatalogue(TacticalState state)
        {return state.ascentRevision>=3?TacticalContent.Skills:PublishedSkillIds.Select(TacticalContent.GetSkill);}
        public static IEnumerable<TacticalRelic> AvailableRelicCatalogue(TacticalState state)
        {return state.ascentRevision>=3?TacticalContent.Relics:PublishedRelicIds.Select(TacticalContent.GetRelic);}
        // revision=1 keeps the original curve contract; revision=2 enables the depth systems (combos,
        // drench, flawless streaks, three-act gates and camp floors) without touching existing saves.
        public static TacticalState NewAscent(uint seed,IEnumerable<string> heroIds,int revision=1,int trial=0)
        {
            var state=NewRun(seed,heroIds);
            state.journeyMode="ascent";state.ascentRevision=Math.Max(1,revision);state.gold=60;state.floor=-1;state.currentNodeId="";
            state.trial=revision>=3?Math.Max(0,trial):0;
            state.mapNodes=CreateAscentGraph(seed,state.ascentRevision);state.units.RemoveAll(u=>u.team=="enemy");state.objects.Clear();
            state.phase="map";state.log.Clear();AddLog(state,"带上补给，与伙伴一起穿过曜京。");return state;
        }
        public static TacticalMapNode CurrentNode(TacticalState state)
        {return state?.mapNodes?.FirstOrDefault(n=>n.id==state.currentNodeId);}
        public static List<TacticalMapNode> AvailableNodes(TacticalState state)
        {
            if(state?.journeyMode!="ascent"||state.phase!="map")return new List<TacticalMapNode>();
            var current=CurrentNode(state);
            return state.mapNodes.Where(n=>!n.visited&&(current==null?n.floor==0:current.completed&&current.next.Contains(n.id))).OrderBy(n=>n.lane).ToList();
        }
        internal static List<TacticalMapNode> CreateAscentGraph(uint seed,int revision=1)
        {
            if(revision>=2)return CreateDepthGraph(seed,revision);
            uint random=seed^0xa511e9b3u;var result=new List<TacticalMapNode>();
            string[] names={"晨灯街","热管廊","回水桥","旧档库","昼轮前庭"};
            string[] kinds={"battle","battle","event","shop","elite"};
            for(int floor=0;floor<AscentFloors;floor++)
            {
                int count=floor==AscentFloors-1?1:2+(int)(MapNext(ref random)%2);
                for(int lane=0;lane<count;lane++)
                {
                    string kind=floor==AscentFloors-1?"boss":floor==0||lane==0?"battle":kinds[MapNext(ref random)%kinds.Length];
                    if(floor==2&&lane==1)kind="event";
                    if(floor==4&&lane==1)kind="shop";
                    if(floor==7&&lane==1)kind="elite";
                    int chapter=Math.Min(4,floor*5/AscentFloors);
                    string label=kind=="boss"?"顶层守卫":kind=="elite"?"强敌巡守":kind=="shop"?"沿路商店":kind=="event"?"路边小遇":"普通战斗";
                    result.Add(new TacticalMapNode{id="floor-"+floor+"-"+lane,floor=floor,lane=lane,chapter=chapter,kind=kind,name=names[chapter]+" · "+label,
                        text=kind=="shop"?"用金币购买补给、旧物或技能。":kind=="event"?"稍作停留，选择一份补给。":kind=="elite"?"击败强敌，获得更多金币与奖励。":kind=="boss"?"击败顶层敌障，守住曜京。":"击败全部敌障，获得金币与奖励。",
                        eventId=kind=="event"?TacticalStory.OriginalEvents[MapNext(ref random)%TacticalStory.OriginalEvents.Length].id:""});
                }
            }
            for(int floor=0;floor<AscentFloors-1;floor++)
            {
                var row=result.Where(n=>n.floor==floor).ToArray();var next=result.Where(n=>n.floor==floor+1).ToArray();
                foreach(var node in row)
                {
                    int nearest=(int)Math.Round(node.lane*(next.Length-1)/(double)(row.Length-1));
                    node.next.Add(next[nearest].id);
                    if(next.Length>1){int adjacent=nearest==0?1:nearest==next.Length-1?nearest-1:MapNext(ref random)%2==0?nearest-1:nearest+1;node.next.Add(next[adjacent].id);node.next.Sort(StringComparer.Ordinal);}
                }
            }
            return result;
        }
        // 深化路线：三幕波形。第 4/8 层为幕门守卫（双精英），第 5/9 层保证商店与营火，
        // 其余层在种子驱动下从分层卡池抽取节点种类。
        internal static List<TacticalMapNode> CreateDepthGraph(uint seed,int revision=2)
        {
            uint random=seed^0x7f4a7c15u;var result=new List<TacticalMapNode>();
            var events=revision>=3?TacticalStory.Events:TacticalStory.OriginalEvents;
            string[] names={"晨灯街","热管廊","回水桥","旧档库","昼轮前庭"};
            string[][] pools={
                new[]{"battle","event","shop"},
                new[]{"battle","battle","event","shop"},
                new[]{"battle","battle","elite","event","shop"},
                new[]{"battle","elite","event","shop"}
            };
            string[] FloorKinds(int floor)
            {
                switch(floor)
                {
                    case 0:return new[]{"battle","battle"};
                    case 4:case 8:return new[]{"elite","elite"};
                    case 5:case 9:return MapNext(ref random)%2==0?new[]{"shop","camp"}:new[]{"camp","shop"};
                    case 1:case 3:return TakeKinds(ref random,pools[0],2);
                    case 2:case 6:return TakeKinds(ref random,pools[1],3);
                    case 7:return TakeKinds(ref random,pools[2],3);
                    case 10:return TakeKinds(ref random,pools[3],3);
                    default:return new[]{"boss"};
                }
            }
            for(int floor=0;floor<AscentFloors;floor++)
            {
                string[] floorKinds=FloorKinds(floor);
                for(int lane=0;lane<floorKinds.Length;lane++)
                {
                    string kind=floorKinds[lane];
                    int chapter=Math.Min(4,floor*5/AscentFloors);
                    string label=kind=="boss"?"顶层守卫":kind=="elite"?"幕门守卫":kind=="shop"?"沿路商店":kind=="event"?"路边小遇":kind=="camp"?"营火歇脚":"普通战斗";
                    result.Add(new TacticalMapNode{id="floor-"+floor+"-"+lane,floor=floor,lane=lane,chapter=chapter,kind=kind,name=names[chapter]+" · "+label,
                        text=kind=="shop"?"用金币购买补给、旧物或技能。":kind=="event"?"稍作停留，选择一份帮助。":kind=="elite"?"幕门强敌驻守，胜利获得更多金币。":kind=="boss"?"击败顶层敌障，守住曜京。":kind=="camp"?"交班灯下休整：治疗、研习或守夜。":"击败全部敌障，获得金币与奖励。",
                        eventId=kind=="event"?events[MapNext(ref random)%events.Length].id:""});
                }
            }
            for(int floor=0;floor<AscentFloors-1;floor++)
            {
                var row=result.Where(n=>n.floor==floor).ToArray();var next=result.Where(n=>n.floor==floor+1).ToArray();
                foreach(var node in row)
                {
                    int nearest=(int)Math.Round(node.lane*(next.Length-1)/(double)(row.Length-1));
                    node.next.Add(next[nearest].id);
                    if(next.Length>1){int adjacent=nearest==0?1:nearest==next.Length-1?nearest-1:MapNext(ref random)%2==0?nearest-1:nearest+1;node.next.Add(next[adjacent].id);node.next.Sort(StringComparer.Ordinal);}
                }
            }
            return result;
        }
        static string[] TakeKinds(ref uint random,string[] pool,int count)
        {
            var copy=new List<string>(pool);
            for(int i=copy.Count-1;i>0;i--){int at=(int)(MapNext(ref random)%(uint)(i+1));string swap=copy[i];copy[i]=copy[at];copy[at]=swap;}
            return copy.Take(count).ToArray();
        }
        static uint MapNext(ref uint random){if(random==0)random=0x9e3779b9u;random^=random<<13;random^=random>>17;random^=random<<5;return random;}
        internal static int AscentEnemyCount(string kind){return kind=="boss"?4:kind=="elite"?3:2;}
        internal static int AscentEnemyCount(TacticalState s)
        {
            if(s.ascentRevision==0)return AscentEnemyCount(s.nodeKind);
            int count=s.nodeKind=="boss"?3:s.nodeKind=="elite"?s.floor>=5?4:3:s.floor>=7?4:s.floor>=3?3:2;
            if(s.ascentRevision>=3&&s.nodeKind=="battle"&&s.trial>=5)count=Math.Min(5,count+1);
            return count;
        }
        // P1 词缀：按种子确定性赋点（楼层/序号/试炼），校验端可用同一函数重建。
        internal static string EnemyAffixes(TacticalState s,int index)
        {
            if(s.ascentRevision<3||s.nodeKind=="boss")return "";
            var rank=TacticalStory.GetEnemy(AscentEnemyId(s,index)).rank;
            string[] pool=TacticalContent.AffixIds;
            string Pick(int salt){uint h=s.seed^((uint)(s.floor+1)*0x9e3779b9u)^(uint)(index+1)*0x85ebca6bu^(uint)salt*0xc2b2ae35u;h^=h>>15;return pool[h%pool.Length];}
            bool elite=rank=="elite";
            if(elite&&(s.floor>=8||s.trial>=3))return Pick(1);
            if(!elite&&index>0&&(s.floor>=5||s.trial>=3)){
                string first=Pick(1),second=Pick(2);
                if(first==second)second=pool[(Array.IndexOf(pool,second)+1)%pool.Length];
                return s.floor>=10?first+"+"+second:first;
            }
            return "";
        }
        internal static void CreateAscentEnemies(TacticalState s)
        {
            int[] xs={7,9,8,10,6},ys={3,5,7,8,2};
            for(int i=0;i<AscentEnemyCount(s);i++){
                string id=AscentEnemyId(s,i);var definition=TacticalStory.GetEnemy(id);
                int hp=AscentEnemyHp(s,i),attack=AscentEnemyAttack(s,i);
                if(i>=4)s.terrain.First(c=>c.x==xs[i]&&c.y==ys[i]).kind="plain";
                s.units.Add(new TacticalUnit{id="enemy-"+s.chapter+"-"+i,heroId=id,enemyType=definition.rank,name=definition.name+(i==0?"":" "+(i+1)),team="enemy",x=xs[i],y=ys[i],hp=hp,maxHp=hp,attackPower=attack,attackRange=definition.range,moveRange=definition.move,affixes=EnemyAffixes(s,i)});
            }
        }
        // 人物事件需要对应角色在场；小队缺人时确定性替换为普通事件（落闸货郎）。
        internal static string ExpectedEventId(TacticalState s,TacticalMapNode node)
        {
            string gate=TacticalStory.EventGate(node.eventId);
            return s.ascentRevision>=3&&!string.IsNullOrEmpty(gate)&&!s.squad.Contains(gate)?"vendor":node.eventId;
        }
        internal static string AscentEnemyId(TacticalState s,int index)
        {
            string[] ordinary={"sluice","loom","echo","seal","afterimage"},elites={"steamwarden","mirrorweaver","tidejudge"};
            if(s.nodeKind=="boss")return index==0?"sunwheel":index==1?"mirrorweaver":"tidejudge";
            if(s.nodeKind=="elite"&&index==0)return elites[s.chapter%elites.Length];
            return ordinary[s.floor<3?s.chapter:(s.chapter+index)%ordinary.Length];
        }
        internal static int AscentEnemyHp(TacticalState s,int index)
        {
            string id=AscentEnemyId(s,index);
            int hp=id=="sunwheel"?72:TacticalStory.GetEnemy(id).rank=="elite"?24+s.floor:14+s.floor;
            if(s.ascentRevision>=3){
                if(s.trial>=1)hp=hp*11/10;
                if(s.trial>=8)hp=hp*11/10;
                if(TacticalContent.HasAffix(new TacticalUnit{team="enemy",affixes=EnemyAffixes(s,index)},"overloaded"))hp=Math.Max(1,hp-5);
            }
            return hp;
        }
        internal static int AscentEnemyAttack(TacticalState s,int index)
        {
            string rank=TacticalStory.GetEnemy(AscentEnemyId(s,index)).rank;
            int attack=5+s.floor/3+(rank=="boss"?3:rank=="elite"?2:0);
            if(s.ascentRevision>=3){
                if(s.trial>=2)attack++;
                if(s.trial>=8)attack++;
                if(TacticalContent.HasAffix(new TacticalUnit{team="enemy",affixes=EnemyAffixes(s,index)},"overloaded"))attack+=3;
            }
            return attack;
        }
        public static int EnemyAttackRange(TacticalState s,TacticalUnit enemy)
        {return enemy.attackRange>0?enemy.attackRange:enemy.heroId=="loom"||enemy.heroId=="seal"?2:1;}
        public static int EnemyAttackPower(TacticalState s,TacticalUnit enemy)
        {return enemy.attackPower>0?enemy.attackPower:5+s.chapter+(s.nodeKind=="elite"?2:0);}
        public static int EnemyMoveBudget(TacticalState s,TacticalUnit enemy)
        {int budget=enemy.moveRange>0?enemy.moveRange:BoardWidth(s)==8?2:3;return s.ascentRevision>=2&&TacticalContent.HasAffix(enemy,"swift")?budget+1:budget;}
        public static int EnemySplashRadius(TacticalState s,TacticalUnit enemy)
        {
            if(s.ascentRevision>=2&&TacticalContent.HasAffix(enemy,"spreading"))return 1;
            return enemy.heroId=="steamwarden"&&s.round%3==0||enemy.heroId=="sunwheel"&&s.round%2==0?1:0;
        }
        public static string EnemyInflicts(TacticalState s,TacticalUnit enemy)
        {return enemy.heroId=="mirrorweaver"?"weakened":enemy.heroId=="tidejudge"?"rooted":enemy.heroId=="sunwheel"&&s.round%2==0?"vulnerable":"";}
        public static string EnemyActionName(TacticalState s,TacticalUnit enemy)
        {
            if(enemy.heroId=="steamwarden")return s.round%3==0?"炉阀震荡 · 目标及邻格受到冲击":"重阀锤击";
            if(enemy.heroId=="mirrorweaver")return "折光蚀心 · 攻击并施加虚弱";
            if(enemy.heroId=="tidejudge")return "沉潮枷锁 · 攻击并定身";
            if(enemy.heroId=="sunwheel")return s.round%2==0?"曜轮横扫 · 范围伤害并施加易伤":"天阙灼线";
            return "构装攻击";
        }
        public static void PrepareEnemyAction(TacticalState s,TacticalUnit enemy)
        {if(enemy.heroId=="sunwheel"&&s.round%3==0)enemy.block=Math.Min(40,enemy.block+8);}
        public static int ShopRerollCost(TacticalState s){return 15+5*s.shopRerolls;}
        static TacticalPreview AscentPreview(TacticalState s,TacticalRequest request,TacticalPreview preview)
        {
            if(s.journeyMode!="ascent")return Denied(preview,"当前旅程没有层级地图。");
            if(request.type=="node")return Allowed(AvailableNodes(s).Any(n=>n.id==request.choice),preview,"只能选择相邻且尚未经过的路线节点。");
            if(request.type=="leaveShop")return Allowed(s.phase=="shop",preview,"当前不在商店。");
            if(request.type=="rerollShop"){
                if(s.phase!="shop")return Denied(preview,"当前商店不能重随技能。");
                preview.resource="gold";preview.cost=ShopRerollCost(s);
                if(s.shopRerolls>=20)return Denied(preview,"本店已重随二十次，请前往下一处商店。");
                return Allowed(s.gold>=preview.cost,preview,"金币不足，重随需要 "+preview.cost+" 金币。");
            }
            var offer=s.shopOffers.FirstOrDefault(o=>o.id==request.choice);
            if(s.phase!="shop"||offer==null)return Denied(preview,"请选择当前商店的一项商品。");
            preview.resource="gold";preview.cost=offer.price;
            if(offer.sold)return Denied(preview,"这件商品已售罄。");
            if(s.gold<offer.price)return Denied(preview,"金币不足，需要 "+offer.price+" 金币。");
            if(offer.kind=="relic"&&s.relics.Contains(offer.relicId))return Denied(preview,"已经拥有这件旧物。");
            if(offer.kind=="relic"&&!RelicAllowedForParty(s,offer.relicId))return Denied(preview,"这件专属旧物的主人未出战，可重随技能或离开商店。");
            if(offer.kind=="skill"&&!CanAcquireSkill(s,offer.skillId))return Denied(preview,"对应同行者未出战或已达到领悟上限，可重随技能或离开商店。");
            if(offer.kind=="heal"&&s.units.Where(u=>u.team=="hero").All(u=>u.hp==u.maxHp))return Denied(preview,"同行者的生命已经恢复满了。");
            return Allowed(true,preview,null);
        }
        static void EnterAscentNode(TacticalState s,string id)
        {
            var node=s.mapNodes.First(n=>n.id==id);node.visited=true;s.floor=node.floor;s.currentNodeId=node.id;s.chapter=node.chapter;
            s.nodeKind=node.kind;s.eventId=ExpectedEventId(s,node);s.sideBattle=node.kind!="boss";s.shopRerolls=0;s.shopRevision=node.kind=="shop"?4:0;s.shopOffers.Clear();
            AddLog(s,"第 "+(node.floor+1)+" 层："+node.name+"。");
            if(node.kind=="battle"||node.kind=="elite"||node.kind=="boss"){SetupBattle(s,node.kind!="boss");s.phase="battle";}
            else {s.units.RemoveAll(u=>u.team=="enemy");s.objects.Clear();s.phase=node.kind;if(node.kind=="shop")s.shopOffers=CreateShopOffers(s,node);}
        }
        static void CompleteAscentNode(TacticalState s)
        {
            var node=CurrentNode(s);node.completed=true;s.shopOffers.Clear();s.shopRerolls=0;s.shopRevision=0;s.eventId="";s.rewards.Clear();s.routes.Clear();s.units.RemoveAll(u=>u.team=="enemy");s.objects.Clear();
            s.phase=node.kind=="boss"?"victory":"map";
            if(s.phase=="victory")AddLog(s,"顶层敌障散去。伙伴们并肩而立，曜京重归安宁。");
        }
        internal static List<TacticalShopOffer> CreateShopOffers(TacticalState s,TacticalMapNode node,bool originalShelf=false)
        {
            bool partyShelf=!originalShelf&&(s.shopRevision==4||s.ascentRevision>=4);
            if(s.ascentRevision==0&&!partyShelf)return CreateLegacyShopOffers(s,node);
            bool depth=s.ascentRevision>=2;
            int skillPrice=depth?45:40,relicPrice=depth?60:55,healPrice=depth?30:25;
            string healText=depth?"全队恢复 30% 生命。":"全队恢复 10 生命。";
            uint random=s.seed^((uint)(node.floor+1)*0x9e3779b9u)^(uint)(node.lane+1)*0x85ebca6bu;
            var relics=AvailableRelicCatalogue(s).Where(r=>(s.ascentRevision<3||r.rarity!="cursed")&&(!partyShelf||RelicAllowedForParty(s,r.id))).ToArray();
            var relic=relics[MapNext(ref random)%relics.Length];
            random^=(uint)s.shopRerolls*0xc2b2ae35u;
            var origins=partyShelf?s.squad.ToList():new[]{"sixuan","lingfeng","cangling","yanzhuying","shangshuo"}.ToList();
            for(int i=origins.Count-1;i>0;i--){int at=(int)(MapNext(ref random)%(uint)(i+1));string swap=origins[i];origins[i]=origins[at];origins[at]=swap;}
            var result=new List<TacticalShopOffer>{
                new TacticalShopOffer{id=node.id+"-relic",kind="relic",name=relic.name,text=relic.text,relicId=relic.id,skillId="",price=relicPrice},
                new TacticalShopOffer{id=node.id+"-heal",kind="heal",name="同行补给",text=healText,relicId="",skillId="",price=healPrice}
            };
            for(int i=0;i<3;i++){
                string origin=origins[i];var choices=AvailableSkillCatalogue(s).Where(k=>k.heroId==origin&&k.resource!="charge"&&k.kind!="passive").ToArray();
                var skill=choices[MapNext(ref random)%choices.Length];
                result.Add(new TacticalShopOffer{id=node.id+"-skill-"+s.shopRerolls+"-"+i,kind="skill",heroId=origin,name=skill.name,text=skill.text+(partyShelf?" 自动由"+TacticalContent.GetHero(origin).name+"领悟；已掌握时升一级，最高三级。":" 可指定同行者领悟；已掌握时升一级，最高三级。"),relicId="",skillId=skill.id,price=skillPrice});
            }
            if(s.ascentRevision>=3){
                // 试炼 4 提价两成；热网图与锈怀表修正物价。
                // 当前货架已标价；本店刚买到的折价旧物从下一家店生效，不能改变已保存价格。
                bool BoughtHere(string id)=>s.shopOffers.Any(o=>o.kind=="relic"&&o.relicId==id&&o.sold);
                int percent=100+(s.trial>=4?20:0)+(s.relics.Contains("rustwatch")&&!BoughtHere("rustwatch")?25:0)-(s.relics.Contains("hotnetmap")&&!BoughtHere("hotnetmap")?15:0);
                foreach(var offer in result)offer.price=Math.Max(1,offer.price*percent/100);
            }
            return result;
        }
        internal static void RerollAscentShop(TacticalState s)
        {
            int cost=ShopRerollCost(s);var retained=s.shopOffers.Where(o=>o.kind!="skill").ToDictionary(o=>o.kind);
            s.gold-=cost;s.shopRerolls++;s.shopRevision=4;var fresh=CreateShopOffers(s,CurrentNode(s));
            for(int i=0;i<fresh.Count;i++)if(retained.TryGetValue(fresh[i].kind,out var previous))fresh[i]=previous;
            s.shopOffers=fresh;
            AddLog(s,"花费 "+cost+" 金币重随三项技能；旧物与补给售罄状态保留。");
        }
        // Old v3 shelves were generated from this exact catalogue. Preserve their text and order
        // rather than rebuilding them from the expanded character skill trees.
        static readonly TacticalSkill[] LegacyShopSkills={
            LegacyShopSkill("strike","破障","近身造成 8 伤害；高地增伤 2。"),
            LegacyShopSkill("throw","投掷旧物","抛出邻接木箱或热罐，对 3 格内敌障造成 7 伤害；热罐波及邻格敌障。"),
            LegacyShopSkill("brace","护人","为自身或邻接同伴提供 6 护盾。"),
            LegacyShopSkill("shove","逼退","造成 4 伤害并击退 1 格；撞墙或单位追加 4 伤害。"),
            LegacyShopSkill("step","借影","移动至 3 格内空位；起点或落点须在阴影，抵达阴影恢复 1 SP。","yanzhuying"),
            LegacyShopSkill("balance","衡域","3 格内造成 8 伤害并定身一轮；目标须可见。","sixuan"),
            LegacyShopSkill("moon","缺月针","邻接敌障受到 10 伤害；施术者在阴影中额外造成 3 伤害。","yanzhuying"),
            LegacyShopSkill("cleave","赤心横刀","近身造成 10 伤害并击退 1 格。","lingfeng"),
            LegacyShopSkill("tide","静海界","3 格内造成 6 伤害并击退 1 格；自身须在水雾中。","cangling"),
            LegacyShopSkill("anchor","镇渊界","自身及邻接同伴获得 9 护盾。","shangshuo"),
            LegacyShopSkill("stitch","缝夜针","为 3 格内同伴恢复 8 生命；不复活战斗中倒下者。","ruanzhuo"),
            LegacyShopSkill("line","穿隙","3 格内造成 7 伤害。"),
            LegacyShopSkill("mend","温汤","为 2 格内同伴恢复 6 生命。"),
            LegacyShopSkill("burst","震管","2 格内目标及邻接敌障各受到 7 伤害。"),
            LegacyShopSkill("hook","牵线","3 格内造成 4 伤害并击退 2 格。"),
            LegacyShopSkill("haste","换班","为 2 格内同伴恢复 1 AP；每两轮可用一次。"),
            LegacyShopSkill("ward","留灯","3 格内同伴获得 8 护盾。"),
            LegacyShopSkill("oath","三息合誓","蓄势达到 100 后，对 3 格内敌障及邻接敌障造成 18 伤害。")
        };
        static TacticalSkill LegacyShopSkill(string id,string name,string text,string heroId="")
        {return new TacticalSkill{id=id,name=name,text=text,heroId=heroId};}
        static List<TacticalShopOffer> CreateLegacyShopOffers(TacticalState s,TacticalMapNode node)
        {
            uint random=s.seed^((uint)(node.floor+1)*0x9e3779b9u)^(uint)(node.lane+1)*0x85ebca6bu;
            var relics=AvailableRelicCatalogue(s).ToArray();var relic=relics[MapNext(ref random)%relics.Length];
            var skills=LegacyShopSkills.Where(k=>string.IsNullOrEmpty(k.heroId)||s.squad.Contains(k.heroId)).ToArray();var skill=skills[MapNext(ref random)%skills.Length];
            return new List<TacticalShopOffer>{
                new TacticalShopOffer{id=node.id+"-relic",kind="relic",name=relic.name,text=relic.text,relicId=relic.id,skillId="",price=55},
                new TacticalShopOffer{id=node.id+"-skill",kind="skill",name=skill.name,text=skill.text+" 已学时提升一级。",relicId="",skillId=skill.id,price=40},
                new TacticalShopOffer{id=node.id+"-heal",kind="heal",name="同行补给",text="全队恢复 10 生命。",relicId="",skillId="",price=25}
            };
        }
        static void BuyAscentOffer(TacticalState s,TacticalRequest request)
        {
            var offer=s.shopOffers.First(o=>o.id==request.choice);s.gold-=offer.price;offer.sold=true;
            if(offer.kind=="relic")GrantRelic(s,offer.relicId);else if(offer.kind=="skill")Learn(SkillRecipient(s,offer.skillId),offer.skillId);
            else if(s.ascentRevision>=2)HealSquadPercent(s,30);else HealSquad(s,10);
            AddLog(s,"购买「"+offer.name+"」，花费 "+offer.price+" 金币。");
        }
        internal static bool ValidAscent(TacticalState s)
        {
            if(s.ascentRevision<0||s.ascentRevision>4||s.shopRerolls<0||s.shopRerolls>20||s.ascentRevision==0&&s.shopRevision==0&&s.shopRerolls!=0||s.phase!="shop"&&(s.shopRerolls!=0||s.shopRevision!=0)||s.shopRevision!=0&&s.shopRevision!=4||s.phase=="shop"&&s.ascentRevision>=4&&s.shopRevision!=4||s.floor< -1||s.floor>=AscentFloors||s.gold<0||s.gold>100000||!new[]{"map","battle","reward","shop","event","camp","victory","defeat"}.Contains(s.phase)||s.squad==null||s.mapNodes.Any(n=>n==null))return false;
            var expected=CreateAscentGraph(s.seed,s.ascentRevision<1?1:s.ascentRevision);if(s.mapNodes.Count!=expected.Count)return false;
            for(int i=0;i<expected.Count;i++)
            {
                var node=s.mapNodes[i];var original=expected[i];
                bool validEvent=node.eventId==original.eventId;
                if(node.id!=original.id||node.kind!=original.kind||node.name!=original.name||node.text!=original.text||!validEvent||node.floor!=original.floor||node.lane!=original.lane||node.chapter!=original.chapter||node.next==null||!node.next.SequenceEqual(original.next)||node.completed&&!node.visited)return false;
            }
            var visits=s.mapNodes.Where(n=>n.visited).OrderBy(n=>n.floor).ToArray();
            if(s.floor==-1)return s.phase=="map"&&s.currentNodeId==""&&visits.Length==0&&s.shopOffers.Count==0&&s.chapter==0&&s.nodeKind=="main"&&s.eventId==""&&!s.sideBattle;
            var current=CurrentNode(s);
            if(current==null||!current.visited||current.floor!=s.floor||current.chapter!=s.chapter||current.kind!=s.nodeKind||visits.Length!=s.floor+1||s.sideBattle!=(current.kind!="boss"))return false;
            for(int i=0;i<visits.Length;i++)if(visits[i].floor!=i||i<visits.Length-1&&(!visits[i].completed||!visits[i].next.Contains(visits[i+1].id)))return false;
            if(current.completed!=(s.phase=="map"||s.phase=="victory")||s.phase=="victory"&&(current.kind!="boss"||s.floor!=AscentFloors-1)||s.phase=="map"&&current.kind=="boss")return false;
            if((s.phase=="battle"||s.phase=="reward"||s.phase=="defeat")&&!new[]{"battle","elite","boss"}.Contains(current.kind)||s.phase=="shop"&&current.kind!="shop"||s.phase=="event"&&current.kind!="event"||s.phase=="camp"&&current.kind!="camp")return false;
            if(s.eventId!=(s.phase=="event"?ExpectedEventId(s,current):""))return false;
            if(s.phase!="shop")return s.shopOffers.Count==0;
            var stock=CreateShopOffers(s,current);if(s.shopOffers.Count!=stock.Count)return false;
            var legacySupplies=s.shopRevision==4&&s.ascentRevision<4&&s.shopRerolls>0?CreateShopOffers(s,current,true).Where(o=>o.kind!="skill").ToArray():Array.Empty<TacticalShopOffer>();
            for(int i=0;i<stock.Count;i++){
                var offer=s.shopOffers[i];var original=stock[i];if(offer==null||offer.kind!=original.kind)return false;
                if(!SameOfferIdentity(offer,original))original=legacySupplies.FirstOrDefault(o=>SameOfferIdentity(offer,o));
                if(original==null||!CompatibleOfferPresentation(s,offer,original))return false;
            }
            return true;
        }
        static bool SameOfferIdentity(TacticalShopOffer saved,TacticalShopOffer expected)
        {return saved.id==expected.id&&saved.kind==expected.kind&&saved.skillId==expected.skillId&&saved.relicId==expected.relicId&&saved.heroId==expected.heroId&&saved.price==expected.price;}
        static bool CompatibleOfferPresentation(TacticalState s,TacticalShopOffer saved,TacticalShopOffer expected)
        {
            // Revision 3 display strings are cached prose, never the identity or effect of an offer.
            // Their IDs, origin, price and shelf position are still matched against seeded stock above.
            if(s.ascentRevision>=3||s.shopRevision==4)return !string.IsNullOrEmpty(saved.name)&&saved.name.Length<=80&&!string.IsNullOrEmpty(saved.text)&&saved.text.Length<=5000;
            return saved.name==expected.name&&CompatibleOfferText(s,saved,expected);
        }
        // Called only after the complete checkpoint passed validation. Refresh presentation,
        // preserving every saved offer ID, price, sold flag and all progression fields.
        public static void RefreshShopOfferPresentation(TacticalState s)
        {
            if(s.phase!="shop"||s.ascentRevision<3&&s.shopRevision!=4)return;
            foreach(var offer in s.shopOffers){
                if(offer.kind=="relic"){var relic=TacticalContent.GetRelic(offer.relicId);offer.name=relic.name;offer.text=relic.text;}
                else if(offer.kind=="skill"){var skill=TacticalContent.GetSkill(offer.skillId);offer.name=skill.name;offer.text=skill.text+(s.shopRevision==4?" 自动由"+TacticalContent.GetHero(skill.heroId).name+"领悟；已掌握时升一级，最高三级。":" 可指定同行者领悟；已掌握时升一级，最高三级。");}
                else {offer.name="同行补给";offer.text=s.ascentRevision>=2?"全队恢复 30% 生命。":"全队恢复 10 生命。";}
            }
        }
        static bool CompatibleOfferText(TacticalState s,TacticalShopOffer saved,TacticalShopOffer expected)
        {
            if(saved.text==expected.text)return true;
            // 0.19 与 P0 仅改写过这三条沧泠说明；旧货架正文原样保存，数值仍逐项校验。
            if(s.ascentRevision!=1||saved.kind!="skill")return false;
            string text=saved.skillId=="tide"?"水雾中压平震动，对目标及邻接敌障造成伤害并削弱一轮。":saved.skillId=="undertow"?"水雾中束住远处敌障并造成伤害。":saved.skillId=="deepcurrent"?"攻击目标及邻接敌障，恢复自身SP。":null;
            return text!=null&&saved.text==text+" 可指定同行者领悟；已掌握时升一级，最高三级。";
        }
    }
}
