Aren.World.Night.NightSetup.Teardown();
Aren.World.Night.NightSetup.Apply(true);
var cam = new UnityEngine.GameObject("tmpcam").AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 60; cam.aspect = 960f/540f;
cam.transform.position = new UnityEngine.Vector3(0,1.8f,-88); cam.transform.LookAt(new UnityEngine.Vector3(2,3,-60));
var sb = new System.Text.StringBuilder();
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsSortMode.None)) {
  var sp = cam.WorldToScreenPoint(r.bounds.center);
  if (sp.z < 0 || sp.z > 15) continue;
  float x = sp.x / cam.pixelWidth * 960, y = 540 - sp.y / cam.pixelHeight * 540;
  if (x < -50 || x > 350 || y < 380) continue;
  sb.Append(r.name + " | " + (r.transform.parent ? r.transform.parent.name : "-") + " | " + (r.sharedMaterial ? r.sharedMaterial.shader.name : "") + " | " + r.bounds.center.ToString("F2") + " px " + x.ToString("F0") + "," + y.ToString("F0") + "\n");
}
UnityEngine.Object.DestroyImmediate(cam.gameObject);
Aren.World.Night.NightSetup.Teardown();
return sb.ToString();
