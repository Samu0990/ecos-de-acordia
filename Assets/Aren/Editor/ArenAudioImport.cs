using UnityEditor;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>
    /// Amostras gravadas (Resources/Audio/Samples): curtas descomprimidas na carga (tocam no
    /// quadro do golpe, sem custo de decodificação), longas comprimidas em memória.
    /// </summary>
    public class ArenAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Aren/Resources/Audio/Samples/")) return;
            var ai = (AudioImporter)assetImporter;
            bool longClip = assetPath.Contains("/amb_") || assetPath.Contains("/sting_") || assetPath.Contains("/ghost_long");
            var s = ai.defaultSampleSettings;
            s.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = longClip ? 0.55f : 0.7f;
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = false;
        }
    }
}
