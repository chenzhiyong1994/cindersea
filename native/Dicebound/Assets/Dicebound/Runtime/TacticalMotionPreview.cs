using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Owns the preview's material and decoder; destroying the modal releases both.</summary>
    public sealed class TacticalMotionPreview : MonoBehaviour
    {
        private TacticalMenuMotion motion;
        private Material material;
        private Text status;
        public void Initialize(RawImage picture,Text label,string file,bool packed=true)
        {
            status=label;
            if(packed)
            {
                var shader=Resources.Load<Shader>("Shaders/Tactics/MenuPackedVideo");
                if(!shader||!shader.isSupported){status.text="动态暂不可用，显示定格画面。";return;}
                material=new Material(shader){name="Companion preview packed coverage",hideFlags=HideFlags.DontSave};
            }
            motion=gameObject.AddComponent<TacticalMenuMotion>();
            motion.Bind(picture,file,material:material,packedColorAspect:packed?16f/9f:0);
        }
        private void Update()
        {
            if(!status||!motion)return;
            string message=TacticalDirector.Instance&&TacticalDirector.Instance.ReducedMotion?"减少动态效果已开启，显示定格画面。":motion.Failed?"动态暂不可用，显示定格画面。":motion.IsDisplayingMovie?"全画幅立绘 · 静音循环":"正在载入动态…";
            if(status.text!=message)status.text=message;
        }
        private void OnDestroy(){if(motion)motion.Dispose();if(material)Destroy(material);}
    }
}
