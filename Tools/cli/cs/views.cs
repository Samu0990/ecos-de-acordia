var prev = UnityEngine.QualitySettings.shadows;
UnityEngine.QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
var camGo = new UnityEngine.GameObject("viewcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 55; cam.farClipPlane = 460; cam.clearFlags = UnityEngine.CameraClearFlags.Skybox;
var rt = new UnityEngine.RenderTexture(960, 540, 24); cam.targetTexture = rt;
var tex = new UnityEngine.Texture2D(960, 540, UnityEngine.TextureFormat.RGB24, false);
var views = new (UnityEngine.Vector3 pos, UnityEngine.Vector3 look, string name)[] {
  (new UnityEngine.Vector3(2, 2.0f, -38), new UnityEngine.Vector3(0, 3f, 0), "v2_mercado"),
  (new UnityEngine.Vector3(-12, 3.0f, -2), new UnityEngine.Vector3(2, 8f, 36), "v3_praca"),
  (new UnityEngine.Vector3(0, 2.2f, -97), new UnityEngine.Vector3(0, 3f, -40), "v1_estrada"),
  (new UnityEngine.Vector3(-70, 55f, -110), new UnityEngine.Vector3(5, 0, 5), "v4_aereo"),
};
foreach (var v in views) {
  cam.transform.position = v.pos; cam.transform.LookAt(v.look);
  cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, 960, 540), 0, 0); tex.Apply();
  System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/" + v.name + ".png"), tex.EncodeToPNG());
}
UnityEngine.RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGo); rt.Release();
UnityEngine.QualitySettings.shadows = prev;
return "ok";
