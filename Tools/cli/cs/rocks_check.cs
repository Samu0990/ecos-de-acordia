var sb = new System.Text.StringBuilder();
foreach (var n in new[] { "Nature_Outcrop_A", "Nature_Stones_A", "Nature_Log_A", "Nature_Cliff_A", "Nature_Outcrop_A_LOD1" }) {
  var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Campanula/Models/" + n + ".fbx");
  if (go == null) { sb.Append(n + ": FALTA\n"); continue; }
  var mr = go.GetComponentInChildren<UnityEngine.MeshRenderer>(); var mf = go.GetComponentInChildren<UnityEngine.MeshFilter>();
  var m = mf.sharedMesh;
  var uv2 = new System.Collections.Generic.List<UnityEngine.Vector2>(); m.GetUVs(1, uv2);
  sb.Append(n + ": mat=" + (mr.sharedMaterial ? mr.sharedMaterial.name + "/" + mr.sharedMaterial.shader.name : "null") + " tris=" + (m.triangles.Length / 3) + " uv2=" + uv2.Count + " tangents=" + m.tangents.Length + " bounds=" + m.bounds.size + "\n");
}
foreach (var t in new[] { "nature_a_normal", "nature_b_normal", "nature_c_mask" }) {
  var tx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Campanula/Textures/" + t + ".png");
  sb.Append(t + ": " + (tx ? tx.width + "x" + tx.height + " " + tx.format : "FALTA") + "\n");
}
return sb.ToString();
