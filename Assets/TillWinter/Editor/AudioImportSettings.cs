using UnityEditor;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Import rules for the three kinds of audio the game ships, applied on import so no .meta has to be edited by
    /// hand: music streams from disk (one long piece at a time, tiny memory), ambience loops sit compressed in memory
    /// (five of them play at once, all mono), and the Kenney one-shots stay as they were.
    /// </summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/");
            bool ambience = assetPath.Contains("/Ambience/");
            if (!music && !ambience) return;

            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.55f : 0.45f;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
            settings.preloadAudioData = false; // nothing loads before it is asked for
            importer.defaultSampleSettings = settings;
            importer.forceToMono = ambience; // the ambience bed is positionless; mono halves it
            importer.loadInBackground = true;
        }
    }
}
