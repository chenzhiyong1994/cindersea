using System;
using System.Linq;
using Dicebound.Tactics;
using Dicebound.Persistence;
using Dicebound.Presentation;

class EventChecks
{
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static void Main(string[] args)
    {
        if(args.Contains("--battle-feedback")){BattleFeedbackChecks();Console.WriteLine("PASS: "+checks+" focused battle-feedback assertions.");return;}
        if(args.Contains("--content-edge")){ContentEdgeChecks();Console.WriteLine("PASS: "+checks+" focused content-edge assertions.");return;}
        var original=TacticalRules.NewAscent(173,new[]{"sixuan","lingfeng","cangling"},1);
        // Values from the actual 0.19 Player checkpoint, 20261002-053207060.
        Check(original.mapNodes.Where(n=>n.kind=="event").Select(n=>n.eventId).SequenceEqual(new[]{"water","dream","dream","witness","witness"}),"seed 173 retains the event ids from its original 0.19 Player route");
        Check(TacticalCodec.TryDecode(TacticalCodec.Encode(original),out _,out _),"original route round trips");
        var depth=TacticalRules.NewAscent(173,new[]{"sixuan","lingfeng","cangling"},2);
        var encounter=depth.mapNodes.First(n=>n.kind=="event");string savedEvent=encounter.eventId;
        Check(TacticalCodec.TryDecode(TacticalCodec.Encode(depth),out var restored,out _)&&restored.mapNodes.First(n=>n.id==encounter.id).eventId==savedEvent,"initial revision-2 event draw survives the expanded catalogue unchanged");
        encounter.eventId="unknown";Check(!TacticalValidation.Valid(depth),"unknown saved event is rejected");
        encounter.eventId="vendor";encounter.next.Clear();Check(!TacticalValidation.Valid(depth),"event compatibility does not permit changed route edges");
        CounterChecks();
        AllEvents();
        TradeoffChecks();
        ReceiptChecks();
        BattleRuleChecks();
        SettlementReceipts();
        RiskChecks();
        HealingAndTerrainChecks();
        CursedRelicChecks();
        SkillKillChecks();
        DeathTransitionChecks();
        ContentEdgeChecks();
        BattleFeedbackChecks();
        Console.WriteLine("PASS: "+checks+" event and outcome assertions.");
    }
    static void CounterChecks()
    {
        var state=TacticalRules.NewAscent(71,new[]{"sixuan","lingfeng","cangling"},1);
        state=TacticalRules.Act(state,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(state)[0].id});
        state.terrain.ForEach(c=>c.kind="plain");state.objects.Clear();
        var target=state.units.First(u=>u.team=="hero");target.x=3;target.y=3;
        var enemy=state.units.First(u=>u.team=="enemy");enemy.x=4;enemy.y=3;
        foreach(var hero in state.units.Where(u=>u.team=="hero"))hero.block=40;
        foreach(string id in state.squad)state=TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId=id});
        Check(state.units.Where(u=>u.team=="enemy").All(u=>u.affixCounter==0),"ordinary old enemies never accumulate a revision-2 affix counter");
        Check(TacticalValidation.Valid(state),"an old journey remains saveable after enemies attack");
        state.ascentRevision=3;
        for(int round=1;round<=4;round++)
        {
            foreach(var hero in state.units.Where(u=>u.team=="hero")){hero.hp=hero.maxHp;hero.block=40;hero.x=hero.id==target.id?3:1;hero.y=hero.id==target.id?3:hero.id=="lingfeng"?1:2;}
            enemy=state.units.First(u=>u.team=="enemy");enemy.x=4;enemy.y=3;enemy.affixes="binding";
            foreach(string id in state.squad)state=TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId=id});
            Check(state.units.First(u=>u.id==enemy.id).affixCounter==round%3,"binding attacks use a bounded three-attack cycle, round "+round);
            Check(TacticalRules.FindUnit(state,target.id).rooted==(round==3),"binding roots only on its third attack, round "+round);
        }
    }
    static TacticalState Event(string id,int revision=3)
    {
        string gate=TacticalStory.EventGate(id);
        var squad=string.IsNullOrEmpty(gate)?new[]{"sixuan","lingfeng","cangling"}:new[]{gate,"sixuan","lingfeng","cangling"}.Distinct().Take(3).ToArray();
        for(uint seed=1;seed<500;seed++)
        {
            var state=TacticalRules.NewAscent(seed,squad,revision);var node=state.mapNodes.FirstOrDefault(n=>n.kind=="event"&&n.eventId==id);if(node==null)continue;
            var current=node;
            while(current.floor>0){current=state.mapNodes.First(n=>n.floor==current.floor-1&&n.next.Contains(current.id));current.visited=true;current.completed=true;}
            node.visited=true;state.floor=node.floor;state.chapter=node.chapter;state.currentNodeId=node.id;state.nodeKind=node.kind;state.eventId=id;state.sideBattle=true;state.phase="event";state.gold=100;
            foreach(var unit in state.units)unit.hp=20;
            Check(TacticalValidation.Valid(state),"valid event fixture "+id+" rev "+revision);return state;
        }
        throw new Exception("Could not locate generated event "+id);
    }
    static TacticalState RoundTrip(TacticalState state)
    {string encoded=TacticalCodec.Encode(state);Check(TacticalCodec.TryDecode(encoded,out var restored,out _)&&TacticalCodec.Encode(restored)==encoded,"event outcome round-trips exactly");return restored;}
    static void AllEvents()
    {
        foreach(var story in TacticalStory.Events)
        {
            var state=Event(story.id);string encoded=TacticalCodec.Encode(state);var view=TacticalRules.CurrentEvent(state);
            Check(view.choices.Count>=2&&view.choices.Count<=3,"events offer two or three visible choices: "+story.id);
            Check(view.choices.All(c=>!string.IsNullOrEmpty(c.title)&&!string.IsNullOrEmpty(c.description)),"every choice states its terms");
            Check(TacticalCodec.Encode(state)==encoded,"opening event details never draws RNG or changes state");
            foreach(var option in view.choices)
            {
                var request=new TacticalRequest{type="event",choice=option.id};var preview=TacticalRules.Preview(state,request);
                Check(preview.ok==option.available,"UI availability matches execution for "+story.id+" "+option.id);
                Check(preview.ok,"fresh fixture can take every offered choice");
                var after=RoundTrip(TacticalRules.Act(state,request));
                Check(after.phase=="map"&&TacticalRules.CurrentNode(after).completed,"event settles and returns to the route once");
                Check(!TacticalRules.Preview(after,request).ok,"resolved event cannot be claimed twice");
                bool rejected=false;try{TacticalRules.Act(after,request);}catch(InvalidOperationException){rejected=true;}Check(rejected,"repeat Act is rejected too");
                var receipt=TacticalRules.DescribeOutcome(state,after,request);
                Check(receipt!=null&&receipt.entries.Count>0,"each event has immediate concrete feedback");
                Check(TacticalCodec.Encode(state)==encoded,"settlement never mutates the previous checkpoint");
                Check(TacticalCodec.Encode(TacticalRules.Act(state,request))==TacticalCodec.Encode(after),"saved RNG gives deterministic event outcomes");
            }
        }
        foreach(int revision in new[]{1,2})foreach(string id in new[]{"witness","dream","water"})
        {
            var state=Event(id,revision);Check(TacticalRules.CurrentEvent(state).choices.Count==2,"old events retain two options");
            var healed=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="event",choice="A"}));
            Check(healed.units.All(u=>u.hp==24)&&healed.gold==100,"old event A remains free 4 HP healing");
            var gift=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="event",choice="B"}));
            Check(gift.relics.Count==1&&gift.units.All(u=>u.hp==20),"old event B remains a free relic without exertion");
        }
    }
    static void TradeoffChecks()
    {
        var state=Event("vendor");state.units[0].hp=2;
        var after=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="event",choice="A"}));
        Check(after.gold==145&&after.units[0].hp==1&&after.units.Skip(1).All(u=>u.hp==16),"pushing the cart trades capped 4 HP for exactly 45 gold");
        var choice=new TacticalRequest{type="event",choice="C"};state.gold=19;string encoded=TacticalCodec.Encode(state);
        var denied=TacticalRules.Preview(state,choice);Check(!denied.ok&&denied.cost==20&&denied.resource=="gold"&&denied.reason.Contains("金币不足"),"unaffordable event identifies its exact price");
        Check(!TacticalRules.CurrentEvent(state).choices.Single(c=>c.id=="C").available,"unaffordable event UI is disabled consistently");
        bool rejected=false;try{TacticalRules.Act(state,choice);}catch(InvalidOperationException){rejected=true;}Check(rejected&&TacticalCodec.Encode(state)==encoded,"poor event leaves checkpoint and RNG untouched");
        state.gold=20;after=RoundTrip(TacticalRules.Act(state,choice));Check(after.gold==0&&after.relics.Count==1,"exact-price trade works without negative gold");
        state=Event("pearlecho");after=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="event",choice="A"}));
        Check(after.pendingShield==5&&after.pendingCharge==10,"listening to the tide grants actual preparation instead of fake map information");
        state=Event("oldxu");after=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="event",choice="A"}));
        Check(after.gold==90&&after.pendingShield==4&&after.pendingCharge==10,"paid intelligence grants its advertised shield and charge");
        state=Event("dream");foreach(var hero in state.units){foreach(var skill in TacticalContent.Skills.Where(k=>TacticalContent.CanEquip(hero,k)&&k.id!="oath")){if(hero.skills.Count==12)break;if(!hero.skills.Contains(skill.id))hero.skills.Add(skill.id);}hero.skillLevels=hero.skills.ToDictionary(k=>k,k=>3);}
        Check(!TacticalRules.Preview(state,new TacticalRequest{type="event",choice="C"}).ok,"full skill builds cannot pay for a no-op study");
        state=Event("genealogy");var lingfeng=state.units.First(u=>u.id=="lingfeng");if(!lingfeng.skills.Contains("ignite"))lingfeng.skills.Add("ignite");lingfeng.skillLevels["ignite"]=3;
        Check(!TacticalRules.Preview(state,new TacticalRequest{type="event",choice="A"}).ok,"maxed genealogy cannot claim a phantom upgrade");
        state=Event("nailreturn");state.relics.AddRange(TacticalContent.Relics.Select(r=>r.id));
        Check(TacticalRules.CurrentEvent(state).choices[0].description.Contains("改为"),"exhausted relic pool discloses its supply fallback before selection");
        var fallbackRequest=new TacticalRequest{type="event",choice="A"};after=RoundTrip(TacticalRules.Act(state,fallbackRequest));
        Check(after.relics.Count==state.relics.Count&&TacticalRules.DescribeOutcome(state,after,fallbackRequest).entries.All(e=>e.kind!="relic"),"exhausted relic gift reports healing and never invents an item");
    }
    static void ReceiptChecks()
    {
        var state=Event("witness");foreach(var unit in state.units)unit.hp=unit.maxHp;state.units[0].hp--;
        var request=new TacticalRequest{type="event",choice="A"};var after=RoundTrip(TacticalRules.Act(state,request));var receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(receipt.entries.Count==1&&receipt.entries[0].kind=="health"&&receipt.entries[0].amount==1,"receipt shows actual capped healing, not the nominal four");
        state=Event("dream");request=new TacticalRequest{type="event",choice="C"};after=RoundTrip(TacticalRules.Act(state,request));receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(receipt.entries.Single(e=>e.kind=="gold").amount==-20&&receipt.entries.Count(e=>e.kind=="skill")==1,"paid study receipt identifies its exact price and real learned skill");
        var learned=receipt.entries.Single(e=>e.kind=="skill");Check(learned.title.Contains(TacticalRules.FindUnit(after,learned.unitId).name)&&learned.detail.Contains("Lv."),"skill feedback names its recipient and resulting level");
        state=Event("vendor");request=new TacticalRequest{type="event",choice="B"};after=RoundTrip(TacticalRules.Act(state,request));receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(receipt.entries.Single().kind=="notice","safe departure does not invent a reward");
        Check(TacticalRules.DescribeOutcome(state,state,request)==null,"uncommitted state cannot produce a receipt");
    }
    static TacticalState Battle()
    {
        var state=TacticalRules.NewAscent(71,new[]{"shangshuo","sixuan","cangling"},3);
        state=TacticalRules.Act(state,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(state)[0].id});
        state.terrain.ForEach(c=>c.kind="plain");state.objects.Clear();return state;
    }
    static TacticalState FinishRound(TacticalState state)
    {foreach(string id in state.squad)state=TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId=id});return state;}
    static void BattleRuleChecks()
    {
        var state=Battle();var shangshuo=TacticalRules.FindUnit(state,"shangshuo");state.terrain.First(c=>c.x==shangshuo.x&&c.y==shangshuo.y).kind="pipe";
        foreach(var enemy in state.units.Where(u=>u.team=="enemy"))enemy.rooted=true;
        var exposed=FinishRound(state);state.relics.Add("rubberboots");var protectedState=FinishRound(state);
        Check(TacticalRules.FindUnit(exposed,"shangshuo").hp==shangshuo.hp-2&&TacticalRules.FindUnit(protectedState,"shangshuo").hp==shangshuo.hp,"rubber boots prevent actual end-round hot-pipe damage");
        Check(TacticalValidation.Valid(protectedState),"rubber-boot protection leaves a saveable checkpoint");
        state=Battle();shangshuo=TacticalRules.FindUnit(state,"shangshuo");shangshuo.x=7;shangshuo.y=1;
        var nearby=TacticalRules.FindUnit(state,"sixuan");nearby.x=2;nearby.y=4;
        var target=state.units.First(u=>u.team=="enemy");target.x=3;target.y=4;target.tauntedBy=shangshuo.id;
        var intent=TacticalRules.EnemyIntents(state).First(i=>i.unitId==target.id);
        Check(intent.targetId==shangshuo.id&&intent.path.Count>0&&Math.Abs(intent.x-shangshuo.x)+Math.Abs(intent.y-shangshuo.y)<7,"taunted enemy moves toward the forced target instead of retaining a nearby hero's path field");
        shangshuo.skills.Add("bastion");shangshuo.skillLevels["bastion"]=1;shangshuo.block=20;
        var passive=new TacticalRequest{type="skill",unitId=shangshuo.id,skillId="bastion",x=target.x,y=target.y};
        Check(!TacticalRules.Preview(state,passive).ok&&TacticalRules.Preview(state,passive).reason.Contains("被动"),"passive cannot be cast as a zero-cost attack");
        for(int level=1;level<=3;level++){shangshuo.skillLevels["bastion"]=level;Check(TacticalRules.BastionRetaliationBonus(shangshuo)==level+3,"passive upgrades really change the current retaliation bonus");}
        string description=TacticalRules.SkillDescription(state,shangshuo,"bastion");Check(description.Contains("常驻生效")&&description.Contains("30%")&&!description.Contains("基础伤害"),"passive detail reports the correct level-dependent effect");
        description=TacticalRules.SkillDescription(state,nearby,"triplebalance");Check(description.Contains("施术者蓄势 +15")&&!description.Contains("额外护盾"),"three balances describes charge instead of phantom shield");
        description=TacticalRules.SkillDescription(state,shangshuo,"stilldepth");Check(description.Contains("每清除一种负面状态获得 4 护盾"),"cleanse detail exposes shield per cleared condition");
        state=Battle();shangshuo=TacticalRules.FindUnit(state,"shangshuo");shangshuo.block=20;
        foreach(string skill in new[]{"bastion","bulwark"}){if(!shangshuo.skills.Contains(skill))shangshuo.skills.Add(skill);shangshuo.skillLevels[skill]=1;}
        nearby=TacticalRules.FindUnit(state,"sixuan");
        for(int level=1;level<=3;level++)
        {
            shangshuo.skillLevels["bastion"]=level;
            var fortified=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=shangshuo.id,skillId="bulwark",x=nearby.x,y=nearby.y});
            Check(TacticalRules.FindUnit(fortified,nearby.id).retaliation==9+level,"bastion level "+level+" changes actual granted retaliation");
        }
        foreach(int mask in new[]{0,1,5})
        {
            state=Battle();state.relics.Add("whistle");nearby=TacticalRules.FindUnit(state,"sixuan");target=state.units.First(u=>u.team=="enemy");target.x=2;target.y=2;target.comboMask=mask;
            var strike=new TacticalRequest{type="skill",unitId=nearby.id,skillId="strike",x=target.x,y=target.y};int expected=mask==0?8:mask==1?11:13;
            var preview=TacticalRules.Preview(state,strike);var hit=TacticalRules.Act(state,strike);
            Check(preview.ok&&preview.damage==expected&&target.hp-TacticalRules.FindUnit(hit,target.id).hp==expected,"whistle adds one damage to each reached combo tier, mask "+mask);
        }
    }
    static TacticalState Noncombat(string kind)
    {
        for(uint seed=1;seed<100;seed++)
        {
            var state=TacticalRules.NewAscent(seed,new[]{"sixuan","lingfeng","cangling"},3);var node=state.mapNodes.FirstOrDefault(n=>n.kind==kind);if(node==null)continue;
            var parent=state.mapNodes.First(n=>n.floor==node.floor-1&&n.next.Contains(node.id));var current=parent;current.visited=current.completed=true;
            while(current.floor>0){current=state.mapNodes.First(n=>n.floor==current.floor-1&&n.next.Contains(current.id));current.visited=current.completed=true;}
            state.floor=parent.floor;state.chapter=parent.chapter;state.currentNodeId=parent.id;state.nodeKind=parent.kind;state.sideBattle=parent.kind!="boss";state.gold=500;foreach(var u in state.units)u.hp=10;
            return RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="node",choice=node.id}));
        }
        throw new Exception("No node of kind "+kind);
    }
    static void SettlementReceipts()
    {
        var state=Noncombat("shop");var offer=state.shopOffers.First(o=>o.kind=="relic");var request=new TacticalRequest{type="buy",choice=offer.id};
        var after=RoundTrip(TacticalRules.Act(state,request));var receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(receipt.entries.Single(e=>e.kind=="gold").amount==-offer.price&&receipt.entries.Single(e=>e.kind=="relic").id==offer.relicId,"shop receipt uses actual saved price and exact relic identity");
        state=after;request=new TacticalRequest{type="rerollShop"};after=RoundTrip(TacticalRules.Act(state,request));receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(receipt.entries.Single().amount==-15&&receipt.title.Contains("重随"),"reroll receipt reports the actual paid cost");
        state=Noncombat("camp");state.relics.Add("medicinebag");request=new TacticalRequest{type="camp",choice="rest"};after=RoundTrip(TacticalRules.Act(state,request));receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(TacticalRules.FindUnit(after,"sixuan").hp==27,"camp percentage healing adds the medicine bag's six HP after calculating the percentage");
        Check(receipt.entries.Where(e=>e.kind=="health").Sum(e=>e.amount)==after.units.Sum(u=>u.hp)-state.units.Sum(u=>u.hp),"camp receipt equals all actual health gains");
        state=Battle();foreach(var u in state.units.Where(u=>u.team=="enemy")){u.hp=0;u.block=0;}
        state=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId="shangshuo"}));
        var reward=state.rewards[0];reward.kind="gold";reward.name="金币行囊";reward.text="获得40金币";reward.amount=40;reward.skillId="";reward.relicId="";
        request=new TacticalRequest{type="reward",choice=reward.id};after=RoundTrip(TacticalRules.Act(state,request));receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(receipt.entries.Single().kind=="gold"&&receipt.entries.Single().amount==40,"direct gold reward has an immediate exact receipt");
        reward.kind="skill";reward.name="破障";reward.text="升级破障";reward.amount=0;reward.skillId="strike";
        request=new TacticalRequest{type="reward",choice=reward.id,unitId="shangshuo"};after=RoundTrip(TacticalRules.Act(state,request));receipt=TacticalRules.DescribeOutcome(state,after,request);
        Check(receipt.entries.Single().detail.Contains("Lv.1 → Lv.2")&&receipt.entries.Single().detail.Contains("基础伤害 10"),"repeat skill reward exposes real upgraded damage");
    }
    static void RiskChecks()
    {
        var state=Event("valveevent");bool foundGift=false,foundInjury=false;
        for(uint rng=1;rng<=30;rng++)
        {
            state.rng=rng;var after=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="event",choice="A"}));
            if(after.relics.Count==1){foundGift=true;Check(after.units.All(u=>u.hp==20),"successful valve work has no hidden injury");}
            else{foundInjury=true;Check(after.units.All(u=>u.hp==12),"failed valve work matches the advertised eight HP cost");}
        }
        Check(foundGift&&foundInjury,"both explicit valve risk outcomes are exercised");
        state=Event("dicestall");bool won=false,lost=false;
        for(uint rng=1;rng<=10;rng++){state.rng=rng;var after=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="event",choice="A"}));won|=after.gold==120;lost|=after.gold==80;Check(after.gold==120||after.gold==80,"dice stall risks only the advertised twenty gold stake");}
        Check(won&&lost,"both explicit stake outcomes are exercised");
    }
    static void HealingAndTerrainChecks()
    {
        var state=Battle();state.relics.AddRange(new[]{"moonledger","flask"});
        foreach(var unit in state.units){if(unit.team=="hero"){unit.hp=10;unit.regeneration=10;}else unit.rooted=true;}
        var after=FinishRound(state);Check(after.units.Where(u=>u.team=="hero").All(u=>u.hp==18),"moon ledger reduces both ten-point regeneration to seven and flask healing to one");
        state=Battle();state.relics.Add("moonledger");foreach(var unit in state.units)unit.hp=unit.team=="hero"?10:0;
        var sixuan=TacticalRules.FindUnit(state,"sixuan");sixuan.hp=0;sixuan.ap=0;sixuan.block=0;
        after=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId="shangshuo"}));
        Check(TacticalRules.FindUnit(after,"shangshuo").hp==12&&TacticalRules.FindUnit(after,"sixuan").hp==11,"moon ledger applies to victory recovery and rejoining health");
        var reward=after.rewards[0];reward.kind="relic";reward.relicId="heavywheel";reward.skillId="";reward.amount=0;reward.name="沉重闸轮";reward.text=TacticalContent.GetRelic("heavywheel").text;
        var claimed=RoundTrip(TacticalRules.Act(after,new TacticalRequest{type="reward",choice=reward.id}));
        Check(TacticalRules.FindUnit(claimed,"shangshuo").hp==16,"moon ledger reduces heavy wheel acquisition healing from six to four");
        state=Battle();state.objects.Add(new TacticalObject{id="object-0-0",kind="valve",x=2,y=2,hp=8});
        state.terrain.First(c=>c.x==3&&c.y==2).kind="wall";state.terrain.First(c=>c.x==3&&c.y==3).kind="gap";
        after=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId="sixuan",skillId="strike",x=2,y=2});
        Check(TacticalRules.TerrainAt(after,3,2)=="wall"&&TacticalRules.TerrainAt(after,3,3)=="gap","broken steam valve cannot erase walls or bridge gaps");
        Check(TacticalRules.TerrainAt(after,2,3)=="mist"&&TacticalRules.ObjectAt(after,2,2)==null,"broken valve still creates usable mist on adjacent walkable floor");
    }
    static void CursedRelicChecks()
    {
        foreach(var pair in new[]{new[]{"nailreturn","heavywheel"},new[]{"valveevent","overheatvalve"},new[]{"oldxu","rustwatch"}})
        {
            var state=Event(pair[0]);var option=TacticalRules.CurrentEvent(state).choices.Single(c=>c.id=="C");
            Check(option.description.Contains(TacticalContent.GetRelic(pair[1]).text),"curse choice exposes the complete persistent advantage and disadvantage: "+pair[1]);
            var request=new TacticalRequest{type="event",choice="C"};var after=RoundTrip(TacticalRules.Act(state,request));
            Check(after.relics.Contains(pair[1])&&TacticalRules.DescribeOutcome(state,after,request).entries.Single(e=>e.kind=="relic").id==pair[1],"curse is actually obtainable and identified in the receipt");
            if(pair[1]=="heavywheel")Check(TacticalRules.MoveBudget(after)==3&&after.units.All(u=>u.hp==26),"heavy wheel immediately heals six and reduces the movement budget by one");
            state.relics.Add(pair[1]);Check(TacticalRules.CurrentEvent(state).choices.Single(c=>c.id=="C").description.Contains("改为"),"duplicate curse discloses its fallback");
            after=RoundTrip(TacticalRules.Act(state,request));Check(after.relics.Count==1&&TacticalRules.DescribeOutcome(state,after,request).entries.All(e=>e.kind!="relic"),"duplicate curse cannot grant or report a second item");
        }
        var battle=Battle();battle.relics.Add("overheatvalve");var hero=TacticalRules.FindUnit(battle,"shangshuo");battle.terrain.First(c=>c.x==hero.x&&c.y==hero.y).kind="pipe";foreach(var enemy in battle.units.Where(u=>u.team=="enemy"))enemy.rooted=true;
        var burned=FinishRound(battle);var changed=TacticalRules.FindUnit(burned,hero.id);Check(changed.hp==hero.hp-4&&changed.damageBonus==3,"overheat valve applies both extra pipe injury and the promised encouragement");
        var normalShop=Noncombat("shop");var parent=normalShop.mapNodes.First(n=>n.floor==normalShop.floor-1&&n.next.Contains(normalShop.currentNodeId));string target=normalShop.currentNodeId;
        var beforeEntry=normalShop.Clone();var selected=TacticalRules.CurrentNode(beforeEntry);selected.visited=false;beforeEntry.phase="map";beforeEntry.floor=parent.floor;beforeEntry.chapter=parent.chapter;beforeEntry.currentNodeId=parent.id;beforeEntry.nodeKind=parent.kind;beforeEntry.sideBattle=parent.kind!="boss";beforeEntry.shopOffers.Clear();beforeEntry.relics.Add("rustwatch");
        var costly=RoundTrip(TacticalRules.Act(beforeEntry,new TacticalRequest{type="node",choice=target}));
        Check(normalShop.shopOffers.First(o=>o.kind=="skill").price==45&&costly.shopOffers.First(o=>o.kind=="skill").price==56,"rust watch raises newly entered shop prices by twenty-five percent with integer pricing");
        battle=Battle();battle.relics.Add("rustwatch");foreach(var enemy in battle.units.Where(u=>u.team=="enemy"))enemy.hp=0;var paid=TacticalRules.Act(battle,new TacticalRequest{type="endTurn",unitId="shangshuo"});
        Check(paid.gold-battle.gold==50,"rust watch adds ten to the first-floor forty-gold survival reward");
    }
    static void ResetCharge(TacticalState state){foreach(var unit in state.units.Where(u=>u.team=="hero"))unit.charge=0;}
    static void AssertCharge(TacticalState before,TacticalRequest request,int kills,int casterCharge,string text)
    {
        var preview=TacticalRules.Preview(before,request);Check(preview.ok,text+" preview is legal");
        var after=TacticalRules.Act(before,request);var caster=TacticalRules.FindUnit(after,request.unitId);
        Check(before.units.Count(u=>u.team=="enemy"&&u.hp>0)-after.units.Count(u=>u.team=="enemy"&&u.hp>0)==kills,text+" defeats the expected distinct enemies");
        Check(caster.charge==casterCharge&&after.units.Where(u=>u.team=="hero"&&u.id!=caster.id).All(u=>u.charge==kills*10),text+" grants one charge reward per defeated enemy, including the horn");
        Check(before.units.Where(u=>u.team=="hero").All(u=>u.charge==0),text+" preview cannot commit or duplicate charge");
    }
    static void SkillKillChecks()
    {
        var state=Battle();ResetCharge(state);state.relics.Add("brasshorn");var caster=TacticalRules.FindUnit(state,"sixuan");caster.skills.Add("ember");caster.skillLevels["ember"]=1;
        var enemy=state.units.First(u=>u.team=="enemy");enemy.x=2;enemy.y=2;enemy.hp=10;enemy.drenched=2;
        AssertCharge(state,new TacticalRequest{type="skill",unitId=caster.id,skillId="ember",x=2,y=2},1,45,"steam kill during a skill's secondary effect");
        state=Battle();ResetCharge(state);state.relics.Add("brasshorn");state.objects.Add(new TacticalObject{id="object-0-0",kind="canister",x=2,y=2,hp=8});
        var enemies=state.units.Where(u=>u.team=="enemy").ToArray();enemies[0].x=3;enemies[0].y=2;enemies[1].x=2;enemies[1].y=3;foreach(var foe in enemies)foe.hp=4;
        AssertCharge(state,new TacticalRequest{type="skill",unitId="sixuan",skillId="strike",x=2,y=2},2,70,"skill-triggered canister blast");
        state=Battle();ResetCharge(state);state.relics.Add("brasshorn");TacticalRules.FindUnit(state,"cangling").x=1;TacticalRules.FindUnit(state,"cangling").y=3;
        enemies=state.units.Where(u=>u.team=="enemy").ToArray();enemies[0].x=2;enemies[0].y=1;enemies[0].hp=8;enemies[1].x=3;enemies[1].y=1;enemies[1].hp=4;
        AssertCharge(state,new TacticalRequest{type="skill",unitId="shangshuo",skillId="shove",x=2,y=1},2,70,"collision kills the target and a neighboring enemy");
        state=Battle();ResetCharge(state);state.relics.Add("brasshorn");enemy=state.units.First(u=>u.team=="enemy");enemy.hp=2;state.terrain.First(c=>c.x==enemy.x&&c.y==enemy.y).kind="pipe";foreach(var foe in state.units.Where(u=>u.team=="enemy"))foe.rooted=true;
        var environmental=FinishRound(state);Check(TacticalRules.FindUnit(environmental,enemy.id).hp==0&&environmental.units.Where(u=>u.team=="hero").All(u=>u.charge==0),"automatic environmental deaths are not credited as active skill kills");
        state=Battle();state.ascentRevision=2;ResetCharge(state);state.objects.Add(new TacticalObject{id="object-0-0",kind="canister",x=2,y=2,hp=8});enemy=state.units.First(u=>u.team=="enemy");enemy.x=3;enemy.y=2;enemy.hp=4;
        var old=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId="sixuan",skillId="strike",x=2,y=2});Check(TacticalRules.FindUnit(old,"sixuan").charge==20,"published revision-2 object-kill charge behavior stays unchanged");
    }
    static void DeathTransitionChecks()
    {
        foreach(string mechanism in new[]{"strike","sever","combo"})
        {
            var state=Battle();state.relics.Add("dreamspool");var enemies=state.units.Where(u=>u.team=="enemy").ToArray();var victim=enemies[0];var neighbor=enemies[1];
            victim.x=2;victim.y=2;victim.hp=mechanism=="strike"?2:mechanism=="sever"?10:6;victim.block=mechanism=="strike"?0:40;victim.affixes="rusted+shellhard";
            neighbor.x=3;neighbor.y=2;neighbor.affixes="guarding";neighbor.rooted=true;
            TacticalState after;
            if(mechanism=="combo")
            {
                victim.comboMask=7;
                // A scorched neighbour falls before completing its enemy action, so
                // its stored vulnerability exposes the combo's death effect directly.
                neighbor.hp=1;neighbor.scorched=1;
                after=FinishRound(state);
            }
            else
            {
                var caster=TacticalRules.FindUnit(state,"sixuan");if(!caster.skills.Contains(mechanism)){caster.skills.Add(mechanism);caster.skillLevels[mechanism]=1;}
                after=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=caster.id,skillId=mechanism,x=2,y=2});
            }
            var dead=TacticalRules.FindUnit(after,victim.id);
            Check(dead.hp==0&&dead.block==0&&!TacticalContent.HasAffix(dead,"rusted")&&TacticalContent.HasAffix(dead,"shellhard"),mechanism+" death consumes rusted but retains other affixes");
            Check(TacticalRules.TerrainAt(after,2,2)=="rubble"&&after.log.Count(l=>l.Contains(victim.name+"锈坏成碎石"))==1,mechanism+" death creates rubble and logs its death effect once");
            Check(TacticalRules.FindUnit(after,neighbor.id).vulnerable==1,mechanism+" death shares vulnerability with its living neighbour");
        }
        var untouched=Battle();untouched.relics.Add("dreamspool");var foes=untouched.units.Where(u=>u.team=="enemy").ToArray();foes[0].x=2;foes[0].y=2;foes[0].hp=10;foes[0].block=40;foes[0].affixes="rusted+shellhard";foes[1].x=3;foes[1].y=2;
        var protectedState=TacticalRules.Act(untouched,new TacticalRequest{type="skill",unitId="sixuan",skillId="strike",x=2,y=2});
        Check(TacticalRules.FindUnit(protectedState,foes[0].id).hp==10&&TacticalRules.FindUnit(protectedState,foes[0].id).block==36&&TacticalRules.FindUnit(protectedState,foes[1].id).vulnerable==0&&TacticalRules.TerrainAt(protectedState,2,2)=="plain","nonlethal reduced damage does not trigger death effects");
        var old=untouched.Clone();old.ascentRevision=2;old.relics.Clear();foes=old.units.Where(u=>u.team=="enemy").ToArray();foes[0].affixes="";foes[0].comboMask=7;foes[0].hp=6;foes[0].block=40;foes[1].rooted=true;
        var oldCollapse=FinishRound(old);Check(TacticalRules.FindUnit(oldCollapse,foes[0].id).hp==0&&TacticalRules.TerrainAt(oldCollapse,2,2)=="plain","revision-2 true-damage collapse preserves its original shield-bypassing behavior");
        var sweep=TacticalRules.NewAscent(71,new[]{"lingfeng","sixuan","cangling"},3,5);sweep=TacticalRules.Act(sweep,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(sweep)[0].id});
        sweep.terrain.ForEach(c=>c.kind="plain");sweep.objects.Clear();sweep.relics.AddRange(new[]{"dreamspool","brasshorn"});ResetCharge(sweep);
        var fire=TacticalRules.FindUnit(sweep,"lingfeng");fire.charge=100;var water=TacticalRules.FindUnit(sweep,"cangling");water.x=1;water.y=3;
        foes=sweep.units.Where(u=>u.team=="enemy").ToArray();
        for(int i=0;i<3;i++){foes[i].x=2+i;foes[i].y=1;foes[i].affixes=i<2?"rusted":"";foes[i].hp=i==1?4:14;foes[i].block=i==0?12:i==2?40:0;}
        foes[0].drenched=2;
        var cascade=TacticalRules.Act(sweep,new TacticalRequest{type="skill",unitId="lingfeng",skillId="lingfeng_ultimate",x=2,y=1});
        Check(foes.Take(2).All(f=>TacticalRules.FindUnit(cascade,f.id).hp==0)&&TacticalRules.FindUnit(cascade,foes[2].id).hp>0,"steam splash defeats an enemy already queued for the same area attack");
        Check(cascade.log.Count(l=>l.Contains("锈坏成碎石"))==2&&TacticalRules.FindUnit(cascade,"lingfeng").charge==50,"area cascade settles each real death once without rewarding the queued corpse hit again");
        var survivor=TacticalRules.FindUnit(cascade,foes[2].id);survivor.vulnerable=0;fire=TacticalRules.FindUnit(cascade,"lingfeng");fire.x=4;fire.y=2;
        if(!fire.skills.Contains("cleave")){fire.skills.Add("cleave");fire.skillLevels["cleave"]=1;}
        var later=TacticalRules.Act(cascade,new TacticalRequest{type="skill",unitId="lingfeng",skillId="cleave",x=4,y=1});
        Check(TacticalRules.FindUnit(later,survivor.id).vulnerable==0&&later.log.Count(l=>l.Contains("锈坏成碎石"))==2,"a later area attack beside those corpses cannot repeat dream-spool or rust effects");
    }
    static void Teach(TacticalUnit unit,string id,int level=1)
    {if(!unit.skills.Contains(id))unit.skills.Add(id);unit.skillLevels[id]=level;}
    static void BattleFeedbackChecks()
    {
        var state=Battle();var actor=TacticalRules.FindUnit(state,"sixuan");actor.ap=0;
        var blocked=TacticalRules.SkillAvailability(state,actor.id,null);var move=new TacticalRequest{type="move",unitId=actor.id,x=3,y=3};
        Check(!blocked.ok&&blocked.resource=="ap"&&blocked.cost==1&&TacticalRules.Preview(state,move).reason==blocked.reason,"movement tray and target preview share the AP refusal");
        Check(!TacticalRules.SkillAvailability(state,actor.id,"strike").ok,"AP skills report unavailable before a target is chosen");
        actor.ap=actor.maxAp;actor.rooted=true;Check(!TacticalRules.SkillAvailability(state,actor.id,null).ok,"rooted movement is unavailable too");actor.rooted=false;
        actor.turnEnded=true;Check(!TacticalRules.SkillAvailability(state,actor.id,"balance").ok,"an ended actor cannot offer available skills");actor.turnEnded=false;
        actor.sp=0;Check(!TacticalRules.SkillAvailability(state,actor.id,"balance").ok,"SP shortage is visible before targeting");actor.sp=actor.maxSp;
        actor.cooldowns["balance"]=2;Check(TacticalRules.SkillAvailability(state,actor.id,"balance").reason.Contains("2 回合"),"cooldown refusal exposes its remaining duration");actor.cooldowns.Clear();
        var water=TacticalRules.FindUnit(state,"cangling");var ground=state.terrain.First(c=>c.x==water.x&&c.y==water.y);
        Check(!TacticalRules.SkillAvailability(state,water.id,"tide").ok&&TacticalRules.SkillAvailability(state,water.id,"tide").reason.Contains("水雾"),"required water terrain is a visible unavailable reason");
        Check(TacticalRules.SkillAvailability(state,water.id,"tidemend").ok,"optional water healing bonus does not block plain-ground healing");
        ground.kind="mist";Check(TacticalRules.SkillAvailability(state,water.id,"tide").ok,"required water skill becomes available immediately on mist");
        Check(!TacticalRules.Preview(state,new TacticalRequest{type="skill",unitId=water.id,skillId="tide",x=7,y=7}).ok,"target-dependent range remains a separate preview check");
        Teach(actor,"shadowveil");Check(!TacticalRules.SkillAvailability(state,actor.id,"shadowveil").ok,"required shadow is unavailable on plain ground");
        state.terrain.First(c=>c.x==actor.x&&c.y==actor.y).kind="shadow";Check(TacticalRules.SkillAvailability(state,actor.id,"shadowveil").ok,"shadow skill becomes available on shadow");
        state.terrain.First(c=>c.x==actor.x&&c.y==actor.y).kind="plain";Teach(actor,"step");state.terrain.First(c=>c.x==2&&c.y==3).kind="shadow";
        Check(TacticalRules.SkillAvailability(state,actor.id,"step").ok&&TacticalRules.Preview(state,new TacticalRequest{type="skill",unitId=actor.id,skillId="step",x=2,y=3}).ok,"borrowed shadow can start on plain ground if the landing cell is shadow");
        Check(!TacticalRules.SkillAvailability(state,actor.id,"throw").ok,"no nearby object gives an immediate throw refusal");
        state.objects.Add(new TacticalObject{id="object-0-0",kind="crate",x=actor.x+2,y=actor.y,hp=8});
        Check(!TacticalRules.SkillAvailability(state,actor.id,"throw").ok,"ordinary throw cannot pick up an object two cells away");state.relics.Add("wrench");
        Check(TacticalRules.SkillAvailability(state,actor.id,"throw").ok,"wrench pickup extension is reflected by availability");
        var tank=TacticalRules.FindUnit(state,"shangshuo");Teach(tank,"bastion");Check(!TacticalRules.SkillAvailability(state,tank.id,"bastion").ok,"passive skill remains readable without being castable");
        Check(!TacticalBattlePresentation.ShouldFaceTarget(actor,new TacticalRequest{type="endTurn",unitId=actor.id}),"ending an actor never turns it toward the default zero coordinates");
        Check(!TacticalBattlePresentation.ShouldFaceTarget(actor,new TacticalRequest{type="move",unitId=actor.id,x=3,y=3}),"movement leaves facing to the actual path segments");
        Check(!TacticalBattlePresentation.ShouldFaceTarget(actor,new TacticalRequest{type="skill",unitId=actor.id,x=actor.x,y=actor.y}),"self-targeted support preserves facing");
        Check(TacticalBattlePresentation.ShouldFaceTarget(actor,new TacticalRequest{type="skill",unitId=actor.id,x=actor.x+1,y=actor.y}),"skills directed at another cell face their real target");
        var changed=state.Clone();changed.terrain[0].kind="mist";Check(TacticalBattlePresentation.KeepFacingOnRebuild(state,changed),"terrain-only rebuilding preserves the ongoing battle's facing");changed.currentNodeId="another-node";
        Check(!TacticalBattlePresentation.KeepFacingOnRebuild(state,changed),"a new battle initializes its own facing");
        actor.rooted=true;actor.weakened=1;actor.regeneration=4;Check(TacticalBattleCopy.StatusSummary(actor).Contains("定身 / 削弱 +1"),"status entry summarizes two conditions and the remaining count");
        foreach(var skill in TacticalContent.Skills)
        {
            Teach(actor,skill.id,3);string[] lines=TacticalBattleCopy.SkillPurpose(state,actor,skill).Split('\n');
            Check(lines.Length==2&&lines.All(line=>line.Length>0),skill.id+" has two concise purpose lines");
            Check(lines.All(line=>line.Sum(c=>c<128?.55:1)<=9.05),skill.id+" purpose fits its reserved 162 px at 18 px CJK text");
        }
        state=Battle();string snapshot=TacticalCodec.Encode(state);TacticalRules.SkillAvailability(state,"sixuan","throw");Check(TacticalCodec.Encode(state)==snapshot,"opening availability never mutates the committed state");
    }
    static void ContentEdgeChecks()
    {
        var state=Battle();var caster=TacticalRules.FindUnit(state,"sixuan");Teach(caster,"ignite");var foes=state.units.Where(u=>u.team=="enemy").ToArray();
        foes[0].x=2;foes[0].y=2;foes[0].hp=4;foes[0].scorched=4;foes[0].drenched=2;foes[1].x=3;foes[1].y=2;
        var after=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=caster.id,skillId="ignite",x=2,y=2});
        Check(TacticalRules.FindUnit(after,foes[0].id).scorched==0&&TacticalRules.FindUnit(after,foes[0].id).drenched==0&&TacticalRules.FindUnit(after,foes[1].id).hp==foes[1].hp-4,"lethal ignite still consumes its statuses and triggers the advertised adjacent steam burst");
        state=Battle();caster=TacticalRules.FindUnit(state,"sixuan");Teach(caster,"transfer");foes=state.units.Where(u=>u.team=="enemy").ToArray();foes[0].x=2;foes[0].y=2;foes[0].hp=6;
        var ally=TacticalRules.FindUnit(state,"shangshuo");ally.rooted=true;ally.weakened=1;ally.vulnerable=2;
        after=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=caster.id,skillId="transfer",x=2,y=2});
        var cleaned=TacticalRules.FindUnit(after,ally.id);Check(!cleaned.rooted&&cleaned.weakened==0&&cleaned.vulnerable==0,"lethal transfer still cleanses the promised teammate conditions");
        state.relics.Add("doublepen");foes[0].hp=foes[0].maxHp;after=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=caster.id,skillId="transfer",x=2,y=2});
        Check(TacticalRules.FindUnit(after,foes[0].id).weakened==2&&TacticalRules.FindUnit(after,foes[0].id).vulnerable==3,"double pen extends transferred weakness and vulnerability without exceeding the duration cap");
        state=Battle();caster=TacticalRules.FindUnit(state,"sixuan");Teach(caster,"ember");state.relics.Add("bladetsuba");foes=state.units.Where(u=>u.team=="enemy").ToArray();foes[0].x=2;foes[0].y=2;foes[0].drenched=2;foes[1].x=3;foes[1].y=2;
        after=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=caster.id,skillId="ember",x=2,y=2});
        Check(TacticalRules.FindUnit(after,foes[1].id).hp==foes[1].hp-6,"blade tsuba increases adjacent steam damage from four to six too");
        state=Battle();state.relics.Add("anchorhammer");ally=TacticalRules.FindUnit(state,"shangshuo");ally.block=2;ally.retaliation=4;TacticalRules.FindUnit(state,"cangling").x=1;TacticalRules.FindUnit(state,"cangling").y=3;
        foes=state.units.Where(u=>u.team=="enemy").ToArray();foes[0].x=2;foes[0].y=1;foreach(var foe in foes)foe.rooted=true;
        after=FinishRound(state);Check(TacticalRules.FindUnit(after,foes[0].id).hp==foes[0].hp-7,"anchor hammer uses the shield held on impact even when that blow breaks it");
        state=Battle();caster=TacticalRules.FindUnit(state,"sixuan");Teach(caster,"triplebalance",2);
        string description=TacticalRules.SkillDescription(state,caster,"triplebalance");Check(description.Contains("恢复 3 AP")&&description.Contains("蓄势 +16")&&!description.Contains("恢复1点行动点")&&!description.Contains("15蓄势"),"upgraded skill detail has one coherent set of current values");
        Teach(caster,"shadowveil");description=TacticalRules.SkillDescription(state,caster,"shadowveil");Check(description.Contains("影遁")&&!description.Contains("获得 0 护盾"),"stealth detail describes its actual effect instead of a zero shield");
        Teach(caster,"ignite");description=TacticalRules.SkillDescription(state,caster,"ignite");Check(description.Contains("结算目标全部余焰")&&!description.Contains("基础伤害 0"),"ignite detail describes the stored scorch instead of misleading zero damage");
        Teach(caster,"ignite",2);description=TacticalRules.SkillDescription(state,caster,"ignite");Check(description.Contains("基础伤害 +2"),"upgraded ignite exposes its extra damage without hiding the stored scorch");
        state=Battle();caster=TacticalRules.FindUnit(state,"sixuan");caster.x=1;caster.y=2;foes=state.units.Where(u=>u.team=="enemy").ToArray();foes[0].x=5;foes[0].y=2;foes[1].x=7;foes[1].y=7;
        state.objects.Add(new TacticalObject{id="object-0-0",kind="crate",x=3,y=2,hp=8});
        var thrown=new TacticalRequest{type="skill",unitId=caster.id,skillId="throw",x=5,y=2};var throwSkill=TacticalContent.GetSkill("throw");
        Check(TacticalRules.EffectiveSkillRange(state,caster,throwSkill)==3&&!TacticalRules.Preview(state,thrown).ok,"ordinary throws retain their original three-cell range");
        state.relics.Add("wrench");description=TacticalRules.SkillDescription(state,caster,"throw");var preview=TacticalRules.Preview(state,thrown);after=TacticalRules.Act(state,thrown);
        Check(TacticalRules.EffectiveSkillRange(state,caster,throwSkill)==4&&description.Contains("范围 4")&&preview.ok&&TacticalRules.FindUnit(after,foes[0].id).hp==foes[0].hp-preview.damage&&after.objects.Count==0,"wrench range is shared by description, preview and committed throw with extended pickup");
        state=Battle();caster=TacticalRules.FindUnit(state,"sixuan");caster.charge=100;foes=state.units.Where(u=>u.team=="enemy").ToArray();foes[0].x=2;foes[0].y=2;foes[1].x=7;foes[1].y=7;
        var ultimate=TacticalContent.GetSkill("sixuan_ultimate");var ultimateRequest=new TacticalRequest{type="skill",unitId=caster.id,skillId=ultimate.id,x=2,y=2};
        Check(TacticalRules.SkillCooldown(state,ultimate)==2&&TacticalRules.SkillDescription(state,caster,ultimate.id).Contains("冷却 2 轮"),"ordinary ultimate retains its two-round cooldown");
        state.relics.Add("judgeseal");after=TacticalRules.Act(state,ultimateRequest);
        Check(TacticalRules.SkillCooldown(state,ultimate)==1&&TacticalRules.SkillDescription(state,caster,ultimate.id).Contains("冷却 1 轮")&&TacticalRules.FindUnit(after,caster.id).cooldowns[ultimate.id]==1,"judge seal cooldown is shared by detail and committed action");
        Check(TacticalRules.EffectiveSkillRange(null,null,throwSkill)==throwSkill.range&&TacticalRules.SkillCooldown(null,ultimate)==ultimate.cooldown,"catalogue inspection without a journey reports unmodified base values");
        state=Event("potshare");state.relics.AddRange(new[]{"medicinebag","blanket","moonledger"});foreach(var unit in state.units)unit.hp=5;
        var meal=TacticalRules.CurrentEvent(state);after=TacticalRules.Act(state,new TacticalRequest{type="event",choice="B"});
        Check(meal.choices[0].description.Contains("最多 18 生命")&&meal.choices[1].description.Contains("最多 14 生命")&&after.units.All(u=>u.hp==19),"meal choices show the same medicine, blanket and curse adjusted healing as settlement");
        state=Event("dreamshop");state.relics.Add("moonledger");var pillow=new TacticalRequest{type="event",choice="B"};after=TacticalRules.Act(state,pillow);
        Check(TacticalRules.CurrentEvent(state).choices[1].description.Contains("最多 4 生命")&&TacticalRules.DescribeOutcome(state,after,pillow).entries.Any(e=>e.id=="pillowBattles"&&e.detail.Contains("最多 4 生命")),"pillow choice and receipt both show its current curse-adjusted healing");
    }
}
