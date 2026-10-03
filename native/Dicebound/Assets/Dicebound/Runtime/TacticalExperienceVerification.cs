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
    /// <summary>Focused live Player checks for the tactical construction and inspection controls.</summary>
    public sealed partial class TacticalExperienceVerification : MonoBehaviour
    {
        private TacticalDirector game;
        private string output;
        private int checks,actions,uiBoundsChecks,receiptChecks,inspectorSelections,loadedRelicIcons,helpPages,trialGuidePages,pathChecks,smallSkillPageChecks,fixtureRestorations;
        private bool eventChecked,rewardReceiptChecked,steamValveChecked,goldRewardChecked,wrenchSelectionChecked;
        private readonly List<string> captures=new List<string>();
        private readonly HashSet<string> visited=new HashSet<string>();
        private IEnumerator Start()
        {
            yield return null;game=TacticalDirector.Instance;output=game.SaveDirectory;
            bool ui022=Environment.GetCommandLineArgs().Contains("--dicebound-ui022-verify");
            if(ui022)Application.logMessageReceived+=RecordUi022Log;
            var work=new Stack<IEnumerator>();work.Push(ui022?RunUi022():Run());string failure=null;
            while(work.Count>0)
            {
                bool moved=false;object current=null;
                try{moved=work.Peek().MoveNext();if(moved)current=work.Peek().Current;}
                catch(Exception e){failure=e.ToString();Debug.LogException(e);break;}
                if(!moved){(work.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested)work.Push(nested);else yield return current;
            }
            while(work.Count>0)(work.Pop() as IDisposable)?.Dispose();
            if(ui022){Application.logMessageReceived-=RecordUi022Log;WriteUi022Report(failure);}
            else File.WriteAllText(Path.Combine(output,"experience-verification.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{complete=failure==null,checks,actions,uiBoundsChecks,receiptChecks,inspectorSelections,loadedRelicIcons,helpPages,trialGuidePages,pathChecks,eventChecked,rewardReceiptChecked,steamValveChecked,goldRewardChecked,wrenchSelectionChecked,smallSkillPageChecks,fixtureRestorations,phase=game.State?.phase,visited=visited.ToArray(),captures,width=Screen.width,height=Screen.height,failure},Newtonsoft.Json.Formatting.Indented));
            Application.Quit(failure==null&&(!ui022||ui022Errors.Count==0)?0:1);
        }
        private void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private Button Control(string name)
        {
            var button=game.Canvas.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==name&&b.gameObject.activeInHierarchy);
            Check(button!=null,"Missing live control: "+name);return button;
        }
        private void Click(string name){var button=Control(name);Check(button.interactable,"Live control disabled: "+name);button.onClick.Invoke();}
        private Text LiveText(string name)
        {var text=game.Canvas.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name==name&&t.gameObject.activeInHierarchy);Check(text!=null,"Missing live text: "+name);return text;}
        private void Fits(string name)
        {Canvas.ForceUpdateCanvases();var text=LiveText(name);Check(!string.IsNullOrWhiteSpace(text.text),"Empty important text: "+name);Check(text.preferredHeight<=text.rectTransform.rect.height+2,"Text clipped: "+name+" needs "+text.preferredHeight+" but has "+text.rectTransform.rect.height);uiBoundsChecks++;}
        private Text[] RuleRuns(string name)
        {
            var runs=game.Canvas.GetComponentsInChildren<Text>().Where(t=>t.isActiveAndEnabled&&t.name.StartsWith(name+" line ",StringComparison.Ordinal)).ToArray();
            Check(runs.Length>0,"Missing visible rule text: "+name);return runs;
        }
        private string ReadableRuns(string name)=>string.Concat(RuleRuns(name).Select(t=>t.text));
        private static string CompactReading(string value)=>string.Concat(value.Where(c=>!char.IsWhiteSpace(c)));
        private void FitsRuns(string name)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var run in RuleRuns(name))
            {
                var rect=run.rectTransform;var content=(RectTransform)rect.parent;
                Check(!string.IsNullOrEmpty(run.text)&&run.preferredHeight<=rect.rect.height+2&&run.preferredWidth<=rect.rect.width+3,"Rule text run clipped: "+run.name);
                Check(rect.anchoredPosition.x>=-2&&rect.anchoredPosition.x+rect.rect.width<=content.rect.width+3&&-rect.anchoredPosition.y>=-2&&-rect.anchoredPosition.y+rect.rect.height<=content.rect.height+2,"Rule text escapes its scroll content: "+run.name);
            }
            uiBoundsChecks++;
        }
        private void VerifyPartySelection()
        {
            var unit=game.State.units.First(u=>u.team=="hero");Click("Inspect companion "+unit.id);
            string skill=unit.skills.FirstOrDefault(id=>TacticalContent.GetSkill(id).heroId==unit.heroId)??unit.skills[0];Click("Inspect skill "+skill);
            Check(LiveText("Selected skill name").text==TacticalContent.GetSkill(skill).name,"Selected skill detail did not change.");
            Check(LiveText("Selected skill level").text.Contains("Lv."+TacticalRules.SkillLevel(unit,skill)),"Selected skill level differs from checkpoint.");
            Fits("Selected skill name");Fits("Selected skill level");Fits("Selected skill value");FitsRuns("Inspected skill effect "+skill);Fits("Selected skill upgrade");inspectorSelections++;
            var unknown=TacticalRules.AvailableSkillCatalogue(game.State).FirstOrDefault(s=>s.heroId==unit.heroId&&!unit.skills.Contains(s.id));
            if(unknown!=null){Click("Inspect skill "+unknown.id);Check(LiveText("Selected skill level").text.Contains("未领悟"),"Unknown skill preview claims ownership.");Check(ReadableRuns("Inspected skill effect "+unknown.id).Contains("尚不能释放"),"Unknown skill preview hides unavailable state.");FitsRuns("Inspected skill effect "+unknown.id);inspectorSelections++;Click("Inspect skill "+skill);}
        }
        private void VerifyInventorySelection()
        {
            if(game.State.relics.Count==0){Fits("Empty inventory");return;}
            foreach(var id in game.State.relics){Click("Inspect relic "+id);Check(CompactReading(ReadableRuns("Selected relic effect"))==CompactReading(TacticalContent.GetRelic(id).text),"Relic detail is not the actual held item's effect.");Fits("Selected relic name");FitsRuns("Selected relic trigger");FitsRuns("Selected relic effect");inspectorSelections++;}
        }
        private void VerifyReceipt(TacticalState before,TacticalRequest request)
        {
            var outcome=TacticalRules.DescribeOutcome(before,game.State,request);Check(outcome!=null,"Missing committed outcome: "+request.type);
            var notice=LiveText("Tactical notice");Check(notice.GetComponentInParent<CanvasGroup>().alpha>.9f,"Receipt is not visible.");
            Check(notice.text.Contains(outcome.title),"Receipt omits settlement title.");
            foreach(var entry in outcome.entries){Check(notice.text.Contains(entry.title),"Receipt omits item/recipient: "+entry.title);Check(notice.text.Contains(entry.detail),"Receipt omits precise effect: "+entry.detail);}
            int delta=game.State.gold-before.gold;if(delta!=0)Check(notice.text.Contains((delta>0?"+":"")+delta+"（"+before.gold+" → "+game.State.gold+"）"),"Receipt omits actual gold delta.");
            foreach(string id in game.State.relics.Except(before.relics))Check(notice.text.Contains(TacticalContent.GetRelic(id).name),"Receipt omits acquired relic.");
            Fits("Tactical notice");receiptChecks++;
        }
        private IEnumerator VerifyGuide()
        {
            game.Notify("");game.Help();
            do{
                yield return null;var numbers=game.Canvas.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("Reading number ")).ToArray();
                Check(numbers.Length==2,"Guide page does not use two independent number labels.");
                for(int i=0;i<numbers.Length;i++){Check(int.TryParse(numbers[i].text,out _),"Guide number not isolated from body.");Fits("Reading text "+i);Check(!char.IsDigit(LiveText("Reading text "+i).text[0]),"Guide body still includes an inline number.");}
                helpPages++;yield return Capture("00-guide-"+helpPages);if(!Control("Next reading page").interactable)break;Click("Next reading page");
            }while(helpPages<12);
            Check(helpPages==3,"Guide did not expose all six entries.");Click("Close modal corner");
        }
        private IEnumerator VerifyMovePreview(TacticalState state)
        {
            var unit=state.units.First(u=>u.team=="hero"&&u.hp>0);game.Select(unit.id);Click("Move mode");
            Check(Control("Move mode").GetComponentsInChildren<Text>().Any(t=>t.name=="Selected skill badge"),"Move selection lacks visible marker.");
            var options=TacticalRules.MoveCells(state,unit.id).Select(c=>new TacticalRequest{type="move",unitId=unit.id,x=c.x,y=c.y}).Select(r=>new{request=r,preview=TacticalRules.Preview(state,r)}).Where(p=>p.preview.ok&&p.preview.path.Count>=2).OrderByDescending(p=>p.preview.path.Count).ToArray();
            Check(options.Length>0,"No multi-cell legal path for visual verification.");var plan=options[0];game.CellPress(plan.request.x,plan.request.y);
            var shown=game.Stage.RangeSnapshot;Check(shown.locked&&shown.semantic==TacticalRangeSemantic.Move,"Move path is not locked.");
            Check(shown.path.Select(c=>c.x+","+c.y).SequenceEqual(plan.preview.path.Select(c=>c.x+","+c.y)),"Rendered path differs from legal rule path.");
            var meshes=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);var path=meshes.FirstOrDefault(m=>m.name=="Rule path");var legal=meshes.FirstOrDefault(m=>m.name=="Legal cells");
            Check(path&&legal&&path.enabled&&path.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"Locked path has no visible geometry.");
            var pathColor=path.sharedMaterial.GetColor("_Color");var areaColor=legal.sharedMaterial.GetColor("_Color");Check(pathColor.r>pathColor.b+.3f&&areaColor.b>areaColor.r+.3f,"Path and legal area colors are not distinguishable.");
            var actionPreview=LiveText("Action preview");var tilePreview=LiveText("Tile preview");
            Check(actionPreview.transform.parent!=tilePreview.transform.parent,"Action and tile information share a flat panel.");
            Check(actionPreview.text.Contains("路径")&&!tilePreview.text.Contains("路径"),"Path explanation leaks into tile information.");
            Check(!actionPreview.GetComponentInParent<ScrollRect>()&&!tilePreview.GetComponentInParent<ScrollRect>(),"Live hover summaries require an unreachable scroll view.");
            Check(!actionPreview.raycastTarget&&!tilePreview.raycastTarget,"Live hover summaries require interactive text.");
            Fits("Action preview");Fits("Tile preview");pathChecks++;
            yield return Capture("01-selected-move-path");Click("Cancel action");
            var skill=unit.skills.Select(TacticalContent.GetSkill).First(s=>s.resource!="charge"&&s.kind!="passive");Click("Skill "+skill.id);
            Check(Control("Skill "+skill.id).GetComponentsInChildren<Text>().Any(t=>t.name=="Selected skill badge"),"Selected skill has no visible marker.");
            yield return Capture("01-selected-skill");Click("Move mode");
        }
        private IEnumerator SavedAction(TacticalRequest request)
        {
            int revision=game.State.revision;game.Request(request);actions++;
            Check(game.State.revision==revision+1,"Action not committed once: "+request.type);
            while(game.Busy)yield return null;
            Check(TacticalCodec.TryDecode(File.ReadAllText(Path.Combine(output,"tactical-journey.json")),out var saved,out var error),"Saved archive invalid: "+error);
            Check(TacticalCodec.Encode(saved)==TacticalCodec.Encode(game.State),"Live result differs from checkpoint.");
        }
        private void FixtureRestored(TacticalState original,string checkpoint,string savedBytes)
        {
            Check(ReferenceEquals(game.State,original),"Presentation fixture did not restore the original State object.");
            Check(TacticalCodec.Encode(game.State)==checkpoint,"Presentation fixture changed the journey state.");
            Check(File.ReadAllText(Path.Combine(output,"tactical-journey.json"))==savedBytes,"Presentation fixture wrote to the journey archive.");fixtureRestorations++;
        }
        private IEnumerator VerifyPresentationFixtures(TacticalState original)
        {
            string checkpoint=TacticalCodec.Encode(original),savedBytes=File.ReadAllText(Path.Combine(output,"tactical-journey.json"));
            var gold=original.Clone();gold.phase="reward";
            foreach(var enemy in gold.units.Where(u=>u.team=="enemy")){enemy.hp=0;enemy.ap=0;enemy.block=0;}
            gold.rewards=new List<TacticalReward>{
                new TacticalReward{id="qa-goldbag",kind="gold",name="金币行囊",text="直接获得 35 金币，自由支配。",amount=35,skillId="",relicId=""},
                new TacticalReward{id="qa-skill",kind="skill",name="衡域",text=TacticalContent.GetSkill("balance").text,skillId="balance",relicId=""},
                new TacticalReward{id="qa-supply",kind="supply",name="同行补给",text="恢复同行者生命。",skillId="",relicId=""}};
            var restore=game.BeginExperiencePresentationFixture(gold);
            try
            {
                Click("Reward choice qa-goldbag");var artwork=game.Canvas.GetComponentsInChildren<RawImage>().FirstOrDefault(i=>i.name=="Reward illustration 0"&&i.gameObject.activeInHierarchy);
                Check(artwork&&artwork.texture&&artwork.uvRect.width>0&&artwork.uvRect.height>0,"Gold reward has no loaded treasure artwork.");
                Check(LiveText("Reward name 0").text=="金币行囊"&&LiveText("Reward category 0").text=="同行补给"&&ReadableRuns("Reward effect 0").Contains("35"),"Gold reward does not show its name, category and amount.");
                Check(Control("Claim reward").interactable,"Gold reward cannot be selected for claiming.");Fits("Reward name 0");Fits("Reward category 0");FitsRuns("Reward effect 0");
                yield return Capture("00-fixture-gold-reward");goldRewardChecked=true;
            }
            finally{restore();}
            FixtureRestored(original,checkpoint,savedBytes);

            var throwing=original.Clone();throwing.objects.Clear();if(!throwing.relics.Contains("wrench"))throwing.relics.Add("wrench");
            var caster=throwing.units.First(u=>u.team=="hero"&&u.hp>0);var victim=throwing.units.First(u=>u.team=="enemy"&&u.hp>0);bool placed=false;
            foreach(var cell in throwing.terrain.Where(c=>TacticalRules.Walkable(throwing,c.x,c.y)&&Math.Abs(c.x-victim.x)+Math.Abs(c.y-victim.y)==2))
            {
                if(throwing.units.Any(u=>u.hp>0&&u.id!=caster.id&&u.x==cell.x&&u.y==cell.y))continue;
                var sources=throwing.terrain.Where(c=>TacticalRules.Walkable(throwing,c.x,c.y)&&Math.Abs(c.x-cell.x)+Math.Abs(c.y-cell.y)==2&&!throwing.units.Any(u=>u.hp>0&&u.id!=caster.id&&u.x==c.x&&u.y==c.y)).Take(2).ToArray();
                if(sources.Length<2)continue;caster.x=cell.x;caster.y=cell.y;throwing.objects.Clear();
                for(int i=0;i<sources.Length;i++)throwing.objects.Add(new TacticalObject{id="object-"+throwing.chapter+"-"+i,kind=i==0?"crate":"canister",hp=8,x=sources[i].x,y=sources[i].y});
                if(!TacticalRules.Preview(throwing,new TacticalRequest{type="skill",unitId=caster.id,skillId="throw",choice=throwing.objects[1].id,x=victim.x,y=victim.y}).ok)continue;placed=true;break;
            }
            Check(placed,"Could not place a valid two-cell pickup fixture.");
            var withoutWrench=throwing.Clone();withoutWrench.relics.Remove("wrench");restore=game.BeginExperiencePresentationFixture(withoutWrench);
            try
            {
                game.Select(caster.id);Click("Skill throw");
                Check(!game.Canvas.GetComponentsInChildren<Button>().Any(b=>b.name=="Throw source"&&b.gameObject.activeInHierarchy),"Two-cell props are available without the wrench relic.");
                Check(LiveText("Battle navigation").text.Contains("没有可投掷物件"),"Missing pickup-range feedback without wrench.");
            }
            finally{restore();}
            FixtureRestored(original,checkpoint,savedBytes);restore=game.BeginExperiencePresentationFixture(throwing);
            try
            {
                game.Select(caster.id);Click("Skill throw");var first=throwing.objects.OrderBy(o=>o.id).First();var second=throwing.objects.OrderBy(o=>o.id).Last();
                string Cell(TacticalObject o)=>((char)('A'+o.x)).ToString()+(o.y+1);
                Check(Control("Throw source").GetComponentInChildren<Text>().text.Contains(Cell(first)),"Wrench did not expose the first two-cell prop.");
                Click("Throw source");Check(Control("Throw source").GetComponentInChildren<Text>().text.Contains(Cell(second)),"Throw source did not cycle to the second two-cell prop.");
                game.CellPress(victim.x,victim.y);var target=game.PendingTarget;
                Check(target!=null&&target.choice==second.id,"Selected prop was not passed into the locked action.");
                var preview=TacticalRules.Preview(game.State,target);Check(preview.ok&&preview.objectId==second.id,"Wrench UI selection disagrees with throw rules.");
                yield return Capture("00-fixture-wrench-throw-source");wrenchSelectionChecked=true;
            }
            finally{restore();}
            FixtureRestored(original,checkpoint,savedBytes);

            foreach(int count in new[]{6,7})
            {
                var paged=original.Clone();var hero=paged.units.First(u=>u.team=="hero"&&u.hp>0);
                foreach(var definition in TacticalRules.AvailableSkillCatalogue(paged).Where(s=>TacticalContent.CanEquip(hero,s)&&s.resource!="charge"&&s.kind!="passive"&&!hero.skills.Contains(s.id)).ToArray())
                {if(hero.skills.Count>=count)break;hero.skills.Add(definition.id);hero.skillLevels[definition.id]=1;}
                Check(hero.skills.Count==count,"Unable to create requested skill-page fixture.");restore=game.BeginExperiencePresentationFixture(paged);
                try
                {
                    game.Select(hero.id);Click("Next skill page");
                    int visible=game.Canvas.GetComponentsInChildren<Button>().Count(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("Skill "));
                    Check(visible==count-5,"Final skill page does not contain the expected 1–2 skill cards.");Fits("Battle navigation");
                    Check(LiveText("Battle navigation").text.Contains("2 / 2"),"Final-page navigation omits its page count.");
                    yield return Capture("00-fixture-skill-last-page-"+(count-5));smallSkillPageChecks++;
                }
                finally{restore();}
                FixtureRestored(original,checkpoint,savedBytes);
            }
        }
        private IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();
            string path=Path.Combine(output,name+".png");ScreenCapture.CaptureScreenshot(path);
            for(int i=0;i<60&&!File.Exists(path);i++)yield return null;
            Check(File.Exists(path),"Screenshot missing: "+name);captures.Add(name+".png");
        }
        private IEnumerator Run()
        {
            game.SelectParty();Click("Trial details");
            for(int tier=2;tier<=5;tier++){Fits("Trial tier "+tier);Fits("Trial effect "+tier);}
            Check(game.Canvas.GetComponentsInChildren<Text>().Count(t=>t.isActiveAndEnabled&&t.name.StartsWith("Trial tier "))==4,"Trial guide did not expose all four current tiers.");
            trialGuidePages=1;yield return Capture("00-trial-details");Click("Close modal corner");
            game.StartRun(173,new[]{"sixuan","cangling","shangshuo"});yield return null;
            Check(game.State.phase=="map","New ascent did not open map.");
            yield return Capture("00-route");Click("Map party details");VerifyPartySelection();yield return Capture("00-route-party");Click("Close modal corner");
            Click("Map inventory");VerifyInventorySelection();yield return Capture("00-route-inventory");
            var iconParent=game.Canvas.GetComponentsInChildren<Transform>().First(t=>t.name=="Modal paper"&&t.gameObject.activeInHierarchy);
            foreach(var relic in TacticalContent.Relics)
            {
                var icon=TacticalArt.Symbol("QA loaded relic "+relic.id,iconParent,"relic-"+relic.id,new Vector2((loadedRelicIcons%10-4.5f)*62,80-(loadedRelicIcons/10)*62),Vector2.one*52);
                Check(icon.sprite&&icon.sprite.texture&&icon.gameObject.activeInHierarchy,"Relic artwork failed to load: "+relic.id);loadedRelicIcons++;
            }
            yield return null;Check(loadedRelicIcons==TacticalContent.Relics.Length,"Some relic art was skipped.");Click("Close modal corner");
            yield return VerifyGuide();
            bool battleChecked=false,rewardChecked=false,shopChecked=false,bossCaptured=false,eliteCaptured=false,levelCaptured=false;
            for(int step=0;step<2200;step++)
            {
                var state=game.State;
                if(state.phase=="victory")
                {
                    game.Notify("");yield return Capture("12-victory");Check(battleChecked&&rewardChecked&&shopChecked&&bossCaptured&&eliteCaptured,"Focused route missed required tactical pages.");
                    Check(eventChecked&&rewardReceiptChecked&&pathChecks>0&&helpPages==3&&inspectorSelections>0,"Focused route missed requested feedback/inspection coverage.");
                    Check(goldRewardChecked&&wrenchSelectionChecked&&smallSkillPageChecks==2&&fixtureRestorations==5&&trialGuidePages==1,"Focused presentation fixtures were not all verified and restored.");yield break;
                }
                Check(state.phase!="defeat","Construction route defeated; inspect combat log.");
                if(state.phase=="map")
                {
                    var available=TacticalRules.AvailableNodes(state).ToArray();
                    if(state.floor<7){var connected=available.Where(n=>CanReach(state,n,"floor-7-1")).ToArray();if(connected.Length>0)available=connected;}
                    var node=available.OrderBy(n=>n.kind=="shop"?0:n.kind=="event"?1:n.kind=="elite"?2:3).First();
                    visited.Add(node.kind);yield return SavedAction(new TacticalRequest{type="node",choice=node.id});continue;
                }
                if(state.phase=="battle")
                {
                    if(!battleChecked)
                    {
                        yield return VerifyPresentationFixtures(state);
                        yield return Capture("01-battle-terrain");
                        yield return VerifyMovePreview(state);
                        var valve=state.objects.FirstOrDefault(o=>o.kind=="valve");if(valve!=null){var valveArt=game.Stage.Environment.ObjectVisual(valve.id);Check(valveArt&&valveArt.name=="青铜蒸汽阀 · "+valve.id&&valveArt.GetComponentsInChildren<MeshRenderer>().Length>1,"Steam valve has no independent model.");steamValveChecked=true;yield return Capture("01-steam-valve");}
                        Click("Battle party details");VerifyPartySelection();yield return Capture("02-party-details");Click("Close modal corner");
                        Click("Battle inventory");VerifyInventorySelection();yield return Capture("03-inventory");Click("Close modal corner");
                        var enemy=state.units.First(u=>u.team=="enemy"&&u.hp>0);game.ChooseSkill(null);game.CellPress(enemy.x,enemy.y);
                        Check(game.Modal,"Enemy click did not open details.");Fits("Enemy next action");Fits("Enemy predicted damage");FitsRuns("Enemy abilities");FitsRuns("Enemy terrain");
                        Check(LiveText("Enemy skill damage").text.Contains(TacticalRules.EnemyAttackPower(state,enemy).ToString()),"Enemy skill does not show actual attack damage.");yield return Capture("04-enemy-details");Click("Close modal corner");
                        var tile=state.terrain.First(c=>c.kind=="mist"&&!state.units.Any(u=>u.hp>0&&u.x==c.x&&u.y==c.y));game.CellPress(tile.x,tile.y);
                        Click("Inspect target");yield return Capture("05-terrain-details");Click("Close modal corner");
                        var first=state.units.First(u=>u.team=="hero");game.Select(first.id);int revision=state.revision;
                        var ultimate=first.skills.First(s=>TacticalContent.GetSkill(s).resource=="charge");game.ChooseSkill(ultimate);
                        Check(game.State.revision==revision,"Denied skill modified checkpoint.");
                        var notice=game.Canvas.GetComponentsInChildren<Text>().First(t=>t.name=="Tactical notice");Check(notice.text.Contains("还差"),"Insufficient charge gives no precise feedback.");yield return Capture("06-insufficient-resource");
                        game.ChooseSkill(null);int round=state.round,enemyPresentations=game.Stage.EnemyTurnPresentations;
                        Click("End round");while(game.Busy)yield return null;
                        Check(game.State.round==round&&game.State.units.First(u=>u.id==first.id).turnEnded,"First end action advanced enemy round.");
                        Check(game.Stage.EnemyTurnPresentations==enemyPresentations,"First end action played uncommitted enemy moves.");
                        Click("End round");while(game.Busy)yield return null;Check(game.State.round==round,"Second end action advanced enemy round.");Check(game.Stage.EnemyTurnPresentations==enemyPresentations,"Second end action played uncommitted enemy moves.");
                        Click("End round");while(game.Busy)yield return null;Check(game.State.round==round+1,"Last end action did not advance enemy round.");Check(game.Stage.EnemyTurnPresentations==enemyPresentations+1,"Last end action did not present committed enemy turn.");
                        Check(game.State.units.Where(u=>u.team=="hero"&&u.hp>0).All(u=>!u.turnEnded),"New round did not refresh individual turns.");
                        yield return Capture("07-next-round");battleChecked=true;
                    }
                    if(state.nodeKind=="elite"&&!eliteCaptured){yield return Capture("08-elite");eliteCaptured=true;}
                    if(state.nodeKind=="boss"&&!bossCaptured){yield return Capture("11-boss");bossCaptured=true;}
                }
                if(state.phase=="reward"&&!rewardChecked)
                {
                    game.Notify("");yield return Capture("09-reward");Click("Reward party details");VerifyPartySelection();yield return Capture("09-reward-party");Click("Close modal corner");
                    Click("Reward inventory");VerifyInventorySelection();yield return Capture("09-reward-inventory");Click("Close modal corner");rewardChecked=true;
                }
                if(state.phase=="event")
                {
                    game.Notify("");var encounter=TacticalRules.CurrentEvent(state);Check(encounter!=null&&encounter.choices.Count>=2,"Event has no meaningful choice view.");
                    foreach(var choice in encounter.choices){Check(Control("Event "+choice.id).interactable==choice.available,"Event availability differs from rules.");Check(CompactReading(ReadableRuns("Event consequence "+choice.id)).Contains(CompactReading(choice.description)),"Event card omits consequences.");FitsRuns("Event consequence "+choice.id);}
                    if(!eventChecked)yield return Capture("09-event-choices");
                }
                if(state.phase=="shop")
                {
                    if(!shopChecked)
                    {
                        Check(state.shopOffers.Count(o=>o.kind=="skill")==3,"Shop did not show three skills.");
                        Check(state.shopOffers.Where(o=>o.kind=="skill").Select(o=>TacticalContent.GetSkill(o.skillId).heroId).Distinct().Count()==3,"Shop skill origins are not distinct.");
                        yield return Capture("10-shop");int gold=state.gold,cost=TacticalRules.ShopRerollCost(state);
                        Click("Reroll shop");yield return null;Check(game.State.gold==gold-cost&&game.State.shopRerolls==1,"Reroll cost/state not committed.");VerifyReceipt(state,new TacticalRequest{type="rerollShop"});
                        yield return Capture("10-shop-rerolled");game.Notify("");shopChecked=true;
                        state=game.State;
                    }
                    if(!levelCaptured)
                    {
                        var hero=state.units.First(u=>u.team=="hero");var offer=state.shopOffers.FirstOrDefault(o=>o.kind=="skill"&&!o.sold&&hero.skills.Contains(o.skillId)&&TacticalRules.Preview(state,new TacticalRequest{type="buy",choice=o.id,unitId=hero.id}).ok);
                        if(offer!=null)
                        {
                            int level=TacticalRules.SkillLevel(hero,offer.skillId);yield return SavedAction(new TacticalRequest{type="buy",choice=offer.id,unitId=hero.id});
                            Check(TacticalRules.SkillLevel(TacticalRules.FindUnit(game.State,hero.id),offer.skillId)==level+1,"Purchase did not upgrade actual skill level.");VerifyReceipt(state,new TacticalRequest{type="buy",choice=offer.id,unitId=hero.id});game.Notify("");game.InspectUnit(hero.id);Click("Inspect skill "+offer.skillId);Check(LiveText("Selected skill level").text.Contains("Lv."+(level+1)),"Upgraded detail still shows old level.");yield return Capture("10-upgraded-skill");game.CloseOverlay();levelCaptured=true;continue;
                        }
                    }
                }
                var next=TacticalRules.Suggest(game.State);Check(next!=null,"No legal suggested action in "+state.phase);
                var chosen=next.type=="reward"?TacticalRules.EffectiveReward(game.State,game.State.rewards.FirstOrDefault(r=>r.id==next.choice)):null;
                var learning=chosen?.kind=="skill"?TacticalRules.SkillRecipient(game.State,chosen.skillId):null;
                int priorLevel=learning!=null&&learning.skills.Contains(chosen.skillId)?TacticalRules.SkillLevel(learning,chosen.skillId):0;
                var beforeAction=game.State;
                yield return SavedAction(next);
                if(new[]{"event","reward","camp","buy","rerollShop"}.Contains(next.type))
                {
                    VerifyReceipt(beforeAction,next);
                    if(next.type=="event"&&!eventChecked){yield return Capture("09-event-receipt");eventChecked=true;}
                    if(next.type=="reward"&&!rewardReceiptChecked){yield return Capture("09-reward-receipt");rewardReceiptChecked=true;}
                    game.Notify("");
                    if(game.State.phase=="map"&&game.State.relics.Count>0){Click("Map inventory");VerifyInventorySelection();if(!captures.Contains("09-owned-inventory.png"))yield return Capture("09-owned-inventory");Click("Close modal corner");}
                }
                if(learning!=null&&priorLevel>0&&!levelCaptured)
                {
                    Check(TacticalRules.SkillLevel(TacticalRules.FindUnit(game.State,learning.id),chosen.skillId)==priorLevel+1,"Repeated reward did not raise actual skill level.");
                    game.InspectUnit(learning.id);Click("Inspect skill "+chosen.skillId);Check(LiveText("Selected skill level").text.Contains("Lv."+(priorLevel+1)),"Reward upgrade detail still shows old level.");yield return Capture("10-upgraded-skill");game.CloseOverlay();levelCaptured=true;
                }
            }
            throw new InvalidOperationException("Focused ascent exceeded legal action bound.");
        }
        private static bool CanReach(TacticalState state,TacticalMapNode node,string id)
        {
            if(node.id==id)return true;
            if(node.floor>=7)return false;
            return node.next.Any(next=>CanReach(state,state.mapNodes.First(n=>n.id==next),id));
        }
    }

    public sealed partial class TacticalDirector
    {
        // Presentation-only fixtures are scoped to the opt-in verification process.
        // They never call Request/TrySave and always return an exact restoration action.
        internal Action BeginExperiencePresentationFixture(TacticalState checkpoint)
        {
            var arguments=System.Environment.GetCommandLineArgs();string directory=Path.GetFullPath(SaveDirectory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(!qa||!arguments.Contains("--dicebound-experience-verify")||!arguments.Contains("--dicebound-save-dir")||
                !Path.GetFileName(directory).StartsWith("experience-player-",StringComparison.Ordinal)||
                string.Equals(directory,Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||Busy||Modal||checkpoint==null)
                throw new InvalidOperationException("Presentation fixtures require the isolated experience verification directory and an idle scene.");
            if(!TacticalValidation.Valid(checkpoint)||!TacticalCodec.TryDecode(TacticalCodec.Encode(checkpoint),out var next,out var error))
                throw new InvalidDataException("Invalid presentation fixture.");
            var original=State;string originalSelected=selected,originalSkill=skill,originalThrow=throwObject,originalReward=jadeRewardChoice;
            int originalPage=skillPage;var originalPending=TacticalRangeSnapshot.Copy(pending);var originalHovered=TacticalRangeSnapshot.Copy(hovered);bool restored=false;
            Action restore=()=>{
                if(restored)return;restored=true;CloseOverlay();State=original;selected=originalSelected;skill=originalSkill;throwObject=originalThrow;skillPage=originalPage;jadeRewardChoice=originalReward;pending=originalPending;hovered=originalHovered;Notify("");Render();
            };
            try{State=next;selected=null;skill=null;throwObject=null;skillPage=0;jadeRewardChoice=null;pending=null;hovered=null;Notify("");Render();return restore;}
            catch{restore();throw;}
        }
    }
}
