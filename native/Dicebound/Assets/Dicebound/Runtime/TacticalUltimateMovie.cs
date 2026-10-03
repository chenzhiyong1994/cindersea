using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dicebound.Presentation
{
    /// <summary>A finite, silent movie of an already committed action. It never accesses battle state.</summary>
    public sealed class TacticalUltimateMovie : MonoBehaviour
    {
        private const float StartupTimeout=1.8f;
        private readonly HashSet<string> warned=new HashSet<string>(StringComparer.Ordinal);
        private VideoPlayer player;
        private RenderTexture surface;
        private Material movieMaterial;
        private RawImage image;
        private bool packed;
        private bool prepared,hasFrame,failed,finished,playRequested;
        private int generation;
        private string currentFile;
        public bool Displaying=>hasFrame&&image&&image.enabled;
        public bool LastPlaybackDisplayed {get;private set;}
        public float Progress {get;private set;}
        public static string MovieFile(string heroId)=>TacticalMenuMotion.CompanionMovieFile(heroId);
        public static float MovieDuration(string heroId)=>1.6f;

        public IEnumerator Play(string heroId,RawImage target,Func<bool> held,Action<float> render)
        {
            Cancel();LastPlaybackDisplayed=false;Progress=0;
            currentFile=MovieFile(heroId);if(currentFile==null||!target)yield break;
            packed=TacticalMenuMotion.CompanionUsesPackedMovie(heroId);
            int ticket=generation;image=target;image.enabled=false;
            if(!StartSource())yield break;
            float startup=0,elapsed=0;
            double duration=MovieDuration(heroId);
            bool retainLastFrame=false;
            try
            {
                while(ticket==generation&&!failed&&!finished&&image&&image.gameObject.activeInHierarchy)
                {
                    if(TacticalDirector.Instance&&TacticalDirector.Instance.ReducedMotion)break;
                    bool pause=held!=null&&held();
                    // Decode the first frame even when a keyframe preview is held. Preparation
                    // must still time out; a hold cannot leave an invisible decoder waiting forever.
                    SetPlaying(!hasFrame||!pause);
                    if(!hasFrame)
                    {
                        startup+=Time.unscaledDeltaTime;
                        if(startup>StartupTimeout){Fail("Timed out waiting for the first action frame.");break;}
                    }
                    else if(!pause)
                    {
                        elapsed+=Time.unscaledDeltaTime;
                        double time=Math.Max(0,player.time);
                        Progress=Mathf.Clamp01((float)(time/duration));
                        render?.Invoke(Progress);
                        // A stalled native decoder cannot leave the saved action input-locked.
                        if(time>=duration){finished=true;break;}
                        if(elapsed>duration+1){Fail("Action movie decoder stopped advancing.");break;}
                    }
                    yield return null;
                }
                // The owner slides the same decoded frame away before releasing the surface.
                // Removing it here would reveal a different still underneath the exit animation.
                retainLastFrame=ticket==generation&&finished&&!failed&&Displaying;
                if(retainLastFrame){Progress=1;render?.Invoke(Progress);}
            }
            finally
            {
                if(ticket==generation)
                {
                    if(retainLastFrame)SetPlaying(false);
                    else Cancel();
                }
            }
        }

        private bool StartSource()
        {
            try
            {
                string path=TacticalMenuMotion.MoviePath(currentFile);
                if(!path.Contains("://")&&!File.Exists(path))throw new FileNotFoundException("Action movie is missing.",path);
                if(packed)
                {
                    var shader=Resources.Load<Shader>("Shaders/Tactics/MenuPackedVideo");
                    if(!shader||!shader.isSupported)throw new InvalidOperationException("Portrait movie coverage shader is unavailable.");
                    movieMaterial=new Material(shader){name="Ultimate animated portrait coverage",hideFlags=HideFlags.DontSave};
                }
                image.material=movieMaterial;image.uvRect=new Rect(0,0,1,1);
                player=gameObject.AddComponent<VideoPlayer>();player.playOnAwake=false;player.source=VideoSource.Url;player.url=path.Replace('\\','/');
                player.renderMode=VideoRenderMode.RenderTexture;player.audioOutputMode=VideoAudioOutputMode.None;player.controlledAudioTrackCount=0;
                player.isLooping=false;player.waitForFirstFrame=true;player.skipOnDrop=true;player.sendFrameReadyEvents=true;
                player.prepareCompleted+=Prepared;player.frameReady+=FrameReady;player.errorReceived+=Error;player.loopPointReached+=Finished;
                player.Prepare();return true;
            }
            catch(Exception e){Fail(e.Message);Cancel();return false;}
        }
        private void Prepared(VideoPlayer sender)
        {
            if(!IsCurrent(sender)||failed)return;
            try
            {
                int width=checked((int)sender.width),height=checked((int)sender.height);
                float colorHeight=packed?height*.5f:height;
                if(width<=0||height<=0||width>SystemInfo.maxTextureSize||height>SystemInfo.maxTextureSize||(packed&&height%2!=0)||Mathf.Abs(width/colorHeight-16f/9f)>.005f)
                    throw new InvalidOperationException("Action movie has unsupported dimensions.");
                surface=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default){name="Ultimate movie: "+currentFile,hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                if(!surface.Create())throw new InvalidOperationException("Action movie render texture allocation failed.");
                if(movieMaterial)movieMaterial.SetFloat("_MaskIsSRGB",surface.sRGB?1:0);
                sender.targetTexture=surface;prepared=true;
            }
            catch(Exception e){Fail(e.Message);}
        }
        private void FrameReady(VideoPlayer sender,long frame)
        {
            if(!IsCurrent(sender)||failed||!image||!surface)return;
            hasFrame=true;LastPlaybackDisplayed=true;image.texture=surface;image.color=Color.white;image.enabled=true;
            sender.sendFrameReadyEvents=false;
        }
        private void SetPlaying(bool play)
        {
            if(!prepared||failed||!player||play==playRequested)return;
            try{if(play)player.Play();else player.Pause();playRequested=play;}
            catch(Exception e){Fail(e.Message);}
        }
        private bool IsCurrent(VideoPlayer sender)=>player&&sender&&ReferenceEquals(player,sender);
        private void Error(VideoPlayer sender,string message){if(IsCurrent(sender))Fail(message);}
        private void Finished(VideoPlayer sender){if(IsCurrent(sender))finished=true;}
        private void Fail(string message)
        {
            failed=true;if(image)image.enabled=false;
            if(warned.Add(currentFile??""))Debug.LogWarning("Ultimate movie '"+currentFile+"' is unavailable; keeping the authored still. "+message,this);
        }
        public void Cancel()
        {
            generation++;
            if(image){image.enabled=false;image.texture=null;image.material=null;image.color=Color.white;}image=null;
            var old=player;player=null;
            if(old)
            {
                old.prepareCompleted-=Prepared;old.frameReady-=FrameReady;old.errorReceived-=Error;old.loopPointReached-=Finished;old.sendFrameReadyEvents=false;
                try{old.Stop();old.targetTexture=null;}catch(Exception){/* Decoder teardown must not interrupt presentation cleanup. */}
                Destroy(old);
            }
            if(surface){surface.Release();Destroy(surface);}surface=null;
            if(movieMaterial)Destroy(movieMaterial);movieMaterial=null;
            prepared=hasFrame=failed=finished=playRequested=false;
        }
        private void OnApplicationFocus(bool focused){if(!focused)Cancel();}
        private void OnApplicationPause(bool paused){if(paused)Cancel();}
        private void OnDisable(){Cancel();}
        private void OnDestroy(){Cancel();}
    }
}
