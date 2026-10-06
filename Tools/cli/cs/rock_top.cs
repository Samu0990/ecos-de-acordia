// mapa inteiro visto de cima (onde as rochas caíram) — shots/rocks/top_mapa.png
var camGo = new UnityEngine.GameObject("topcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.orthographic = true; cam.farClipPlane = 400; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = UnityEngine.Color.black;
cam.transform.rotation = UnityEngine.Quaternion.Euler(90, 0, 0);
var prevShadows = UnityEngine.QualitySettings.shadows; UnityEngine.QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/rocks/");
System.IO.Directory.CreateDirectory(dir);
string info = "";
foreach (var (cx, cz, half, name) in new[] { (0f, 0f, 160f, "top_mapa"), (-100f, -110f, 50f, "top_sudoeste") }) {
  int W = 1400, H = 1400;
  var rt = new UnityEngine.RenderTexture(W, H, 24); cam.targetTexture = rt;
  cam.orthographicSize = half; cam.transform.position = new UnityEngine.Vector3(cx, 150, cz);
  cam.Render(); UnityEngine.RenderTexture.active = rt;
  var tex = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
  tex.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0); tex.Apply();
  System.IO.File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
  UnityEngine.RenderTexture.active = null; cam.targetTexture = null; rt.Release();
  info += name + " ";
}
UnityEngine.QualitySettings.shadows = prevShadows;
UnityEngine.Object.DestroyImmediate(camGo);
return info;
