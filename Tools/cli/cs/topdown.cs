// vistas ortográficas de cima com grade de 5 m (planejar a posição dos props)
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
string info = scene.path;
var camGo = new UnityEngine.GameObject("topcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.orthographic = true; cam.farClipPlane = 400; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = UnityEngine.Color.black;
cam.transform.rotation = UnityEngine.Quaternion.Euler(90, 0, 0);
var prevShadows = UnityEngine.QualitySettings.shadows; UnityEngine.QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/");
foreach (var (cx, cz, half, name) in new[] { (0f, -23f, 22f, "top_mercado"), (0f, 18f, 28f, "top_praca"), (0f, -75f, 32f, "top_estrada"), (60f, 14f, 32f, "top_leste") }) {
  int W = 1000, H = 1000;
  var rt = new UnityEngine.RenderTexture(W, H, 24); cam.targetTexture = rt;
  cam.orthographicSize = half; cam.transform.position = new UnityEngine.Vector3(cx, 120, cz);
  cam.Render(); UnityEngine.RenderTexture.active = rt;
  var tex = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
  tex.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0);
  // grade de 5 m (linha amarela a cada 10 m)
  float ppm = W / (half * 2);
  for (int g = -200; g <= 200; g += 5) {
    float px = (g - (cx - half)) * ppm; float pz = (g - (cz - half)) * ppm;
    var c = g % 10 == 0 ? new UnityEngine.Color(1, 1, 0) : new UnityEngine.Color(0.5f, 0.5f, 0.5f);
    if (px >= 0 && px < W) for (int y = 0; y < H; y += 2) tex.SetPixel((int)px, y, c);
    if (pz >= 0 && pz < H) for (int x = 0; x < W; x += 2) tex.SetPixel(x, (int)pz, c);
  }
  tex.Apply();
  System.IO.File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
  UnityEngine.RenderTexture.active = null; cam.targetTexture = null; rt.Release();
  info += "\n" + name + ": x[" + (cx - half) + "," + (cx + half) + "] z[" + (cz - half) + "," + (cz + half) + "]";
}
UnityEngine.QualitySettings.shadows = prevShadows;
UnityEngine.Object.DestroyImmediate(camGo);
return info;
