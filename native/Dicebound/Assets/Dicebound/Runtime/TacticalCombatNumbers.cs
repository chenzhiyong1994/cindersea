using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Screen-sized receipts for committed health/shield changes; never owns battle state.</summary>
    public sealed class TacticalCombatNumbers : MonoBehaviour
    {
        private sealed class Receipt
        {
            public RectTransform root;
            public CanvasGroup group;
            public Vector3 world;
            public string unit;
            public float started, duration;
            public int lane;
            public bool reduced;
        }
        private readonly List<Receipt> receipts = new List<Receipt>();
        private Camera view;
        private TacticalStage stage;
        private RectTransform layer;
        public int ActiveCount => receipts.Count;
        public int PresentedCount { get; private set; }
        public int DamageCount { get; private set; }
        public int ShieldCount { get; private set; }
        public int HealCount { get; private set; }

        public void Initialize(Camera camera, Canvas canvas, TacticalStage owner)
        {
            view = camera; stage = owner;
            layer = Ui.Rect("Combat receipts", canvas.transform, Vector2.one * .5f, Vector2.zero, PaperViewport.DesignSize);
            var group = layer.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
        }

        public void Show(string unit, Vector3 world, int amount, string kind, bool heavy, bool reduced)
        {
            if (!layer || amount <= 0) return;
            int lane = 0;
            foreach (var receipt in receipts) if (receipt.unit == unit) lane = Mathf.Max(lane, receipt.lane + 1);
            var root = Ui.Rect("Committed " + kind + " " + unit, layer, Vector2.one * .5f, Vector2.zero, new Vector2(168, 92));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Color color = kind == "heal" ? new Color(.67f, .95f, .79f) : kind == "shield" || kind == "block" ? new Color(.64f, .86f, 1f) : heavy ? new Color(1f, .83f, .46f) : new Color(1f, .96f, .85f);
            string caption = kind == "shield" ? "护盾" : kind == "block" ? "格挡" : kind == "heal" ? "恢复" : heavy ? "重击" : "";
            string value = (kind == "heal" || kind == "shield" ? "+" : "") + amount;
            var text = Ui.Label("Amount", root, value, Vector2.one * .5f, new Vector2(0, 0), new Vector2(168, 76), heavy && kind == "damage" ? 56 : 46, color);
            text.font = Ui.DisplayFont; text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.025f, .04f, .055f, .8f); shadow.effectDistance = new Vector2(1.5f, -3);
            var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.045f, .065f, .075f, .94f); outline.effectDistance = new Vector2(1, -1);
            if (caption.Length > 0)
            {
                var label = Ui.Label("Receipt caption", root, caption, Vector2.one * .5f, new Vector2(0, 36), new Vector2(156, 32), 20, color);
                var edge = label.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(.035f, .055f, .07f, .9f); edge.effectDistance = new Vector2(1, -1);
            }
            var item = new Receipt { root = root, group = group, world = world, unit = unit, lane = lane, started = Time.unscaledTime, duration = reduced ? .44f : .84f, reduced = reduced };
            receipts.Add(item); PresentedCount++;
            if (kind == "damage") DamageCount++; else if (kind == "heal") HealCount++; else ShieldCount++;
            layer.SetAsLastSibling(); UpdateReceipt(item, 0);
        }

        private void LateUpdate()
        {
            for (int i = receipts.Count - 1; i >= 0; i--)
            {
                var item = receipts[i]; float age = Time.unscaledTime - item.started;
                if (!item.root || age >= item.duration)
                {
                    if (item.root) Destroy(item.root.gameObject);
                    receipts.RemoveAt(i); continue;
                }
                UpdateReceipt(item, age);
            }
        }
        private void UpdateReceipt(Receipt item, float age)
        {
            var screen = view.WorldToScreenPoint(item.world);
            Vector2 p = Vector2.Scale(PaperViewport.ToViewport(screen) - Vector2.one * .5f, PaperViewport.DesignSize);
            float rise = item.reduced ? 0 : 28 * (1 - Mathf.Exp(-age * 9)) + age * 17;
            p += new Vector2(item.lane % 2 == 0 ? -12 : 30, rise + item.lane * 42);
            Rect safe = stage.BoardSafeRect;
            item.root.anchoredPosition = new Vector2(Mathf.Clamp(p.x, safe.xMin + 84, safe.xMax - 84), Mathf.Clamp(p.y, safe.yMin + 44, safe.yMax - 62));
            float scale = 1;
            if (!item.reduced)
            {
                if (age < .075f) scale = Mathf.Lerp(.74f, 1.16f, 1 - Mathf.Pow(1 - age / .075f, 3));
                else if (age < .18f) scale = Mathf.Lerp(1.16f, 1, Mathf.SmoothStep(0, 1, (age - .075f) / .105f));
            }
            item.root.localScale = Vector3.one * scale;
            float fadeStart = item.duration - (item.reduced ? .13f : .22f);
            item.group.alpha = screen.z > 0 ? 1 - Mathf.SmoothStep(0, 1, (age - fadeStart) / (item.duration - fadeStart)) : 0;
        }
        public void Cancel()
        {
            foreach (var receipt in receipts) if (receipt.root) { receipt.root.gameObject.SetActive(false); Destroy(receipt.root.gameObject); }
            receipts.Clear();
        }
        private void OnDestroy() { Cancel(); if (layer) Destroy(layer.gameObject); }
    }
}
