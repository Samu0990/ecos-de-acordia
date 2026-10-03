var prev = UnityEngine.QualitySettings.shadows;
UnityEngine.QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
var camGo = new UnityEngine.GameObject("viewcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 55; cam.farClipPlane = 460; cam.clearFlags = UnityEngine.CameraClearFlags.Skybox;
var rt = new UnityEngine.RenderTexture(1280, 720, 24); cam.targetTexture = rt;
var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
var views = new (UnityEngine.Vector3 pos, UnityEngine.Vector3 look, string name)[] {
  (new UnityEngine.Vector3(0, 1.9f, -98f), new UnityEngine.Vector3(0, 2.5f, -60f), "w_estrada"),
  (new UnityEngine.Vector3(-14f, 2.2f, -78f), new UnityEngine.Vector3(-40f, 3f, -70f), "w_campo_trigo"),
  (new UnityEngine.Vector3(60f, 2.2f, -2f), new UnityEngine.Vector3(84f, 3f, 24f), "w_campo_fenda"),
  (new UnityEngine.Vector3(30f, 2.4f, 4f), new UnityEngine.Vector3(44f, 3.5f, 14f), "w_riacho"),
  (new UnityEngine.Vector3(-2f, 1.8f, -30f), new UnityEngine.Vector3(-6f, 1.4f, -24f), "w_mercado_perto"),
  (new UnityEngine.Vector3(-70f, 45f, -120f), new UnityEngine.Vector3(5f, 0f, 0f), "w_aereo"),
};
foreach (var v in views) {
  cam.transform.position = v.pos; cam.transform.LookAt(v.look);
  cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
  System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/" + v.name + ".png"), tex.EncodeToPNG());
}
UnityEngine.RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGo); rt.Release();
UnityEngine.QualitySettings.shadows = prev;
return "ok";
