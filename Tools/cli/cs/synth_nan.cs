// gera o banco do sintetizador (AudioRunner.Generate via reflexão) e procura NaN/Inf/picos em cada som
var t = typeof(Aren.AudioRunner);
var runner = (Aren.AudioRunner)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
var gen = t.GetMethod("Generate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
gen.Invoke(runner, null);
var bank = t.GetField("bank", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(runner);
var bt = bank.GetType();
var sb = new System.Text.StringBuilder();
string Check(string name, float[] d) {
  int nan = 0; float peak = 0; int firstNan = -1;
  for (int i = 0; i < d.Length; i++) { float v = d[i]; if (float.IsNaN(v) || float.IsInfinity(v)) { nan++; if (firstNan < 0) firstNan = i; } else if (System.Math.Abs(v) > peak) peak = System.Math.Abs(v); }
  return (nan > 0 ? "*** " : "") + name + " len=" + d.Length + " nan=" + nan + (firstNan >= 0 ? " primeiro=" + firstNan : "") + " pico=" + peak.ToString("F3") + "\n";
}
var sfx = (System.Collections.Generic.Dictionary<int, float[][]>)bt.GetField("sfx").GetValue(bank);
foreach (var kv in sfx) for (int i = 0; i < kv.Value.Length; i++) { var line = Check(((Aren.Sfx)kv.Key) + "_" + i, kv.Value[i]); if (line.StartsWith("***") || true) sb.Append(line); }
var notes = (float[][])bt.GetField("notes").GetValue(bank);
for (int i = 0; i < notes.Length; i++) sb.Append(Check("note_" + i, notes[i]));
foreach (var f in new[] { "charge", "wind", "drone", "drums", "ostinato" }) sb.Append(Check(f, (float[])bt.GetField(f).GetValue(bank)));
return sb.ToString();
