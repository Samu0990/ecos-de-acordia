// renderiza o Player em poses do Animator (frente e lado) para conferir a retargetagem
var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Dynamic Parkour System/Prefabs/Player.prefab"));
go.transform.SetPositionAndRotation(new UnityEngine.Vector3(800, 0, 800), UnityEngine.Quaternion.identity);
var an = go.GetComponentInChildren<UnityEngine.Animator>(); an.applyRootMotion = false; an.cullingMode = UnityEngine.AnimatorCullingMode.AlwaysAnimate;
an.Rebind(); an.Update(0f);
foreach (var smr in go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
UnityEngine.Transform flute = null, socket = null;
foreach (var t in go.GetComponentsInChildren<UnityEngine.Transform>(true)) { if (t.name == "Aren_Flute") { flute = t; } if (t.name == "FluteSocket") { socket = t; } }
var camGo = new UnityEngine.GameObject("posecam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 30; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.3f, 0.32f, 0.36f);
var lgo = new UnityEngine.GameObject("poselight"); var l = lgo.AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = 1.2f; lgo.transform.rotation = UnityEngine.Quaternion.Euler(35, 150, 0);
var W = 360; var H = 540;
var rt = new UnityEngine.RenderTexture(W, H, 24); cam.targetTexture = rt;
var states = new[] { ("Aren Death", 0.0f, false), ("Aren Death", 0.2f, false), ("Aren Death", 0.4f, false), ("Aren Death", 0.6f, false), ("Aren Death", 0.9f, false), ("Aren Revive", 0.05f, false), ("Aren Revive", 0.6f, false), ("Aren Revive", 1.1f, false), ("Aren Revive", 1.5f, false) };
var sheet = new UnityEngine.Texture2D(W * states.Length, H * 2, UnityEngine.TextureFormat.RGB24, false);
var tex = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
for (int k = 0; k < states.Length; k++) {
  var (st, time, hand) = states[k];
  if (flute != null && socket != null && hand) { flute.SetParent(socket, false); flute.localPosition = UnityEngine.Vector3.zero; flute.localRotation = UnityEngine.Quaternion.identity; }
  an.Play(st, 0, 0f); an.Update(0f); an.Update(time);
  for (int v = 0; v < 2; v++) {
    var center = go.transform.position + UnityEngine.Vector3.up * 0.95f;
    cam.transform.position = center + (v == 0 ? new UnityEngine.Vector3(0, 0.2f, 4.2f) : new UnityEngine.Vector3(4.2f, 0.2f, 0));
    cam.transform.LookAt(center);
    cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0); tex.Apply();
    sheet.SetPixels(k * W, (1 - v) * H, W, H, tex.GetPixels());
  }
}
sheet.Apply();
System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/knock_poses.png"), sheet.EncodeToPNG());
UnityEngine.RenderTexture.active = null; cam.targetTexture = null; rt.Release();
UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(lgo); UnityEngine.Object.DestroyImmediate(go);
return "ok flute=" + (flute != null) + " socket=" + (socket != null);
