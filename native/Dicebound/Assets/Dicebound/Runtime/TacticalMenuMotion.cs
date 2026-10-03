using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Dicebound.Presentation
{
    /// <summary>Keeps one menu movie alive across page rebuilds, with the authored image as its fallback.</summary>
    public sealed class TacticalMenuMotion : MonoBehaviour, IDisposable
    {
        private const float StartupTimeoutSeconds = 20f;
        private static readonly int BackdropId = Shader.PropertyToID("_TacticalGlassBackdrop");
        private static readonly int BackdropUvId = Shader.PropertyToID("_TacticalGlassBackdropUV");
        private static readonly int BackdropColorId = Shader.PropertyToID("_TacticalGlassBackdropColor");
        private static readonly int BackdropWashId = Shader.PropertyToID("_TacticalGlassBackdropWash");
        private static readonly int MaskIsSrgbId = Shader.PropertyToID("_MaskIsSRGB");

        private sealed class Source
        {
            public string File;
            public VideoPlayer Player;
            public RenderTexture Surface;
            public bool Prepared, HasFrame, Failed, PlayRequested;
            public float StartupElapsed, PackedColorAspect;
            public VideoPlayer.EventHandler PreparedHandler;
            public VideoPlayer.FrameReadyEventHandler FrameHandler;
            public VideoPlayer.ErrorEventHandler ErrorHandler;
        }

        private readonly HashSet<string> warnedFiles = new HashSet<string>(StringComparer.Ordinal);
        private Source source;
        private RawImage target;
        private Texture stillTexture;
        private Material stillMaterial, movieMaterial;
        private Texture ownedBackdropTexture;
        private bool glassBackdrop, displayingMovie, ownsBackdrop, suspended, disposed;
        private bool applicationPaused,applicationFocused=true;

        public string CurrentFile => source?.File;
        public bool IsPrepared => source != null && source.Prepared && source.Player && source.Player.isPrepared;
        public bool HasFrame => source != null && source.HasFrame && source.Surface;
        public bool IsPlaying => source != null && source.Player && source.Player.isPlaying;
        public double Time => IsPrepared ? source.Player.time : 0d;
        public bool IsSuspended => suspended||applicationPaused||!applicationFocused;
        public bool IsBound => target;
        public bool IsDisplayingMovie => displayingMovie&&target&&HasFrame;
        public bool Failed => source!=null&&source.Failed;

        public static string CompanionMovieFile(string heroId)
        {
            switch(heroId){
                case "sixuan":case "lingfeng":case "cangling":case "yanzhuying":return heroId+"-packed-loop.mp4";
                case "shangshuo":return "shangshuo-ultimate-action.mp4";
                default:return null;
            }
        }
        public static bool CompanionUsesPackedMovie(string heroId)
        {
            return heroId!="shangshuo"&&CompanionMovieFile(heroId)!=null;
        }
        public static string MoviePath(string fileName)
        {
            if(string.IsNullOrWhiteSpace(fileName)||Path.GetFileName(fileName)!=fileName)
                throw new IOException("The menu video filename is invalid.");
            string folder=fileName=="shangshuo-ultimate-action.mp4"?"UltimateMotion":"MenuMotion";
            return Path.Combine(Application.streamingAssetsPath,folder,fileName);
        }
        public static Texture2D CompanionPoster(string heroId)
        {
            string poster=heroId=="shangshuo"?"shangshuo-action-poster":heroId+"-preview-poster";
            var texture=Resources.Load<Texture2D>("Tactics/MenuMotion/"+poster);
            return texture?texture:Resources.Load<Texture2D>("Tactics/Cinematics/"+heroId+"-ultimate-v2");
        }
        private bool ReducedMotion => TacticalDirector.Instance && TacticalDirector.Instance.ReducedMotion;

        /// <summary>Call before destroying the old page. A Bind in this frame can reuse its decoder and time.</summary>
        public void BeginPage()
        {
            if (disposed) return;
            DetachTarget();
        }

        public void Bind(RawImage target, string fileName, bool glassBackdrop = false, Material material = null, float packedColorAspect = 0)
        {
            if (disposed) return;
            DetachTarget();
            if (!target) return;

            if (source == null || !string.Equals(source.File, fileName, StringComparison.Ordinal))
            {
                ReleaseSource();
                source = new Source { File = fileName };
            }

            this.target = target;
            source.PackedColorAspect=packedColorAspect;
            stillTexture = target.texture;
            stillMaterial = target.material;
            movieMaterial = material;
            this.glassBackdrop = glassBackdrop;
            if(source.Prepared)ValidatePackedFrame(source);
            RefreshPresentation();
        }

        public void SetSuspended(bool value)
        {
            if (disposed || suspended == value) return;
            suspended = value;
            RefreshPresentation();
            if (suspended) PauseSource();
        }

        private void LateUpdate()
        {
            if (disposed) return;
            // Destroyed Unity objects compare equal to null even while their managed references survive.
            if (!target)
            {
                DetachTarget();
                ReleaseSource();
                return;
            }

            RefreshPresentation();
            if (source == null || source.Failed || source.HasFrame || IsSuspended || ReducedMotion || !isActiveAndEnabled) return;
            source.StartupElapsed += UnityEngine.Time.unscaledDeltaTime;
            if (source.StartupElapsed >= StartupTimeoutSeconds)
                Fail(source, "Timed out waiting for the first video frame.");
        }

        private void RefreshPresentation()
        {
            if (disposed || !target || source == null) return;
            if (ReducedMotion || source.Failed)
            {
                RestoreStill();
                PauseSource();
                return;
            }

            if (!source.Player) Prepare(source);
            if (source == null || source.Failed) return;
            if (source.HasFrame && source.Surface)
            {
                if (movieMaterial && movieMaterial.HasProperty(MaskIsSrgbId))
                    movieMaterial.SetFloat(MaskIsSrgbId, source.Surface.sRGB ? 1f : 0f);
                target.texture = source.Surface;
                target.material = movieMaterial ? movieMaterial : stillMaterial;
                displayingMovie = true;
            }
            else RestoreStill();
            UpdateBackdrop();

            if (IsSuspended || !isActiveAndEnabled) PauseSource();
            else if (source.Prepared && !source.PlayRequested)
            {
                try
                {
                    source.PlayRequested = true;
                    source.Player.Play();
                }
                catch (Exception e) { Fail(source, e.Message); }
            }
        }

        private void Prepare(Source candidate)
        {
            if (candidate.Failed || candidate.Player || disposed) return;
            try
            {
                string path = MoviePath(candidate.File);
                // StreamingAssets is a normal directory in the desktop Player. URL-backed platforms report errors through VideoPlayer.
                if (!path.Contains("://") && !File.Exists(path))
                    throw new FileNotFoundException("The menu video file is missing.", path);

                var player = gameObject.AddComponent<VideoPlayer>();
                candidate.Player = player;
                player.playOnAwake = false;
                player.source = VideoSource.Url;
                player.url = path.Replace('\\', '/');
                player.renderMode = VideoRenderMode.RenderTexture;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.controlledAudioTrackCount = 0;
                player.isLooping = true;
                player.waitForFirstFrame = true;
                player.skipOnDrop = true;
                player.sendFrameReadyEvents = true;
                candidate.PreparedHandler = sender => Prepared(candidate, sender);
                candidate.FrameHandler = (sender, frame) => FrameReady(candidate, sender);
                candidate.ErrorHandler = (sender, message) =>
                {
                    if (IsCurrent(candidate, sender)) Fail(candidate, message);
                };
                player.prepareCompleted += candidate.PreparedHandler;
                player.frameReady += candidate.FrameHandler;
                player.errorReceived += candidate.ErrorHandler;
                player.Prepare();
            }
            catch (Exception e) { Fail(candidate, e.Message); }
        }

        private bool IsCurrent(Source candidate, VideoPlayer sender)
        {
            return !disposed && ReferenceEquals(source, candidate) && !candidate.Failed &&
                   candidate.Player && ReferenceEquals(candidate.Player, sender);
        }

        private void Prepared(Source candidate, VideoPlayer player)
        {
            if (!IsCurrent(candidate, player) || candidate.Prepared) return;
            try
            {
                int width = checked((int)player.width), height = checked((int)player.height);
                if (width <= 0 || height <= 0 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
                    throw new InvalidOperationException("The menu video has unsupported frame dimensions.");
                candidate.Surface = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
                {
                    name = "Menu motion: " + candidate.File,
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!candidate.Surface.Create()) throw new InvalidOperationException("The menu video render texture could not be created.");
                player.targetTexture = candidate.Surface;
                ValidatePackedFrame(candidate);
                if(candidate.Failed)return;
                candidate.Prepared = true;
                candidate.StartupElapsed = 0;
                RefreshPresentation();
            }
            catch (Exception e) { Fail(candidate, e.Message); }
        }

        private void FrameReady(Source candidate, VideoPlayer player)
        {
            if (!IsCurrent(candidate, player) || !candidate.Prepared || !candidate.Surface) return;
            candidate.HasFrame = true;
            // The RenderTexture continues updating without a managed callback for every decoded frame.
            player.sendFrameReadyEvents = false;
            RefreshPresentation();
        }

        private void ValidatePackedFrame(Source candidate)
        {
            if(candidate.PackedColorAspect<=0||!candidate.Surface)return;
            float aspect=candidate.Surface.width/(candidate.Surface.height*.5f);
            if(candidate.Surface.height%2!=0||Mathf.Abs(aspect-candidate.PackedColorAspect)>.005f)
                Fail(candidate,"The packed video does not match the portrait's color-frame aspect.");
        }

        private void PauseSource()
        {
            if (source == null || !source.Player || source.Failed) return;
            try
            {
                if (source.PlayRequested || source.Player.isPlaying) source.Player.Pause();
                source.PlayRequested = false;
            }
            catch (Exception e) { Fail(source, e.Message); }
        }

        private void RestoreStill()
        {
            if (target && displayingMovie)
            {
                target.texture = stillTexture;
                target.material = stillMaterial;
            }
            displayingMovie = false;
            UpdateBackdrop();
        }

        private void UpdateBackdrop()
        {
            if (!glassBackdrop || !target) return;
            Shader.SetGlobalTexture(BackdropId, target.texture);
            var uv = target.uvRect;
            Shader.SetGlobalVector(BackdropUvId, new Vector4(uv.x, uv.y, uv.width, uv.height));
            Shader.SetGlobalColor(BackdropColorId, target.color);
            ownedBackdropTexture = target.texture;
            ownsBackdrop = true;
        }

        private void ClearOwnedBackdrop()
        {
            // Another page may already have installed its own still backdrop by the time this decoder is retired.
            if (ownsBackdrop && Shader.GetGlobalTexture(BackdropId) == ownedBackdropTexture)
            {
                Shader.SetGlobalTexture(BackdropId, Texture2D.blackTexture);
                Shader.SetGlobalVector(BackdropUvId, new Vector4(0, 0, 1, 1));
                Shader.SetGlobalColor(BackdropColorId, Color.white);
                Shader.SetGlobalColor(BackdropWashId, Color.clear);
            }
            ownedBackdropTexture = null;
            ownsBackdrop = false;
        }

        private void DetachTarget()
        {
            // Restore the image without claiming global shader state that may now belong to a new page.
            if (target && displayingMovie)
            {
                target.texture = stillTexture;
                target.material = stillMaterial;
            }
            ClearOwnedBackdrop();
            target = null;
            stillTexture = null;
            stillMaterial = movieMaterial = null;
            glassBackdrop = displayingMovie = false;
        }

        private void Fail(Source candidate, string message)
        {
            if (disposed || !ReferenceEquals(source, candidate) || candidate.Failed) return;
            candidate.Failed = true;
            RestoreStill();
            ReleaseResources(candidate);
            if (warnedFiles.Add(candidate.File ?? ""))
                Debug.LogWarning("Menu motion '" + candidate.File + "' is unavailable; keeping the still image. " + message, this);
        }

        private void ReleaseSource()
        {
            var previous = source;
            source = null; // Invalidate callbacks before stopping the native decoder.
            if (previous != null) ReleaseResources(previous);
        }

        private static void ReleaseResources(Source previous)
        {
            if (previous.Player)
            {
                previous.Player.prepareCompleted -= previous.PreparedHandler;
                previous.Player.frameReady -= previous.FrameHandler;
                previous.Player.errorReceived -= previous.ErrorHandler;
                previous.Player.sendFrameReadyEvents = false;
                try { previous.Player.Stop(); previous.Player.targetTexture = null; }
                catch (Exception) { /* Native decoder teardown must still release the owned texture. */ }
                Destroy(previous.Player);
            }
            if (previous.Surface)
            {
                previous.Surface.Release();
                Destroy(previous.Surface);
            }
            previous.Player = null;
            previous.Surface = null;
            previous.Prepared = previous.HasFrame = previous.PlayRequested = false;
        }

        private void OnDisable() { PauseSource(); }
        private void OnApplicationFocus(bool focused) {applicationFocused=focused;RefreshPresentation();}
        private void OnApplicationPause(bool paused) {applicationPaused=paused;RefreshPresentation();}
        private void OnDestroy() { Dispose(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DetachTarget();
            ReleaseSource();
        }
    }
}
