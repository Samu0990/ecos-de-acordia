// objetos visíveis grandes (> 0,8 m) sem colisor neles nem num ancestral até o contêiner
var sb = new System.Text.StringBuilder();
var counts = new System.Collections.Generic.Dictionary<string,int>();
string[] skip = { "Cachoeira", "Nevoa", "Névoa", "Mist", "Riacho", "Agua", "Água", "Flame", "Chama", "Halo", "Banner", "Estandarte", "Janela", "Terrain", "Fenda", "Rift", "Luz", "Poca", "Waterfall", "Sails" };
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsSortMode.None))
{
    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
    var b = r.bounds;
    if (b.size.x < 0.8f && b.size.z < 0.8f) continue;
    if (b.size.y < 0.25f) continue;
    bool sk = false; foreach (var k in skip) if (r.name.Contains(k) || (r.transform.parent && r.transform.parent.name.Contains(k))) sk = true;
    if (sk) continue;
    // LOD1: tem colisor no LOD0 do mesmo contêiner
    var lg = r.GetComponentInParent<UnityEngine.LODGroup>();
    bool has = false;
    for (var t = r.transform; t != null; t = t.parent) { if (t.GetComponent<UnityEngine.Collider>() != null) { has = true; break; } if (t.GetComponent<UnityEngine.LODGroup>() != null) { has = t.GetComponentInChildren<UnityEngine.Collider>() != null; break; } }
    if (has) continue;
    var holder = r.transform; while (holder.parent != null && holder.parent.name != "Static" && holder.parent.name != "Campanula") holder = holder.parent;
    string key = holder.name;
    counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
}
foreach (var kv in counts) sb.Append(kv.Key + " x" + kv.Value + "\n");
return sb.ToString();
