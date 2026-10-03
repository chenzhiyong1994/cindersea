using System;
using System.Linq;
namespace Dicebound.Tactics
{
    public static class TacticalValidation
    {
        static readonly string[] Phases={"story","battle","reward","route","event","camp","victory","defeat","map","shop"};
        public static bool Valid(TacticalState s)
        {
            if(s==null||s.version!=3||s.revision<0||s.revision>10000000||s.chapter<0||s.chapter>4||s.round<1||s.round>10000||s.rng==0||!Phases.Contains(s.phase))return false;
            bool ascent=s.journeyMode=="ascent";
            if(s.journeyMode!="legacy"&&!ascent||s.currentNodeId==null||s.mapNodes==null||s.shopOffers==null)return false;
            if(ascent){if(!TacticalRules.ValidAscent(s))return false;}
            else if(s.ascentRevision!=0||s.shopRerolls!=0||s.shopRevision!=0||s.mapNodes.Count!=0||s.shopOffers.Count!=0||s.floor!=-1||s.gold!=0||s.currentNodeId!=""||s.phase=="map"||s.phase=="shop")return false;
            if(s.nodeKind==null||!(ascent?new[]{"main","battle","elite","event","shop","camp","boss"}:new[]{"main","battle","elite","event","camp"}).Contains(s.nodeKind)||s.eventId==null)return false;
            if(s.squad==null||s.squad.Count!=3||s.squad.Distinct().Count()!=3||s.squad.Any(id=>TacticalContent.GetHero(id)==null))return false;
            if(s.relics==null||s.relics.Count>TacticalRules.AvailableRelicCatalogue(s).Count()||s.relics.Distinct().Count()!=s.relics.Count||s.relics.Any(id=>!TacticalRules.AvailableRelicCatalogue(s).Any(r=>r.id==id)))return false;
            if(s.log==null||s.log.Count>60||s.log.Any(line=>line==null||line.Length>5000))return false;
            // Depth revision 2 journey fields: bounded, and zero for every older mode.
            bool depth=ascent&&s.ascentRevision>=2;
            bool expanded=ascent&&s.ascentRevision>=3;
            if(s.flawlessStreak<0||s.flawlessStreak>12||s.pendingShield<0||s.pendingShield>40||s.pendingCharge<0||s.pendingCharge>100)return false;
            if(!depth&&(s.flawlessStreak!=0||s.pendingShield!=0||s.pendingCharge!=0))return false;
            if(s.trial<0||s.trial>10||s.pillowBattles<0||s.pillowBattles>3||s.zhangBattles<0||s.zhangBattles>3||s.kiteBattles<0||s.kiteBattles>3||s.immunityUsed<0||s.immunityUsed>1)return false;
            if(!expanded&&(s.trial!=0||s.pillowBattles!=0||s.zhangBattles!=0||s.kiteBattles!=0||s.immunityUsed!=0))return false;
            if(!(s.boardWidth==8&&s.boardHeight==8)&&!(s.boardWidth==12&&s.boardHeight==10))return false;
            int width=s.boardWidth,count=width*s.boardHeight;
            if(s.terrain==null||s.terrain.Count!=count||s.terrain.Any(c=>c==null||!TacticalRules.Inside(s,c.x,c.y)||!TacticalContent.TerrainKinds.Contains(c.kind))||s.terrain.Select(c=>c.y*width+c.x).Distinct().Count()!=count)return false;
            if(s.objectiveX!=6||s.objectiveY!=6)return false;
            // Accept both the original v3 objective data and the neutral fields used by new battles.
            // Legacy values remain serialized, but no longer decide combat progress or victory.
            int legacyRequired=s.sideBattle?0:new[]{1,2,3,2,3}[s.chapter],legacySurvive=!s.sideBattle&&s.chapter==4?3:1;
            bool legacy=s.objectiveRequired==legacyRequired&&s.surviveRounds==legacySurvive;
            bool combatOnly=s.objectiveRequired==0&&s.surviveRounds==0;
            if(legacy&&!TacticalRules.Walkable(s,6,6))return false;
            if(!legacy&&!combatOnly||s.objectiveProgress<0||s.objectiveProgress>s.objectiveRequired||s.objectiveTurn< -1||s.objectiveTurn>s.round)return false;
            if((s.objectiveProgress==0)!=(s.objectiveTurn==-1))return false;
            if(s.units==null||s.units.Count<3||s.units.Count>(expanded?8:7)||s.units.Any(u=>u==null)||s.units.Select(u=>u.id).Distinct().Count()!=s.units.Count)return false;
            if(s.units.Count!=3+(ascent?(s.phase=="battle"||s.phase=="reward"||s.phase=="defeat"?TacticalRules.AscentEnemyCount(s):0):s.sideBattle?2:s.chapter==4?4:3))return false;
            if(s.units.Count(u=>u.team=="hero")!=3||s.units.Where(u=>u.team=="hero").Any(u=>!s.squad.Contains(u.id)))return false;
            foreach(var u in s.units){
                if(string.IsNullOrEmpty(u.id)||u.id.Length>80||string.IsNullOrEmpty(u.name)||u.name.Length>80||!TacticalRules.Inside(s,u.x,u.y)||u.hp<0||u.maxHp<1||u.hp>u.maxHp||u.block<0||u.block>40||u.charge<0||u.charge>100)return false;
                if(u.hp>0&&!TacticalRules.Walkable(s,u.x,u.y))return false;
                if(u.ap<0||u.ap>u.maxAp+1||u.sp<0||u.sp>u.maxSp||u.maxAp<0||u.maxSp<0||u.hp==0&&(u.ap!=0||u.block!=0))return false;
                int scorchCap=expanded&&s.relics.Contains("emberfurnace")?18:12;
                if(u.weakened<0||u.weakened>3||u.vulnerable<0||u.vulnerable>3||u.regeneration<0||u.regeneration>30||u.damageBonus<0||u.damageBonus>12||u.retaliation<0||u.retaliation>15||u.scorched<0||u.scorched>scorchCap)return false;
                if(u.drenched<0||u.drenched>3||u.comboMask<0||u.comboMask>7)return false;
                if(!depth&&(u.drenched!=0||u.comboMask!=0))return false;
                if(u.affixes==null||(u.affixes!=""&&u.team!="enemy")||!expanded&&u.affixes!="")return false;
                if(u.affixes!="")
                {
                    var parts=u.affixes.Split('+');if(parts.Length>2||parts.Distinct().Count()!=parts.Length||parts.Any(p=>!TacticalContent.AffixIds.Contains(p)))return false;
                }
                if(u.tauntedBy==null||u.tauntedBy.Length>80||!expanded&&u.tauntedBy!="")return false;
                if(u.tauntedBy!=""&&(u.team!="enemy"||!s.squad.Contains(u.tauntedBy)))return false;
                if(u.shellUsed<0||u.shellUsed>1||u.affixCounter<0||u.affixCounter>2||u.concealed<0||u.concealed>1)return false;
                if(!expanded&&(u.shellUsed!=0||u.affixCounter!=0||u.concealed!=0))return false;
                if(u.team!="enemy"&&u.affixCounter!=0)return false;
                if(u.enemyType==null||u.attackPower<0||u.attackPower>30||u.attackRange<0||u.attackRange>5||u.moveRange<0||u.moveRange>4)return false;
                if(u.skills==null||u.cooldowns==null||u.skillLevels==null||u.skills.Distinct().Count()!=u.skills.Count||u.skills.Any(id=>TacticalContent.GetSkill(id)==null))return false;
                if(u.team=="hero"&&u.skills.Any(id=>TacticalContent.GetSkill(id).resource=="charge"&&!string.IsNullOrEmpty(TacticalContent.GetSkill(id).heroId)&&TacticalContent.GetSkill(id).heroId!=u.heroId))return false;
                if(u.cooldowns.Any(c=>!u.skills.Contains(c.Key)||c.Value<0||c.Value>TacticalContent.GetSkill(c.Key).cooldown))return false;
                if(u.skillLevels.Count!=u.skills.Count||u.skills.Any(id=>!u.skillLevels.ContainsKey(id))||u.skillLevels.Any(l=>l.Value<1||l.Value>3))return false;
                if(u.team=="hero"){
                    var hero=TacticalContent.GetHero(u.heroId);if(hero==null||u.id!=hero.id||u.name!=hero.name||u.maxHp!=hero.maxHp||u.maxAp!=hero.maxAp||u.maxSp!=hero.maxSp||u.skills.Count<3||u.skills.Count>12||!u.skills.Contains("strike")||!u.skills.Contains("throw")||u.enemyType!=""||u.attackPower!=0||u.attackRange!=0||u.moveRange!=0)return false;
                    if(u.turnEnded&&u.ap!=0)return false;
                }
                else if(u.team=="enemy"){
                    int index;string prefix="enemy-"+s.chapter+"-";
                    if(!u.id.StartsWith(prefix,StringComparison.Ordinal)||!int.TryParse(u.id.Substring(prefix.Length),out index)||index<0||index>=(ascent?TacticalRules.AscentEnemyCount(s):s.sideBattle?2:s.chapter==4?4:3)||u.id!=prefix+index)return false;
                    if(u.maxAp!=0||u.maxSp!=0||u.ap!=0||u.sp!=0||u.charge!=0||u.skills.Count!=0||u.skillLevels.Count!=0||u.cooldowns.Count!=0||u.turnEnded)return false;
                    if(ascent&&s.ascentRevision>=1){
                        var enemy=TacticalStory.GetEnemy(TacticalRules.AscentEnemyId(s,index));
                        string expectedAffixes=TacticalRules.EnemyAffixes(s,index);
                        // 锈坏只在死亡时消耗；之后蒸汽阀仍可改变死亡地格，不能拿当前地形证明死亡历史。
                        bool spentRust=u.hp==0&&expectedAffixes.Split('+').Contains("rusted")&&u.affixes==string.Join("+",expectedAffixes.Split('+').Where(p=>p!="rusted"));
                        if(u.heroId!=enemy.id||u.name!=enemy.name+(index==0?"":" "+(index+1))||u.maxHp!=TacticalRules.AscentEnemyHp(s,index)||u.enemyType!=enemy.rank||u.attackPower!=TacticalRules.AscentEnemyAttack(s,index)||u.attackRange!=enemy.range||u.moveRange!=enemy.move||u.affixes!=expectedAffixes&&!spentRust)return false;
                    }else {
                        string[] enemyIds={"sluice","loom","echo","seal","afterimage"};
                        int hp=14+s.chapter*3+(s.nodeKind=="elite"?6:0)+((ascent?s.nodeKind=="boss":s.chapter==4)&&index==0?10:0);
                        if(u.heroId!=enemyIds[s.chapter]||u.maxHp!=hp||u.enemyType!=""||u.attackPower!=0||u.attackRange!=0||u.moveRange!=0)return false;
                    }
                }else return false;
            }
            if(s.units.Where(u=>u.hp>0).Select(u=>u.y*width+u.x).Distinct().Count()!=s.units.Count(u=>u.hp>0))return false;
            if(s.objects==null||s.objects.Count>4||s.objects.Any(o=>o==null||string.IsNullOrEmpty(o.id)||!o.id.StartsWith("object-"+s.chapter+"-",StringComparison.Ordinal)||o.id.Length>80||!new[]{"crate","canister","valve"}.Contains(o.kind)||!TacticalRules.Inside(s,o.x,o.y)||o.hp<1||o.hp>10||!TacticalRules.Walkable(s,o.x,o.y)||s.units.Any(u=>u.hp>0&&u.x==o.x&&u.y==o.y))||s.objects.Select(o=>o.id).Distinct().Count()!=s.objects.Count||s.objects.Select(o=>o.y*width+o.x).Distinct().Count()!=s.objects.Count)return false;
            foreach(var obj in s.objects){string prefix="object-"+s.chapter+"-";if(!int.TryParse(obj.id.Substring(prefix.Length),out int index)||index<0||index>3||obj.id!=prefix+index)return false;}
            bool heroesAlive=s.units.Any(u=>u.team=="hero"&&u.hp>0),enemiesAlive=s.units.Any(u=>u.team=="enemy"&&u.hp>0);
            if(s.phase=="defeat"&&heroesAlive||s.phase!="defeat"&&!heroesAlive)return false;
            if(s.phase=="reward"||s.phase=="route"||s.phase=="event"||s.phase=="camp"||s.phase=="victory"||s.phase=="map"||s.phase=="shop")if(enemiesAlive)return false;
            if(s.phase=="victory"&&(s.chapter!=4||s.sideBattle))return false;
            if(s.phase=="story"&&(s.sideBattle||s.round!=1||s.objectiveProgress!=0||!enemiesAlive))return false;
            if(s.routes==null||s.rewards==null)return false;
            if(s.phase=="route"){
                if(s.routes.Count!=3||s.routes.Select(r=>r?.id).Distinct().Count()!=3||s.routes.Any(r=>r==null||!new[]{"battle","event","camp","elite"}.Contains(r.kind)||string.IsNullOrEmpty(r.id)||string.IsNullOrEmpty(r.name)||r.text==null))return false;
            }else if(s.routes.Count!=0)return false;
            if(s.phase=="reward"){
                if(s.rewards.Count!=3||s.rewards.Any(r=>r==null)||s.rewards.Select(r=>r.id).Distinct().Count()!=3)return false;
                foreach(var r in s.rewards){if(string.IsNullOrEmpty(r.id)||string.IsNullOrEmpty(r.name)||r.text==null)return false;
                    if(r.kind=="skill"){if(TacticalContent.GetSkill(r.skillId)==null||!string.IsNullOrEmpty(r.relicId))return false;}
                    else if(r.kind=="relic"){if(TacticalContent.GetRelic(r.relicId)==null||s.relics.Contains(r.relicId)||!string.IsNullOrEmpty(r.skillId))return false;}
                    else if(r.kind=="gold"){if(!depth||r.amount<=0||!string.IsNullOrEmpty(r.skillId)||!string.IsNullOrEmpty(r.relicId))return false;}
                    else if(r.kind=="supply"){if(!string.IsNullOrEmpty(r.skillId)||!string.IsNullOrEmpty(r.relicId))return false;}
                    else return false;
                }
            }else if(s.rewards.Count!=0)return false;
            if(s.phase=="event"&&!TacticalStory.Events.Any(e=>e.id==s.eventId))return false;
            return true;
        }
    }
}
