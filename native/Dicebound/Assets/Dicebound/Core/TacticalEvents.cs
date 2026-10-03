using System;
using System.Collections.Generic;
using System.Linq;

namespace Dicebound.Tactics
{
    public sealed class TacticalEventChoice
    {
        public string id,title,description,reason;
        public bool available;
    }
    public sealed class TacticalEventView
    {
        public string id,title,body;
        public List<TacticalEventChoice> choices=new List<TacticalEventChoice>();
    }
    public sealed class TacticalOutcomeEntry
    {
        public string kind,id,unitId,title,detail;
        public int amount;
    }
    public sealed class TacticalOutcome
    {
        public string title,narrative;
        public List<TacticalOutcomeEntry> entries=new List<TacticalOutcomeEntry>();
    }

    public static partial class TacticalRules
    {
        // Presentation and execution read the same choices, prices and prerequisites.
        // This view never draws RNG or mutates a checkpoint.
        public static TacticalEventView CurrentEvent(TacticalState state)
        {
            if(state==null||state.phase!="event")return null;
            var story=Array.Find(TacticalStory.Events,e=>e.id==state.eventId);
            if(story==null)return null;
            var view=new TacticalEventView{id=story.id,title=story.title,body=story.body};
            if(state.ascentRevision<3)
            {
                AddChoice(view,state,"A","稍作休整",HealText(state,4));
                AddChoice(view,state,"B","带上旧物",RelicText(state));
                return view;
            }
            switch(state.eventId)
            {
                case "witness":
                    view.body="街边食堂的汤只剩半锅，老板正替晚归的工人留饭。\n\n‘先喝一碗也好，帮我把隔壁的米扛来，旧工具就送你们。’伙伴们看向还有余温的汤锅。";
                    AddChoice(view,state,"A","喝碗热汤",HealText(state,4));
                    AddChoice(view,state,"B","替老板搬米","每位存活同行者失去 3 生命，最低保留 1；"+RelicText(state));
                    AddChoice(view,state,"C","添一锅好汤","支付 20 金币；"+HealText(state,10)+"；全队 +10 蓄势（最高 100）。");break;
                case "dream":
                    view.body="帘下的修补铺快打烊了，工人把热饼和一件旧物放在桌上。\n\n‘都赶路呢。歇脚我请；要带走旧物，替我试一试刚补好的重帘。手法也能教，只收些灯油钱。’";
                    AddChoice(view,state,"A","分饼歇脚",HealText(state,4));
                    AddChoice(view,state,"B","合力试帘","每位存活同行者失去 3 生命，最低保留 1；"+RelicText(state));
                    AddChoice(view,state,"C","请教修补手法","支付 20 金币；一位可领悟的同行者随机学习或升级 1 项技能。",!CanStudy(state)?"队伍已无可学习或升级的技能。":null);break;
                case "water":
                    view.body="渠边医者正在整理最后几卷药布，水里还沉着工班落下的工具匣。\n\n‘包扎不用钱。下水捞匣会伤手；肯凑些药钱，我再替你们备一份护身药。’";
                    AddChoice(view,state,"A","包扎伤口",HealText(state,4));
                    AddChoice(view,state,"B","下水取匣","每位存活同行者失去 3 生命，最低保留 1；"+RelicText(state));
                    AddChoice(view,state,"C","置办药布","支付 15 金币；"+HealText(state,8)+"；下一战全队 +3 护盾。");break;
                case "vendor":
                    view.body="落闸压住了货郎的车轮，伙伴们试着抬了抬，闸轨比想象中更沉。\n\n‘搭把手，45 金币归你们。实在搬不动，凑 20 金币请工班来，我送件旧物作谢。’";
                    AddChoice(view,state,"A","合力抬车","每位存活同行者失去 4 生命，最低保留 1；获得 45 金币。");
                    AddChoice(view,state,"B","留下记号求援","指明位置，请后来的工班接手；不获得奖励，也不付出代价。");
                    AddChoice(view,state,"C","垫付修车钱","支付 20 金币；"+RelicText(state,"common"));break;
                case "potshare":
                    AddChoice(view,state,"A","添柴加汤","支付 15 金币；"+HealText(state,12));
                    AddChoice(view,state,"B","吃一份",HealText(state,8));break;
                case "dreamshop":
                    AddChoice(view,state,"A","听个故事","支付 25 金币；一位可领悟的同行者随机学习或升级 1 项技能。",EventPrerequisite(state,"A"));
                    AddChoice(view,state,"B","收下枕头","接下来 2 场战斗胜利后，全队各恢复最多 "+HealingMod(state,6)+" 生命（随治疗修正变化）。");break;
                case "pipeleak":
                    view.body="热管裂开一道口子，烬芯炉就在蒸汽旁。商队工头远远喊道：\n\n‘引走蒸汽，炉子归你们；只替我封住小裂口，也照付工钱。要压住主阀，就得多吃些苦。’";
                    AddChoice(view,state,"A","引走蒸汽","每位存活同行者失去 3 生命，最低保留 1；"+(state.relics.Contains("emberfurnace")?"已拥有烬芯炉，改为"+HealText(state,4):"获得「烬芯炉」："+TacticalContent.GetRelic("emberfurnace").text));
                    AddChoice(view,state,"B","封堵小裂口","获得 15 金币，不损失生命。");
                    AddChoice(view,state,"C","合力压住主阀","每位存活同行者失去 4 生命，最低保留 1；获得 25 金币，下一战全队 +8 护盾。");break;
                case "kite":
                    view.body="纸鸢挂在管廊高处，孩子攥着断线不肯走。附近的绳匠还有一架长梯。\n\n伙伴们商量：自己爬，得蹭破些皮；借梯要花钱，却能让大家安稳地歇一会儿。";
                    AddChoice(view,state,"A","攀上管廊取回","每位存活同行者失去 3 生命，最低保留 1；接下来 3 场战斗开局全队 +5 护盾。");
                    AddChoice(view,state,"B","托工棚代为照看","工棚主接过断线，给小队 10 金币路费。");
                    AddChoice(view,state,"C","借梯取回纸鸢","支付 15 金币；"+HealText(state,6)+"；全队 +10 蓄势（最高 100）。");break;
                case "valveevent":
                    view.body+="\n\n工头又指向拆下的旧阀：‘它还够有劲，只是脾气烫人；想带走，就得认这份险。’";
                    AddChoice(view,state,"A","冒险关闭","70%："+RelicText(state,"rare","elite","boss")+" 30%：每位存活同行者失去 8 生命，最低保留 1。");
                    AddChoice(view,state,"B","呼叫工班","获得 10 金币，没有受伤风险。");
                    AddChoice(view,state,"C","带走过热阀",FixedRelicText(state,"overheatvalve"));break;
                case "nailreturn":
                    view.body+="\n\n守闸人还愿托付一只沉重的闸轮。带着它走得慢些，但能换到沿路的药。";
                    AddChoice(view,state,"A","物归原主",RelicText(state,"rare","elite","boss"));
                    AddChoice(view,state,"B","留作纪念","获得 15 金币。");
                    AddChoice(view,state,"C","背走沉重闸轮",FixedRelicText(state,"heavywheel"));break;
                case "oldxu":
                    view.body+="\n\n老许又摸出一只锈表：‘沿路商贩认它的旧账，讨伐工班却认它的功劳。要不要带着？’";
                    AddChoice(view,state,"A","打听门道","支付 10 金币；下一战全队 +4 护盾、+10 蓄势。");
                    AddChoice(view,state,"B","点头致意","继续赶路，不获得奖励，也不付出代价。");
                    AddChoice(view,state,"C","接过锈怀表",FixedRelicText(state,"rustwatch"));break;
                case "grainhaul":
                    AddChoice(view,state,"A","亲自扛粮","凌风失去 6 生命，最低保留 1；"+RelicText(state,"common"));
                    AddChoice(view,state,"B","托人送粮","获得 20 金币。");break;
                case "moonclue":
                    AddChoice(view,state,"A","追索欠款","获得 60 金币；"+(state.relics.Contains("moonledger")?"已拥有蚀月旧账，改为"+HealText(state,4):"获得「蚀月旧账」："+TacticalContent.GetRelic("moonledger").text));
                    AddChoice(view,state,"B","暂且放下","接下来 3 场战斗，晏烛影开局 +20 蓄势（最高 100）。");break;
                case "dicestall":
                    AddChoice(view,state,"A","押一把","支付 20 金币；50% 取回 40 金币，50% 没有返还。");
                    AddChoice(view,state,"B","帮忙收摊","获得 5 金币，不参与下注。");break;
                default:
                    AddChoice(view,state,"A",ChoiceTitle(story.choiceA),ChoiceDetail(story.choiceA),EventPrerequisite(state,"A"));
                    AddChoice(view,state,"B",ChoiceTitle(story.choiceB),ChoiceDetail(story.choiceB),EventPrerequisite(state,"B"));break;
            }
            return view;
        }
        static string ChoiceTitle(string text){int split=text.IndexOf('·');return split<0?text:text.Substring(0,split).Trim();}
        static string ChoiceDetail(string text){int split=text.IndexOf('·');return split<0?text:text.Substring(split+1).Trim();}
        static string HealText(TacticalState state,int amount){return "全队恢复最多 "+SupportHeal(state,amount)+" 生命。";}
        static string FixedRelicText(TacticalState state,string id)
        {var relic=TacticalContent.GetRelic(id);return state.relics.Contains(id)?"已拥有「"+relic.name+"」，改为"+HealText(state,4):"获得「"+relic.name+"」（持续利弊）："+relic.text;}
        static string RelicText(TacticalState state,params string[] rarities)
        {
            var allowed=rarities.Length==0?new[]{"common","rare"}:rarities;
            bool any=PartyRelicCatalogue(state).Any(r=>!state.relics.Contains(r.id)&&allowed.Contains(r.rarity));
            return any?"获得 1 件"+(allowed.Length==1&&allowed[0]=="common"?"普通":!allowed.Contains("common")?"稀有以上":"")+"旧物。":"符合条件的旧物已齐，改为"+HealText(state,4);
        }
        static bool CanStudy(TacticalState state){return LearnablePartySkills(state).Any();}
        static string EventPrerequisite(TacticalState state,string choice)
        {
            if(choice!="A")return null;
            if(state.eventId=="dreamshop"&&!CanStudy(state))return "队伍已无可学习或升级的技能。";
            if(state.eventId=="archiveseal"&&!UpgradeablePartySkills(state).Any())return "对应同行者已学技能均已满级。";
            if(state.eventId=="genealogy")
            {
                var hero=FindUnit(state,"lingfeng");
                if(hero==null||hero.hp<=0)return "需要凌风同行并能行动。";
                if(!CanAcquireSkill(state,"ignite"))return "凌风的引燎已满级，或已掌握十二种技能。";
            }
            return null;
        }
        static void AddChoice(TacticalEventView view,TacticalState state,string id,string title,string description,string prerequisite=null)
        {
            int cost=state.ascentRevision>=3?EventCost(state.eventId,id):0;
            string reason=cost>state.gold?"金币不足，需要 "+cost+" 金币，当前 "+state.gold+"。":prerequisite;
            view.choices.Add(new TacticalEventChoice{id=id,title=title,description=description,available=string.IsNullOrEmpty(reason),reason=reason??"可以回应。"});
        }
        static TacticalPreview PreviewEvent(TacticalState state,TacticalRequest request,TacticalPreview preview)
        {
            var choice=CurrentEvent(state)?.choices.FirstOrDefault(c=>c.id==request.choice);
            if(choice==null)return Denied(preview,"请选择当前事件的一项回应。");
            preview.cost=state.ascentRevision>=3?EventCost(state.eventId,request.choice):0;preview.resource="gold";
            return Allowed(choice.available,preview,choice.reason);
        }
        static void EventExertion(TacticalState state,int damage)
        {foreach(var unit in state.units.Where(u=>u.team=="hero"&&u.hp>0))unit.hp=Math.Max(1,unit.hp-damage);}
        static void EventCharge(TacticalState state,int amount)
        {foreach(var unit in state.units.Where(u=>u.team=="hero"&&u.hp>0))unit.charge=Math.Min(100,unit.charge+amount);}

        // Call only after the returned state was successfully saved. The receipt is
        // derived from committed values, including capped healing and real skill levels.
        public static TacticalOutcome DescribeOutcome(TacticalState before,TacticalState after,TacticalRequest request)
        {
            if(before==null||after==null||request==null||after.revision!=before.revision+1||!new[]{"event","reward","buy","camp","rerollShop"}.Contains(request.type))return null;
            string title=request.type=="event"?(CurrentEvent(before)?.title??"沿途相逢")+" · 已结算":request.type=="reward"?"战利品已领取":request.type=="buy"?"购买完成":request.type=="camp"?"整装出发":"货架已重随";
            var result=new TacticalOutcome{title=title,narrative=request.type=="event"?after.log.LastOrDefault()??"":""};
            AddDelta(result,"gold","gold",null,"金币",before.gold,after.gold);
            foreach(string id in after.relics.Except(before.relics))
            {var relic=TacticalContent.GetRelic(id);result.entries.Add(new TacticalOutcomeEntry{kind="relic",id=id,title=relic?.name??id,detail=relic?.text??"",amount=1});}
            foreach(string id in before.relics.Except(after.relics))
            {var relic=TacticalContent.GetRelic(id);result.entries.Add(new TacticalOutcomeEntry{kind="relic",id=id,title=relic?.name??id,detail="已交出",amount=-1});}
            foreach(var unit in after.units.Where(u=>u.team=="hero"))
            {
                var old=FindUnit(before,unit.id);if(old==null)continue;
                foreach(string id in unit.skills)
                {
                    int was=old.skills.Contains(id)?SkillLevel(old,id):0,now=SkillLevel(unit,id);if(was==now)continue;
                    var skill=TacticalContent.GetSkill(id);string values=SkillDescription(after,unit,id).Split('\n')[0];int separator=values.IndexOf('·');if(separator>=0)values=values.Substring(separator+1).Trim();
                    result.entries.Add(new TacticalOutcomeEntry{kind="skill",id=id,unitId=unit.id,title=unit.name+" · "+(skill?.name??id),detail=(was==0?"新掌握 Lv."+now:"Lv."+was+" → Lv."+now)+" · "+values,amount=now-was});
                }
                AddDelta(result,"health",unit.id,unit.id,unit.name+" · 生命",old.hp,unit.hp);
                // Legacy events enter the next battle immediately. AP, SP and round-start
                // shield are not event grants; report portable resources only in ascent.
                if(before.journeyMode=="ascent")
                {
                    AddDelta(result,"shield",unit.id,unit.id,unit.name+" · 护盾",old.block,unit.block);
                    AddDelta(result,"charge",unit.id,unit.id,unit.name+" · 蓄势",old.charge,unit.charge);
                }
            }
            AddDelta(result,"blessing","pendingShield",null,"下一战 · 全队护盾",before.pendingShield,after.pendingShield);
            AddDelta(result,"blessing","pendingCharge",null,"下一战 · 全队蓄势",before.pendingCharge,after.pendingCharge);
            AddBlessing(result,"pillowBattles","旧梦余温",before.pillowBattles,after.pillowBattles,"每场胜利后全队恢复最多 "+HealingMod(after,6)+" 生命（随治疗修正变化）");
            AddBlessing(result,"zhangBattles","旧账暂歇",before.zhangBattles,after.zhangBattles,"晏烛影开局 +20 蓄势");
            AddBlessing(result,"kiteBattles","纸鸢祝福",before.kiteBattles,after.kiteBattles,"全队开局 +5 护盾");
            if(result.entries.Count==0)result.entries.Add(new TacticalOutcomeEntry{kind="notice",id="unchanged",title="本次没有数值变化",detail="生命或蓄势可能已满，或选择了直接继续赶路。"});
            return result;
        }
        static void AddDelta(TacticalOutcome outcome,string kind,string id,string unitId,string title,int before,int after)
        {
            if(before==after)return;int amount=after-before;
            outcome.entries.Add(new TacticalOutcomeEntry{kind=kind,id=id,unitId=unitId,title=title,amount=amount,detail=(amount>0?"+":"")+amount+"（"+before+" → "+after+"）"});
        }
        static void AddBlessing(TacticalOutcome outcome,string id,string title,int before,int after,string effect)
        {
            if(before==after)return;
            outcome.entries.Add(new TacticalOutcomeEntry{kind="blessing",id=id,title=title,amount=after-before,detail=effect+"，剩余 "+after+" 场。"});
        }
    }
}
