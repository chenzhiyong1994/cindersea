using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dicebound.Presentation
{
    // Visual presentation only: --dicebound-jade-preview --dicebound-save-dir "<fresh isolated folder>".
    // Captures real Player pages and both initial/progressed maps. No broad QA suites.
    // --menu-motion-preview captures only the three animated menus and their still fallback.
    // Add --route-art-preview to capture only the title and four route presentation states.
    public sealed class TacticalJadePreview : MonoBehaviour
    {
        private static string directory;
        private static bool menuMotionPreview;
        private static bool routeArtPreview;
        private TacticalDirector game;
        private TacticalState map, reward, bridge;
        private bool ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();
            menuMotionPreview=args.Contains("--menu-motion-preview");
            routeArtPreview=args.Contains("--route-art-preview");
            if(!args.Contains("--dicebound-jade-preview")&&!menuMotionPreview)return;
            int index=Array.IndexOf(args,"--dicebound-save-dir");
            if(index<0||index+1>=args.Length)throw new InvalidOperationException("Jade preview requires a fresh isolated save directory.");
            directory=Path.GetFullPath(args[index+1]).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string normal=Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(string.Equals(directory,normal,StringComparison.OrdinalIgnoreCase)||directory.StartsWith(normal+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
                File.Exists(Path.Combine(directory,"tactical-journey.json"))||args.Contains("--dicebound-tactical-verify")||args.Contains("--dicebound-ui-smoke")||args.Contains("--dicebound-bridge-study")||
                (menuMotionPreview&&(args.Contains("--dicebound-jade-preview")||routeArtPreview)))
                throw new InvalidOperationException("Use a fresh isolated folder and only the Jade visual preview flag.");
            Directory.CreateDirectory(directory);
            new GameObject("Jade visual preview").AddComponent<TacticalJadePreview>();
        }

        private IEnumerator Start()
        {
            yield return null;
            game=TacticalDirector.Instance;
            if(!game){Debug.LogError("Jade preview could not find the game.");yield break;}
            Application.runInBackground=true;
            game.GuardPreviewPreferences();
            if(menuMotionPreview){game.SetMenuMotionPreviewReducedMotion(false);yield return MenuMotionPreview();yield break;}
            var state=TacticalRules.NewAscent(71,new[]{"sixuan","lingfeng","cangling"});
            var initialMap=state.Clone();
            map=state.Clone();
            // Produce ordinary legal checkpoints in memory; this is scene preparation, not a test drive.
            for(int step=0;step<1000;step++)
            {
                if(state.phase=="map")map=state.Clone();
                if(state.phase=="reward"&&reward==null)reward=state.Clone();
                if(state.phase=="battle"&&state.chapter==2){bridge=state.Clone();break;}
                if(state.phase=="victory"||state.phase=="defeat")break;
                var request=TacticalRules.Suggest(state);
                if(state.phase=="map")
                {
                    var next=TacticalRules.AvailableNodes(state).OrderBy(n=>n.kind=="battle"?0:n.kind=="event"?1:n.kind=="shop"?2:3).ThenBy(n=>n.lane).FirstOrDefault();
                    if(next!=null)request=new TacticalRequest{type="node",choice=next.id};
                }
                if(request==null)break;
                state=TacticalRules.Act(state,request);
                if(step%32==0)yield return null;
            }
            if(reward==null||bridge==null){Debug.LogError("Jade preview could not prepare the requested scenes.");yield break;}
            if(routeArtPreview){yield return RouteArtPreview(initialMap);yield break;}
            string[] names={"01-title","02-party","03-route","04-battle","05-treasure","06-settings","07-route-start","08-route-locked"};
            for(int screen=0;screen<names.Length;screen++)
            {
                if(screen==7)game.ShowJadePreview(2,initialMap,initialMap.mapNodes.Last().id);
                else if(screen==6)game.ShowJadePreview(2,initialMap);
                else Show(screen==5?0:screen);
                if(screen==5)game.Settings();
                yield return new WaitForSecondsRealtime(.8f);
                game.Stage.enabled=false;game.HoverExit();
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,names[screen]+".png"));
                yield return new WaitForSecondsRealtime(.35f);
                game.Stage.enabled=true;
            }
            Show(0);ready=true;
            Debug.Log("Jade visual preview ready: F6 title / F7 party / F8 route / F9 battle / F10 treasure. Screenshots: "+directory);
        }

        private IEnumerator RouteArtPreview(TacticalState initialMap)
        {
            string[] names={"01-title","02-route-start","03-route-current","04-route-locked","05-route-shop"};
            string start=TacticalRules.AvailableNodes(initialMap).First().id;
            string current=TacticalRules.AvailableNodes(map).FirstOrDefault()?.id??map.currentNodeId;
            string boss=initialMap.mapNodes.First(n=>n.kind=="boss").id;
            string shop=initialMap.mapNodes.First(n=>n.kind=="shop").id;
            for(int screen=0;screen<names.Length;screen++)
            {
                if(screen==0)Show(0);
                else if(screen==1)game.ShowJadePreview(2,initialMap,start);
                else if(screen==2)game.ShowJadePreview(2,map,current);
                else game.ShowJadePreview(2,initialMap,screen==3?boss:shop);
                yield return new WaitForSecondsRealtime(.8f);
                yield return CapturePreview(names[screen]);
            }
            game.ShowJadePreview(2,map,current);ready=true;
            Debug.Log("Route art preview ready: F6 title / F7 party / F8 route / F9 battle / F10 treasure. Screenshots: "+directory);
        }

        [Serializable]
        private sealed class MotionPage
        {
            public string page,file;
            public bool prepared,hasFrame,playing,advanced,sameDecoder,samePosition,
                reducedMotionStopped,reducedMotionStill,leftPageStopped,loopContinues,presentationPaused,presentationResumed;
            public int loopCount;
            public double firstTime,secondTime,beforeRebuild,afterRebuild,loopTime,afterLoop,pausedTime,afterPause;
        }

        [Serializable]
        private sealed class MotionReport
        {
            public int width,height;
            public bool successful;
            public List<MotionPage> pages=new List<MotionPage>();
        }

        private IEnumerator MenuMotionPreview()
        {
            var state=TacticalRules.NewAscent(71,new[]{"sixuan","lingfeng","cangling"});
            map=state.Clone();
            // Reach just the first legal reward checkpoint; no combat presentation or regression suite.
            for(int step=0;step<1000&&state.phase!="reward";step++)
            {
                var request=TacticalRules.Suggest(state);
                if(state.phase=="map")
                {
                    var next=TacticalRules.AvailableNodes(state).OrderBy(n=>n.kind=="battle"?0:1).ThenBy(n=>n.lane).FirstOrDefault();
                    if(next!=null)request=new TacticalRequest{type="node",choice=next.id};
                }
                if(request==null||state.phase=="victory"||state.phase=="defeat")break;
                state=TacticalRules.Act(state,request);
                if(step%32==0)yield return null;
            }
            if(state.phase!="reward"){Debug.LogError("Menu motion preview could not prepare a reward page.");yield break;}
            reward=state.Clone();
            var report=new MotionReport{width=Screen.width,height=Screen.height};
            int[] screens={0,1,4};
            string[] names={"01-title","02-sixuan","03-treasure"};
            for(int i=0;i<screens.Length;i++)
            {
                game.SetMenuMotionPreviewReducedMotion(false);
                Show(screens[i]);
                // The native decoder can prepare asynchronously on the first launch.
                float deadline=Time.realtimeSinceStartup+22f;
                var motion=game.GetComponent<TacticalMenuMotion>();
                while(Time.realtimeSinceStartup<deadline&&motion&&(!motion.IsPrepared||!motion.HasFrame))yield return null;
                yield return new WaitForSecondsRealtime(.45f);
                var page=new MotionPage{page=names[i],file=motion?motion.CurrentFile:null,
                    prepared=motion&&motion.IsPrepared,hasFrame=motion&&motion.HasFrame,playing=motion&&motion.IsPlaying,
                    firstTime=motion?motion.Time:0};
                yield return CapturePreview(names[i]+"-a");
                yield return new WaitForSecondsRealtime(1.1f);
                page.secondTime=motion?motion.Time:0;
                page.advanced=page.playing&&Math.Abs(page.secondTime-page.firstTime)>.1d;
                yield return CapturePreview(names[i]+"-b");

                var decoder=game.GetComponent<VideoPlayer>();
                VideoPlayer.EventHandler looped=player=>page.loopCount++;
                try
                {
                    if(decoder)decoder.loopPointReached+=looped;
                    deadline=Time.realtimeSinceStartup+(decoder?(float)decoder.length:0f)+3f;
                    while(decoder&&page.loopCount==0&&Time.realtimeSinceStartup<deadline)yield return null;
                }
                finally { if(decoder)decoder.loopPointReached-=looped; }
                page.loopTime=motion?motion.Time:0;
                yield return new WaitForSecondsRealtime(.2f);
                page.afterLoop=motion?motion.Time:0;
                page.loopContinues=page.loopCount>0&&motion&&motion.HasFrame&&motion.IsPlaying&&Math.Abs(page.afterLoop-page.loopTime)>.03d;
                yield return CapturePreview(names[i]+"-loop");

                game.SetPresentationPaused(true);
                try
                {
                    page.pausedTime=motion?motion.Time:0;
                    yield return new WaitForSecondsRealtime(.3f);
                    page.afterPause=motion?motion.Time:0;
                    page.presentationPaused=motion&&!motion.IsPlaying&&Math.Abs(page.afterPause-page.pausedTime)<.1d;
                }
                finally { game.SetPresentationPaused(false); }
                yield return new WaitForSecondsRealtime(.15f);
                page.presentationResumed=motion&&motion.IsPlaying&&motion.HasFrame;

                page.beforeRebuild=motion?motion.Time:0;
                Show(screens[i]);
                page.afterRebuild=motion?motion.Time:0;
                page.sameDecoder=decoder&&decoder==game.GetComponent<VideoPlayer>();
                page.samePosition=page.prepared&&Math.Abs(page.afterRebuild-page.beforeRebuild)<.15d;
                yield return new WaitForEndOfFrame();

                game.SetMenuMotionPreviewReducedMotion(true);
                yield return new WaitForEndOfFrame();
                page.reducedMotionStopped=motion&&!motion.IsPlaying;
                page.reducedMotionStill=decoder&&decoder.targetTexture&&
                    !game.Canvas.GetComponentsInChildren<RawImage>().Any(image=>image.texture==decoder.targetTexture);
                yield return CapturePreview(names[i]+"-still");
                game.SetMenuMotionPreviewReducedMotion(false);
                Show(2);
                yield return new WaitForEndOfFrame();
                yield return null;
                page.leftPageStopped=motion&&!motion.IsPlaying&&string.IsNullOrEmpty(motion.CurrentFile)&&(!decoder||!decoder.isPlaying);
                report.pages.Add(page);
            }
            report.successful=report.pages.All(p=>p.prepared&&p.hasFrame&&p.playing&&p.advanced&&p.sameDecoder&&p.samePosition&&
                p.reducedMotionStopped&&p.reducedMotionStill&&p.leftPageStopped&&p.loopContinues&&p.presentationPaused&&p.presentationResumed);
            File.WriteAllText(Path.Combine(directory,"menu-motion-preview.json"),JsonUtility.ToJson(report,true));
            Show(0);ready=true;
            Debug.Log("DICEBOUND_MENU_MOTION_PREVIEW successful="+report.successful+" output="+directory);
            Debug.Log("Menu motion preview ready: F6 title / F7 Sixuan / F8 treasure / F9 toggle reduced motion. Uses isolated saves and does not write user preferences.");
        }

        private IEnumerator CapturePreview(string name)
        {
            game.Stage.enabled=false;game.HoverExit();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            yield return new WaitForSecondsRealtime(.25f);
            game.Stage.enabled=true;
        }

        private void Show(int screen)
        {
            var snapshot=screen==3?bridge:screen==4?reward:map;
            game.ShowJadePreview(screen,snapshot);
        }
        private void Update()
        {
            if(!ready||!game||game.Busy)return;
            if(menuMotionPreview)
            {
                if(Input.GetKeyDown(KeyCode.F6))Show(0);
                if(Input.GetKeyDown(KeyCode.F7))Show(1);
                if(Input.GetKeyDown(KeyCode.F8))Show(4);
                if(Input.GetKeyDown(KeyCode.F9))game.SetMenuMotionPreviewReducedMotion(!game.ReducedMotion);
                return;
            }
            for(int i=0;i<5;i++)if(Input.GetKeyDown(KeyCode.F6+i))Show(i);
        }
    }

    public sealed partial class TacticalDirector
    {
        internal void ShowJadePreview(int screen,TacticalState checkpoint,string inspectRoute=null)
        {
            var args=Environment.GetCommandLineArgs();
            if((!args.Contains("--dicebound-jade-preview")&&!args.Contains("--menu-motion-preview"))||Busy||checkpoint==null)return;
            CloseOverlay();State=checkpoint.Clone();
            selected=null;skill=null;pending=null;hovered=null;
            inspectedHero="sixuan";companionSkills=false;
            party.Clear();party.AddRange(new[]{"sixuan","lingfeng","cangling"});
            if(!store.TrySave(State,out var error)){SaveError=error;Notify(error);return;}
            SaveError=null;
            if(inspectRoute!=null){inspectedNode=inspectRoute;displayedMapFloor=State.floor;}
            if(screen==0)Title();else if(screen==1)SelectParty();else Render();
        }

        internal void SetMenuMotionPreviewReducedMotion(bool value)
        {
            if(!Environment.GetCommandLineArgs().Contains("--menu-motion-preview"))return;
            // Reuse the preference-write guard without creating TacticalVerification or touching PlayerPrefs.
            GuardPreviewPreferences();ReducedMotion=value;
        }

        internal void GuardPreviewPreferences()
        {
            var args=Environment.GetCommandLineArgs();
            if(!args.Contains("--dicebound-jade-preview")&&!args.Contains("--menu-motion-preview"))return;
            // Awake has already loaded the user's settings; only suppress later preference writes.
            qa=true;
        }
    }
}
