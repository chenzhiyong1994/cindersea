using System;

namespace Dicebound.Tactics
{
    public static partial class TacticalRules
    {
        // Target-independent availability drives both the skill tray and Preview.
        // A null skill id denotes movement; target, path and line-of-sight checks
        // remain in Preview after this shared gate.
        public static TacticalPreview SkillAvailability(TacticalState state,string unitId,string skillId)
        {
            var preview=new TacticalPreview();
            if(state==null||state.phase!="battle")return Denied(preview,"当前不在战场。");
            var unit=FindUnit(state,unitId);
            if(unit==null||unit.team!="hero"||unit.hp<=0)return Denied(preview,"请选择仍能行动的同行者。");
            if(unit.turnEnded)return Denied(preview,"这位同行者已经结束本轮行动，请切换其他同行者。");
            if(skillId==null)
            {
                preview.cost=1;preview.resource="ap";
                if(unit.rooted)return Denied(preview,"本轮被定身，无法移动。");
                return Allowed(unit.ap>=1,preview,"移动需要 1 AP，当前 AP 不足。");
            }
            var skill=TacticalContent.GetSkill(skillId);
            if(skill==null||!unit.skills.Contains(skill.id))return Denied(preview,"尚未掌握这项技能。");
            if(skill.kind=="passive")return Denied(preview,"这是一项常驻生效的被动技能，无需施放。");
            preview.cost=skill.cost;preview.resource=skill.resource;
            if(unit.cooldowns.TryGetValue(skill.id,out int remaining)&&remaining>0)return Denied(preview,skill.name+"还需冷却 "+remaining+" 回合。");
            int available=skill.resource=="sp"?unit.sp:skill.resource=="charge"?unit.charge:unit.ap;
            string resource=skill.resource=="charge"?"蓄势":skill.resource.ToUpperInvariant();
            if(available<skill.cost)return Denied(preview,skill.name+"需要 "+skill.cost+" "+resource+"，当前 "+available+"，还差 "+(skill.cost-available)+"。");
            string terrain=TerrainAt(state,unit.x,unit.y);
            if(skill.condition=="mist"&&terrain!="mist")return Denied(preview,"需要立于水雾中才能施展「"+skill.name+"」。");
            if(skill.condition=="shadow"&&terrain!="shadow")return Denied(preview,"需要立于阴影中才能施展「"+skill.name+"」。");
            if(skill.kind=="step"&&unit.rooted)return Denied(preview,"定身期间无法借影。");
            if(skill.kind=="throw"&&ThrowObject(state,unit,null)==null)return Denied(preview,"身旁需要有可投掷的木箱或热罐。");
            return Allowed(true,preview,null);
        }
    }
}
