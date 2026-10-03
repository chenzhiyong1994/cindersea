using UnityEditor;
using UnityEngine;

namespace Dicebound.Editor
{
    /// <summary>Long stereo scores stream from the Player's packaged resources.</summary>
    public sealed class TacticalMusicImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/Resources/Audio/Music/"))return;
            var importer=(AudioImporter)assetImporter;
            importer.forceToMono=false;
            importer.loadInBackground=true;
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=.8f;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData=false;
            importer.defaultSampleSettings=settings;
        }
    }
}
