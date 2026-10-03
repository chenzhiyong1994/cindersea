using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dicebound.Presentation
{
    public sealed class Soundscape:MonoBehaviour
    {
        [Serializable] public class Score {public Theme[] themes;}
        [Serializable] public class Theme {public string key,title;public float bpm;public Beat[] beats;}
        [Serializable] public class Beat {public Note[] notes;}
        [Serializable] public class Note {public string instrument;public int midi;public float beats,gain,pan;}
        private class Voice {public AudioSource source;public double start,end;public float gain,attack,release;}
        private class EffectVoice {public AudioSource source;public double end;public float gain;public bool tactical;}
        private readonly List<Voice> voices=new List<Voice>();
        private readonly List<EffectVoice> effectVoices=new List<EffectVoice>();
        private readonly Dictionary<string,AudioClip> samples=new Dictionary<string,AudioClip>();
        private readonly Dictionary<string,AudioClip> effects=new Dictionary<string,AudioClip>();
        private readonly Dictionary<string,AudioClip[]> tacticalEffects=new Dictionary<string,AudioClip[]>();
        private readonly Dictionary<string,double> tacticalCueTimes=new Dictionary<string,double>();
        private readonly Dictionary<string,int> lastVariants=new Dictionary<string,int>();
        private Score score;private Theme theme;private int beat;private double next;private AudioSource ambience;
        private readonly Dictionary<string,AudioClip> musicClips=new Dictionary<string,AudioClip>();
        private readonly AudioSource[] musicSources=new AudioSource[2];
        private readonly float[] musicGains=new float[2];
        private int activeMusic=-1;
        private string themeKey;
        private const float MusicFadeSeconds=1.2f;
        private float musicDuck=1,duckStrength=1;private double duckUntil;
        public float Music=.38f,Effects=.70f,Ambience=.28f;
        public string ThemeKey => themeKey;
        public string ThemeTitle => UsesRecordedMusic?TacticalMusic.TitleFor(themeKey):theme==null?"":theme.title;
        public bool UsesRecordedMusic => activeMusic>=0&&musicSources[activeMusic]&&musicSources[activeMusic].clip;
        public int RecordedClipCount => musicClips.Count;
        public int TacticalClipCount {get;private set;}
        public void Build()
        {
            score=JsonUtility.FromJson<Score>(Resources.Load<TextAsset>("Audio/jinhai-score").text);
            foreach(string name in new[]{"piano-49","piano-61","piano-73","piano-85","cello-43","cello-53","cello-64","strings-62","strings-72","strings-79","harp-62","harp-72","harp-83"})samples[name]=Resources.Load<AudioClip>("Audio/"+name);
            samples["bell-74"]=BronzeBell();
            for(int i=0;i<48;i++){var source=new GameObject("Chamber voice "+i).AddComponent<AudioSource>();source.transform.SetParent(transform);source.playOnAwake=false;voices.Add(new Voice{source=source});}
            for(int i=0;i<20;i++){var source=new GameObject("Combat and interface voice "+i).AddComponent<AudioSource>();source.transform.SetParent(transform);source.playOnAwake=false;source.spatialBlend=0;source.priority=80;source.dopplerLevel=0;effectVoices.Add(new EffectVoice{source=source});}
            LoadTacticalEffects();
            foreach(string name in new[]{"paper","hit","shield","magic","select","deny","heal","cleave","balance","tide","oath"})effects[name]=Synthesize(name);
            effects["talent"]=samples["bell-74"];
            foreach(string key in TacticalMusic.Keys)
            {
                var clip=Resources.Load<AudioClip>("Audio/Music/"+key);
                if(clip)musicClips[key]=clip;
            }
            for(int i=0;i<musicSources.Length;i++)
            {
                var source=new GameObject("Wuxia music "+i).AddComponent<AudioSource>();source.transform.SetParent(transform);
                source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.priority=128;source.dopplerLevel=0;source.volume=0;
                musicSources[i]=source;
            }
            ambience=new GameObject("Rain beyond the windows").AddComponent<AudioSource>();ambience.transform.SetParent(transform);ambience.clip=Synthesize("rain");ambience.loop=true;ambience.volume=Ambience*.1f;ambience.Play();
            SetTheme("title");
        }
        public void SetTheme(string key)
        {
            key=key=="garden"?"battle":key=="reward"?"victory":key;
            if(themeKey==key)return;
            themeKey=key;
            if(musicClips.TryGetValue(key,out var clip)&&clip.loadState!=AudioDataLoadState.Failed)
            {
                int index=Array.FindIndex(musicSources,s=>s.clip==clip);
                if(index<0)
                {
                    index=musicGains[0]<=musicGains[1]?0:1;
                    var source=musicSources[index];source.Stop();source.clip=clip;source.volume=0;musicGains[index]=0;
                }
                activeMusic=index;
                if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
                if(!musicSources[index].isPlaying)musicSources[index].Play();
                theme=null;
            }
            else UseScore(key);
            double releaseAt=AudioSettings.dspTime+.12;
            foreach(var v in voices)if(v.end>releaseAt){v.end=Math.Min(v.end,releaseAt+1.4);v.release=1.4f;}
        }
        private void UseScore(string key)
        {
            activeMusic=-1;
            string fallback=key=="battle"?"garden":key=="travel"?"reward":key;
            theme=Array.Find(score.themes,t=>t.key==fallback)??score.themes[0];beat=0;next=AudioSettings.dspTime+.12;
        }
        private void Update()
        {
            if(AudioListener.pause)return;
            if(activeMusic>=0&&musicSources[activeMusic].clip.loadState==AudioDataLoadState.Failed)
            {Debug.LogWarning("Music could not load; using the sampled score: "+themeKey);UseScore(themeKey);}
            double now=AudioSettings.dspTime;ambience.volume=Ambience*.10f;
            float duckTarget=now<duckUntil?duckStrength:1;
            musicDuck=Mathf.MoveTowards(musicDuck,duckTarget,Time.unscaledDeltaTime*(duckTarget<musicDuck?16:2.5f));
            for(int i=0;i<musicSources.Length;i++)
            {
                musicGains[i]=Mathf.MoveTowards(musicGains[i],i==activeMusic?1:0,Time.unscaledDeltaTime/MusicFadeSeconds);
                musicSources[i].volume=Mathf.Clamp01(Music)*musicDuck*musicGains[i];
                if(i!=activeMusic&&musicGains[i]==0&&musicSources[i].isPlaying)musicSources[i].Stop();
            }
            if(theme!=null)
            {
                if(next<now-.5)next=now+.1;
                while(next<now+.15){foreach(var note in theme.beats[beat%128].notes)Schedule(note,next,60/theme.bpm);next+=60/theme.bpm;beat++;}
            }
            foreach(var v in voices)if(v.end>now) {
                float age=(float)(now-v.start),left=(float)(v.end-now);
                v.source.volume=v.gain*Music*musicDuck*Mathf.Clamp01(age/v.attack)*Mathf.Clamp01(left/v.release);
            } else if(v.source.isPlaying)v.source.Stop();
            foreach(var v in effectVoices)if(v.end>now)v.source.volume=Mathf.Clamp01(Effects*v.gain);
        }
        private void Schedule(Note note,double when,float duration)
        {
            Voice voice=voices.Find(v=>v.end<AudioSettings.dspTime);if(voice==null)return;
            string instrument=note.instrument=="bass"?"cello":note.instrument=="pulse"?"piano":note.instrument;
            string selected=null;int distance=100,baseMidi=60;
            foreach(var pair in samples)if(pair.Key.StartsWith(instrument+"-")){int midi=int.Parse(pair.Key.Substring(instrument.Length+1));int delta=Math.Abs(midi-note.midi);if(delta<distance){selected=pair.Key;baseMidi=midi;distance=delta;}}
            if(selected==null)return;
            voice.source.clip=samples[selected];voice.source.pitch=Mathf.Pow(2,(note.midi-baseMidi)/12f);voice.source.panStereo=note.pan;
            voice.start=when;voice.end=when+note.beats*duration;voice.gain=note.gain*1.8f;voice.attack=instrument=="strings"?.7f:.025f;voice.release=instrument=="strings"?1.1f:.5f;
            voice.source.volume=0;voice.source.PlayScheduled(when);
        }
        public void Play(string key,float strength=1)
        {
            if(!effects.TryGetValue(key,out var clip))return;
            PlayEffect(clip,strength*.55f,UnityEngine.Random.Range(.95f,1.05f),false);
        }
        private void LoadTacticalEffects()
        {
            foreach(string key in new[]{"windup","hit","heavy","guard","break","down","step","element-fire","element-water","element-shadow","element-jade","element-stone","element-cast","element-heal"})
            {
                var clips=new List<AudioClip>();
                for(int i=1;i<=3;i++){var clip=Resources.Load<AudioClip>("Audio/Combat/"+key+"-"+i);if(clip){clips.Add(clip);TacticalClipCount++;}}
                if(clips.Count>0)tacticalEffects[key]=clips.ToArray();
            }
        }
        /// <summary>Short recorded contact sounds; cues consume presentation only and never resolve actions.</summary>
        public void PlayTactical(string cue,string heroId="",float intensity=1f)
        {
            if(AudioListener.pause||Effects<=0||string.IsNullOrEmpty(cue))return;
            if(float.IsNaN(intensity)||float.IsInfinity(intensity))intensity=1;
            intensity=Mathf.Clamp(intensity,.1f,1.4f);
            double now=AudioSettings.dspTime;
            // A multi-target contact is one audible event, rather than a stack of identical transients.
            double interval=cue=="step"?.075:cue=="windup"?.025:.045;
            if(tacticalCueTimes.TryGetValue(cue,out var previous)&&now-previous<interval)return;
            tacticalCueTimes[cue]=now;
            string element=heroId=="lingfeng"?"element-fire":heroId=="cangling"?"element-water":heroId=="yanzhuying"?"element-shadow":heroId=="sixuan"?"element-jade":heroId=="shangshuo"?"element-stone":"element-cast";
            float pitch=UnityEngine.Random.Range(.975f,1.025f);
            switch(cue)
            {
                case "windup":PlayTacticalClip("windup",.34f*intensity,pitch);break;
                case "step":PlayTacticalClip("step",.24f*intensity,pitch);break;
                case "hit":
                    PlayTacticalClip("hit",.60f*intensity,pitch);
                    DuckMusic(.86f,.16f);break;
                case "heavy":
                    PlayTacticalClip("heavy",.69f*intensity,pitch*.94f);
                    if(!string.IsNullOrEmpty(heroId))PlayTacticalClip(element,.28f*intensity,pitch);
                    DuckMusic(.76f,.25f);break;
                case "guard":
                    PlayTacticalClip("guard",.46f*intensity,pitch);
                    if(heroId=="sixuan")PlayTacticalClip("element-jade",.18f*intensity,1.08f);
                    DuckMusic(.91f,.13f);break;
                case "break":
                    PlayTacticalClip("break",.50f*intensity,pitch);
                    DuckMusic(.83f,.18f);break;
                case "down":
                    PlayTacticalClip("down",.54f*intensity,pitch*.91f);break;
                case "cast":
                    PlayTacticalClip(element,.54f*intensity,pitch);
                    DuckMusic(.87f,.22f);break;
                case "heal":
                    PlayTacticalClip("element-heal",.45f*intensity,pitch);
                    if(heroId=="cangling")PlayTacticalClip("element-water",.27f*intensity,pitch);
                    break;
            }
        }
        public void CancelTactical()
        {
            foreach(var voice in effectVoices)if(voice.tactical){voice.source.Stop();voice.end=0;voice.tactical=false;}
            tacticalCueTimes.Clear();duckUntil=0;duckStrength=1;
        }
        private void PlayTacticalClip(string key,float gain,float pitch)
        {
            if(!tacticalEffects.TryGetValue(key,out var clips))return;
            int index=UnityEngine.Random.Range(0,clips.Length);
            if(clips.Length>1&&lastVariants.TryGetValue(key,out var last)&&index==last)index=(index+1)%clips.Length;
            lastVariants[key]=index;PlayEffect(clips[index],gain,pitch,true);
        }
        private void PlayEffect(AudioClip clip,float gain,float pitch,bool tactical)
        {
            if(!clip||AudioListener.pause||effectVoices.Count==0)return;
            double now=AudioSettings.dspTime;EffectVoice selected=null;int active=0;
            foreach(var voice in effectVoices)
            {
                if(voice.end<=now){if(selected==null)selected=voice;}else active++;
            }
            if(selected==null){selected=effectVoices[0];foreach(var voice in effectVoices)if(voice.end<selected.end)selected=voice;}
            if(tactical&&active>4)gain*=Mathf.Sqrt(4f/active);
            selected.source.Stop();selected.source.clip=clip;selected.source.pitch=pitch;selected.gain=Mathf.Clamp01(gain);selected.tactical=tactical;
            selected.source.volume=Mathf.Clamp01(Effects*selected.gain);selected.source.Play();selected.end=now+clip.length/Mathf.Abs(pitch)+.025;
        }
        private void DuckMusic(float strength,float duration)
        {
            double now=AudioSettings.dspTime;
            duckStrength=now<duckUntil?Mathf.Min(duckStrength,strength):strength;duckUntil=Math.Max(duckUntil,now+duration);
        }
        private AudioClip Synthesize(string key)
        {
            const int rate=44100;float seconds=key=="rain"?12:key=="magic"||key=="heal"?1.1f:key=="tide"?.95f:key=="balance"||key=="oath"?.82f:key=="paper"?.32f:.45f;
            var data=new float[(int)(rate*seconds)];var random=new System.Random(719+key.Length);float filtered=0;
            for(int i=0;i<data.Length;i++) {
                float t=i/(float)rate,x=t/seconds,n=(float)random.NextDouble()*2-1;filtered=filtered*.89f+n*.11f;float value;
                switch(key){
                    case "rain":value=filtered*.55f+n*.025f;break;
                    case "paper":value=(n-filtered)*Mathf.Sin(x*Mathf.PI)*.16f+filtered*Mathf.Sin(x*Mathf.PI*5)*.24f;break;
                    case "hit":value=(Mathf.Sin(t*(170-t*150))*.7f+filtered*.9f+n*.2f)*Mathf.Exp(-t*13);break;
                    case "shield":value=(Mathf.Sin(t*3100)+Mathf.Sin(t*4630)*.4f)*Mathf.Exp(-t*9)*.3f;break;
                    case "select":value=Mathf.Sin(t*2100)*Mathf.Exp(-t*70)*.3f;break;
                    case "deny":value=(Mathf.Sin(t*1500)+Mathf.Sin(t*1140)*.45f)*Mathf.Exp(-t*18)*.24f;break;
                    case "cleave":
                        value=((n-filtered)*.43f+filtered*.35f)*Mathf.Pow(Mathf.Sin(x*Mathf.PI),2)*Mathf.Exp(-x*1.8f)
                            +Mathf.Sin(t*(880-t*680))*Mathf.Exp(-t*17)*.24f;break;
                    case "balance":
                        value=(Mathf.Sin(t*5100)+Mathf.Sin(t*7660)*.35f)*Mathf.Exp(-t*7)*.22f
                            +Mathf.Sin(t*2040)*Mathf.Exp(-t*3.5f)*.12f;break;
                    case "tide":
                        value=filtered*Mathf.Sin(x*Mathf.PI)*.8f+Mathf.Sin(t*(1100+Mathf.Sin(t*12)*70))*Mathf.Pow(1-x,3)*.1f;break;
                    case "oath":
                        value=(Mathf.Sin(t*(450-t*220))*.48f+filtered*.55f)*Mathf.Exp(-t*6)
                            +(Mathf.Sin(t*1760)+Mathf.Sin(t*2640)*.4f)*Mathf.Exp(-t*5)*.1f;break;
                    default:value=(Mathf.Sin(t*1650)+Mathf.Sin(t*2470)*.4f+Mathf.Sin(t*3300)*.2f)*Mathf.Sin(Mathf.Min(1,x*4)*Mathf.PI*.5f)*Mathf.Pow(1-x,3)*.23f;break;
                }
                data[i]=Mathf.Clamp(value,-.9f,.9f);
            }
            var clip=Visuals.Own(AudioClip.Create("Original "+key,data.Length,1,rate,false));clip.SetData(data,0);return clip;
        }
        private AudioClip BronzeBell()
        {
            const int rate=44100;var data=new float[rate*3];const float fundamental=587.3295f;
            for(int i=0;i<data.Length;i++){
                float t=i/(float)rate,w=2*Mathf.PI*fundamental*t;
                float value=Mathf.Sin(w)*Mathf.Exp(-t*2.1f)+.45f*Mathf.Sin(w*2.01f)*Mathf.Exp(-t*3.3f)+.22f*Mathf.Sin(w*2.74f)*Mathf.Exp(-t*5f)+.10f*Mathf.Sin(w*4.05f)*Mathf.Exp(-t*7f);
                data[i]=value*.25f*Mathf.Clamp01(t/.0025f)*Mathf.Clamp01((3-t)/.06f);
            }
            var clip=Visuals.Own(AudioClip.Create("Original bronze bell D5",data.Length,1,rate,false));clip.SetData(data,0);return clip;
        }
    }
}
