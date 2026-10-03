using System;
namespace Dicebound.Tactics
{
    public static class TacticalContent
    {
        public static readonly string[] TerrainKinds={"plain","high","cover","mist","shadow","pipe","wall","gap","rubble"};
        // Each scene keeps two approaches across its central divide. Decor never closes them.
        internal static readonly string[][] BattleLayouts={
            new[]{".....#......",".s...#..c...","..m..s...h..","..c..#..c...","...r.#...s..",".....#......",".s...#..m...","..c..m..c...",".....#...h..",".....#......"},
            new[]{".....#..pp..",".s...#..p...","..m..p...c..","..c..#...m..","...p.#.pp...","..p..#...s..",".....#......","..r..m..h...",".....#..pp..",".....#......"},
            new[]{".....~~.....",".s...~~..c..","..m..~~..m..","..c..hh..c..",".....cc.....","...s.~~..s..","..m..~~.....","..c..rr..h..",".....~~..c..",".....~~....."},
            new[]{".....#......",".s.c.#.c....","..m..c...h..","..##.#..#c..","...c.#..#...",".....#..#s..",".s.c.#...m..","..c..s..c...",".....#...h..",".....#......"},
            new[]{".....#.~....",".s.h.#.~.c..","..m..h.hh...","..c..#..hc..","..rr.#..hh..",".....#...s..",".s...#..m...","..c..c...c..",".....#.~.h..",".....#.~...."}
        };
        internal static string LayoutKind(char tile)
        {
            switch(tile){case '#':return "wall";case '~':return "gap";case 'r':return "rubble";case 'p':return "pipe";case 'c':return "cover";case 'h':return "high";case 'm':return "mist";case 's':return "shadow";default:return "plain";}
        }
        public static readonly TacticalHero[] Heroes={
            Hero("sixuan","司玄","天衡玉律","衡域 · 定身与裁断",32,3,"strike","jadeguard","balance"),
            Hero("yanzhuying","晏烛影","蚀月遗姬","借影 · 侧袭回身",27,3,"strike","step","moon"),
            Hero("lingfeng","凌风","赤心刀","赤心 · 击退破阵",34,2,"strike","shove","cleave"),
            Hero("cangling","沧泠","听潮王嗣","潮汐 · 治疗与压制",29,3,"strike","tidemend","tide"),
            Hero("shangshuo","商朔","镇渊玄戍","界钉 · 守护阵线",38,2,"strike","shove","anchor"),
            Hero("ruanzhuo","阮灼","缝夜人","织线 · 治疗支援",30,3,"strike","brace","stitch")
        };
        static TacticalHero Hero(string id,string name,string title,string style,int hp,int sp,params string[] skills)
        {var values=new System.Collections.Generic.List<string>(skills);if(!values.Contains("throw"))values.Add("throw");values.Add(UltimateId(id));return new TacticalHero{id=id,name=name,title=title,style=style,maxHp=hp,maxAp=2,maxSp=sp,skills=values.ToArray()};}
        public static string UltimateId(string heroId){return heroId+"_ultimate";}
        public static readonly TacticalSkill[] Skills={
            Skill("strike","破障","近身攻击；站在高地攻击低处时增伤2。伤害受目标护盾与掩体影响。","attack","ap",1,1,0,8),
            Skill("throw","投掷旧物","抛出邻接木箱或热罐；热罐波及目标邻格敌障。","throw","ap",1,3,0,7),
            Skill("brace","护人","为自身或邻接同伴提供护盾，最高可持有40护盾。","guard","ap",1,1,1,6),
            Skill("shove","逼退","造成伤害并击退1格；撞墙或单位追加4伤害。仅力量型角色可研习。","attack","ap",1,1,1,4,1),
            Skill("step","借影","移动到空位，升级增加距离；起点或落点须在阴影，抵达阴影恢复1SP。","step","ap",1,3,1,0),
            Skill("balance","衡域","约束可见的敌障，造成伤害并定身一輪。","bind","sp",1,3,0,8),
            Skill("moon","缺月针","近身针刺；施术者在阴影中额外造成3伤害。","attack","sp",1,1,0,10),
            Skill("cleave","赤心横刀","近身刀击并击退1格。仅力量型角色可研习。","attack","sp",1,1,1,10,1),
            Talent("cangling","tide","静海界","水雾中震动压制目标及邻接敌障，削弱一轮并淬水；遇热即蒸汽爆散。","attack",1,3,1,6,1,"weaken","mist"),
            Skill("anchor","镇渊界","自身及邻接同伴获得护盾。","guard","sp",1,0,1,9,0,1),
            Skill("stitch","缝夜针","借当地热网为同伴恢复生命；不复活战斗中倒下者。","heal","sp",1,3,1,8),
            Skill("line","穿隙","攻击可见的远处敌障。","attack","sp",1,3,0,7),
            Skill("mend","温汤","为同伴恢复生命。","heal","sp",1,2,1,6),
            Skill("burst","震管","对目标及邻接敌障造成伤害。","attack","sp",2,2,2,7,0,1),
            Skill("hook","牵线","术法牵线造成伤害并移退2格。","attack","sp",1,3,1,4,2),
            Skill("haste","换班","为同伴恢复AP，最多达到基础AP+1；不能恢复已结束角色的行动。","haste","sp",1,2,2,1),
            Skill("ward","留灯","为同伴提供护盾。","guard","sp",1,3,1,8),
            Skill("oath","三息合誓","旧旅程保留的合击。对目标及邻接敌障造成伤害。","attack","charge",100,3,2,18,0,1),
            // 司玄：先校势、再裁断；保护同伴和定点封锁是独立构筑分支。
            Talent("sixuan","jadeguard","玉律护契","为同伴提供护盾，清除定身、削弱与易伤。","guard",1,2,1,7,0,"cleanse"),
            Talent("sixuan","lawmark","勘妄印","攻击远处敌障并施加两轮易伤；易伤使承受伤害增加3。","attack",1,3,1,5,0,"vulnerable"),
            Talent("sixuan","edict","止戈令","约束目标及邻接敌障，造成伤害并削弱一轮。","attack",2,3,2,7,1,"weaken"),
            Talent("sixuan","verdict","裁云断界","对被定身或易伤的敌障追加6伤害。","attack",2,3,1,12,0,"judgement"),
            Talent("sixuan","lawshare","并契同筹","为同伴恢复AP，最多达到基础AP+1；不能让已结束的同伴再行动。","haste",1,3,2,1,step:1),
            Talent("sixuan","countermove","进退有据","攻击后为自己获得护盾，兼顾攻守。","attack",1,2,1,6,0,"selfguard",secondary:5),
            Talent("sixuan","sever","裁云一线","攻击穿过护盾，目标护盾不抵消这次伤害。","attack",2,3,2,10,0,"pierce"),
            Ultimate("sixuan","天衡裁云","目标及周围两格敌障受到伤害并定身；为全队提供护盾。","attack",3,19,2,"bindward",6),
            // 晏烛影：依靠阴影、易伤和精确针剑，不以力量击退。
            Talent("yanzhuying","moonveil","黑月纱","阴影中为同伴提供护盾，抵达下一敌方攻击前反击近身敌障。","guard",1,2,1,7,0,"retaliate","shadow",5),
            Talent("yanzhuying","nightpin","蚀月钉","远距针刺并削弱目标一轮。","attack",1,3,1,6,0,"weaken"),
            Talent("yanzhuying","pursuit","寻隙追针","对易伤或被定身的敌障追加6伤害。","attack",1,2,1,9,0,"judgement"),
            Talent("yanzhuying","needlesweep","月弧缀针","刺穿目标与邻接敌障，并施加两轮易伤。","attack",2,2,2,7,1,"vulnerable"),
            Talent("yanzhuying","veiledge","偏锋架险","近身攻击后获得护盾；阴影中额外增伤3。","attack",1,1,1,8,0,"shadowguard",secondary:6),
            Talent("yanzhuying","duskguard","留一分力","给同伴护盾并恢复SP，为下次连招蓄力。","guard",1,2,2,5,0,"restoresp",secondary:1),
            Ultimate("yanzhuying","蚀月封喉","目标及邻接敌障受到穿盾伤害并削弱两轮；阴影中追加3伤害。","attack",2,22,1,"eclipse"),
            // 凌风：步伐刀势、击退碰撞、余焰和群体鼓舞。
            Talent("lingfeng","ember","赤炎刀痕","攻击后留下余焰；敌方行动前再受到余焰伤害。","attack",1,1,0,8,0,"scorch",secondary:3),
            Talent("lingfeng","flamesweep","烈山横扫","横刀攻击目标与邻接敌障。","attack",2,1,1,11,1),
            Talent("lingfeng","battlecry","众火同心","为自己与邻接同伴增加本轮攻击伤害。","empower",1,0,2,3,1),
            Talent("lingfeng","mountainstance","撼山架","为邻近同伴提供护盾；近身受击时反击。","guard",1,1,1,8,0,"retaliate",secondary:5),
            Talent("lingfeng","execution","全力开路","目标生命不超过一半时追加8伤害。","attack",2,1,1,13,0,"execute"),
            Talent("lingfeng","advanceguard","护身进刀","攻击后为自己获得护盾。","attack",1,1,1,7,0,"selfguard",secondary:6),
            Talent("lingfeng","groundbreaker","烈山崩","强力刀势击退2格，撞击追加伤害。","attack",2,1,2,12,0,"",push:2),
            Ultimate("lingfeng","赤心燎原","目标及周围两格敌障受到重击与余焰伤害。","attack",2,25,2,"scorch",6),
            // 沧泠：借环境水雾恢复、消弱与控制，术法不使用力量击退。
            Talent("cangling","tidemend","回潮息","恢复同伴生命；施术者立于水雾中额外恢复2。","heal",1,3,1,7),
            Talent("cangling","tideward","静海护界","为同伴提供护盾并清除负面状态。","guard",1,3,1,8,0,"cleanse"),
            Talent("cangling","rainrest","凝露续息","治疗目标与邻接同伴，下一轮开始继续恢复生命。","heal",2,3,2,5,1,"regen",secondary:4),
            Talent("cangling","cleansingwave","涤潮","治疗同伴并清除定身、削弱和易伤。","heal",1,3,1,5,0,"cleanse"),
            Talent("cangling","resonance","沉珠应和","以1AP恢复同伴SP；需要水雾作为声媒。","restore",1,3,2,1,0,"","mist",resource:"ap",step:1),
            Talent("cangling","undertow","四声沉降","水雾中束住远处敌障并造成伤害，使其淬水两轮。","bind",1,3,1,7,0,"","mist"),
            Talent("cangling","deepcurrent","回水共鸣","攻击目标及邻接敌障并使其淬水，恢复自身SP。","attack",2,3,2,8,1,"restoresp",secondary:1),
            Ultimate("cangling","静海回潮","全队恢复生命，清除负面状态并获得护盾；需要水雾。","heal",0,13,99,"healward",10,"mist"),
            // 商朔：守线、界钉、护盾反击和以位置限制敌人。
            Talent("shangshuo","nail","定疆钉","掷出界钉，伤害并定身远处敌障。","bind",1,3,1,6),
            Talent("shangshuo","bulwark","负山","为同伴提供护盾，近身受击时反击。","guard",1,2,1,10,0,"retaliate",secondary:6),
            Talent("shangshuo","counterthrust","逆潮槊","攻击后获得护盾，保持阵线。","attack",1,1,1,8,0,"selfguard",secondary:7),
            Talent("shangshuo","quake","镇地槊","攻击目标与邻接敌障，各击退1格。","attack",2,1,2,8,1,"",push:1),
            Talent("shangshuo","ironmarch","稳阵行","为同伴提供护盾并清除负面状态。","guard",1,3,1,7,0,"cleanse"),
            Talent("shangshuo","earthbind","十二钉阵","攻击目标与邻接敌障，削弱一轮。","attack",2,3,2,6,1,"weaken"),
            Talent("shangshuo","lastline","守住这边","自身与邻接同伴获得护盾；下一轮恢复生命。","guard",1,0,2,7,1,"regen",secondary:3),
            Ultimate("shangshuo","镇渊玄戍","全队获得厚重护盾；近身受击时以界钉反击。","guard",0,18,99,"retaliate",8),
            // 深化 P1 新技能（区域/暗记/潮向类见设计文档第二批）。
            Talent("shangshuo","tauntward","嘲讽界","自身获得护盾，喝令2格内敌障下轮只能攻击自己。","guard",1,0,2,8,0,"taunt"),
            Talent("shangshuo","stilldepth","稳如渊","清除自身的定身、削弱、易伤、余焰与淬水，按清除的种类获得护盾。","guard",1,0,2,0,0,"depthcleanse",secondary:4),
            Talent("shangshuo","bastion","反击要塞","被动：施加守势时，按商朔当前护盾追加反击伤害；比例随等级提升。","passive",0,0,0,0),
            Talent("sixuan","verdictstrike","定谳一击","攻击远处敌障；目标每带一种定身、削弱、易伤或淬水，追加4伤害，最多追加12。","attack",2,2,1,12,0,"verdictcount"),
            Talent("sixuan","transfer","并契裁定","攻击敌障，清除全队定身、削弱与易伤；目标存活时，承接各状态的最长持续时间。","attack",1,3,2,6,0,"transfer"),
            Talent("sixuan","triplebalance","三衡并立","为同伴恢复行动点，最高达到行动上限+1；施术者获得蓄势。","haste",1,2,2,1,0,"apcharge",secondary:15),
            Talent("lingfeng","ignite","引燎","立即结算目标全部余焰并移除；淬水的敌障将蒸汽爆散。","attack",1,1,2,0,0,"ignite"),
            Talent("lingfeng","quakestomp","烈山踏","猛击目标及邻格敌障，并将命中者击退1格。","attack",2,1,2,8,1,"quakepush"),
            Talent("cangling","tideregen","潮息盾","为同伴提供护盾与续息，下轮开始恢复生命。","guard",1,3,1,8,0,"regen",secondary:4),
            Talent("cangling","ebbtide","退潮·露滩","攻击远处敌障并使其淬水；敌障存活且前方可通行时，拉近1格。","attack",2,3,2,6,0,"pull"),
            Talent("cangling","pearlconduit","沉珠引","以热网水汽为同伴恢复术力，最高达到术力上限。","restore",2,2,2,3),
            Talent("yanzhuying","collect","收账","对生命不超过四成的敌障追加8伤害；本次技能造成击杀时，返还1点行动点。","attack",2,1,1,11,0,"settle"),
            Talent("yanzhuying","shadowveil","影遁","藏入阴影：敌障不再优先瞄准自己，直到自己出手或下一轮开始。","guard",1,0,3,0,0,"stealth",condition:"shadow"),
            Ultimate("ruanzhuo","缝夜停工线","当地热网中的全队恢复生命并获得护盾。","heal",0,12,99,"healward",8)
        };
        static TacticalSkill Skill(string id,string name,string text,string kind,string resource,int cost,int range,int cooldown,int power,int push=0,int area=0)
        {return new TacticalSkill{id=id,name=name,text=text,kind=kind,resource=resource,heroId="",cost=cost,range=range,cooldown=cooldown,power=power,push=push,area=area};}
        static TacticalSkill Talent(string hero,string id,string name,string text,string kind,int cost,int range,int cooldown,int power,int area=0,string effect="",string condition="",int secondary=0,int push=0,string resource="sp",int step=2)
        {var skill=Skill(id,name,text,kind,resource,cost,range,cooldown,power,push,area);skill.heroId=hero;skill.effect=effect;skill.condition=condition;skill.secondaryPower=secondary;skill.powerStep=step;return skill;}
        static TacticalSkill Ultimate(string hero,string name,string text,string kind,int range,int power,int area,string effect,int secondary=0,string condition="")
        {return Talent(hero,UltimateId(hero),name,text,kind,100,range,2,power,area,effect,condition,secondary,resource:"charge",step:3);}
        static TacticalContent()
        {
            GetSkill("balance").heroId="sixuan";GetSkill("moon").heroId="yanzhuying";GetSkill("step").heroId="yanzhuying";
            GetSkill("cleave").heroId="lingfeng";GetSkill("tide").heroId="cangling";GetSkill("anchor").heroId="shangshuo";GetSkill("stitch").heroId="ruanzhuo";
            GetSkill("haste").powerStep=1;
        }
        public static bool CanLearn(TacticalUnit unit,TacticalSkill skill)
        {return unit!=null&&unit.team=="hero"&&skill!=null&&CanEquip(unit,skill)&&(skill.id!="oath"||unit.skills.Contains("oath"))&&(!unit.skills.Contains(skill.id)?unit.skills.Count<12:TacticalRules.SkillLevel(unit,skill.id)<3);}
        public static bool CanEquip(TacticalUnit unit,TacticalSkill skill)
        {
            if(unit==null||skill==null)return false;
            if(skill.resource=="charge")return skill.id=="oath"||unit.heroId==skill.heroId;
            if(skill.kind=="passive")return unit.heroId==skill.heroId;
            if(skill.id=="shove"||skill.id=="cleave"||skill.id=="quake"||skill.id=="groundbreaker"||skill.id=="quakestomp")return unit.heroId=="lingfeng"||unit.heroId=="shangshuo";
            return true;
        }
        // P1 affixes: deterministic enemy modifiers, shown on intents and inspection.
        public static readonly string[] AffixIds={"plated","swift","overloaded","shellhard","spreading","rusted","guarding","binding"};
        public static string AffixName(string id)
        {
            switch(id)
            {
                case "plated":return "镀膜";case "swift":return "疾走";case "overloaded":return "过载";case "shellhard":return "坚壳";
                case "spreading":return "蔓延";case "rusted":return "锈坏";case "guarding":return "庇邻";case "binding":return "缠缚";
                default:return id;
            }
        }
        public static bool HasAffix(TacticalUnit unit,string id){return unit!=null&&!string.IsNullOrEmpty(unit.affixes)&&System.Linq.Enumerable.Contains(unit.affixes.Split('+'),id);}
        public static readonly TacticalRelic[] Relics={
            // 普通：六件既有旧物与八件沿途新拾。
            Relic("common","bell","交班铃","每场战斗第一轮，每名同行者增加 1 AP。"),
            Relic("common","needle","旧钢凿","击退碰撞额外造成 3 伤害。"),
            Relic("common","flask","姜汤罐","每个玩家回合开始，存活同行者恢复 2 生命。"),
            Relic("common","lamp","守夜灯","在阴影中的同行者回合开始获得 3 护盾。"),
            Relic("common","pearl","冷凝珠","在水雾中的同行者回合开始额外恢复 1 SP。"),
            Relic("common","seal","同行信物","每场战斗开始，全队增加 15 蓄势，最高 100。"),
            Relic("common","whetstone","磨石","「破障」伤害增加 2。"),
            Relic("common","medicinebag","药囊","事件、营火与补给的治疗效果增加 6。"),
            Relic("common","whistle","哨绳","同行连携的每档加伤提高 1。"),
            Relic("common","rubberboots","胶靴","同行者免疫热管灼伤。"),
            Relic("common","shoulderline","并肩绳","相邻的同伴每轮开始互获 2 护盾。"),
            Relic("common","wrench","检修扳手","对场地物件的伤害增加 2，投掷射程与取物距离增加 1。"),
            Relic("common","worktag","旧工牌","每场战斗胜利额外获得 5 金币。"),
            Relic("common","blanket","薄毯","事件、营火与补给的治疗效果提高一半。"),
            // 稀有。
            Relic("rare","ruler","量尺","全队一次移动的步数上限增加 1。"),
            Relic("rare","emberfurnace","烬芯炉","余焰上限提高到 18，施加时向邻格敌障蔓延。"),
            Relic("rare","doublepen","双笔","技能直接施加或转移的削弱与易伤延长 1 轮，最多 3 轮。"),
            Relic("rare","brasshorn","铜号","任一同行者的技能每击杀一名敌障，全队获得 10 蓄势。"),
            Relic("rare","hotnetmap","热网图","商店价格降低 15%。"),
            Relic("rare","brokenscabbard","断鞘","「全力开路」的处决阈值提高到六成生命。"),
            Relic("rare","echoshell","回声螺","三人连携的溃散伤害提高到 12。"),
            Relic("rare","verdictpen","断案笔","全队对在案敌障的伤害增加 2。"),
            Relic("rare","immunitytalisman","免死金牌","每场战斗中首次倒下的同行者以 1 生命稳住。"),
            // 精英与顶层掉落。
            Relic("elite","sunwheelshard","曜轮残片","奥义释放后保留 30 蓄势。"),
            Relic("elite","valvecore","阀核","击退碰撞伤害增加 4，击退距离增加 1。"),
            Relic("elite","dreamspool","梦线轴","敌障死亡时，其邻格敌障获得 1 轮易伤。"),
            Relic("elite","judgeseal","判官玺","司玄奥义的冷却减少 1 轮。"),
            Relic("elite","anchorhammer","界锤","商朔拥有守势、且近身受击时持有护盾，反击额外造成 3 伤害。"),
            // 英雄本命。
            Relic("boss","jadeimprint","天衡玉印","在案敌障每带一种控制状态，全队对它的伤害增加 1，最多 3。"),
            Relic("boss","bladetsuba","赤心刀镡","蒸汽爆散的伤害提高一半。"),
            // 诅咒：只来自风险事件。
            Relic("cursed","heavywheel","沉重闸轮","全队一次移动的步数上限减少 1；获得时全队恢复 6 生命。"),
            Relic("cursed","moonledger","蚀月旧账","所有治疗效果降低三成。"),
            Relic("cursed","overheatvalve","过热阀","热管对同行者的灼伤增加 2；站在热管上的同行者获得鼓舞 3。"),
            Relic("cursed","rustwatch","锈怀表","商店价格上涨 25%；每场胜利额外获得 10 金币。")
        };
        static TacticalRelic Relic(string rarity,string id,string name,string text)
        {return new TacticalRelic{id=id,name=name,text=text,rarity=rarity};}
        public static string RelicTag(string rarity)
        {
            switch(rarity)
            {
                case "rare":return "稀有";case "elite":return "巡守掉落";case "boss":return "本命";case "cursed":return "诅咒";
                default:return "普通";
            }
        }
        public static TacticalHero GetHero(string id) {return Array.Find(Heroes,h=>h.id==id);}
        public static TacticalSkill GetSkill(string id) {return Array.Find(Skills,h=>h.id==id);}
        public static TacticalRelic GetRelic(string id) {return Array.Find(Relics,h=>h.id==id);}
    }
}
