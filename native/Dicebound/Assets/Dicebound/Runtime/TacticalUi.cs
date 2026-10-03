using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    // Shared tactical controls use the authored jade-and-gold materials.
    public static class TacticalUi
    {
        private static readonly Vector2 Center=Vector2.one*.5f;
        public static readonly Color Gold=TacticalTheme.Gold;
        public static readonly Color Muted=TacticalTheme.OnDarkSub;
        public static readonly Color Teal=TacticalTheme.Ally;
        /// <summary>Exact battlefield text bounds; callers reserve the complete CJK line height.</summary>
        public static Text FieldLabel(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var rect=Ui.Rect(name,parent,Center,pos,size);
            var label=rect.gameObject.AddComponent<Text>();label.font=Ui.Font;label.text=text;label.fontSize=fontSize;label.color=color;label.alignment=alignment;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.supportRichText=false;label.lineSpacing=1;
            return label;
        }
        public static Button Button(string name,Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,TacticalButtonVariant variant=TacticalButtonVariant.Secondary,string icon=null,int fontSize=0,TacticalSurfaceContext context=TacticalSurfaceContext.Dark,bool field=false)
        {
            if(!field)return TacticalMenuArt.TextButton(name,parent,text,pos,size,action,variant==TacticalButtonVariant.Primary,fontSize:fontSize>0?fontSize:TacticalTheme.FontButton);
            bool quiet=variant==TacticalButtonVariant.Quiet;
            var image=field?TacticalArt.FieldSurface(name,parent,pos,size,variant==TacticalButtonVariant.Primary?TacticalTheme.HudSelected:quiet?TacticalTheme.HudInset:TacticalTheme.HudInk,variant==TacticalButtonVariant.Primary?TacticalTheme.HudSelectedRule:TacticalTheme.HudRule,5,true):quiet?TacticalArt.Outline(name,parent,pos,size,context==TacticalSurfaceContext.Light?TacticalTheme.GoldDeep:TacticalTheme.JadeRim,Mathf.Min(16,size.y*.4f),true):TacticalArt.Surface(name,parent,pos,size,variant==TacticalButtonVariant.Primary?"button-vermilion-v1":"button-ink-v1",true);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            if(field)button.transition=Selectable.Transition.None;
            if(field)image.GetComponent<TacticalBattleFrame>().Configure(variant==TacticalButtonVariant.Primary,false,true);
            var colors=button.colors;colors.normalColor=TacticalTheme.ButtonNormal;colors.highlightedColor=TacticalTheme.ButtonHover;colors.pressedColor=TacticalTheme.ButtonPressed;colors.disabledColor=field?TacticalTheme.HudButtonDisabled:TacticalTheme.ButtonDisabled;colors.fadeDuration=TacticalTheme.ButtonFadeDuration;button.colors=colors;
            bool illustrated=!string.IsNullOrEmpty(icon),labelled=!string.IsNullOrEmpty(text);
            float paddingX=size.x<=TacticalTheme.Space64?TacticalTheme.Space8:TacticalTheme.ButtonPaddingX;
            float iconSize=Mathf.Min(TacticalTheme.ButtonIconSize,Mathf.Max(0,size.y-TacticalTheme.Space16));
            float reservation=illustrated&&labelled?iconSize+TacticalTheme.ButtonIconGap:0;
            if(illustrated){var position=new Vector2(labelled?-size.x*.5f+paddingX+iconSize*.5f:0,0);if(field)TacticalArt.BattleSymbol(name+" emblem",image.transform,icon,position,Vector2.one*iconSize);else TacticalArt.Symbol(name+" emblem",image.transform,icon,position,Vector2.one*iconSize);}
            if(labelled)
            {
                int sizeFont=fontSize>0?fontSize:quiet?TacticalTheme.FontBody:TacticalTheme.FontButton;
                var labelSize=new Vector2(Mathf.Max(0,size.x-paddingX*2-reservation),size.y-TacticalTheme.ButtonPaddingY*2);
                var labelColor=quiet?(context==TacticalSurfaceContext.Light?TacticalTheme.Ink:TacticalTheme.OnDarkSub):TacticalTheme.OnDark;
                if(field)FieldLabel("Label",image.transform,text,new Vector2(reservation*.5f,0),labelSize,sizeFont,labelColor,TextAnchor.MiddleCenter);
                else Ui.Heading("Label",image.transform,text,Center,new Vector2(reservation*.5f,0),labelSize,sizeFont,labelColor);
            }
            var motion=image.gameObject.AddComponent<UiButtonMotion>();motion.HoverScale=TacticalTheme.ButtonHoverScale;motion.PressedScale=TacticalTheme.ButtonPressedScale;
            image.gameObject.AddComponent<TacticalButtonVisual>();
            if(action!=null)button.onClick.AddListener(action);return button;
        }
        public static Button FieldButton(string name,Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,bool selected=false,string icon=null,int fontSize=TacticalTheme.FontButton,bool subtle=false)
        {
            size.y=Mathf.Max(TacticalTheme.BattleToolbarHeight,size.y);
            var button=Button(name,parent,text,pos,size,action,selected?TacticalButtonVariant.Primary:TacticalButtonVariant.Secondary,icon,fontSize,TacticalSurfaceContext.Dark,true);
            button.GetComponent<TacticalBattleFrame>().Configure(selected,subtle,true);
            return button;
        }
        public static void HoverHint(Button button,UnityEngine.Events.UnityAction enter,UnityEngine.Events.UnityAction exit)
        {var hint=button.gameObject.AddComponent<TacticalHoverHint>();hint.Enter=enter;hint.Exit=exit;}
        // Temporary call compatibility; icons are always explicit and never derived from object names.
        public static Button Button(string name,Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,bool primary,string icon=null,int fontSize=0,TacticalSurfaceContext context=TacticalSurfaceContext.Dark)
        {return Button(name,parent,text,pos,size,action,primary?TacticalButtonVariant.Primary:TacticalButtonVariant.Secondary,icon,fontSize,context);}
        public static RawImage Portrait(string name,Transform parent,Texture2D texture,string hero,Vector2 pos,Vector2 size,bool face=false,float border=TacticalTheme.PortraitBorder,Color? frameColor=null,Color? tint=null,float radius=20,bool field=false)
        {
            if(border>0)
            {
                if(field)TacticalArt.FieldSurface(name+" rounded frame",parent,pos,size+Vector2.one*(border*2),TacticalTheme.HudInk,frameColor??TacticalTheme.HudRule,radius+border);
                else{var frame=TacticalArt.Surface(name+" rounded frame",parent,pos,size+Vector2.one*(border*2),"panel-ink-v1");frame.color=frameColor??TacticalTheme.SkinFrame;TacticalRoundedSprites.Apply(frame,TacticalRoundedKind.Ink,radius+border);var shadow=frame.GetComponent<TacticalSurfaceShadow>();shadow.Radius=radius+border;shadow.Blur=8;shadow.Drop=3;shadow.Strength=.22f;}
            }
            var image=TacticalArt.CropPicture(name,parent,texture,pos,size,radius,tint??TacticalTheme.SkinFrame);
            if(!texture)return image;
            if(face)
            {
                bool paper=Resources.Load<Texture2D>("Art/Paper/Heroes/"+hero);
                var region=paper?new Rect(.22f,.53f,.56f,.46f):JinhaiArt.Face(hero);
                float aspect=(float)texture.width/texture.height,target=size.x/size.y;
                float height=region.height,width=height*target/aspect;
                if(width>region.width){width=region.width;height=width*aspect/target;}
                image.uvRect=new Rect(region.center.x-width*.5f,region.center.y-height*.5f,width,height);
            }
            else Ui.Crop(image);
            return image;
        }
        public static void Meter(string name,Transform parent,float x,float y,float width,float height,float fraction,Color color,Color? trackColor=null,float border=TacticalTheme.MeterBorder,bool field=false)
        {
            TacticalArt.Plate(name+" track",parent,new Vector2(x,y),new Vector2(width+border*2,height+border*2),trackColor??(field?TacticalTheme.HudMeterTrack:TacticalTheme.Inset),(height+border*2)*.5f);
            float fill=width*Mathf.Clamp01(fraction);
            if(fill>0){var image=TacticalArt.Plate(name+" fill",parent,new Vector2(x-(width-fill)*.5f,y),new Vector2(fill,height),color,field?1:height*.5f);if(!field)TacticalRoundedSprites.Apply(image,TacticalRoundedKind.Meter,height*.5f);}
        }
        // x is the fixed area's left edge. Bonus AP above maximum is legal and remains visible.
        public static void Pips(string name,Transform parent,float x,float y,int amount,int maximum,Color color,string icon="ap",float width=TacticalTheme.ResourcePipsWidth)
        {
            int count=Mathf.Max(0,Mathf.Max(amount,maximum));if(count==0||width<=0)return;
            float step=Mathf.Min(TacticalTheme.ResourcePipStep,width/count);
            float size=Mathf.Min(TacticalTheme.ResourcePipSize,Mathf.Max(0,step-TacticalTheme.ResourcePipGap));
            float first=x+width-step*count+step*.5f;
            for(int i=0;i<count;i++)
            {var pip=TacticalArt.Symbol(name+" "+i,parent,icon,new Vector2(first+i*step,y),Vector2.one*size);pip.color=i<amount?color:TacticalTheme.ResourceEmpty;}
        }
        public static string Role(string id)=>id=="sixuan"?"控制 · 定身敌人":id=="yanzhuying"?"刺击 · 借影突袭":id=="lingfeng"?"近战 · 击退破阵":id=="cangling"?"远攻 · 水雾施术":id=="shangshuo"?"守护 · 群体护盾":"治疗 · 支援同伴";
        public static string Advice(string id)=>id=="sixuan"?"用衡域限制远处敌人，再与同伴一起进攻。":id=="yanzhuying"?"借阴影靠近敌人，以缺月针打出高伤害。":id=="lingfeng"?"近身挥刀，把敌人推向墙壁或其他敌人。":id=="cangling"?"站进水雾，用静海界远攻并击退敌人。":id=="shangshuo"?"站在同伴身边展开护盾，稳住阵线。":"为受伤的同伴治疗，让全队继续前进。";
    }

    public sealed class TacticalHoverHint:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public UnityEngine.Events.UnityAction Enter,Exit;
        public void OnPointerEnter(PointerEventData data){Enter?.Invoke();}
        public void OnPointerExit(PointerEventData data){Exit?.Invoke();}
    }

    /// <summary>Applies the tactical disabled state to late-added labels/icons without a second group fade.</summary>
    public sealed class TacticalButtonVisual:MonoBehaviour
    {
        private sealed class GraphicColor
        {
            public Color Original,Applied;
            public GraphicColor(Color color){Original=Applied=color;}
        }
        private Button button;
        private readonly List<Graphic> graphics=new List<Graphic>();
        private readonly Dictionary<Graphic,GraphicColor> colors=new Dictionary<Graphic,GraphicColor>();
        private void Awake(){button=GetComponent<Button>();}
        private void LateUpdate()
        {
            if(!button)return;
            graphics.Clear();GetComponentsInChildren(true,graphics);
            bool disabled=!button.IsInteractable();
            foreach(var graphic in graphics)
            {
                if(!graphic||graphic==button.targetGraphic)continue;
                if(!colors.TryGetValue(graphic,out var state)){state=new GraphicColor(graphic.color);colors.Add(graphic,state);}
                // A caller may change a label colour or append an icon while already disabled.
                else if(graphic.color!=state.Applied)state.Original=graphic.color;
                var target=disabled?state.Original*button.colors.disabledColor:state.Original;
                if(graphic.color!=target)graphic.color=target;
                state.Applied=target;
            }
        }
        private void OnDisable()
        {
            foreach(var item in colors)if(item.Key)item.Key.color=item.Value.Original;
            colors.Clear();
        }
    }
}
