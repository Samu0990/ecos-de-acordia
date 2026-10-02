var prev = UnityEngine.QualitySettings.shadows;
UnityEngine.QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
var camGo = new UnityEngine.GameObject("viewcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 55; cam.farClipPlane = 460; cam.clearFlags = UnityEngine.CameraClearFlags.Skybox;
var rt = new UnityEngine.RenderTexture(1280, 720, 24); cam.targetTexture = rt;
var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
var views = new (UnityEngine.Vector3 pos, UnityEngine.Vector3 look, string name)[] {
  (new UnityEngine.Vector3(0.5f, 1.8f, -40f), new UnityEngine.Vector3(0, 1.6f, -22f), "p_mercado"),
  (new UnityEngine.Vector3(2.5f, 1.7f, -33f), new UnityEngine.Vector3(-5.5f, 1.0f, -29f), "p_barraca"),
  (new UnityEngine.Vector3(1.5f, 1.7f, -19f), new UnityEngine.Vector3(5.5f, 1.0f, -15.5f), "p_pocoes"),
  (new UnityEngine.Vector3(8f, 2.2f, 0f), new UnityEngine.Vector3(18f, 0.8f, 6f), "p_taverna"),
  (new UnityEngine.Vector3(0f, 2.4f, 14f), new UnityEngine.Vector3(0, 3f, 32f), "p_torre"),
  (new UnityEngine.Vector3(-8f, 2.0f, 10f), new UnityEngine.Vector3(-18f, 1.2f, 18f), "p_praca_oeste"),
  (new UnityEngine.Vector3(0, 2.2f, -55f), new UnityEngine.Vector3(0, 3f, -42f), "p_portao"),
  (new UnityEngine.Vector3(56f, 3f, 0f), new UnityEngine.Vector3(140f, 70f, 70f), "p_fenda"),
  (new UnityEngine.Vector3(62f, 2.5f, 8f), new UnityEngine.Vector3(88f, 1f, 20f), "p_campo"),
};
foreach (var v in views) {
  cam.transform.position = v.pos; cam.transform.LookAt(v.look);
  cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
  System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/" + v.name + ".png"), tex.EncodeToPNG());
}
UnityEngine.RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGo); rt.Release();
UnityEngine.QualitySettings.shadows = prev;
return "ok";
