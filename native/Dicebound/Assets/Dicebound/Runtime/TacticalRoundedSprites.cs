using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    internal enum TacticalRoundedKind { Flat, Ink, Paper, Vermilion, Outline, Meter }

    /// <summary>Reusable antialiased nine-slice geometry textures, generated once per treatment.</summary>
    internal static class TacticalRoundedSprites
    {
        private const int Pixels=128;
        private const float Corner=32;
        private static readonly Dictionary<TacticalRoundedKind,Sprite> sprites=new Dictionary<TacticalRoundedKind,Sprite>();
        public static TacticalRoundedKind SkinKind(string skin)
        {
            if(skin=="folio-ivory-v1")return TacticalRoundedKind.Paper;
            if(skin=="button-vermilion-v1")return TacticalRoundedKind.Vermilion;
            if(skin=="lens-jade-v1")return TacticalRoundedKind.Ink;
            if(skin=="panel-ink-v1"||skin=="button-ink-v1")return TacticalRoundedKind.Ink;
            throw new ArgumentException("未知战棋表面："+skin,nameof(skin));
        }
        public static void Apply(Image image,TacticalRoundedKind kind,float radius)
        {
            image.sprite=Get(kind);image.type=Image.Type.Sliced;
            var size=image.rectTransform.sizeDelta;
            radius=Mathf.Clamp(radius,.01f,Mathf.Max(.01f,Mathf.Min(size.x,size.y)*.5f));
            image.pixelsPerUnitMultiplier=Corner/radius;
            var optical=image.GetComponent<TacticalGlassSurface>();if(optical)optical.SetRadius(radius);
        }
        private static Sprite Get(TacticalRoundedKind kind)
        {
            if(sprites.TryGetValue(kind,out var cached)&&cached)return cached;
            var texture=Visuals.Own(new Texture2D(Pixels,Pixels,TextureFormat.RGBA32,true));
            texture.name="Tactical rounded "+kind;texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Trilinear;
            var data=new Color32[Pixels*Pixels];
            for(int y=0;y<Pixels;y++)for(int x=0;x<Pixels;x++)
            {
                Color sum=Color.clear;
                for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)sum+=Sample(kind,x+(sx+.5f)*.5f,y+(sy+.5f)*.5f);
                data[y*Pixels+x]=sum*.25f;
            }
            texture.SetPixels32(data);texture.Apply(true,false);
            var sprite=Visuals.Own(Sprite.Create(texture,new Rect(0,0,Pixels,Pixels),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(Corner,Corner,Corner,Corner)));
            sprite.name="Rounded surface · "+kind;sprites[kind]=sprite;return sprite;
        }
        private static Color Sample(TacticalRoundedKind kind,float x,float y)
        {
            float qx=Mathf.Abs(x-Pixels*.5f)-(Pixels*.5f-Corner),qy=Mathf.Abs(y-Pixels*.5f)-(Pixels*.5f-Corner);
            float distance=Mathf.Min(Mathf.Max(qx,qy),0)+Mathf.Sqrt(Mathf.Max(qx,0)*Mathf.Max(qx,0)+Mathf.Max(qy,0)*Mathf.Max(qy,0))-Corner;
            float alpha=Mathf.Clamp01(.5f-distance);
            if(kind==TacticalRoundedKind.Flat)return new Color(1,1,1,alpha);
            if(kind==TacticalRoundedKind.Outline)return new Color(1,1,1,alpha*(1-Mathf.Clamp01((-distance-.7f)/1.5f)));
            float t=y/Pixels;
            Color top,bottom;
            if(kind==TacticalRoundedKind.Paper){top=TacticalTheme.PaperLight;bottom=TacticalTheme.Paper;}
            else if(kind==TacticalRoundedKind.Vermilion){top=Color.Lerp(TacticalTheme.Vermilion,TacticalTheme.PaperLight,.12f);bottom=Color.Lerp(TacticalTheme.Vermilion,TacticalTheme.Navy,.17f);}
            else if(kind==TacticalRoundedKind.Meter){top=Color.white;bottom=new Color(.72f,.72f,.72f,1);}
            else{top=Color.Lerp(TacticalTheme.Navy,TacticalTheme.JadeRim,.23f);bottom=Color.Lerp(TacticalTheme.Navy,Color.black,.12f);}
            Color color=Color.Lerp(bottom,top,Mathf.SmoothStep(0,1,t));
            // A broad upper bevel and a narrow restrained rim create volume without carved corners.
            float highlight=Mathf.Exp(-Mathf.Pow((Pixels-y-5)/3.5f,2))*(kind==TacticalRoundedKind.Paper?.11f:.23f);
            color=Color.Lerp(color,TacticalTheme.PaperLight,highlight);
            if(kind!=TacticalRoundedKind.Meter)
            {
                float rim=1-Mathf.Clamp01((-distance-.45f)/1.45f);
                color=Color.Lerp(color,kind==TacticalRoundedKind.Paper?TacticalTheme.GoldDeep:TacticalTheme.JadeRim,rim*(.32f+t*.23f));
            }
            color.a=alpha;return color;
        }
    }
}
