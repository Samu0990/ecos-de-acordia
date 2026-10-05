var sb = new System.Text.StringBuilder();
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsSortMode.None))
{
    var b = r.bounds;
    if (b.max.x < -12 || b.min.x > 30 || b.max.z < -160 || b.min.z > -105) continue;
    if (b.size.magnitude > 400) continue;
    sb.Append(r.name + " | " + (r.transform.parent? r.transform.parent.name : "") + " | " + (r.sharedMaterial? r.sharedMaterial.shader.name : "") + " | " + b.center.ToString("F1") + " " + b.size.ToString("F1") + "\n");
}
return sb.ToString();
