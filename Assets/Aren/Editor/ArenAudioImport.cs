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
            if (assetPath.StartsWith("Assets/Aren/Resources/Audio/Rupture/")) { Rupture(); return; }
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

        /// <summary>
        /// Sons da noite pré-gerados (RuptureBake): laços em PCM (emenda sem clique), os longos em
        /// Vorbis comprimido em memória, os curtos descomprimidos na carga.
        /// </summary>
        void Rupture()
        {
            var ai = (AudioImporter)assetImporter;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool loop = n.Contains("loop") || n.StartsWith("crickets") || n == "windtone" || n.StartsWith("melody");
            long size = new System.IO.FileInfo(assetPath).Length;
            var s = ai.defaultSampleSettings;
            if (loop) { s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.PCM; }
            else if (size > 600000) { s.loadType = AudioClipLoadType.CompressedInMemory; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f; }
            else { s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.75f; }
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
            ai.forceToMono = true;
            ai.loadInBackground = false;
        }
    }
}
