using System;
using System.Collections.Generic;
using System.Linq;

namespace Dicebound.Tactics
{
    [Serializable] public sealed class TacticalCell
    {
        public int x,y; public string kind;
        public TacticalCell() { }
        public TacticalCell(int x,int y,string kind="plain") {this.x=x;this.y=y;this.kind=kind;}
    }
    [Serializable] public sealed class TacticalUnit
    {
        public string id,heroId,name,team;
        public string enemyType="";
        public int x,y,hp,maxHp,ap,maxAp,sp,maxSp,charge,block;
        public int weakened,vulnerable,regeneration,damageBonus,retaliation,scorched;
        // Depth revision 2: drenched rounds on enemies, plus a per-round mask of which squad heroes hit them.
        public int drenched,comboMask;
        // P1: enemy affixes ("plated+swift"), per-round affix state, and the hero stealth stance.
        public string affixes="";
        public int shellUsed,affixCounter,concealed;
        // P1: 嘲讽来源（英雄 id），敌人行动后清除。
        public string tauntedBy="";
        public int attackPower,attackRange,moveRange;
        public bool rooted,turnEnded;
        public List<string> skills=new List<string>();
        public Dictionary<string,int> cooldowns=new Dictionary<string,int>(),skillLevels=new Dictionary<string,int>();
        public TacticalUnit Clone() {var u=(TacticalUnit)MemberwiseClone();u.skills=new List<string>(skills);u.cooldowns=new Dictionary<string,int>(cooldowns);u.skillLevels=new Dictionary<string,int>(skillLevels);return u;}
    }
    [Serializable] public sealed class TacticalRoute
    {public string id,kind,name,text;}
    [Serializable] public sealed class TacticalReward
    {public string id,kind,name,text,skillId,relicId;public int amount;}
    [Serializable] public sealed class TacticalObject
    {public string id,kind;public int x,y,hp;}
    [Serializable] public sealed class TacticalMapNode
    {
        public string id,kind,name,text,eventId;
        public int floor,lane,chapter;
        public bool visited,completed;
        public List<string> next=new List<string>();
        public TacticalMapNode Clone(){var node=(TacticalMapNode)MemberwiseClone();node.next=new List<string>(next);return node;}
    }
    [Serializable] public sealed class TacticalShopOffer
    {public string id,kind,name,text,skillId,relicId;public string heroId="";public int price;public bool sold;}
    [Serializable] public sealed class TacticalState
    {
        public int version=3,revision,chapter,round,objectiveX,objectiveY,objectiveProgress,objectiveRequired,surviveRounds,objectiveTurn=-1;
        public int boardWidth=8,boardHeight=8;
        public uint seed,rng;
        public string phase,nodeKind,eventId;
        public bool sideBattle;
        public string journeyMode="legacy",currentNodeId="";
        public int floor=-1,gold,ascentRevision,shopRerolls;
        // 0 preserves an archived shelf; 4 uses the current party's acquisition policy.
        public int shopRevision;
        // Depth revision 2: flawless-win streak, and start-of-next-battle bonuses granted after a flawless win.
        public int flawlessStreak,pendingShield,pendingCharge;
        // P1: ascent trial level and timed blessings granted by road events.
        public int trial,pillowBattles,zhangBattles,kiteBattles,immunityUsed;
        public List<TacticalMapNode> mapNodes=new List<TacticalMapNode>();
        public List<TacticalShopOffer> shopOffers=new List<TacticalShopOffer>();
        public List<string> squad=new List<string>(),relics=new List<string>(),log=new List<string>();
        public List<TacticalUnit> units=new List<TacticalUnit>();
        public List<TacticalObject> objects=new List<TacticalObject>();
        public List<TacticalCell> terrain=new List<TacticalCell>();
        public List<TacticalRoute> routes=new List<TacticalRoute>();
        public List<TacticalReward> rewards=new List<TacticalReward>();
        public TacticalState Clone()
        {
            var s=(TacticalState)MemberwiseClone();s.squad=new List<string>(squad);s.relics=new List<string>(relics);s.log=new List<string>(log);
            s.units=units.Select(u=>u.Clone()).ToList();s.terrain=terrain.Select(c=>new TacticalCell(c.x,c.y,c.kind)).ToList();
            s.objects=objects.Select(o=>new TacticalObject{id=o.id,kind=o.kind,x=o.x,y=o.y,hp=o.hp}).ToList();
            s.mapNodes=mapNodes.Select(n=>n.Clone()).ToList();
            s.shopOffers=shopOffers.Select(o=>new TacticalShopOffer{id=o.id,kind=o.kind,name=o.name,text=o.text,skillId=o.skillId,relicId=o.relicId,heroId=o.heroId,price=o.price,sold=o.sold}).ToList();
            s.routes=routes.Select(r=>new TacticalRoute{id=r.id,kind=r.kind,name=r.name,text=r.text}).ToList();
            s.rewards=rewards.Select(r=>new TacticalReward{id=r.id,kind=r.kind,name=r.name,text=r.text,skillId=r.skillId,relicId=r.relicId,amount=r.amount}).ToList();return s;
        }
    }
    public sealed class TacticalRequest
    {public string type,unitId,skillId,choice;public int x,y;}
    public sealed class TacticalPreview
    {
        public bool ok;public string reason,resource,objectId;
        public int cost,damage,heal,block;
        public List<TacticalCell> affected=new List<TacticalCell>(),path=new List<TacticalCell>();
    }
    public sealed class TacticalIntent
    {public string unitId,targetId,text;public bool attacks;public int x,y,damage;public List<TacticalCell> path=new List<TacticalCell>();}
    public sealed class TacticalSkill
    {public string id,name,text,kind,resource,heroId;public string effect="",condition="";public int cost,range,cooldown,power,push,area,powerStep=2,secondaryPower,secondaryStep=1;}
    public sealed class TacticalHero
    {public string id,name,title,style;public int maxHp,maxAp,maxSp;public string[] skills;}
    public sealed class TacticalRelic
    {public string id,name,text;public string rarity="common";}
}
