using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Dicebound.Tactics;
using Dicebound.Persistence;
using Newtonsoft.Json.Linq;

static class TacticalChecks
{
    static int assertions,journeys,actions,maxBattleRounds;
    static readonly string[] Roster={"sixuan","lingfeng","cangling"};
    static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    static TacticalState EndPartyTurn(TacticalState state)
    {
        int round=state.round;
        do{var active=TacticalRules.NextActiveHero(state,null);state=TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId=active?.id});}
        while(state.phase=="battle"&&state.round==round);
        return state;
    }
    static void Main(string[] args)
    {
        string root=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"dicebound-tactical-tests");
        MapDimensions();ScenarioRoutes();TerrainRestrictions();LegacyMapPersistence(root);EnemyDetour();ClearEnemyVictory();BattleOpeningRelic();Mechanics();Boundaries();Persistence(root);if(!args.Contains("--focused"))Journeys();
        Console.WriteLine("PASS: "+assertions+" tactical assertions, "+journeys+" complete seeded journeys, "+actions+" immutable save/restore actions, longest battle "+maxBattleRounds+" rounds.");
    }
    static void MapDimensions()
    {
        var s=TacticalRules.NewRun(17,Roster);
        Check(s.terrain.Count==120,"new journey still uses the old 8x8 battlefield");
        var json=JObject.Parse(TacticalCodec.Encode(s));
        Check((int)json["run"]["boardWidth"]==12&&(int)json["run"]["boardHeight"]==10,"new battlefield dimensions not persisted");
        Check(TacticalRules.BoardWidth(s)==12&&TacticalRules.BoardHeight(s)==10&&TacticalRules.Inside(s,11,9)&&!TacticalRules.Inside(s,12,9)&&!TacticalRules.Inside(s,11,10),"rectangular bounds incorrect");
        string text=TacticalCodec.Encode(s);Check(TacticalCodec.TryDecode(text,out var restored,out _)&&text==TacticalCodec.Encode(restored),"expanded battlefield round trip");
    }
    static TacticalState GeneratedChapter(uint seed,int chapter)
    {
        var s=TacticalRules.NewRun(seed,Roster);
        while(s.chapter<chapter){
            s=TacticalRules.Act(s,new TacticalRequest{type="begin"});foreach(var e in s.units.Where(u=>u.team=="enemy"))e.hp=0;
            s=EndPartyTurn(s);s=TacticalRules.Act(s,TacticalRules.Suggest(s));
            var route=s.routes.First(r=>r.kind=="camp"||r.kind=="event");s=TacticalRules.Act(s,new TacticalRequest{type="route",choice=route.id});
            s=TacticalRules.Act(s,new TacticalRequest{type=s.phase,choice=s.phase=="camp"?"rest":"A"});
        }
        return s;
    }
    static void ScenarioRoutes()
    {
        var alley=GeneratedChapter(17,0);
        Check(TacticalRules.TerrainAt(alley,5,4)=="wall"&&TacticalRules.Walkable(alley,5,2)&&TacticalRules.Walkable(alley,5,7),"alley lacks its split narrow approaches");
        var bridge=GeneratedChapter(17,2);
        Check(TacticalRules.TerrainAt(bridge,5,2)=="gap"&&TacticalRules.Walkable(bridge,5,3)&&TacticalRules.Walkable(bridge,5,7),"bridge lacks water separation and alternate crossing");
        for(uint seed=1;seed<=20;seed++)for(int chapter=0;chapter<5;chapter++){
            var s=GeneratedChapter(seed,chapter);Check(TacticalValidation.Valid(s),"generated scene invalid "+seed+"/"+chapter);
            Check(s.terrain.Any(c=>c.kind=="wall"||c.kind=="gap"),"scene has no restrictive terrain");
            Check(TacticalRules.TerrainAt(s,2,2)=="mist"&&TacticalRules.TerrainAt(s,1,3)=="shadow","scene lacks reachable personal ability tiles");
            // Closing either approach must leave the other usable, including fixed object blockers.
            foreach(bool closeUpper in new[]{false,true}){
                var blocked=new HashSet<string>();blocked.Add("5,"+(closeUpper?(chapter==2?3:2):7));if(closeUpper&&chapter==2)blocked.Add("5,4");
                var reachable=Reachable(s,1,1,blocked);
                foreach(var enemy in s.units.Where(u=>u.team=="enemy"))Check(reachable.Contains(enemy.x+","+enemy.y),"alternate scene approach blocked "+seed+"/"+chapter+"/"+closeUpper+"/"+enemy.id);
            }
            var all=Reachable(s,1,1,new HashSet<string>());foreach(var cell in s.terrain.Where(c=>TacticalRules.Walkable(s,c.x,c.y)&&TacticalRules.ObjectAt(s,c.x,c.y)==null))Check(all.Contains(cell.x+","+cell.y),"scene contains isolated walkable ground");
        }
    }
    static HashSet<string> Reachable(TacticalState s,int x,int y,HashSet<string> extraBlocked)
    {
        var found=new HashSet<string>();var queue=new Queue<TacticalCell>();queue.Enqueue(new TacticalCell(x,y));found.Add(x+","+y);
        int[] dx={1,0,-1,0},dy={0,1,0,-1};while(queue.Count>0){var cell=queue.Dequeue();for(int i=0;i<4;i++){int nx=cell.x+dx[i],ny=cell.y+dy[i];string key=nx+","+ny;
            if(!TacticalRules.Inside(s,nx,ny)||new[]{"wall","gap"}.Contains(TacticalRules.TerrainAt(s,nx,ny))||TacticalRules.ObjectAt(s,nx,ny)!=null||extraBlocked.Contains(key)||!found.Add(key))continue;queue.Enqueue(new TacticalCell(nx,ny));}}
        return found;
    }
    static TacticalState ExpandedBattle()
    {
        var s=TacticalRules.Act(TacticalRules.NewRun(17,Roster),new TacticalRequest{type="begin"});foreach(var cell in s.terrain)cell.kind="plain";s.objects.Clear();return s;
    }
    static void TerrainRestrictions()
    {
        var s=ExpandedBattle();var actor=s.units[0];Place(s,actor.id,0,4);var move=new TacticalRequest{type="move",unitId=actor.id,x=4,y=4};
        Check(TacticalRules.MoveBudget(s)==4&&TacticalRules.Preview(s,move).ok,"expanded scene makes ordinary four-step movement impossible");
        foreach(string terrain in new[]{"rubble","pipe"}){
            s.terrain.First(c=>c.x==4&&c.y==4).kind=terrain;Illegal(s,move,terrain+" movement did not consume two steps");
            var shortMove=new TacticalRequest{type="move",unitId=actor.id,x=3,y=4};s.terrain.First(c=>c.x==3&&c.y==4).kind=terrain;
            Check(TacticalRules.Preview(s,shortMove).ok&&TacticalRules.MoveCost(s,3,4)==2,"weighted four-step route wrongly refused");s.terrain.First(c=>c.x==3&&c.y==4).kind="plain";
        }
        s=ExpandedBattle();actor=s.units[0];Place(s,actor.id,1,4);var enemy=s.units.First(u=>u.team=="enemy");Place(s,enemy.id,4,4);
        s.terrain.First(c=>c.x==2&&c.y==4).kind="wall";Illegal(s,Skill(actor,"balance",4,4),"wall did not block ranged sight");
        s.terrain.First(c=>c.x==2&&c.y==4).kind="gap";Check(TacticalRules.Preview(s,Skill(actor,"balance",4,4)).ok,"open gap incorrectly blocks ranged sight");
        Illegal(s,new TacticalRequest{type="move",unitId=actor.id,x=2,y=4},"gap accepts movement");
        Place(s,actor.id,11,6);Place(s,enemy.id,11,9);Check(TacticalRules.SkillTargets(s,actor.id,"balance").Any(c=>c.x==11&&c.y==9),"skill target enumeration still stops at old edges");
        Check(TacticalRules.MoveCells(s,actor.id).Any(c=>c.x==11&&c.y==8),"movement enumeration still stops at old edges");
        s=ExpandedBattle();actor=s.units[1];Place(s,actor.id,2,4);enemy=s.units.First(u=>u.team=="enemy");Place(s,enemy.id,3,4);s.terrain.First(c=>c.x==4&&c.y==4).kind="gap";
        var pushed=TacticalRules.Act(s,Skill(actor,"shove",3,4));Check(TacticalRules.FindUnit(pushed,enemy.id).x==3&&TacticalRules.FindUnit(pushed,enemy.id).hp==enemy.hp-8,"push entered gap instead of collision settlement");
        var shadow=TacticalRules.Act(TacticalRules.NewRun(17,new[]{"yanzhuying","lingfeng","cangling"}),new TacticalRequest{type="begin"});foreach(var cell in shadow.terrain)cell.kind="plain";shadow.objects.Clear();shadow.terrain.First(c=>c.x==1&&c.y==1).kind="shadow";shadow.terrain.First(c=>c.x==2&&c.y==2).kind="gap";
        Illegal(shadow,Skill(shadow.units[0],"step",2,2),"borrow shadow can land in gap");
        for(int y=0;y<10;y++)shadow.terrain.First(c=>c.x==3&&c.y==y).kind="gap";
        Check(TacticalRules.MoveCells(shadow,shadow.units[0].id).All(c=>c.x<3),"movement crossed an impassable gap strip");
        Check(TacticalRules.Preview(shadow,Skill(shadow.units[0],"step",4,1)).ok,"shadow step cannot cross open sight to a walkable landing");
        foreach(string terrain in new[]{"rubble","pipe"}){
            var heat=ExpandedBattle();foreach(var unit in heat.units.Where(u=>u.team=="enemy"))unit.rooted=true;
            var u=heat.units[0];heat.terrain.First(c=>c.x==u.x&&c.y==u.y).kind=terrain;var next=EndPartyTurn(heat);
            Check(next.units[0].hp==u.hp-(terrain=="pipe"?2:0),"terrain hazard damage differs from advertised effect");
        }
    }
    static void LegacyMapPersistence(string root)
    {
        var original=Battle();var json=JObject.Parse(TacticalCodec.Encode(original));((JObject)json["run"]).Remove("boardWidth");((JObject)json["run"]).Remove("boardHeight");string text=json.ToString();
        string directory=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var store=new TacticalStore(directory);File.WriteAllText(store.SavePath,text);
        Check(store.TryLoad(out var s,out _)&&s.boardWidth==8&&s.boardHeight==8&&TacticalRules.MoveBudget(s)==3,"v3 save without dimensions cannot resume as 8x8");
        Check(File.ReadAllText(store.SavePath)==text&&JToken.DeepEquals(JArray.FromObject(s.terrain),json["run"]["terrain"])&&JToken.DeepEquals(JArray.FromObject(s.units),json["run"]["units"]),"loading legacy save changed terrain, coordinates or file");
        Place(s,s.units[0].id,0,4);Illegal(s,new TacticalRequest{type="move",unitId=s.units[0].id,x=4,y=4},"legacy movement budget silently enlarged");
        var moved=TacticalRules.Act(s,new TacticalRequest{type="move",unitId=s.units[0].id,x=1,y=4});Check(store.TrySave(moved,out _)&&store.TryLoad(out s,out _)&&s.boardWidth==8&&s.terrain.Count==64,"resaved legacy current battlefield changed dimensions");
        foreach(var e in s.units.Where(u=>u.team=="enemy"))e.hp=0;s=EndPartyTurn(s);s=TacticalRules.Act(s,TacticalRules.Suggest(s));
        var route=s.routes.First(r=>r.kind=="camp"||r.kind=="event");s=TacticalRules.Act(s,new TacticalRequest{type="route",choice=route.id});s=TacticalRules.Act(s,new TacticalRequest{type=s.phase,choice=s.phase=="camp"?"rest":"A"});
        Check(s.chapter==1&&s.boardWidth==12&&s.boardHeight==10&&s.terrain.Count==120&&TacticalValidation.Valid(s),"next encounter did not upgrade resumed legacy journey");
        var expanded=JObject.Parse(TacticalCodec.Encode(s));foreach(string key in new[]{"boardWidth","boardHeight"})foreach(JToken value in new JToken[]{new JValue("12"),new JValue(12.0),JValue.CreateNull(),new JValue(0),new JValue(999999)}){var damaged=(JObject)expanded.DeepClone();damaged["run"][key]=value;Reject(damaged,"invalid dimension accepted");}
        foreach(string key in new[]{"boardWidth","boardHeight"}){var damaged=(JObject)expanded.DeepClone();((JObject)damaged["run"]).Remove(key);Reject(damaged,"partial dimensions accepted");}
        var wrongShape=(JObject)expanded.DeepClone();wrongShape["run"]["boardWidth"]=10;wrongShape["run"]["boardHeight"]=12;Reject(wrongShape,"transposed dimensions accepted");
        wrongShape=(JObject)expanded.DeepClone();((JObject)wrongShape["run"]).Remove("boardWidth");((JObject)wrongShape["run"]).Remove("boardHeight");Reject(wrongShape,"120 cells without dimensions coerced into legacy board");
        wrongShape=(JObject)json.DeepClone();wrongShape["run"]["terrain"][0]["x"]=8;Reject(wrongShape,"legacy coordinate outside 8x8 accepted");
        wrongShape=(JObject)expanded.DeepClone();wrongShape["run"]["terrain"][0]["kind"]="lava";Reject(wrongShape,"unknown new terrain accepted");
        wrongShape=(JObject)expanded.DeepClone();wrongShape["run"]["terrain"][0]=wrongShape["run"]["terrain"][1].DeepClone();Reject(wrongShape,"duplicate expanded coordinate accepted");
    }
    static void EnemyDetour()
    {
        var s=Battle();Place(s,s.units[0].id,1,4);Place(s,s.units[1].id,0,0);Place(s,s.units[2].id,0,1);
        var enemy=s.units.First(u=>u.team=="enemy");Place(s,enemy.id,3,4);foreach(var other in s.units.Where(u=>u.team=="enemy"&&u!=enemy))other.hp=0;
        for(int y=3;y<=5;y++)s.terrain.First(c=>c.x==2&&c.y==y).kind="wall";
        string before=TacticalCodec.Encode(s);var intent=TacticalRules.EnemyIntents(s).Single();
        Check(intent.path.Count==2&&intent.path[0].x==3&&intent.path[0].y==3&&intent.path[1].x==3&&intent.path[1].y==2,"enemy is stuck when approach first requires moving away from the target");
        var next=EndPartyTurn(s);var arrived=TacticalRules.FindUnit(next,enemy.id);
        Check(arrived.x==intent.x&&arrived.y==intent.y&&before==TacticalCodec.Encode(s),"detour intention differs from immutable settlement");
        s=ExpandedBattle();Place(s,s.units[0].id,1,4);Place(s,s.units[1].id,0,0);Place(s,s.units[2].id,0,1);enemy=s.units.First(u=>u.team=="enemy");Place(s,enemy.id,5,4);foreach(var other in s.units.Where(u=>u.team=="enemy"&&u!=enemy))other.hp=0;
        intent=TacticalRules.EnemyIntents(s).Single();Check(intent.path.Count==3&&intent.x==2&&intent.y==4&&intent.damage>0,"expanded enemy cannot close a four-cell approach in one turn");
        s.terrain.First(c=>c.x==4&&c.y==4).kind="rubble";intent=TacticalRules.EnemyIntents(s).Single();
        Check(intent.path.Sum(c=>TacticalRules.MoveCost(s,c.x,c.y))<=3&&intent.damage==0,"enemy ignored rubble movement budget");
    }
    static void Boundaries()
    {
        var s=Battle();var actor=s.units[0];Check(TacticalContent.CanLearn(actor,TacticalContent.GetSkill("moon")),"cross-character small skill cannot enter a build");Check(!TacticalContent.CanLearn(actor,TacticalContent.GetSkill("yanzhuying_ultimate")),"foreign ultimate lost character identity");Check(!TacticalContent.CanLearn(actor,TacticalContent.GetSkill("shove")),"mage can learn a strength shove");
        var owner=TacticalRules.NewRun(1,new[]{"yanzhuying","ruanzhuo","shangshuo"});owner=TacticalRules.Act(owner,new TacticalRequest{type="begin"});foreach(var cell in owner.terrain)cell.kind="plain";owner.objects.Clear();var shadow=owner.units[0];
        Illegal(owner,Skill(shadow,"step",0,1),"borrow shadow without shadow");owner.terrain.First(cell=>cell.x==0&&cell.y==1).kind="shadow";shadow.sp=0;var stepped=TacticalRules.Act(owner,Skill(shadow,"step",0,1));Check(stepped.units[0].sp==1&&stepped.units[0].x==0,"borrow shadow missing SP recovery");
        s=Battle();actor=s.units[0];var enemy=s.units.First(u=>u.team=="enemy");Place(s,enemy.id,1,3);var repeat=Skill(actor,"balance",1,3);var one=TacticalRules.Act(s,repeat);Check(TacticalRules.Preview(one,repeat).ok,"available SP cannot chain same special");var two=TacticalRules.Act(one,repeat);Check(two.units[0].sp==s.units[0].sp-2&&two.units[0].ap==s.units[0].ap&&two.units[0].charge==40,"special chain resources incorrect");
        s=Battle();actor=s.units[1];enemy=s.units.First(u=>u.team=="enemy");Place(s,actor.id,0,4);Place(s,enemy.id,0,5);enemy.hp=2;s.terrain.First(cell=>cell.x==0&&cell.y==6).kind="wall";var shove=Skill(actor,"shove",0,5);var preview=TacticalRules.Preview(s,shove);var killed=TacticalRules.Act(s,shove);Check(preview.damage==2&&killed.units.First(u=>u.id==enemy.id).hp==0,"lethal collision preview diverged");
        s=Battle();foreach(var unit in s.units.Where(u=>u.team=="hero")){
            foreach(var skill in TacticalContent.Skills.Where(skill=>TacticalContent.CanEquip(unit,skill)))if(unit.skills.Count<12&&!unit.skills.Contains(skill.id)){unit.skills.Add(skill.id);unit.skillLevels[skill.id]=1;}
            foreach(string id in unit.skillLevels.Keys.ToArray())unit.skillLevels[id]=3;
        }
        // 此夹具是旧章节旅程；其完整遗物收藏仍是发布时的六件，rev3 扩充另有专项覆盖。
        s.relics=new[]{"bell","needle","flask","lamp","pearl","seal"}.ToList();foreach(var unit in s.units.Where(u=>u.team=="enemy")){unit.hp=0;unit.ap=0;}
        s=EndPartyTurn(s);Check(s.rewards.Count==3&&s.rewards.All(r=>r.kind=="supply")&&TacticalValidation.Valid(s),"maxed build cannot claim reward");
        var supply=TacticalRules.Act(s,new TacticalRequest{type="reward",choice=s.rewards[0].id});Check(supply.phase=="route","supply fallback failed transition");
        var capped=s.Clone();capped.phase="camp";capped.nodeKind="camp";capped.rewards.Clear();Illegal(capped,new TacticalRequest{type="camp",choice="study"},"study at complete skill cap");
        var fallback=s.Clone();fallback.phase="event";fallback.nodeKind="event";fallback.eventId="witness";fallback.rewards.Clear();var responded=TacticalRules.Act(fallback,new TacticalRequest{type="event",choice="B"});Check(responded.log.Any(line=>line.Contains("所有旧物已经备齐")),"full relic pool claimed nonexistent relic");
    }
    static TacticalState Battle(uint seed=17)
    {
        var s=TacticalRules.Act(TacticalRules.NewRun(seed,Roster),new TacticalRequest{type="begin"});
        // Focused combat examples deliberately retain the old 8x8 geometry.
        s.boardWidth=8;s.boardHeight=8;s.terrain.RemoveAll(c=>c.x>=8||c.y>=8);
        int[] xs={5,6,4,6},ys={5,4,6,7};int i=0;foreach(var enemy in s.units.Where(u=>u.team=="enemy")){enemy.x=xs[i];enemy.y=ys[i++];}
        foreach(var c in s.terrain)c.kind="plain";s.objects.Clear();return s;
    }
    static TacticalState ChapterBattle(int chapter,bool side)
    {
        var s=Battle();s.chapter=chapter;s.sideBattle=side;s.nodeKind=side?"battle":"main";s.units.RemoveAll(u=>u.team=="enemy");
        string[] ids={"sluice","loom","echo","seal","afterimage"};int[] xs={5,6,4,6},ys={5,4,6,7};
        for(int i=0;i<(side?2:chapter==4?4:3);i++){
            int hp=14+chapter*3+(chapter==4&&i==0?10:0);
            s.units.Add(new TacticalUnit{id="enemy-"+chapter+"-"+i,heroId=ids[chapter],name="敌兵"+(i+1),team="enemy",x=xs[i],y=ys[i],hp=hp,maxHp=hp});
        }
        return s;
    }
    static void ClearEnemyVictory()
    {
        // 2026-10-01: 用户明确取消机关目标和守轮；原目标完成/守三轮断言改为清敌即胜。
        var start=TacticalRules.NewRun(17,Roster);Check(start.objectiveRequired==0&&start.surviveRounds==0,"new journey retained cancelled objective or survival target");
        for(int chapter=0;chapter<5;chapter++)foreach(bool side in new[]{false,true})foreach(bool legacy in new[]{false,true}){
            if(side&&chapter==4)continue;
            var s=ChapterBattle(chapter,side);
            if(legacy){s.objectiveRequired=side?0:new[]{1,2,3,2,3}[chapter];s.surviveRounds=!side&&chapter==4?3:1;if(!side&&chapter==4){s.objectiveProgress=1;s.objectiveTurn=1;}}
            var enemies=s.units.Where(u=>u.team=="enemy").ToArray();foreach(var enemy in enemies.Skip(1))enemy.hp=0;var last=enemies[0];last.hp=1;Place(s,last.id,0,1);
            string encoded=TacticalCodec.Encode(s);Check(TacticalCodec.TryDecode(encoded,out var restored,out _)&&TacticalCodec.Encode(restored)==encoded,"new or legacy battle changed on restore "+chapter+"/"+side+"/"+legacy);
            Illegal(restored,new TacticalRequest{type="interact",unitId=restored.units[0].id},"removed objective interaction");
            Check(TacticalRules.Preview(restored,new TacticalRequest{type="interact",unitId=restored.units[0].id}).reason.Contains("清除"),"removed interaction reason does not explain combat goal");
            Check(TacticalRules.Suggest(restored)?.type!="interact","bot chased removed objective");
            var won=TacticalRules.Act(restored,Skill(restored.units[0],"strike",0,1));
            Check(won.phase=="reward"&&won.round==1&&won.units.Where(u=>u.team=="enemy").All(u=>u.hp==0),"last enemy did not immediately end battle "+chapter+"/"+side+"/"+legacy);
            Check(TacticalValidation.Valid(won)&&won.rewards.Count==3,"clear-enemy reward invalid "+chapter+"/"+side+"/"+legacy);
            Check(won.objectiveRequired==s.objectiveRequired&&won.objectiveProgress==s.objectiveProgress&&won.surviveRounds==s.surviveRounds&&won.objectiveTurn==s.objectiveTurn,"legacy objective fields changed during victory");
            Check(TacticalCodec.Encode(restored)==encoded&&won.revision==restored.revision+1,"clear-enemy victory mutated prior save");
            string reward=TacticalCodec.Encode(won);Check(TacticalCodec.TryDecode(reward,out won,out _)&&TacticalCodec.Encode(won)==reward,"new-rule reward with legacy fields cannot restore");
            var advanced=TacticalRules.Act(won,TacticalRules.Suggest(won));
            Check(advanced.phase==(side?"story":chapter==4?"victory":"route")&&TacticalValidation.Valid(advanced),"clear-enemy reward cannot advance");
            if(side)Check(advanced.objectiveRequired==0&&advanced.surviveRounds==0,"legacy side reward created another objective battle");
        }
        var cleared=ChapterBattle(4,false);cleared.objectiveRequired=3;cleared.surviveRounds=3;cleared.objectiveProgress=1;cleared.objectiveTurn=1;
        foreach(var u in cleared.units){if(u.team=="enemy")u.hp=0;else {u.hp=1;cleared.terrain.First(c=>c.x==u.x&&c.y==u.y).kind="pipe";}}
        string before=TacticalCodec.Encode(cleared);Check(TacticalCodec.TryDecode(before,out var resumed,out _)&&TacticalCodec.Encode(resumed)==before,"legacy cleared battlefield reset on load");
        var settle=TacticalRules.Suggest(resumed);Check(settle.type=="endTurn","cleared legacy battlefield still pursued objective");var rewardState=TacticalRules.Act(resumed,settle);
        Check(rewardState.phase=="reward"&&rewardState.round==1&&rewardState.units.Where(u=>u.team=="hero").All(u=>u.hp==5),"cleared legacy battlefield took another enemy/heat turn");
        var heat=ChapterBattle(4,false);var victim=heat.units.First(u=>u.team=="enemy");foreach(var enemy in heat.units.Where(u=>u.team=="enemy"&&u.id!=victim.id))enemy.hp=0;
        victim.hp=2;victim.rooted=true;Place(heat,victim.id,7,7);heat.terrain.First(c=>c.x==7&&c.y==7).kind="pipe";heat.relics.Add("flask");foreach(var hero in heat.units.Where(u=>u.team=="hero"))hero.hp=1;
        var heatWon=EndPartyTurn(heat);Check(heatWon.phase=="reward"&&heatWon.round==1&&heatWon.units.Where(u=>u.team=="hero").All(u=>u.hp==5),"last enemy heat death started another player round");
    }
    static TacticalRequest Skill(TacticalUnit u,string id,int x,int y){return new TacticalRequest{type="skill",unitId=u.id,skillId=id,x=x,y=y};}
    static void BattleOpeningRelic()
    {
        foreach(bool side in new[]{false,true}){
            var s=Battle();s.relics.Add("seal");int[] initial={20,90,100};for(int i=0;i<3;i++)s.units[i].charge=initial[i];
            foreach(var enemy in s.units.Where(u=>u.team=="enemy"))enemy.hp=0;s=EndPartyTurn(s);
            Check(s.units.Where(u=>u.team=="hero").Select(u=>u.charge).SequenceEqual(initial),"opening relic incorrectly fired at victory");
            s=TacticalRules.Act(s,TacticalRules.Suggest(s));var route=s.routes.First(r=>side?r.kind=="battle"||r.kind=="elite":r.kind=="camp"||r.kind=="event");
            s=TacticalRules.Act(s,new TacticalRequest{type="route",choice=route.id});
            if(!side)s=TacticalRules.Act(s,new TacticalRequest{type=s.phase,choice=s.phase=="camp"?"rest":"A"});
            Check(s.units.Where(u=>u.team=="hero").Select(u=>u.charge).SequenceEqual(new[]{35,100,100}),"seal relic did not grant capped charge at battle opening "+side);
            Check(TacticalContent.GetRelic("seal").name=="同行信物"&&TacticalContent.GetRelic("seal").text.Contains("战斗"),"legacy seal ID still advertises objective interaction");
            string encoded=TacticalCodec.Encode(s);Check(TacticalCodec.TryDecode(encoded,out s,out _)&&TacticalCodec.Encode(s)==encoded,"opening relic charge changed on restore");
            if(!side){s=TacticalRules.Act(s,new TacticalRequest{type="begin"});Check(s.units[0].charge==35,"opening relic fired again at begin");}
            var next=EndPartyTurn(s);Check(next.units[0].charge==35,"opening relic fired again next round");
        }
    }
    static void Illegal(TacticalState s,TacticalRequest request,string message)
    {
        string original=TacticalCodec.Encode(s);Check(!TacticalRules.Preview(s,request).ok,message+" preview");
        bool refused=false;try{TacticalRules.Act(s,request);}catch(InvalidOperationException){refused=true;}
        Check(refused,message+" act");Check(original==TacticalCodec.Encode(s),message+" mutated input");
    }
    static void Place(TacticalState s,string id,int x,int y){var u=TacticalRules.FindUnit(s,id);u.x=x;u.y=y;}
    static void Mechanics()
    {
        bool badRoster=false;try{TacticalRules.NewRun(2,new[]{"sixuan","sixuan","cangling"});}catch(ArgumentException){badRoster=true;}Check(badRoster,"duplicate roster accepted");
        var a=TacticalRules.NewRun(123,Roster);var b=TacticalRules.NewRun(123,Roster);var c=TacticalRules.NewRun(124,Roster);
        Check(TacticalCodec.Encode(a)==TacticalCodec.Encode(b),"seed replay differs");Check(string.Join(",",a.terrain.Select(t=>t.kind))!=string.Join(",",c.terrain.Select(t=>t.kind)),"different seeds have identical terrain");
        var s=Battle();var u=s.units[0];var e=s.units.First(t=>t.team=="enemy");Place(s,e.id,2,3);
        Illegal(s,new TacticalRequest{type="move",unitId=u.id,x=1,y=2},"occupied movement");
        s.terrain.First(t=>t.x==2&&t.y==2).kind="wall";Illegal(s,new TacticalRequest{type="move",unitId=u.id,x=2,y=2},"wall movement");
        Illegal(s,new TacticalRequest{type="move",unitId=u.id,x=7,y=7},"overlong movement");
        var move=new TacticalRequest{type="move",unitId=u.id,x=0,y=1};var p=TacticalRules.Preview(s,move);Check(p.ok&&p.cost==1&&p.path.Count==1,"movement cost/path");
        string before=TacticalCodec.Encode(s);var after=TacticalRules.Act(s,move);Check(TacticalCodec.Encode(s)==before&&after.units[0].ap==1&&after.revision==s.revision+1,"move is mutable or free");
        after.units[0].ap=0;Illegal(after,new TacticalRequest{type="move",unitId=u.id,x=0,y=0},"AP bypass");
        s=Battle();u=s.units[0];e=s.units.First(t=>t.team=="enemy");Place(s,e.id,0,1);s.terrain.First(t=>t.x==0&&t.y==1).kind="cover";
        var strike=Skill(u,"strike",e.x,e.y);p=TacticalRules.Preview(s,strike);after=TacticalRules.Act(s,strike);Check(p.damage==4&&e.hp-after.units.First(t=>t.id==e.id).hp==p.damage,"cover damage preview mismatch");
        s.terrain.First(t=>t.x==1&&t.y==1).kind="high";p=TacticalRules.Preview(s,strike);Check(p.damage==5,"high ground plus cover");
        e.block=3;p=TacticalRules.Preview(s,strike);after=TacticalRules.Act(s,strike);Check(p.damage==2&&after.units.First(t=>t.id==e.id).block==0&&e.hp-after.units.First(t=>t.id==e.id).hp==2,"shield resolution");
        s=Battle();u=s.units[1];e=s.units.First(t=>t.team=="enemy");Place(s,u.id,0,4);Place(s,e.id,0,5);s.terrain.First(t=>t.x==0&&t.y==6).kind="wall";
        var shove=Skill(u,"shove",0,5);p=TacticalRules.Preview(s,shove);after=TacticalRules.Act(s,shove);Check(p.damage==8&&e.hp-after.units.First(t=>t.id==e.id).hp==8&&after.units.First(t=>t.id==e.id).y==5,"wall collision");
        Illegal(after,shove,"same-round skill cooldown");
        s.terrain.First(t=>t.x==0&&t.y==6).kind="plain";Place(s,s.units[2].id,0,6);p=TacticalRules.Preview(s,shove);after=TacticalRules.Act(s,shove);Check(p.damage==8&&after.units[2].hp==s.units[2].hp-4,"unit collision");
        s=Battle();u=s.units[2];e=s.units.First(t=>t.team=="enemy");Place(s,e.id,2,3);
        Illegal(s,Skill(u,"tide",2,3),"water condition");s.terrain.First(t=>t.x==u.x&&t.y==u.y).kind="mist";Check(TacticalRules.Preview(s,Skill(u,"tide",2,3)).ok,"mist ability unavailable");
        after=TacticalRules.Act(s,Skill(u,"tide",2,3));Check(after.units[2].sp==s.units[2].sp-1&&after.units[2].charge==20,"SP and charge payment");
        u.sp=0;Illegal(s,Skill(u,"tide",2,3),"SP bypass");
        s=Battle();u=s.units[0];u.skills.Add("oath");u.skillLevels["oath"]=1;e=s.units.First(t=>t.team=="enemy");Place(s,e.id,1,4);
        Illegal(s,Skill(u,"oath",1,4),"charge bypass");u.charge=100;after=TacticalRules.Act(s,Skill(u,"oath",1,4));Check(after.units[0].charge==0,"ultimate did not spend charge");
        s=Battle();u=s.units[0];e=s.units.First(t=>t.team=="enemy");Place(s,e.id,1,4);s.terrain.First(t=>t.x==1&&t.y==3).kind="wall";
        Illegal(s,Skill(u,"balance",1,4),"line of sight");
        var intents=TacticalRules.EnemyIntents(Battle());Check(intents.Count==3&&intents.All(i=>TacticalRules.Inside(i.x,i.y)&&i.damage>=0),"enemy intentions missing");
        s=Battle();Place(s,s.units[0].id,3,4);Place(s,s.units[1].id,0,0);Place(s,s.units[2].id,0,1);e=s.units.First(t=>t.team=="enemy");
        foreach(var enemy in s.units.Where(t=>t.team=="enemy"&&t.id!=e.id))enemy.rooted=true;s.terrain.First(t=>t.x==4&&t.y==5).kind="wall";
        before=TacticalCodec.Encode(s);intents=TacticalRules.EnemyIntents(s);var bending=intents.First(i=>i.unitId==e.id);
        Check(bending.path.Count==2&&bending.path[0].x==5&&bending.path[0].y==4&&bending.path[1].x==4&&bending.path[1].y==4,"enemy intent lost actual two-step corner path");
        Check(bending.path.All(cell=>cell.kind==TacticalRules.TerrainAt(s,cell.x,cell.y))&&intents.Where(i=>i.unitId!=e.id).All(i=>i.path.Count==0),"enemy paths include invalid terrain or rooted movement");
        Check(TacticalCodec.Encode(s)==before,"enemy intent path mutated input");after=EndPartyTurn(s);var arrived=after.units.First(t=>t.id==e.id);
        Check(arrived.x==bending.x&&arrived.y==bending.y&&arrived.x==bending.path.Last().x&&arrived.y==bending.path.Last().y&&s.units[0].hp-after.units[0].hp==bending.damage,"enemy intent path disagrees with settlement");
        s=Battle();u=s.units[2];e=s.units.First(t=>t.team=="enemy");Place(s,e.id,4,1);var objectItem=new TacticalObject{id="object-0-0",kind="canister",x=3,y=1,hp=10};s.objects.Add(objectItem);var enemy2=s.units.Last();Place(s,enemy2.id,4,2);
        Illegal(s,new TacticalRequest{type="move",unitId=u.id,x=3,y=1},"object movement blocker");
        var thrown=Skill(u,"throw",4,1);p=TacticalRules.Preview(s,thrown);after=TacticalRules.Act(s,thrown);
        Check(p.ok&&p.objectId==objectItem.id&&p.damage==7&&after.objects.Count==0&&e.hp-after.units.First(t=>t.id==e.id).hp==7&&enemy2.hp-after.units.First(t=>t.id==enemy2.id).hp==7,"canister throw settlement");
        Illegal(after,thrown,"throw without object");
        s=Battle();u=s.units[2];e=s.units.First(t=>t.team=="enemy");Place(s,e.id,4,1);s.objects.Add(new TacticalObject{id="object-0-0",kind="canister",x=3,y=1,hp=3});p=TacticalRules.Preview(s,Skill(u,"strike",3,1));after=TacticalRules.Act(s,Skill(u,"strike",3,1));
        Check(p.damage==3&&after.objects.Count==0&&after.units[2].hp==u.hp-4&&after.units.First(t=>t.id==e.id).hp==e.hp-4,"destroyed canister blast");
        s=Battle();u=s.units[1];e=s.units.First(t=>t.team=="enemy");Place(s,u.id,0,4);Place(s,e.id,0,5);s.objects.Add(new TacticalObject{id="object-0-0",kind="canister",x=0,y=6,hp=3});p=TacticalRules.Preview(s,Skill(u,"shove",0,5));after=TacticalRules.Act(s,Skill(u,"shove",0,5));
        Check(p.damage==12&&e.hp-after.units.First(t=>t.id==e.id).hp==12&&after.objects.Count==0,"collision blast preview mismatch");
    }
    static void Reject(JObject value,string message){Check(!TacticalCodec.TryDecode(value.ToString(),out var state,out _),message);Check(state==null,"invalid decode retained state");}
    static void Persistence(string root)
    {
        var s=TacticalRules.NewRun(66,Roster);string encoded=TacticalCodec.Encode(s);Check(TacticalCodec.TryDecode(encoded,out var restored,out _)&&encoded==TacticalCodec.Encode(restored),"v3 round trip");
        var json=JObject.Parse(encoded);json["run"]["version"]=2;Reject(json,"v2 treated as tactical");
        json=JObject.Parse(encoded);json["run"]["units"][0]["ap"]="2";Reject(json,"coerced number string");
        json=JObject.Parse(encoded);((JObject)json["run"]).Remove("objectiveTurn");Reject(json,"missing schema field");
        json=JObject.Parse(encoded);json["run"]["objectiveRequired"]=999;Reject(json,"invalid legacy objective range");
        json=JObject.Parse(encoded);json["run"]["objectiveProgress"]=1;json["run"]["objectiveTurn"]=1;Reject(json,"neutral objective fields accepted legacy progress");
        json=JObject.Parse(encoded);json["run"]["surviveRounds"]=3;Reject(json,"mixed new and legacy objective fields");
        json=JObject.Parse(encoded);json["run"]["units"][0]["ap"]=999;Reject(json,"invalid AP range");
        json=JObject.Parse(encoded);json["run"]["units"][1]["id"]=json["run"]["units"][0]["id"].DeepClone();Reject(json,"duplicate unit ID");
        json=JObject.Parse(encoded);((JArray)json["run"]["units"]).RemoveAt(3);Reject(json,"missing enemy bypasses encounter roster");
        json=JObject.Parse(encoded);json["run"]["objects"][0]["id"]="object-0-999";Reject(json,"noncanonical object ID");
        json=JObject.Parse(encoded);json["run"]["terrain"][1]=json["run"]["terrain"][0].DeepClone();Reject(json,"duplicate terrain coordinate");
        json=JObject.Parse(encoded);json["run"]["phase"]="victory";Reject(json,"illegal terminal phase");
        Check(!TacticalCodec.TryDecode(encoded.Replace("\"version\":3,","\"version\":3,\"version\":3,"),out _,out _),"duplicate JSON property");
        Check(!TacticalCodec.TryDecode(encoded+" {}",out _,out _),"trailing document");
        string directory=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"journey.json"),"legacy-sentinel");var store=new TacticalStore(directory);
        Check(store.TrySave(s,out _),"first atomic save");var next=TacticalRules.Act(s,new TacticalRequest{type="begin"});Check(store.TrySave(next,out _),"second atomic save");
        Check(store.TryRecoverPrevious(out var previous,out _)&&previous.phase=="story", "atomic previous checkpoint");
        Check(File.ReadAllText(Path.Combine(directory,"journey.json"))=="legacy-sentinel","legacy save overwritten");
        string latest=File.ReadAllText(store.SavePath);using(var held=new FileStream(store.SavePath,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Check(!store.TrySave(next,out _),"locked save unexpectedly succeeded");}
        Check(File.ReadAllText(store.SavePath)==latest&&store.TryLoad(out restored,out _)&&restored.phase=="battle","failed write damaged last good save");
        var invalid=next.Clone();invalid.units[0].hp=-1;Check(!store.TrySave(invalid,out _)&&File.ReadAllText(store.SavePath)==latest,"invalid state damaged checkpoint");
        var legacy=ChapterBattle(4,false);legacy.objectiveRequired=3;legacy.surviveRounds=3;legacy.objectiveProgress=1;legacy.objectiveTurn=1;legacy.relics.Add("seal");legacy.units[0].charge=20;
        var last=legacy.units.First(u=>u.team=="enemy");foreach(var enemy in legacy.units.Where(u=>u.team=="enemy"&&u.id!=last.id))enemy.hp=0;last.hp=1;Place(legacy,last.id,0,1);
        string legacyText=TacticalCodec.Encode(legacy);string legacyDirectory=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(legacyDirectory);var legacyStore=new TacticalStore(legacyDirectory);File.WriteAllText(legacyStore.SavePath,legacyText);
        Check(legacyStore.TryLoad(out restored,out _)&&TacticalCodec.Encode(restored)==legacyText&&File.ReadAllText(legacyStore.SavePath)==legacyText,"legacy disk save was reset or rewritten while loading");
        var won=TacticalRules.Act(restored,Skill(restored.units[0],"strike",0,1));Check(won.phase=="reward"&&legacyStore.TrySave(won,out _),"legacy disk save could not clear enemies and persist");
        Check(legacyStore.TryRecoverPrevious(out previous,out _)&&TacticalCodec.Encode(previous)==legacyText,"legacy disk checkpoint was not preserved");
        Check(legacyStore.TryLoad(out restored,out _)&&restored.phase=="reward"&&restored.objectiveProgress==1&&restored.objectiveRequired==3&&restored.surviveRounds==3&&restored.round==1,"unfinished legacy fields prevented saved victory reload");
        var completed=TacticalRules.Act(restored,TacticalRules.Suggest(restored));Check(completed.phase=="victory"&&legacyStore.TrySave(completed,out _)&&legacyStore.TryLoad(out restored,out _)&&restored.phase=="victory","legacy final chapter could not persist campaign victory");
    }
    static void Journeys()
    {
        string[] all=TacticalContent.Heroes.Select(h=>h.id).ToArray();
        var rosters=new List<string[]>();for(int i=0;i<all.Length-2;i++)for(int j=i+1;j<all.Length-1;j++)for(int k=j+1;k<all.Length;k++)rosters.Add(new[]{all[i],all[j],all[k]});
        for(int roster=0;roster<rosters.Count;roster++)for(uint seed=1;seed<=6;seed++){
            var s=TacticalRules.NewRun(seed,rosters[roster]);int step=0;
            while(s.phase!="victory"&&s.phase!="defeat"&&step<1200){
                Check(TacticalValidation.Valid(s),"invalid checkpoint "+roster+"/"+seed+" "+s.phase+" chapter "+s.chapter+" action "+step);
                Check(s.boardWidth==12&&s.boardHeight==10,"new journey reverted to legacy geometry");
                if(s.phase=="battle")maxBattleRounds=Math.Max(maxBattleRounds,s.round);
                Check(s.objectiveRequired==0&&s.surviveRounds==0,"new encounter generated a cancelled objective");
                string before=TacticalCodec.Encode(s);var request=TacticalRules.Suggest(s);
                // Exercise all route types and both non-combat choices across the complete journeys.
                if(s.phase=="route"){
                    string wanted=new[]{"battle","elite","event","camp"}[(int)((seed+(uint)s.chapter)%4)];var route=s.routes.FirstOrDefault(r=>r.kind==wanted);if(route!=null)request=new TacticalRequest{type="route",choice=route.id};
                }
                if(s.phase=="event"&&seed%2==0)request.choice="B";
                if(s.phase=="camp"&&seed%2==0)request.choice="study";
                Check(request!=null&&request.type!="interact"&&TacticalRules.Preview(s,request).ok,"bot suggested invalid action");var next=TacticalRules.Act(s,request);
                Check(TacticalCodec.Encode(s)==before,"action mutated input");Check(next.revision==s.revision+1,"revision did not advance once");
                Check(TacticalValidation.Valid(next),"invalid post-action "+request.type+" "+next.phase+" chapter "+next.chapter);
                string encoded=TacticalCodec.Encode(next);Check(TacticalCodec.TryDecode(encoded,out var decoded,out _)&&encoded==TacticalCodec.Encode(decoded),"action round-trip failed");s=decoded;step++;actions++;
            }
            Check(s.phase=="victory","journey did not win: roster "+roster+", seed "+seed+", phase "+s.phase+", chapter "+s.chapter+", round "+s.round+", steps "+step);journeys++;
        }
    }
}
