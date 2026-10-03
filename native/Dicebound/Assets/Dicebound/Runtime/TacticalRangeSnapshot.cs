using System.Collections.Generic;
using Dicebound.Tactics;

namespace Dicebound.Presentation
{
    public enum TacticalRangeSemantic { None, Move, Target, Damage, Support, Invalid }

    /// <summary>A read-only presentation query. Preview remains the only authority for changed cells.</summary>
    public sealed class TacticalRangeSnapshot
    {
        public readonly List<TacticalCell> legal = new List<TacticalCell>();
        public readonly List<TacticalCell> affected = new List<TacticalCell>();
        public readonly List<TacticalCell> path = new List<TacticalCell>();
        public TacticalRequest request;
        public TacticalPreview preview;
        public bool locked;
        public TacticalRangeSemantic semantic;
        public static TacticalRangeSnapshot Build(TacticalState state,string unitId,string skillId,TacticalRequest focus,bool locked)
        {
            var result=new TacticalRangeSnapshot{request=Copy(focus),locked=focus!=null&&locked};
            if(state==null||state.phase!="battle")return result;
            var unit=TacticalRules.FindUnit(state,unitId);
            if(unit!=null&&unit.hp>0)
                result.legal.AddRange(skillId==null?TacticalRules.MoveCells(state,unitId):TacticalRules.SkillTargets(state,unitId,skillId));
            if(focus==null)return result;
            result.preview=TacticalRules.Preview(state,focus);
            if(!result.preview.ok){result.semantic=TacticalRangeSemantic.Invalid;return result;}
            result.path.AddRange(result.preview.path);result.affected.AddRange(result.preview.affected);
            var definition=TacticalContent.GetSkill(focus.skillId);
            result.semantic=focus.type=="move"||definition?.kind=="step"?TacticalRangeSemantic.Move:
                definition?.kind=="heal"||definition?.kind=="guard"||definition?.kind=="haste"?TacticalRangeSemantic.Support:TacticalRangeSemantic.Damage;
            return result;
        }
        public static TacticalRequest Copy(TacticalRequest value)
        {return value==null?null:new TacticalRequest{type=value.type,unitId=value.unitId,skillId=value.skillId,choice=value.choice,x=value.x,y=value.y};}
    }
}
