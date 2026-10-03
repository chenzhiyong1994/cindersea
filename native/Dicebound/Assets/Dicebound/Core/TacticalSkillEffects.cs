using System;
using System.Linq;
namespace Dicebound.Tactics
{
    public static partial class TacticalRules
    {
        public static int SkillRange(TacticalUnit unit,TacticalSkill skill)
        {return skill.range+(skill.kind=="step"?SkillLevel(unit,skill.id)-1:0);}
        public static int EffectiveSkillRange(TacticalState state,TacticalUnit unit,TacticalSkill skill)
        {return skill==null?0:SkillRange(unit,skill)+(skill.kind=="throw"&&state!=null&&state.relics.Contains("wrench")?1:0);}
        public static int SkillCooldown(TacticalState state,TacticalSkill skill)
        {return skill==null?0:Math.Max(0,skill.cooldown-(state!=null&&state.relics.Contains("judgeseal")&&skill.id=="sixuan_ultimate"?1:0));}
        static bool FriendlySkill(TacticalSkill skill)
        {return skill.kind=="heal"||skill.kind=="guard"||skill.kind=="haste"||skill.kind=="restore"||skill.kind=="empower";}
        static int MistHealBonus(TacticalState state,TacticalUnit caster,TacticalSkill skill)
        {return TerrainAt(state,caster.x,caster.y)=="mist"&&(caster.heroId=="cangling"||skill.heroId=="cangling")?2:0;}
        static void ClearHarmful(TacticalUnit target)
        {target.rooted=false;target.weakened=0;target.vulnerable=0;target.scorched=0;target.drenched=0;}
        static void Hit(TacticalState state,TacticalUnit attacker,TacticalUnit target,int amount,bool penetrate=false)
        {
            if(state.ascentRevision>=3&&target.hp<=0)return;
            int shieldOnImpact=target.block;
            if(penetrate){int hpBefore=target.hp;target.hp=Math.Max(0,target.hp-amount);ResolveDeath(state,target,hpBefore);}
            else Damage(state,target,amount);
            // Depth revision 2: taking a real enemy hit steadies the victim's ultimate charge.
            if(amount>0&&state.ascentRevision>=2&&attacker.team=="enemy"&&target.team=="hero"&&target.hp>0)target.charge=Math.Min(100,target.charge+10);
            if(amount>0&&target.retaliation>0&&attacker.hp>0&&Distance(attacker.x,attacker.y,target.x,target.y)<=1)
            {
                // 界锤：商朔持盾受击时额外反弹 3。
                int riposte=target.retaliation+(state.relics.Contains("anchorhammer")&&target.heroId=="shangshuo"&&shieldOnImpact>0?3:0);
                Damage(state,attacker,riposte);
            }
        }
        // Shields past the cap are not discarded in revision 2; half of the overflow lingers as regeneration.
        static void AddBlock(TacticalState state,TacticalUnit unit,int amount)
        {
            int added=Math.Max(0,Math.Min(40-unit.block,amount));unit.block+=added;
            int overflow=amount-added;
            if(state.ascentRevision>=2&&overflow>0)unit.regeneration=Math.Min(30,unit.regeneration+overflow/2);
        }
        static void ApplySkillEffect(TacticalState state,TacticalUnit caster,TacticalUnit target,TacticalSkill skill,int secondary)
        {
            // 深化规则：沧泠的攻击与束流术法使敌障淬水，为余焰蒸汽爆与潮汐连招供能。
            if(state.ascentRevision>=2&&skill.heroId=="cangling"&&(skill.kind=="attack"||skill.kind=="bind")&&target.team=="enemy")ApplyDrench(state,target,2);
            // These committed support/terrain effects still finish when the direct
            // hit defeats its target. They do not apply new debuffs to a corpse.
            if(skill.effect=="ignite")
            {
                target.scorched=0;
                if(target.drenched>0)ApplyScorch(state,target,0);
                return;
            }
            if(skill.effect=="transfer")
            {
                int worstWeakened=0,worstVulnerable=0;bool anyRooted=false;
                foreach(var ally in state.units.Where(t=>t.team=="hero"&&t.hp>0))
                {
                    worstWeakened=Math.Max(worstWeakened,ally.weakened);worstVulnerable=Math.Max(worstVulnerable,ally.vulnerable);anyRooted|=ally.rooted;
                    ally.rooted=false;ally.weakened=0;ally.vulnerable=0;
                }
                if(target.hp>0)
                {
                    int extension=state.ascentRevision>=3&&state.relics.Contains("doublepen")?1:0;
                    if(anyRooted)target.rooted=true;
                    if(worstWeakened>0)target.weakened=Math.Max(target.weakened,Math.Min(3,worstWeakened+extension));
                    if(worstVulnerable>0)target.vulnerable=Math.Max(target.vulnerable,Math.Min(3,worstVulnerable+extension));
                }
                return;
            }
            if(target.hp<=0)return;
            bool prolonged=state.ascentRevision>=2&&state.relics.Contains("doublepen");
            switch(skill.effect)
            {
                case "vulnerable":target.vulnerable=Math.Max(target.vulnerable,prolonged?3:2);break;
                case "weaken":target.weakened=Math.Max(target.weakened,prolonged?2:1);break;
                case "eclipse":target.weakened=Math.Max(target.weakened,prolonged?3:2);break;
                case "scorch":ApplyScorch(state,target,secondary);break;
                case "cleanse":ClearHarmful(target);break;
                case "retaliate":
                    // 反击要塞：商朔的守势值额外获得护盾五分之一的加成。
                    int ward=secondary+(state.ascentRevision>=3&&caster.heroId=="shangshuo"&&caster.skills.Contains("bastion")?BastionRetaliationBonus(caster):0);
                    target.retaliation=Math.Min(15,Math.Max(target.retaliation,ward));break;
                case "regen":target.regeneration=Math.Min(30,Math.Max(target.regeneration,secondary));break;
                case "restoresp":
                    var recipient=FriendlySkill(skill)?target:caster;recipient.sp=Math.Min(recipient.maxSp,recipient.sp+secondary);break;
                case "healward":AddBlock(state,target,secondary);ClearHarmful(target);break;
                case "bindward":target.rooted=true;break;
                // —— 深化 P1 新效果 ——
                case "apcharge":caster.charge=Math.Min(100,caster.charge+secondary);break;
                case "quakepush":Push(state,caster,target,1+(state.relics.Contains("valvecore")?1:0));break;
                case "pull":
                {
                    int dx=0,dy=0;
                    if(Math.Abs(caster.x-target.x)>=Math.Abs(caster.y-target.y))dx=Math.Sign(caster.x-target.x);else dy=Math.Sign(caster.y-target.y);
                    int nx=target.x+dx,ny=target.y+dy;
                    if((dx!=0||dy!=0)&&Walkable(state,nx,ny)&&Occupied(state,nx,ny)==null&&ObjectAt(state,nx,ny)==null){target.x=nx;target.y=ny;AddLog(state,target.name+"被拉向 "+CellName(nx,ny)+"。");}
                    break;
                }
                case "taunt":
                    foreach(var foe in state.units.Where(t=>t.team=="enemy"&&t.hp>0&&Distance(t.x,t.y,caster.x,caster.y)<=2).ToList())
                        foe.tauntedBy=caster.id;
                    break;
                case "depthcleanse":
                {
                    int cleared=(target.rooted?1:0)+(target.weakened>0?1:0)+(target.vulnerable>0?1:0)+(target.scorched>0?1:0)+(target.drenched>0?1:0);
                    ClearHarmful(target);target.rooted=false;
                    if(cleared>0)AddBlock(state,target,secondary*cleared);
                    break;
                }
                case "stealth":target.concealed=1;break;
            }
        }
        static int EffectAttackPower(TacticalState state,TacticalUnit caster,TacticalUnit target,TacticalSkill skill,int power)
        {
            // Revision 2 widens "on record" targets to any control verdict the squad has laid down.
            if(skill.effect=="judgement"&&(state.ascentRevision>=2?(target.rooted||target.weakened>0||target.vulnerable>0):(target.rooted||target.vulnerable>0)))power+=6;
            // 断鞘：处决阈值从半血提高到六成。
            if(skill.effect=="execute"&&target.hp*10<=target.maxHp*(state.relics.Contains("brokenscabbard")?6:5))power+=8;
            if((skill.effect=="shadowguard"||skill.effect=="eclipse")&&TerrainAt(state,caster.x,caster.y)=="shadow")power+=3;
            // 定谳一击：目标每带一种控制状态追加 4，最多 12。
            if(skill.effect=="verdictcount")power+=4*Math.Min(3,(target.rooted?1:0)+(target.weakened>0?1:0)+(target.vulnerable>0?1:0)+(target.drenched>0?1:0));
            // 收账：目标生命不足四成时追加 8。
            if(skill.effect=="settle"&&target.hp*10<=target.maxHp*4)power+=8;
            // 引燎：把目标已带的余焰一并结算。
            if(skill.effect=="ignite")power+=target.scorched;
            return AttackPower(state,caster,target,power,skill.id);
        }
        static void FinishSkillEffect(TacticalState state,TacticalUnit caster,TacticalSkill skill,int secondary)
        {
            if(skill.effect=="selfguard"||skill.effect=="shadowguard")AddBlock(state,caster,secondary);
            if(skill.effect=="bindward")foreach(var hero in state.units.Where(u=>u.team=="hero"&&u.hp>0))AddBlock(state,hero,secondary);
        }
        // Revision 2: scorched stacks instead of replacing, and igniting a drenched enemy detonates everything at once.
        internal static void ApplyScorch(TacticalState state,TacticalUnit target,int amount)
        {
            if(state.ascentRevision<2){target.scorched=Math.Min(12,Math.Max(target.scorched,amount));return;}
            int cap=state.relics.Contains("emberfurnace")?18:12;
            if(target.drenched>0){
                // 赤心刀镡：蒸汽爆散伤害提高一半。
                int burst=(target.scorched+amount+3)*(state.relics.Contains("bladetsuba")?3:2)/2;target.scorched=0;target.drenched=0;
                bool alive=target.hp>0;Damage(state,target,burst);AddLog(state,alive?"淬水遇热，"+target.name+"蒸汽爆散，受到 "+burst+" 伤害。":"引燎散尽余焰，残余水汽爆开。");
                foreach(var splash in state.units.Where(t=>t.hp>0&&t.team=="enemy"&&t.id!=target.id&&Distance(t.x,t.y,target.x,target.y)<=1).ToList()){
                    Damage(state,splash,state.ascentRevision>=3&&state.relics.Contains("bladetsuba")?6:4);if(splash.hp>0)splash.drenched=Math.Max(splash.drenched,2);
                }
                AddLog(state,"蒸汽波及邻格敌障。");
                return;
            }
            target.scorched=Math.Min(cap,target.scorched+amount);
            // 烬芯炉：施加时向邻格敌障蔓延一层余焰。
            if(state.relics.Contains("emberfurnace")&&amount>0)
                foreach(var neighbor in state.units.Where(t=>t.hp>0&&t.team=="enemy"&&t.id!=target.id&&Distance(t.x,t.y,target.x,target.y)<=1).ToList())
                    neighbor.scorched=Math.Min(cap,neighbor.scorched+1);
        }
        internal static void ApplyDrench(TacticalState state,TacticalUnit target,int rounds=2)
        {if(state.ascentRevision>=2&&target.hp>0&&target.team=="enemy")target.drenched=Math.Max(target.drenched,rounds);}
        public static string UnitStatusDescription(TacticalUnit unit)
        {
            if(unit==null)return "";var effects=new System.Collections.Generic.List<string>();
            if(unit.turnEnded)effects.Add("本轮行动已结束");if(unit.rooted)effects.Add("定身：无法移动");
            if(unit.weakened>0)effects.Add("削弱：伤害降低3（"+unit.weakened+"轮）");
            if(unit.vulnerable>0)effects.Add("易伤：受伤增加3（"+unit.vulnerable+"轮）");
            if(unit.scorched>0)effects.Add("余焰：行动前受到"+unit.scorched+"伤害");
            if(unit.drenched>0)effects.Add("淬水：遇热将蒸汽爆散（"+unit.drenched+"轮）");
            if(unit.comboMask>0)effects.Add("连携：已被 "+CountComboHits(unit.comboMask)+" 名同行者命中");
            if(unit.damageBonus>0)effects.Add("鼓舞：本轮伤害增加"+unit.damageBonus);
            if(unit.retaliation>0)effects.Add("守势：近身受击反击"+unit.retaliation+"伤害");
            if(unit.regeneration>0)effects.Add("续息：下轮恢复"+unit.regeneration+"生命");
            if(unit.concealed>0)effects.Add("影遁：敌障暂不优先瞄准");
            if(!string.IsNullOrEmpty(unit.affixes))effects.Add("词缀："+string.Join("/",unit.affixes.Split('+').Select(TacticalContent.AffixName)));
            if(!string.IsNullOrEmpty(unit.tauntedBy))effects.Add("被喝令：下轮只能攻击"+unit.tauntedBy);
            return string.Join(" · ",effects);
        }
        static int CountComboHits(int mask){int count=0;while(mask>0){count+=mask&1;mask>>=1;}return count;}
    }
}
