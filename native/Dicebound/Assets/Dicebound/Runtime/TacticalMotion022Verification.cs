using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicebound.Persistence;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dicebound.Presentation
{
    // Opt-in presentation checks only. No battle actions or full journey simulation.
    public sealed class TacticalMotion022Verification : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string name;
            public bool firstFrame, advanced, looped, reducedStill, cancelled, fallback, savedUnchanged;
            public bool movieEntry,movieExit,noStillFlash;
            public bool currentPortraitSource,correctVideoFormat,impactEffects,resourcesReleased;
            public int impactVertexCount;
            public bool uniformPortraitFrame,portraitGapClosed,completePreviewFrame;
            public float portraitPanelOverlap;
        }
        [Serializable] private sealed class Report
        {
            public string mode="motion022-focus";
            public bool complete,isolated=true,presentationOnly=true,fullJourneyVerification=false;
            public int width,height,errors;
            public List<Evidence> cases=new List<Evidence>();
            public List<string> captures=new List<string>();
        }
        private static string directory;
        private readonly Report report=new Report();
        private TacticalDirector game;
        private string failure,savedText,stateText;
        private bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();if(!args.Contains("--dicebound-motion022-verify"))return;
            int index=Array.IndexOf(args,"--dicebound-save-dir");
            if(index<0||index+1>=args.Length)throw new InvalidOperationException("Motion checks require an explicit fresh save directory.");
            directory=Path.GetFullPath(args[index+1]).TrimEnd('\\','/');
            string normal=Path.GetFullPath(Application.persistentDataPath).TrimEnd('\\','/');
            string[] conflicting={"--dicebound-tactical-verify","--dicebound-ui-smoke","--dicebound-experience-verify","--dicebound-combat-feedback-verify","--dicebound-jade-preview","--menu-motion-preview"};
            if(directory==Path.GetPathRoot(directory).TrimEnd('\\','/')||string.Equals(directory,normal,StringComparison.OrdinalIgnoreCase)||directory.StartsWith(normal+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
                File.Exists(Path.Combine(directory,"tactical-journey.json"))||File.Exists(Path.Combine(directory,"tactical-chronicle.json"))||conflicting.Any(args.Contains))
                throw new InvalidOperationException("Motion checks cannot share verification modes or use existing player archives.");
            Directory.CreateDirectory(directory);new GameObject("Motion 0.22 focused verification").AddComponent<TacticalMotion022Verification>();
        }
        private void OnEnable(){Application.logMessageReceived+=OnLog;}
        private void OnDisable(){Application.logMessageReceived-=OnLog;}
        private void OnLog(string message,string stack,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){report.errors++;failure=message+"\n"+stack;}}
        private void Update(){if(failure!=null&&!quitting)Finish(false);}
        private void Finish(bool success)
        {
            quitting=true;report.complete=success&&failure==null;
            if(failure!=null)File.WriteAllText(Path.Combine(directory,"motion022-failed.txt"),failure);
            File.WriteAllText(Path.Combine(directory,"motion022-verification.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.complete?0:1);
        }
        private IEnumerator Start()
        {
            yield return null;game=TacticalDirector.Instance;
            if(!game){failure="No live TacticalDirector.";yield break;}
            game.BeginMotion022Check();Application.runInBackground=true;
            report.width=Screen.width;report.height=Screen.height;
            savedText=File.ReadAllText(Path.Combine(directory,"tactical-journey.json"));stateText=TacticalCodec.Encode(game.State);
            var stack=new Stack<IEnumerator>();stack.Push(Verify());
            try
            {
                while(stack.Count>0&&failure==null)
                {
                    bool moved=false;object current=null;
                    try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                    catch(Exception e){failure=e.ToString();}
                    if(failure!=null)break;
                    if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                    if(current is IEnumerator nested)stack.Push(nested);else yield return current;
                }
            }
            finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();game.Stage.CancelEffects();}
            Finish(failure==null);
        }
        private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private void SaveUnchanged(Evidence evidence)
        {
            evidence.savedUnchanged=File.ReadAllText(Path.Combine(directory,"tactical-journey.json"))==savedText&&TacticalCodec.Encode(game.State)==stateText&&!File.Exists(Path.Combine(directory,"tactical-chronicle.json"));
            Require(evidence.savedUnchanged,evidence.name+": presentation changed saved progress.");
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();string file=name+".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,file));report.captures.Add(file);yield return null;
        }
        private IEnumerator Verify()
        {
            foreach(string hero in new[]{"sixuan","lingfeng","cangling","yanzhuying","shangshuo"})yield return Loop(hero,false);
            foreach(string hero in new[]{"sixuan","lingfeng","cangling","yanzhuying","shangshuo"})yield return Loop(hero,true);
            game.ShowMotion022Map();yield return null;yield return null;
            foreach(string hero in new[]{"sixuan","lingfeng","cangling","yanzhuying","shangshuo"})
            {
                yield return CutIn(hero,false,false);
                yield return CutIn(hero,false,true);
                yield return CutIn(hero,true,false);
            }
            yield return MissingMenuFallback();
            float deadline=Time.realtimeSinceStartup+8;
            while(report.captures.Any(f=>!File.Exists(Path.Combine(directory,f)))&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(report.captures.All(f=>File.Exists(Path.Combine(directory,f))),"A Player screenshot was not written.");
            Require(report.cases.Count==26,"Missing focused presentation cases.");
            Require(report.captures.Count==35&&report.captures.Distinct().Count()==35,"Missing or duplicate focused presentation captures.");
        }
        private static bool UsesCurrentPortrait(string hero,Texture poster,VideoPlayer decoder)
        {
            string file=TacticalMenuMotion.CompanionMovieFile(hero);
            return !string.IsNullOrEmpty(file)&&TacticalUltimateMovie.MovieFile(hero)==file&&poster==TacticalMenuMotion.CompanionPoster(hero)&&decoder&&
                decoder.url.Replace('\\','/')==TacticalMenuMotion.MoviePath(file).Replace('\\','/');
        }
        private static bool HasCorrectVideoFormat(string hero,RawImage picture,VideoPlayer decoder)
        {
            bool packed=TacticalMenuMotion.CompanionUsesPackedMovie(hero);
            bool coverage=picture.material&&picture.material.shader&&picture.material.shader.name=="Dicebound/UI/MenuPackedVideo";
            double colorHeight=decoder.height*(packed?.5:1);
            return packed==coverage&&decoder.width>0&&colorHeight>0&&Math.Abs(decoder.width/colorHeight-16d/9d)<.005&&
                decoder.audioOutputMode==VideoAudioOutputMode.None&&decoder.controlledAudioTrackCount==0;
        }
        private void CheckPortraitFraming(Evidence evidence,RawImage target,bool preview,Texture still)
        {
            Canvas.ForceUpdateCanvases();var rect=target.rectTransform;
            if(preview)
            {
                var uv=target.uvRect;
                float sourceAspect=(float)still.width/still.height,shownAspect=rect.rect.width/rect.rect.height;
                evidence.completePreviewFrame=Mathf.Abs(uv.x)<.001f&&Mathf.Abs(uv.y)<.001f&&Mathf.Abs(uv.width-1)<.001f&&Mathf.Abs(uv.height-1)<.001f&&Mathf.Abs(shownAspect-sourceAspect)<.003f;
                Require(evidence.completePreviewFrame,evidence.name+": full-frame preview cropped or stretched the complete source artwork.");return;
            }
            var clip=target.transform.parent as RectTransform;
            var data=game.Canvas.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Companion data");
            evidence.uniformPortraitFrame=clip&&Vector2.Distance(clip.anchoredPosition,new Vector2(-14,40))<.5f&&Vector2.Distance(clip.rect.size,new Vector2(760,920))<.5f&&Vector2.Distance(rect.rect.size,clip.rect.size)<.5f&&rect.anchoredPosition.sqrMagnitude<.25f;
            Require(evidence.uniformPortraitFrame,evidence.name+": portrait no longer fills the common 760 by 920 close-up frame.");
            Require(clip.parent==data.parent&&Vector2.Distance(data.anchoredPosition,new Vector2(639,40))<.5f&&Vector2.Distance(data.rect.size,new Vector2(604,920))<.5f,evidence.name+": portrait and detail panel must retain their shared layout coordinates.");
            evidence.portraitPanelOverlap=clip.anchoredPosition.x+clip.rect.xMax-(data.anchoredPosition.x+data.rect.xMin);
            evidence.portraitGapClosed=Mathf.Abs(evidence.portraitPanelOverlap-29)<.5f&&Mathf.Abs(clip.anchoredPosition.y+clip.rect.yMin-data.anchoredPosition.y-data.rect.yMin)<.5f&&Mathf.Abs(clip.anchoredPosition.y+clip.rect.yMax-data.anchoredPosition.y-data.rect.yMax)<.5f;
            Require(evidence.portraitGapClosed,evidence.name+": portrait and detail panel have a geometric gap or mismatched vertical bounds.");
        }
        private IEnumerator Loop(string hero,bool preview)
        {
            game.SetMotion022Reduced(false);game.ShowMotion022Hero(hero);
            var previewButton=game.Canvas.GetComponentsInChildren<Button>().SingleOrDefault(b=>b.name=="Companion motion preview");
            Require(previewButton&&previewButton.interactable,hero+": no accessible full-frame appreciation entry.");
            if(preview)previewButton.onClick.Invoke();
            var target=game.Canvas.GetComponentsInChildren<RawImage>().Single(i=>i.name==(preview?"Companion motion preview":"Full companion illustration"));
            var motion=preview?target.GetComponentInParent<TacticalMotionPreview>().GetComponent<TacticalMenuMotion>():game.GetComponent<TacticalMenuMotion>();
            Texture still=target.texture;var evidence=new Evidence{name=(preview?"preview-":"portrait-")+hero};report.cases.Add(evidence);
            Require(still,evidence.name+": missing authored portrait poster.");CheckPortraitFraming(evidence,target,preview,still);
            float deadline=Time.realtimeSinceStartup+8;
            while(motion&&!motion.IsDisplayingMovie&&!motion.Failed&&Time.realtimeSinceStartup<deadline)yield return null;
            evidence.firstFrame=motion&&motion.IsPrepared&&motion.HasFrame&&motion.IsDisplayingMovie&&motion.IsPlaying;
            Require(evidence.firstFrame,evidence.name+": no displayed video frame within 8 seconds.");
            CheckPortraitFraming(evidence,target,preview,still);
            double first=motion.Time;yield return new WaitForSecondsRealtime(.3f);
            evidence.advanced=Math.Abs(motion.Time-first)>.03;
            Require(evidence.advanced,evidence.name+": decoded time did not advance.");
            yield return Capture(evidence.name+"-movie");
            var decoder=motion.GetComponents<VideoPlayer>().Single(p=>p.targetTexture==target.texture);int loops=0;
            evidence.currentPortraitSource=motion.CurrentFile==TacticalMenuMotion.CompanionMovieFile(hero)&&UsesCurrentPortrait(hero,still,decoder);
            evidence.correctVideoFormat=HasCorrectVideoFormat(hero,target,decoder);
            Require(evidence.currentPortraitSource,evidence.name+": movie and poster do not use the current companion source.");
            Require(evidence.correctVideoFormat,evidence.name+": portrait movie has the wrong coverage format, aspect or audio mode.");
            var decodedSurface=target.texture as RenderTexture;
            var ownedPreviewMaterial=preview&&TacticalMenuMotion.CompanionUsesPackedMovie(hero)?target.material:null;
            VideoPlayer.EventHandler looped=p=>loops++;
            decoder.loopPointReached+=looped;
            try{deadline=Time.realtimeSinceStartup+8;while(loops==0&&Time.realtimeSinceStartup<deadline)yield return null;}
            finally{if(decoder)decoder.loopPointReached-=looped;}
            evidence.looped=loops>0&&decoder&&decoder.isLooping&&motion.IsPlaying;
            Require(evidence.looped,evidence.name+": loop did not continue within 8 seconds.");
            game.SetMotion022Reduced(true);yield return null;yield return null;
            evidence.reducedStill=motion&&!motion.IsPlaying&&!motion.IsDisplayingMovie&&target.texture==still;
            Require(evidence.reducedStill,evidence.name+": reduced motion did not restore the authored still.");
            CheckPortraitFraming(evidence,target,preview,still);
            yield return Capture(evidence.name+"-still");
            game.SetMotion022Reduced(false);
            if(preview)game.CloseOverlay();else game.ShowMotion022Map();
            yield return null;yield return null;
            evidence.cancelled=(!motion||!motion.IsBound&&!motion.IsPlaying&&motion.CurrentFile==null)&&!decoder;
            Require(evidence.cancelled,evidence.name+": leaving the page leaked the movie decoder.");SaveUnchanged(evidence);
            evidence.resourcesReleased=!decodedSurface&&!ownedPreviewMaterial;
            Require(evidence.resourcesReleased,evidence.name+": leaving the page leaked the decoded render texture or preview material.");
        }
        private IEnumerator CutIn(string hero,bool reduced,bool cancel)
        {
            game.SetMotion022Reduced(reduced);
            var cutIn=game.Stage.UltimateCutIn;
            var evidence=new Evidence{name="ultimate-"+hero+(reduced?"-reduced":cancel?"-cancel":"-complete")};report.cases.Add(evidence);
            Require(!string.IsNullOrEmpty(TacticalUltimateMovie.MovieFile(hero))&&TacticalUltimateMovie.MovieFile(hero)==TacticalMenuMotion.CompanionMovieFile(hero),evidence.name+": every current companion must reuse its selection movie for an ultimate.");
            Require(Mathf.Abs(TacticalUltimateMovie.MovieDuration(hero)-1.6f)<.001f,evidence.name+": the portrait presentation must use the shared 1.6-second playback span.");
            bool observedStill=false,observedMovie=false,requestedCancel=false,captured=false;
            bool observedMovieEntry=false,observedMovieExit=false;
            RenderTexture decodedSurface=null;Material decodedMaterial=null;RawImage moviePicture=null;VideoPlayer decoder=null;
            var stack=new Stack<IEnumerator>();stack.Push(cutIn.Play(hero,reduced));float deadline=Time.realtimeSinceStartup+8;
            try
            {
                while(stack.Count>0)
                {
                    Require(Time.realtimeSinceStartup<deadline,evidence.name+": cut-in exceeded the 8-second bound.");
                    bool moved=stack.Peek().MoveNext();object current=moved?stack.Peek().Current:null;
                    observedStill|=cutIn.IsShowingStillArtwork;observedMovie|=cutIn.IsPlayingActionMovie;
                    if(cutIn.IsPlayingActionMovie&&!moviePicture)
                    {
                        moviePicture=cutIn.GetComponentsInChildren<RawImage>(true).Single(i=>i.name=="Ultimate action movie");
                        decoder=cutIn.GetComponents<VideoPlayer>().Single(p=>p.targetTexture==moviePicture.texture);
                        decodedSurface=moviePicture.texture as RenderTexture;
                        if(TacticalMenuMotion.CompanionUsesPackedMovie(hero))decodedMaterial=moviePicture.material;
                        evidence.currentPortraitSource=UsesCurrentPortrait(hero,cutIn.PortraitTexture,decoder);
                        evidence.correctVideoFormat=HasCorrectVideoFormat(hero,moviePicture,decoder);
                        Require(evidence.currentPortraitSource,evidence.name+": ultimate did not play the current selection portrait source.");
                        Require(evidence.correctVideoFormat,evidence.name+": ultimate has the wrong coverage format, aspect or audio mode.");
                        Require(!decoder.isLooping,evidence.name+": the committed ultimate must remain finite.");
                    }
                    observedMovieEntry|=cutIn.IsPlayingActionMovie&&cutIn.Progress>0&&cutIn.Progress<.18f&&decoder&&decoder.isPlaying;
                    observedMovieExit|=cutIn.IsPlayingActionMovie&&cutIn.Progress>.79f&&cutIn.Progress<1&&decoder&&!decoder.isPlaying;
                    bool captureFrame=reduced?cutIn.Progress>.08f&&cutIn.Progress<.75f:cutIn.IsPlayingActionMovie&&cutIn.Progress>.25f;
                    if(!captured&&cutIn.Active&&captureFrame)
                    {
                        Canvas.ForceUpdateCanvases();
                        evidence.impactEffects=cutIn.HasImpactEffects;
                        Require(evidence.impactEffects,evidence.name+": cinematic has no live foreground/background impact layers.");
                        evidence.impactVertexCount=cutIn.ImpactVertexCount;
                        Require(reduced?evidence.impactVertexCount==0:evidence.impactVertexCount>0,evidence.name+": impact geometry must be visible in normal playback and absent with reduced motion.");
                        captured=true;yield return Capture(evidence.name);
                    }
                    if(cancel&&cutIn.IsPlayingActionMovie&&cutIn.Progress>.25f&&!requestedCancel){requestedCancel=true;cutIn.Cancel();}
                    if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                    if(current is IEnumerator nested)stack.Push(nested);else yield return current;
                }
            }
            finally{bool interrupted=stack.Count>0;while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();if(interrupted)cutIn.Cancel();}
            yield return null;yield return null;
            evidence.firstFrame=observedMovie;evidence.reducedStill=reduced&&observedStill&&!observedMovie;
            evidence.cancelled=!cutIn.Active&&!cutIn.IsPlayingActionMovie&&!game.GetComponents<VideoPlayer>().Any()&&!cutIn.GetComponents<VideoPlayer>().Any()&&!decoder;
            evidence.resourcesReleased=!decodedSurface&&!decodedMaterial&&(!moviePicture||!moviePicture.texture);
            if(reduced)evidence.currentPortraitSource=cutIn.PortraitTexture==TacticalMenuMotion.CompanionPoster(hero);
            evidence.movieEntry=observedMovieEntry;evidence.movieExit=observedMovieExit;evidence.noStillFlash=!observedStill;
            Require(cutIn.ShowsCompleteArtwork,evidence.name+": authored fallback illustration lost its original aspect ratio.");
            Require(reduced?evidence.reducedStill:evidence.firstFrame,evidence.name+": wrong video/static presentation path.");
            Require(evidence.currentPortraitSource&&evidence.resourcesReleased,evidence.name+": stale portrait source or unreleased movie resources.");
            if(!reduced)
                Require(evidence.noStillFlash&&evidence.movieEntry&&(cancel||evidence.movieExit),evidence.name+": video must enter directly and retain its frame through the exit without a still-art flash.");
            Require(evidence.cancelled&&(!cancel||requestedCancel),evidence.name+": cancellation or natural cleanup was incomplete.");
            Require(!game.Busy,evidence.name+": presentation left input locked.");SaveUnchanged(evidence);
        }
        private IEnumerator MissingMenuFallback()
        {
            game.SetMotion022Reduced(false);
            var target=Ui.Picture("Missing movie fallback probe",game.Canvas.transform,Texture2D.whiteTexture,Vector2.one*.5f,Vector2.zero,new Vector2(80,80),Color.white);
            var motion=target.gameObject.AddComponent<TacticalMenuMotion>();var evidence=new Evidence{name="missing-menu-fallback"};report.cases.Add(evidence);
            try
            {
                motion.Bind(target,"motion022-intentionally-missing.mp4",packedColorAspect:16f/9f);yield return null;
                evidence.fallback=motion.Failed&&!motion.IsPlaying&&!motion.IsDisplayingMovie&&target.texture==Texture2D.whiteTexture;
                Require(evidence.fallback,"Missing menu clip did not retain its authored still.");SaveUnchanged(evidence);
            }
            finally{motion.Dispose();Destroy(target.gameObject);}
        }
    }

    public sealed partial class TacticalDirector
    {
        private static void RequireMotion022Flag()
        {if(!Environment.GetCommandLineArgs().Contains("--dicebound-motion022-verify"))throw new InvalidOperationException("Motion-only helper requires its explicit verification flag.");}
        internal void BeginMotion022Check()
        {RequireMotion022Flag();qa=true;ReducedMotion=false;StartRun(71,new[]{"sixuan","lingfeng","shangshuo"},2);if(State==null||SaveError!=null)throw new InvalidOperationException("Cannot save isolated motion checkpoint: "+SaveError);}
        internal void SetMotion022Reduced(bool value){RequireMotion022Flag();ReducedMotion=value;}
        internal void ShowMotion022Hero(string id){RequireMotion022Flag();InspectCompanion(id);}
        internal void ShowMotion022Map(){RequireMotion022Flag();Render();}
    }
}
