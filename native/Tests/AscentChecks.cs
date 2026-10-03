using System;
using System.Linq;
using System.Collections.Generic;
using Dicebound.Tactics;
using Dicebound.Persistence;
using Newtonsoft.Json.Linq;
class AscentChecks
{
    static int checks,journeys,actions;
    // This suite keeps the original v3 ascent contract, including its authored old shelves
    // and encounter populations. The current build/curve is covered by ProgressionChecks.
    static TacticalState LegacyAscent(uint seed,IEnumerable<string> heroes)
    {var state=TacticalRules.NewAscent(seed,heroes);state.ascentRevision=0;return state;}
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static TacticalState Step(TacticalState state,TacticalRequest request)
    {
        Check(request!=null&&TacticalRules.Preview(state,request).ok,"continuation is legal");
        string before=TacticalCodec.Encode(state);var next=TacticalRules.Act(state,request);
        Check(TacticalCodec.Encode(state)==before,"settlement leaves previous checkpoint unchanged");
        Check(next.revision==state.revision+1,"one request increments revision once");
        string encoded=TacticalCodec.Encode(next);
        Check(TacticalCodec.TryDecode(encoded,out var restored,out _)&&TacticalCodec.Encode(restored)==encoded,"every phase restores exactly");actions++;return restored;
    }
    static bool LeadsTo(TacticalState s,TacticalMapNode node,string destination)
    {return node.id==destination||node.next.Any(id=>LeadsTo(s,s.mapNodes.Single(n=>n.id==id),destination));}
    static TacticalState Shop()
    {
        var s=LegacyAscent(71,new[]{"sixuan","lingfeng","cangling"});var target=s.mapNodes.First(n=>n.kind=="shop");
        for(int i=0;i<2000&&s.phase!="shop";i++){
            var action=s.phase=="map"?new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(s).First(n=>LeadsTo(s,n,target.id)).id}:TacticalRules.Suggest(s);
            s=Step(s,action);Check(s.phase!="defeat","ordinary legal progression reaches the shop");
        }
        Check(s.phase=="shop","shop reached through ordinary rules");return s;
    }
    static void GraphChecks()
    {
        var signatures=new HashSet<string>();
        for(uint seed=0;seed<40;seed++){
            var s=LegacyAscent(seed,new[]{"sixuan","lingfeng","cangling"});
            var again=LegacyAscent(seed,new[]{"sixuan","lingfeng","cangling"});
            Check(TacticalCodec.Encode(s)==TacticalCodec.Encode(again),"same seed gives exact initial checkpoint");
            signatures.Add(string.Join(";",s.mapNodes.Select(n=>n.id+":"+n.kind+":"+string.Join(",",n.next))));
            var reachable=new HashSet<string>(TacticalRules.AvailableNodes(s).Select(n=>n.id));
            foreach(var node in s.mapNodes.OrderBy(n=>n.floor)){Check(reachable.Contains(node.id),"every generated node is reachable");foreach(string next in node.next)reachable.Add(next);}
            foreach(var row in s.mapNodes.Where(n=>n.floor<11).GroupBy(n=>n.floor))Check(row.Count()>=2&&row.Count()<=3,"each ordinary layer has two or three choices");
            Check(!TacticalRules.Preview(s,new TacticalRequest{type="node",choice=s.mapNodes.Single(n=>n.floor==11).id}).ok,"cannot skip to the boss");
        }
        Check(signatures.Count>=35,"different seeds produce different route maps");
    }
    static void SpawnGround(TacticalState state)
    {
        int[] xs={7,9,8,10},ys={3,5,7,8};
        for(int i=0;i<xs.Length;i++)Check(TacticalRules.TerrainAt(state,xs[i],ys[i])=="plain","fixed spawn candidates stay level plain ground for "+state.nodeKind+" in region "+state.chapter+" at "+xs[i]+","+ys[i]);
    }
    static void RejectMutation(TacticalState s,Action<JObject> change,string message)
    {
        var raw=JObject.Parse(TacticalCodec.Encode(s));change((JObject)raw["run"]);
        Check(!TacticalCodec.TryDecode(raw.ToString(),out _,out _),message);
    }
    static void Boundaries(TacticalState initial,TacticalState battle,TacticalState shop,TacticalState map)
    {
        RejectMutation(initial,s=>s["sideBattle"]=true,"initial map cannot claim a selected side battle");
        RejectMutation(battle,s=>s["sideBattle"]=false,"ordinary ascent battle cannot claim boss status");
        RejectMutation(initial,s=>s.Remove("mapNodes"),"partial ascent field group is corruption");
        RejectMutation(initial,s=>s["journeyMode"]="unknown","unknown journey mode is corruption");
        RejectMutation(initial,s=>s["floor"]=0.0,"floor must remain an integer");
        RejectMutation(initial,s=>s["mapNodes"][0]["visited"]="true","node flags cannot be coerced");
        RejectMutation(initial,s=>s["mapNodes"][0]["completed"]=true,"unvisited node cannot be completed");
        RejectMutation(map,s=>s["currentNodeId"]="missing","current node must exist");
        RejectMutation(map,s=>s["mapNodes"][0]["visited"]=false,"saved visit path cannot skip the first layer");
        RejectMutation(shop,s=>s["shopOffers"][0]["price"]=1,"saved stock cannot change its price");
        RejectMutation(shop,s=>s["shopOffers"][0]["sold"]="false","saved sold state must be boolean");
        RejectMutation(shop,s=>s["shopOffers"][0]["relicId"]="unknown","saved stock cannot replace its relic");
        RejectMutation(map,s=>s["shopOffers"]=JArray.FromObject(shop.shopOffers),"stock cannot survive leaving the shop");
        string encoded=TacticalCodec.Encode(map);
        var unavailable=map.mapNodes.FirstOrDefault(n=>n.floor==map.floor+1&&!TacticalRules.AvailableNodes(map).Any(a=>a.id==n.id));
        if(unavailable!=null){var action=new TacticalRequest{type="node",choice=unavailable.id};Check(!TacticalRules.Preview(map,action).ok,"disconnected next-layer node is rejected");bool threw=false;try{TacticalRules.Act(map,action);}catch(InvalidOperationException){threw=true;}Check(threw&&TacticalCodec.Encode(map)==encoded,"rejected selection cannot change checkpoint");}
        var clone=shop.Clone();clone.shopOffers[0].sold=true;clone.shopOffers[0].name="changed";
        Check(!shop.shopOffers[0].sold&&shop.shopOffers[0].name!="changed","shop stock cloning is deep");
        var skill=shop.shopOffers.Single(o=>o.kind=="skill");
        var recipient=shop.units.First(u=>TacticalContent.CanLearn(u,TacticalContent.GetSkill(skill.skillId)));
        int level=recipient.skillLevels.TryGetValue(skill.skillId,out var learned)?learned:0;
        var trained=Step(shop,new TacticalRequest{type="buy",choice=skill.id,unitId=recipient.id});
        Check(trained.gold==shop.gold-40&&TacticalRules.FindUnit(trained,recipient.id).skillLevels[skill.skillId]==level+1,"shop skill purchase grants one level at exact price");
        Check(trained.shopOffers.Single(o=>o.id==skill.id).sold,"skill purchase survives restore as sold");
    }
    static void EliteAndEvent()
    {
        foreach(string target in new[]{"floor-2-1","floor-7-1"}){
            var s=LegacyAscent(71,new[]{"sixuan","lingfeng","cangling"});
            for(int i=0;i<2000&&s.currentNodeId!=target;i++){
                var action=s.phase=="map"?new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(s).First(n=>LeadsTo(s,n,target)).id}:TacticalRules.Suggest(s);
                s=Step(s,action);Check(s.phase!="defeat","planned ordinary path survives to selected encounter");
            }
            Check(s.currentNodeId==target,"planned encounter reached without skipping layers");
            if(s.phase=="event"){
                foreach(string choice in new[]{"A","B"}){var resolved=Step(s,new TacticalRequest{type="event",choice=choice});Check(resolved.phase=="map"&&TacticalRules.CurrentNode(resolved).completed,"either event choice returns directly to map");}
            }
            else {
                Check(s.phase=="battle"&&s.nodeKind=="elite"&&s.units.Count(u=>u.team=="enemy")==3,"selected elite creates three stronger enemies");
                SpawnGround(s);
                int gold=s.gold;while(s.phase=="battle")s=Step(s,TacticalRules.Suggest(s));
                Check(s.phase=="reward"&&s.gold==gold+45,"elite win grants exactly 45 gold and a reward");
                s=Step(s,TacticalRules.Suggest(s));Check(s.phase=="map"&&TacticalRules.CurrentNode(s).completed,"elite reward returns to map");
            }
        }
    }
    static void Journeys()
    {
        var heroes=TacticalContent.Heroes.Select(h=>h.id).ToArray();
        foreach(uint seed in new uint[]{0,71})for(int a=0;a<heroes.Length;a++)for(int b=a+1;b<heroes.Length;b++)for(int c=b+1;c<heroes.Length;c++){
            var s=LegacyAscent(seed,new[]{heroes[a],heroes[b],heroes[c]});int steps=0;
            while(s.phase!="victory"&&s.phase!="defeat"&&steps<2000){var next=Step(s,TacticalRules.Suggest(s));if(s.phase=="map"&&next.phase=="battle")SpawnGround(next);Check(next.floor>=s.floor,"ascent never moves backwards");Check(next.phase!="story"&&next.phase!="route"&&next.phase!="camp","ascent never enters an old unit-page flow");s=next;steps++;}
            Check(s.phase=="victory","all hero trios can finish ascent: "+seed+" "+string.Join(",",s.squad)+" phase="+s.phase+" floor="+s.floor);
            Check(s.floor==11&&TacticalRules.CurrentNode(s).kind=="boss"&&TacticalRules.CurrentNode(s).completed,"victory completes top boss");
            Check(s.mapNodes.Count(n=>n.visited)==12&&s.mapNodes.Count(n=>n.completed)==12,"exactly one completed node per layer");journeys++;
        }
    }
    static void Main()
    {
        var state=LegacyAscent(71,new[]{"sixuan","lingfeng","cangling"});
        Check(state.phase=="map"&&state.journeyMode=="ascent"&&state.floor==-1,"new ascent starts at the map, without a story page");
        Check(state.mapNodes.Select(n=>n.floor).Distinct().Count()==12,"twelve layers");
        Check(state.mapNodes.Where(n=>n.floor==11).Count()==1&&state.mapNodes.Single(n=>n.floor==11).kind=="boss","one top boss");
        Check(TacticalRules.AvailableNodes(state).Count>=2,"first layer branches are selectable");
        Check(state.mapNodes.Where(n=>n.floor<11).All(n=>n.next.Count>=1&&n.next.All(id=>state.mapNodes.Any(m=>m.id==id&&m.floor==n.floor+1))),"all exits advance exactly one layer");
        Check(state.mapNodes.Any(n=>n.kind=="shop")&&state.mapNodes.Any(n=>n.kind=="event")&&state.mapNodes.Any(n=>n.kind=="elite"),"map offers shops, events and elites");
        var first=TacticalRules.AvailableNodes(state)[0];
        var enter=new TacticalRequest{type="node",choice=first.id};
        Check(TacticalRules.Preview(state,enter).ok,"an available node can be entered");
        var battle=TacticalRules.Act(state,enter);
        Check(battle.phase=="battle"&&battle.floor==0&&battle.currentNodeId==first.id,"entering battle skips story and records node");
        Check(state.floor==-1&&!state.mapNodes.Any(n=>n.visited),"entering a node does not mutate previous checkpoint");
        Check(!TacticalRules.Preview(battle,enter).ok,"node cannot be selected during battle");
        Check(TacticalCodec.TryDecode(TacticalCodec.Encode(state),out var restored,out _)&&restored.mapNodes.Count==state.mapNodes.Count,"initial map round trip");
        Check(TacticalCodec.TryDecode(TacticalCodec.Encode(battle),out restored,out _)&&restored.currentNodeId==first.id,"battle round trip retains selected node");
        var shop=Shop();var offer=shop.shopOffers.Single(o=>o.kind=="relic");
        var buy=new TacticalRequest{type="buy",choice=offer.id};
        Check(TacticalRules.Preview(shop,buy).ok,"a shop relic can be bought");
        var purchased=Step(shop,buy);
        Check(purchased.gold==shop.gold-55&&purchased.relics.Contains(offer.relicId),"purchase settles exact price and grants relic");
        Check(purchased.shopOffers.Single(o=>o.id==offer.id).sold&&!shop.shopOffers.Single(o=>o.id==offer.id).sold,"sold flag is saved without mutating previous stock");
        Check(!TacticalRules.Preview(purchased,buy).ok,"sold item cannot be purchased twice");
        var poor=shop.Clone();poor.gold=54;
        Check(!TacticalRules.Preview(poor,buy).ok,"insufficient funds reject a 55 gold purchase");
        var map=Step(purchased,new TacticalRequest{type="leaveShop"});
        Check(map.phase=="map"&&TacticalRules.CurrentNode(map).completed&&map.shopOffers.Count==0,"leaving completes shop and returns to map");
        Check(!TacticalRules.Preview(map,new TacticalRequest{type="node",choice=first.id}).ok,"past nodes cannot be revisited");
        var clone=map.Clone();clone.mapNodes[0].next.Clear();clone.mapNodes[0].visited=false;
        Check(map.mapNodes[0].next.Count>0&&map.mapNodes[0].visited,"node and edge cloning is deep");
        var raw=JObject.Parse(TacticalCodec.Encode(map));raw["run"]["gold"]="100";
        Check(!TacticalCodec.TryDecode(raw.ToString(),out _,out _),"coerced gold is corruption");
        raw=JObject.Parse(TacticalCodec.Encode(map));raw["run"]["mapNodes"][0]["next"]=new JArray(first.id);
        Check(!TacticalCodec.TryDecode(raw.ToString(),out _,out _),"changed or cyclic map edges are corruption");
        var old=JObject.Parse(TacticalCodec.Encode(TacticalRules.NewRun(71,new[]{"sixuan","lingfeng","cangling"})));
        foreach(string key in new[]{"journeyMode","currentNodeId","floor","gold","mapNodes","shopOffers"})((JObject)old["run"]).Remove(key);
        Check(TacticalCodec.TryDecode(old.ToString(),out var legacy,out _)&&legacy.journeyMode=="legacy"&&legacy.phase=="story","original v3 without ascent fields remains a legacy checkpoint");
        Check(TacticalRules.Act(legacy,new TacticalRequest{type="begin"}).phase=="battle","restored original v3 can continue");
        Boundaries(state,battle,shop,map);GraphChecks();EliteAndEvent();Journeys();
        Console.WriteLine("PASS: "+checks+" original-v3 ascent assertions, "+journeys+" complete legacy-revision seeded journeys, "+actions+" immutable save/restore actions.");
    }
}
