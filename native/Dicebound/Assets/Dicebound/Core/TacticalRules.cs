using System;
using System.Collections.Generic;
using System.Linq;

namespace Dicebound.Tactics
{
    public static partial class TacticalRules
    {
        public const int BoardSize=8;
        public static int BoardWidth(TacticalState s){return s?.boardWidth??8;}
        public static int BoardHeight(TacticalState s){return s?.boardHeight??8;}
        public static bool Inside(TacticalState s,int x,int y){return x>=0&&x<BoardWidth(s)&&y>=0&&y<BoardHeight(s);}
        public static int MoveBudget(TacticalState s){int budget=BoardWidth(s)==8?3:4;if(s!=null&&s.ascentRevision>=3){if(s.relics.Contains("ruler"))budget++;if(s.relics.Contains("heavywheel"))budget--;}return Math.Max(2,budget);}
        public static bool Walkable(TacticalState s,int x,int y){return Inside(s,x,y)&&TerrainAt(s,x,y)!="wall"&&TerrainAt(s,x,y)!="gap";}
        public static int MoveCost(TacticalState s,int x,int y){string kind=TerrainAt(s,x,y);return kind=="pipe"||kind=="rubble"?2:1;}
        static readonly int[] Dx={1,0,-1,0},Dy={0,1,0,-1};
        public static TacticalState NewRun(uint seed,IEnumerable<string> heroIds)
        {
            var ids=heroIds==null?new List<string>():heroIds.ToList();
            if(ids.Count!=3||ids.Distinct().Count()!=3||ids.Any(id=>TacticalContent.GetHero(id)==null))throw new ArgumentException("请选择三位不同的同行者。",nameof(heroIds));
            var s=new TacticalState{seed=seed,rng=seed==0?0x9e3779b9u:seed,phase="story",nodeKind="main",eventId="",squad=ids};
            foreach(string id in ids){var h=TacticalContent.GetHero(id);s.units.Add(new TacticalUnit{id=id,heroId=id,name=h.name,team="hero",hp=h.maxHp,maxHp=h.maxHp,maxAp=h.maxAp,maxSp=h.maxSp,sp=h.maxSp,skills=h.skills.ToList(),skillLevels=h.skills.ToDictionary(k=>k,k=>1)});}
            SetupBattle(s,false);s.phase="story";AddLog(s,"三位同行者进入曜京。种子 "+seed+"。");return s;
        }
        public static TacticalUnit FindUnit(TacticalState s,string id) {return s?.units?.FirstOrDefault(u=>u.id==id);}
        public static TacticalUnit NextActiveHero(TacticalState s,string afterId)
        {
            if(s==null)return null;int index=s.squad.IndexOf(afterId);
            for(int offset=1;offset<=s.squad.Count;offset++){var unit=FindUnit(s,s.squad[(index+offset+s.squad.Count)%s.squad.Count]);if(unit!=null&&unit.hp>0&&!unit.turnEnded)return unit;}
            return null;
        }
        public static TacticalObject ObjectAt(TacticalState s,int x,int y){return s.objects.FirstOrDefault(o=>o.hp>0&&o.x==x&&o.y==y);}
        public static string TerrainAt(TacticalState s,int x,int y) {return Inside(s,x,y)?s.terrain.FirstOrDefault(c=>c.x==x&&c.y==y)?.kind??"plain":"wall";}
        public static TacticalPreview Preview(TacticalState s,TacticalRequest a)
        {
            var p=new TacticalPreview();
            if(s==null||a==null){p.reason="行动缺少旅程或指令。";return p;}
            if(a.type=="node"||a.type=="buy"||a.type=="leaveShop"||a.type=="rerollShop")return AscentPreview(s,a,p);
            if(a.type=="interact")return Denied(p,"清除全部敌人即可获胜，无需操作目标。");
            if(a.type=="begin")return Allowed(s.phase=="story",p,"当前没有待开始的战场。");
            if(a.type=="endTurn"){
                if(s.phase!="battle")return Denied(p,"当前不在战场。");
                if(!s.units.Any(t=>t.team=="enemy"&&t.hp>0))return Allowed(true,p,null);
                var ending=string.IsNullOrEmpty(a.unitId)?NextActiveHero(s,null):FindUnit(s,a.unitId);
                return Allowed(ending!=null&&ending.team=="hero"&&ending.hp>0&&!ending.turnEnded,p,"这位同行者已经结束本轮行动。");
            }
            if(a.type=="route")return Allowed(s.phase=="route"&&s.routes.Any(r=>r.id==a.choice),p,"请选择当前显示的路线。");
            if(a.type=="event")return PreviewEvent(s,a,p);
            if(a.type=="camp")return Allowed(s.phase=="camp"&&(a.choice=="rest"||a.choice=="study"&&LearnablePartySkills(s).Any()||a.choice=="ward"&&s.journeyMode=="ascent"&&s.ascentRevision>=2),p,"请选择休整；所有技能满级时无法继续研习。");
            if(a.type=="reward"){
                var reward=EffectiveReward(s,s.rewards.FirstOrDefault(r=>r.id==a.choice));
                if(s.phase!="reward"||reward==null)return Denied(p,"请选择当前显示的奖励。");
                if(reward.kind=="skill"&&!CanAcquireSkill(s,reward.skillId))return Denied(p,"对应同行者未出战或已达到领悟上限。");
                return Allowed(true,p,null);
            }
            if(s.phase!="battle")return Denied(p,"当前不在战场。");
            var u=FindUnit(s,a.unitId);
            if(u==null||u.team!="hero"||u.hp<=0)return Denied(p,"请选择仍能行动的同行者。");
            if(u.turnEnded)return Denied(p,"这位同行者已经结束本轮行动，请切换其他同行者。");
            if(a.type=="move"){
                p=SkillAvailability(s,u.id,null);if(!p.ok)return p;
                if(!Walkable(s,a.x,a.y)||Occupied(s,a.x,a.y)!=null||ObjectAt(s,a.x,a.y)!=null)return Denied(p,"该格被障碍、缺口、物件或单位占据。");
                var path=Path(s,u,a.x,a.y);if(path==null||path.Count==0)return Denied(p,"无法抵达该格。");
                if(PathCost(s,path)>MoveBudget(s))return Denied(p,"一次移动最多走 "+MoveBudget(s)+" 步，热管与碎石消耗 2 步。");
                p.path=path;p.affected.Add(new TacticalCell(a.x,a.y,TerrainAt(s,a.x,a.y)));
                return Allowed(true,p,null);
            }
            if(a.type!="skill")return Denied(p,"未知行动。");
            var skill=TacticalContent.GetSkill(a.skillId);
            if(skill==null||!u.skills.Contains(skill.id))return Denied(p,"尚未掌握这项技能。");
            p=SkillAvailability(s,u.id,skill.id);if(!p.ok)return p;
            int reach=EffectiveSkillRange(s,u,skill);
            if(!Inside(s,a.x,a.y)||Distance(u.x,u.y,a.x,a.y)>reach)return Denied(p,"目标超出技能范围。");
            if(!Visible(s,u.x,u.y,a.x,a.y,skill.kind=="throw"))return Denied(p,"障碍遮住了目标。");
            var target=Occupied(s,a.x,a.y);
            var objectTarget=ObjectAt(s,a.x,a.y);
            if(skill.kind=="step"){
                if(target!=null||objectTarget!=null||!Walkable(s,a.x,a.y))return Denied(p,"请选择可通行的空位。");
                if(TerrainAt(s,u.x,u.y)!="shadow"&&TerrainAt(s,a.x,a.y)!="shadow")return Denied(p,"借影需要起点或落点处于阴影。");
                p.affected.Add(new TacticalCell(a.x,a.y,TerrainAt(s,a.x,a.y)));return Allowed(true,p,null);
            }
            bool friendly=FriendlySkill(skill);
            bool attackingObject=objectTarget!=null&&(skill.kind=="attack"||skill.kind=="bind");
            if(!attackingObject&&(target==null||target.team!=(friendly?"hero":"enemy")))return Denied(p,friendly?"请选择存活的同行者。":"请选择敌障或可破坏物件。");
            if(skill.kind=="throw"){
                var obj=ThrowObject(s,u,a.choice);if(obj==null)return Denied(p,"身旁需要有可投掷的木箱或热罐。");p.objectId=obj.id;
            }
            if(skill.kind=="heal"&&skill.effect!="healward"&&s.units.Where(t=>t.hp>0&&t.team=="hero"&&Distance(t.x,t.y,target.x,target.y)<=skill.area).All(t=>t.hp>=t.maxHp))return Denied(p,"这个范围内的同行者无需治疗。");
            if(skill.kind=="haste"&&target.ap>=target.maxAp+1)return Denied(p,"这位同行者的 AP 已充足。");
            if(skill.kind=="haste"&&target.turnEnded)return Denied(p,"这位同行者已经结束本轮行动，不能恢复行动。");
            if(skill.kind=="restore"&&target.sp>=target.maxSp)return Denied(p,"这位同行者的 SP 已充足。");
            // Settlement on a clone keeps lethal hits, shields, collision and heat-canister blasts exact.
            var simulated=s.Clone();UseSkill(simulated,a);
            foreach(var t in s.units){var changed=FindUnit(simulated,t.id);if(t.hp!=changed.hp||t.block!=changed.block||t.x!=changed.x||t.y!=changed.y||t.ap!=changed.ap||t.sp!=changed.sp||UnitStatusDescription(t)!=UnitStatusDescription(changed))p.affected.Add(new TacticalCell(t.x,t.y,TerrainAt(s,t.x,t.y)));}
            if(target!=null){var changed=FindUnit(simulated,target.id);p.damage=Math.Max(0,target.hp-changed.hp);p.heal=Math.Max(0,changed.hp-target.hp);p.block=Math.Max(0,changed.block-target.block);}
            else {p.damage=objectTarget.hp-(ObjectAt(simulated,a.x,a.y)?.hp??0);p.affected.Add(new TacticalCell(a.x,a.y,TerrainAt(s,a.x,a.y)));}
            return Allowed(true,p,null);
        }
        static TacticalPreview Allowed(bool valid,TacticalPreview p,string error){p.ok=valid;p.reason=valid?"可以行动。":error;return p;}
        static TacticalPreview Denied(TacticalPreview p,string error){p.ok=false;p.reason=error;return p;}
        public static TacticalState Act(TacticalState before,TacticalRequest a)
        {
            var preview=Preview(before,a);if(!preview.ok)throw new InvalidOperationException(preview.reason);
            var s=before.Clone();s.revision++;
            if(a.type=="node")EnterAscentNode(s,a.choice);
            else if(a.type=="buy")BuyAscentOffer(s,a);
            else if(a.type=="rerollShop")RerollAscentShop(s);
            else if(a.type=="leaveShop")CompleteAscentNode(s);
            else if(a.type=="begin"){s.phase="battle";AddLog(s,"战场开始："+TacticalStory.Stage(s.chapter).title);}
            else if(a.type=="move"){var u=FindUnit(s,a.unitId);u.ap--;u.x=a.x;u.y=a.y;AddLog(s,u.name+"移动至 "+CellName(a.x,a.y)+"。");CheckOutcome(s);}
            else if(a.type=="skill"){UseSkill(s,a);CheckOutcome(s);}
            else if(a.type=="endTurn"){
                // A restored v3 battlefield may already be cleared with an unfinished legacy objective.
                CheckOutcome(s);
                if(s.phase=="battle"){
                    var ending=string.IsNullOrEmpty(a.unitId)?NextActiveHero(s,null):FindUnit(s,a.unitId);
                    ending.turnEnded=true;ending.ap=0;ending.rooted=false;AddLog(s,ending.name+"结束本轮行动。");
                    if(s.units.Any(u=>u.team=="hero"&&u.hp>0&&!u.turnEnded))return s;
                    CollapseCombos(s);
                    ResolveEnemies(s,null);
                    foreach(var u in s.units.Where(t=>t.hp>0))if(TerrainAt(s,u.x,u.y)=="pipe"&&!(s.ascentRevision>=3&&u.team=="hero"&&s.relics.Contains("rubberboots")))Damage(s,u,2+(s.ascentRevision>=3&&u.team=="hero"&&s.relics.Contains("overheatvalve")?2:0));
                    CheckOutcome(s);
                    if(s.phase=="battle"){s.round++;StartRound(s);AddLog(s,"第 "+s.round+" 轮。");}
                    else if(s.phase=="defeat")AddLog(s,"同行者已全部倒下。");
                }
            }
            else if(a.type=="reward"){
                var r=EffectiveReward(s,s.rewards.First(t=>t.id==a.choice));
                if(r.kind=="skill"){var u=SkillRecipient(s,r.skillId);Learn(u,r.skillId);AddLog(s,u.name+"领悟「"+r.name+"」。");}
                else if(r.kind=="gold"){s.gold+=r.amount;AddLog(s,"打开金币行囊，获得 "+r.amount+" 金币。");}
                else {GrantRelic(s,r.relicId);AddLog(s,"获得「"+r.name+"」。");}
                s.rewards.Clear();if(s.journeyMode=="ascent")CompleteAscentNode(s);else if(s.sideBattle)Advance(s);else if(s.chapter==4){s.phase="victory";AddLog(s,"敌军溃败，曜京重归安宁。同行者守住了这座城。");}else {s.phase="route";CreateRoutes(s);}
            }
            else if(a.type=="route"){
                var route=s.routes.First(r=>r.id==a.choice);s.nodeKind=route.kind;s.routes.Clear();AddLog(s,"选择支路："+route.name+"。");
                if(route.kind=="event"){s.phase="event";s.eventId=TacticalStory.OriginalEvents[(int)(Next(s)%TacticalStory.OriginalEvents.Length)].id;}
                else if(route.kind=="camp")s.phase="camp";
                else {SetupBattle(s,true);s.phase="battle";}
            }
            else if(a.type=="event"){
                string outcome=ResolveEvent(s,a.choice);
                var e=Array.Find(TacticalStory.Events,t=>t.id==s.eventId);
                AddLog(s,string.IsNullOrEmpty(outcome)?(a.choice=="A"?e.resultA:e.resultB):outcome);
                if(s.journeyMode=="ascent")CompleteAscentNode(s);else Advance(s);
            }
            else if(a.type=="camp"){
                if(a.choice=="rest"){
                    if(s.journeyMode=="ascent"&&s.ascentRevision>=2){HealSquadPercent(s,35);AddLog(s,"营火旁包扎休整，全队恢复 35% 生命。");}
                    else {HealSquad(s,10);AddLog(s,"包扎与休息使全队恢复 10 生命。");}
                }
                else if(a.choice=="ward"){s.pendingShield=Math.Min(40,s.pendingShield+5);AddLog(s,"守夜戒备，下一场战斗全员获得 5 护盾。");}
                else LearnRandom(s);
                if(s.journeyMode=="ascent")CompleteAscentNode(s);else Advance(s);
            }
            return s;
        }
        static void UseSkill(TacticalState s,TacticalRequest a)
        {
            var u=FindUnit(s,a.unitId);var skill=TacticalContent.GetSkill(a.skillId);int power=SkillPower(u,skill),secondary=SkillSecondaryPower(u,skill);
            var livingEnemyIds=s.ascentRevision>=3?new HashSet<string>(s.units.Where(t=>t.team=="enemy"&&t.hp>0).Select(t=>t.id)):null;
            // 出手即破影遁；磨石强化普攻；判官玺缩短司玄奥义冷却。
            u.concealed=0;
            if(s.relics.Contains("whetstone")&&skill.id=="strike")power+=2;
            if(skill.resource=="sp")u.sp-=skill.cost;
            else if(skill.resource=="charge")u.charge=s.relics.Contains("sunwheelshard")?30:0;
            else u.ap-=skill.cost;
            if(skill.cooldown>0)u.cooldowns[skill.id]=SkillCooldown(s,skill);
            if(skill.kind=="step"){u.x=a.x;u.y=a.y;if(TerrainAt(s,a.x,a.y)=="shadow")u.sp=Math.Min(u.maxSp,u.sp+1);}
            else {
                var target=Occupied(s,a.x,a.y);var objectTarget=ObjectAt(s,a.x,a.y);
                if(objectTarget!=null){DamageObject(s,objectTarget,power+(s.relics.Contains("wrench")?2:0));FinishSkillEffect(s,u,skill,secondary);if(skill.resource!="charge")u.charge=Math.Min(100,u.charge+20);RewardSkillKills(s,u,skill,livingEnemyIds);AddLog(s,u.name+"击中场地物件。");return;}
                int area=skill.area;
                if(skill.kind=="throw"){var obj=ThrowObject(s,u,a.choice);area=obj.kind=="canister"?1:0;s.objects.Remove(obj);}
                var affected=s.units.Where(t=>t.hp>0&&t.team==target.team&&Distance(t.x,t.y,target.x,target.y)<=area).ToList();
                bool strikes=skill.kind=="attack"||skill.kind=="bind"||skill.kind=="throw";int killed=0;var deadCells=new List<TacticalCell>();
                foreach(var t in affected){
                    int strikePower=power;
                    // 同行连携：记录哪些同行者命中过；第 2/3 名不同同行者的命中追加伤害。
                    if(s.ascentRevision>=2&&strikes&&u.team=="hero"&&t.team=="enemy"){
                        int bit=1<<Math.Max(0,Math.Min(2,s.squad.IndexOf(u.heroId)));
                        t.comboMask=(t.comboMask|bit)&7;
                        int distinct=BitCount(t.comboMask);
                        if(distinct>=2)strikePower+=(distinct==2?2:4)+(s.ascentRevision>=3&&s.relics.Contains("whistle")?1:0);
                    }
                    if(skill.kind=="heal"){
                        int wanted=HealingMod(s,power+MistHealBonus(s,u,skill)),before=t.hp;
                        t.hp=Math.Min(t.maxHp,t.hp+wanted);
                        if(s.ascentRevision>=2&&t.hp-before<wanted)t.regeneration=Math.Min(30,t.regeneration+(wanted-(t.hp-before))/2);
                    }
                    else if(skill.kind=="guard")AddBlock(s,t,power);
                    else if(skill.kind=="haste")t.ap=Math.Min(t.maxAp+1,t.ap+power);
                    else if(skill.kind=="restore")t.sp=Math.Min(t.maxSp,t.sp+power);
                    else if(skill.kind=="empower")t.damageBonus=Math.Min(12,t.damageBonus+power);
                    else {Hit(s,u,t,EffectAttackPower(s,u,t,skill,strikePower),skill.effect=="pierce"||skill.effect=="eclipse");if(t.hp>0&&skill.push>0)Push(s,u,t,skill.push+(s.relics.Contains("valvecore")?1:0));if(skill.kind=="bind"&&t.hp>0)t.rooted=true;}
                    if(t.team=="enemy"&&t.hp<=0){killed++;deadCells.Add(new TacticalCell(t.x,t.y,"plain"));}
                    ApplySkillEffect(s,u,t,skill,secondary);
                }
                FinishSkillEffect(s,u,skill,secondary);
                if(skill.resource!="charge")u.charge=Math.Min(100,u.charge+20);
                // 蓄势 v2：技能击杀敌障的施放者额外蓄势；铜号波及全队；梦线轴震伤邻格。
                if(s.ascentRevision==2&&killed>0&&u.team=="hero"){
                    u.charge=Math.Min(100,u.charge+15);
                    if(s.relics.Contains("brasshorn"))foreach(var ally in s.units.Where(t=>t.team=="hero"&&t.hp>0))ally.charge=Math.Min(100,ally.charge+10);
                    if(s.relics.Contains("dreamspool"))foreach(var cell in deadCells)foreach(var neighbor in s.units.Where(t=>t.team=="enemy"&&t.hp>0&&Distance(t.x,t.y,cell.x,cell.y)<=1).ToList())neighbor.vulnerable=Math.Max(neighbor.vulnerable,1);
                    if(skill.effect=="settle"&&u.ap<u.maxAp+1){u.ap++;AddLog(s,u.name+"收账返还 1 行动点。");}
                }
                RewardSkillKills(s,u,skill,livingEnemyIds);
            }
            AddLog(s,u.name+"施展「"+skill.name+"」。");
        }
        static void RewardSkillKills(TacticalState state,TacticalUnit caster,TacticalSkill skill,HashSet<string> livingEnemyIds)
        {
            if(state.ascentRevision<3||livingEnemyIds==null||caster.team!="hero")return;
            var defeated=state.units.Where(u=>u.team=="enemy"&&u.hp<=0&&livingEnemyIds.Contains(u.id)).ToArray();
            if(defeated.Length==0)return;
            caster.charge=Math.Min(100,caster.charge+15*defeated.Length);
            if(state.relics.Contains("brasshorn"))foreach(var ally in state.units.Where(u=>u.team=="hero"&&u.hp>0))ally.charge=Math.Min(100,ally.charge+10*defeated.Length);
            if(skill.effect=="settle"&&caster.ap<caster.maxAp+1){caster.ap++;AddLog(state,caster.name+"收账返还 1 行动点。");}
        }
        public static int SkillLevel(TacticalUnit unit,string skillId){return unit!=null&&unit.skillLevels.TryGetValue(skillId,out int level)?Math.Max(1,Math.Min(3,level)):1;}
        public static int SkillPower(TacticalUnit unit,TacticalSkill skill){return skill==null?0:skill.power+skill.powerStep*(SkillLevel(unit,skill.id)-1);}
        public static int SkillSecondaryPower(TacticalUnit unit,TacticalSkill skill){return skill==null||skill.secondaryPower==0?0:skill.secondaryPower+skill.secondaryStep*(SkillLevel(unit,skill.id)-1);}
        public static int BastionRetaliationBonus(TacticalUnit unit)
        {return unit==null?0:unit.block*(20+5*(SkillLevel(unit,"bastion")-1))/100;}
        public static string SkillDescription(TacticalState state,TacticalUnit unit,string skillId)
        {
            var skill=TacticalContent.GetSkill(skillId);if(skill==null)return skillId??"";
            int level=SkillLevel(unit,skillId),power=SkillPower(unit,skill),secondary=SkillSecondaryPower(unit,skill);string resource=skill.resource=="charge"?"蓄势":skill.resource.ToUpperInvariant();
            if(skill.kind=="passive")return "Lv."+level+" / 3 · 被动 · 常驻生效\n施加近身反击时，追加自身护盾的 "+(20+5*(level-1))+"% 作为反击伤害（当前 +"+BastionRetaliationBonus(unit)+"）。\n无需施放 · 不消耗资源";
            string value=skill.kind=="heal"?"恢复 "+power+" 生命":skill.kind=="guard"?"获得 "+power+" 护盾":skill.kind=="haste"?"恢复 "+power+" AP":skill.kind=="restore"?"恢复 "+power+" SP":skill.kind=="empower"?"本轮伤害增加 "+power:skill.kind=="step"?"借影移动 "+skill.range+" 格":"基础伤害 "+power;
            if(skill.kind=="step")value="借影移动 "+EffectiveSkillRange(state,unit,skill)+" 格";
            if(skill.effect=="depthcleanse")value=(power>0?"基础护盾 "+power+" · ":"")+"每清除一种负面状态获得 "+secondary+" 护盾";
            if(skill.effect=="stealth")value="进入影遁"+(power>0?" · 获得 "+power+" 护盾":"");
            if(skill.effect=="ignite")value="结算目标全部余焰"+(power>0?" · 基础伤害 +"+power:"");
            string extra=secondary>0&&skill.effect!="depthcleanse"?(skill.effect=="apcharge"?"施术者蓄势 +"+secondary:skill.effect=="scorch"?"余焰 "+secondary+" 伤害":skill.effect=="retaliate"?"近身反击 "+secondary+" 伤害":skill.effect=="regen"?"下轮恢复 "+secondary+" 生命":skill.effect=="restoresp"?"额外恢复 "+secondary+" SP":"额外护盾 "+secondary):"";
            if(unit!=null&&state!=null){if(skill.kind=="heal"&&MistHealBonus(state,unit,skill)>0)extra+=(extra==""?"":" · ")+"当前水雾治疗 +2";
                if(!FriendlySkill(skill)&&skill.kind!="step"){if(unit.damageBonus>0)extra+=(extra==""?"":" · ")+"当前鼓舞 +"+unit.damageBonus;if(unit.weakened>0)extra+=(extra==""?"":" · ")+"当前削弱 -3";}}
            return "Lv."+level+" / 3 · "+value+(extra==""?"":"\n"+extra)+"\n"+skill.text+"\n消耗 "+skill.cost+" "+resource+" · 范围 "+EffectiveSkillRange(state,unit,skill)+" · 冷却 "+SkillCooldown(state,skill)+" 轮";
        }
        static int AttackPower(TacticalState s,TacticalUnit attacker,TacticalUnit target,int power,string skillId)
        {
            if(TerrainAt(s,attacker.x,attacker.y)=="high"&&TerrainAt(s,target.x,target.y)!="high")power+=2;
            if(skillId=="moon"&&TerrainAt(s,attacker.x,attacker.y)=="shadow")power+=3;
            power+=attacker.damageBonus;if(attacker.weakened>0)power=Math.Max(0,power-3);if(target.vulnerable>0)power+=3;
            if(TerrainAt(s,target.x,target.y)=="cover")power=(power+1)/2;
            if(s.ascentRevision>=2)power+=OnRecordBonus(s,target);
            return power;
        }
        // 在案：敌障身上带任意控制状态。断案笔/天衡玉印提供全队加成。
        internal static int OnRecordBonus(TacticalState s,TacticalUnit target)
        {
            if(s.ascentRevision<3||target.team!="enemy")return 0;
            bool onRecord=target.rooted||target.weakened>0||target.vulnerable>0||target.drenched>0;
            if(!onRecord)return 0;
            int bonus=s.relics.Contains("verdictpen")?2:0;
            if(s.relics.Contains("jadeimprint"))bonus+=Math.Min(3,(target.rooted?1:0)+(target.weakened>0?1:0)+(target.vulnerable>0?1:0)+(target.drenched>0?1:0));
            return bonus;
        }
        static void Damage(TacticalState s,TacticalUnit u,int amount)
        {
            if(s.ascentRevision>=3&&u.hp<=0)return;
            int hpBefore=u.hp;
            if(amount>0&&s.ascentRevision>=3&&u.team=="enemy")
            {
                // 坚壳：每轮首次承受的伤害减半；庇邻：邻接带庇邻词缀的敌障减伤 2。
                if(TacticalContent.HasAffix(u,"shellhard")&&u.shellUsed==0){amount=(amount+1)/2;u.shellUsed=1;}
                if(s.units.Any(t=>t.team=="enemy"&&t.hp>0&&t.id!=u.id&&Distance(t.x,t.y,u.x,u.y)<=1&&TacticalContent.HasAffix(t,"guarding")))amount=Math.Max(0,amount-2);
            }
            int absorbed=Math.Min(u.block,amount);u.block-=absorbed;u.hp=Math.Max(0,u.hp-(amount-absorbed));
            ResolveDeath(s,u,hpBefore);
        }
        // Every damage path shares death effects, but only on the transition from
        // living to defeated. Shield/armour calculation stays with the caller.
        static void ResolveDeath(TacticalState s,TacticalUnit u,int hpBefore)
        {
            if(hpBefore>0&&u.hp==0)
            {
                u.ap=0;u.block=0;
                if(s.ascentRevision>=3&&u.team=="enemy"&&TacticalContent.HasAffix(u,"rusted"))
                {
                    // 锈坏：死亡处留下碎石格，词缀随之消耗。
                    s.terrain.First(c=>c.x==u.x&&c.y==u.y).kind="rubble";
                    u.affixes=string.Join("+",u.affixes.Split('+').Where(p=>p!="rusted"));
                    AddLog(s,u.name+"锈坏成碎石。");
                }
                if(s.ascentRevision>=3&&u.team=="enemy"&&s.relics.Contains("dreamspool"))
                    foreach(var neighbor in s.units.Where(t=>t.team=="enemy"&&t.hp>0&&Distance(t.x,t.y,u.x,u.y)<=1).ToList())
                        neighbor.vulnerable=Math.Max(neighbor.vulnerable,1);
                if(s.ascentRevision>=3&&u.team=="hero"&&s.relics.Contains("immunitytalisman")&&s.immunityUsed==0)
                {
                    s.immunityUsed=1;u.hp=1;u.block=0;AddLog(s,"免死金牌碎裂，"+u.name+"以 1 生命稳住。");
                }
            }
        }

        static void Push(TacticalState s,TacticalUnit attacker,TacticalUnit target,int count)
        {
            Direction(attacker,target,out int dx,out int dy);
            for(int i=0;i<count;i++){
                int x=target.x+dx,y=target.y+dy;var hit=Occupied(s,x,y);var obj=ObjectAt(s,x,y);
                if(!Walkable(s,x,y)||hit!=null||obj!=null){int damage=4+(s.relics.Contains("needle")?3:0)+(s.relics.Contains("valvecore")?4:0);Damage(s,target,damage);if(hit!=null)Damage(s,hit,damage);if(obj!=null)DamageObject(s,obj,damage);AddLog(s,"击退碰撞造成 "+damage+" 伤害。");break;}
                target.x=x;target.y=y;
            }
        }
        static TacticalObject ThrowObject(TacticalState s,TacticalUnit unit,string choice)
        {int reach=1+(s.relics.Contains("wrench")?1:0);return s.objects.Where(o=>o.hp>0&&Distance(unit.x,unit.y,o.x,o.y)<=reach&&(string.IsNullOrEmpty(choice)||o.id==choice)).OrderBy(o=>o.id).FirstOrDefault();}
        static void DamageObject(TacticalState s,TacticalObject obj,int damage)
        {
            obj.hp=Math.Max(0,obj.hp-damage);if(obj.hp>0)return;s.objects.Remove(obj);
            if(obj.kind=="canister"){foreach(var unit in s.units.Where(u=>u.hp>0&&Distance(u.x,u.y,obj.x,obj.y)<=1).ToList())Damage(s,unit,4);AddLog(s,"热罐破裂，邻格所有单位受到 4 伤害。");}
            else if(obj.kind=="valve"){
                foreach(var cell in s.terrain.Where(c=>Distance(c.x,c.y,obj.x,obj.y)<=2&&Walkable(s,c.x,c.y)))cell.kind="mist";
                foreach(var unit in s.units.Where(u=>u.hp>0&&u.team=="enemy"&&Distance(u.x,u.y,obj.x,obj.y)<=1).ToList())ApplyDrench(s,unit,2);
                AddLog(s,"蒸汽阀破裂，四周化为水雾；邻格敌障被蒸汽浸湿。");
            }
        }
        static void Direction(TacticalUnit attacker,TacticalUnit target,out int dx,out int dy)
        {dx=0;dy=0;if(Math.Abs(target.x-attacker.x)>=Math.Abs(target.y-attacker.y))dx=target.x>=attacker.x?1:-1;else dy=target.y>=attacker.y?1:-1;}
        public static List<TacticalCell> MoveCells(TacticalState s,string unitId)
        {
            var result=new List<TacticalCell>();var u=FindUnit(s,unitId);
            if(s?.phase!="battle"||u==null||u.team!="hero"||u.hp<=0||u.turnEnded||u.rooted||u.ap<1)return result;
            int width=BoardWidth(s);var distances=SearchPaths(s,u,out _);
            for(int y=0;y<BoardHeight(s);y++)for(int x=0;x<width;x++)if(distances[y*width+x]>0&&distances[y*width+x]<=MoveBudget(s))result.Add(new TacticalCell(x,y,TerrainAt(s,x,y)));return result;
        }
        public static List<TacticalCell> SkillTargets(TacticalState s,string unitId,string skillId)
        {var result=new List<TacticalCell>();for(int y=0;y<BoardHeight(s);y++)for(int x=0;x<BoardWidth(s);x++)if(Preview(s,new TacticalRequest{type="skill",unitId=unitId,skillId=skillId,x=x,y=y}).ok)result.Add(new TacticalCell(x,y,TerrainAt(s,x,y)));return result;}
        public static List<TacticalIntent> EnemyIntents(TacticalState s)
        {var result=new List<TacticalIntent>();if(s.phase=="battle"){var clone=s.Clone();CollapseCombos(clone);ResolveEnemies(clone,result);}return result;}
        // 同行连携溃散：被全部三名同行者命中过的敌障在玩家阶段结束时受到真实伤害。
        static void CollapseCombos(TacticalState s)
        {
            if(s.ascentRevision<2||s.squad.Count!=3)return;
            foreach(var enemy in s.units.Where(t=>t.team=="enemy"&&t.hp>0&&t.comboMask==7).ToList()){
                int collapse=s.relics.Contains("echoshell")?12:6;
                int hpBefore=enemy.hp;enemy.hp=Math.Max(0,enemy.hp-collapse);ResolveDeath(s,enemy,hpBefore);
                AddLog(s,"三位同行者接连命中，"+enemy.name+"溃散，受到 "+collapse+" 点真实伤害。");
            }
        }
        static int BitCount(int value){int count=0;while(value>0){count+=value&1;value>>=1;}return count;}
        static void ResolveEnemies(TacticalState s,List<TacticalIntent> intents)
        {
            foreach(var enemy in s.units.Where(u=>u.team=="enemy"&&u.hp>0).OrderBy(u=>u.id).ToList()){
                if(enemy.scorched>0){Damage(s,enemy,enemy.scorched);enemy.scorched=0;if(enemy.hp<=0){intents?.Add(new TacticalIntent{unitId=enemy.id,targetId="",x=enemy.x,y=enemy.y,text=enemy.name+"将被余焰击倒。"});continue;}}
                PrepareEnemyAction(s,enemy);
                var heroes=s.units.Where(u=>u.team=="hero"&&u.hp>0).ToList();if(heroes.Count==0)break;
                // 影遁：藏匿的同行者不进入敌障的候选目标，除非全员藏匿。
                var candidates=s.ascentRevision>=3?heroes.Where(h=>h.concealed==0).ToList():heroes;if(candidates.Count==0)candidates=heroes;
                int width=BoardWidth(s),range=EnemyAttackRange(s,enemy);
                var approach=candidates.Select(hero=>new{hero,distances=GoalDistances(s,enemy,new List<TacticalUnit>{hero},range)}).OrderBy(a=>a.distances[enemy.y*width+enemy.x]).ThenBy(a=>a.hero.hp).ThenBy(a=>a.hero.id).First();
                // 嘲讽：被喝令的敌障下轮只能以喝令者为目标。
                var forced=s.ascentRevision<3||string.IsNullOrEmpty(enemy.tauntedBy)?null:candidates.FirstOrDefault(h=>h.id==enemy.tauntedBy)??heroes.FirstOrDefault(h=>h.id==enemy.tauntedBy);
                var target=forced??approach.hero;
                var distances=forced==null?approach.distances:GoalDistances(s,enemy,new List<TacticalUnit>{target},range);
                var path=intents==null?null:new List<TacticalCell>();int budget=EnemyMoveBudget(s,enemy);
                while(!enemy.rooted&&budget>0&&(Distance(enemy.x,enemy.y,target.x,target.y)>range||!Visible(s,enemy.x,enemy.y,target.x,target.y))){
                    var options=new List<TacticalCell>();for(int d=0;d<4;d++){int x=enemy.x+Dx[d],y=enemy.y+Dy[d];if(Walkable(s,x,y)&&Occupied(s,x,y)==null&&ObjectAt(s,x,y)==null)options.Add(new TacticalCell(x,y));}
                    var chosen=options.Where(c=>MoveCost(s,c.x,c.y)<=budget).OrderBy(c=>MoveCost(s,c.x,c.y)+distances[c.y*width+c.x]).ThenBy(c=>c.y).ThenBy(c=>c.x).FirstOrDefault();
                    if(chosen==null||distances[chosen.y*width+chosen.x]>=distances[enemy.y*width+enemy.x])break;
                    budget-=MoveCost(s,chosen.x,chosen.y);enemy.x=chosen.x;enemy.y=chosen.y;path?.Add(new TacticalCell(enemy.x,enemy.y,TerrainAt(s,enemy.x,enemy.y)));
                }
                bool attacks=Distance(enemy.x,enemy.y,target.x,target.y)<=range&&Visible(s,enemy.x,enemy.y,target.x,target.y);
                int power=EnemyAttackPower(s,enemy),raw=attacks?AttackPower(s,enemy,target,power,""):0;
                string shield=enemy.heroId=="sunwheel"&&s.round%3==0?"轮盾蓄能 +8护盾；":"";
                string forcedNote=forced!=null?"（被喝令）":"";
                intents?.Add(new TacticalIntent{unitId=enemy.id,targetId=target.id,attacks=attacks,x=enemy.x,y=enemy.y,path=path,damage=Math.Min(target.hp,Math.Max(0,raw-target.block)),text=attacks?enemy.name+"将移动至 "+CellName(enemy.x,enemy.y)+"，"+shield+EnemyActionName(s,enemy)+" → "+target.name+"。"+forcedNote:enemy.name+"将向 "+CellName(enemy.x,enemy.y)+" 接近。"});
                if(attacks){
                    bool binding=s.ascentRevision>=3&&TacticalContent.HasAffix(enemy,"binding");
                    if(binding)enemy.affixCounter=(enemy.affixCounter+1)%3;
                    int radius=EnemySplashRadius(s,enemy);foreach(var victim in heroes.Where(h=>Distance(h.x,h.y,target.x,target.y)<=radius).ToList()){
                    Hit(s,enemy,victim,AttackPower(s,enemy,victim,power,""));if(victim.hp>0){string effect=EnemyInflicts(s,enemy);if(effect=="rooted")victim.rooted=true;else if(effect=="weakened")victim.weakened=Math.Max(victim.weakened,2);else if(effect=="vulnerable")victim.vulnerable=Math.Max(victim.vulnerable,2);
                    // 缠缚：每第三次攻击附带定身。
                    if(binding&&enemy.affixCounter==0)victim.rooted=true;
                }}
                }
                enemy.rooted=false;enemy.tauntedBy="";enemy.weakened=Math.Max(0,enemy.weakened-1);enemy.vulnerable=Math.Max(0,enemy.vulnerable-1);enemy.drenched=Math.Max(0,enemy.drenched-1);
            }
        }
        static void StartRound(TacticalState s)
        {
            foreach(var u in s.units.Where(t=>t.team=="enemy")){u.comboMask=0;u.shellUsed=0;if(u.hp>0&&s.ascentRevision>=3&&TacticalContent.HasAffix(u,"plated"))u.block=Math.Min(40,u.block+6);}
            foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0)){
                u.turnEnded=false;u.concealed=0;
                u.ap=u.maxAp+(s.round==1&&s.relics.Contains("bell")?1:0);u.sp=Math.Min(u.maxSp,u.sp+1+(s.relics.Contains("pearl")&&TerrainAt(s,u.x,u.y)=="mist"?1:0));
                u.hp=Math.Min(u.maxHp,u.hp+HealingMod(s,u.regeneration));u.regeneration=0;u.damageBonus=0;u.retaliation=0;u.weakened=Math.Max(0,u.weakened-1);u.vulnerable=Math.Max(0,u.vulnerable-1);
                foreach(string key in u.cooldowns.Keys.ToList())u.cooldowns[key]=Math.Max(0,u.cooldowns[key]-1);
                if(s.relics.Contains("flask"))u.hp=Math.Min(u.maxHp,u.hp+HealingMod(s,2));
                if(s.relics.Contains("lamp")&&TerrainAt(s,u.x,u.y)=="shadow")u.block=Math.Min(40,u.block+3);
                if(s.ascentRevision>=3){
                    if(s.relics.Contains("overheatvalve")&&TerrainAt(s,u.x,u.y)=="pipe")u.damageBonus=Math.Min(12,u.damageBonus+3);
                    if(s.relics.Contains("shoulderline")&&s.units.Any(t=>t.team=="hero"&&t.hp>0&&t.id!=u.id&&Distance(t.x,t.y,u.x,u.y)<=1))u.block=Math.Min(40,u.block+2);
                }
            }
        }
        static void SetupBattle(TacticalState s,bool side)
        {
            // Keep v3 fields for save compatibility; new battles have only a clear-enemy victory condition.
            s.boardWidth=12;s.boardHeight=10;s.sideBattle=side;s.round=1;s.objectiveX=6;s.objectiveY=6;s.objectiveProgress=0;s.objectiveTurn=-1;s.objectiveRequired=0;s.surviveRounds=0;s.terrain.Clear();
            string[] layout=TacticalContent.BattleLayouts[s.chapter];
            string[] palette={"plain","plain","cover","mist","shadow"};
            for(int y=0;y<BoardHeight(s);y++)for(int x=0;x<BoardWidth(s);x++){
                string kind=TacticalContent.LayoutKind(layout[y][x]);
                if(kind=="plain")kind=palette[(int)(Next(s)%palette.Length)];
                s.terrain.Add(new TacticalCell(x,y,kind));
            }
            // Accessible starting support tiles, and objects beside approaches rather than inside bottlenecks.
            s.terrain.First(c=>c.x==2&&c.y==2).kind="mist";s.terrain.First(c=>c.x==1&&c.y==1).kind="shadow";s.terrain.First(c=>c.x==1&&c.y==3).kind="shadow";
            s.objects.Clear();int[] ox={3,3,9,10},oy={1,6,2,6};int objects=3+(int)(Next(s)%2);
            for(int i=0;i<objects;i++){s.terrain.First(c=>c.x==ox[i]&&c.y==oy[i]).kind="plain";string objectKind=s.ascentRevision>=2&&i==1?"valve":Next(s)%2==0?"crate":"canister";s.objects.Add(new TacticalObject{id="object-"+s.chapter+"-"+i,kind=objectKind,x=ox[i],y=oy[i],hp=objectKind=="valve"?8:10});}
            s.units.RemoveAll(u=>u.team=="enemy");int[] xs={1,1,2},ys={1,2,1};
            for(int i=0;i<s.squad.Count;i++){var u=FindUnit(s,s.squad[i]);u.x=xs[i];u.y=ys[i];u.ap=u.maxAp+(s.relics.Contains("bell")?1:0);u.sp=u.maxSp;u.block=0;u.rooted=false;u.turnEnded=false;u.weakened=u.vulnerable=u.scorched=u.regeneration=u.damageBonus=u.retaliation=0;u.drenched=0;u.comboMask=0;u.cooldowns.Clear();if(s.relics.Contains("seal"))u.charge=Math.Min(100,u.charge+15);}
            // 全员生还连胜奖励：下一场战斗开局的全队护盾与蓄势，应用后即清零。
            if(s.ascentRevision>=2){
                s.immunityUsed=0;
                // 纸鸢护符与旧账蓄势：进战即结算并递减。
                if(s.kiteBattles>0){s.kiteBattles--;foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0))u.block=Math.Min(40,u.block+5);}
                if(s.zhangBattles>0){s.zhangBattles--;var shadow=FindUnit(s,"yanzhuying");if(shadow!=null&&shadow.hp>0)shadow.charge=Math.Min(100,shadow.charge+20);}
                if(s.pendingShield>0||s.pendingCharge>0){
                    foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0)){u.block=Math.Min(40,u.block+s.pendingShield);u.charge=Math.Min(100,u.charge+s.pendingCharge);}
                    s.pendingShield=0;s.pendingCharge=0;
                }
            }
            int count=s.journeyMode=="ascent"?AscentEnemyCount(s.nodeKind):side?2:(s.chapter==4?4:3);int[] ex={7,9,8,10},ey={3,5,7,8};string[] enemies={"sluice","loom","echo","seal","afterimage"};string[] names={"失控闸卫","缚梦织偶","回声渡影","封签校卫","昼轮蜕影"};
            // The authored battlefield height must not change with encounter population.
            for(int i=0;i<ex.Length;i++)s.terrain.First(c=>c.x==ex[i]&&c.y==ey[i]).kind="plain";
            if(s.journeyMode=="ascent"&&s.ascentRevision>=1){CreateAscentEnemies(s);return;}
            for(int i=0;i<count;i++){int hp=14+s.chapter*3+(s.nodeKind=="elite"?6:0)+((s.journeyMode=="ascent"?s.nodeKind=="boss":s.chapter==4)&&i==0?10:0);s.units.Add(new TacticalUnit{id="enemy-"+s.chapter+"-"+i,heroId=enemies[s.chapter],name=names[s.chapter]+(i+1),team="enemy",x=ex[i],y=ey[i],hp=hp,maxHp=hp,maxAp=0,maxSp=0});}
        }
        static void CheckOutcome(TacticalState s)
        {
            if(s.units.Where(u=>u.team=="hero").All(u=>u.hp<=0)){s.phase="defeat";return;}
            if(s.units.Any(u=>u.team=="enemy"&&u.hp>0))return;
            // 全员生还判定必须在倒下者重新归队之前完成：有人倒下即连胜中断。
            bool flawless=s.units.Where(u=>u.team=="hero").All(u=>u.hp>0);
            s.phase="reward";int[] xs={1,1,2},ys={1,2,1};for(int i=0;i<s.squad.Count;i++){var u=FindUnit(s,s.squad[i]);u.hp=u.hp<=0?Math.Max(1,HealingMod(s,u.maxHp/2)):Math.Min(u.maxHp,u.hp+HealingMod(s,4));u.ap=0;u.block=0;u.x=xs[i];u.y=ys[i];}
            if(s.journeyMode=="ascent"){
                int gold;string note="";
                if(s.ascentRevision>=2){
                    gold=s.nodeKind=="boss"?90:s.nodeKind=="elite"?(s.floor>=9?60:55):s.floor>=9?40:s.floor>=5?35:30;
                    if(s.relics.Contains("worktag"))gold+=5;
                    if(s.relics.Contains("rustwatch"))gold+=10;
                    if(flawless){s.flawlessStreak=Math.Min(12,s.flawlessStreak+1);gold+=10;s.pendingShield=Math.Min(40,s.pendingShield+10);s.pendingCharge=Math.Min(100,s.pendingCharge+10);note=" · 全员生还连胜 "+s.flawlessStreak+"，下一战全员 +10 蓄势与护盾";}
                    else {s.flawlessStreak=0;s.pendingShield=0;s.pendingCharge=0;}
                }
                else gold=s.nodeKind=="boss"?75:s.nodeKind=="elite"?45:30;
                s.gold+=gold;AddLog(s,"敌障清除，获得 "+gold+" 金币。"+note);
            }
            CreateRewards(s);AddLog(s,s.sideBattle?"支路敌障清除。":TacticalStory.Stage(s.chapter).outro);
            // 枕头祝福：接下来的每场战斗胜利后全队恢复 6。
            if(s.journeyMode=="ascent"&&s.ascentRevision>=2&&s.pillowBattles>0){
                foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0))u.hp=Math.Min(u.maxHp,u.hp+HealingMod(s,6));
                s.pillowBattles--;AddLog(s,"旧梦的余温让全队恢复最多 "+HealingMod(s,6)+" 生命（剩 "+s.pillowBattles+" 场）。");
            }
        }
        static void Advance(TacticalState s)
        {s.chapter++;s.phase="story";s.nodeKind="main";s.eventId="";s.routes.Clear();s.rewards.Clear();SetupBattle(s,false);s.phase="story";}
        static void CreateRoutes(TacticalState s)
        {
            string[] kinds={"battle","event","camp","elite"};string[] names={"热管巡路","灯下证人","交班歇脚","封锁旁廊"};string[] texts={"清除一组敌障，再选择一次奖励。","听取现场的请求，选择休整或借用旧物。","全队恢复生命，或一名同行者领悟技能。","更强的敌障，战后仍可选择三项构筑奖励。"};
            var order=Enumerable.Range(0,4).ToList();Shuffle(s,order);s.routes.Clear();for(int i=0;i<3;i++){int k=order[i];s.routes.Add(new TacticalRoute{id="route-"+s.chapter+"-"+i,kind=kinds[k],name=names[k],text=texts[k]});}
        }
        static void CreateRewards(TacticalState s)
        {
            s.rewards.Clear();
            if(s.journeyMode=="ascent"&&s.ascentRevision>=2){
                // 精英战三选一必出稀有以上；普通战走标准深化奖励表。
                if(s.ascentRevision>=3&&s.nodeKind=="elite"){CreateEliteRewards(s);return;}
                CreateDepthRewards(s);return;
            }
            var choices=LearnablePartySkills(s).Select(skill=>new TacticalReward{kind="skill",name=skill.name,text=skill.text+" 已掌握时提升一级，最高三级。",skillId=skill.id,relicId=""}).ToList();
            choices.AddRange(PartyRelicCatalogue(s).Where(r=>!s.relics.Contains(r.id)).Select(r=>new TacticalReward{kind="relic",name=r.name,text=r.text,relicId=r.id,skillId=""}));Shuffle(s,choices);
            while(choices.Count<3)choices.Add(new TacticalReward{kind="supply",name="一碗温汤",text="全队恢复 4 生命。",skillId="",relicId=""});
            for(int i=0;i<3;i++){choices[i].id="reward-"+s.chapter+"-"+i;s.rewards.Add(choices[i]);}
        }
        // 深化奖励表：必含技能与一项非技能，金币行囊兜底，不再出现多个温汤填充。
        static void CreateDepthRewards(TacticalState s)
        {
            var learnable=LearnablePartySkills(s).Select(skill=>new TacticalReward{kind="skill",name=skill.name,text=skill.text+" 已掌握时提升一级，最高三级。",skillId=skill.id,relicId=""}).ToList();
            int amount=s.floor>=9?70:s.floor>=5?55:40;
            var extras=PartyRelicCatalogue(s).Where(r=>!s.relics.Contains(r.id)&&(r.rarity=="common"||r.rarity=="rare")).Select(r=>new TacticalReward{kind="relic",name=r.name,text=TacticalContent.RelicTag(r.rarity)+" · "+r.text,skillId="",relicId=r.id}).ToList();
            extras.Add(new TacticalReward{kind="gold",name="金币行囊",text="直接获得 "+amount+" 金币，自由支配。",skillId="",relicId="",amount=amount});
            Shuffle(s,learnable);Shuffle(s,extras);
            var picks=new List<TacticalReward>();
            var pool=new List<TacticalReward>();pool.AddRange(learnable);pool.AddRange(extras);
            if(learnable.Count>0){picks.Add(learnable[0]);pool.Remove(learnable[0]);}
            if(extras.Count>0){picks.Add(extras[0]);pool.Remove(extras[0]);}
            Shuffle(s,pool);
            while(picks.Count<3&&pool.Count>0){picks.Add(pool[0]);pool.RemoveAt(0);}
            while(picks.Count<3)picks.Add(new TacticalReward{kind="supply",name="一碗温汤",text="全队恢复 4 生命。",skillId="",relicId=""});
            Shuffle(s,picks);
            for(int i=0;i<3;i++){picks[i].id="reward-"+s.chapter+"-"+i;s.rewards.Add(picks[i]);}
        }
        // 精英战奖励：三选一稀有以上遗物（不足时以金币行囊补位）。
        static void CreateEliteRewards(TacticalState s)
        {
            var rares=PartyRelicCatalogue(s).Where(r=>!s.relics.Contains(r.id)&&(r.rarity=="rare"||r.rarity=="elite"||r.rarity=="boss")).Select(r=>new TacticalReward{kind="relic",name=r.name,text=TacticalContent.RelicTag(r.rarity)+" · "+r.text,skillId="",relicId=r.id}).ToList();
            Shuffle(s,rares);
            var picks=rares.Take(3).ToList();
            int amount=s.floor>=9?70:s.floor>=5?55:40;
            while(picks.Count<3)picks.Add(new TacticalReward{kind="gold",name="金币行囊",text="直接获得 "+amount+" 金币，自由支配。",skillId="",relicId="",amount=amount});
            for(int i=0;i<3;i++){picks[i].id="reward-"+s.chapter+"-"+i;s.rewards.Add(picks[i]);}
        }
        // 事件效果分派：按事件 id 结算选择，返回覆盖默认文案的实际结果（可空）。
        static string ResolveEvent(TacticalState s,string choice)
        {
            if(s.ascentRevision<3){
                string relic=choice=="B"?RandomRelic(s):"";
                if(choice=="A")HealSquad(s,4);else GrantRelic(s,relic);
                return choice=="B"&&relic==""?"所有旧物已经备齐，换成一份补给：全队恢复 4 生命。":null;
            }
            s.gold-=EventCost(s.eventId,choice);
            switch(s.eventId)
            {
                case "witness":case "dream":case "water":
                    if(choice=="A"){HealSquad(s,4);return "大家稍作休整，谢过沿途相助的人。";}
                    if(choice=="B"){EventExertion(s,3);GrantRelic(s,RandomRelic(s));return "伙伴们一起出了力，带上谢礼继续赶路。";}
                    if(s.eventId=="dream"){LearnRandom(s);return "灯下的手法记在了心里，同行者又多了一份本领。";}
                    if(s.eventId=="water"){HealSquad(s,8);s.pendingShield=Math.Min(40,s.pendingShield+3);return "药布备妥，医者叮嘱大家照看彼此。";}
                    HealSquad(s,10);EventCharge(s,10);return "新汤热了，工人和伙伴们围坐一桌，精神也足了。";
                case "dreamshop":
                    if(choice=="A"){LearnRandom(s);return "大家听完故事，有人默默记下了新的手法。";}
                    s.pillowBattles=2;return "枕头送给了最能睡的同伴。接下来两场战斗胜利后，全队各恢复最多 "+HealingMod(s,6)+" 生命（随治疗修正变化）。";
                case "vendor":
                    if(choice=="A"){EventExertion(s,4);s.gold+=45;return "货车终于越过闸轨。大家擦去汗水，收下货郎递来的酬金。";}
                    if(choice=="C"){GrantRelic(s,RandomRelicOfRarity(s,"common"));return "工班接手了车轮。货郎从箱底挑出一件谢礼。";}
                    return "小队留下清楚的求援记号，后来的工班会接手。";
                case "potshare":
                    if(choice=="A"){HealSquad(s,12);return "灶火烧旺，伙伴们分完热汤，又帮着添了几把柴。";}
                    HealSquad(s,8);return "掌勺人笑着给每人盛了一份：‘慢点喝，管够。’";
                case "darkrest":
                    if(choice=="A"){foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0))u.charge=Math.Min(100,u.charge+15);return "黑暗中大家调匀了呼吸，睁眼时蓄势满满。全队获得 15 蓄势。";}
                    s.gold+=30;return "借着黑暗的掩护，小队快步穿过了关卡。获得 30 金币。";
                case "valveevent":
                    if(choice=="C"){GrantRelic(s,"overheatvalve");return "工头交出旧阀，再三叮嘱：热得越猛，站得越稳。伙伴们记下了它的危险。";}
                    if(choice=="A"){
                        if(Next(s)%10<7){string rare=RandomRelicOfRarity(s,"rare","elite","boss");GrantRelic(s,rare);return rare==""?"阀轮被合力压住。珍藏旧物已齐，工班改送了一份补给。":"阀轮被合力压住，工班送来一件事先封存的稀有旧物。";}
                        foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0))u.hp=Math.Max(1,u.hp-8);
                        return "蒸汽灼伤了伙伴们。大家互相扶稳，由赶来的工班接手。";
                    }
                    s.gold+=10;return "工班赶到接手，酬谢了消息。获得 10 金币。";
                case "pearlecho":
                    if(choice=="A"){s.pendingShield=Math.Min(40,s.pendingShield+5);s.pendingCharge=Math.Min(100,s.pendingCharge+10);return "沧泠听出前方敌障的脚步，大家照着她的手势先作防备。";}
                    s.gold+=20;return "沉珠被收进行囊，换成了实实在在的盘缠。获得 20 金币。";
                case "moonclue":
                    if(choice=="A"){s.gold+=60;bool owned=s.relics.Contains("moonledger");GrantRelic(s,"moonledger");return owned?"欠款追了回来。旧账已在行囊，额外的谢礼换成补给。":"欠款连本带利追了回来——和那本不祥的旧账一起。";}
                    s.zhangBattles=3;return "晏烛影收起针剑：‘这一程，我出力。’接下来三场她开局获得 20 蓄势。";
                case "grainhaul":
                    if(choice=="A"){var f=FindUnit(s,"lingfeng");if(f!=null)f.hp=Math.Max(1,f.hp-6);string gift=RandomRelicOfRarity(s,"common");GrantRelic(s,gift);return gift==""?"凌风把粮食送到了。旧物已齐，同门把谢礼换成了补给。":"凌风扛起粮袋一路小跑。同门替他擦净汗水，送上一件旧物。";}
                    s.gold+=20;return "粮车有人接手，同门塞了些盘缠。获得 20 金币。";
                case "archiveseal":
                    if(choice=="A"){
                        var upgradable=UpgradeablePartySkills(s).Select(skill=>new{u=SkillRecipient(s,skill.id),k=skill.id}).ToList();
                        if(upgradable.Count>0){var pick=upgradable[(int)(Next(s)%upgradable.Count)];Learn(pick.u,pick.k);return pick.u.name+"的「"+TacticalContent.GetSkill(pick.k).name+"」提升一级。";}
                        return "卷宗核校完毕，但没有可以提升的手法。";
                    }
                    s.gold+=15;return "证物随队放行，署里的酬金很快送到。获得 15 金币。";
                case "nailreturn":
                    if(choice=="C"){GrantRelic(s,"heavywheel");return "沉重闸轮收进行囊，守闸人取来药，嘱咐大家放稳脚步。";}
                    if(choice=="A"){string rare=RandomRelicOfRarity(s,"rare","elite","boss");GrantRelic(s,rare);return rare==""?"旧钉物归原主。珍藏旧物已齐，对方改送了一份补给。":"旧钉物归原主，对方回赠了一件珍藏的稀有旧物。";}
                    s.gold+=15;return "旧钉留在了匣里。获得 15 金币。";
                case "dicestall":
                    if(choice=="A"){
                        if(Next(s)%2==0){s.gold+=40;return "骰盅揭开——赢了！20 金币变成了 40。";}
                        return "骰盅揭开——输了。小队摇摇头离开。";
                    }
                    s.gold+=5;return "小队帮忙收好摊位，收下工人递来的谢钱。";
                case "pipeleak":
                    if(choice=="A"){EventExertion(s,3);GrantRelic(s,"emberfurnace");return "蒸汽被引进废管，伙伴们抹好药，收下了工头的谢礼。";}
                    if(choice=="C"){EventExertion(s,4);s.gold+=25;s.pendingShield=Math.Min(40,s.pendingShield+8);return "主阀合上了。工班付清酬金，也帮小队加固了护具。";}
                    s.gold+=15;return "裂口封好了。工头数出 15 金币工钱。";
                case "kite":
                    if(choice=="A"){EventExertion(s,3);s.kiteBattles=3;return "纸鸢回到了孩子手里。伙伴们裹好擦伤的胳膊，把孩子送的护符系紧。";}
                    if(choice=="C"){HealSquad(s,6);EventCharge(s,10);return "梯子稳稳架好，纸鸢被取了下来。伙伴们坐在阶上，听孩子笑着道谢。";}
                    s.gold+=10;return "孩子被安抚着回家了。工棚主塞来 10 金币谢意。";
                case "oldxu":
                    if(choice=="C"){GrantRelic(s,"rustwatch");return "锈表换了主人。老许把商贩的旧账和工班的酬劳，都一五一十交代清楚。";}
                    if(choice=="A"){s.pendingShield=Math.Min(40,s.pendingShield+4);s.pendingCharge=Math.Min(100,s.pendingCharge+10);return "老许指出了敌障的惯用手法，伙伴们照着他的提醒整理护具。";}
                    return "老许摆摆手继续翻他的废料堆。小队继续赶路。";
                case "genealogy":
                    if(choice=="A"){var f=SkillRecipient(s,"ignite");if(f!=null){Learn(f,"ignite");return "凌风照着缺页练了一遍，刀势里多了些火候。他领悟了「引燎」。";}
                        return null;}
                    s.gold+=20;return "祖谱放回原处。书摊主人送的干粮值 20 金币。";
                default:return null;
            }
        }
        static int EventCost(string eventId,string choice)
        {
            if(choice=="C")return eventId=="witness"||eventId=="dream"||eventId=="vendor"?20:eventId=="water"||eventId=="kite"?15:0;
            if(choice!="A")return 0;
            switch(eventId)
            {
                case "dreamshop":return 25;case "potshare":return 15;case "dicestall":return 20;case "oldxu":return 10;
                default:return 0;
            }
        }
        static string RandomRelicOfRarity(TacticalState s,params string[] rarities)
        {var relics=PartyRelicCatalogue(s).Where(r=>!s.relics.Contains(r.id)&&rarities.Contains(r.rarity)).ToArray();return relics.Length==0?"":relics[(int)(Next(s)%relics.Length)].id;}
        static void LearnRandom(TacticalState s)
        {
            var options=LearnablePartySkills(s).ToList();if(options.Count==0)return;
            var skill=options[(int)(Next(s)%options.Count)];var learner=SkillRecipient(s,skill.id);
            Learn(learner,skill.id);AddLog(s,learner.name+"领悟「"+skill.name+"」。");
        }
        static void Learn(TacticalUnit u,string id){if(!u.skills.Contains(id)){u.skills.Add(id);u.skillLevels[id]=1;}else u.skillLevels[id]=Math.Min(3,u.skillLevels[id]+1);}
        // 蚀月旧账：一切治疗效果打七折。
        internal static int HealingMod(TacticalState s,int amount)
        {return s!=null&&s.ascentRevision>=3&&s.relics.Contains("moonledger")?amount*7/10:amount;}
        // 事件、营火与补给的支援治疗：药囊 +6，薄毯提高一半。
        internal static int SupportHeal(TacticalState s,int amount)
        {
            if(s==null||s.ascentRevision<3)return amount;
            if(s.relics.Contains("medicinebag"))amount+=6;
            if(s.relics.Contains("blanket"))amount=amount*3/2;
            return HealingMod(s,amount);
        }
        static void HealSquad(TacticalState s,int amount){foreach(var u in s.units.Where(t=>t.team=="hero"))u.hp=Math.Min(u.maxHp,u.hp+SupportHeal(s,amount));}
        static void HealSquadPercent(TacticalState s,int percent){foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0))u.hp=Math.Min(u.maxHp,u.hp+SupportHeal(s,u.maxHp*Math.Min(100,percent)/100));}
        static string RandomRelic(TacticalState s){var relics=PartyRelicCatalogue(s).Where(r=>!s.relics.Contains(r.id)&&(r.rarity=="common"||r.rarity=="rare")).ToArray();return relics.Length==0?"":relics[(int)(Next(s)%relics.Length)].id;}
        static void GrantRelic(TacticalState s,string id)
        {
            if(!string.IsNullOrEmpty(id)&&RelicAllowedForParty(s,id)&&!s.relics.Contains(id))
            {
                s.relics.Add(id);
                if(s.ascentRevision>=3&&id=="heavywheel")foreach(var u in s.units.Where(t=>t.team=="hero"&&t.hp>0))u.hp=Math.Min(u.maxHp,u.hp+HealingMod(s,6));
            }
            else HealSquad(s,4);
        }
        static uint Next(TacticalState s){uint x=s.rng;x^=x<<13;x^=x>>17;x^=x<<5;s.rng=x==0?0x9e3779b9u:x;return s.rng;}
        static void Shuffle<T>(TacticalState s,List<T> values){for(int i=values.Count-1;i>0;i--){int j=(int)(Next(s)%(uint)(i+1));T value=values[i];values[i]=values[j];values[j]=value;}}
        static void AddLog(TacticalState s,string text){s.log.Add(text);if(s.log.Count>60)s.log.RemoveAt(0);}
        public static string CellName(int x,int y){return ((char)('A'+x)).ToString()+(y+1);}
        public static bool Inside(int x,int y){return x>=0&&x<8&&y>=0&&y<8;}
        public static int Distance(int ax,int ay,int bx,int by){return Math.Abs(ax-bx)+Math.Abs(ay-by);}
        static TacticalUnit Occupied(TacticalState s,int x,int y){return s.units.FirstOrDefault(u=>u.hp>0&&u.x==x&&u.y==y);}
        static int PathCost(TacticalState s,List<TacticalCell> path){return path.Sum(c=>MoveCost(s,c.x,c.y));}
        static List<TacticalCell> Path(TacticalState s,TacticalUnit u,int tx,int ty)
        {
            int width=BoardWidth(s),start=u.y*width+u.x,goal=ty*width+tx;var distances=SearchPaths(s,u,out var previous);
            if(distances[goal]>=999)return null;var result=new List<TacticalCell>();while(goal!=start){result.Add(new TacticalCell(goal%width,goal/width,TerrainAt(s,goal%width,goal/width)));goal=previous[goal];if(goal<0)return null;}result.Reverse();return result;
        }
        static void NavigationGrid(TacticalState s,TacticalUnit unit,out string[] kinds,out bool[] blocked)
        {
            int width=BoardWidth(s),count=width*BoardHeight(s);kinds=Enumerable.Repeat("plain",count).ToArray();blocked=new bool[count];
            foreach(var cell in s.terrain){int index=cell.y*width+cell.x;kinds[index]=cell.kind;blocked[index]=cell.kind=="wall"||cell.kind=="gap";}
            foreach(var u in s.units)if(u.hp>0&&u.id!=unit.id)blocked[u.y*width+u.x]=true;
            foreach(var obj in s.objects)if(obj.hp>0)blocked[obj.y*width+obj.x]=true;
        }
        static int TileCost(string kind){return kind=="pipe"||kind=="rubble"?2:1;}
        static int[] SearchPaths(TacticalState s,TacticalUnit unit,out int[] previous)
        {
            int width=BoardWidth(s),count=width*BoardHeight(s);NavigationGrid(s,unit,out var kinds,out var blocked);
            int[] distances=Enumerable.Repeat(999,count).ToArray();previous=Enumerable.Repeat(-1,count).ToArray();var visited=new bool[count];distances[unit.y*width+unit.x]=0;
            for(int iteration=0;iteration<count;iteration++){
                int index=-1;for(int i=0;i<count;i++)if(!visited[i]&&(index<0||distances[i]<distances[index]))index=i;
                if(index<0||distances[index]>=999)break;visited[index]=true;
                int x=index%width,y=index/width;
                for(int d=0;d<4;d++){int nx=x+Dx[d],ny=y+Dy[d];if(!Inside(s,nx,ny))continue;int next=ny*width+nx;if(blocked[next])continue;int cost=distances[index]+TileCost(kinds[next]);if(cost<distances[next]){distances[next]=cost;previous[next]=index;}}
            }
            return distances;
        }
        static bool Visible(TacticalState s,int ax,int ay,int bx,int by,bool ignoreObjects=false)
        {
            int dx=Math.Abs(bx-ax),sx=ax<bx?1:-1,dy=-Math.Abs(by-ay),sy=ay<by?1:-1,error=dx+dy,x=ax,y=ay;
            while(x!=bx||y!=by){int twice=2*error;if(twice>=dy){error+=dy;x+=sx;}if(twice<=dx){error+=dx;y+=sy;}if((x!=bx||y!=by)&&(TerrainAt(s,x,y)=="wall"||!ignoreObjects&&ObjectAt(s,x,y)!=null))return false;}return TerrainAt(s,bx,by)!="wall";
        }
        public static TacticalRequest Suggest(TacticalState s)
        {
            if(s.phase=="map"){var nodes=AvailableNodes(s);var node=nodes.FirstOrDefault(n=>n.kind=="event")??nodes.FirstOrDefault(n=>n.kind=="shop")??nodes.FirstOrDefault(n=>n.kind=="battle")??nodes.FirstOrDefault();return node==null?null:new TacticalRequest{type="node",choice=node.id};}
            if(s.phase=="shop"){foreach(var offer in s.shopOffers.OrderBy(o=>o.kind=="relic"?0:o.kind=="heal"?1:2))foreach(var u in s.units.Where(u=>u.team=="hero")){var buy=new TacticalRequest{type="buy",choice=offer.id,unitId=u.id};if(Preview(s,buy).ok)return buy;}return new TacticalRequest{type="leaveShop"};}
            if(s.phase=="story")return new TacticalRequest{type="begin"};
            if(s.phase=="route"){var route=s.routes.FirstOrDefault(r=>r.kind=="camp")??s.routes.FirstOrDefault(r=>r.kind=="event")??s.routes[0];return new TacticalRequest{type="route",choice=route.id};}
            if(s.phase=="camp"){
                if(s.units.Any(u=>u.team=="hero"&&u.hp>0&&u.hp<u.maxHp))return new TacticalRequest{type="camp",choice="rest"};
                if(LearnablePartySkills(s).Any())return new TacticalRequest{type="camp",choice="study"};
                if(s.journeyMode=="ascent"&&s.ascentRevision>=2)return new TacticalRequest{type="camp",choice="ward"};
                return new TacticalRequest{type="camp",choice="rest"};
            }
            if(s.phase=="event"){var choice=CurrentEvent(s)?.choices.FirstOrDefault(c=>c.available);return choice==null?null:new TacticalRequest{type="event",choice=choice.id};}
            if(s.phase=="reward"){
                foreach(var r in s.rewards.OrderBy(r=>r.kind=="relic"?0:1))foreach(var u in s.units.Where(t=>t.team=="hero")){
                    var request=new TacticalRequest{type="reward",choice=r.id,unitId=u.id};if(Preview(s,request).ok)return request;}
            }
            if(s.phase!="battle")return null;
            var heroes=s.units.Where(u=>u.team=="hero"&&u.hp>0&&!u.turnEnded).ToList();var enemies=s.units.Where(u=>u.team=="enemy"&&u.hp>0).ToList();
            if(enemies.Count==0)return new TacticalRequest{type="endTurn"};
            foreach(var u in heroes)foreach(string skillId in u.skills){var skill=TacticalContent.GetSkill(skillId);if(skill.kind!="heal")continue;foreach(var ally in heroes.Where(t=>t.hp<t.maxHp-6).OrderBy(t=>t.hp)){
                var request=new TacticalRequest{type="skill",unitId=u.id,skillId=skillId,x=ally.x,y=ally.y};if(Preview(s,request).ok)return request;}}
            TacticalRequest best=null;int score=-1;
            foreach(var u in heroes)foreach(string skillId in u.skills){var skill=TacticalContent.GetSkill(skillId);if(skill.kind!="attack"&&skill.kind!="bind"&&skill.kind!="throw")continue;foreach(var e in enemies){var request=new TacticalRequest{type="skill",unitId=u.id,skillId=skillId,x=e.x,y=e.y};var p=Preview(s,request);if(p.ok){int value=p.damage+(p.damage>=e.hp?10:0)+p.affected.Count*2;if(value>score){score=value;best=request;}}}}
            if(best!=null)return best;
            foreach(var u in heroes.OrderByDescending(t=>t.ap)){
                int width=BoardWidth(s);var cells=MoveCells(s,u.id);if(cells.Count==0)continue;var distances=GoalDistances(s,u,enemies);int current=distances[u.y*width+u.x];var cell=cells.OrderBy(c=>distances[c.y*width+c.x]).ThenBy(c=>TerrainAt(s,c.x,c.y)=="pipe"?1:0).ThenBy(c=>Distance(u.x,u.y,c.x,c.y)).First();
                if(distances[cell.y*width+cell.x]<current)return new TacticalRequest{type="move",unitId=u.id,x=cell.x,y=cell.y};
            }
            foreach(var u in heroes)foreach(string skillId in u.skills){var skill=TacticalContent.GetSkill(skillId);if(skill.kind!="guard")continue;var request=new TacticalRequest{type="skill",unitId=u.id,skillId=skillId,x=u.x,y=u.y};if(Preview(s,request).ok)return request;}
            return new TacticalRequest{type="endTurn"};
        }
        static int[] GoalDistances(TacticalState s,TacticalUnit unit,List<TacticalUnit> enemies,int range=1)
        {
            int width=BoardWidth(s),count=width*BoardHeight(s);NavigationGrid(s,unit,out var kinds,out var blocked);var distances=Enumerable.Repeat(999,count).ToArray();var visited=new bool[count];
            for(int y=0;y<BoardHeight(s);y++)for(int x=0;x<width;x++){
                if(!blocked[y*width+x]&&enemies.Any(e=>Distance(x,y,e.x,e.y)<=range&&Visible(s,x,y,e.x,e.y)))distances[y*width+x]=0;
            }
            for(int iteration=0;iteration<count;iteration++){
                int index=-1;for(int i=0;i<count;i++)if(!visited[i]&&(index<0||distances[i]<distances[index]))index=i;
                if(index<0||distances[index]>=999)break;visited[index]=true;int x=index%width,y=index/width;
                for(int d=0;d<4;d++){int nx=x+Dx[d],ny=y+Dy[d];if(!Inside(s,nx,ny))continue;
                    int next=ny*width+nx;if(blocked[next])continue;int cost=distances[index]+TileCost(kinds[index]);if(cost<distances[next])distances[next]=cost;
                }
            }
            return distances;
        }
    }
}
