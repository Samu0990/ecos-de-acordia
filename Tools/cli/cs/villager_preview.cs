// prévia dos aldeões: os 4 vivos (linha de cima) e a corrupção/desintegração (linha de baixo)
var names = new[] { "Villager_M1", "Villager_M2", "Villager_F1", "Villager_F2" };
var W = 360; var H = 560;
var camGo = new UnityEngine.GameObject("vcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 28; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.06f, 0.06f, 0.09f);
var lgo = new UnityEngine.GameObject("vlight"); var l = lgo.AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = 0.55f; l.color = new UnityEngine.Color(0.7f, 0.75f, 1f); lgo.transform.rotation = UnityEngine.Quaternion.Euler(30, 160, 0);
var pgo = new UnityEngine.GameObject("vpoint"); var pl = pgo.AddComponent<UnityEngine.Light>(); pl.type = UnityEngine.LightType.Point; pl.intensity = 2.2f; pl.range = 6f; pl.color = new UnityEngine.Color(1f, 0.7f, 0.4f);
var amb = UnityEngine.RenderSettings.ambientLight; UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(0.12f, 0.12f, 0.16f);
var rt = new UnityEngine.RenderTexture(W, H, 24); cam.targetTexture = rt;
var sheet = new UnityEngine.Texture2D(W * 4, H * 2, UnityEngine.TextureFormat.RGB24, false);
var tex = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
var mpb = new UnityEngine.MaterialPropertyBlock();
for (int row = 0; row < 2; row++)
for (int k = 0; k < 4; k++) {
  var prefab = UnityEngine.Resources.Load<UnityEngine.GameObject>("Villagers/" + (row == 0 ? names[k] : names[k % 2 == 0 ? 0 : 2]));
  var go = UnityEngine.Object.Instantiate(prefab, new UnityEngine.Vector3(900, 0, 900), UnityEngine.Quaternion.Euler(0, 180, 0));
  var an = go.GetComponent<UnityEngine.Animator>(); an.Rebind(); an.Update(0f);
  foreach (var smr in go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
  an.Play(row == 0 ? (k % 2 == 0 ? "Idle_FoldArms" : "Idle_No") : (k < 3 ? "Convulse" : "Stagger"), 0, 0f); an.Update(0f); an.Update(0.6f);
  if (row == 1) {
    float corrupt = k == 0 ? 0.3f : k == 1 ? 0.6f : 1f; float dis = k == 3 ? 0.45f : 0f;
    foreach (var r in go.GetComponentsInChildren<UnityEngine.Renderer>()) { r.GetPropertyBlock(mpb); mpb.SetFloat("_Corrupt", k == 3 ? 0.2f : corrupt); mpb.SetFloat("_Dissolve", dis); r.SetPropertyBlock(mpb); }
  }
  var center = go.transform.position + UnityEngine.Vector3.up * 0.95f;
  cam.transform.position = center + new UnityEngine.Vector3(0.4f, 0.15f, -4.4f); cam.transform.LookAt(center);
  pgo.transform.position = center + new UnityEngine.Vector3(-1.4f, 0.8f, -1.6f);
  cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0); tex.Apply();
  sheet.SetPixels(k * W, (1 - row) * H, W, H, tex.GetPixels());
  UnityEngine.Object.DestroyImmediate(go);
}
sheet.Apply();
System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/villagers.png"), sheet.EncodeToPNG());
UnityEngine.RenderTexture.active = null; cam.targetTexture = null; rt.Release();
UnityEngine.RenderSettings.ambientLight = amb;
UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(lgo); UnityEngine.Object.DestroyImmediate(pgo);
return "ok";
