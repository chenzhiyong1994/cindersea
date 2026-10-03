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
    /// <summary>Focused recorded-music checks in the actual Player; journey preparation runs only in memory.</summary>
    public sealed class TacticalBgmVerification : MonoBehaviour
    {
        private const string Flag="--dicebound-bgm-verify";
        [Serializable] private sealed class ActionTrace {public uint seed;public string policy;public List<string> actions=new List<string>();}
        [Serializable] private sealed class CaseEvidence
        {
            public string name,key,title,clip,checkpointFile,traceFile;
            public int preparationActions,channels,frequency;
            public long audioSamples;
            public float audioPeak,clipSeconds,beforeRebuild,afterRebuild;
            public bool decoded,savedStateMatches,recorded,streaming,stereo,looping,nonSpatial,sameSource,positionContinues,loopWrapped;
        }
        [Serializable] private sealed class Report
        {
            public string mode="bgm-focus",scope="Recorded music routing and playback; legal journey preparation runs in memory. No full Player journey, combat or visual regression.",
                fallbackScope="Unknown theme key exercises missing-entry fallback; corrupt files are not injected.";
            public bool complete,isolated=true,legalCheckpoints=true,fullJourneyVerification=false,preparationOnlyInMemory=true,
                musicZero,paused,resumed,ducked,duckRecovered,unknownFallback,recordedRestored;
            public int width,height,errors,recordedClips,winningPreparationActions,losingPreparationActions;
            public float mutePeak,duckBefore,duckMinimum,duckAfter,pauseBefore,pauseAfter,resumeAfter,fallbackPeak;
            public List<CaseEvidence> cases=new List<CaseEvidence>();
        }
        private sealed class Fixture {public TacticalState state;public int actions;public string trace;}
        private static string directory;
        private static bool authorized;
        private TacticalDirector game;
        private AudioMeter meter;
        private string failure;
        private readonly Report report=new Report();
        private readonly Dictionary<string,Fixture> fixtures=new Dictionary<string,Fixture>();
        private readonly HashSet<string> loopChecked=new HashSet<string>();
        private static readonly string[] FixtureNames={"map","shop","event","battle","elite","summit-ordinary","boss","reward","victory","defeat"};

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();if(!args.Contains(Flag))return;
            if(args.Contains("-nographics")||args.Any(a=>a!=Flag&&a.StartsWith("--dicebound-",StringComparison.Ordinal)&&
                (a.Contains("verify")||a.Contains("preview")||a.Contains("smoke")||a.Contains("study")||a.Contains("matrix")))||args.Contains("--menu-motion-preview"))
                throw new InvalidOperationException("Use only the focused BGM verification flag and an audio-capable Player.");
            int index=Array.IndexOf(args,"--dicebound-save-dir");
            if(index<0||index+1>=args.Length)throw new InvalidOperationException("BGM verification requires an explicit fresh save directory.");
            directory=Path.GetFullPath(args[index+1]).TrimEnd('\\','/');
            string normal=Path.GetFullPath(Application.persistentDataPath).TrimEnd('\\','/');
            if(string.Equals(directory,normal,StringComparison.OrdinalIgnoreCase)||directory.StartsWith(normal+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
                Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any(p=>Path.GetFileName(p)!="build-identity.json"&&Path.GetFileName(p)!="Player.log"))
                throw new InvalidOperationException("BGM verification cannot use normal player archives or a previously used directory.");
            Directory.CreateDirectory(directory);authorized=true;
            new GameObject("Focused BGM verification").AddComponent<TacticalBgmVerification>();
        }
        internal static bool AuthorizedDirectory(string path)=>authorized&&Environment.GetCommandLineArgs().Contains(Flag)&&
            string.Equals(Path.GetFullPath(path).TrimEnd('\\','/'),directory,StringComparison.OrdinalIgnoreCase);
        private void OnEnable(){Application.logMessageReceived+=OnLog;}
        private void OnDisable(){Application.logMessageReceived-=OnLog;AudioListener.pause=false;}
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){report.errors++;failure=message+"\n"+stack;}}
        private void Update()
        {
            if(failure==null)return;
            File.WriteAllText(Path.Combine(directory,"bgm-failed.txt"),failure);
            File.WriteAllText(Path.Combine(directory,"bgm-verification.json"),JsonUtility.ToJson(report,true));Application.Quit(1);
        }
        private IEnumerator Start()
        {
            yield return null;game=TacticalDirector.Instance;
            if(!game)throw new InvalidOperationException("BGM verification has no live director.");
            game.PrepareBgmVerification();
            meter=FindFirstObjectByType<AudioListener>().gameObject.AddComponent<AudioMeter>();
            yield return Guarded(Verify());
        }
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                bool moved=false;object current=null;
                try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception e){failure=e.ToString();Debug.LogException(e);}
                if(failure!=null){AudioListener.pause=false;yield break;}
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
        }
        private IEnumerator Verify()
        {
            report.width=Screen.width;report.height=Screen.height;report.recordedClips=game.Audio.RecordedClipCount;
            Require(report.recordedClips==7,"All seven recorded tracks must load.");
            yield return PrepareFixtures();
            game.Title();yield return CheckCase("title","title",null);
            game.SelectParty();yield return CheckCase("party","title",null);
            foreach(string name in FixtureNames)
            {
                var fixture=fixtures[name];game.LoadBgmCheckpoint(fixture.state);
                string key=name=="map"||name=="shop"||name=="event"?"travel":name=="reward"?"victory":name=="summit-ordinary"?"battle":name;
                yield return CheckCase(name,key,fixture);
                if(name=="battle")yield return CheckControls();
            }
            Require(loopChecked.SetEquals(TacticalMusic.Keys),"Each recorded track must cross its loop boundary.");
            report.complete=true;File.WriteAllText(Path.Combine(directory,"bgm-verification.json"),JsonUtility.ToJson(report,true));
            Debug.Log("DICEBOUND_BGM_VERIFIED scope=recorded-music-only cases="+report.cases.Count+" output="+directory);Application.Quit(0);
        }
        private IEnumerator CheckCase(string name,string key,Fixture fixture)
        {
            yield return new WaitForSecondsRealtime(1.6f);
            float loadingDeadline=Time.realtimeSinceStartup+8;
            while(!MusicSources().Any(s=>s.isPlaying&&s.clip&&s.clip.name==key)&&Time.realtimeSinceStartup<loadingDeadline)yield return null;
            Require(game.Audio.ThemeKey==key&&game.Audio.UsesRecordedMusic&&game.Audio.ThemeTitle==TacticalMusic.TitleFor(key),name+": actual track routing/title differs.");
            var source=Source(key);
            var evidence=new CaseEvidence{name=name,key=key,title=game.Audio.ThemeTitle,clip=source.clip.name,channels=source.clip.channels,
                frequency=source.clip.frequency,clipSeconds=source.clip.length,recorded=game.Audio.UsesRecordedMusic,streaming=source.clip.loadType==AudioClipLoadType.Streaming,
                stereo=source.clip.channels==2,looping=source.loop,nonSpatial=source.spatialBlend==0&&!source.ignoreListenerPause};
            report.cases.Add(evidence);
            Require(evidence.streaming&&evidence.stereo&&evidence.looping&&evidence.nonSpatial,name+": import or AudioSource settings differ.");
            Require(MusicSources().Where(s=>s!=source).All(s=>!s.isPlaying||s.volume<.0001f),name+": outgoing recorded track did not fade out.");
            if(fixture!=null)
            {
                string encoded=TacticalCodec.Encode(game.State);
                evidence.decoded=TacticalCodec.TryDecode(encoded,out var decoded,out var error)&&TacticalCodec.Encode(decoded)==encoded;
                evidence.savedStateMatches=new TacticalStore(directory).TryLoad(out var saved,out error)&&TacticalCodec.Encode(saved)==encoded;
                Require(evidence.decoded&&evidence.savedStateMatches,name+": legal saved checkpoint differs.");
                evidence.checkpointFile="checkpoint-"+name+".json";evidence.traceFile=fixture.trace;evidence.preparationActions=fixture.actions;
                File.WriteAllText(Path.Combine(directory,evidence.checkpointFile),encoded);
            }
            ResetMeter();float deadline=Time.realtimeSinceStartup+8;
            while((meter.Samples==0||meter.Peak<.0001f)&&Time.realtimeSinceStartup<deadline)yield return null;
            evidence.audioSamples=meter.Samples;evidence.audioPeak=meter.Peak;
            Require(evidence.audioSamples>0&&evidence.audioPeak>=.0001f,name+": solo BGM produced no actual audio output.");
            evidence.beforeRebuild=Position(source);Rebuild(name);yield return new WaitForSecondsRealtime(.25f);
            evidence.afterRebuild=Position(source);evidence.sameSource=Source(key)==source;
            float advanced=Forward(evidence.beforeRebuild,evidence.afterRebuild,source.clip.length);
            evidence.positionContinues=advanced>.05f&&advanced<.8f;
            Require(evidence.sameSource&&evidence.positionContinues,name+": rebuilding the page restarted its music.");
            if(loopChecked.Add(key))
            {
                Require(source.clip.length>2,key+": track is too short for the loop check.");
                source.timeSamples=Math.Max(0,source.clip.samples-(int)(source.clip.frequency*.35f));
                yield return new WaitForSecondsRealtime(.85f);
                evidence.loopWrapped=source.isPlaying&&Position(source)<1.5f&&source.clip.name==key;
                Require(evidence.loopWrapped,key+": recorded track did not continue through its loop boundary.");
            }
        }
        private IEnumerator CheckControls()
        {
            var source=Source("battle");report.duckBefore=source.volume;game.Audio.Effects=.7f;
            game.Audio.PlayTactical("heavy","lingfeng");yield return new WaitForSecondsRealtime(.10f);
            report.duckMinimum=source.volume;game.Audio.Effects=0;report.ducked=report.duckMinimum<report.duckBefore*.9f;
            yield return new WaitForSecondsRealtime(.8f);report.duckAfter=source.volume;report.duckRecovered=report.duckAfter>=report.duckBefore*.98f;
            Require(report.ducked&&report.duckRecovered,"Recorded BGM did not duck and recover around the battle cue.");
            game.Audio.CancelTactical();game.Audio.Music=0;yield return new WaitForSecondsRealtime(.2f);ResetMeter();
            yield return new WaitForSecondsRealtime(.3f);report.mutePeak=meter.Peak;
            report.musicZero=meter.Samples>0&&report.mutePeak<.0001f&&MusicSources().All(s=>s.volume==0);
            Require(report.musicZero,"Music zero left an audible BGM signal.");game.Audio.Music=.38f;
            yield return new WaitForSecondsRealtime(.2f);AudioListener.pause=true;report.pauseBefore=Position(source);
            yield return new WaitForSecondsRealtime(.35f);report.pauseAfter=Position(source);report.paused=Mathf.Abs(report.pauseAfter-report.pauseBefore)<.06f;
            AudioListener.pause=false;yield return new WaitForSecondsRealtime(.3f);report.resumeAfter=Position(source);
            report.resumed=source.isPlaying&&Forward(report.pauseAfter,report.resumeAfter,source.clip.length)>.1f;
            Require(report.paused&&report.resumed,"Listener pause/resume restarted or advanced a paused recorded track.");
            game.Audio.SetTheme("__missing-bgm-verification__");yield return new WaitForSecondsRealtime(1.6f);ResetMeter();
            yield return new WaitForSecondsRealtime(.6f);report.fallbackPeak=meter.Peak;
            report.unknownFallback=!game.Audio.UsesRecordedMusic&&!string.IsNullOrEmpty(game.Audio.ThemeTitle)&&meter.Samples>0&&meter.Peak>.0001f;
            Require(report.unknownFallback,"Unknown music entry did not produce the original-score fallback.");
            game.Render();yield return new WaitForSecondsRealtime(1.6f);
            report.recordedRestored=game.Audio.ThemeKey=="battle"&&game.Audio.UsesRecordedMusic&&Source("battle").isPlaying;
            Require(report.recordedRestored,"Recorded music did not recover after fallback.");
        }
        private AudioSource[] MusicSources()=>game.Audio.GetComponentsInChildren<AudioSource>().Where(s=>s.name.StartsWith("Wuxia music ",StringComparison.Ordinal)).ToArray();
        private AudioSource Source(string key)
        {
            var sources=MusicSources();Require(sources.Length==2,"Expected exactly two recorded music channels.");
            var source=sources.SingleOrDefault(s=>s.isPlaying&&s.clip&&s.clip.name==key);
            Require(source!=null,"Expected playing recorded resource: "+key);return source;
        }
        private void ResetMeter(){meter.Peak=0;System.Threading.Interlocked.Exchange(ref meter.Samples,0);}
        private static float Position(AudioSource source)=>source.timeSamples/(float)source.clip.frequency;
        private void Rebuild(string name){if(name=="title")game.Title();else if(name=="party")game.SelectParty();else game.Render();}
        private static float Forward(float before,float after,float length)=>after>=before?after-before:length-before+after;
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}

        private IEnumerator PrepareFixtures()
        {
            foreach(uint seed in new uint[]{71,123})
            {
                var state=TacticalRules.NewAscent(seed,new[]{"sixuan","lingfeng","cangling"});
                var path=new List<string>();
                if(!FindPath(state,state.mapNodes.Where(n=>n.floor==0),0,path))continue;
                var prepared=new Dictionary<string,Fixture>();var trace=new ActionTrace{seed=seed,policy="winning route with shop, event, elite, summit ordinary and boss"};
                for(int step=0;step<1700;step++)
                {
                    string name=state.phase=="battle"?(state.nodeKind=="boss"?"boss":state.nodeKind=="elite"?"elite":state.floor==10?"summit-ordinary":"battle"):state.phase;
                    if(FixtureNames.Contains(name)&&name!="defeat"&&!prepared.ContainsKey(name))prepared[name]=new Fixture{state=state.Clone(),actions=trace.actions.Count,trace="winning-legal-actions.json"};
                    if(state.phase=="victory"||state.phase=="defeat")break;
                    var request=state.phase=="map"?new TacticalRequest{type="node",choice=path[state.floor+1]}:TacticalRules.Suggest(state);
                    Advance(ref state,request,trace);if(step%32==0)yield return null;
                }
                if(state.phase!="victory"||FixtureNames.Where(n=>n!="defeat").Any(n=>!prepared.ContainsKey(n)))continue;
                foreach(var pair in prepared)fixtures[pair.Key]=pair.Value;
                report.winningPreparationActions=trace.actions.Count;File.WriteAllText(Path.Combine(directory,"winning-legal-actions.json"),JsonUtility.ToJson(trace,true));break;
            }
            Require(fixtures.ContainsKey("victory"),"Known preparation seeds did not reach all legal music checkpoints.");
            var losing=TacticalRules.NewAscent(71,new[]{"sixuan","lingfeng","cangling"});
            var lossTrace=new ActionTrace{seed=71,policy="enter first ordinary battle and legally end turns without attacking"};
            Advance(ref losing,new TacticalRequest{type="node",choice=TacticalRules.AvailableNodes(losing).First(n=>n.kind=="battle").id},lossTrace);
            for(int step=0;step<1700&&losing.phase=="battle";step++)
            {Advance(ref losing,new TacticalRequest{type="endTurn"},lossTrace);if(step%32==0)yield return null;}
            Require(losing.phase=="defeat","Legal end-turn preparation did not reach defeat.");
            fixtures["defeat"]=new Fixture{state=losing.Clone(),actions=lossTrace.actions.Count,trace="losing-legal-actions.json"};
            report.losingPreparationActions=lossTrace.actions.Count;File.WriteAllText(Path.Combine(directory,"losing-legal-actions.json"),JsonUtility.ToJson(lossTrace,true));
        }
        private static bool FindPath(TacticalState state,IEnumerable<TacticalMapNode> candidates,int mask,List<string> path)
        {
            foreach(var node in candidates)
            {
                if(node.floor==10&&node.kind!="battle")continue;
                int nextMask=mask|(node.kind=="shop"?1:node.kind=="event"?2:node.kind=="elite"?4:0);path.Add(node.id);
                if(node.kind=="boss"&&nextMask==7)return true;
                if(node.next.Count>0&&FindPath(state,node.next.Select(id=>state.mapNodes.First(n=>n.id==id)),nextMask,path))return true;
                path.RemoveAt(path.Count-1);
            }
            return false;
        }
        private static void Advance(ref TacticalState state,TacticalRequest request,ActionTrace trace)
        {
            Require(request!=null&&TacticalRules.Preview(state,request).ok,"Illegal music checkpoint preparation action.");
            trace.actions.Add(state.revision+":"+state.phase+":"+request.type+":"+request.unitId+":"+request.skillId+":"+request.choice+":"+request.x+","+request.y);
            state=TacticalRules.Act(state,request);
        }
    }

    public sealed partial class TacticalDirector
    {
        internal void PrepareBgmVerification()
        {
            if(!TacticalBgmVerification.AuthorizedDirectory(SaveDirectory))throw new InvalidOperationException("BGM verification requires its guarded fresh directory.");
            qa=true;ReducedMotion=true;Application.runInBackground=true;AudioListener.pause=false;
            Audio.Music=.38f;Audio.Effects=0;Audio.Ambience=0;Stage.enabled=false;
        }
        internal void LoadBgmCheckpoint(TacticalState checkpoint)
        {
            if(!TacticalBgmVerification.AuthorizedDirectory(SaveDirectory)||Busy||checkpoint==null)throw new InvalidOperationException("Invalid BGM verification checkpoint request.");
            qa=true;var next=checkpoint.Clone();
            if(!TacticalCodec.TryDecode(TacticalCodec.Encode(next),out var decoded,out var error)||TacticalCodec.Encode(decoded)!=TacticalCodec.Encode(next))
                throw new InvalidDataException("BGM checkpoint is not a valid round-trippable ascent state: "+error);
            if(!store.TrySave(next,out error))throw new IOException("BGM checkpoint was not saved: "+error);
            CloseOverlay();State=next;SaveError=null;selected=null;skill=null;pending=null;hovered=null;Render();
        }
    }
}
