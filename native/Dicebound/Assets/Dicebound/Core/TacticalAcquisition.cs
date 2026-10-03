using System;
using System.Collections.Generic;
using System.Linq;

namespace Dicebound.Tactics
{
    public static partial class TacticalRules
    {
        // Existing foreign skills remain playable. This policy governs every new grant.
        public static TacticalUnit SkillRecipient(TacticalState state,string skillId)
        {
            if(state==null||state.squad==null||state.units==null)return null;
            var skill=AvailableSkillCatalogue(state).FirstOrDefault(k=>k.id==skillId);if(skill==null)return null;
            var candidates=state.squad.Select(id=>FindUnit(state,id)).Where(u=>u!=null&&u.team=="hero"&&u.hp>0&&TacticalContent.CanLearn(u,skill));
            if(!string.IsNullOrEmpty(skill.heroId))return candidates.FirstOrDefault(u=>u.heroId==skill.heroId);
            // Shared techniques first upgrade an existing learner; ties use lowest level,
            // then the player's fixed party order, so a grant never depends on UI selection.
            return candidates.OrderBy(u=>u.skills.Contains(skillId)?0:1).ThenBy(u=>u.skills.Contains(skillId)?SkillLevel(u,skillId):0).FirstOrDefault();
        }
        public static bool CanAcquireSkill(TacticalState state,string skillId){return SkillRecipient(state,skillId)!=null;}
        public static string RelicOwnerHeroId(string relicId)
        {
            switch(relicId)
            {
                case "judgeseal":case "jadeimprint":return "sixuan";
                case "brokenscabbard":case "bladetsuba":return "lingfeng";
                case "anchorhammer":return "shangshuo";
                default:return "";
            }
        }
        public static bool RelicAllowedForParty(TacticalState state,string relicId)
        {
            if(state==null||TacticalContent.GetRelic(relicId)==null)return false;
            string owner=RelicOwnerHeroId(relicId);return owner==""||state.squad.Contains(owner);
        }
        static IEnumerable<TacticalRelic> PartyRelicCatalogue(TacticalState state)
        {return AvailableRelicCatalogue(state).Where(r=>RelicAllowedForParty(state,r.id));}
        static IEnumerable<TacticalSkill> LearnablePartySkills(TacticalState state)
        {return AvailableSkillCatalogue(state).Where(k=>CanAcquireSkill(state,k.id));}
        static IEnumerable<TacticalSkill> UpgradeablePartySkills(TacticalState state)
        {return LearnablePartySkills(state).Where(k=>CanUpgradeOwnedSkill(state,SkillRecipient(state,k.id),k.id));}
        // Old saved reward cards remain intact. An unusable skill / exclusive relic is
        // redeemed as a visible supply instead of trapping the run on an old reward page.
        public static TacticalReward EffectiveReward(TacticalState state,TacticalReward reward)
        {
            if(reward==null)return null;
            bool unavailable=reward.kind=="skill"&&!CanAcquireSkill(state,reward.skillId)||reward.kind=="relic"&&!RelicAllowedForParty(state,reward.relicId);
            if(!unavailable)return reward;
            bool gold=state.journeyMode=="ascent";
            return new TacticalReward{id=reward.id,kind=gold?"gold":"supply",name=gold?"折换盘缠":"折换补给",text=gold?"原奖励不适用于当前队伍，领取 40 金币。":"原奖励不适用于当前队伍，全队恢复 4 生命。",amount=gold?40:0,skillId="",relicId=""};
        }
        static bool CanUpgradeOwnedSkill(TacticalState state,TacticalUnit unit,string skillId)
        {
            var skill=TacticalContent.GetSkill(skillId);
            return unit!=null&&unit.hp>0&&unit.skills.Contains(skillId)&&skill!=null&&(string.IsNullOrEmpty(skill.heroId)||skill.heroId==unit.heroId)&&TacticalContent.CanLearn(unit,skill);
        }
    }
}
