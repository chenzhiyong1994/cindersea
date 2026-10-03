using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Dicebound.Tactics;
using Dicebound.Persistence;
using Newtonsoft.Json.Linq;

class AcquisitionChecks
{
    static int checks;
    static readonly string[] WaterParty={"sixuan","cangling","shangshuo"};
    static readonly string[] FireParty={"lingfeng","yanzhuying","cangling"};
    static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
    static TacticalState RoundTrip(TacticalState state)
    {string text=TacticalCodec.Encode(state);Check(TacticalCodec.TryDecode(text,out var restored,out var error)&&TacticalCodec.Encode(restored)==text,"checkpoint round trip: "+error);return restored;}
    static TacticalState Act(TacticalState state,TacticalRequest request)
    {Check(TacticalRules.Preview(state,request).ok,"legal "+request.type+" "+request.choice);return RoundTrip(TacticalRules.Act(state,request));}
    static void Reject(TacticalState state,Action<JObject> change,string reason)
    {var json=JObject.Parse(TacticalCodec.Encode(state));change((JObject)json["run"]);Check(!TacticalCodec.TryDecode(json.ToString(),out _,out _),reason);}
    // Isolated node fixtures retain a real seed/path. No full battles or journeys are simulated.
    static TacticalState Node(string kind,string[] squad,string eventId="",uint firstSeed=1,int revision=4)
    {
        for(uint seed=firstSeed;seed<firstSeed+500;seed++)
        {
            var state=TacticalRules.NewAscent(seed,squad,revision);var node=state.mapNodes.FirstOrDefault(n=>n.kind==kind&&(eventId==""||n.eventId==eventId));if(node==null)continue;
            if(node.floor>0){var parent=state.mapNodes.First(n=>n.floor==node.floor-1&&n.next.Contains(node.id));var ancestor=parent;
                while(true){ancestor.visited=ancestor.completed=true;if(ancestor.floor==0)break;ancestor=state.mapNodes.First(n=>n.floor==ancestor.floor-1&&n.next.Contains(ancestor.id));}
                state.floor=parent.floor;state.chapter=parent.chapter;state.currentNodeId=parent.id;state.nodeKind=parent.kind;state.sideBattle=parent.kind!="boss";
            }
            state.gold=1000;
            return Act(RoundTrip(state),new TacticalRequest{type="node",choice=node.id});
        }
        throw new Exception("No node fixture: "+kind+" / "+eventId);
    }
    static void Teach(TacticalUnit unit,string id,int level=1){if(!unit.skills.Contains(id))unit.skills.Add(id);unit.skillLevels[id]=level;}
    static void OwnershipChanges(TacticalState before,TacticalState after)
    {
        int changed=0;
        foreach(var unit in after.units.Where(u=>u.team=="hero"))foreach(var entry in unit.skillLevels){var old=TacticalRules.FindUnit(before,unit.id);int was=old.skillLevels.TryGetValue(entry.Key,out int value)?value:0;if(was==entry.Value)continue;
            var skill=TacticalContent.GetSkill(entry.Key);Check(string.IsNullOrEmpty(skill.heroId)||skill.heroId==unit.heroId,"a new exclusive technique belongs to its own hero: "+entry.Key);
            Check(TacticalRules.SkillRecipient(before,entry.Key)?.id==unit.id,"all grants use the same automatic recipient");changed++;
        }
        Check(changed==1,"exactly one skill is learned or upgraded");
    }
    static void Recipients()
    {
        var state=TacticalRules.NewAscent(71,WaterParty,4);var sixuan=TacticalRules.FindUnit(state,"sixuan");Teach(sixuan,"ember");
        var saved=RoundTrip(state);Check(TacticalRules.FindUnit(saved,"sixuan").skillLevels["ember"]==1,"existing cross-character skill is not deleted");
        Check(TacticalRules.SkillRecipient(saved,"ember")==null&&!TacticalRules.CanAcquireSkill(saved,"ember"),"an absent owner cannot grant a new level to an old foreign learner");
        Check(TacticalRules.SkillRecipient(saved,"tidemend").id=="cangling","exclusive technique resolves its deployed owner");
        sixuan=TacticalRules.FindUnit(saved,"sixuan");Teach(sixuan,"strike",3);Teach(TacticalRules.FindUnit(saved,"cangling"),"strike",2);
        Check(TacticalRules.SkillRecipient(saved,"strike").id=="shangshuo","shared technique upgrades the lowest existing eligible level");
        Teach(TacticalRules.FindUnit(saved,"cangling"),"mend",2);
        Check(TacticalRules.SkillRecipient(saved,"mend").id=="cangling","shared technique prioritizes an existing learner over a new one");
        Check(TacticalRules.SkillRecipient(saved,"line").id==WaterParty[0],"a new shared technique uses stable party order");
        foreach(string id in new[]{"brokenscabbard","bladetsuba"})Check(!TacticalRules.RelicAllowedForParty(saved,id),"fire-exclusive relic requires Lingfeng: "+id);
        foreach(string id in new[]{"judgeseal","jadeimprint","anchorhammer","emberfurnace"})Check(TacticalRules.RelicAllowedForParty(saved,id),"present-owner or shared relic remains available: "+id);
        Reject(saved,s=>s.Remove("shopRevision"),"revision-four checkpoints cannot omit shelf policy");
    }
    static void Shops()
    {
        foreach(var squad in new[]{WaterParty,FireParty,new[]{"sixuan","ruanzhuo","cangling"}})
        {
            var shop=Node("shop",squad);Check(shop.shopRevision==4&&shop.ascentRevision==4,"new shop uses revision-four party stock");
            Check(shop.shopOffers.Where(o=>o.kind=="skill").Select(o=>o.heroId).OrderBy(x=>x).SequenceEqual(squad.OrderBy(x=>x)),"shop has exactly one characteristic technique for each deployed hero");
            Check(shop.shopOffers.Where(o=>o.kind=="relic").All(o=>TacticalRules.RelicAllowedForParty(shop,o.relicId)),"shop relic is useful to the deployed party");
            foreach(var offer in shop.shopOffers.Where(o=>o.kind=="skill").ToArray())
            {
                string owner=TacticalRules.SkillRecipient(shop,offer.skillId).id;string wrong=squad.First(id=>id!=owner);
                var bought=Act(shop,new TacticalRequest{type="buy",choice=offer.id,unitId=wrong});OwnershipChanges(shop,bought);
                Check(bought.gold==shop.gold-offer.price&&bought.shopOffers.Single(o=>o.id==offer.id).sold,"automatic purchase charges saved price and marks its own offer sold");shop=bought;
            }
            var reset=Act(shop,new TacticalRequest{type="rerollShop"});Check(reset.shopOffers.Where(o=>o.kind=="skill").All(o=>squad.Contains(o.heroId)&&!o.sold),"reroll stays in the current party and refreshes skill sold flags");
            Reject(reset,s=>s["shopOffers"][2]["heroId"]="ruanzhuo","party shelf rejects forged origin");
            Reject(reset,s=>s["shopOffers"][0]["price"]=1,"party shelf rejects forged price");
            Reject(reset,s=>s["shopRevision"]=3,"unknown shelf policy is rejected");
        }
    }
    static void OldShelves()
    {
        foreach(string name in new[]{"tactical-rev1-published-shop.json","tactical-rev2-published-shop.json","tactical-rev3-historical-shop-text.json"})
        {
            Check(TacticalCodec.TryDecode(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",name)),out var state,out var error),"old shelf loads without regeneration: "+error);
            Check(state.shopRevision==0,"missing policy is interpreted as the original shelf");
            state.gold=1000;var foreign=state.shopOffers.FirstOrDefault(o=>o.kind=="skill"&&!state.squad.Contains(o.heroId));
            if(foreign!=null){string before=TacticalCodec.Encode(state);Check(!TacticalRules.Preview(state,new TacticalRequest{type="buy",choice=foreign.id,unitId=state.squad[0]}).ok&&TacticalCodec.Encode(state)==before,"old inapplicable skill cannot charge gold or cross-teach");}
            var left=Act(state,new TacticalRequest{type="leaveShop"});Check(left.gold==state.gold&&left.shopOffers.Count==0&&left.shopRevision==0,"an old inapplicable shop can be left without resetting currency");
            var relic=state.shopOffers.Single(o=>o.kind=="relic");if(TacticalRules.Preview(state,new TacticalRequest{type="buy",choice=relic.id}).ok)state=Act(state,new TacticalRequest{type="buy",choice=relic.id});
            var beforeJson=JObject.Parse(TacticalCodec.Encode(state))["run"];var supplies=beforeJson["shopOffers"].Where(o=>(string)o["kind"]!="skill").OrderBy(o=>(string)o["kind"]).ToArray();
            var rerolled=Act(state,new TacticalRequest{type="rerollShop"});var afterJson=JObject.Parse(TacticalCodec.Encode(rerolled))["run"];
            Check(rerolled.ascentRevision==state.ascentRevision&&rerolled.shopRevision==4&&rerolled.gold==state.gold-TacticalRules.ShopRerollCost(state),"old reroll updates only shelf policy and charges its visible price");
            Check(JToken.DeepEquals(beforeJson["mapNodes"],afterJson["mapNodes"]),"old reroll preserves the archived route");
            Check(supplies.Zip(afterJson["shopOffers"].Where(o=>(string)o["kind"]!="skill").OrderBy(o=>(string)o["kind"]),(a,b)=>JToken.DeepEquals(a,b)).All(x=>x),"old non-skill stock retains identity, presentation, price and sold flags");
            Check(rerolled.shopOffers.Where(o=>o.kind=="skill").Select(o=>o.heroId).OrderBy(x=>x).SequenceEqual(state.squad.OrderBy(x=>x)),"old reroll now offers only the deployed trio");
            Reject(rerolled,s=>s["shopOffers"][0]["relicId"]="anchorhammer","compatibility does not admit an arbitrary retained relic");
        }
        // No archived revision-zero shop exists in the QA snapshots. Build only its
        // frozen legacy shelf on the published revision-one route to check migration.
        Check(TacticalCodec.TryDecode(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","tactical-rev1-published-shop.json")),out var oldest,out _),"original route fixture is readable for the revision-zero boundary");
        oldest.ascentRevision=0;oldest.shopRevision=0;oldest.shopRerolls=0;oldest.gold=1000;
        var factory=typeof(TacticalRules).GetMethod("CreateLegacyShopOffers",BindingFlags.NonPublic|BindingFlags.Static);
        oldest.shopOffers=(List<TacticalShopOffer>)factory.Invoke(null,new object[]{oldest,TacticalRules.CurrentNode(oldest)});oldest=RoundTrip(oldest);
        var oldSupplies=oldest.shopOffers.Where(o=>o.kind!="skill").OrderBy(o=>o.kind).Select(o=>JObject.FromObject(o)).ToArray();
        var migrated=Act(oldest,new TacticalRequest{type="rerollShop"});
        Check(migrated.ascentRevision==0&&migrated.shopRevision==4&&migrated.shopOffers.Count==5&&migrated.gold==985,"original three-slot shop can upgrade to party stock without rewriting its journey");
        Check(oldSupplies.Zip(migrated.shopOffers.Where(o=>o.kind!="skill").OrderBy(o=>o.kind),(a,b)=>JToken.DeepEquals(a,JObject.FromObject(b))).All(x=>x),"revision-zero supplies keep their original IDs, prices and positions by kind");
        Check(migrated.shopOffers.Where(o=>o.kind=="skill").Select(o=>o.heroId).OrderBy(x=>x).SequenceEqual(oldest.squad.OrderBy(x=>x)),"revision-zero reroll has exactly the deployed trio's techniques");
    }
    static TacticalState Rewards(string kind,string[] squad)
    {
        var battle=Node(kind,squad);battle.relics=TacticalContent.Relics.Where(r=>TacticalRules.RelicAllowedForParty(battle,r.id)).Select(r=>r.id).ToList();
        foreach(var foe in battle.units.Where(u=>u.team=="enemy")){foe.hp=0;foe.block=0;foe.ap=0;}
        return Act(RoundTrip(battle),new TacticalRequest{type="endTurn"});
    }
    static void DropsAndAwards()
    {
        foreach(var squad in new[]{WaterParty,FireParty})foreach(string kind in new[]{"battle","elite"})
        {
            var reward=Rewards(kind,squad);
            Check(reward.rewards.All(r=>r.kind!="relic"||TacticalRules.RelicAllowedForParty(reward,r.relicId)),"exhausted allowed relics never fall back to an absent hero's exclusive item");
            Check(reward.rewards.Where(r=>r.kind=="skill").All(r=>TacticalRules.CanAcquireSkill(reward,r.skillId)),"reward skills always have an eligible deployed owner");
            var skill=reward.rewards.FirstOrDefault(r=>r.kind=="skill");if(skill!=null){var after=Act(reward,new TacticalRequest{type="reward",choice=skill.id});OwnershipChanges(reward,after);}
            else Check(reward.rewards.All(r=>r.kind=="gold"),"elite reward pool uses gold when eligible relics are exhausted");
        }
        var old=Rewards("battle",WaterParty);old.rewards[0]=new TacticalReward{id=old.rewards[0].id,kind="skill",name="旧烈山技能",text="旧档已生成的奖励",skillId="ember",relicId=""};
        old=RoundTrip(old);var card=TacticalRules.EffectiveReward(old,old.rewards[0]);Check(card.kind=="gold"&&card.amount==40&&old.rewards[0].kind=="skill","old unusable reward is visibly redeemable without rewriting saved cards");
        var paid=Act(old,new TacticalRequest{type="reward",choice=card.id});Check(paid.gold==old.gold+40&&paid.phase=="map","old unusable reward cannot trap progression or silently vanish");
    }
    static void EventsAndCamp()
    {
        var camp=Node("camp",WaterParty);var studied=Act(camp,new TacticalRequest{type="camp",choice="study"});OwnershipChanges(camp,studied);
        foreach(string id in new[]{"dream","dreamshop","archiveseal","genealogy"})
        {
            var squad=id=="genealogy"?new[]{"lingfeng","sixuan","cangling"}:WaterParty;
            var state=Node("event",squad,id);var result=Act(state,new TacticalRequest{type="event",choice=id=="dream"?"C":"A"});OwnershipChanges(state,result);
        }
        foreach(string id in new[]{"witness","nailreturn"})
        {
            var state=Node("event",WaterParty,id);state.relics=TacticalContent.Relics.Where(r=>TacticalRules.RelicAllowedForParty(state,r.id)&&r.id!="brasshorn").Select(r=>r.id).ToList();
            var result=Act(RoundTrip(state),new TacticalRequest{type="event",choice=id=="witness"?"B":"A"});
            Check(result.relics.Except(state.relics).SequenceEqual(new[]{"brasshorn"}),"event random relic grant excludes absent owners in "+id);
        }
    }
    static void Main(){Recipients();Shops();OldShelves();DropsAndAwards();EventsAndCamp();Console.WriteLine("PASS: "+checks+" focused acquisition and shelf compatibility assertions.");}
}
