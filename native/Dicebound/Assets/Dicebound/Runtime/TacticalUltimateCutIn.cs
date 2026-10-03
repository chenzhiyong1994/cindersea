using System;
using System.Collections;
using System.Linq;
using Dicebound.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>A separate transient overlay; shared charge and damage are owned entirely by TacticalRules.</summary>
    public sealed class TacticalUltimateCutIn : MonoBehaviour
    {
        public const float Duration=1.48f;
        public bool Active => canvas&&canvas.gameObject.activeSelf;
        public string CurrentHero { get; private set; }
        public float Progress { get; private set; }
        public int PresentedCount { get; private set; }
        public bool UsingDedicatedArt { get; private set; }
        public Texture PortraitTexture => portrait?portrait.texture:null;
        public Vector2 PortraitDisplaySize => portrait?portrait.rectTransform.rect.size:Vector2.zero;
        public float PortraitAspectError => !PortraitTexture||PortraitDisplaySize.y<=0?float.PositiveInfinity:Mathf.Abs(PortraitDisplaySize.x/PortraitDisplaySize.y-(float)PortraitTexture.width/PortraitTexture.height);
        public bool ShowsCompleteArtwork => portrait&&portrait.uvRect==new Rect(0,0,1,1)&&PortraitAspectError<.001f;
        public int ArtworkAspectChecks { get; private set; }
        public bool PreviewHeld { get; private set; }
        public bool HasPaintedEffects => UsingDedicatedArt&&portrait&&portrait.material&&portrait.material.shader.name=="Dicebound/UI/CinematicInk";
        public bool HasImpactEffects => impactBack&&impactFront;
        public int ImpactVertexCount => (impactBack?impactBack.VisibleVertexCount:0)+(impactFront?impactFront.VisibleVertexCount:0);
        public bool IsPlayingActionMovie => actionMovie&&actionMovie.Displaying;
        public bool LastActionMovieDisplayed => actionMovie&&actionMovie.LastPlaybackDisplayed;
        public bool IsShowingStillArtwork => Active&&portrait&&portrait.enabled&&group.alpha>.001f;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform content,moving;
        private RawImage portrait,actionPicture;
        private TacticalUltimateMovie actionMovie;
        private Image veil;
        private Text heroName,skillName,heroTitle;
        private TacticalCutInMotes motes;
        private TacticalCutInImpact impactBack,impactFront;
        private Material illustrationMaterial;
        private int generation;
        private Color accent;
        private Vector2 artPosition;
        private bool awaitingMovieFrame,renderedReduced;
        private bool entrySound,impactSound;
        private float previewHeldAt;

        public void Initialize()
        {
            if(canvas)return;
            canvas=Ui.Canvas("Tactical ultimate cinematic overlay");canvas.transform.SetParent(transform,false);canvas.sortingOrder=120;
            content=canvas.GetComponent<PaperViewportCanvas>().Content;
            group=canvas.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=true;
            veil=Ui.Panel("Ultimate battlefield veil",content,Vector2.one*.5f,Vector2.zero,PaperViewport.DesignSize,new Color(.008f,.014f,.022f,.88f),true);
            impactBack=Ui.Rect("Ultimate rushing speed field",content,Vector2.one*.5f,Vector2.zero,PaperViewport.DesignSize).gameObject.AddComponent<TacticalCutInImpact>();impactBack.raycastTarget=false;
            moving=Ui.Rect("Ultimate diagonal composition",content,Vector2.one*.5f,Vector2.zero,PaperViewport.DesignSize);
            // The painted plate includes its own ink, backlight and elemental sweep. The live battle remains visible through alpha.
            portrait=Ui.Picture("Ultimate painted action and elemental backlight",moving,null,Vector2.one*.5f,Vector2.zero,new Vector2(1880,1060),Color.white);
            var shader=Resources.Load<Shader>("Tactics/Cinematics/CinematicInk");
            if(!shader||!shader.isSupported)throw new InvalidOperationException("缺少大招绘制材质。");
            illustrationMaterial=new Material(shader){name="Ultimate painted ink and light sweep"};portrait.material=illustrationMaterial;
            // Use the current selection portrait, including its authored coverage format.
            // Full UV keeps the artwork complete; the entry and impact are live UI effects.
            actionPicture=Ui.Picture("Ultimate action movie",moving,null,Vector2.one*.5f,Vector2.zero,PaperViewport.DesignSize,Color.white);actionPicture.enabled=false;
            actionMovie=gameObject.AddComponent<TacticalUltimateMovie>();
            motes=Ui.Rect("Ultimate moving embers and droplets",moving,Vector2.one*.5f,Vector2.zero,PaperViewport.DesignSize).gameObject.AddComponent<TacticalCutInMotes>();motes.raycastTarget=false;
            impactFront=Ui.Rect("Ultimate impact arcs and edge streaks",moving,Vector2.one*.5f,Vector2.zero,PaperViewport.DesignSize).gameObject.AddComponent<TacticalCutInImpact>();impactFront.Foreground=true;impactFront.raycastTarget=false;
            heroTitle=Ui.Heading("Ultimate hero title",moving,"",Vector2.one*.5f,new Vector2(665,-193),new Vector2(470,44),22,new Color(.95f,.82f,.57f));heroTitle.alignment=TextAnchor.MiddleRight;
            heroName=Ui.Heading("Ultimate hero name",moving,"",Vector2.one*.5f,new Vector2(650,-290),new Vector2(510,170),126,new Color(1,.84f,.49f));heroName.alignment=TextAnchor.MiddleRight;
            skillName=Ui.Heading("Ultimate skill name",moving,"三息合誓",Vector2.one*.5f,new Vector2(658,-402),new Vector2(460,92),60,TacticalTheme.OnDark);skillName.alignment=TextAnchor.MiddleRight;
            var brush=Resources.Load<Font>("Fonts/MaShanZheng-Regular");
            if(!brush)throw new InvalidOperationException("缺少大招书法字体。");
            heroName.font=brush;skillName.font=brush;heroName.gameObject.AddComponent<TacticalGoldLettering>();skillName.gameObject.AddComponent<TacticalGoldLettering>();
            var flourish=Ui.Rect("Ultimate gold name flourish",moving,Vector2.one*.5f,new Vector2(694,-468),new Vector2(412,28)).gameObject.AddComponent<TacticalCutInFlourish>();flourish.color=new Color(.96f,.73f,.34f);flourish.raycastTarget=false;
            var outline=heroName.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.055f,.02f,.015f,.92f);outline.effectDistance=new Vector2(1.5f,-1.5f);
            var skillOutline=skillName.gameObject.AddComponent<Outline>();skillOutline.effectColor=new Color(.015f,.02f,.028f,.94f);skillOutline.effectDistance=new Vector2(2,-2);
            canvas.gameObject.SetActive(false);
        }
        public IEnumerator Play(string heroId,bool reduced)
        {
            Initialize();Cancel();int ticket=generation;CurrentHero=heroId;PresentedCount++;accent=TacticalSkillVfx.Accent(heroId);
            var hero=TacticalContent.GetHero(heroId);if(hero==null)throw new InvalidOperationException("大招演出收到未知角色："+heroId);
            var texture=TacticalMenuMotion.CompanionPoster(heroId);
            if(!texture)throw new InvalidOperationException("大招缺少角色动作与元素原画："+heroId);
            bool useMovie=!reduced&&TacticalUltimateMovie.MovieFile(heroId)!=null;
            awaitingMovieFrame=useMovie;
            UsingDedicatedArt=true;portrait.texture=texture;portrait.uvRect=new Rect(0,0,1,1);portrait.enabled=!useMovie;
            float ratio=(float)texture.width/texture.height;
            float height=Mathf.Min(1060,1900/ratio);portrait.rectTransform.sizeDelta=new Vector2(height*ratio,height);
            artPosition=Vector2.zero;portrait.rectTransform.anchoredPosition=artPosition;
            heroName.text=hero.name;heroTitle.text=hero.title;heroName.color=Color.white;skillName.color=Color.white;
            skillName.text=TacticalContent.Skills.FirstOrDefault(s=>s.heroId==heroId&&s.resource=="charge"&&s.id!="oath")?.name??"三息合誓";
            motes.Accent=Color.Lerp(accent,Color.white,.34f);motes.Tide=heroId=="cangling";
            impactBack.HeroId=impactFront.HeroId=heroId;impactBack.Accent=impactFront.Accent=accent;
            entrySound=impactSound=false;
            illustrationMaterial.SetColor("_Accent",accent);illustrationMaterial.SetFloat("_Motion",reduced?0:1);
            // Preparing a native decoder is asynchronous. Keep the whole overlay transparent
            // until a decoded frame can enter, instead of introducing the still first.
            Progress=0;Render(0,reduced);canvas.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();
            float duration=reduced?.18f:Duration;
            try
            {
                if(!ShowsCompleteArtwork)throw new InvalidOperationException("大招立绘未保留完整原比。");ArtworkAspectChecks++;
                if(useMovie)
                {
                    float movieDuration=TacticalUltimateMovie.MovieDuration(heroId);
                    yield return actionMovie.Play(heroId,actionPicture,()=>PreviewHeld,p=>
                    {
                        awaitingMovieFrame=false;
                        float seconds=p*movieDuration;
                        Progress=seconds<.22f?Mathf.Lerp(0,.18f,seconds/.22f):Mathf.Lerp(.18f,.79f,(seconds-.22f)/(movieDuration-.22f));
                        Render(Progress,false);
                    });
                    if(ticket!=generation)yield break;
                    if(actionMovie.Displaying)
                        yield return AnimateRange(.79f,1,.32f,false,ticket);
                    else
                    {
                        // Only unavailable, failed or newly reduced-motion playback uses art.
                        bool fallbackReduced=reduced||(TacticalDirector.Instance&&TacticalDirector.Instance.ReducedMotion);
                        awaitingMovieFrame=false;Progress=0;Render(0,fallbackReduced);portrait.enabled=true;
                        yield return AnimateRange(0,1,fallbackReduced?.18f:Duration,fallbackReduced,ticket);
                    }
                }
                else yield return AnimateRange(0,1,duration,reduced,ticket);
            }
            finally {if(ticket==generation)Cancel();}
        }
        private IEnumerator AnimateRange(float from,float to,float seconds,bool reduced,int ticket)
        {
            for(float elapsed=0;ticket==generation&&elapsed<seconds;)
            {
                if(!PreviewHeld){Progress=Mathf.Lerp(from,to,Mathf.Clamp01(elapsed/seconds));Render(Progress,reduced);elapsed+=Time.unscaledDeltaTime;}
                yield return null;
            }
        }
        /// <summary>Hold the actual overlay at a chosen keyframe. ReleasePreview resumes its finite animation.</summary>
        public void SetPreviewProgress(float progress)
        {if(!Active)throw new InvalidOperationException("只能暂停正在播放的大招立绘。");PreviewHeld=true;previewHeldAt=Time.unscaledTime;Progress=Mathf.Clamp01(progress);if(!awaitingMovieFrame)Render(Progress,renderedReduced);}
        public void ReleasePreview(){PreviewHeld=false;}
        private void Update(){if(PreviewHeld&&Time.unscaledTime-previewHeldAt>15)ReleasePreview();}
        private void Render(float p,bool reduced)
        {
            renderedReduced=reduced;
            motes.gameObject.SetActive(true);
            impactBack.Render(p,reduced);impactFront.Render(p,reduced);
            if(reduced)
            {
                group.alpha=Mathf.Min(1,p*8,(1-p)*8);moving.anchoredPosition=Vector2.zero;portrait.rectTransform.anchoredPosition=artPosition;illustrationMaterial.SetFloat("_Progress",.44f);motes.Render(.44f,true);return;
            }
            float enter=Mathf.Clamp01(p/.18f),exit=Mathf.Clamp01((p-.79f)/.21f);
            float ease=1-Mathf.Pow(1-enter,3);group.alpha=Mathf.Min(1,enter*2)*(1-exit);
            float hit=Mathf.Clamp01((p-.18f)/.12f);
            float punch=p>=.18f&&p<.30f?Mathf.Sin(hit*Mathf.PI*3)*(1-hit)*8:0;
            moving.anchoredPosition=new Vector2(Mathf.Lerp(-620,0,ease)+exit*760+punch,Mathf.Lerp(-72,0,ease)+exit*90-punch*.35f);
            portrait.rectTransform.anchoredPosition=artPosition+new Vector2((1-ease)*80,0);
            illustrationMaterial.SetFloat("_Progress",p);motes.Render(p,false);
            // One entry and one impact cue per presentation, never per sampled frame.
            if(!PreviewHeld&&p>0&&!entrySound){entrySound=true;if(TacticalDirector.Instance)TacticalDirector.Instance.Audio.PlayTactical("windup",CurrentHero,1.15f);}
            if(!PreviewHeld&&p>=.18f&&!impactSound){impactSound=true;if(TacticalDirector.Instance)TacticalDirector.Instance.Audio.PlayTactical("heavy",CurrentHero,.95f);}
            var shade=veil.color;shade.a=.88f;veil.color=shade;
        }
        public void Cancel()
        {generation++;PreviewHeld=false;awaitingMovieFrame=false;if(actionMovie)actionMovie.Cancel();if(canvas)canvas.gameObject.SetActive(false);}
        private void OnApplicationFocus(bool focused){if(!focused)Cancel();}
        private void OnApplicationPause(bool paused){if(paused)Cancel();}
        private void OnDisable(){Cancel();}
        private void OnDestroy(){Cancel();if(canvas)Destroy(canvas.gameObject);if(illustrationMaterial)Destroy(illustrationMaterial);}
    }

}
