using System.IO;
using System.Threading.Tasks;
using UnityEditor;

namespace Aren.EditorTools
{
    /// <summary>
    /// Grava os sons da noite da Ruptura (OpeningSound.Generators) como WAV em
    /// Assets/Aren/Resources/Audio/Rupture: o jogo carrega pronto em vez de sintetizar no começo
    /// (a síntese disputava a CPU com o prólogo e o deixava lento). Rodar de novo sempre que mudar
    /// RuptureSynth / RuptureSynthCinematic. CLI: Tools/cli/job Tools/cli/cs/rupture_bake.cs
    /// </summary>
    public static class RuptureBake
    {
        public const string Dir = "Assets/Aren/Resources/Audio/Rupture";

        [MenuItem("Aren/Áudio/Gerar sons da noite (Ruptura)")]
        public static string Bake()
        {
            Directory.CreateDirectory(Dir);
            var gen = World.Night.OpeningSound.Generators();
            var data = new float[gen.Count][];
            Parallel.For(0, gen.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => data[i] = gen[i].Value());
            long bytes = 0;
            for (int i = 0; i < gen.Count; i++)
            {
                string path = Path.Combine(Dir, gen[i].Key + ".wav");
                WriteWav(path, data[i], RuptureSynth.SR);
                bytes += new FileInfo(path).Length;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return $"{gen.Count} sons gravados ({bytes / 1024} KB em WAV)";
        }

        static void WriteWav(string path, float[] x, int sr)
        {
            using (var f = new BinaryWriter(File.Create(path)))
            {
                int n = x.Length;
                f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); f.Write(36 + n * 2);
                f.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); f.Write(16); f.Write((short)1); f.Write((short)1);
                f.Write(sr); f.Write(sr * 2); f.Write((short)2); f.Write((short)16);
                f.Write(System.Text.Encoding.ASCII.GetBytes("data")); f.Write(n * 2);
                for (int i = 0; i < n; i++)
                {
                    float v = float.IsNaN(x[i]) || float.IsInfinity(x[i]) ? 0f : x[i];
                    if (v > 1f) v = 1f; else if (v < -1f) v = -1f;
                    f.Write((short)(v * 32767f));
                }
            }
        }
    }
}
