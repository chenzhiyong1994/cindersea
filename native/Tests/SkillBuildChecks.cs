using System;
using System.Linq;
using Dicebound.Tactics;
using Dicebound.Persistence;

static class SkillBuildChecks
{
    static int checks;
    static void Require(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static TacticalState Battlefield()
    {
        var state=TacticalRules.NewRun(5102,new[]{"sixuan","lingfeng","cangling"});
        state=TacticalRules.Act(state,new TacticalRequest{type="begin"});
        state.objects.Clear();foreach(var tile in state.terrain)tile.kind="plain";
        return state;
    }
    static void Teach(TacticalUnit unit,string skill,int level=1){if(!unit.skills.Contains(skill))unit.skills.Add(skill);unit.skillLevels[skill]=level;}
    static TacticalState EndParty(TacticalState state)
    {int round=state.round;while(state.phase=="battle"&&state.round==round)state=TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId=TacticalRules.NextActiveHero(state,null)?.id});return state;}
    static TacticalRequest Cast(TacticalUnit unit,string skill,TacticalUnit target)
    {return new TacticalRequest{type="skill",unitId=unit.id,skillId=skill,x=target.x,y=target.y};}
    static void EnemyAttackFlags()
    {
        foreach(var example in new[]{new{kind="sunwheel",round=1},new{kind="sunwheel",round=2},new{kind="sunwheel",round=3},new{kind="steamwarden",round=1},new{kind="steamwarden",round=3}}){
            var state=Battlefield();state.round=example.round;var enemy=state.units.First(u=>u.team=="enemy");
            foreach(var other in state.units.Where(u=>u.team=="enemy"&&u!=enemy))other.hp=0;
            enemy.heroId=example.kind;enemy.x=3;enemy.y=1;enemy.attackRange=example.kind=="sunwheel"?3:1;enemy.rooted=true;
            var intent=TacticalRules.EnemyIntents(state).Single();
            Require(!intent.text.Contains("攻击"),example.kind+"第"+example.round+"轮使用不含攻击字样的独立招式文案。");
            Require(intent.attacks&&intent.damage>0,example.kind+"攻击标记独立于展示文案，且具有真实伤害。");
            var victim=TacticalRules.FindUnit(state,intent.targetId);int beforeHp=victim.hp;
            var settled=EndParty(state);
            Require(beforeHp-TacticalRules.FindUnit(settled,victim.id).hp==intent.damage,example.kind+"攻击标记对应真实敌方行动结算。");
        }
        var approach=Battlefield();var walker=approach.units.First(u=>u.team=="enemy");
        foreach(var other in approach.units.Where(u=>u.team=="enemy"&&u!=walker))other.hp=0;
        var approachIntent=TacticalRules.EnemyIntents(approach).Single();
        Require(!approachIntent.attacks&&approachIntent.damage==0&&approachIntent.path.Count>0,"超出攻击范围的接近意图不应触发攻击演出。");
        var approached=EndParty(approach);
        Require(approached.units.Where(u=>u.team=="hero").All(u=>u.hp==TacticalRules.FindUnit(approach,u.id).hp),"接近意图实际没有造成伤害。");
        var burning=Battlefield();var doomed=burning.units.First(u=>u.team=="enemy");
        foreach(var other in burning.units.Where(u=>u.team=="enemy"&&u!=doomed))other.hp=0;
        doomed.hp=3;doomed.scorched=3;doomed.x=3;doomed.y=1;
        var burnIntent=TacticalRules.EnemyIntents(burning).Single();
        Require(!burnIntent.attacks&&burnIntent.damage==0&&burnIntent.targetId==""&&burnIntent.text.Contains("余焰"),"余焰自灭意图不含攻击标记或攻击目标。");
        var burned=EndParty(burning);
        Require(TacticalRules.FindUnit(burned,doomed.id).hp==0&&burned.phase=="reward"&&burned.units.Where(u=>u.team=="hero").All(u=>u.hp==u.maxHp),"余焰在敌方出手前击倒最后敌障，全队无需承受一次虚构攻击。");
    }
    static void EffectCombinations()
    {
        var state=Battlefield();var mage=state.units[0];var enemy=state.units.First(u=>u.team=="enemy");enemy.x=3;enemy.y=1;enemy.block=40;
        Teach(mage,"sever");var piercing=TacticalRules.Act(state,Cast(mage,"sever",enemy));
        Require(TacticalRules.FindUnit(piercing,enemy.id).hp==4&&TacticalRules.FindUnit(piercing,enemy.id).block==40,"裁云一线真实穿盾，保留目标原护盾。");
        state=Battlefield();var healer=state.units[2];mage=state.units[0];mage.hp-=10;mage.rooted=true;mage.weakened=mage.vulnerable=1;mage.scorched=3;
        Teach(healer,"cleansingwave",3);var cleansed=TacticalRules.Act(state,Cast(healer,"cleansingwave",mage));var cleanMage=cleansed.units[0];
        Require(cleanMage.hp==31&&!cleanMage.rooted&&cleanMage.weakened==0&&cleanMage.vulnerable==0&&cleanMage.scorched==0,"三级涤潮治疗9并清除全部负面状态。");
        state=Battlefield();var defender=state.units[1];mage=state.units[0];healer=state.units[2];mage.x=0;mage.y=0;healer.x=0;healer.y=1;
        enemy=state.units.First(u=>u.team=="enemy");enemy.x=2;enemy.y=2;foreach(var other in state.units.Where(u=>u.team=="enemy"&&u!=enemy))other.rooted=true;
        Teach(defender,"mountainstance");state=TacticalRules.Act(state,Cast(defender,"mountainstance",defender));state=EndParty(state);
        Require(TacticalRules.FindUnit(state,enemy.id).hp==9&&state.units[1].hp==34,"撼山架护盾吸收攻击后真实反击5伤害。");
        Require(state.units[1].retaliation==0,"反击守势在下一轮正确消退。");
        state=Battlefield();defender=state.units[1];mage=state.units[0];healer=state.units[2];mage.x=0;mage.y=0;healer.x=0;healer.y=1;
        enemy=state.units.First(u=>u.team=="enemy");enemy.x=2;enemy.y=2;foreach(var other in state.units.Where(u=>u.team=="enemy"&&u!=enemy))other.rooted=true;
        Teach(defender,"ember");state=TacticalRules.Act(state,Cast(defender,"ember",enemy));state=EndParty(state);
        Require(TacticalRules.FindUnit(state,enemy.id).hp==3&&TacticalRules.FindUnit(state,enemy.id).scorched==0,"赤炎刀痕伤害8后，敌方行动前余焰再结算3且仅一次。");
        state=Battlefield();healer=state.units[2];defender=state.units[1];defender.x=2;defender.y=2;foreach(var hero in state.units.Where(u=>u.team=="hero"))hero.hp-=15;foreach(var hostile in state.units.Where(u=>u.team=="enemy"))hostile.rooted=true;
        Teach(healer,"rainrest");state=TacticalRules.Act(state,Cast(healer,"rainrest",healer));state=EndParty(state);
        Require(state.units.Where(u=>u.team=="hero").All(u=>u.hp==u.maxHp-6&&u.regeneration==0),"凝露续息范围治疗5与下一轮恢复4独立结算。");
        state=Battlefield();mage=state.units[0];Teach(mage,"lawshare",3);defender=state.units[1];defender.ap=0;
        Require(TacticalRules.SkillDescription(state,mage,"lawshare").Contains("恢复 3 AP"),"三级并契同筹展示3AP真实上限。");
        state=TacticalRules.Act(state,Cast(mage,"lawshare",defender));Require(state.units[1].ap==3,"技能升级使AP恢复从1提升至3。");
        Require(!TacticalContent.CanLearn(mage,TacticalContent.GetSkill("oath")),"新构筑不再获得共享旧奥义。");
        state=Battlefield();healer=state.units[2];mage=state.units[0];Teach(healer,"tideward",3);
        var wardPreview=TacticalRules.Preview(state,Cast(healer,"tideward",mage));state=TacticalRules.Act(state,Cast(healer,"tideward",mage));
        Require(wardPreview.block==12&&state.units[0].block==12,"三级静海护界的预览与真实护盾均为12。");
        state=TacticalRules.Act(TacticalRules.NewRun(5102,new[]{"yanzhuying","lingfeng","cangling"}),new TacticalRequest{type="begin"});foreach(var tile in state.terrain)tile.kind="plain";state.objects.Clear();
        mage=state.units[0];state.terrain.First(t=>t.x==mage.x&&t.y==mage.y).kind="shadow";Teach(mage,"step",3);
        var longStep=new TacticalRequest{type="skill",unitId=mage.id,skillId="step",x=6,y=1};
        Require(TacticalRules.Preview(state,longStep).ok&&TacticalRules.Act(state,longStep).units[0].x==6,"三级借影真实增加到5格距离。");
    }
    static void Main()
    {
        var state=Battlefield();
        var next=TacticalRules.Act(state,new TacticalRequest{type="endTurn",unitId="sixuan"});
        Require(next.round==1,"结束司玄行动不应结束全队或触发敌方回合。");
        Require(TacticalRules.FindUnit(next,"lingfeng").ap==2,"另一位同行者仍能使用本轮AP。");
        Require(TacticalRules.FindUnit(next,"sixuan").turnEnded,"司玄结束状态可供存档/UI读取。");
        Require(!TacticalRules.Preview(next,new TacticalRequest{type="endTurn",unitId="sixuan"}).ok,"同一角色不能重复结束。");
        Require(!TacticalRules.Preview(next,new TacticalRequest{type="skill",unitId="sixuan",skillId="brace",x=1,y=1}).ok,"结束角色不能继续施术。");
        Require(TacticalRules.NextActiveHero(next,"sixuan").id=="lingfeng","按队伍顺序自动切换下一角色。");
        next=TacticalRules.Act(next,new TacticalRequest{type="endTurn",unitId="lingfeng"});
        Require(next.round==1,"两位结束时第三位仍有行动。");
        next=TacticalRules.Act(next,new TacticalRequest{type="endTurn",unitId="cangling"});
        Require(next.round==2&&next.units.Where(u=>u.team=="hero").All(u=>!u.turnEnded),"三位结束后敌方结算并开始下一轮。");
        var striker=TacticalRules.FindUnit(state,"sixuan");striker.skillLevels["strike"]=3;
        Require(TacticalRules.SkillDescription(state,striker,"strike").Contains("Lv.3"),"技能信息展示真实当前等级。");
        Require(TacticalRules.SkillDescription(state,striker,"strike").Contains("12"),"三级普攻显示12基础伤害。");
        foreach(var hero in TacticalContent.Heroes.Where(h=>h.id!="ruanzhuo")){
            Require(TacticalContent.Skills.Count(k=>k.heroId==hero.id&&k.resource!="charge")>=7,hero.name+"应有至少7项特色小技能。");
            Require(hero.skills.Contains(TacticalContent.UltimateId(hero.id)),hero.name+"有独立奥义。");
            Require(!hero.skills.Contains("oath"),"新角色不使用共享三息合誓。");
            if(hero.id!="lingfeng"&&hero.id!="shangshuo")Require(!hero.skills.Contains("shove"),"非力量角色不初始获得逼退。");
        }
        state=Battlefield();striker=TacticalRules.FindUnit(state,"sixuan");
        striker.skills.Add("lawmark");striker.skillLevels["lawmark"]=2;
        var enemy=state.units.First(u=>u.team=="enemy");enemy.x=3;enemy.y=1;enemy.hp=enemy.maxHp=100;
        var marked=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId="sixuan",skillId="lawmark",x=3,y=1});
        Require(TacticalRules.FindUnit(marked,enemy.id).hp==93,"二级勘妄印真实造成7伤害。");
        Require(TacticalRules.FindUnit(marked,enemy.id).vulnerable==2,"勘妄印施加两轮易伤支撑后续构筑。");
        var markedPreview=TacticalRules.Preview(marked,new TacticalRequest{type="skill",unitId="sixuan",skillId="balance",x=3,y=1});
        Require(markedPreview.damage==11,"易伤使下一次衡域真实增加3伤害。");
        state=Battlefield();var healer=TacticalRules.FindUnit(state,"cangling");
        foreach(var hero in state.units.Where(u=>u.team=="hero"))hero.hp-=15;
        state.terrain.First(t=>t.x==healer.x&&t.y==healer.y).kind="mist";healer.charge=100;
        var healed=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=healer.id,skillId="cangling_ultimate",x=healer.x,y=healer.y});
        Require(healed.units.Where(u=>u.team=="hero").All(u=>u.hp==u.maxHp&&u.block==10),"沧泠奥义全队治疗15并护盾10。");
        EffectCombinations();
        EnemyAttackFlags();
        DepthMechanics();
        ExpandedSkillEffects();
        PullOneCellAndObstruction();
        Console.WriteLine("Skill build checks passed: "+checks);
    }
    // ---- Depth revision 2 combat: companion combos, drench steam-burst, charge v2, steam valves. ----
    static TacticalState DepthAscent(uint seed,string[] heroes){return TacticalRules.NewAscent(seed,heroes,2);}
    static TacticalState DepthBattle(bool keepObjects=false)
    {
        var state=TacticalRules.NewAscent(5102,new[]{"sixuan","lingfeng","cangling"},2);
        state=TacticalRules.Act(state,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(state)[0].id});
        if(!keepObjects)state.objects.Clear();
        foreach(var tile in state.terrain)tile.kind="plain";
        return state;
    }
    static TacticalState ExpandedBattle(string[] heroes=null)
    {
        var state=TacticalRules.NewAscent(5102,heroes??new[]{"sixuan","lingfeng","cangling"},3);
        state=TacticalRules.Act(state,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(state)[0].id});
        state.objects.Clear();foreach(var tile in state.terrain)tile.kind="plain";return state;
    }
    static void ExpandedSkillEffects()
    {
        var state=ExpandedBattle();var caster=TacticalRules.FindUnit(state,"lingfeng");caster.x=2;caster.y=2;Teach(caster,"quakestomp");
        var enemies=state.units.Where(u=>u.team=="enemy").ToArray();enemies[0].x=3;enemies[0].y=2;enemies[1].x=3;enemies[1].y=3;
        var request=Cast(caster,"quakestomp",enemies[0]);var preview=TacticalRules.Preview(state,request);var stomped=TacticalRules.Act(state,request);
        Require(preview.ok&&preview.damage==8&&enemies.All(e=>TacticalRules.FindUnit(stomped,e.id).hp==e.hp-8),"烈山踏对目标与邻格敌障都造成8伤害。");
        Require(TacticalRules.FindUnit(stomped,enemies[0].id).x==4&&TacticalRules.FindUnit(stomped,enemies[1].id).x==4,"烈山踏实际震退两名敌障各一格。");
        Require(TacticalCodec.TryDecode(TacticalCodec.Encode(stomped),out _,out _),"范围震退后的新旅程可以保存和恢复。");
        foreach(string hero in new[]{"sixuan","cangling","yanzhuying"})
            Require(!TacticalContent.CanLearn(new TacticalUnit{team="hero",heroId=hero},TacticalContent.GetSkill("quakestomp")),hero+"不能跨学力量震退招式。");
        foreach(int level in new[]{1,3})
        {
            state=ExpandedBattle(new[]{"shangshuo","lingfeng","cangling"});caster=TacticalRules.FindUnit(state,"shangshuo");caster.x=3;caster.y=2;caster.block=10;
            Teach(caster,"bastion",level);Teach(caster,"bulwark");
            var enemy=state.units.First(u=>u.team=="enemy");enemy.x=4;enemy.y=2;enemy.rooted=true;
            state=TacticalRules.Act(state,Cast(caster,"bulwark",caster));caster=TacticalRules.FindUnit(state,caster.id);
            int expected=level==1?10:12;
            Require(caster.block==20&&caster.retaliation==expected,"反击要塞 Lv."+level+" 的百分比确实增加反击伤害。");
            Require(TacticalRules.SkillDescription(state,caster,"bastion").Contains(level==1?"20%":"30%"),"被动描述显示对应等级的真实比例。");
            var countered=EndParty(state);
            Require(TacticalRules.FindUnit(countered,enemy.id).hp==14-expected,"反击要塞 Lv."+level+" 在敌方近击时结算对应反伤。");
            Require(TacticalCodec.TryDecode(TacticalCodec.Encode(countered),out _,out _),"被动反击后的档案仍合法。");
        }
    }
    static void PullOneCellAndObstruction()
    {
        var state=ExpandedBattle();var caster=TacticalRules.FindUnit(state,"cangling");caster.x=2;caster.y=2;Teach(caster,"ebbtide");
        var enemy=state.units.First(u=>u.team=="enemy");enemy.x=4;enemy.y=3;
        var request=Cast(caster,"ebbtide",enemy);var preview=TacticalRules.Preview(state,request);
        Require(preview.ok&&preview.damage==6,"斜向退潮目标在射程内，预览仍显示真实伤害。");
        var pulled=TacticalRules.Act(state,request);var moved=TacticalRules.FindUnit(pulled,enemy.id);
        Require(TacticalRules.Distance(enemy.x,enemy.y,moved.x,moved.y)==1,"斜向牵引只移动一格，不能同时沿两轴移动两格。");
        Require(moved.x==3&&moved.y==3&&moved.hp==8&&moved.drenched==2,"牵引沿较远的横轴靠近，保留伤害和淬水效果。");
        Require(TacticalCodec.TryDecode(TacticalCodec.Encode(pulled),out _,out _),"单格牵引后的检查点能够恢复。");
        foreach(string obstruction in new[]{"wall","gap","ally","object"})
        {
            state=ExpandedBattle();caster=TacticalRules.FindUnit(state,"cangling");caster.x=2;caster.y=2;Teach(caster,"ebbtide");
            enemy=state.units.First(u=>u.team=="enemy");enemy.x=3;enemy.y=3;
            // 对角目标视线畅通；(2,3) 是牵引落点，单独阻挡它来区分命中与位移。
            if(obstruction=="wall"||obstruction=="gap")state.terrain.First(c=>c.x==2&&c.y==3).kind=obstruction;
            else if(obstruction=="ally"){var ally=TacticalRules.FindUnit(state,"sixuan");ally.x=2;ally.y=3;}
            else state.objects.Add(new TacticalObject{id="object-0-0",kind="crate",x=2,y=3,hp=10});
            request=Cast(caster,"ebbtide",enemy);
            Require(TacticalRules.Preview(state,request).ok,"落点被 "+obstruction+" 阻挡时，目标本身仍可被术法命中。");
            pulled=TacticalRules.Act(state,request);moved=TacticalRules.FindUnit(pulled,enemy.id);
            Require(moved.x==enemy.x&&moved.y==enemy.y&&moved.hp==8&&moved.drenched==2,"牵引不越过 "+obstruction+"，仍正确结算伤害和淬水。");
            Require(TacticalCodec.TryDecode(TacticalCodec.Encode(pulled),out _,out _),obstruction+" 阻挡牵引后的存档仍然合法。");
        }
    }
    static void DepthMechanics()
    {
        // 连携：第 2/3 名不同同行者的命中追加伤害，全队命中后溃散 6 点真实伤害。
        var state=DepthBattle();var enemy=state.units.First(u=>u.team=="enemy");enemy.hp=enemy.maxHp=60;enemy.x=1;enemy.y=1;enemy.rooted=true;
        foreach(var other in state.units.Where(u=>u.team=="enemy"&&u!=enemy))other.hp=0;
        var heroes=state.units.Where(u=>u.team=="hero").ToArray();
        heroes[0].x=0;heroes[0].y=1;heroes[1].x=1;heroes[1].y=0;heroes[2].x=2;heroes[2].y=1;
        state=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=heroes[0].id,skillId="strike",x=1,y=1});
        Require(TacticalRules.FindUnit(state,enemy.id).hp==52,"第一位同行者命中不产生连携加成。");
        state=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=heroes[1].id,skillId="strike",x=1,y=1});
        Require(TacticalRules.FindUnit(state,enemy.id).hp==42,"第二名不同同行者命中追加2点连携伤害。");
        state=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=heroes[2].id,skillId="strike",x=1,y=1});
        Require(TacticalRules.FindUnit(state,enemy.id).hp==30,"第三名不同同行者命中追加4点连携伤害。");
        var collapsed=EndParty(state);
        Require(TacticalRules.FindUnit(collapsed,enemy.id).hp==24,"三名同行者接连命中后溃散6点真实伤害。");
        // 蓄势 v2：技能击杀额外 +15 蓄势。
        state=DepthBattle();enemy=state.units.First(u=>u.team=="enemy");enemy.hp=3;enemy.maxHp=60;enemy.x=1;enemy.y=1;enemy.rooted=true;
        foreach(var other in state.units.Where(u=>u.team=="enemy"&&u!=enemy))other.hp=0;
        var killer=state.units.First(u=>u.heroId=="sixuan");killer.x=0;killer.y=1;killer.charge=0;
        var killed=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=killer.id,skillId="strike",x=1,y=1});
        Require(TacticalRules.FindUnit(killed,killer.id).charge==35,"技能击杀在施术蓄势之上追加15点蓄势。");
        Require(killed.phase=="reward","击杀最后敌障正常进入领奖。");
        // 蓄势 v2：被敌方攻击命中的同行者获得 10 蓄势。
        state=DepthBattle();var victim=state.units.First(u=>u.heroId=="sixuan");victim.charge=0;victim.x=2;victim.y=2;
        enemy=state.units.First(u=>u.team=="enemy");enemy.x=3;enemy.y=2;enemy.attackRange=1;enemy.rooted=true;
        foreach(var other in state.units.Where(u=>u.team=="enemy"&&u!=enemy))other.hp=0;
        var hitState=EndParty(state);
        Require(TacticalRules.FindUnit(hitState,victim.id).charge==10,"被敌方攻击命中获得10点蓄势。");
        // 淬水与蒸汽爆：沧泠水术淬水，余焰触发立即爆散。
        state=DepthBattle();var tide=state.units.First(u=>u.heroId=="cangling");var burner=state.units.First(u=>u.heroId=="lingfeng");
        enemy=state.units.First(u=>u.team=="enemy");enemy.hp=enemy.maxHp=60;enemy.x=3;enemy.y=1;enemy.rooted=true;enemy.comboMask=0;enemy.drenched=0;
        foreach(var other in state.units.Where(u=>u.team=="enemy"&&u!=enemy))other.hp=0;
        tide.x=2;tide.y=2;burner.x=2;burner.y=1;Teach(burner,"ember");
        state.terrain.First(t=>t.x==tide.x&&t.y==tide.y).kind="mist";
        state=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=tide.id,skillId="tide",x=3,y=1});
        var soaked=TacticalRules.FindUnit(state,enemy.id);
        Require(soaked.hp==54&&soaked.drenched==2,"静海界造成6点伤害并施加两轮淬水。");
        TacticalRules.FindUnit(state,enemy.id).comboMask=0; // 隔离连携，单测蒸汽爆。
        state=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=burner.id,skillId="ember",x=3,y=1});
        var burst=TacticalRules.FindUnit(state,enemy.id);
        Require(burst.hp==40&&burst.drenched==0&&burst.scorched==0,"淬水遇余焰立即蒸汽爆散：6+8+6，不残留状态。");
        // 蒸汽阀：击碎后四周化为水雾。
        state=DepthBattle(keepObjects:true);
        var valve=state.objects.FirstOrDefault(o=>o.kind=="valve");
        Require(valve!=null,"深化战场布置蒸汽阀物件。");
        var breaker=state.units.First(u=>u.heroId=="sixuan");breaker.x=2;breaker.y=2;valve.x=3;valve.y=2;
        var smashed=TacticalRules.Act(state,new TacticalRequest{type="skill",unitId=breaker.id,skillId="strike",x=3,y=2});
        Require(TacticalRules.ObjectAt(smashed,3,2)==null,"普攻一击击碎蒸汽阀。");
        Require(TacticalRules.TerrainAt(smashed,3,2)=="mist","蒸汽阀破裂后原地化为水雾。");
        // 无伤连胜加成：下一场战斗开局的全队护盾与蓄势。
        var fresh=DepthAscent(71,new[]{"sixuan","lingfeng","cangling"});
        fresh.pendingShield=7;fresh.pendingCharge=9;
        var entered=TacticalRules.Act(fresh,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(fresh)[0].id});
        Require(entered.units.Where(u=>u.team=="hero").All(u=>u.block==7&&u.charge==9),"预存的护盾与蓄势在战斗开局生效。");
        Require(entered.pendingShield==0&&entered.pendingCharge==0,"开局加成应用后立即清零。");
        string encoded=TacticalCodec.Encode(entered);
        Require(TacticalCodec.TryDecode(encoded,out var restored,out _)&&TacticalCodec.Encode(restored)==encoded,"深化战场检查点可完整恢复。");
    }
}
