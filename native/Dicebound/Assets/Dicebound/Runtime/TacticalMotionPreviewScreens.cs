using Dicebound.Tactics;
using UnityEngine;

namespace Dicebound.Presentation
{
    public sealed partial class TacticalDirector
    {
        public void PreviewCompanionMotion(string heroId)
        {
            var hero=TacticalContent.GetHero(heroId);string file=TacticalMenuMotion.CompanionMovieFile(heroId);
            if(hero==null||file==null)return;
            var frame=Overlay(hero.name+" · 全画幅欣赏",1560,980);
            var poster=TacticalMenuMotion.CompanionPoster(heroId);
            var picture=Ui.Picture("Companion motion preview",frame,poster,Center,Vector2.zero,new Vector2(1216,684),Color.white);
            picture.uvRect=new Rect(0,0,1,1);
            var status=Label("Companion motion status",frame,"正在载入动态…",0,-364,1280,40,23,TacticalMenuArt.Muted,TextAnchor.MiddleCenter);
            var preview=frame.gameObject.AddComponent<TacticalMotionPreview>();
            preview.Initialize(picture,status,file,TacticalMenuMotion.CompanionUsesPackedMovie(heroId));
        }
    }

}
