var prev = UnityEngine.QualitySettings.shadows;
UnityEngine.QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
var camGo = new UnityEngine.GameObject("viewcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 55; cam.farClipPlane = 460; cam.clearFlags = UnityEngine.CameraClearFlags.Skybox;
var rt = new UnityEngine.RenderTexture(1280, 720, 24); cam.targetTexture = rt;
var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
var views = new (UnityEngine.Vector3 pos, UnityEngine.Vector3 look, string name)[] {
  (new UnityEngine.Vector3(3.6f, 1.55f, -16f), new UnityEngine.Vector3(5.6f, 1.0f, -16f), "z_pocoes"),
  (new UnityEngine.Vector3(-3.6f, 1.55f, -29.4f), new UnityEngine.Vector3(-5.6f, 1.0f, -29.4f), "z_frutas"),
  (new UnityEngine.Vector3(3.2f, 1.6f, -38.5f), new UnityEngine.Vector3(-6f, 2.6f, -38.5f), "z_tocha"),
  (new UnityEngine.Vector3(-14f, 1.6f, 20f), new UnityEngine.Vector3(-18f, 1.0f, 20f), "z_barraca_kit"),
};
foreach (var v in views) {
  cam.transform.position = v.pos; cam.transform.LookAt(v.look);
  cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
  System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/" + v.name + ".png"), tex.EncodeToPNG());
}
UnityEngine.RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGo); rt.Release();
UnityEngine.QualitySettings.shadows = prev;
return "ok";
