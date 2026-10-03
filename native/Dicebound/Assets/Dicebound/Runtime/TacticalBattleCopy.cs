using System.Collections.Generic;
using System.Linq;
using Dicebound.Tactics;

namespace Dicebound.Presentation
{
    // Short battlefield copy; complete rules remain in the inspectable skill detail.
    public static class TacticalBattleCopy
    {
        public static string StatusSummary(TacticalUnit unit)
        {
            var tags=new List<string>();
            if(unit.rooted)tags.Add("定身");if(unit.weakened>0)tags.Add("削弱");if(unit.vulnerable>0)tags.Add("易伤");
            if(unit.scorched>0)tags.Add("余焰");if(unit.drenched>0)tags.Add("淬水");
            if(unit.damageBonus>0)tags.Add("鼓舞");if(unit.retaliation>0)tags.Add("守势");if(unit.regeneration>0)tags.Add("续息");if(unit.concealed>0)tags.Add("影遁");
            return tags.Count==0?"状态 · 查看详情  ›":"状态 · "+string.Join(" / ",tags.Take(2))+(tags.Count>2?" +"+(tags.Count-2):"")+"  ›";
        }
        public static string SkillPurpose(TacticalState state,TacticalUnit unit,TacticalSkill skill)
        {
            int power=TacticalRules.SkillPower(unit,skill),extra=TacticalRules.SkillSecondaryPower(unit,skill);
            string first=skill.kind=="heal"?"恢复 "+power+" 生命":skill.kind=="guard"?"获得 "+power+" 护盾":skill.kind=="haste"?"恢复 "+power+" AP":skill.kind=="restore"?"恢复 "+power+" SP":skill.kind=="empower"?"本轮伤害 +"+power:(skill.area>0?"群攻·基础 ":"基础伤害 ")+power;
            string second=skill.area>=99?"支援全队同伴":skill.area>0?"同时作用于邻格":skill.kind=="attack"?"射程 "+TacticalRules.EffectiveSkillRange(state,unit,skill)+" 格":"支援所选同伴";
            if(skill.kind=="passive")return "强化守势反击\n追加护盾的 "+(20+5*(TacticalRules.SkillLevel(unit,skill.id)-1))+"%";
            if(skill.kind=="step")return "借影移动 "+TacticalRules.EffectiveSkillRange(state,unit,skill)+" 格\n落影恢复 1 SP";
            if(skill.kind=="throw")return "投掷伤害 "+power+"\n热罐波及邻格";
            if(skill.kind=="bind")second=skill.heroId=="cangling"?"定身并附加淬水":"定身目标一轮";
            if(skill.push>0)second="击退"+(skill.push+(state!=null&&state.relics.Contains("valvecore")?1:0))+"格·碰撞增伤";
            switch(skill.effect)
            {
                case "cleanse":second="清除负面状态";break;
                case "vulnerable":second="施加易伤";break;
                case "weaken":second=skill.heroId=="cangling"?"削弱并施加淬水":"削弱敌方伤害";break;
                case "eclipse":second="穿盾并施加削弱";break;
                case "scorch":second="附加 "+extra+" 点余焰";break;
                case "retaliate":second="近身反击 "+extra+" 伤害";break;
                case "regen":second="下轮恢复 "+extra+" 生命";break;
                case "restoresp":second=(skill.kind=="guard"?"目标":"自身")+"恢复 "+extra+" SP";break;
                case "healward":second="净化并获得护盾";break;
                case "bindward":second="定身并为全队加盾";break;
                case "selfguard":case "shadowguard":second="自身护盾 +"+extra;break;
                case "pierce":second="伤害穿透护盾";break;
                case "judgement":second="对受控目标追加6";break;
                case "execute":second=(state!=null&&state.relics.Contains("brokenscabbard")?"六成":"半血")+"以下伤害 +8";break;
                case "verdictcount":second="控制越多越强";break;
                case "settle":return "低血增伤 +8\n击杀返还 1 AP";
                case "ignite":return "结算全部余焰"+(power>0?" +"+power:"")+"\n淬水时蒸汽爆散";
                case "transfer":second="净化并转诉负面";break;
                case "apcharge":second="自身蓄势 +"+extra;break;
                case "quakepush":second="击退邻格·碰撞增伤";break;
                case "pull":second="拉近并附加淬水";break;
                case "taunt":second="喝令2格内敌人";break;
                case "depthcleanse":return "清除自身负面状态\n每项获得 "+extra+" 护盾";
                case "stealth":return (power>0?"影遁 · 护盾 +"+power:"施术或下轮解除")+"\n敌人不优先瞄准";
            }
            if(skill.id=="strike")second="高地攻击额外增伤";
            if(skill.id=="moon")second="阴影出手伤害 +3";
            if(skill.id=="tidemend")second="水雾施术额外治疗";
            return first+"\n"+second;
        }
    }
    internal static class TacticalBattlePresentation
    {
        internal static bool ShouldFaceTarget(TacticalUnit actor,TacticalRequest request)
        {return actor!=null&&request!=null&&request.type=="skill"&&(request.x!=actor.x||request.y!=actor.y);}
        internal static bool KeepFacingOnRebuild(TacticalState before,TacticalState after)
        {return before!=null&&after!=null&&before.seed==after.seed&&before.chapter==after.chapter&&before.currentNodeId==after.currentNodeId&&before.sideBattle==after.sideBattle&&before.nodeKind==after.nodeKind&&TacticalRules.BoardWidth(before)==TacticalRules.BoardWidth(after)&&TacticalRules.BoardHeight(before)==TacticalRules.BoardHeight(after);}
    }
}
