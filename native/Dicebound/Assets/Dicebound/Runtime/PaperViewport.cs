using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>One letterboxed design area for the camera, paper UI and pointer coordinates.</summary>
    public static class PaperViewport
    {
        public static readonly Vector2 DesignSize = new Vector2(1920, 1080);
        public const float Aspect = 16f / 9f;
        public static Rect Pixels => Fit(Screen.width, Screen.height);

        public static Rect Fit(float width, float height)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            float scale = Mathf.Min(width / DesignSize.x, height / DesignSize.y);
            var size = DesignSize * scale;
            return new Rect((width - size.x) * .5f, (height - size.y) * .5f, size.x, size.y);
        }

        public static bool Contains(Vector2 screenPoint) => Pixels.Contains(screenPoint);
        public static Vector2 ToViewport(Vector2 screenPoint)
        {
            var area = Pixels;
            return new Vector2((screenPoint.x - area.x) / area.width, (screenPoint.y - area.y) / area.height);
        }
        public static Vector2 ToScreen(Vector2 viewportPoint)
        {
            var area = Pixels;
            return area.position + Vector2.Scale(viewportPoint, area.size);
        }
        public static void Apply(Camera camera)
        {
            var area = Pixels;
            var rect = new Rect(area.x / Mathf.Max(1, Screen.width), area.y / Mathf.Max(1, Screen.height),
                area.width / Mathf.Max(1, Screen.width), area.height / Mathf.Max(1, Screen.height));
            if (camera.rect != rect) camera.rect = rect;
            if (Mathf.Abs(camera.aspect - Aspect) > .00001f) camera.aspect = Aspect;
        }
    }

    /// <summary>The overlay canvas stays screen-sized; its authored content uses the camera's 16:9 area.</summary>
    public sealed class PaperViewportCanvas : MonoBehaviour
    {
        public RectTransform Content { get; private set; }
        private RectTransform root;
        private readonly RectTransform[] margins = new RectTransform[4];
        private Vector2 previousSize;

        public void Build()
        {
            root = (RectTransform)transform;
            Content = MakeRect("16:9 paper viewport");
            Content.sizeDelta = PaperViewport.DesignSize;
            Content.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < margins.Length; i++)
            {
                margins[i] = MakeRect("Outside folio " + i);
                var image = margins[i].gameObject.AddComponent<Image>();
                image.color = new Color(.035f, .028f, .024f, 1);
                image.raycastTarget = true;
            }
            Layout();
        }
        private RectTransform MakeRect(string name)
        {
            var item = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            item.SetParent(transform, false);
            item.anchorMin = item.anchorMax = item.pivot = Vector2.one * .5f;
            return item;
        }
        private void LateUpdate()
        {
            if (root && root.rect.size != previousSize) Layout();
        }
        private void OnRectTransformDimensionsChange()
        {
            if (root && margins[3]) Layout();
        }
        private void Layout()
        {
            previousSize = root.rect.size;
            float horizontal = Mathf.Max(0, (previousSize.x - PaperViewport.DesignSize.x) * .5f);
            float vertical = Mathf.Max(0, (previousSize.y - PaperViewport.DesignSize.y) * .5f);
            Place(0, new Vector2(-(PaperViewport.DesignSize.x + horizontal) * .5f, 0), new Vector2(horizontal, previousSize.y));
            Place(1, new Vector2((PaperViewport.DesignSize.x + horizontal) * .5f, 0), new Vector2(horizontal, previousSize.y));
            Place(2, new Vector2(0, -(PaperViewport.DesignSize.y + vertical) * .5f), new Vector2(previousSize.x, vertical));
            Place(3, new Vector2(0, (PaperViewport.DesignSize.y + vertical) * .5f), new Vector2(previousSize.x, vertical));
        }
        private void Place(int index, Vector2 position, Vector2 size)
        {
            var margin = margins[index];
            margin.anchoredPosition = position; margin.sizeDelta = size;
            margin.gameObject.SetActive(size.x > .001f && size.y > .001f);
        }
    }
}
