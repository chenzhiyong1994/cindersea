using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Displays the authored material with transparent cut corners and an opaque ink centre.</summary>
    public sealed class TacticalBattleFrame:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        private Image rim;
        private Button button;
        private bool selected,subtle,hovered,wasInteractable=true;

        public void Configure(bool selected,bool subtle=false,bool interactive=false)
        {
            var border=transform.Find("Authored metal rim");rim=border?border.GetComponent<Image>():null;
            var size=((RectTransform)transform).rect.size;bool longButton=interactive&&size.x>size.y*2.4f;
            var jade=longButton?TacticalMenuArt.CraftSprite(selected?"primary-button":"secondary-button"):TacticalMenuArt.JadeFrameSprite();
            if(rim&&jade){rim.sprite=jade;rim.type=longButton?Image.Type.Simple:Image.Type.Sliced;rim.fillCenter=true;rim.preserveAspect=false;rim.pixelsPerUnitMultiplier=4;}
            button=interactive?GetComponent<Button>():null;
            this.selected=selected;this.subtle=subtle;hovered=false;
            wasInteractable=!button||button.IsInteractable();Refresh();
        }
        public void OnPointerEnter(PointerEventData data)
        {
            if(!button||!button.IsInteractable()||hovered)return;
            hovered=true;Refresh();
        }
        public void OnPointerExit(PointerEventData data)
        {
            if(!hovered)return;
            hovered=false;Refresh();
        }
        private void LateUpdate()
        {
            if(!button)return;
            bool available=button.IsInteractable();
            if(available==wasInteractable)return;
            wasInteractable=available;if(!available)hovered=false;Refresh();
        }
        private void OnDisable(){hovered=false;Refresh();}
        private void Refresh()
        {
            if(!rim)return;
            float opacity=wasInteractable?1:.48f;
            // The sprite supplies the metal/jade pixels; interaction only changes
            // their visibility, without recoloring the opaque text backing.
            float light=hovered?1.14f:selected?1.06f:1;rim.color=new Color(light,light,light,opacity);
        }
    }
}
