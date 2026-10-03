using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Dicebound.Tactics;
using Dicebound.Persistence;
using Newtonsoft.Json.Linq;
class ProgressionChecks
{
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static bool LeadsTo(TacticalState state,TacticalMapNode node,string target)
    {return node.id==target||node.next.Any(id=>LeadsTo(state,state.mapNodes.Single(n=>n.id==id),target));}
    static TacticalState Reach(uint seed,string kind)
    {
        var state=TacticalRules.NewAscent(seed,new[]{"sixuan","lingfeng","cangling"});
        string target=state.mapNodes.First(n=>n.kind==kind).id;
        for(int i=0;i<2000&&state.currentNodeId!=target;i++){
            var request=state.phase=="map"?new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(state).First(n=>LeadsTo(state,n,target)).id}:TacticalRules.Suggest(state);
            Check(request!=null&&TacticalRules.Preview(state,request).ok,"reachable path remains playable");
            state=RoundTrip(TacticalRules.Act(state,request));Check(state.phase!="defeat","early progression reaches selected encounter");
        }
        Check(state.currentNodeId==target,"selected encounter reached through connected nodes");return state;
    }
    static void Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--player-end-turn")
        {
            PlayerEndTurnRetaliation();Console.WriteLine("PASS: "+checks+" Player end-turn checkpoint assertions.");return;
        }
        if(args.Length==1&&args[0]=="--shop-text")
        {
            ShopPresentationCompatibility();Console.WriteLine("PASS: "+checks+" shop presentation checkpoint assertions.");return;
        }
        PublishedContentCompatibility();
        PlayerEndTurnRetaliation();
        ShopPresentationCompatibility();
        if(args.Length>0){foreach(string path in args)LegacyCheckpoint(path);Console.WriteLine("PASS: "+checks+" original-v3 checkpoint assertions across "+args.Length+" archived QA saves.");return;}
        var shop=Reach(71,"shop");
        var skills=shop.shopOffers.Where(o=>o.kind=="skill").ToArray();
        Check(skills.Length==3,"each shop offers three character skills");
        Check(skills.Select(o=>TacticalContent.GetSkill(o.skillId).heroId).Distinct().Count()==3,"three shop skills have distinct character origins");
        Check(skills.All(o=>TacticalContent.GetSkill(o.skillId).resource!="charge"),"shop skills exclude ultimates");
        Check(skills.All(o=>o.heroId==TacticalContent.GetSkill(o.skillId).heroId),"stock saves each skill origin for display");
        shop.gold=1000;
        var offer=skills.First(o=>TacticalRules.CanAcquireSkill(shop,o.skillId));
        var recipient=TacticalRules.SkillRecipient(shop,offer.skillId);
        var other=shop.units.First(u=>u.team=="hero"&&u.id!=recipient.id);
        int beforeLevel=recipient.skillLevels.TryGetValue(offer.skillId,out int level)?level:0;
        var purchased=RoundTrip(TacticalRules.Act(shop,new TacticalRequest{type="buy",choice=offer.id,unitId=other.id}));
        Check(purchased.gold==960&&TacticalRules.FindUnit(purchased,recipient.id).skillLevels[offer.skillId]==beforeLevel+1,"purchase automatically grants its own hero a real skill level at the saved price");
        Check(purchased.shopOffers.Single(o=>o.id==offer.id).sold,"automatic skill sale persists");
        var relic=purchased.shopOffers.Single(o=>o.kind=="relic");
        purchased=RoundTrip(TacticalRules.Act(purchased,new TacticalRequest{type="buy",choice=relic.id}));
        int gold=purchased.gold;
        var rerolled=RoundTrip(TacticalRules.Act(purchased,new TacticalRequest{type="rerollShop"}));
        Check(rerolled.gold==gold-15&&rerolled.shopRerolls==1,"first reroll charges exactly 15 gold and saves its count");
        Check(rerolled.shopOffers.Single(o=>o.kind=="relic").sold,"reroll retains the relic sold state");
        Check(rerolled.shopOffers.Where(o=>o.kind=="skill").All(o=>!o.sold)&&rerolled.shopOffers.Where(o=>o.kind=="skill").Select(o=>o.heroId).Distinct().Count()==3,"reroll supplies three fresh different origins");
        Check(TacticalRules.ShopRerollCost(rerolled)==20,"next reroll has an explicit increasing gold price");
        Check(TacticalCodec.Encode(TacticalRules.Act(purchased,new TacticalRequest{type="rerollShop"}))==TacticalCodec.Encode(rerolled),"reroll is deterministic from saved node, seed and count");
        var poor=rerolled.Clone();poor.gold=19;string encoded=TacticalCodec.Encode(poor);
        var preview=TacticalRules.Preview(poor,new TacticalRequest{type="rerollShop"});
        Check(!preview.ok&&preview.cost==20&&preview.resource=="gold"&&preview.reason.Contains("金币不足"),"insufficient reroll gold has an exact resource feedback");
        Check(TacticalCodec.Encode(poor)==encoded,"rejected reroll leaves checkpoint unchanged");
        Reject(rerolled,s=>s["shopRerolls"]="1","reroll count cannot be coerced from string");
        Reject(rerolled,s=>s["shopRerolls"]=2,"saved count must match generated shelf");
        Reject(rerolled,s=>s["shopOffers"][2]["heroId"]="ruanzhuo","skill origin cannot be replaced");
        Reject(rerolled,s=>s["units"][0]["turnEnded"]="false","saved character end-turn flag must remain boolean");
        Reject(rerolled,s=>((JObject)s["units"][0]).Remove("turnEnded"),"current ascent cannot lose its end-turn state");
        Reject(rerolled,s=>s["units"][0]["weakened"]=-1,"negative status duration is corruption");
        Reject(rerolled,s=>s["units"][0]["regeneration"]=31,"saved healing status is bounded");
        var left=RoundTrip(TacticalRules.Act(rerolled,new TacticalRequest{type="leaveShop"}));
        Check(left.shopRerolls==0&&left.shopOffers.Count==0,"leaving the shop clears only current stock/reroll state");
        UpgradePurchase(purchased);
        Journey(173,new[]{"sixuan","cangling","shangshuo"});
        Journey(71,new[]{"sixuan","lingfeng","cangling"});
        DepthAll();
        ExpandedPersistence();
        Console.WriteLine("PASS: "+checks+" progression assertions.");
    }
    static void PublishedContentCompatibility()
    {
        foreach(string name in new[]{"tactical-rev1-published-shop.json","tactical-rev2-published-shop.json","tactical-rev2-published-victory.json"})
        {
            string path=Path.Combine(AppContext.BaseDirectory,"Fixtures",name),text=File.ReadAllText(path);
            Check(TacticalCodec.TryDecode(text,out var state,out var error),"published checkpoint remains readable: "+name+" / "+error);
            var original=JObject.Parse(text)["run"];
            Check(JToken.DeepEquals(original["mapNodes"],JObject.Parse(TacticalCodec.Encode(state))["run"]["mapNodes"]),"published route is not regenerated: "+name);
            if(state.phase!="shop")continue;
            Check(JToken.DeepEquals(original["shopOffers"],JObject.Parse(TacticalCodec.Encode(state))["run"]["shopOffers"]),"published shelves are kept exactly: "+name);
            Reject(state,s=>s["shopOffers"][0]["price"]=1,"published prices cannot be forged");
            var rerolled=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="rerollShop"}));
            Check(rerolled.ascentRevision==state.ascentRevision&&rerolled.shopRerolls==1,"published shops reroll within their saved content revision");
        }
    }
    static void ShopPresentationCompatibility()
    {
        string text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","tactical-rev3-historical-shop-text.json"));
        var original=JObject.Parse(text);
        Check(TacticalCodec.TryDecode(text,out var state,out var error),"historical revision-three shop text remains readable: "+error);
        var skill=state.shopOffers.Single(o=>o.skillId=="verdictstrike");
        Check((string)original["run"]["shopOffers"][3]["text"]!=skill.text,"the fixture retains the actual earlier skill description");
        Check(skill.text==TacticalContent.GetSkill(skill.skillId).text+" 可指定同行者领悟；已掌握时升一级，最高三级。","restored shop displays current skill content");
        Check(skill.sold,"the historical sold offer stays sold");
        var expected=(JObject)original["run"].DeepClone();expected["shopRevision"]=0;
        for(int i=0;i<state.shopOffers.Count;i++){expected["shopOffers"][i]["name"]=state.shopOffers[i].name;expected["shopOffers"][i]["text"]=state.shopOffers[i].text;}
        Check(JToken.DeepEquals(expected,JObject.Parse(TacticalCodec.Encode(state))["run"]),"only presentation fields and the explicit legacy shelf policy can refresh; route, price, stock and resources persist exactly");
        var earlierTitle=(JObject)original.DeepClone();earlierTitle["run"]["shopOffers"][3]["name"]="早期标题：定谳一击";
        Check(TacticalCodec.TryDecode(earlierTitle.ToString(),out var renamed,out _),"a bounded historical display title does not replace the verified offer identity");
        Check(renamed.shopOffers[3].name==TacticalContent.GetSkill("verdictstrike").name,"historical titles are never rendered as current offer names");
        RoundTrip(state);
        Reject(state,s=>s["shopOffers"][3]["price"]=1,"presentation compatibility cannot change a quoted price");
        Reject(state,s=>s["shopOffers"][3]["id"]="forged-offer","presentation compatibility cannot change offer identity");
        Reject(state,s=>s["shopOffers"][3]["kind"]="relic","presentation compatibility cannot change offer kind");
        Reject(state,s=>s["shopOffers"][3]["skillId"]="ignite","presentation compatibility cannot change the skill");
        Reject(state,s=>s["shopOffers"][0]["relicId"]="bell","presentation compatibility cannot change the relic");
        Reject(state,s=>s["shopOffers"][3]["heroId"]="lingfeng","presentation compatibility cannot change the originating hero");
        Reject(state,s=>s["shopOffers"][3]["sold"]="true","sold state must remain a typed boolean");
        Reject(state,s=>((JArray)s["shopOffers"]).RemoveAt(4),"presentation compatibility cannot remove stock");
        Reject(state,s=>s["shopOffers"][3]["text"]=new string('x',5001),"historical presentation text remains bounded");
    }
    static void PlayerEndTurnRetaliation()
    {
        string text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","tactical-rev3-player-endturn-plated.json"));
        Check(TacticalCodec.TryDecode(text,out var state,out var error),"real Player end-turn checkpoint remains readable: "+error);
        Check(state.ascentRevision==3&&state.floor==8&&state.revision==128,"regression uses the original failing Player checkpoint");
        string before=TacticalCodec.Encode(state);
        var actors=state.units.Where(u=>u.team=="hero"&&u.hp>0&&!u.turnEnded).ToArray();
        Check(actors.Length>0,"Player checkpoint has an unfinished living actor");
        foreach(var actor in actors)
        {
            var request=new TacticalRequest{type="endTurn",unitId=actor.id};
            Check(TacticalRules.Preview(state,request).ok,"every unfinished actor has a legal end-turn request: "+actor.id);
            var next=TacticalRules.Act(state,request);
            var plated=TacticalRules.FindUnit(next,"enemy-3-0");
            Console.WriteLine("PLAYER_END_TURN actor="+actor.id+" round="+next.round+" platedHp="+plated.hp+" platedBlock="+plated.block+" valid="+TacticalValidation.Valid(next));
            Check(plated.hp==0&&plated.block==0,"a plated enemy killed by retaliation cannot regain shield at next round start");
            Check(next.revision==state.revision+1&&TacticalCodec.Encode(state)==before,"end-turn commits once without changing the previous checkpoint");
            RoundTrip(next);
            Reject(next,s=>s["units"][3]["block"]=6,"a defeated plated enemy with shield remains an invalid checkpoint");

            var surviving=state.Clone();foreach(var hero in surviving.units.Where(u=>u.team=="hero"))hero.retaliation=0;
            RoundTrip(surviving);
            var withoutRetaliation=TacticalRules.Act(surviving,request);var alive=TacticalRules.FindUnit(withoutRetaliation,plated.id);
            Check(alive.hp>0&&alive.block==6,"a surviving plated enemy still gains its promised six shield at round start");
            RoundTrip(withoutRetaliation);
        }
    }
    static TacticalState ExpandedFixture(Func<TacticalState,bool> wanted,uint seed=71,int trial=0)
    {
        var s=TacticalRules.NewAscent(seed,new[]{"sixuan","lingfeng","cangling"},3,trial);
        for(int step=0;step<100&&s.phase!="victory";step++)
        {
            if(wanted(s))return RoundTrip(s);
            TacticalRequest request;
            if(s.phase=="map")request=new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(s).OrderByDescending(n=>n.kind=="battle").First().id};
            else if(s.phase=="battle")
            {
                // 此夹具只覆盖各楼层生成后的存档边界；战斗策略由完整旅程检查覆盖。
                foreach(var enemy in s.units.Where(u=>u.team=="enemy")){enemy.hp=0;enemy.ap=0;enemy.block=0;}
                request=new TacticalRequest{type="endTurn"};
            }
            else request=TacticalRules.Suggest(s);
            s=RoundTrip(TacticalRules.Act(s,request));
        }
        return null;
    }
    static void ExpandedPersistence()
    {
        var first=ExpandedFixture(s=>s.phase=="battle");
        first.relics=TacticalContent.Relics.Select(r=>r.id).ToList();
        Check(first.relics.Count>6,"expanded catalogue genuinely exceeds six relics");
        RoundTrip(first);
        Reject(first,s=>((JArray)s["relics"]).Add("bell"),"duplicate relic is still corruption");
        var target=first.units.First(u=>u.team=="enemy");target.scorched=18;
        RoundTrip(first);
        Reject(first,s=>s["units"][3]["scorched"]=19,"furnace scorch remains bounded at 18");
        var withoutFurnace=first.Clone();withoutFurnace.relics.Remove("emberfurnace");
        Check(!TacticalValidation.Valid(withoutFurnace),"scorch above 12 requires the furnace relic");
        var fifth=ExpandedFixture(s=>s.phase=="battle"&&s.units.Count==8,71,5);
        Check(fifth!=null&&fifth.units.Count(u=>u.team=="enemy")==5,"trial five supports a persisted five-enemy encounter");
        TacticalState rusted=null;
        for(uint seed=1;seed<=20&&rusted==null;seed++)rusted=ExpandedFixture(s=>s.phase=="battle"&&s.units.Any(u=>TacticalContent.HasAffix(u,"rusted")),seed);
        Check(rusted!=null,"seeded route reaches a real rusted enemy");
        foreach(var tile in rusted.terrain)tile.kind="plain";rusted.objects.Clear();
        var hero=rusted.units.First(u=>u.heroId=="sixuan");hero.x=2;hero.y=2;
        var foe=rusted.units.First(u=>TacticalContent.HasAffix(u,"rusted"));foe.x=3;foe.y=2;foe.hp=1;foe.block=0;
        rusted.objects.Add(new TacticalObject{id="object-"+rusted.chapter+"-0",kind="valve",x=2,y=3,hp=1});
        RoundTrip(rusted);
        var slain=RoundTrip(TacticalRules.Act(rusted,new TacticalRequest{type="skill",unitId=hero.id,skillId="strike",x=foe.x,y=foe.y}));
        var dead=TacticalRules.FindUnit(slain,foe.id);
        Check(dead.hp==0&&!TacticalContent.HasAffix(dead,"rusted")&&TacticalRules.TerrainAt(slain,foe.x,foe.y)=="rubble","rusted death settles into a readable rubble checkpoint");
        var breakValve=new TacticalRequest{type="skill",unitId=hero.id,skillId="strike",x=2,y=3};
        Check(TacticalRules.Preview(slain,breakValve).ok,"a second legal strike can break the nearby valve after a rusted death");
        var mist=TacticalRules.Act(slain,breakValve);
        Check(mist.objects.Count==0&&TacticalRules.TerrainAt(mist,foe.x,foe.y)=="mist"&&TacticalRules.FindUnit(mist,foe.id).hp==0,"the valve can change a consumed rusted corpse's rubble to mist");
        mist=RoundTrip(mist);
        var forged=mist.Clone();var forgedEnemy=TacticalRules.FindUnit(forged,foe.id);forgedEnemy.affixes=forgedEnemy.affixes=="swift"?"binding":"swift";
        Check(!TacticalValidation.Valid(forged),"a dead enemy cannot replace its remaining seeded affixes after the terrain changes");
        var living=rusted.Clone();foe=TacticalRules.FindUnit(living,foe.id);foe.affixes=string.Join("+",foe.affixes.Split('+').Where(v=>v!="rusted"));
        Check(!TacticalValidation.Valid(living),"a living enemy cannot erase its seeded affix");
        Reject(first,s=>((JObject)s).Remove("trial"),"expanded trial state cannot silently disappear");
        Reject(first,s=>((JObject)s["units"][3]).Remove("affixes"),"expanded affix state cannot silently disappear");
        TacticalState discount=null;
        for(uint seed=1;seed<=200&&discount==null;seed++)discount=ExpandedFixture(s=>s.phase=="shop"&&!s.relics.Contains("hotnetmap")&&s.shopOffers.Any(o=>o.relicId=="hotnetmap"),seed);
        Check(discount!=null,"seeded merchant offers a real discount relic");
        discount.gold=1000;var offer=discount.shopOffers.Single(o=>o.relicId=="hotnetmap");
        var prices=discount.shopOffers.Select(o=>o.price).ToArray();
        var bought=RoundTrip(TacticalRules.Act(discount,new TacticalRequest{type="buy",choice=offer.id}));
        Check(bought.relics.Contains("hotnetmap")&&bought.shopOffers.Select(o=>o.price).SequenceEqual(prices),"buying a discount relic preserves the quoted current shelf");
        var rerolled=RoundTrip(TacticalRules.Act(bought,new TacticalRequest{type="rerollShop"}));
        Check(rerolled.shopOffers.Single(o=>o.relicId=="hotnetmap").sold,"reroll after a discount purchase keeps the old relic sold");
    }
    static TacticalState RoundTrip(TacticalState state)
    {string encoded=TacticalCodec.Encode(state);Check(TacticalCodec.TryDecode(encoded,out var restored,out _)&&TacticalCodec.Encode(restored)==encoded,"expanded shop and combat checkpoint restores exactly");return restored;}
    static void Reject(TacticalState state,Action<JObject> mutation,string message)
    {var raw=JObject.Parse(TacticalCodec.Encode(state));mutation((JObject)raw["run"]);Check(!TacticalCodec.TryDecode(raw.ToString(),out _,out _),message);}
    static void LegacyCheckpoint(string path)
    {
        string text=File.ReadAllText(path);var run=(JObject)JObject.Parse(text)["run"];
        Check(TacticalCodec.TryDecode(text,out var state,out _),"actual original-v3 QA checkpoint remains readable: "+path);
        Check(state.ascentRevision==0,"old checkpoint retains its original ascent curve");
        Check(state.phase==(string)run["phase"]&&state.revision==(int)run["revision"]&&state.seed==(uint)run["seed"],"decode preserves phase, revision and seed");
        foreach(JObject saved in (JArray)run["units"]){var unit=state.units.Single(u=>u.id==(string)saved["id"]);Check(unit.hp==(int)saved["hp"]&&unit.sp==(int)saved["sp"]&&unit.x==(int)saved["x"]&&unit.y==(int)saved["y"]&&unit.skills.SequenceEqual(saved["skills"].Values<string>()),"decode preserves actual battle health, position and build");}
        RoundTrip(state);
    }
    static void Journey(uint seed,string[] heroes)
    {
        var state=TacticalRules.NewAscent(seed,heroes);int steps=0,shops=0,elites=0,bought=0;string elite="floor-7-1";
        while(state.phase!="victory"&&state.phase!="defeat"&&steps<3000){
            TacticalRequest request;
            if(state.phase=="map"){
                var nodes=TacticalRules.AvailableNodes(state).Where(n=>state.floor>=7||LeadsTo(state,n,elite)).ToArray();
                var node=nodes.FirstOrDefault(n=>n.kind=="shop")??nodes.FirstOrDefault(n=>n.kind=="event")??nodes.FirstOrDefault(n=>n.kind=="elite")??nodes[0];
                request=new TacticalRequest{type="node",choice=node.id};
            }else request=TacticalRules.Suggest(state);
            Check(request!=null&&TacticalRules.Preview(state,request).ok,"new curve continuation remains legal");
            string before=TacticalCodec.Encode(state);var next=RoundTrip(TacticalRules.Act(state,request));
            Check(before==TacticalCodec.Encode(state)&&next.revision==state.revision+1,"new curve action preserves its previous checkpoint and commits once");
            if(request.type=="buy")bought++;
            if(state.phase=="map"&&next.phase=="shop")shops++;
            if(state.phase=="map"&&next.phase=="battle"){
                var enemies=next.units.Where(u=>u.team=="enemy").ToArray();
                if(next.nodeKind=="elite"){elites++;Check(enemies.Any(u=>u.enemyType=="elite"&&TacticalStory.GetEnemy(u.heroId).rank=="elite"),"elite encounters contain an independent elite archetype");}
                if(next.nodeKind=="boss"){
                    Check(enemies.Count(u=>u.heroId=="sunwheel")==1&&enemies.All(u=>u.enemyType!="normal")&&enemies.Single(u=>u.heroId=="sunwheel").maxHp==72,"final encounter has the independent boss and elite threats");
                    EnemyMechanics(next);
                }
                if(next.floor==0)Check(enemies.Length==2&&enemies.All(u=>u.maxHp==14&&u.attackPower==5),"first layer introduces two low-pressure enemies");
                if(next.floor>=7&&next.nodeKind=="battle")Check(enemies.Length==4&&enemies.All(u=>u.maxHp>=21&&u.attackPower>=7),"upper floors increase population, health and attack pressure");
            }
            state=next;steps++;
        }
        Console.WriteLine("JOURNEY seed="+seed+" squad="+string.Join(",",heroes)+" phase="+state.phase+" floor="+state.floor+" steps="+steps+" shops="+shops+" elites="+elites+" buys="+bought);
        Check(state.phase=="victory"&&state.floor==11,"a constructed squad completes the new twelve-layer curve: "+seed);
        Check(shops>0&&elites>0&&bought>0,"complete journey exercises merchant construction, elite and boss");
    }
    static TacticalState Arena(TacticalState boss)
    {
        var state=boss.Clone();state.round=2;state.objects.Clear();
        foreach(var cell in state.terrain)cell.kind="plain";
        int[] xs={3,4,1},ys={2,2,1};
        for(int i=0;i<3;i++){var u=TacticalRules.FindUnit(state,state.squad[i]);u.x=xs[i];u.y=ys[i];u.hp=u.maxHp;u.ap=u.maxAp;u.block=0;u.turnEnded=false;u.rooted=false;u.weakened=0;u.vulnerable=0;u.retaliation=0;u.regeneration=0;}
        foreach(var enemy in state.units.Where(u=>u.team=="enemy")){enemy.x=enemy.heroId=="sunwheel"?3:10;enemy.y=enemy.heroId=="sunwheel"?3:enemy.heroId=="mirrorweaver"?8:7;enemy.rooted=true;}
        return RoundTrip(state);
    }
    static TacticalState EndAll(TacticalState state)
    {foreach(string id in state.squad.ToArray())state=RoundTrip(TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId=id}));return state;}
    static void EnemyMechanics(TacticalState boss)
    {
        var arena=Arena(boss);string checkpoint=TacticalCodec.Encode(arena);
        var intent=TacticalRules.EnemyIntents(arena).Single(i=>TacticalRules.FindUnit(arena,i.unitId).heroId=="sunwheel");
        Check(intent.text.Contains("曜轮横扫")&&intent.damage==11,"boss intent discloses its alternating area attack and exact target damage");
        Check(TacticalCodec.Encode(arena)==checkpoint,"inspecting enemy intent never settles its attack");
        var after=EndAll(arena);
        Check(after.round==3,"boss acts after the whole squad has individually ended");
        for(int i=0;i<2;i++){var prior=TacticalRules.FindUnit(arena,arena.squad[i]);var changed=TacticalRules.FindUnit(after,arena.squad[i]);Check(changed.hp==prior.hp-11&&changed.vulnerable==1,"boss sweep actually damages adjacent allies and persists next-round vulnerability");}
        Check(TacticalRules.FindUnit(after,arena.squad[2]).hp==TacticalRules.FindUnit(arena,arena.squad[2]).hp,"spacing prevents boss splash on the distant ally");
        after=EndAll(after);
        Check(after.units.Single(u=>u.heroId=="sunwheel").block==8,"boss third-round action actually generates its independent shield mechanic");
        var mirror=Arena(boss);var wheel=mirror.units.Single(u=>u.heroId=="sunwheel");wheel.x=11;wheel.y=9;
        var weaver=mirror.units.Single(u=>u.heroId=="mirrorweaver");weaver.x=4;weaver.y=3;
        var mirrorIntent=TacticalRules.EnemyIntents(mirror).Single(i=>i.unitId==weaver.id);
        var mirrorAfter=EndAll(mirror);var weak=TacticalRules.FindUnit(mirrorAfter,mirrorIntent.targetId);
        Check(weak.weakened==1&&weak.hp<TacticalRules.FindUnit(mirror,weak.id).hp,"independent mirror elite inflicts damaging next-round weakness");
        var tide=Arena(boss);wheel=tide.units.Single(u=>u.heroId=="sunwheel");wheel.x=11;wheel.y=9;
        var judge=tide.units.Single(u=>u.heroId=="tidejudge");judge.x=4;judge.y=3;
        var tideIntent=TacticalRules.EnemyIntents(tide).Single(i=>i.unitId==judge.id);var tideAfter=EndAll(tide);var rooted=TacticalRules.FindUnit(tideAfter,tideIntent.targetId);
        Check(rooted.rooted&&TacticalRules.MoveCells(tideAfter,rooted.id).Count==0,"independent tide elite restricts the player's next movement after restoring the checkpoint");
    }
    static void UpgradePurchase(TacticalState shop)
    {        shop=shop.Clone();shop.gold=10000;TacticalShopOffer offer=null;TacticalUnit recipient=null;
        for(int roll=0;roll<=20;roll++){
            foreach(var item in shop.shopOffers.Where(o=>o.kind=="skill"&&!o.sold)){
                var skill=TacticalContent.GetSkill(item.skillId);if(skill.power<1||skill.kind=="haste"||skill.kind=="restore")continue;
                recipient=TacticalRules.SkillRecipient(shop,item.skillId);
                if(recipient!=null&&!recipient.skills.Contains(item.skillId))recipient=null;
                if(recipient!=null){offer=item;break;}
            }
            if(offer!=null||roll==20)break;
            shop=RoundTrip(TacticalRules.Act(shop,new TacticalRequest{type="rerollShop"}));
        }
        Check(offer!=null,"rerolled stock can offer an already learned skill for construction upgrades");
        var selected=TacticalContent.GetSkill(offer.skillId);int oldLevel=TacticalRules.SkillLevel(recipient,offer.skillId),oldPower=TacticalRules.SkillPower(recipient,selected),gold=shop.gold;
        var upgraded=RoundTrip(TacticalRules.Act(shop,new TacticalRequest{type="buy",choice=offer.id,unitId=recipient.id}));
        var trained=TacticalRules.FindUnit(upgraded,recipient.id);
        Check(TacticalRules.SkillLevel(trained,offer.skillId)==oldLevel+1&&TacticalRules.SkillPower(trained,selected)>oldPower&&upgraded.gold==gold-40,"buying the same learned skill raises its displayed level and its actual effect power");
        Check(TacticalRules.SkillDescription(upgraded,trained,selected.id).Contains("Lv."+(oldLevel+1)),"merchant upgrade immediately appears in readable skill details");
        trained.skillLevels[offer.skillId]=3;
        Check(!TacticalContent.CanLearn(trained,selected),"level-three skills cannot consume another purchase for no effect");
    }
    // ---- Depth revision 2: three-act route, campfires, gold packages, flawless streak fields. ----
    static TacticalState DepthAscent(uint seed,string[] heroes){return TacticalRules.NewAscent(seed,heroes,2);}
    static TacticalState DepthStep(TacticalState state,TacticalRequest request)
    {
        Check(request!=null&&TacticalRules.Preview(state,request).ok,"depth continuation is legal: "+(request==null?"null":request.type));
        string before=TacticalCodec.Encode(state);var next=TacticalRules.Act(state,request);
        Check(TacticalCodec.Encode(state)==before,"depth settlement leaves previous checkpoint unchanged");
        string encoded=TacticalCodec.Encode(next);
        Check(TacticalCodec.TryDecode(encoded,out var restored,out _)&&TacticalCodec.Encode(restored)==encoded,"depth checkpoint restores exactly");
        return restored;
    }
    static void DepthAll()
    {
        DepthGraph();
        DepthShop(DepthReach(71,"shop"));
        DepthCampAndGold();
        DepthJourney(71,new[]{"sixuan","lingfeng","cangling"});
        DepthJourney(173,new[]{"shangshuo","yanzhuying","cangling"});
    }
    static void DepthGraph()
    {
        var signatures=new HashSet<string>();
        for(uint seed=0;seed<12;seed++){
            var s=DepthAscent(seed,new[]{"sixuan","lingfeng","cangling"});
            Check(s.ascentRevision==2&&s.flawlessStreak==0&&s.pendingShield==0&&s.pendingCharge==0,"depth ascent starts with clean depth fields");
            signatures.Add(string.Join(";",s.mapNodes.Select(n=>n.id+":"+n.kind)));
            foreach(var floor in s.mapNodes.GroupBy(n=>n.floor)){
                if(floor.Key==4||floor.Key==8)Check(floor.All(n=>n.kind=="elite")&&floor.Count()==2,"gate floors hold two elite guardians");
                if(floor.Key==5||floor.Key==9)Check(floor.Count()==2&&floor.Any(n=>n.kind=="shop")&&floor.Any(n=>n.kind=="camp"),"rest floors guarantee a shop and a campfire");
                if(floor.Key==11)Check(floor.Count()==1&&floor.Single().kind=="boss","depth top holds a single boss");
            }
            Check(s.mapNodes.Count(n=>n.kind=="camp")==2,"depth route holds exactly two campfires");
            Check(TacticalRules.Preview(s,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(s)[0].id}).ok,"depth first node is selectable");
            string encoded=TacticalCodec.Encode(s);
            Check(TacticalCodec.TryDecode(encoded,out var restored,out _)&&TacticalCodec.Encode(restored)==encoded,"depth initial map round trip");
        }
        Check(signatures.Count>=10,"depth seeds produce differing routes");
    }
    static TacticalState DepthReach(uint seed,string kind)
    {
        var state=DepthAscent(seed,new[]{"sixuan","lingfeng","cangling"});
        string target=state.mapNodes.First(n=>n.kind==kind).id;
        for(int i=0;i<3000&&state.currentNodeId!=target;i++){
            var request=state.phase=="map"?new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(state).First(n=>LeadsTo(state,n,target)).id}:TacticalRules.Suggest(state);
            Check(request!=null&&TacticalRules.Preview(state,request).ok,"depth reachable path remains playable");
            state=DepthStep(state,request);Check(state.phase!="defeat","depth early progression reaches selected encounter");
        }
        Check(state.currentNodeId==target,"depth selected encounter reached through connected nodes");return state;
    }
    static void DepthShop(TacticalState shop)
    {
        var skills=shop.shopOffers.Where(o=>o.kind=="skill").ToArray();
        Check(skills.Length==3&&skills.All(o=>o.price==45),"depth shop sells skills at 45 gold");
        Check(shop.shopOffers.Single(o=>o.kind=="relic").price==60,"depth shop relic costs 60 gold");
        var heal=shop.shopOffers.Single(o=>o.kind=="heal");
        Check(heal.price==30&&heal.text.Contains("30%"),"depth supply restores 30% of max health");
        var wounded=shop.Clone();foreach(var u in wounded.units.Where(u=>u.team=="hero"))u.hp=u.maxHp/2;
        var bought=DepthStep(wounded,new TacticalRequest{type="buy",choice=heal.id});
        Check(bought.units.Where(u=>u.team=="hero").All(u=>u.hp==u.maxHp/2+u.maxHp*30/100),"depth supply heals exactly 30% of each max health");
        Check(bought.shopOffers.Single(o=>o.kind=="heal").sold,"depth supply sale persists");
    }
    static TacticalState FightToReward(TacticalState state)
    {int guard=0;while(state.phase=="battle"&&guard++<500)state=DepthStep(state,TacticalRules.Suggest(state));return state;}
    static void DepthCampAndGold()
    {
        var s=DepthAscent(71,new[]{"sixuan","lingfeng","cangling"});
        var camp=s.mapNodes.First(n=>n.kind=="camp");
        for(int i=0;i<3000&&s.currentNodeId!=camp.id;i++){
            var request=s.phase=="map"?new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(s).First(n=>LeadsTo(s,n,camp.id)).id}:TacticalRules.Suggest(s);
            s=DepthStep(s,request);Check(s.phase!="defeat","depth path reaches the campfire");
        }
        Check(s.phase=="camp"&&s.nodeKind=="camp","depth campfire opens the rest phase");
        var wounded=s.Clone();foreach(var u in wounded.units.Where(u=>u.team=="hero"))u.hp=1;
        var rested=DepthStep(wounded,new TacticalRequest{type="camp",choice="rest"});
        Check(TacticalRules.CurrentNode(rested).completed&&rested.phase=="map","camp rest completes the node");
        Check(rested.units.Where(u=>u.team=="hero").All(u=>u.hp==1+u.maxHp*35/100),"camp rest heals 35% of max health");
        int banked=s.pendingShield,bankCharge=s.pendingCharge;
        var chargeBefore=s.units.Where(u=>u.team=="hero").Select(u=>u.charge).ToArray();
        var warded=DepthStep(s,new TacticalRequest{type="camp",choice="ward"});
        Check(warded.pendingShield==Math.Min(40,banked+5)&&TacticalRules.CurrentNode(warded).completed,"camp ward stacks onto any banked flawless bonus");
        var current=warded.mapNodes.First(n=>n.id==warded.currentNodeId);
        var nextBattle=warded.mapNodes.First(n=>current.next.Contains(n.id)&&new[]{"battle","elite","boss"}.Contains(n.kind));
        var entered=DepthStep(warded,new TacticalRequest{type="node",choice=nextBattle.id});
        var heroesAfter=entered.units.Where(u=>u.team=="hero").ToList();
        Check(entered.phase=="battle"&&warded.pendingShield>0&&heroesAfter.All(u=>u.block==warded.pendingShield&&u.charge==Math.Min(100,chargeBefore[heroesAfter.IndexOf(u)]+warded.pendingCharge)),"banked shields and charge apply at battle start");
        Check(entered.pendingShield==0&&entered.pendingCharge==0,"start bonuses are consumed once");
        var victory=FightToReward(entered);
        Check(victory.phase=="reward","depth battle reaches rewards");
        var replaced=victory.rewards[0];
        victory.rewards[0]=new TacticalReward{id=replaced.id,kind="gold",name="金币行囊",text="测试用金币行囊。",skillId="",relicId="",amount=40};
        int beforeGold=victory.gold;
        var claimed=DepthStep(victory,new TacticalRequest{type="reward",choice=victory.rewards[0].id,unitId=victory.squad[0]});
        Check(claimed.gold==beforeGold+40&&claimed.phase=="map","gold package reward pays out and completes the node");
    }
    static void DepthJourney(uint seed,string[] heroes)
    {
        var state=DepthAscent(seed,heroes);int steps=0,shops=0,camps=0,gates=0;
        while(state.phase!="victory"&&state.phase!="defeat"&&steps<3000){
            TacticalRequest request;
            if(state.phase=="map"){
                var nodes=TacticalRules.AvailableNodes(state);
                var node=nodes.FirstOrDefault(n=>n.kind=="shop")??nodes.FirstOrDefault(n=>n.kind=="camp")??nodes.FirstOrDefault(n=>n.kind=="elite")??nodes.FirstOrDefault(n=>n.kind=="event")??nodes[0];
                request=new TacticalRequest{type="node",choice=node.id};
                if(node.floor==4||node.floor==8)gates++;
            }else request=TacticalRules.Suggest(state);
            var next=DepthStep(state,request);
            if(state.phase=="map"&&next.phase=="shop")shops++;
            if(state.phase=="map"&&next.phase=="camp")camps++;
            if(next.phase=="battle"&&next.floor==0)Check(next.units.Count(u=>u.team=="enemy")==2,"depth first battle stays gentle");
            Check(next.flawlessStreak>=0&&next.flawlessStreak<=12&&next.pendingShield>=0&&next.pendingShield<=40&&next.pendingCharge>=0&&next.pendingCharge<=100,"depth fields stay bounded");
            state=next;steps++;
        }
        Console.WriteLine("DEPTH seed="+seed+" squad="+string.Join(",",heroes)+" phase="+state.phase+" floor="+state.floor+" steps="+steps+" shops="+shops+" camps="+camps+" gates="+gates);
        Check(state.phase=="victory"&&state.floor==11,"a constructed squad completes the depth twelve-layer curve: "+seed);
        Check(shops+camps>=2,"depth journey rests at a shop or campfire on both rest floors");
        Check(gates==2,"depth journey crosses both gates");
    }
}
