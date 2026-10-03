using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    public static class TacticalMenuArt
    {
        public const string BackgroundResource="Tactics/Jade/title-environment";
        public static readonly Color Ink=new Color(.025f,.047f,.048f,.98f);
        public static readonly Color RaisedInk=new Color(.06f,.13f,.12f,.98f);
        public static readonly Color Gold=new Color(.96f,.81f,.53f);
        public static readonly Color Text=new Color(.98f,.93f,.80f);
        public static readonly Color Muted=new Color(.75f,.75f,.64f);
        public static readonly Color Accent=new Color(.47f,.85f,.73f);
        private static readonly Vector2 Center=Vector2.one*.5f;
        private static readonly Dictionary<string,Sprite> craft=new Dictionary<string,Sprite>();
        private static Font brush;
        public static Font Brush {get{if(!brush)brush=Resources.Load<Font>("Fonts/MaShanZheng-Regular")??Ui.DisplayFont;return brush;}}

        public static Sprite CraftSprite(string key)
        {
            if(craft.TryGetValue(key,out var existing))return existing;
            var texture=Resources.Load<Texture2D>("Tactics/JadeCraft/"+key);if(!texture)return null;
            var rect=new Rect(0,0,texture.width,texture.height);
            // Authored sources stay intact. These are packaging bounds, not a repaint.
            if(key=="inset-panel")rect=new Rect(20,208,1496,606);
            if(key=="primary-button")rect=new Rect(60,138,1926,503);
            if(key=="secondary-button")rect=new Rect(134,168,1746,464);
            if(key=="menu-focus")rect=new Rect(148,268,1893,200);
            if(key=="title-logo")rect=new Rect(70,17,2047,656);
            if(key=="title-ornament")rect=new Rect(185,123,354,1928);
            if(key=="panel-tall")rect=new Rect(40,72,944,1420);
            float edge=Mathf.Min(rect.width,rect.height)*.17f;
            var sprite=Visuals.Own(Sprite.Create(texture,rect,Center,100,0,SpriteMeshType.FullRect,new Vector4(edge,edge,edge,edge)));
            craft[key]=sprite;return sprite;
        }
        public static Image CraftImage(string name,Transform parent,string key,Vector2 pos,Vector2 size,bool raycast=false,bool sliced=false)
        {
            var image=Ui.Panel(name,parent,Center,pos,size,Color.white,raycast);
            image.sprite=CraftSprite(key);image.type=sliced?Image.Type.Sliced:Image.Type.Simple;
            image.preserveAspect=!sliced;image.pixelsPerUnitMultiplier=sliced?4:1;
            if(!image.sprite)image.color=Ink;
            return image;
        }
        public static Sprite JadeFrameSprite()=>CraftSprite("inset-panel");

        public static Image QuietPanel(string name,Transform parent,Vector2 pos,Vector2 size,bool raycast=false,bool selected=false)
        {
            bool tall=size.y>440&&size.x<780;
            var image=CraftImage(name,parent,tall?"panel-tall":"inset-panel",pos,size,raycast,!tall);
            if(tall)image.preserveAspect=false;
            if(selected)image.color=new Color(1.16f,1.12f,.98f);
            return image;
        }
        public static RawImage HeroPortrait(string name,Transform parent,string id,Vector2 pos,Vector2 size)
        {return TacticalUi.Portrait(name,parent,Resources.Load<Texture2D>("Art/Paper/Heroes/"+id),id,pos,size,true,0,radius:1);}

        public static Text Heading(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fontSize=32,bool gold=false)
        {
            var heading=Ui.Heading(name,parent,text,Center,pos,size,Mathf.Max(22,fontSize),gold?Gold:Text);
            heading.font=Brush;heading.alignment=TextAnchor.MiddleLeft;
            var shadow=heading.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.005f,.015f,.018f,.85f);shadow.effectDistance=new Vector2(1.2f,-1.6f);shadow.useGraphicAlpha=true;
            return heading;
        }
        public static Text Label(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fontSize=22,Color? color=null,TextAnchor alignment=TextAnchor.UpperLeft)
        {return Ui.Label(name,parent,text,Center,pos,size,Mathf.Max(22,fontSize),color??Text,alignment);}

        public static Image Decoration(string name,Transform parent,string key,Vector2 pos,Vector2 size,Color? tint=null)
        {
            string cacheKey="ornament-"+key;
            if(!craft.TryGetValue(cacheKey,out var sprite))
            {
                var texture=Resources.Load<Texture2D>("Tactics/JadeCraft/ornament-atlas");
                if(texture){var top=key=="divider"?new Rect(639,301,604,74):key=="seal"?new Rect(168,736,289,290):key=="medallion"?new Rect(168,642,289,595):key=="workshop"?new Rect(657,705,563,509):new Rect(5,241,621,173);
                    sprite=Visuals.Own(Sprite.Create(texture,new Rect(top.x,texture.height-top.y-top.height,top.width,top.height),Center,100));craft[cacheKey]=sprite;}
            }
            var image=Ui.Panel(name,parent,Center,pos,size,tint??Color.white);image.sprite=sprite;image.preserveAspect=true;
            if(!sprite)image.color=Color.clear;return image;
        }
        public static Image Rule(string name,Transform parent,Vector2 pos,float width,Color? color=null)
        {return Decoration(name,parent,"divider",pos,new Vector2(width,30),color??new Color(1,1,1,.7f));}

        public static Button TextButton(string name,Transform parent,string text,Vector2 pos,Vector2 size,UnityAction action,bool primary=false,bool selected=false,int fontSize=26)
        {
            bool square=size.y>size.x*.52f;
            bool wide=size.x>size.y*6;
            var image=CraftImage(name,parent,square?"inset-panel":primary||selected?"primary-button":"secondary-button",pos,size,true,square||wide);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;
            float inset=square?Mathf.Min(20,size.x*.2f):primary?80:50;
            var label=Heading("Label",image.transform,text,new Vector2(0,1),size-new Vector2(inset,8),fontSize,primary||selected);label.alignment=TextAnchor.MiddleCenter;
            if(text=="×")label.font=Ui.Font;
            image.gameObject.AddComponent<TacticalMenuButtonVisual>().Configure(button,label,image,primary||selected);
            if(action!=null)button.onClick.AddListener(action);return button;
        }
        public static Button MenuAction(string name,Transform parent,string text,Vector2 pos,Vector2 size,UnityAction action,bool selected=false)
        {
            var hit=Ui.Panel(name,parent,Center,pos,size,Color.clear,true);
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;
            var highlight=CraftImage("Bronze selected illumination",hit.transform,"menu-focus",Vector2.zero,size);
            var label=Heading("Label",hit.transform,text,new Vector2(34,0),size-new Vector2(120,4),37,true);label.alignment=TextAnchor.MiddleLeft;
            hit.gameObject.AddComponent<TacticalMenuButtonVisual>().Configure(button,label,highlight,selected,true);
            if(action!=null)button.onClick.AddListener(action);return button;
        }

        public static RawImage Backdrop(string name,Transform parent,Texture texture)=>DrawBackdrop(name,parent,texture,.16f);
        public static RawImage PlateBackdrop(string name,Transform parent,string resource,float bottomShade=.15f)
        {return DrawBackdrop(name,parent,Resources.Load<Texture2D>("Tactics/Jade/"+resource)??Resources.Load<Texture2D>(BackgroundResource),bottomShade);}
        private static RawImage DrawBackdrop(string name,Transform parent,Texture texture,float bottomShade)
        {
            var image=Ui.Picture(name,parent,texture,Center,Vector2.zero,PaperViewport.DesignSize,Color.white);Ui.Crop(image);
            if(bottomShade>0)Ui.Fade(name+" lower shade",parent,Center,new Vector2(0,-350),new Vector2(1920,380),new Color(Ink.r,Ink.g,Ink.b,bottomShade),false,true);
            Shader.SetGlobalTexture("_TacticalGlassBackdrop",image.texture);
            Shader.SetGlobalVector("_TacticalGlassBackdropUV",new Vector4(image.uvRect.x,image.uvRect.y,image.uvRect.width,image.uvRect.height));
            Shader.SetGlobalColor("_TacticalGlassBackdropColor",image.color);Shader.SetGlobalColor("_TacticalGlassBackdropWash",Color.clear);return image;
        }
    }
    public sealed class TacticalMenuButtonVisual:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler
    {
        private Button button;private Text label;private Image art;private bool selected,menu,hovered,focused,pressed,available;
        public void Configure(Button button,Text label,Image art,bool selected,bool menu=false)
        {this.button=button;this.label=label;this.art=art;this.selected=selected;this.menu=menu;available=button.IsInteractable();Apply();}
        private void LateUpdate(){if(button&&button.IsInteractable()!=available){available=button.IsInteractable();if(!available)pressed=false;Apply();}}
        private void Apply()
        {
            if(!label)return;bool light=available&&(hovered||focused||selected);
            float gain=pressed?.83f:hovered||focused?1.12f:1;
            art.color=new Color(gain,gain,gain,available?(menu?(light?1:0):1):.45f);
            label.color=available?(light?TacticalMenuArt.Gold:TacticalMenuArt.Text):new Color(.66f,.65f,.56f,.8f);

        }
        public void OnPointerEnter(PointerEventData data){hovered=true;Apply();}
        public void OnPointerExit(PointerEventData data){hovered=false;pressed=false;Apply();}
        public void OnPointerDown(PointerEventData data){if(data.button==PointerEventData.InputButton.Left){pressed=true;Apply();}}
        public void OnPointerUp(PointerEventData data){pressed=false;Apply();}
        public void OnSelect(BaseEventData data){focused=true;Apply();}
        public void OnDeselect(BaseEventData data){focused=false;pressed=false;Apply();}
    }
}
