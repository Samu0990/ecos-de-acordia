var sb = new System.Text.StringBuilder();
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.BoxCollider>(UnityEngine.FindObjectsSortMode.None)) {
  if (c.gameObject.layer != 8) continue;
  var p = c.bounds.center;
  if (UnityEngine.Mathf.Abs(p.x - CX) < RX && UnityEngine.Mathf.Abs(p.z - CZ) < RZ) sb.Append(p.ToString("F2") + " size=" + c.bounds.size.ToString("F2") + " fwd=" + c.transform.forward.ToString("F2") + "\n");
}
var lst = sb.ToString().Split('\n'); System.Array.Sort(lst, (a, b) => a.CompareTo(b));
return string.Join("\n", lst);
