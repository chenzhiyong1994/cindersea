using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicebound.Persistence;
using Dicebound.Tactics;
using UnityEngine;

namespace Dicebound.Presentation
{
    /// <summary>
    /// An explicitly isolated, playable art-production slice. Its checkpoint is obtained
    /// through ordinary rules actions, never by replacing battle terrain or unit stats.
    /// It cannot write to the player's normal archive directory.
    /// </summary>
    public sealed class TacticalBridgeStudy : MonoBehaviour
    {
        [Serializable] private sealed class Preparation
        {
            public string purpose = "playable-bridge-art-study";
            public uint seed = 71;
            public bool legalRulesProgression = true;
            public string[] party = { "sixuan", "lingfeng", "cangling" };
            public int actions, chapter, revision;
            public List<string> trace = new List<string>();
        }

        private static string directory;
        private TacticalDirector game;
        private bool failed;

        [Serializable] private sealed class Verification
        {
            public bool complete, isolated = true, legalCheckpoint = true;
            public bool moveCommittedExactlyOnce, savedStateMatches, hoverDoesNotCommit, clickLocksTarget, supportPreview, damagePreview, invalidPreview;
            public int errors, width, height, legalCellHitChecks, liveActions;
            public float medianFrameMs, p95FrameMs;
            public string skill, sceneResource;
            public bool paintedSceneActive, liveDepthCopy;
            public int calibratedLandingCells, battleIcons;
            public bool cameraBounds, cameraReset, cameraDoesNotCommit;
            public int panCases, zoomCases;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Prepare()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--dicebound-bridge-study")) return;
            int index = Array.IndexOf(args, "--dicebound-save-dir");
            if (index < 0 || index + 1 >= args.Length)
                throw new InvalidOperationException("Bridge study requires an explicit, isolated save directory.");
            directory = Path.GetFullPath(args[index + 1]);
            string normal = Path.GetFullPath(Application.persistentDataPath);
            if (string.Equals(directory.TrimEnd('\\', '/'), normal.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bridge study cannot use the normal player archive.");
            Directory.CreateDirectory(directory);
            var store = new TacticalStore(directory);
            if (!File.Exists(Path.Combine(directory, "tactical-journey.json")))
            {
                var report = new Preparation();
                var state = TacticalRules.NewRun(report.seed, report.party);
                for (int i = 0; i < 500 && !(state.chapter == 2 && state.phase == "battle" && !state.sideBattle); i++)
                {
                    if (state.phase == "victory" || state.phase == "defeat")
                        throw new InvalidOperationException("Bridge study progression ended before the bridge.");
                    var request = TacticalRules.Suggest(state);
                    if (state.phase == "route")
                    {
                        var route = state.routes.FirstOrDefault(r => r.kind == "camp") ?? state.routes.FirstOrDefault(r => r.kind == "event");
                        if (route != null) request = new TacticalRequest { type = "route", choice = route.id };
                    }
                    if (request == null || !TacticalRules.Preview(state, request).ok)
                        throw new InvalidOperationException("Bridge study has no legal progression action.");
                    report.trace.Add(state.revision + ":" + state.phase + ":" + request.type + ":" + request.unitId + ":" + request.skillId + ":" + request.choice + ":" + request.x + "," + request.y);
                    state = TacticalRules.Act(state, request);
                    report.actions++;
                }
                if (state.chapter != 2 || state.phase != "battle" || state.sideBattle)
                    throw new InvalidOperationException("Bridge study did not reach the required legal checkpoint.");
                if (!store.TrySave(state, out var error)) throw new IOException(error);
                report.chapter = state.chapter; report.revision = state.revision;
                File.WriteAllText(Path.Combine(directory, "bridge-preparation.json"), JsonUtility.ToJson(report, true));
            }
            if (!store.TryLoad(out _, out var loadError)) throw new InvalidDataException(loadError);
            new GameObject("Playable bridge art study").AddComponent<TacticalBridgeStudy>();
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void Update()
        {
            if (failed && (Environment.GetCommandLineArgs().Contains("--dicebound-bridge-verify") || Environment.GetCommandLineArgs().Contains("--dicebound-art-guide"))) Application.Quit(1);
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            failed = true;
            if (directory != null) File.WriteAllText(Path.Combine(directory, "bridge-failed.txt"), message + "\n" + stack);
        }

        private IEnumerator Start()
        {
            yield return null;
            game = TacticalDirector.Instance;
            if (!game || game.State == null) throw new InvalidOperationException("Bridge study could not load its prepared checkpoint.");
            Application.runInBackground = true;
            game.Render();
            yield return new WaitForSecondsRealtime(.8f);
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--dicebound-art-guide"))
            {
                // Filled by the scene's production exporter, kept separate from gameplay QA.
                yield return ExportArtGuide();
                Application.Quit(failed ? 1 : 0);
                yield break;
            }
            game.Stage.enabled = false; game.HoverExit();
            yield return Capture("bridge-playable");
            game.Stage.enabled = true;
            if (args.Contains("--dicebound-bridge-verify"))
            {
                yield return Verify();
                Application.Quit(failed ? 1 : 0);
            }
        }

        private IEnumerator ExportArtGuide()
        {
            // The exporter is installed by the painted environment production module.
            var exporters = game.GetComponents<MonoBehaviour>().OfType<ITacticalArtGuideExporter>().ToArray();
            if (exporters.Length != 1) throw new InvalidOperationException("Expected one scene art-guide exporter.");
            yield return exporters[0].ExportGuide(game.Stage, game.State, directory);
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(directory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 120 && !File.Exists(path); i++) yield return null;
            if (!File.Exists(path)) throw new IOException("Bridge screenshot was not written: " + path);
        }

        private IEnumerator Verify()
        {
            var report = new Verification { width = Screen.width, height = Screen.height };
            report.paintedSceneActive = game.Stage.Environment.PaintedActive;
            report.sceneResource = game.Stage.Environment.PaintedResourceInfo;
            report.calibratedLandingCells = game.Stage.Environment.PaintedScene.DepthValidatedCells;
            report.battleIcons = TacticalArt.BattleIconCount;
            if (!report.paintedSceneActive || report.calibratedLandingCells != 106 || report.battleIcons != 20)
                throw new InvalidOperationException("Bridge production assets are not active: " + report.sceneResource);
            var timing = new List<float>();
            for (int i = 0; i < 180; i++) { yield return null; timing.Add(Time.unscaledDeltaTime * 1000); }
            timing.Sort(); report.medianFrameMs = timing[timing.Count / 2]; report.p95FrameMs = timing[Mathf.FloorToInt((timing.Count - 1) * .95f)];
            report.liveDepthCopy = TacticalRangeRendererFeature.LastDepthCopiedFrame >= Time.frameCount-2;
            if (!report.liveDepthCopy) throw new InvalidOperationException("Tactical ranges did not copy current rendered scene depth.");
            yield return VerifyCameraBounds(report);
            var state = game.State;
            int revision = state.revision;
            var ally = state.units.First(u => u.team == "hero" && u.hp > 0);
            // Physical mouse position must not overwrite a controlled hover while a QA
            // screenshot is being written. The ordinary live callbacks below are unchanged.
            game.Stage.enabled = false;
            game.Select(ally.id);
            var legal = TacticalRules.MoveCells(state, ally.id);
            if (game.Stage.RangeSnapshot.legal.Count != legal.Count) throw new InvalidOperationException("Displayed move range differs from rules.");
            foreach (var cell in legal)
            {
                var point = game.Stage.TargetScreenPoint(cell.x, cell.y);
                if (!game.Stage.PointCell(point, out int hx, out int hy) || hx != cell.x || hy != cell.y)
                    throw new InvalidOperationException("Painted scene cell picking disagrees with rules at " + cell.x + "," + cell.y);
                report.legalCellHitChecks++;
            }
            var destination = legal.OrderBy(c => state.units.Where(u => u.team == "enemy" && u.hp > 0).Min(e => Math.Abs(e.x-c.x)+Math.Abs(e.y-c.y))).First();
            game.Hover(destination.x, destination.y);
            yield return Capture("bridge-move-hover");
            if (game.State.revision != revision) throw new InvalidOperationException("Hover committed an action.");
            report.hoverDoesNotCommit = true;
            game.CellPress(destination.x, destination.y);
            game.Hover(ally.x, ally.y);
            if (!game.Stage.RangeSnapshot.locked || game.PendingTarget == null || game.PendingTarget.x != destination.x || game.PendingTarget.y != destination.y || game.HoveredTarget != null)
                throw new InvalidOperationException("Hover changed the clicked target.");
            report.clickLocksTarget = true;
            yield return Capture("bridge-move-locked");
            if (game.State.revision != revision) throw new InvalidOperationException("Target selection committed an action.");
            game.CancelSelection(); game.Select(ally.id); game.Hover(-1, -1);
            if (game.Stage.RangeSnapshot.semantic != TacticalRangeSemantic.Invalid || string.IsNullOrEmpty(game.Stage.RangeSnapshot.preview?.reason))
                throw new InvalidOperationException("Forbidden target has no invalid semantic/reason.");
            report.invalidPreview = true;
            game.Commit();
            if (game.State.revision != revision) throw new InvalidOperationException("Uncommitted invalid preview changed state.");
            var gap = state.terrain.First(c => c.kind == "gap");
            game.Hover(gap.x, gap.y); yield return Capture("bridge-invalid-target");
            game.CancelSelection(); game.Select(ally.id); game.ChooseSkill("brace");
            var support = TacticalRules.SkillTargets(state, ally.id, "brace").First();
            game.Hover(support.x, support.y);
            if (game.Stage.RangeSnapshot.semantic != TacticalRangeSemantic.Support) throw new InvalidOperationException("Guard preview is not support-coloured.");
            report.supportPreview = true;
            yield return Capture("bridge-support-hover");
            game.CancelSelection(); game.Select(ally.id); game.CellPress(destination.x, destination.y);
            game.Stage.enabled = true;
            game.Commit();
            while (game.Busy) yield return null;
            if (game.State.revision != revision + 1) throw new InvalidOperationException("Move did not settle exactly once.");
            report.liveActions++; report.moveCommittedExactlyOnce = true;
            if (!new TacticalStore(directory).TryLoad(out var saved, out var error) || TacticalCodec.Encode(saved) != TacticalCodec.Encode(game.State))
                throw new InvalidOperationException("Bridge archive differs from committed state: " + error);
            report.savedStateMatches = true;
            yield return Capture("bridge-after-move");
            for (int i = 0; i < 40 && game.State.phase == "battle" && !report.damagePreview; i++)
            {
                var request = TacticalRules.Suggest(game.State);
                if (request == null) throw new InvalidOperationException("Bridge has no legal continuation.");
                bool damage = request.type == "skill" && TacticalRules.Preview(game.State, request).damage > 0;
                if (request.type == "move" || request.type == "skill")
                {
                    game.Stage.enabled = false;
                    game.Select(request.unitId);
                    if (request.type == "skill") game.ChooseSkill(request.skillId);
                    game.Hover(request.x, request.y);
                    if (damage)
                    {
                        var snapshot = game.Stage.RangeSnapshot;
                        if (snapshot.semantic != TacticalRangeSemantic.Damage || !snapshot.preview.ok || snapshot.affected.Count != TacticalRules.Preview(game.State, request).affected.Count)
                            throw new InvalidOperationException("Skill hover does not show the exact rules impact.");
                        report.damagePreview = true; report.skill = request.skillId;
                        yield return Capture("bridge-damage-hover");
                    }
                    game.CellPress(request.x, request.y);
                    game.Stage.enabled = true;
                    game.Commit();
                }
                else game.Request(request);
                bool effectCaptured = false;
                while (game.Busy)
                {
                    var vfx = game.Stage.SkillVfx;
                    if (damage && !effectCaptured && vfx.Active && vfx.Progress >= .32f)
                    { yield return Capture("bridge-live-skill"); effectCaptured = true; }
                    yield return null;
                }
                report.liveActions++;
            }
            if (!report.damagePreview) throw new InvalidOperationException("Bridge study did not exercise a legal damaging skill.");
            if (!new TacticalStore(directory).TryLoad(out saved, out error) || TacticalCodec.Encode(saved) != TacticalCodec.Encode(game.State))
                throw new InvalidOperationException("Skill checkpoint differs from the displayed state: " + error);
            yield return Capture("bridge-after-skill");
            report.complete = true; report.errors = failed ? 1 : 0;
            File.WriteAllText(Path.Combine(directory, "bridge-verification.json"), JsonUtility.ToJson(report, true));
        }

        private IEnumerator VerifyCameraBounds(Verification report)
        {
            var stage=game.Stage;var camera=stage.Camera;bool wasEnabled=stage.enabled;
            string before=TacticalCodec.Encode(game.State);
            stage.enabled=false;game.HoverExit();stage.ResetView();
            Vector3 homePosition=camera.transform.position;float homeSize=camera.orthographicSize;
            try
            {
                AssertCameraFrame();
                stage.ZoomView(10000);
                AssertCameraFrame();
                float closeSize=camera.orthographicSize;
                if(closeSize>=homeSize-.001f)throw new InvalidOperationException("Bridge zoom-in did not change the real camera.");
                report.zoomCases++;
                yield return Capture("bridge-zoom-in");
                stage.ZoomView(-10000);AssertCameraFrame();
                if(camera.orthographicSize<=closeSize+.001f)throw new InvalidOperationException("Bridge zoom-out did not change the real camera.");
                report.zoomCases++;
                foreach(var delta in new[]{new Vector2(100000,0),new Vector2(-100000,0),new Vector2(0,100000),new Vector2(0,-100000)})
                {
                    stage.ResetView();stage.ZoomView(-10000);
                    Vector3 start=camera.transform.position;
                    stage.PanView(delta);AssertCameraFrame();
                    Vector3 boundary=camera.transform.position;
                    if(Vector3.Distance(start,boundary)<.001f)throw new InvalidOperationException("Bridge pan did not move the real camera.");
                    yield return null;
                    // Recompute through the same Stage entry used by ordinary input.
                    // A late-only camera clamp with stale focus would jump here.
                    stage.ZoomView(0);AssertCameraFrame();
                    if(Vector3.Distance(boundary,camera.transform.position)>.002f)throw new InvalidOperationException("Bridge camera drifted after its boundary clamp.");
                    stage.PanView(-delta.normalized*10);AssertCameraFrame();
                    if(Vector3.Distance(boundary,camera.transform.position)<.001f)throw new InvalidOperationException("Bridge camera could not immediately reverse from its pan boundary.");
                    report.panCases++;
                }
                yield return Capture("bridge-pan-limit");
                stage.ResetView();AssertCameraFrame();
                if(Vector3.Distance(homePosition,camera.transform.position)>.002f||Mathf.Abs(homeSize-camera.orthographicSize)>.002f)
                    throw new InvalidOperationException("Bridge camera reset did not restore its real home view.");
                report.cameraReset=true;
                yield return Capture("bridge-camera-reset");
                if(TacticalCodec.Encode(game.State)!=before)throw new InvalidOperationException("Bridge camera controls changed the tactical checkpoint.");
                report.cameraDoesNotCommit=true;report.cameraBounds=true;
            }
            finally{stage.ResetView();stage.enabled=wasEnabled;}
        }
        private void AssertCameraFrame()
        {
            var painted=game.Stage.Environment.PaintedScene;var projection=painted.Projection;var camera=game.Stage.Camera;
            if(!painted.Active||projection==null||!camera.orthographic||Vector3.Dot(camera.transform.forward,projection.cameraForward)<.9999f||Vector3.Dot(camera.transform.up,projection.cameraUp)<.9999f)
                throw new InvalidOperationException("Bridge camera lost its calibrated painted projection.");
            float size=camera.orthographicSize;
            if(float.IsNaN(size)||float.IsInfinity(size)||size<=0)throw new InvalidOperationException("Bridge camera zoom is not finite and positive.");
            Vector3 offset=camera.transform.position-projection.cameraPosition;
            float x=Vector3.Dot(offset,projection.cameraRight),y=Vector3.Dot(offset,projection.cameraUp),halfWidth=size*camera.aspect;
            Rect frame=painted.AvailableFrame;
            const float tolerance=.002f;
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||x-halfWidth<frame.xMin-tolerance||x+halfWidth>frame.xMax+tolerance||y-size<frame.yMin-tolerance||y+size>frame.yMax+tolerance)
                throw new InvalidOperationException("Bridge camera footprint exposed the outside of its painted frame.");
        }
    }

    public interface ITacticalArtGuideExporter
    {
        IEnumerator ExportGuide(TacticalStage stage, TacticalState state, string directory);
    }
}
