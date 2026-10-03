using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicebound.Persistence;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    // Opt-in, short presentation verification. It never enters the journey loop.
    public sealed partial class TacticalExperienceVerification
    {
        private readonly List<string> ui022Errors=new List<string>();
        private int ui022Entrypoints,ui022Resources,ui022NoticeChecks;
        private void RecordUi022Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)ui022Errors.Add(message);}
        private void WriteUi022Report(string failure)
        {
            File.WriteAllText(Path.Combine(output,"ui022-verification.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{
                mode="ui022-layout",complete=failure==null&&ui022Errors.Count==0,isolated=true,presentationFixtures=true,
                fullJourneyVerification=false,version=Application.version,checks,actions,uiBoundsChecks,
                entrypointChecks=ui022Entrypoints,resourceChecks=ui022Resources,noticeChecks=ui022NoticeChecks,fixtureRestorations,
                phase=game.State?.phase,seed=game.State?.seed,captures,width=Screen.width,height=Screen.height,
                errors=ui022Errors.ToArray(),failure},Newtonsoft.Json.Formatting.Indented));
        }
        private void Ui022Click(string name){Click(name);ui022Entrypoints++;}
        private Transform Ui022Object(string name)
        {
            var item=game.Canvas.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==name&&t.gameObject.activeInHierarchy);
            Check(item!=null,"Missing live layout object: "+name);return item;
        }
        private void Ui022Artwork(string prefix)
        {
            var graphics=game.Canvas.GetComponentsInChildren<Graphic>().Where(g=>g.gameObject.activeInHierarchy&&g.name.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            Check(graphics.Any(g=>g is RawImage raw&&raw.texture!=null||g is Image image&&image.sprite!=null),"Missing loaded artwork: "+prefix);ui022Resources++;
        }
        private void Ui022Keyword(string prefix,string keyword)
        {
            var control=game.Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith(prefix+" line ",StringComparison.Ordinal)&&b.GetComponent<Text>()?.text==keyword);
            Check(control!=null,"Missing live clickable rule keyword: "+keyword);Ui022Click(control.name);
            Check(LiveText("Rule title").text==keyword,"Rule keyword opened the wrong explanation.");
            Check(ReadableRuns("Rule explanation").Length>20,"Rule popup has no useful explanation.");FitsRuns("Rule explanation");
        }
        private Action Ui022Fixture(string name,TacticalState fixture)
        {
            Check(TacticalValidation.Valid(fixture),"Invalid isolated layout fixture: "+name);
            string folder=Path.Combine(output,"fixtures");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,name+".json"),TacticalCodec.Encode(fixture));
            return game.BeginExperiencePresentationFixture(fixture);
        }
        private TacticalState Ui022Shop(TacticalState initial)
        {
            var state=initial.Clone();var shop=state.mapNodes.First(n=>n.kind=="shop");
            var path=new List<TacticalMapNode>();var cursor=shop;
            while(cursor.floor>0){cursor=state.mapNodes.First(n=>n.floor==cursor.floor-1&&n.next.Contains(cursor.id));path.Add(cursor);}
            foreach(var ancestor in path){ancestor.visited=true;ancestor.completed=true;}
            var previous=path.First();state.currentNodeId=previous.id;state.floor=previous.floor;state.chapter=previous.chapter;state.nodeKind=previous.kind;state.sideBattle=previous.kind!="boss";
            state.gold=180;state.eventId="";state.phase="map";
            return TacticalRules.Act(state,new TacticalRequest{type="node",choice=shop.id});
        }
        private IEnumerator Ui022Notices()
        {
            string unchanged=TacticalCodec.Encode(game.State);
            Ui022Click("Move mode");var notice=LiveText("Tactical notice");var group=notice.GetComponentInParent<CanvasGroup>();
            Check(notice.text.Contains("AP")&&group.alpha>.9f&&group.blocksRaycasts,"Unavailable movement did not display clickable resource feedback.");
            Fits("Tactical notice");yield return new WaitForSecondsRealtime(3.15f);
            Check(notice.text.Length==0&&group.alpha==0&&!group.blocksRaycasts,"Notice did not disappear and release input after three seconds.");ui022NoticeChecks++;
            Ui022Click("Move mode");Check(notice.text.Length>0&&group.alpha>.9f,"Repeated unavailable movement did not show feedback.");
            Ui022Click("Notice panel");Check(notice.text.Length==0&&group.alpha==0&&!group.blocksRaycasts,"Clicking the actual notice control did not dismiss it.");ui022NoticeChecks++;
            Check(TacticalCodec.Encode(game.State)==unchanged,"A rejected action or notice dismissal changed the battle state.");
        }
        private IEnumerator RunUi022()
        {
            var args=Environment.GetCommandLineArgs();string full=Path.GetFullPath(output).TrimEnd('\\','/');
            Check(args.Contains("--dicebound-save-dir")&&Path.GetFileName(full).StartsWith("experience-player-ui022-",StringComparison.Ordinal)&&
                !string.Equals(full,Path.GetFullPath(Application.persistentDataPath).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase),"UI 0.22 verification requires its own explicit save directory.");
            Check(!args.Contains("--dicebound-tactical-verify")&&!args.Contains("--dicebound-ui-smoke"),"UI 0.22 mode cannot be combined with another player verifier.");
            Check(game.State==null&&!File.Exists(Path.Combine(output,"tactical-journey.json")),"Use a fresh directory; never replace an existing journey for layout verification.");
            Ui022Click("New tactics");Ui022Click("Trial details");
            for(int tier=2;tier<=5;tier++){Fits("Trial tier "+tier);Fits("Trial effect "+tier);}
            Check(game.Canvas.GetComponentsInChildren<Text>().Count(t=>t.gameObject.activeInHierarchy&&t.name.StartsWith("Trial tier "))==4,"Difficulty page must show the four available tiers.");
            yield return Capture("01-trial-details");Ui022Click("Close modal corner");Ui022Click("Start tactics");yield return null;
            Check(game.State!=null&&game.State.phase=="map"&&game.State.ascentRevision==4&&game.State.trial==2,"Actual start control did not create a current default journey.");
            var original=game.State;string checkpoint=TacticalCodec.Encode(original),saved=File.ReadAllText(Path.Combine(output,"tactical-journey.json"));
            var first=TacticalRules.AvailableNodes(original).First(n=>n.kind=="battle");
            Ui022Click("Map node "+first.id);Check(Control("Enter map node").interactable,"Inspected start node is not available.");
            Fits("Destination category");Fits("Destination description");Ui022Artwork("Destination scene painting");yield return Capture("02-route");

            var battle=TacticalRules.Act(original,new TacticalRequest{type="node",choice=first.id});var hero=battle.units.First(u=>u.team=="hero");
            hero.block=7;hero.damageBonus=2;hero.weakened=1;hero.ap=0;
            var extra=TacticalRules.AvailableSkillCatalogue(battle).First(s=>s.heroId==hero.heroId&&s.resource!="charge"&&s.kind!="passive"&&!hero.skills.Contains(s.id));
            hero.skills.Add(extra.id);hero.skillLevels[extra.id]=1;
            var restore=Ui022Fixture("battle-selected",battle);
            try
            {
                Ui022Click("Select "+hero.id);
                yield return Ui022Notices();
                string selectedSkill=hero.skills.Take(5).First(id=>TacticalContent.GetSkill(id).resource=="sp"&&TacticalRules.SkillAvailability(game.State,hero.id,id).ok);
                Ui022Click("Skill "+selectedSkill);game.Notify("");
                var selectedButton=Control("Skill "+selectedSkill);
                Check(selectedButton.transform.Find("Selected skill soft glow 1")&&selectedButton.transform.Find("Selected skill badge"),"Selected skill lacks its soft highlight and checkmark.");
                Check(game.Canvas.GetComponentsInChildren<Button>().Count(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("Skill "))==5,"Battle page must show five skills plus movement.");
                Check(LiveText("Move cost").color.r>LiveText("Move cost").color.g&&LiveText("Move cost").text.Contains("不足"),"AP shortage is not red on the movement card.");
                Ui022Object("Health "+hero.id+" shield");Check(LiveText("Ally shield "+hero.id).text.Contains("7"),"HUD omits the real shield amount.");
                foreach(string id in hero.skills.Take(5)){Fits("Skill title "+id);Fits("Skill purpose "+id);Fits("Skill cost "+id);Ui022Artwork("Skill illustration "+id);}
                yield return Capture("03-battle-selected");
                Ui022Click("Ally status "+hero.id);Check(LiveText("Status health").text.Contains("护盾 7"),"Status entry did not open this actor's current shield.");
                Check(ReadableRuns("Current status effects").Contains("削弱"),"Status detail omitted the active condition.");FitsRuns("Current status effects");yield return Capture("04-current-status");Ui022Click("Close modal corner");
                Ui022Click("Next skill page");Check(game.Canvas.GetComponentsInChildren<Button>().Count(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("Skill "))==1,"Six learned skills must leave one card on the second page.");
                Fits("Battle navigation");yield return Capture("08-battle-last-page");
            }
            finally{restore();}
            FixtureRestored(original,checkpoint,saved);

            var reward=battle.Clone();reward.phase="reward";foreach(var enemy in reward.units.Where(u=>u.team=="enemy")){enemy.hp=0;enemy.ap=0;enemy.block=0;}
            var definition=TacticalContent.GetSkill("tide");var relic=TacticalContent.GetRelic("echoshell");
            reward.rewards=new List<TacticalReward>{
                new TacticalReward{id="ui022-skill",kind="skill",name=definition.name,text=definition.text,skillId=definition.id,relicId=""},
                new TacticalReward{id="ui022-relic",kind="relic",name=relic.name,text=relic.text,relicId=relic.id,skillId=""},
                new TacticalReward{id="ui022-gold",kind="gold",name="金币行囊",text="直接获得 40 金币。",amount=40,skillId="",relicId=""}};
            restore=Ui022Fixture("reward",reward);
            try
            {
                Ui022Click("Reward choice ui022-skill");Check(Control("Claim reward").interactable,"Skill reward cannot be selected through its real control.");
                Check(LiveText("Reward recipient heading").text.Contains("沧泠"),"Skill reward omits its automatic owner.");
                for(int i=0;i<3;i++){Fits("Reward name "+i);Fits("Reward category "+i);Check(ReadableRuns("Reward effect "+i).Length>0,"Reward card lacks its effect.");FitsRuns("Reward effect "+i);}
                Ui022Artwork("Reward skill seal 0");Ui022Artwork("Reward illustration 1");Ui022Artwork("Reward illustration 2");
                yield return Capture("05-reward");Ui022Keyword("Reward effect 1","溃散");yield return Capture("06-rule-popup");Ui022Click("Close rule");
            }
            finally{restore();}
            FixtureRestored(original,checkpoint,saved);

            restore=Ui022Fixture("shop",Ui022Shop(original));
            try
            {
                var offers=game.State.shopOffers.Where(o=>o.kind=="skill").ToArray();Check(offers.Length==3,"Shop must show three skill offers.");
                foreach(var offer in offers){Check(game.State.squad.Contains(offer.heroId),"Shop skill belongs to an absent hero.");Control("Shop buy "+offer.id);Fits("Shop name "+offer.id);Fits("Shop source "+offer.id);Check(ReadableRuns("Shop effect "+offer.id).Length>0,"Shop card lacks its effect.");FitsRuns("Shop effect "+offer.id);Ui022Artwork("Shop art "+offer.id);}
                Check(Control("Reroll shop").interactable,"Funded shop fixture cannot reroll.");yield return Capture("07-shop");
            }
            finally{restore();}
            FixtureRestored(original,checkpoint,saved);
            Check(fixtureRestorations==3&&game.State.phase=="map","Layout fixtures did not restore the saved route.");
            Check(captures.Count==8&&actions==0,"UI layout mode accidentally entered the full action loop.");
        }
    }
}
