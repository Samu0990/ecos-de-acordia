var sb = new System.Text.StringBuilder();
var counts = new System.Collections.Generic.Dictionary<string,int>();
foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsSortMode.None)) {
  if (t.GetComponent<UnityEngine.MeshFilter>() == null) continue;
  string n = t.name; int i = n.IndexOf('('); if (i > 0) n = n.Substring(0, i).Trim();
  n = System.Text.RegularExpressions.Regex.Replace(n, @"[_ ]?\d+$", "");
  counts[n] = counts.TryGetValue(n, out var c) ? c + 1 : 1;
}
foreach (var kv in counts) if (kv.Value >= 2) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("; ");
var lights = UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None);
sb.Append(" | luzes=").Append(lights.Length);
return sb.ToString();
