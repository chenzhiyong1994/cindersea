using System;
using Dicebound.Tactics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Original production UI textures; source pixels stay intact and are sliced only at runtime.</summary>
    public static class TacticalArt
    {
        [Serializable] private sealed class IconLayout { public int version=0; public IconRegion[] icons=null; }
        [Serializable] private sealed class IconRegion { public string id=null,atlas=null; public int x=0,y=0,width=0,height=0; }
        [Serializable] private sealed class BattleLayout { public int version=0; public IconRegion[] icons=null; public SkinRegion[] skins=null; }
        [Serializable] private sealed class SkinRegion
        {
            public string id=null,atlas=null;
            public int x=0,y=0,width=0,height=0,borderLeft=0,borderBottom=0,borderRight=0,borderTop=0;
        }
        private static readonly Dictionary<string,Sprite> icons=new Dictionary<string,Sprite>();
        private static readonly Dictionary<string,Sprite> battleIcons=new Dictionary<string,Sprite>();
        private static readonly Dictionary<string,Sprite> battleSkins=new Dictionary<string,Sprite>();
        private static Sprite battleLining;
        private static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static int IconCount { get { LoadIcons();return icons.Count; } }
        public static int BattleIconCount { get { LoadBattleArt();return battleIcons.Count; } }
        private static Texture2D Texture(string name)
        {
            if(textures.TryGetValue(name,out var cached))return cached;
            var texture=Resources.Load<Texture2D>("Tactics/UI/Art/"+name);
            if(!texture)throw new InvalidOperationException("缺少精绘界面素材："+name);
            textures.Add(name,texture);return texture;
        }
        private static void LoadIcons()
        {
            if(icons.Count>0)return;
            var source=Resources.Load<TextAsset>("Tactics/UI/icons-layout");
            if(!source)throw new InvalidOperationException("缺少精绘图标布局。");
            var layout=JsonUtility.FromJson<IconLayout>(source.text);
            if(layout==null||layout.version!=1||layout.icons==null)throw new InvalidOperationException("精绘图标布局无效。");
            foreach(var item in layout.icons)
            {
                var texture=Texture(item.atlas);
                if(string.IsNullOrEmpty(item.id)||icons.ContainsKey(item.id)||item.x<0||item.y<0||item.width<1||item.height<1||item.x+item.width>texture.width||item.y+item.height>texture.height)
                    throw new InvalidOperationException("图标范围无效："+item.id);
                var sprite=Sprite.Create(texture,new Rect(item.x,item.y,item.width,item.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
                sprite.name="Painted icon · "+item.id;icons.Add(item.id,sprite);
            }
        }
        public static Sprite Icon(string id)
        { LoadIcons();if(!icons.TryGetValue(id,out var value))throw new InvalidOperationException("缺少精绘图标："+id);return value; }
        private static void LoadBattleArt()
        {
            if(battleIcons.Count>0)return;
            var source=Resources.Load<TextAsset>("Tactics/UI/battle-v2-layout");
            if(!source)throw new InvalidOperationException("缺少正式战斗界面切片记录。");
            var layout=JsonUtility.FromJson<BattleLayout>(source.text);
            if(layout==null||layout.version!=1||layout.icons==null||layout.icons.Length!=20||layout.skins==null||layout.skins.Length!=4)
                throw new InvalidOperationException("正式战斗界面切片记录无效。");
            var loadedIcons=new Dictionary<string,Sprite>();var loadedSkins=new Dictionary<string,Sprite>();
            foreach(var item in layout.icons)
            {
                var texture=Texture(item.atlas);
                if(string.IsNullOrEmpty(item.id)||loadedIcons.ContainsKey(item.id)||!Inside(texture,item.x,item.y,item.width,item.height)||item.width!=item.height)
                    throw new InvalidOperationException("独立技能图标范围无效："+item.id);
                var sprite=Visuals.Own(Sprite.Create(texture,new Rect(item.x,item.y,item.width,item.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect));
                sprite.name="Battle painted icon · "+item.id;loadedIcons.Add(item.id,sprite);
            }
            foreach(var item in layout.skins)
            {
                var texture=Texture(item.atlas);
                if(string.IsNullOrEmpty(item.id)||loadedSkins.ContainsKey(item.id)||!Inside(texture,item.x,item.y,item.width,item.height)||item.borderLeft<0||item.borderBottom<0||item.borderRight<0||item.borderTop<0||item.borderLeft+item.borderRight>=item.width||item.borderBottom+item.borderTop>=item.height)
                    throw new InvalidOperationException("正式战斗皮肤范围无效："+item.id);
                var border=new Vector4(item.borderLeft,item.borderBottom,item.borderRight,item.borderTop);
                var sprite=Visuals.Own(Sprite.Create(texture,new Rect(item.x,item.y,item.width,item.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,border));
                sprite.name="Battle painted skin · "+item.id;loadedSkins.Add(item.id,sprite);
            }
            foreach(var item in loadedIcons)battleIcons.Add(item.Key,item.Value);
            foreach(var item in loadedSkins)battleSkins.Add(item.Key,item.Value);
            // A single quiet interior sample, kept separate from the authored metal border.
            // Never tile the etched lining or sample the decorative corners underneath text.
            var lining=Array.Find(layout.skins,item=>item.id=="inset");var inset=80;
            if(lining==null||lining.width<=inset*2||lining.height<=inset*2)throw new InvalidOperationException("正式界面缺少安静墨色内衬。");
            battleLining=Visuals.Own(Sprite.Create(Texture(lining.atlas),new Rect(lining.x+inset,lining.y+inset,lining.width-inset*2,lining.height-inset*2),Vector2.one*.5f,100,0,SpriteMeshType.FullRect));
            battleLining.name="Battle quiet ink lining";
        }
        private static bool Inside(Texture2D texture,int x,int y,int width,int height)=>x>=0&&y>=0&&width>0&&height>0&&x+width<=texture.width&&y+height<=texture.height;
        public static Image BattleSymbol(string name,Transform parent,string id,Vector2 pos,Vector2 size)
        {
            LoadBattleArt();
            if(!battleIcons.TryGetValue(id,out var sprite))return Symbol(name,parent,id,pos,size);
            var image=Ui.Panel(name,parent,Vector2.one*.5f,pos,size,Color.white);
            image.sprite=sprite;image.preserveAspect=true;return image;
        }
        public static Image Symbol(string name,Transform parent,string id,Vector2 pos,Vector2 size)
        {
            LoadIcons();
            if(id.StartsWith("relic-")&&!icons.ContainsKey(id))
            {
                var relic=TacticalContent.GetRelic(id.Substring(6));
                if(relic!=null)id=RelicSymbolId(relic.id);
            }
            var image=Ui.Panel(name,parent,Vector2.one*.5f,pos,size,Color.white);
            image.sprite=Icon(id);image.preserveAspect=true;return image;
        }
        public static string RelicSymbolId(string id)
        {
            LoadIcons();if(icons.ContainsKey("relic-"+id))return "relic-"+id;
            switch(id)
            {
                case "blanket":case "immunitytalisman":return "relic-flask";
                case "rubberboots":case "echoshell":case "moonledger":return "relic-pearl";
                case "whetstone":case "brokenscabbard":case "bladetsuba":case "doublepen":case "verdictpen":return "relic-needle";
                case "emberfurnace":case "overheatvalve":case "sunwheelshard":return "relic-lamp";
                case "heavywheel":case "valvecore":case "anchorhammer":case "wrench":case "shoulderline":case "brasshorn":return "relic-bell";
                default:return "relic-seal";
            }
        }
        public static Image SkillSymbol(string name,Transform parent,string id,Vector2 pos,float size)
        {
            LoadBattleArt();LoadIcons();
            if(!battleIcons.ContainsKey(id)&&!icons.ContainsKey(id))
            {
                var skill=TacticalContent.GetSkill(id);
                id=skill?.resource=="charge"?"oath":skill?.kind=="heal"?"mend":skill?.kind=="guard"?"brace":skill?.heroId=="sixuan"?"balance":skill?.heroId=="yanzhuying"?"moon":skill?.heroId=="lingfeng"?"cleave":skill?.heroId=="cangling"?"tide":skill?.heroId=="shangshuo"?"anchor":"strike";
            }
            return BattleSymbol(name,parent,id,pos,Vector2.one*size);
        }
        public static Image Plate(string name,Transform parent,Vector2 pos,Vector2 size,Color color,float radius=12,bool raycast=false)
        {
            var image=Ui.Panel(name,parent,Vector2.one*.5f,pos,size,color,raycast);
            TacticalRoundedSprites.Apply(image,TacticalRoundedKind.Flat,radius);return image;
        }
        public static readonly Color ShieldMeterColor=new Color(.40f,.51f,.98f);
        public static readonly Color ShieldLabelColor=new Color(.73f,.80f,1);
        public static void HealthShieldMeter(string name,Transform parent,Vector2 position,Vector2 size,int health,int maximum,int shield,Color life)
        {
            float scale=Mathf.Max(1,maximum,health+shield),lifeWidth=size.x*Mathf.Max(0,health)/scale,shieldWidth=size.x*Mathf.Max(0,shield)/scale;
            Plate(name+" track",parent,position,size+Vector2.one*2,TacticalTheme.HudMeterTrack,2);
            float left=position.x-size.x*.5f;
            if(lifeWidth>0)Plate(name+" health",parent,new Vector2(left+lifeWidth*.5f,position.y),new Vector2(lifeWidth,size.y),life,1);
            if(shieldWidth>0)
            {
                Plate(name+" shield",parent,new Vector2(left+lifeWidth+shieldWidth*.5f,position.y),new Vector2(shieldWidth,size.y),ShieldMeterColor,1);
                if(lifeWidth>0)
                {
                    Plate(name+" boundary",parent,new Vector2(left+lifeWidth,position.y),new Vector2(3,size.y+4),new Color(.035f,.055f,.10f),0);
                    Plate(name+" boundary glint",parent,new Vector2(left+lifeWidth,position.y),new Vector2(1,size.y+2),new Color(.94f,.96f,1),0);
                }
            }
        }
        public static Image FieldSurface(string name,Transform parent,Vector2 pos,Vector2 size,Color? fill=null,Color? rim=null,float radius=5,bool raycast=false)
        {
            var image=Ui.Panel(name,parent,Vector2.one*.5f,pos,size,Color.clear,raycast);
            var frame=TacticalMenuArt.CraftImage("Authored metal rim",image.transform,"inset-panel",Vector2.zero,size,false,true);
            frame.rectTransform.anchorMin=Vector2.zero;frame.rectTransform.anchorMax=Vector2.one;frame.rectTransform.sizeDelta=Vector2.zero;
            image.gameObject.AddComponent<TacticalBattleFrame>().Configure(fill==TacticalTheme.HudSelected||(rim.HasValue&&rim.Value==TacticalTheme.HudSelectedRule));
            return image;
        }        internal static Image Outline(string name,Transform parent,Vector2 pos,Vector2 size,Color color,float radius,bool raycast=false)
        {
            var image=Plate(name,parent,pos,size,color,radius,raycast);
            TacticalRoundedSprites.Apply(image,TacticalRoundedKind.Outline,radius);return image;
        }
        public static RawImage CropPicture(string name,Transform parent,Texture texture,Vector2 pos,Vector2 size,float radius=20,Color? tint=null)
        {
            var clip=Plate(name+" rounded clip",parent,pos,size,Color.white,radius);
            var mask=clip.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var image=Ui.Picture(name,clip.transform,texture,Vector2.one*.5f,Vector2.zero,size,tint??Color.white);
            Ui.Crop(image);return image;
        }
        public static Image Surface(string name,Transform parent,Vector2 pos,Vector2 size,string skin,bool raycast=false)
        {
            var image=Ui.Panel(name,parent,Vector2.one*.5f,pos,size,Color.white,raycast);
            bool button=skin.StartsWith("button-",StringComparison.Ordinal);
            bool lens=skin=="lens-jade-v1";
            float radius=lens?Mathf.Min(size.x,size.y)*.5f:Mathf.Min(button?16:skin=="folio-ivory-v1"?28:24,Mathf.Min(size.x,size.y)*.4f);
            // Keep a white silhouette for native Mask compatibility; the shader owns all optical shading.
            TacticalRoundedSprites.SkinKind(skin);
            TacticalRoundedSprites.Apply(image,TacticalRoundedKind.Flat,radius);
            var shadow=image.gameObject.AddComponent<TacticalSurfaceShadow>();shadow.Radius=radius;shadow.Blur=button?10:14;shadow.Drop=button?4:6;shadow.Strength=button?.28f:.24f;
            var kind=skin=="folio-ivory-v1"?TacticalGlassKind.Pearl:skin=="button-vermilion-v1"?TacticalGlassKind.Cinnabar:lens?TacticalGlassKind.Lens:button?TacticalGlassKind.JadePolish:TacticalGlassKind.JadeGlass;
            image.gameObject.AddComponent<TacticalGlassSurface>().Configure(kind,radius);
            return image;
        }
    }
}
