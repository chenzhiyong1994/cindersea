using UnityEngine;

namespace Dicebound.Presentation
{
    public static class JinhaiArt
    {
        // UV portraits keep their native aspect ratio; full-body art remains untouched.
        public static Rect Face(string id)
        {
            if(Resources.Load<Texture2D>("Art/Paper/Heroes/"+id))return new Rect(.24f,.62f,.50f,.375f);
            return id=="shangshuo"?new Rect(.332f,.711f,.225f,.165f):id=="ruanzhuo"?new Rect(.412f,.776f,.234f,.167f):new Rect(.24f,.62f,.50f,.375f);
        }
        public static Color Accent(string id) { return id=="sixuan"?new Color(.67f,.57f,.78f):id=="yanzhuying"?new Color(.62f,.24f,.29f):id=="lingfeng"?new Color(.88f,.40f,.28f):id=="ruanzhuo"?new Color(.91f,.65f,.31f):id=="cangling"?new Color(.43f,.79f,.85f):new Color(.65f,.76f,.85f); }
    }
}
