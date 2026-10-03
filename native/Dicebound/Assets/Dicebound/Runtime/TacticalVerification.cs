using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicebound.Tactics;
using Dicebound.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dicebound.Presentation
{
    /// <summary>Opt-in Player verification uses the live controls, isolated files and ordinary legal actions.</summary>
    public sealed class TacticalVerification : MonoBehaviour
    {
        private TacticalDirector game;
        private string output;
        private int actions, errors, journeys, skills, moves, throws,mapEntries,shopPurchases,mapInspectionChecks,companionSkillChecks,groundViewChecks,groundViewReveals;
        private bool groundViewControlsChecked;
        private readonly HashSet<string> ascentKinds=new HashSet<string>();
        private readonly HashSet<string> phases = new HashSet<string>();
        private readonly List<float> frames = new List<float>();
        private string failure;
        private AudioMeter meter;
        private bool interrupted;
        private bool boardRefreshChecked;
        private bool uiChecked;
        private readonly bool uiMatrix=Environment.GetCommandLineArgs().Contains("--dicebound-ui-matrix");
        private readonly bool uiSmoke=Environment.GetCommandLineArgs().Contains("--dicebound-ui-smoke");
        private readonly bool groundViewFocus=Environment.GetCommandLineArgs().Contains("--dicebound-ground-view-verify");
        private readonly List<string> smokeCaptures=new List<string>();
        private int portraitChecks;
        private int matrixCaptures;
        private bool battleArtChecked,cinematicCleanupChecked,actionCameraInterrupted,scenePerformanceChecked;
        private readonly HashSet<string> visualSkills=new HashSet<string>();
        private readonly HashSet<string> cinematicHeroes=new HashSet<string>();
        private readonly HashSet<string> cinematicGallery=new HashSet<string>();
        private readonly HashSet<string> facingSheets=new HashSet<string>();
        private readonly HashSet<string> actionImages=new HashSet<string>();
        private int facingChecks,projectionChecks,liveMoveFacingChecks,liveSkillFacingChecks;
        private void OnEnable() { Application.logMessageReceived += Log; }
        private void OnDisable() { Application.logMessageReceived -= Log; }
        private void Log(string text, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) { errors++; failure = text + "\n" + stack; } }
        private void Update()
        {
            frames.Add(Time.unscaledDeltaTime * 1000);
            if (failure != null && output != null) { File.WriteAllText(Path.Combine(output, uiSmoke?"ui-smoke-failed.txt":"verification-failed.txt"), failure); Application.Quit(1); }
        }
        private IEnumerator Start()
        {
            game = GetComponent<TacticalDirector>(); output = game.SaveDirectory;
            Directory.CreateDirectory(output);
            if(uiSmoke&&(!Environment.GetCommandLineArgs().Contains("--dicebound-save-dir")||game.State!=null||
                string.Equals(Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)))
                throw new Exception("UI smoke requires a fresh, explicitly isolated save directory.");
            if(TacticalArt.IconCount!=56)throw new Exception("Incomplete painted UI icon set.");
            meter = FindFirstObjectByType<AudioListener>().gameObject.AddComponent<AudioMeter>();
            yield return Guarded(groundViewFocus?VerifyGroundViewFixture():uiSmoke?Smoke():Verify());
        }
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool moved = false; object result = null;
                try { moved = stack.Peek().MoveNext(); if (moved) result = stack.Peek().Current; }
                catch (Exception e) { failure = e.ToString(); Debug.LogException(e); }
                if (failure != null) yield break;
                if (!moved) { stack.Pop(); continue; }
                if (result is IEnumerator nested) stack.Push(nested); else yield return result;
            }
        }
        [Serializable] private sealed class UiSmokeReport
        {
            public string mode="ui-smoke";
            public bool complete=true,fullJourneyVerification=false;
            public string phase;
            public int chapter,floor,actions,errors,width,height,portraitChecks,matrixCaptures;
            public string journeyMode;
            public string[] captures,inspectedHeroes;
        }
        private IEnumerator Smoke()
        {
            yield return new WaitForSecondsRealtime(.7f);yield return Capture("00-title");
            Invoke("Title settings");VerifyVolumeControls();yield return Capture("00-settings");Invoke("Close modal corner");
            Invoke("New tactics");yield return Capture("01-party");
            var inspected=new List<string>();
            foreach(var hero in TacticalContent.Heroes.Where(h=>h.id!="ruanzhuo"))
            {yield return VerifyInspection(hero.id);yield return Capture("01-portrait-"+hero.id);inspected.Add(hero.id);}
            Invoke("Start tactics");yield return null;
            if(game.State==null||game.State.phase!="map"||game.State.journeyMode!="ascent"||game.State.floor!=-1)throw new Exception("Live departure did not open the new ascent map.");
            var captured=new HashSet<string>();bool terrainCaptured=false,bridgeCaptured=false;
            for(int step=0;step<1000;step++)
            {
                var state=game.State;phases.Add(state.phase);
                if(state.phase=="defeat"||state.phase=="victory")throw new Exception("UI smoke ended before the bridge scene: "+state.phase);
                string key=state.phase+"-"+state.chapter;
                bool wanted=(state.phase=="battle"&&(state.chapter==0||state.chapter==2))||state.phase=="reward"&&state.chapter==0||state.phase=="map"||state.phase=="shop"||state.phase=="event"||state.phase=="camp";
                if(wanted&&captured.Add(key))yield return Capture("team0-"+key);
                if(state.phase=="battle"&&!terrainCaptured)
                {Invoke("Terrain guide");yield return Capture("10-terrain-guide");Invoke("Close modal corner");terrainCaptured=true;}
                if(state.phase=="battle"&&state.chapter==2){bridgeCaptured=true;break;}
                var request=AscentCoverage(state)??TacticalRules.Suggest(state);
                if(request==null||!TacticalRules.Preview(state,request).ok)throw new Exception("No legal UI smoke action in "+key);
                yield return Drive(request);
            }
            if(!bridgeCaptured||!terrainCaptured||!captured.Contains("reward-0")||!captured.Any(k=>k.StartsWith("map-"))||!phases.Contains("shop")||!phases.Contains("event")||inspected.Count!=5)
                throw new Exception("UI smoke did not collect its required live pages.");
            string encoded=TacticalCodec.Encode(game.State);
            if(!new TacticalStore(output).TryLoad(out var saved,out var error)||TacticalCodec.Encode(saved)!=encoded)
                throw new Exception("UI smoke checkpoint differs from the legal displayed journey: "+error);
            game.Stage.Environment.SetShadowDebug(true);
            yield return Capture("lighting-realtime-shadow-diagnostic");
            game.Stage.Environment.SetShadowDebug(false);
            yield return VerifyCinematicGallery();
            var report=new UiSmokeReport{phase=game.State.phase,chapter=game.State.chapter,floor=game.State.floor,journeyMode=game.State.journeyMode,actions=actions,errors=errors,
                width=Screen.width,height=Screen.height,portraitChecks=portraitChecks,matrixCaptures=matrixCaptures,
                captures=smokeCaptures.ToArray(),inspectedHeroes=inspected.ToArray()};
            File.WriteAllText(Path.Combine(output,"ui-smoke.json"),JsonUtility.ToJson(report,true));
            Debug.Log("DICEBOUND_UI_SMOKE previewOnly=true captures="+smokeCaptures.Count+" legalActions="+actions);
            Application.Quit(errors==0?0:1);
        }
        private IEnumerator Verify()
        {
            yield return new WaitForSecondsRealtime(.7f); yield return Capture("00-title");
            game.Settings();VerifyVolumeControls();yield return Capture("00-settings");game.CloseOverlay();
            Invoke("New tactics"); yield return Capture("01-party");
            foreach(var hero in TacticalContent.Heroes.Where(h=>h.id!="ruanzhuo"))
            { yield return VerifyInspection(hero.id); yield return Capture("01-portrait-"+hero.id); }
            Invoke("Roster sixuan"); yield return null;
            Invoke(CompanionToggleName("sixuan")); yield return null;
            if(Control("Start tactics").interactable)throw new Exception("Party must require three companions.");
            Invoke(CompanionToggleName("sixuan")); yield return null;
            if(!Control("Start tactics").interactable)throw new Exception("Three companions must enable departure.");
            var teams = new[] { new[] { "sixuan", "lingfeng", "cangling" }, new[] { "yanzhuying", "shangshuo", "sixuan" } };
            for (int team = 0; team < teams.Length; team++)
            {
                if(team==1){game.Settings();Invoke("Reduce motion");game.CloseOverlay();if(!game.ReducedMotion)throw new Exception("Reduced motion control did not apply.");}
                game.StartRun((uint)(71 + team * 52), teams[team]);
                if(game.State.phase!="map"||game.State.journeyMode!="ascent"||game.State.floor!=-1)throw new Exception("Default live journey did not start at the ascent map.");
                var captured = new HashSet<string>();
                for (int step = 0; step < 1700 && game.State.phase != "victory" && game.State.phase != "defeat"; step++)
                {
                    var state = game.State; phases.Add(state.phase);
                    if(state.phase=="battle"&&!boardRefreshChecked){VerifyBoardRefresh();boardRefreshChecked=true;}
                    if(state.phase=="battle"&&!groundViewControlsChecked){yield return VerifyGroundViewControls();groundViewControlsChecked=true;}
                    if(state.phase=="battle"&&!uiChecked){yield return VerifyTacticalUi();uiChecked=true;}
                    if(state.phase=="battle")VerifyFacingFrames();
                    string key = state.phase + "-" + state.chapter + (state.journeyMode!="ascent"&&state.sideBattle ? "-side" : state.nodeKind=="elite"?"-elite":"");
                    if (captured.Add(key)) yield return Capture("team" + team + "-" + key);
                    if(state.phase=="battle"&&!scenePerformanceChecked){yield return MeasureSceneFrames();scenePerformanceChecked=true;}
                    var request = PreferVisualCoverage(state) ?? AscentCoverage(state) ?? TacticalRules.Suggest(state);
                    // Exercise the route choices as displayed, including the additional battle path.
                    if (state.phase == "route")
                    {
                        string preferred = new[] { "battle", "event", "camp", "elite" }[(state.chapter + team) % 4];
                        var route = state.routes.FirstOrDefault(r => r.kind == preferred);
                        if (route != null) request = new TacticalRequest { type = "route", choice = route.id };
                    }
                    if (state.phase == "camp" && team == 0 && TacticalRules.Preview(state, new TacticalRequest { type = "camp", choice = "study" }).ok)
                        request = new TacticalRequest { type = "camp", choice = "study" };
                    if (request == null || !TacticalRules.Preview(state, request).ok) throw new Exception("No legal verification action in " + key);
                    yield return Drive(request);
                }
                if (game.State.phase != "victory" || game.State.chapter != 4||game.State.floor!=TacticalRules.AscentFloors-1||game.State.journeyMode!="ascent"||game.State.mapNodes.Count(n=>n.visited&&n.completed)!=TacticalRules.AscentFloors) throw new Exception("Live controls did not complete all twelve ascent floors. Team " + team + " phase=" + game.State.phase);
                journeys++; phases.Add("victory"); yield return Capture("team" + team + "-ending");
                var encoded = TacticalCodec.Encode(game.State);
                if (!new TacticalStore(output).TryLoad(out var loaded, out var error) || TacticalCodec.Encode(loaded) != encoded) throw new Exception("Committed Player archive mismatch: " + error);
                if (!TacticalCodec.TryDecode(encoded, out var imported, out error) || TacticalCodec.Encode(imported) != encoded) throw new Exception("Portable tactical export mismatch: " + error);
            }
            game.Chronicle(); yield return Capture("09-chronicle"); game.CloseOverlay();
            game.Help(); yield return Capture("10-guide"); int guidePage=1; while(Control("Next reading page").interactable){Invoke("Next reading page");yield return Capture("10-guide-"+(++guidePage));} game.CloseOverlay();
            game.TerrainGuide();yield return Capture("10-terrain-guide");game.CloseOverlay();
            game.Settings(); yield return Capture("11-settings"); game.CloseOverlay();
            if (meter.Samples == 0 || meter.Peak <= .0001f) throw new Exception("Tactical audio produced no signal.");
            if (moves == 0 || skills == 0) throw new Exception("Incomplete tactical action coverage.");
            if(groundViewChecks==0||groundViewReveals==0)throw new Exception("Incomplete real ground-view control / obscured-target coverage.");
            if(mapEntries!=journeys*TacticalRules.AscentFloors||mapInspectionChecks!=mapEntries||shopPurchases==0||new[]{"battle","event","shop","elite","boss"}.Any(k=>!ascentKinds.Contains(k)))throw new Exception("Incomplete live ascent node/shop coverage.");
            if(companionSkillChecks<TacticalContent.Skills.Length)throw new Exception("Companion detail inspection did not cover the actual skill data.");
            foreach(string id in new[]{"cleave","balance","tide","oath"})if(!visualSkills.Contains(id))throw new Exception("Missing live skill visual: "+id);
            if(!cinematicHeroes.Contains("lingfeng")||!cinematicCleanupChecked||cinematicGallery.Count!=5||!actionCameraInterrupted)throw new Exception("Cinematic coverage is incomplete.");
            File.WriteAllText(Path.Combine(output,"art-verification.json"),"{\"skills\":["+string.Join(",",visualSkills.OrderBy(s=>s).Select(s=>"\""+s+"\""))+"],\"cinematics\":["+string.Join(",",cinematicHeroes.OrderBy(s=>s).Select(s=>"\""+s+"\""))+"],\"presentationOnlyGallery\":["+string.Join(",",cinematicGallery.OrderBy(s=>s).Select(s=>"\""+s+"\""))+"],\"cleanupAndReducedMotion\":true,\"portraitAspectChecks\":"+game.Stage.UltimateCutIn.ArtworkAspectChecks+"}");
            // 两队覆盖五位可选同行者与五类普通敌；精英、首领图集在此基础上增加。
            var requiredSheets=new[]{"sixuan","lingfeng","cangling","yanzhuying","shangshuo","sluice","loom","echo","seal","afterimage","sunwheel"};
            var missingSheets=requiredSheets.Where(id=>!facingSheets.Contains(id)).ToArray();
            if(missingSheets.Length>0||facingChecks!=facingSheets.Count*48||projectionChecks!=facingSheets.Count*48||liveMoveFacingChecks==0||liveSkillFacingChecks==0)throw new Exception("Incomplete rendered facing coverage. Missing: "+string.Join(",",missingSheets));
            Debug.Log("DICEBOUND_FACING_VERIFIED sheets="+facingSheets.Count+" framesAndDirections="+facingChecks+" liveMoveSamples="+liveMoveFacingChecks+" liveSkillSamples="+liveSkillFacingChecks);
            Debug.Log("DICEBOUND_PROJECTION_VERIFIED undistortedFrames="+projectionChecks+" paintedIcons="+TacticalArt.IconCount);
            if(!uiChecked||uiMatrix&&matrixCaptures<40)throw new Exception("Incomplete tactical UI verification.");
            Debug.Log("DICEBOUND_UI_VERIFIED edgeCases=true matrixCaptures="+matrixCaptures);
            frames.Sort(); float p95 = frames[(int)((frames.Count - 1) * .95f)];
            string report = "{\"phase\":\"victory\",\"chapter\":4,\"journeyMode\":\"ascent\",\"floor\":11,\"floors\":12,\"mapEntries\":"+mapEntries+",\"mapInspectionChecks\":"+mapInspectionChecks+",\"shopPurchases\":"+shopPurchases+",\"groundViewChecks\":"+groundViewChecks+",\"groundViewReveals\":"+groundViewReveals+",\"nodeKinds\":["+string.Join(",",ascentKinds.OrderBy(k=>k).Select(k=>"\""+k+"\""))+"],\"journeys\":" + journeys + ",\"actions\":" + actions + ",\"moves\":" + moves + ",\"skills\":" + skills + ",\"throws\":" + throws + ",\"interactions\":" + 0 + ",\"errors\":" + errors + ",\"width\":" + Screen.width + ",\"height\":" + Screen.height + ",\"audioPeak\":" + meter.Peak.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ",\"p95FrameMs\":" + p95.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"phases\":[" + string.Join(",", phases.Select(p => "\"" + p + "\"")) + "]}";
            File.WriteAllText(Path.Combine(output, "verification.json"), report); Debug.Log("DICEBOUND_TACTICAL_VERIFICATION " + report); Application.Quit(errors == 0 ? 0 : 1);
        }
        private Button Control(string name) => game.Canvas.GetComponentsInChildren<Button>().Single(b => b.isActiveAndEnabled && b.name == name);
        private bool ButtonExists(string name) => game.Canvas.GetComponentsInChildren<Button>().Any(b => b.isActiveAndEnabled && b.name == name);
        [Serializable] private sealed class ScenePerformance
        {
            public string scope="Idle live battlefield, after warmup; excludes screenshot captures and resizing",gpu;
            public int width,height,samples;
            public float medianFrameMs,p95FrameMs;
        }
        private IEnumerator MeasureSceneFrames()
        {
            for(int i=0;i<30;i++)yield return new WaitForEndOfFrame();
            var sample=new List<float>();
            for(int i=0;i<180;i++){yield return new WaitForEndOfFrame();sample.Add(Time.unscaledDeltaTime*1000);}
            sample.Sort();
            var report=new ScenePerformance{gpu=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,samples=sample.Count,
                medianFrameMs=sample[sample.Count/2],p95FrameMs=sample[(int)((sample.Count-1)*.95f)]};
            File.WriteAllText(Path.Combine(output,"render-performance.json"),JsonUtility.ToJson(report,true));
            Debug.Log("DICEBOUND_SCENE_FRAMES "+JsonUtility.ToJson(report));
        }
        private void VerifyVolumeControls()
        {
            var sliders=game.Canvas.GetComponentsInChildren<Slider>().Where(s=>s.isActiveAndEnabled).ToArray();
            if(sliders.Length!=3)throw new Exception("Missing volume controls.");
            foreach(var slider in sliders)
            {
                float original=slider.value;
                foreach(float value in new[]{0f,.37f,1f})
                {
                    slider.value=value;Canvas.ForceUpdateCanvases();
                    var fill=slider.fillRect;var area=(RectTransform)fill.parent;var corners=new Vector3[4];fill.GetWorldCorners(corners);
                    foreach(var point in corners){var local=area.InverseTransformPoint(point);if(local.x<area.rect.xMin-.1f||local.x>area.rect.xMax+.1f||local.y<area.rect.yMin-.1f||local.y>area.rect.yMax+.1f)throw new Exception("Volume fill escapes its track: "+slider.name);}
                    if(Mathf.Abs(fill.rect.width-area.rect.width*value)>.1f)throw new Exception("Volume fill differs from its value: "+slider.name);
                    var knob=slider.handleRect;
                    if(Mathf.Abs(knob.rect.width-knob.rect.height)>.1f)throw new Exception("Round volume handle is stretched: "+slider.name);
                }
                slider.value=original;
            }
            Debug.Log("DICEBOUND_VOLUME_CONTROLS_VERIFIED controls=3 boundaryValues=3");
        }
        private void VerifyBoardRefresh()
        {
            var state=game.State;string encoded=TacticalCodec.Encode(state);
            var cell=state.terrain.First(c=>c.kind=="plain");
            var original=game.Stage.GetComponentsInChildren<TacticalTileHit>().Single(t=>t.X==cell.x&&t.Y==cell.y);
            float originalY=original.transform.position.y;
            var alternate=state.Clone();alternate.terrain.Single(c=>c.x==cell.x&&c.y==cell.y).kind="high";
            if(!TacticalValidation.Valid(alternate))throw new Exception("Invalid terrain refresh fixture.");
            game.Stage.Synchronize(alternate);
            var refreshed=game.Stage.GetComponentsInChildren<TacticalTileHit>().Single(t=>t.X==cell.x&&t.Y==cell.y);
            if(refreshed==original||refreshed.transform.position.y-originalY<.2f)throw new Exception("Same-seed alternate archive retained stale ground geometry.");
            game.Stage.Synchronize(state);
            var restored=game.Stage.GetComponentsInChildren<TacticalTileHit>().Single(t=>t.X==cell.x&&t.Y==cell.y);
            if(Mathf.Abs(restored.transform.position.y-originalY)>.001f||TacticalCodec.Encode(state)!=encoded)throw new Exception("Terrain refresh changed the committed journey.");
            Debug.Log("DICEBOUND_TERRAIN_REFRESH_VERIFIED");
            game.Render();
        }
        private void VerifyFacingFrames()
        {
            foreach(var actor in game.Stage.GetComponentsInChildren<TacticalActor>())
            {
                string id=TacticalRules.FindUnit(game.State,actor.GetComponent<TacticalActorHit>().UnitId).heroId;
                if(!facingSheets.Add(id))continue;
                Quaternion rotation=actor.transform.rotation;
                foreach(var direction in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left})
                foreach(string pose in new[]{"idle","run","attack"})for(int frame=0;frame<4;frame++)
                {
                    actor.transform.rotation=Quaternion.LookRotation(direction);actor.Pose(pose,(frame+.25f)/4);
                    CheckProjectedFacing(actor,direction,id+" "+pose+" frame="+frame);facingChecks++;
                    CheckSpriteProjection(actor);projectionChecks++;
                }
                actor.transform.rotation=rotation;actor.Idle();
            }
        }
        private void CheckProjectedFacing(TacticalActor actor,Vector3 direction,string context)
        {
            var camera=game.Stage.Camera;
            if(direction.sqrMagnitude<.000001f||Mathf.Abs(Vector3.Dot(direction.normalized,camera.transform.right))<.05f)return;
            var visual=actor.GetComponentInChildren<MeshFilter>();var vertices=visual.sharedMesh.vertices;var uv=visual.sharedMesh.uv;
            // The original sprite poses look towards increasing image U. Check the actual rendered
            // mesh in screen space, independently of its mirror flag or the actor's local axes.
            if(uv[3].x<=uv[0].x)throw new Exception("Facing sample lacks an increasing-U edge.");
            float visibleRight=camera.WorldToScreenPoint(visual.transform.TransformPoint(vertices[3])).x-camera.WorldToScreenPoint(visual.transform.TransformPoint(vertices[0])).x;
            float targetRight=camera.WorldToScreenPoint(actor.transform.position+direction.normalized).x-camera.WorldToScreenPoint(actor.transform.position).x;
            if(visibleRight*targetRight<=0)throw new Exception("Sprite faces away from travel/target: "+context+" imageRight="+visibleRight+" targetRight="+targetRight);
        }
        private void CheckSpriteProjection(TacticalActor actor)
        {
            var visual=actor.GetComponentInChildren<MeshFilter>();var mesh=visual.sharedMesh;var vertices=mesh.vertices;var camera=game.Stage.Camera;
            Vector2 origin=camera.WorldToScreenPoint(visual.transform.TransformPoint(vertices[0]));
            Vector2 right=(Vector2)camera.WorldToScreenPoint(visual.transform.TransformPoint(vertices[3]))-origin;
            Vector2 up=(Vector2)camera.WorldToScreenPoint(visual.transform.TransformPoint(vertices[1]))-origin;
            float horizontalScale=right.magnitude/Vector3.Distance(vertices[3],vertices[0]);
            float verticalScale=up.magnitude/Vector3.Distance(vertices[1],vertices[0]);
            if(Mathf.Abs(Vector2.Dot(right.normalized,up.normalized))>.003f||Mathf.Abs(right.y)>right.magnitude*.003f||Mathf.Abs(up.x)>up.magnitude*.003f||Mathf.Abs(horizontalScale/verticalScale-1)>.003f)
                throw new Exception("Pixel silhouette is skewed or compressed: "+mesh.name+" ratio="+horizontalScale/verticalScale);
        }
        private void Invoke(string name)
        { var button = Control(name); if (!button.interactable) throw new Exception("Disabled live control: " + name); button.onClick.Invoke(); }
        private string CompanionToggleName(string heroId)
        {
            return game.Canvas.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&
                (b.name=="Choose "+heroId||b.name=="Remove "+heroId)).name;
        }
        private string CompanionSelectionSnapshot()
        {
            // Membership is visible on the five available roster portraits, while the action button
            // belongs only to the currently inspected hero in the approved layout.
            return string.Join("|",TacticalContent.Heroes.Where(h=>h.id!="ruanzhuo").OrderBy(h=>h.id).Select(hero=>
            {
                var roster=Control("Roster "+hero.id);
                bool chosen=roster.GetComponentsInChildren<Transform>().Any(t=>
                    t.gameObject.activeInHierarchy&&t.name=="Chosen badge "+hero.id);
                return hero.id+":"+chosen;
            }));
        }
        private void CheckGroundArchive(string encoded)
        {
            bool loaded=new TacticalStore(output).TryLoad(out var saved,out var error);
            if(TacticalCodec.Encode(game.State)!=encoded||!loaded||TacticalCodec.Encode(saved)!=encoded)
                throw new Exception("Ground inspection changed the live or saved journey: "+error);
            groundViewChecks++;
        }
        private void CheckGroundBodies(bool visible)
        {
            var actors=game.Stage.GetComponentsInChildren<TacticalActor>().Where(a=>a.gameObject.activeInHierarchy).ToArray();
            if(actors.Length==0||actors.Any(a=>a.BodyVisible!=visible||!a.GetComponentInChildren<SpriteRenderer>().enabled))throw new Exception("Ground view lost its character body / retained foot shadow contract.");
            foreach(var actor in actors)
            {
                var unit=TacticalRules.FindUnit(game.State,actor.GetComponent<TacticalActorHit>().UnitId);
                var badge=game.Stage.GetComponentsInChildren<Canvas>().Single(c=>c.name=="Status "+unit.id);
                var nameTransform=badge.transform.Find("Name");
                // The badge anchors to the atlas's real frame height, not the 1.65 nominal constant;
                // assert the same live anchor so alternate sprite imports stay verifiable.
                float expected=Vector3.Distance(badge.transform.position,actor.VisualPoint(visible?actor.StandingHeight+.22f:.26f));
                if(nameTransform==null||!nameTransform.gameObject.activeInHierarchy||expected>.001f)
                    throw new Exception("Ground view lost its readable, correctly placed unit name. unit="+unit.id+" nameActive="+(nameTransform!=null&&nameTransform.gameObject.activeInHierarchy)+" dist="+expected.ToString("F4")+" standing="+actor.StandingHeight.ToString("F3"));
            }
            groundViewChecks++;
        }
        private IEnumerator VerifyGroundViewControls()
        {
            string encoded=TacticalCodec.Encode(game.State);
            var hero=game.State.units.First(u=>u.team=="hero"&&u.hp>0);
            // Ordinary visible silhouette clicking still chooses that companion.
            var point=game.Stage.TargetScreenPoint(hero.x,hero.y);
            if(!game.Stage.PressAt(point))throw new Exception("Ordinary character selection no longer accepts a visible body.");
            yield return null;
            var cell=TacticalRules.MoveCells(game.State,hero.id).FirstOrDefault();
            if(cell==null)throw new Exception("Ground selection fixture has no ordinary move preview.");
            game.Hover(cell.x,cell.y);
            if(game.HoveredTarget?.unitId!=hero.id)throw new Exception("Ordinary body picking chose a different companion.");
            Invoke("Cancel action");CheckGroundArchive(encoded);
            Invoke("Ground view");if(!game.Stage.GroundViewActive)throw new Exception("Ground button did not reveal the ground.");CheckGroundBodies(false);CheckGroundArchive(encoded);
            // A real wall continues to be a raycast target and an invalid move.
            var wall=game.State.terrain.Where(c=>c.kind=="wall").FirstOrDefault(c=>game.Stage.TryTargetScreenPoint(c.x,c.y,out _));
            if(wall==null)throw new Exception("Ground fixture has no visible real wall.");
            if(!game.Stage.PressAt(game.Stage.TargetScreenPoint(wall.x,wall.y))||game.PendingTarget?.x!=wall.x||game.PendingTarget?.y!=wall.y||Control("Commit action").interactable)
                throw new Exception("Ground inspection bypassed real wall picking or movement rejection.");
            Invoke("Cancel action");if(game.Stage.GroundViewActive)throw new Exception("Cancel retained ground inspection.");CheckGroundBodies(true);CheckGroundArchive(encoded);
            Invoke("Ground view");Invoke("Ground view");if(game.Stage.GroundViewActive)throw new Exception("Ground button could not restore characters.");CheckGroundBodies(true);CheckGroundArchive(encoded);
            Invoke("Ground view");game.SetPresentationPaused(true);if(game.Stage.GroundViewActive)throw new Exception("Presentation pause retained hidden characters.");CheckGroundBodies(true);CheckGroundArchive(encoded);
            Invoke("Ground view");Invoke("Terrain guide");if(game.Stage.GroundViewActive)throw new Exception("Modal retained ground inspection.");Invoke("Close modal corner");CheckGroundBodies(true);CheckGroundArchive(encoded);
            Invoke("Ground view");Invoke("Title");if(game.Stage.GroundViewActive)throw new Exception("Leaving battle retained ground inspection.");Invoke("Continue tactics");yield return null;CheckGroundBodies(true);CheckGroundArchive(encoded);
        }
        private IEnumerator RevealGroundTarget(TacticalRequest request)
        {
            string encoded=TacticalCodec.Encode(game.State);
            Invoke("Ground view");if(!game.Stage.GroundViewActive)throw new Exception("Obscured target was not revealed by the live ground button.");
            CheckGroundBodies(false);CheckGroundArchive(encoded);groundViewReveals++;
            var point=game.Stage.TargetScreenPoint(request.x,request.y);
            int pixelChecks=0;
            var actors=game.Stage.GetComponentsInChildren<TacticalActor>().Where(a=>a.gameObject.activeInHierarchy).ToArray();
            for(int frame=0;frame<4;frame++)
            {
                foreach(var actor in actors)actor.Pose("idle",(frame+.25f)/4);
                // A contiguous 9x9 pixel area works for every idle frame, not tiny
                // transparent holes found by waiting for a particular silhouette.
                for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=4;dy++)
                {if(!game.Stage.PointCell(point+new Vector2(dx,dy),out int x,out int y)||x!=request.x||y!=request.y)throw new Exception("Revealed ground target is not a stable 9x9 pointer area: "+request.x+","+request.y);pixelChecks++;}
                groundViewChecks++;yield return null;
            }
            foreach(var actor in actors)actor.Idle();CheckGroundArchive(encoded);
            Debug.Log("DICEBOUND_GROUND_REVEAL cell="+TacticalDirector.GridName(request.x,request.y)+" idleFrames=4 contiguousPixels=81 rayChecks="+pixelChecks+" screen="+point+" immutable=true");
            if(groundViewFocus)yield return Capture("ground-view-revealed");
        }
        [Serializable] private sealed class GroundViewReport
        {
            public string mode="ground-view-focus";
            public bool complete,normalTargetObscured,committedExactlyOnce,savedStateMatches;
            public int chapter,revisionBefore,revisionAfter,actions,errors,groundViewChecks,groundViewReveals,width,height;
            public string cell="E4";
        }
        private IEnumerator VerifyGroundViewFixture()
        {
            if(!Environment.GetCommandLineArgs().Contains("--dicebound-save-dir")||string.Equals(Path.GetFullPath(output).TrimEnd('\\','/'),Path.GetFullPath(Application.persistentDataPath).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))
                throw new Exception("Ground focus requires an explicitly isolated copy of the failed checkpoint.");
            var state=game.State;if(state==null||state.phase!="battle"||state.chapter!=4||state.units.Any(u=>u.hp>0&&u.x==4&&u.y==3))throw new Exception("Ground focus requires the chapter-four E4 obstruction checkpoint.");
            yield return new WaitForSecondsRealtime(.7f);Invoke("Continue tactics");yield return null;
            yield return VerifyGroundViewControls();
            var request=state.units.Where(u=>u.team=="hero"&&u.hp>0).Select(u=>new TacticalRequest{type="move",unitId=u.id,x=4,y=3}).FirstOrDefault(r=>TacticalRules.Preview(state,r).ok);
            if(request==null)throw new Exception("E4 focus checkpoint has no legal move to reproduce.");
            Invoke("Select "+request.unitId);Invoke("Move mode");
            if(game.Stage.TryTargetScreenPoint(4,3,out _))throw new Exception("E4 obstruction fixture no longer reproduces its pre-reveal occlusion.");
            yield return Capture("ground-view-before");int before=game.State.revision;
            yield return Drive(request);
            var moved=TacticalRules.FindUnit(game.State,request.unitId);
            if(moved.x!=4||moved.y!=3||game.State.revision!=before+1||groundViewReveals!=1||game.Stage.GroundViewActive)throw new Exception("Revealed E4 move did not complete exactly once and restore characters.");
            yield return Capture("ground-view-after");CheckGroundArchive(TacticalCodec.Encode(game.State));
            var report=new GroundViewReport{complete=true,normalTargetObscured=true,committedExactlyOnce=true,savedStateMatches=true,chapter=game.State.chapter,revisionBefore=before,revisionAfter=game.State.revision,actions=actions,errors=errors,groundViewChecks=groundViewChecks,groundViewReveals=groundViewReveals,width=Screen.width,height=Screen.height};
            File.WriteAllText(Path.Combine(output,"ground-view-verify.json"),JsonUtility.ToJson(report,true));Application.Quit(errors==0?0:1);
        }
        private IEnumerator VerifyInspection(string heroId)
        {
            string archive=game.State==null?null:TacticalCodec.Encode(game.State);
            Func<string> selection=CompanionSelectionSnapshot;
            Func<string> definitions=()=>string.Join("|",TacticalContent.Heroes.Select(h=>h.id+","+h.name+","+h.maxHp+","+h.maxAp+","+h.maxSp+","+string.Join("/",h.skills)))+string.Join("|",TacticalContent.Skills.Select(s=>s.id+","+s.name+","+s.text+","+s.heroId+","+s.kind+","+s.resource+","+s.cost+","+s.range+","+s.cooldown+","+s.power+","+s.push+","+s.area));
            string selectedBefore=selection(),definitionsBefore=definitions();bool departure=Control("Start tactics").interactable;
            Invoke("Roster "+heroId);yield return null;
            if(game.Canvas.GetComponentsInChildren<Button>().Any(b=>b.isActiveAndEnabled&&b.name=="Companion attributes"))
            {Invoke("Companion attributes");yield return null;}
            var hero=TacticalContent.GetHero(heroId);int[] stats={hero.maxHp,hero.maxAp,hero.maxSp};
            for(int i=0;i<stats.Length;i++)if(game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Companion stat value "+i&&t.isActiveAndEnabled).text!=stats[i].ToString())throw new Exception("Companion attributes differ from content: "+heroId);
            Invoke("Companion skills");yield return null;
            var inspected=new HashSet<string>();
            while(true)
            {
                var names=game.Canvas.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled&&b.name.StartsWith("Inspect skill ")).Select(b=>b.name).ToArray();
                foreach(string name in names)
                {
                    Invoke(name);yield return null;var definition=TacticalContent.GetSkill(name.Substring("Inspect skill ".Length));
                    var effect=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Companion skill effect"&&t.isActiveAndEnabled);
                    var values=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.isActiveAndEnabled&&t.name=="Companion skill stats").text;
                    string cost=definition.cost+" "+(definition.resource=="charge"?"蓄势":definition.resource.ToUpperInvariant());
                    string expected=$"消耗 {cost}    距离 {definition.range} 格    冷却 {definition.cooldown} 轮";
                    if(effect.text!=definition.text||values!=expected)throw new Exception("Companion skill detail differs from its actual rules: "+heroId+" / "+definition.id);
                    CheckText("Companion detail "+heroId+" / "+definition.id);inspected.Add(definition.id);companionSkillChecks++;
                }
                if(!Control("Next companion skills").interactable)break;
                Invoke("Next companion skills");yield return null;
            }
            if(TacticalContent.Skills.Where(s=>string.IsNullOrEmpty(s.heroId)||s.heroId==hero.id||hero.skills.Contains(s.id)).Any(s=>!inspected.Contains(s.id)))throw new Exception("Companion skill pages omitted a legal ability: "+heroId);
            Invoke("Companion attributes");yield return null;
            if(selection()!=selectedBefore||definitions()!=definitionsBefore||Control("Start tactics").interactable!=departure||(game.State==null?null:TacticalCodec.Encode(game.State))!=archive)
                throw new Exception("Inspecting a companion changed the party, attributes or skills: "+heroId);
        }
        private static int NodeCoverage(TacticalMapNode node)
        {
            int mask=node.kind=="battle"||node.kind=="elite"||node.kind=="boss"?1<<node.chapter:0;
            if(node.kind=="event")mask|=1<<5;if(node.kind=="shop")mask|=1<<6;if(node.kind=="elite")mask|=1<<7;if(node.kind=="camp")mask|=1<<8;return mask;
        }
        private static int CoverageScore(int mask)
        {int score=0;for(int bit=0;bit<9;bit++)if((mask&(1<<bit))!=0)score+=bit<5?1000:500;return score;}
        private TacticalRequest AscentCoverage(TacticalState state)
        {
            if(state.journeyMode!="ascent")return null;
            if(state.phase=="shop")
            {
                // An actually offered ultimate may be learned; never inject skills or currency for coverage.
                var lingfeng=state.units.FirstOrDefault(u=>u.team=="hero"&&u.heroId=="lingfeng");
                if(lingfeng!=null&&!lingfeng.skills.Contains("oath"))
                    foreach(var offer in state.shopOffers.Where(o=>o.kind=="skill"&&o.skillId=="oath"))
                    {var buy=new TacticalRequest{type="buy",choice=offer.id,unitId=lingfeng.id};if(TacticalRules.Preview(state,buy).ok)return buy;}
                foreach(var offer in state.shopOffers.OrderBy(o=>o.kind=="relic"?0:o.kind=="heal"?1:2))
                foreach(var hero in state.units.Where(u=>u.team=="hero"))
                {
                    if(offer.kind=="skill"&&lingfeng!=null&&!lingfeng.skills.Contains("oath")&&hero==lingfeng)continue;
                    var buy=new TacticalRequest{type="buy",choice=offer.id,unitId=hero.id};if(TacticalRules.Preview(state,buy).ok)return buy;
                }
                return new TacticalRequest{type="leaveShop"};
            }
            if(state.phase!="map")return null;
            var nodes=state.mapNodes.ToDictionary(n=>n.id);int seen=0;
            foreach(var visited in state.mapNodes.Where(n=>n.visited))seen|=NodeCoverage(visited);
            var memo=new Dictionary<string,int>();
            Func<TacticalMapNode,int,int> score=null;
            score=(node,mask)=>
            {
                mask|=NodeCoverage(node);string key=node.id+":"+mask;
                if(memo.TryGetValue(key,out int cached))return cached;
                int required=node.floor==2&&node.kind=="event"||node.floor==4&&node.kind=="shop"||node.floor==7&&node.kind=="elite"||node.kind=="camp"?10000:0;
                int result=required+(node.next.Count==0?CoverageScore(mask):node.next.Max(id=>score(nodes[id],mask)));
                memo.Add(key,result);return result;
            };
            // Depth routes only guarantee campfires at fixed floors; take one greedily before the
            // symmetric coverage search can route past it.
            var selected=TacticalRules.AvailableNodes(state).FirstOrDefault(n=>n.kind=="camp"&&(seen&256)==0)
                ??TacticalRules.AvailableNodes(state).OrderByDescending(n=>score(n,seen))
                .ThenByDescending(n=>(NodeCoverage(n)&~seen)!=0).ThenBy(n=>n.kind=="battle"?0:n.kind=="elite"?1:n.kind=="shop"?2:3).ThenBy(n=>n.lane).FirstOrDefault();
            return selected==null?null:new TacticalRequest{type="node",choice=selected.id};
        }
        private TacticalRequest PreferVisualCoverage(TacticalState state)
        {
            var lingfeng=state.units.FirstOrDefault(u=>u.team=="hero"&&u.heroId=="lingfeng");
            if(state.phase=="reward"&&lingfeng!=null&&!cinematicHeroes.Contains("lingfeng"))
            {
                // Oath is a learned reward, not a starting ability. Use an actually offered card and its legal recipient.
                foreach(var reward in state.rewards.Where(r=>r.kind=="skill"&&r.skillId=="oath"))
                {
                    var learn=new TacticalRequest{type="reward",choice=reward.id,unitId=lingfeng.id};
                    if(TacticalRules.Preview(state,learn).ok)return learn;
                }
            }
            if(state.phase!="battle")return null;
            if(lingfeng!=null&&lingfeng.hp>0&&!cinematicHeroes.Contains("lingfeng")&&lingfeng.skills.Contains("oath"))
            {
                var ultimate=CoverageAttack(state,lingfeng,"oath");if(ultimate!=null)return ultimate;
            }
            var cangling=state.units.FirstOrDefault(u=>u.team=="hero"&&u.hp>0&&u.heroId=="cangling");
            if(cangling!=null&&!visualSkills.Contains("tide"))
            {
                var tide=CoverageAttack(state,cangling,"tide");if(tide!=null)return tide;
                // The normal shortest-route bot need not stand in mist. Reach existing water first,
                // then defend while the ordinary enemy turn brings a target into the real three-cell range.
                if(TacticalRules.TerrainAt(state,cangling.x,cangling.y)!="mist")
                {
                    var mist=TacticalRules.MoveCells(state,cangling.id).Where(c=>c.kind=="mist")
                        .OrderBy(c=>TacticalRules.Preview(state,new TacticalRequest{type="move",unitId=cangling.id,x=c.x,y=c.y}).path.Sum(p=>TacticalRules.MoveCost(state,p.x,p.y)))
                        .ThenBy(c=>state.units.Where(u=>u.team=="enemy"&&u.hp>0).Min(u=>TacticalRules.Distance(c.x,c.y,u.x,u.y))).FirstOrDefault();
                    if(mist!=null)return new TacticalRequest{type="move",unitId=cangling.id,x=mist.x,y=mist.y};
                }
                return CoverageDefend(state,cangling);
            }
            // Select ordinary legal player actions; never grant resources or relocate actors for art evidence.
            foreach(var unit in state.units.Where(u=>u.team=="hero"&&u.hp>0))
            foreach(string id in unit.skills.Where(id=>id=="cleave"||id=="balance"||id=="tide"||id=="oath"))
            {
                if(visualSkills.Contains(id)&&(id!="oath"||unit.heroId!="lingfeng"||cinematicHeroes.Contains("lingfeng")))continue;
                var attack=CoverageAttack(state,unit,id);if(attack!=null)return attack;
            }
            if(lingfeng!=null&&lingfeng.hp>0&&!cinematicHeroes.Contains("lingfeng")&&lingfeng.skills.Contains("oath"))
            {
                // Charge is accumulated by real attacks across battles. Prefer the least damaging legal
                // strike so that the last opponent is not cleared before the learned ultimate can be used.
                var attacks=new List<TacticalRequest>();
                foreach(string id in lingfeng.skills.Where(id=>id!="oath"))
                foreach(var enemy in state.units.Where(u=>u.team=="enemy"&&u.hp>0))
                {
                    var request=new TacticalRequest{type="skill",unitId=lingfeng.id,skillId=id,x=enemy.x,y=enemy.y};
                    if(TacticalRules.Preview(state,request).ok)attacks.Add(request);
                }
                var charge=attacks.OrderBy(a=>TacticalRules.Preview(state,a).damage).FirstOrDefault();if(charge!=null)return charge;
                var enemies=state.units.Where(u=>u.team=="enemy"&&u.hp>0).ToArray();
                int distance=enemies.Min(e=>TacticalRules.Distance(lingfeng.x,lingfeng.y,e.x,e.y));
                var cell=TacticalRules.MoveCells(state,lingfeng.id).OrderBy(c=>enemies.Min(e=>TacticalRules.Distance(c.x,c.y,e.x,e.y))).FirstOrDefault();
                if(cell!=null&&enemies.Min(e=>TacticalRules.Distance(cell.x,cell.y,e.x,e.y))<distance)
                    return new TacticalRequest{type="move",unitId=lingfeng.id,x=cell.x,y=cell.y};
                return CoverageDefend(state,lingfeng);
            }
            return null;
        }
        private static TacticalRequest CoverageAttack(TacticalState state,TacticalUnit unit,string id)
        {
            foreach(var enemy in state.units.Where(u=>u.team=="enemy"&&u.hp>0))
            {var request=new TacticalRequest{type="skill",unitId=unit.id,skillId=id,x=enemy.x,y=enemy.y};if(TacticalRules.Preview(state,request).ok)return request;}
            return null;
        }
        private static TacticalRequest CoverageDefend(TacticalState state,TacticalUnit protectedUnit)
        {
            foreach(var unit in state.units.Where(u=>u.team=="hero"&&u.hp>0))
            foreach(string id in unit.skills.Where(id=>TacticalContent.GetSkill(id).kind=="guard"))
            foreach(var target in new[]{protectedUnit,unit})
            {var request=new TacticalRequest{type="skill",unitId=unit.id,skillId=id,x=target.x,y=target.y};if(TacticalRules.Preview(state,request).ok)return request;}
            return new TacticalRequest{type="endTurn"};
        }
        private IEnumerator Drive(TacticalRequest request)
        {
            int before = game.State.revision;
            uint seedBefore=game.State.seed;
            Vector3 viewBefore=game.Stage.Camera.transform.position;float sizeBefore=game.Stage.Camera.orthographicSize;
            var unit=request.type=="move"||request.type=="skill"?TacticalRules.FindUnit(game.State,request.unitId):null;
            Vector3 origin=unit!=null?game.Stage.Environment.CellWorld(unit.x,unit.y):Vector3.zero;
            Vector3 castDirection=request.type=="skill"?game.Stage.Environment.CellWorld(request.x,request.y)-origin:Vector3.zero;
            Vector3[] affected=request.type=="skill"?TacticalRules.Preview(game.State,request).affected.Select(c=>game.Stage.Environment.CellWorld(c.x,c.y)).ToArray():Array.Empty<Vector3>();
            Vector3[] movePath=request.type=="move"?new[]{origin}.Concat(TacticalRules.Preview(game.State,request).path.Select(c=>game.Stage.Environment.CellWorld(c.x,c.y))).ToArray():Array.Empty<Vector3>();
            if (request.type == "begin") Invoke("Begin chapter");
            else if(request.type=="node")
            {
                var node=TacticalRules.AvailableNodes(game.State).Single(n=>n.id==request.choice);string archive=TacticalCodec.Encode(game.State);
                Invoke("Map node "+node.id);yield return null;
                bool inspectLoaded=new TacticalStore(output).TryLoad(out var inspected,out var inspectError);
                if(TacticalCodec.Encode(game.State)!=archive||!inspectLoaded||TacticalCodec.Encode(inspected)!=archive)
                    throw new Exception("Map inspection committed or altered a node: "+inspectError);
                mapInspectionChecks++;Invoke("Enter map node");mapEntries++;ascentKinds.Add(node.kind);
            }
            else if(request.type=="buy")
            {Invoke("Shop recipient "+request.unitId);yield return null;Invoke("Shop buy "+request.choice);shopPurchases++;}
            else if(request.type=="leaveShop")Invoke("Leave shop");
            else if (request.type == "reward")
            {
                Invoke("Reward recipient " + request.unitId); yield return null;
                Invoke("Reward choice " + request.choice); yield return null;
                Invoke("Claim reward");
            }
            else if (request.type == "route") Invoke("Route choice " + request.choice);
            else if (request.type == "event") Invoke("Event " + request.choice);
            else if (request.type == "camp") Invoke(game.State.journeyMode == "ascent" && game.State.ascentRevision >= 2 ? "Camp " + request.choice : request.choice == "rest" ? "Camp rest" : "Camp train");
            else if (request.type == "endTurn") Invoke("End round");
            else
            {
                Invoke("Select " + request.unitId); yield return null;
                if (request.type == "interact") throw new Exception("Retired special objective requested by ordinary battle.");
                else
                {
                    if (request.type == "skill")
                    {
                        // The paged skill bar shows seven skills per page; learned skills can sit on a later page.
                        int pageTries=0;
                        while(!ButtonExists("Skill "+request.skillId)&&pageTries++<12){Invoke("Next skill page");yield return null;}
                        Invoke("Skill " + request.skillId); skills++; if (request.skillId == "throw") throws++;
                    }
                    else { Invoke("Move mode"); moves++; }
                    yield return null;
                    if(!game.Stage.TryTargetScreenPoint(request.x,request.y,out var point))
                    {yield return RevealGroundTarget(request);point=game.Stage.TargetScreenPoint(request.x,request.y);}
                    if(!game.Stage.PointCell(point,out int x,out int y)||x!=request.x||y!=request.y)throw new Exception("3D tile raycast mismatch "+request.x+","+request.y+" -> "+x+","+y);
                    if(!game.Stage.PressAt(point))throw new Exception("3D target did not accept pointer");
                    yield return null; CheckPreviewHeight("pending "+request.type); CheckText("action preview "+request.type+" "+request.skillId); Invoke("Commit action");
                }
            }
            yield return null;
            if(uiSmoke&&game.Busy)
            {
                string committed=TacticalCodec.Encode(game.State);game.SetPresentationPaused(true);yield return null;
                if(game.Busy||TacticalCodec.Encode(game.State)!=committed)throw new Exception("UI smoke presentation changed its committed legal action.");
            }
            if(!interrupted&&game.Busy&&(request.type=="move"||request.type=="skill"))
            {
                string committed=TacticalCodec.Encode(game.State);game.SetPresentationPaused(true);yield return null;
                if(game.Busy||TacticalCodec.Encode(game.State)!=committed)throw new Exception("Interrupted presentation altered or locked the committed journey.");
                interrupted=true;
            }
            var acting=game.Stage.GetComponentsInChildren<TacticalActor>().FirstOrDefault(a=>{var hit=a.GetComponent<TacticalActorHit>();return hit!=null&&hit.UnitId==request.unitId;});
            if(acting==null&&(request.type=="move"||request.type=="skill"))throw new Exception("No live actor for the committed action: "+request.type+" "+request.skillId+" unit="+request.unitId+" phase="+game.State.phase);
            while (game.Busy)
            {
                yield return new WaitForEndOfFrame();
                if(!game.Busy)break;
                var cutIn=game.Stage.UltimateCutIn;
                if(cutIn.Active&&cutIn.Progress>=.28f&&cinematicHeroes.Add(cutIn.CurrentHero))
                {
                    if(!cutIn.ShowsCompleteArtwork||!cutIn.UsingDedicatedArt||!cutIn.HasPaintedEffects||!cutIn.HasImpactEffects)throw new Exception("Ultimate art or live impact effects are missing or distorted.");
                    cutIn.SetPreviewProgress(.44f);
                    yield return new WaitForEndOfFrame();
                    string path=Path.Combine(output,"ultimate-"+cutIn.CurrentHero+".png");ScreenCapture.CaptureScreenshot(path);yield return AwaitScreenshot(path,Screen.width,Screen.height);
                    cutIn.ReleasePreview();
                }
                var vfx=game.Stage.SkillVfx;
                if(vfx.Active&&vfx.Progress>=.32f&&visualSkills.Add(vfx.RenderedSkill))
                {
                    vfx.SetPreviewProgress(.44f);
                    if(vfx.ActiveRendererCount<1)throw new Exception("Skill keyframe has no visible renderers: "+vfx.CurrentSkill);
                    foreach(var renderer in game.Stage.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.transform.parent&&r.transform.parent.name.StartsWith("Skill VFX · ")))
                        if(!renderer.sharedMaterial||!renderer.sharedMaterial.shader.isSupported||(renderer.sharedMaterial.shader.name!="Dicebound/PaperStroke"&&renderer.sharedMaterial.shader.name!="Dicebound/PaintedSkill"))throw new Exception("Skill visual uses an unsupported shader.");
                    if(new[]{"cleave","balance","tide","moon","anchor","stitch"}.Contains(vfx.CurrentSkill)&&!game.Stage.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.transform.parent&&r.transform.parent.name.StartsWith("Skill VFX · ")&&r.sharedMaterial&&r.sharedMaterial.shader.name=="Dicebound/PaintedSkill"&&r.sharedMaterial.mainTexture&&r.sharedMaterial.mainTexture.name==vfx.CurrentSkill+"-painted-v1"))
                        throw new Exception("Principal skill has no painted effect atlas: "+vfx.CurrentSkill);
                    yield return new WaitForEndOfFrame();
                    string path=Path.Combine(output,"skill-"+vfx.CurrentSkill+".png");ScreenCapture.CaptureScreenshot(path);yield return AwaitScreenshot(path,Screen.width,Screen.height);
                    vfx.ReleasePreview();
                    if(!actionCameraInterrupted&&Mathf.Abs(game.Stage.Camera.orthographicSize-sizeBefore)>.1f)
                    {
                        string committed=TacticalCodec.Encode(game.State);
                        game.SetPresentationPaused(true);yield return null;
                        if(game.Busy||vfx.Active||game.Stage.UltimateCutIn.Active||TacticalCodec.Encode(game.State)!=committed||
                            !new TacticalStore(output).TryLoad(out var saved,out var error)||TacticalCodec.Encode(saved)!=committed)
                            throw new Exception("Interrupted action close-up altered its saved action or retained effects.");
                        actionCameraInterrupted=true;
                        Debug.Log("DICEBOUND_ACTION_CAMERA_INTERRUPTION restoredAfterLiveCast=true");
                        break;
                    }
                }
                if(acting&&(request.type=="move"||request.type=="skill"))
                {
                    // Use the original cast origin: victory feedback may reposition the whole party.
                    Vector3 direction=castDirection;
                    if(request.type=="move")
                    {
                        // At a corner, Travel may finish one segment and face the next in one frame.
                        // The planned path supplies an independent direction inside each segment.
                        direction=Vector3.zero;
                        for(int i=1;i<movePath.Length;i++)
                        {
                            var line=movePath[i]-movePath[i-1];if(line.sqrMagnitude<.000001f)continue;
                            float t=Vector3.Dot(acting.transform.position-movePath[i-1],line)/line.sqrMagnitude;
                            if(t>.01f&&t<.99f&&Vector3.Distance(acting.transform.position,movePath[i-1]+line*t)<.01f){direction=line;break;}
                        }
                    }
                    if(direction.sqrMagnitude>.000001f)
                    {
                        CheckProjectedFacing(acting,direction,"live "+request.type+" "+request.skillId);
                        if(request.type=="move")liveMoveFacingChecks++;else liveSkillFacingChecks++;
                    }
                    string imageKey="team"+journeys+"-action-"+request.type;
                    if(actionImages.Add(imageKey))ScreenCapture.CaptureScreenshot(Path.Combine(output,imageKey+".png"));
                }
                yield return null;
            }
            if(request.type=="skill"&&(Vector3.Distance(viewBefore,game.Stage.Camera.transform.position)>.01f||Mathf.Abs(sizeBefore-game.Stage.Camera.orthographicSize)>.001f))
                throw new Exception("Action close-up did not restore the player's camera.");
            if (game.State.revision != before + 1) throw new Exception("Live control did not commit exactly once: " + request.type + " / " + game.SaveError);
            bool checkpointLoaded=new TacticalStore(output).TryLoad(out var committedState,out var checkpointError);
            if(game.State.seed!=seedBefore||!checkpointLoaded||TacticalCodec.Encode(committedState)!=TacticalCodec.Encode(game.State))throw new Exception("Live action differs from its persisted immutable-seed checkpoint: "+request.type+" / "+checkpointError);
            if (!TacticalValidation.Valid(game.State)) throw new Exception("Invalid committed state after " + request.type);
            if(game.Stage.GroundViewActive||game.Stage.GetComponentsInChildren<TacticalActor>().Any(a=>a.gameObject.activeInHierarchy&&!a.BodyVisible))throw new Exception("Committed action retained hidden character bodies.");
            actions++;
            if(!uiSmoke&&!cinematicCleanupChecked&&request.type=="skill"&&unit!=null&&unit.heroId=="lingfeng"&&cinematicHeroes.Contains("lingfeng"))yield return VerifyCinematicCleanup(request,unit.heroId,origin,origin+castDirection,affected);
        }
        private IEnumerator VerifyCinematicCleanup(TacticalRequest request,string hero,Vector3 source,Vector3 target,Vector3[] affected)
        {
            // A finishing cast leaves the battlefield for the reward page, which hides the whole
            // effect world; probe on a later cast instead of sampling a hidden hierarchy.
            if(game.State.phase!="battle")yield break;
            string committed=TacticalCodec.Encode(game.State);
            var cutIn=game.Stage.UltimateCutIn;
            // Start the nested decoder as well as the overlay: the first outer MoveNext only
            // configures an invisible canvas now, so cancelling there does not exercise cleanup.
            foreach(bool duringPreparation in new[]{true,false})
            {
                var probe=new Stack<IEnumerator>();probe.Push(cutIn.Play("lingfeng",false));
                try
                {
                    if(duringPreparation)
                        yield return AdvanceCinematicProbe(probe,cutIn,()=>game.Stage.GetComponents<VideoPlayer>().Any(),"Cinematic preparation never created a decoder.");
                    else
                        yield return AdvanceCinematicToVisible(probe,cutIn,"lingfeng",false);
                    game.Stage.CancelEffects();
                }
                finally{CloseCinematicProbe(probe,cutIn);}
                yield return null;
                if(cutIn.Active||cutIn.IsPlayingActionMovie||game.Stage.SkillVfx.Active||cutIn.PreviewHeld||game.Stage.GetComponents<VideoPlayer>().Any())
                    throw new Exception("Interrupted cinematic left visible layers or a decoder (preparing="+duringPreparation+").");
            }
            float began=Time.realtimeSinceStartup;yield return cutIn.Play("lingfeng",true);
            if(cutIn.Active||Time.realtimeSinceStartup-began>.6f)throw new Exception("Reduced-motion cinematic did not finish promptly.");
            // Replay only the just-committed action's visual inputs, without applying its rules again.
            var vfx=game.Stage.SkillVfx;var effectProbe=vfx.Play(request.skillId,hero,source,target,affected,false);
            if(!effectProbe.MoveNext()||!vfx.Active)throw new Exception("Skill cancellation probe did not begin.");
            // The 0.18.2 cast rhythm staggers each archetype's particles; scan the progress curve
            // for its first visible renderer instead of assuming one fixed instant.
            float particleProgress=-1f;
            for(float progress=.05f;progress<=1.001f&&particleProgress<0;progress+=.05f)
            {
                vfx.SetPreviewProgress(progress);
                if(vfx.ActiveRendererCount>=1)particleProgress=progress;
            }
            if(particleProgress<0)throw new Exception("Skill cancellation probe has no visible particles. "+vfx.PieceSummary());
            game.Stage.CancelEffects();(effectProbe as IDisposable)?.Dispose();
            if(vfx.Active||vfx.ActiveRendererCount!=0||vfx.PreviewHeld)throw new Exception("Cancelled skill retained renderers.");
            began=Time.realtimeSinceStartup;yield return vfx.Play(request.skillId,hero,source,target,affected,true);
            if(vfx.Active||Time.realtimeSinceStartup-began>.6f)throw new Exception("Reduced-motion skill did not finish promptly.");
            if(TacticalCodec.Encode(game.State)!=committed||!new TacticalStore(output).TryLoad(out var saved,out var error)||TacticalCodec.Encode(saved)!=committed)throw new Exception("Cinematic cleanup altered the saved action.");
            cinematicCleanupChecked=true;
            yield return VerifyCinematicGallery();
            if(uiMatrix)yield return VerifyCinematicLetterbox();
        }
        private IEnumerator VerifyCinematicLetterbox()
        {
            int width=Screen.width,height=Screen.height;
            Screen.SetResolution(1280,540,FullScreenMode.Windowed);yield return null;yield return null;Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var baseline=ScreenCapture.CaptureScreenshotAsTexture();var original=baseline.GetPixels32();Destroy(baseline);
            var cutIn=game.Stage.UltimateCutIn;var playback=new Stack<IEnumerator>();playback.Push(cutIn.Play("lingfeng",game.ReducedMotion));
            try
            {
                yield return AdvanceCinematicToVisible(playback,cutIn,"lingfeng",game.ReducedMotion);
                foreach(float phase in new[]{.01f,.89f})
                {
                    yield return HoldCinematicFrame(playback,cutIn,phase);yield return new WaitForEndOfFrame();
                    AssertCinematicFrame(cutIn,"lingfeng",game.ReducedMotion);
                    var frame=ScreenCapture.CaptureScreenshotAsTexture();var pixels=frame.GetPixels32();Destroy(frame);
                    Rect safe=PaperViewport.Pixels;
                    for(int y=2;y<Screen.height-2;y+=3)for(int x=2;x<Screen.width-2;x+=3)
                    {
                        if(x>=safe.xMin-2&&x<=safe.xMax+2)continue;
                        int index=y*Screen.width+x;var a=original[index];var b=pixels[index];
                        if(Math.Abs(a.r-b.r)>2||Math.Abs(a.g-b.g)>2||Math.Abs(a.b-b.b)>2)
                            throw new Exception("Cinematic escaped the 16:9 content area at phase "+phase);
                    }
                    string path=Path.Combine(output,phase<.5f?"cinematic-wide-enter.png":"cinematic-wide-exit.png");
                    ScreenCapture.CaptureScreenshot(path);yield return AwaitScreenshot(path,Screen.width,Screen.height);
                }
            }
            finally{CloseCinematicProbe(playback,cutIn);Screen.SetResolution(width,height,FullScreenMode.Windowed);}
            yield return null;yield return null;
            Debug.Log("DICEBOUND_CINEMATIC_LETTERBOX verifiedEntryAndExit=true");
        }
        private IEnumerator VerifyCinematicGallery()
        {
            string committed=TacticalCodec.Encode(game.State);var cutIn=game.Stage.UltimateCutIn;
            foreach(var hero in TacticalContent.Heroes.Where(h=>h.id!="ruanzhuo"))
            {
                if(cinematicGallery.Contains(hero.id))continue;
                var preview=new Stack<IEnumerator>();preview.Push(cutIn.Play(hero.id,game.ReducedMotion));
                string name="cinematic-gallery-"+hero.id+".png",path=Path.Combine(output,name);
                try
                {
                    yield return AdvanceCinematicToVisible(preview,cutIn,hero.id,game.ReducedMotion);
                    yield return HoldCinematicFrame(preview,cutIn,.44f);yield return new WaitForEndOfFrame();
                    AssertCinematicFrame(cutIn,hero.id,game.ReducedMotion);
                    ScreenCapture.CaptureScreenshot(path);yield return AwaitScreenshot(path,Screen.width,Screen.height);
                }
                finally{CloseCinematicProbe(preview,cutIn);}
                cinematicGallery.Add(hero.id);
                if(uiSmoke)smokeCaptures.Add(name);
            }
            if(cutIn.Active||TacticalCodec.Encode(game.State)!=committed||!new TacticalStore(output).TryLoad(out var saved,out var error)||TacticalCodec.Encode(saved)!=committed)
                throw new Exception("Presentation-only cinematic gallery altered the saved journey.");
            Debug.Log("DICEBOUND_CINEMATIC_GALLERY presentationOnly=true dedicatedHeroes="+cinematicGallery.Count);
        }
        private IEnumerator AdvanceCinematicToVisible(Stack<IEnumerator> playback,TacticalUltimateCutIn cutIn,string hero,bool reduced)
        {
            bool movie=!reduced&&TacticalUltimateMovie.MovieFile(hero)!=null;
            yield return AdvanceCinematicProbe(playback,cutIn,()=>cutIn.Active&&cutIn.Progress>.04f&&cutIn.Progress<.75f&&
                (movie?cutIn.IsPlayingActionMovie:cutIn.IsShowingStillArtwork),"No visible cinematic frame for "+hero+".");
            AssertCinematicFrame(cutIn,hero,reduced);
        }
        private IEnumerator HoldCinematicFrame(Stack<IEnumerator> playback,TacticalUltimateCutIn cutIn,float progress)
        {
            cutIn.SetPreviewProgress(progress);
            // VideoPlayer advances independently. Resume the nested loop once so it observes
            // PreviewHeld and pauses the decoder before the screenshot waits for its frame.
            yield return AdvanceCinematicProbe(playback,cutIn,()=>cutIn.PreviewHeld&&!game.Stage.GetComponents<VideoPlayer>().Any(p=>p.isPlaying),"Cinematic preview did not pause its decoder.");
            cutIn.SetPreviewProgress(progress);
        }
        private IEnumerator AdvanceCinematicProbe(Stack<IEnumerator> playback,TacticalUltimateCutIn cutIn,Func<bool> ready,string message)
        {
            bool arrived=false;float deadline=Time.realtimeSinceStartup+6;
            try
            {
                while(playback.Count>0)
                {
                    if(Time.realtimeSinceStartup>=deadline)throw new Exception(message+" Timed out after 6 seconds.");
                    bool moved=playback.Peek().MoveNext();object current=moved?playback.Peek().Current:null;
                    if(!moved){(playback.Pop() as IDisposable)?.Dispose();continue;}
                    if(current is IEnumerator nested){playback.Push(nested);continue;}
                    if(ready()){arrived=true;yield break;}
                    yield return current;
                }
                throw new Exception(message+" Playback ended before the requested frame.");
            }
            finally{if(!arrived)CloseCinematicProbe(playback,cutIn);}
        }
        private static void AssertCinematicFrame(TacticalUltimateCutIn cutIn,string hero,bool reduced)
        {
            Canvas.ForceUpdateCanvases();
            if(!cutIn.Active||!cutIn.ShowsCompleteArtwork||!cutIn.UsingDedicatedArt||!cutIn.HasPaintedEffects||!cutIn.HasImpactEffects||cutIn.PortraitTexture!=TacticalMenuMotion.CompanionPoster(hero))
                throw new Exception("Dedicated cinematic artwork is missing or distorted: "+hero);
            string companionMovie=TacticalMenuMotion.CompanionMovieFile(hero);
            if(companionMovie!=null&&TacticalUltimateMovie.MovieFile(hero)!=companionMovie)
                throw new Exception("Ultimate must reuse the current selection portrait movie: "+hero);
            if(companionMovie==null&&(TacticalUltimateMovie.MovieFile(hero)!=null||!cutIn.PortraitTexture.name.EndsWith("-ultimate-v2")))
                throw new Exception("An archived companion must retain its authored still fallback: "+hero);
            if(reduced?cutIn.ImpactVertexCount!=0:cutIn.ImpactVertexCount<=0)
                throw new Exception("Cinematic impact geometry does not respect the selected motion mode: "+hero);
            bool movie=!reduced&&TacticalUltimateMovie.MovieFile(hero)!=null;
            if(movie?(!cutIn.IsPlayingActionMovie||cutIn.IsShowingStillArtwork):(!cutIn.IsShowingStillArtwork||cutIn.IsPlayingActionMovie))
                throw new Exception("Cinematic has no visible "+(movie?"video":"still")+" frame: "+hero);
            var picture=cutIn.GetComponentsInChildren<RawImage>(true).SingleOrDefault(i=>i.name==(movie?"Ultimate action movie":"Ultimate painted action and elemental backlight"));
            if(!picture||!picture.enabled||!picture.gameObject.activeInHierarchy||!picture.texture||picture.color.a<=.001f||picture.GetComponentsInParent<CanvasGroup>().Any(g=>g.alpha<=.001f))
                throw new Exception("Cinematic frame is hidden or transparent: "+hero);
            if(movie)
            {
                var decoder=cutIn.GetComponents<VideoPlayer>().SingleOrDefault(p=>p.targetTexture==picture.texture);
                bool packed=TacticalMenuMotion.CompanionUsesPackedMovie(hero);
                bool coverage=picture.material&&picture.material.shader&&picture.material.shader.name=="Dicebound/UI/MenuPackedVideo";
                if(!decoder||decoder.url.Replace('\\','/')!=TacticalMenuMotion.MoviePath(TacticalMenuMotion.CompanionMovieFile(hero)).Replace('\\','/')||packed!=coverage||
                    picture.uvRect!=new Rect(0,0,1,1)||Mathf.Abs(picture.rectTransform.rect.width/picture.rectTransform.rect.height-16f/9f)>.001f)
                    throw new Exception("Cinematic has a stale movie, incorrect coverage material, or incomplete portrait frame: "+hero);
            }
        }
        private static void CloseCinematicProbe(Stack<IEnumerator> playback,TacticalUltimateCutIn cutIn)
        {cutIn.Cancel();while(playback.Count>0)(playback.Pop() as IDisposable)?.Dispose();}
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.1f); Canvas.ForceUpdateCanvases();
            if(game.State?.phase=="battle"&&(game.Stage.AnimatedActorCount<3||game.Stage.ArchitecturalPartCount<500||game.Stage.SceneModelCount<1))throw new Exception("Incomplete tactical battlefield: animatedSprites="+game.Stage.AnimatedActorCount+" architecturalParts="+game.Stage.ArchitecturalPartCount+" renderers="+game.Stage.SceneModelCount);
            CheckText(name);
            if(game.State?.phase=="battle")CheckBattleHudBounds(name);
            if(uiSmoke)CheckPortraits(name);
            yield return new WaitForEndOfFrame();
            if(!battleArtChecked&&game.State?.phase=="battle"){VerifyBattleArt();battleArtChecked=true;}
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null;
            if(uiSmoke){yield return AwaitScreenshot(Path.Combine(output,name+".png"),Screen.width,Screen.height);smokeCaptures.Add(name+".png");}
            if(uiMatrix)
            {
                int originalWidth=Screen.width,originalHeight=Screen.height;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(1280,720),new Vector2Int(2560,1080)})
                {
                    // Windows clamps windows to the physical desktop. Preserve the target
                    // aspect on smaller displays and let Unity render a supersized screenshot.
                    int scale=size.x>Screen.currentResolution.width||size.y>Screen.currentResolution.height?2:1;
                    int width=size.x/scale,height=size.y/scale;
                    Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return null;yield return null;
                    if(Screen.width!=width||Screen.height!=height)throw new Exception("UI matrix resolution was not applied: "+size+" actual="+Screen.width+"x"+Screen.height);
                    Canvas.ForceUpdateCanvases();CheckText(name+" "+size);
                    string directory=Path.Combine(output,size.x+"x"+size.y);Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory,"capture-mode.txt"),"Target "+size.x+"x"+size.y+"; Player window "+width+"x"+height+"; Unity screenshot supersampling "+scale+"x.");
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"),scale);yield return null;matrixCaptures++;
                    if(uiSmoke)yield return AwaitScreenshot(Path.Combine(directory,name+".png"),size.x,size.y);
                }
                Screen.SetResolution(originalWidth,originalHeight,FullScreenMode.Windowed);yield return null;yield return null;
            }
        }
        private void VerifyBattleArt()
        {
            // The approved matte world / thin ink HUD replaces the rejected glass-panel target.
            // Check production assets, the live terrain and visible controls; a shader count is not art quality.
            foreach(string id in new[]{"yaojing-painted-stone-relief-v2","yaojing-painted-wood-v1"})
            {
                var texture=Resources.Load<Texture2D>("Tactics/Textures/"+id);
                if(!texture||texture.width<1024||texture.wrapMode!=TextureWrapMode.Repeat||texture.mipmapCount<2)throw new Exception("Painted surface is missing or incorrectly imported: "+id);
            }
            var tiles=game.Stage.GetComponentsInChildren<TacticalTileHit>();
            int width=TacticalRules.BoardWidth(game.State),height=TacticalRules.BoardHeight(game.State);
            // Raised walls add their own picking contour above the ground collider for the same cell.
            if(tiles.Any(t=>t.X<0||t.Y<0||t.X>=width||t.Y>=height)||tiles.Select(t=>t.Y*width+t.X).Distinct().Count()!=width*height)throw new Exception("Rendered ground does not cover the actual tactical map.");
            foreach(var renderer in game.Stage.Environment.GetComponentsInChildren<Renderer>())
                foreach(var material in renderer.sharedMaterials)if(!material||!material.shader||!material.shader.isSupported)throw new Exception("Unsupported battlefield material.");
            if(!game.Stage.Environment.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Any(m=>m.shader.name=="Dicebound/PainterlyStone"&&m.mainTexture&&m.mainTexture.name=="yaojing-painted-stone-relief-v2"))
                throw new Exception("New painted relief stone is not bound in the battlefield.");
            foreach(string name in new[]{"End round","Move mode","Enemy intentions","Terrain guide","Ground view"})if(!Control(name).isActiveAndEnabled)throw new Exception("Missing compact HUD command: "+name);
            Debug.Log("DICEBOUND_PAINTED_BATTLE_VERIFIED tiles="+tiles.Length+" textures=2 renderers="+game.Stage.SceneModelCount);
        }
        private IEnumerator AwaitScreenshot(string path,int width,int height)
        {
            for(int attempt=0;attempt<200;attempt++)
            {
                bool ready=false;
                try
                {
                    if(File.Exists(path)&&new FileInfo(path).Length>=45)
                    {
                        var header=new byte[24];var ending=new byte[12];int count,endCount;
                        using(var stream=File.OpenRead(path))
                        {count=stream.Read(header,0,header.Length);stream.Seek(-12,SeekOrigin.End);endCount=stream.Read(ending,0,ending.Length);}
                        if(count==24&&endCount==12&&header[0]==137&&header[1]==80&&header[2]==78&&header[3]==71&&
                            ending[0]==0&&ending[1]==0&&ending[2]==0&&ending[3]==0&&ending[4]==73&&ending[5]==69&&ending[6]==78&&ending[7]==68)
                        {
                            int actualWidth=(header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19];
                            int actualHeight=(header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23];
                            if(actualWidth!=width||actualHeight!=height)throw new Exception("UI smoke screenshot has wrong dimensions: "+path);
                            ready=true;
                        }
                    }
                }
                catch(IOException) { }
                if(ready)yield break;
                yield return new WaitForSecondsRealtime(.05f);
            }
            throw new Exception("UI smoke screenshot was not written: "+path);
        }
        private void CheckPortraits(string context)
        {
            foreach(var image in game.Canvas.GetComponentsInChildren<RawImage>().Where(i=>i.isActiveAndEnabled&&
                (i.name.StartsWith("Portrait ")||i.name.StartsWith("Original portrait ")||i.name=="Full companion illustration"||i.name=="Story companion"||i.name.StartsWith("Ally portrait ")||i.name.StartsWith("Ending portrait "))))
            {
                if(!image.texture||image.texture.width<1||image.texture.height<1||image.color.a<=0||image.canvasRenderer.cull||
                    image.uvRect.width<=0||image.uvRect.height<=0||image.uvRect.xMin<-.001f||image.uvRect.yMin<-.001f||image.uvRect.xMax>1.001f||image.uvRect.yMax>1.001f)
                    throw new Exception("Empty or invisible portrait: "+context+" / "+image.name);
                var mask=image.GetComponentInParent<Mask>();
                if(!image.maskable||!mask||!mask.isActiveAndEnabled||!mask.graphic||!mask.graphic.isActiveAndEnabled)
                    throw new Exception("Portrait lacks an active ancestor mask: "+context+" / "+image.name);
                var material=image.materialForRendering;
                if(!material||!material.HasProperty("_Stencil")||material.GetFloat("_Stencil")<1||
                    !material.HasProperty("_StencilComp")||material.GetFloat("_StencilComp")!=(float)UnityEngine.Rendering.CompareFunction.Equal)
                    throw new Exception("Portrait ancestor mask did not reach its rendered stencil material: "+image.name);
                var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);var bounds=(RectTransform)mask.transform;
                foreach(var point in corners)
                {
                    var local=bounds.InverseTransformPoint(point);
                    if(local.x<bounds.rect.xMin-.1f||local.x>bounds.rect.xMax+.1f||local.y<bounds.rect.yMin-.1f||local.y>bounds.rect.yMax+.1f)
                        throw new Exception("Portrait content escapes its rounded outer bounds: "+image.name);
                }
                portraitChecks++;
            }
        }
        private IEnumerator VerifyTacticalUi()
        {
            string archive=TacticalCodec.Encode(game.State);
            game.Title();game.CancelSelection();if(!Control("Continue tactics"))throw new Exception("Cancel unexpectedly left the title page.");
            game.SelectParty();game.CancelSelection();if(!Control("Roster sixuan"))throw new Exception("Right-click unexpectedly left party selection.");
            game.CancelSelection(true);if(!Control("Continue tactics"))throw new Exception("Escape did not return from party selection to the title.");game.Render();
            CheckPreviewHeight("battle idle");
            var preview=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Action preview");string resting=preview.text;
            var tile=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Tile preview");string restingTile=tile.text;
            var cell=game.State.terrain.First(t=>t.kind=="rubble"||t.kind=="plain");game.Hover(cell.x,cell.y);
            CheckPreviewHeight("cell hover");
            if(preview.text==resting)throw new Exception("Cell hover did not update its description.");
            if(!tile.text.StartsWith(TacticalDirector.GridName(cell.x,cell.y)+" · "))throw new Exception("Cell hover did not update the independent tile information.");
            game.HoverExit();if(preview.text!=resting)throw new Exception("Cell hover did not restore the current action preview.");
            if(tile.text!=restingTile)throw new Exception("Hover exit did not restore the selected companion's tile information.");
            CheckPreviewHeight("hover exit");
            Invoke("Enemy intentions");yield return Capture("02-enemy-intentions");Invoke("Close modal corner");
            if(game.Modal)throw new Exception("Modal corner close failed.");
            game.Notify("小队已有三人，请先移出一位。");yield return Capture("02-notice");
            var notice=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Tactical notice");
            var noticeBounds=(RectTransform)notice.transform.parent;
            if(notice.preferredHeight>notice.rectTransform.rect.height+.1f||noticeBounds.rect.width-notice.rectTransform.rect.width<47.9f)throw new Exception("Notice padding or wrapping failed.");
            // Presentation stress fixture is restored before any action/save; never modifies a checkpoint.
            var unit=game.State.units.First(u=>u.team=="hero");int hp=unit.hp,ap=unit.ap,sp=unit.sp,charge=unit.charge;
            var skillsBefore=unit.skills.ToList();var cooldownsBefore=unit.cooldowns.ToDictionary(p=>p.Key,p=>p.Value);
            try
            {
                unit.hp=1;unit.ap=unit.maxAp+1;unit.sp=unit.maxSp;unit.charge=100;
                foreach(var definition in TacticalContent.Skills.Where(s=>string.IsNullOrEmpty(s.heroId)))if(unit.skills.Count<7&&!unit.skills.Contains(definition.id))unit.skills.Add(definition.id);
                unit.cooldowns[unit.skills[0]]=2;game.Select(unit.id);yield return null;
                foreach(var definition in TacticalContent.Skills)
                {game.ChooseSkill(definition.id);CheckPreviewHeight("selected skill "+definition.id);Canvas.ForceUpdateCanvases();CheckText("skill description "+definition.id);}
                unit.ap=unit.sp=unit.charge=0;
                foreach(var definition in TacticalContent.Skills)
                {game.ChooseSkill(definition.id);CheckPreviewHeight("exhausted skill "+definition.id);Canvas.ForceUpdateCanvases();CheckText("insufficient resource description "+definition.id);}
                game.ChooseSkill(unit.skills[1]);game.CellPress(unit.x,unit.y);
                CheckPreviewHeight("invalid pending");
                if(Control("Commit action").interactable)throw new Exception("Exhausted action unexpectedly became committable.");
                Invoke("Cancel action");CheckPreviewHeight("cancelled action");
                unit.ap=unit.maxAp+1;unit.sp=unit.maxSp;unit.charge=100;
                game.Select(unit.id);
                var pips=game.Canvas.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("AP "+unit.id+" ")).ToArray();
                if(pips.Length!=Mathf.Max(unit.ap,unit.maxAp))throw new Exception("Bonus AP disappeared from the fixed resource region.");
                foreach(var pip in pips){var area=(RectTransform)pip.transform.parent;var corners=new Vector3[4];pip.rectTransform.GetWorldCorners(corners);foreach(var point in corners)if(!area.rect.Contains((Vector2)area.InverseTransformPoint(point)))throw new Exception("Resource pip overflowed the card.");}
                foreach(var id in unit.skills)
                {
                    var button=Control("Skill "+id);var rect=((RectTransform)button.transform).rect;
                    if(rect.width<80||rect.height<80||button.GetComponentsInChildren<Text>().All(t=>!t.text.Contains(TacticalContent.GetSkill(id).name)))throw new Exception("Compact skill lacks a readable name or usable hit area.");
                }
                // Since 0.19 a cooling skill stays clickable and answers with a notice + deny tone;
                // the contract to verify is the explanation, plus a rejected (uncommittable) action.
                game.ChooseSkill(unit.skills[0]);
                var cooldownNotice=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Tactical notice");
                if(!cooldownNotice.text.Contains("还需冷却"))throw new Exception("Cooling skill did not explain its cooldown: "+cooldownNotice.text);
                if(Control("Commit action").interactable)throw new Exception("Cooldown skill produced a committable action.");
                var name=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Ally name "+unit.id);
                if(name.color!=TacticalTheme.Danger)throw new Exception("Low-health name lost its warning color.");
                yield return Capture("02-resource-skill-stress");
            }
            finally
            {
                unit.hp=hp;unit.ap=ap;unit.sp=sp;unit.charge=charge;unit.skills.Clear();unit.skills.AddRange(skillsBefore);unit.cooldowns.Clear();foreach(var pair in cooldownsBefore)unit.cooldowns.Add(pair.Key,pair.Value);game.Render();
            }
            if(TacticalCodec.Encode(game.State)!=archive)throw new Exception("UI fixture changed the committed journey.");
            var end=Control("End round");var pointer=new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(end.gameObject,pointer,ExecuteEvents.pointerEnterHandler);yield return new WaitForSecondsRealtime(.15f);yield return Capture("02-button-hover");
            ExecuteEvents.Execute(end.gameObject,pointer,ExecuteEvents.pointerDownHandler);yield return new WaitForSecondsRealtime(.15f);yield return Capture("02-button-pressed");
            ExecuteEvents.Execute(end.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(end.gameObject,pointer,ExecuteEvents.pointerExitHandler);
        }
        private void CheckPreviewHeight(string context)
        {
            var panel=game.Canvas.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Tactical commands");
            var terrain=game.Canvas.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Tile information");
            if(Mathf.Abs(panel.rect.height-316)>.1f||Mathf.Abs(panel.anchoredPosition.y-138)>.1f)
                throw new Exception("Action preview layout "+context+": expected height 316 at y=138, got "+panel.rect.height+" at "+panel.anchoredPosition.y);
            if(Mathf.Abs(terrain.rect.height-222)>.1f||Mathf.Abs(terrain.anchoredPosition.y+143)>.1f)
                throw new Exception("Tile information layout "+context+": expected height 222 at y=-143, got "+terrain.rect.height+" at "+terrain.anchoredPosition.y);
            var actionText=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Action preview");
            var tileText=game.Canvas.GetComponentsInChildren<Text>().Single(t=>t.name=="Tile preview");
            if(!actionText.transform.IsChildOf(panel)||!tileText.transform.IsChildOf(terrain))
                throw new Exception("Action and tile information lost their independent panels: "+context);
            if(panel.GetComponentInChildren<ScrollRect>(true)||terrain.GetComponentInChildren<ScrollRect>(true))
                throw new Exception("Live hover previews must not require scrolling: "+context);
            if(Vector2.Distance(actionText.rectTransform.rect.size,new Vector2(244,250))>.1f||actionText.fontSize!=20||actionText.resizeTextForBestFit||
                Vector2.Distance(tileText.rectTransform.rect.size,new Vector2(244,160))>.1f||tileText.fontSize!=19||tileText.resizeTextForBestFit)
                throw new Exception("Live hover preview text lost its fixed readable dimensions or font size: "+context);
            using(var generator=new TextGenerator())
            {
                generator.Populate(tileText.text,tileText.GetGenerationSettings(tileText.rectTransform.rect.size));
                if(generator.lineCount>5)throw new Exception("Tile summary exceeds five visible lines: "+context+" / "+generator.lineCount);
            }
            var actionCorners=new Vector3[4];var terrainCorners=new Vector3[4];
            panel.GetWorldCorners(actionCorners);terrain.GetWorldCorners(terrainCorners);
            float actionBottom=actionCorners.Min(p=>panel.parent.InverseTransformPoint(p).y);
            float terrainTop=terrainCorners.Max(p=>panel.parent.InverseTransformPoint(p).y);
            if(actionBottom-terrainTop<11.9f)
                throw new Exception("Action and tile panels overlap or lose their 12-unit gap: "+context+" / "+(actionBottom-terrainTop));
        }
        private void CheckBattleHudBounds(string context)
        {
            foreach(var hero in game.State.units.Where(u=>u.team=="hero"))
            {
                var card=game.Canvas.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Ally "+hero.id);
                foreach(string prefix in new[]{"Ally name ","Ally health ","AP label ","SP label ","Charge amount "})
                {
                    var text=card.GetComponentsInChildren<Text>().Single(t=>t.name==prefix+hero.id);
                    var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                    foreach(var point in corners){var p=card.InverseTransformPoint(point);if(p.x<card.rect.xMin+7||p.x>card.rect.xMax-7||p.y<card.rect.yMin+7||p.y>card.rect.yMax-7)throw new Exception("Battle resource text touches or escapes its card: "+context+" / "+text.name);}
                }
                foreach(var image in card.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Health "+hero.id)||i.name.StartsWith("Charge "+hero.id)||i.name.StartsWith("AP "+hero.id+" ")||i.name.StartsWith("SP "+hero.id+" ")))
                {var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);foreach(var point in corners)if(!card.rect.Contains((Vector2)card.InverseTransformPoint(point)))throw new Exception("Battle meter/pip escapes its card: "+image.name);}
            }
            foreach(var frame in game.Canvas.GetComponentsInChildren<TacticalBattleFrame>().Where(f=>f.isActiveAndEnabled))
            {
                var surface=frame.GetComponent<Image>();var rim=frame.transform.Find("Authored metal rim").GetComponent<Image>();
                if(surface.color.a>.001f||!rim.sprite||!rim.fillCenter||rim.type==Image.Type.Tiled)throw new Exception("Battle field lost its authored material / transparent cut corners: "+frame.name);
                var parent=(RectTransform)frame.transform;
                if(Vector2.Distance(rim.rectTransform.rect.size,parent.rect.size)>.1f)throw new Exception("Battle border did not follow its resized field: "+frame.name);
            }
            foreach(string name in new[]{"Skill caption plate","Battle navigation plate","Throw source"})
            {
                var strip=game.Canvas.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.name==name&&t.gameObject.activeInHierarchy);
                if(strip&&strip.rect.height<44-.1f)throw new Exception("Battle toolbar caption is too narrow: "+name);
            }
            if(game.State.journeyMode=="ascent"&&game.State.chapter==2&&game.State.boardWidth==12&&game.State.boardHeight==10&&!game.Stage.Environment.PaintedActive)
                throw new Exception("The live ascent bridge did not activate its calibrated painted environment: "+game.Stage.Environment.PaintedResourceInfo);
        }
        private void CheckText(string name)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var narrative in game.Canvas.GetComponentsInChildren<PaperNarrativeText>().Where(n => n.isActiveAndEnabled))
                if (!narrative.ReflowNow() || narrative.LayoutError != null) throw new Exception("Narrative layout " + name + " / " + narrative.name + ": " + narrative.LayoutError);
            Canvas.ForceUpdateCanvases();
            foreach (var label in game.Canvas.GetComponentsInChildren<Text>().Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text) && t.name != "Tactical notice"))
                if (label.preferredHeight > label.rectTransform.rect.height + .1f) throw new Exception("Clipped tactical text " + name + " / " + label.name + ": " + label.text + " (" + label.preferredHeight + " > " + label.rectTransform.rect.height + ")");
        }
    }
}
