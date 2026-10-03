using System;
using UnityEngine;

namespace Dicebound.Presentation
{
    // Shared by the tactical Player's optional audio verification.
    public sealed class AudioMeter : MonoBehaviour
    {
        public volatile float Peak;
        public long Samples;
        private void OnAudioFilterRead(float[] data, int channels)
        {
            float peak = Peak;
            foreach (float value in data) peak = Math.Max(peak, Math.Abs(value));
            Peak = peak;
            System.Threading.Interlocked.Add(ref Samples, data.Length);
        }
    }
}
