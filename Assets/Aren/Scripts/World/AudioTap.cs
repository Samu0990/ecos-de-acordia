using System.Collections.Generic;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Grava a mixagem final do jogo (OnAudioFilterRead no objeto do AudioListener) num WAV —
    /// só o som do jogo, sem capturar mais nada do computador. Usado por -eda-intro-audio.
    /// </summary>
    public class AudioTap : MonoBehaviour
    {
        readonly List<float> data = new List<float>(48000 * 2 * 70);
        readonly object lk = new object();
        int channels = 2;
        public bool recording = true;

        void OnAudioFilterRead(float[] buf, int ch)
        {
            if (!recording) return;
            lock (lk) { channels = ch; data.AddRange(buf); }
        }

        public void Save(string path)
        {
            recording = false;
            float[] d; int ch;
            lock (lk) { d = data.ToArray(); ch = channels; }
            int sr = AudioSettings.outputSampleRate;
            using (var f = new System.IO.FileStream(path, System.IO.FileMode.Create))
            using (var w = new System.IO.BinaryWriter(f))
            {
                int bytes = d.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch);
                w.Write(sr); w.Write(sr * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var v in d) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }
    }
}
