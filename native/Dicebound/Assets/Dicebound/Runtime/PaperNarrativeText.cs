using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Dicebound.Presentation
{
    /// <summary>Plain narrative copy with Chinese line-breaking rules and unchanged source text.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class PaperNarrativeText : MonoBehaviour
    {
        public string SourceText => source;
        public string LayoutError { get; private set; }
        public int LineCount { get; private set; }

        private Text label;
        private readonly TextGenerator lines = new TextGenerator();
        private readonly TextGenerator heights = new TextGenerator();
        private TextGenerationSettings lastSettings;
        private string source = "", rendered = "", reportedError;
        private bool hasSource, dirty = true, formatting;

        public void SetText(string value)
        {
            source = value ?? "";
            hasSource = true;
            if (!label) label = GetComponent<Text>();
            rendered = source;
            label.text = source;
            dirty = true;
            ReflowNow();
        }

        /// <summary>Returns false on overflow or an unresolvable break; LayoutError explains it.</summary>
        public bool ReflowNow()
        {
            dirty = true;
            ReflowIfNeeded();
            return LayoutError == null;
        }

        private void ReflowIfNeeded()
        {
            if (formatting || !isActiveAndEnabled) return;
            if (!label) label = GetComponent<Text>();
            // Keep the Text-returning Ui API usable when a caller replaces narrative copy.
            if (!hasSource || label.text != rendered)
            {
                source = label.text ?? "";
                rendered = source;
                hasSource = true;
                dirty = true;
            }
            var settings = label.GetGenerationSettings(label.GetPixelAdjustedRect().size);
            if (!dirty && settings.Equals(lastSettings)) return;
            lastSettings = settings;
            formatting = true;
            string result = source, error = null;
            int count = 0;
            try
            {
                if (!label.font || settings.scaleFactor <= 0 || settings.generationExtents.x <= 0)
                    throw new InvalidOperationException("Narrative font or text width is unavailable.");
                if (label.supportRichText || label.resizeTextForBestFit || label.horizontalOverflow != HorizontalWrapMode.Wrap)
                    throw new InvalidOperationException("Narrative layout requires plain, wrapped text without automatic font resizing.");

                // Only the measuring generator may overflow. The visible Text retains its policy.
                var measuring = settings;
                measuring.verticalOverflow = VerticalWrapMode.Overflow;
                NarrativeLineBreaker.TryReflow(source, value => Measure(value, measuring),
                    settings.generationExtents.y, out result, out error, out count);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                result = source;
            }
            finally
            {
                formatting = false;
                dirty = false;
            }
            LineCount = count;
            LayoutError = error;
            rendered = result;
            if (label.text != result) label.text = result;
            if (error != null && error != reportedError)
                Debug.LogWarning("DICEBOUND_NARRATIVE_LAYOUT: " + name + " — " + error, this);
            reportedError = error;
        }

        private NarrativeLayout Measure(string value, TextGenerationSettings settings)
        {
            if (!lines.Populate(value, settings))
                throw new InvalidOperationException("The native text generator could not lay out the narrative.");
            var starts = new int[lines.lines.Count];
            for (int i = 0; i < starts.Length; ++i) starts[i] = lines.lines[i].startCharIdx;
            settings.generationExtents = new Vector2(settings.generationExtents.x, 0);
            return new NarrativeLayout
            {
                Starts = starts,
                Height = heights.GetPreferredHeight(value, settings) / settings.scaleFactor
            };
        }

        private void OnEnable()
        {
            dirty = true;
            Font.textureRebuilt += FontRebuilt;
            Canvas.willRenderCanvases += ReflowIfNeeded;
        }
        private void OnDisable()
        {
            Font.textureRebuilt -= FontRebuilt;
            Canvas.willRenderCanvases -= ReflowIfNeeded;
        }
        private void FontRebuilt(Font font) { if (!formatting && label && label.font == font) dirty = true; }
        private void OnRectTransformDimensionsChange() { dirty = true; }
        private void OnCanvasHierarchyChanged() { dirty = true; }
        private void LateUpdate() { ReflowIfNeeded(); }
        private void OnDestroy() { ((IDisposable)lines).Dispose(); ((IDisposable)heights).Dispose(); }
    }

    internal sealed class NarrativeLayout
    {
        internal int[] Starts;
        internal float Height;
    }

    internal static class NarrativeLineBreaker
    {
        private const string NoLineStart = "，。！？；：、）》」』】〕〉〗〙〛’”〟»›％‰℃°…,:;.!?)]}";
        private const string NoLineEnd = "（《「『【〔〈〖〘〚‘“〝«‹([{￥";

        internal static bool TryReflow(string source, Func<string, NarrativeLayout> measure, float height,
            out string result, out string error, out int lineCount)
        {
            result = source;
            error = null;
            lineCount = 0;
            string candidate = source;
            // Each pass inserts one new break and makes progress through a finite source.
            for (int attempt = 0; attempt <= source.Length; ++attempt)
            {
                var layout = measure(candidate);
                if (attempt == 0) lineCount = layout.Starts.Length;
                if (float.IsNaN(layout.Height) || float.IsInfinity(layout.Height) ||
                    float.IsNaN(height) || float.IsInfinity(height) || height < 0 || layout.Height > height + .01f)
                {
                    error = "Complete narrative exceeds its text box (" + layout.Height + " > " + height + "). Source preserved.";
                    return false;
                }
                if (candidate.Length > 0 && (layout.Starts.Length == 0 || layout.Starts[0] != 0))
                {
                    error = "The text generator returned an invalid first line. Source preserved.";
                    return false;
                }
                var boundaries = new HashSet<int>(StringInfo.ParseCombiningCharacters(candidate));
                boundaries.Add(candidate.Length);
                int split = -1;
                for (int i = 1; i < layout.Starts.Length; ++i)
                {
                    int start = layout.Starts[i], previous = layout.Starts[i - 1];
                    if (start <= previous || start > candidate.Length)
                    {
                        error = "The text generator returned invalid line indices. Source preserved.";
                        return false;
                    }
                    if (start == candidate.Length) continue;
                    // Authored paragraph boundaries are immutable, including CRLF and blank lines.
                    if (candidate[start - 1] == '\n' || candidate[start - 1] == '\r')
                    {
                        int lastContent = start - 1;
                        while (lastContent >= previous && char.IsWhiteSpace(candidate[lastContent])) --lastContent;
                        if (!LegalBoundary(candidate, start, boundaries) ||
                            lastContent >= previous && NoLineEnd.IndexOf(candidate[lastContent]) >= 0)
                        {
                            error = "An authored paragraph boundary violates narrative line-breaking rules. Source preserved.";
                            return false;
                        }
                        continue;
                    }
                    if (LegalBoundary(candidate, start, boundaries)) continue;
                    split = start - 1;
                    while (split > previous && !LegalBoundary(candidate, split, boundaries)) --split;
                    if (split <= previous)
                    {
                        error = "The text box is too narrow for an indivisible text group. Source preserved.";
                        return false;
                    }
                    break;
                }
                if (split < 0)
                {
                    result = candidate;
                    lineCount = layout.Starts.Length;
                    return true;
                }
                candidate = candidate.Insert(split, "\n");
            }
            error = "Narrative line-breaking reached its finite iteration limit. Source preserved.";
            return false;
        }

        private static bool LegalBoundary(string value, int index, HashSet<int> boundaries)
        {
            if (!boundaries.Contains(index)) return false;
            int right = index, left = index - 1;
            while (right < value.Length && char.IsWhiteSpace(value[right]) && value[right] != '\r' && value[right] != '\n') ++right;
            while (left >= 0 && char.IsWhiteSpace(value[left]) && value[left] != '\r' && value[left] != '\n') --left;
            if (right < value.Length && NoLineStart.IndexOf(value[right]) >= 0) return false;
            if (left >= 0 && NoLineEnd.IndexOf(value[left]) >= 0) return false;
            if (index > 0 && index < value.Length)
            {
                char a = value[index - 1], b = value[index];
                if (a == '\u200d' || b == '\u200d') return false;
                if (a == b && (a == '…' || a == '‥' || a == '—' || a == '–' || a == '-' || a == '.')) return false;
            }
            return true;
        }
    }
}
