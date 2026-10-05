var go = new UnityEngine.GameObject("tmpcam");
var cam = go.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 60; cam.aspect = 800f/450f; cam.pixelRect = new UnityEngine.Rect(0,0,800,450);
go.transform.position = new UnityEngine.Vector3(10,12,-95);
go.transform.LookAt(new UnityEngine.Vector3(18,22,-135));
var sb = new System.Text.StringBuilder();
foreach (var px in new[]{ new UnityEngine.Vector2(195,268), new UnityEngine.Vector2(230,262), new UnityEngine.Vector2(20,415), new UnityEngine.Vector2(100,330)}) {
  var ray = cam.ScreenPointToRay(new UnityEngine.Vector3(px.x * cam.pixelWidth/800f, (450-px.y) * cam.pixelHeight/450f, 0));
  UnityEngine.RaycastHit h;
  if (UnityEngine.Physics.Raycast(ray, out h, 2000f)) sb.Append(px + " -> " + h.collider.name + " " + h.point.ToString("F1") + "\n");
  else sb.Append(px + " -> nada\n");
}
UnityEngine.Object.DestroyImmediate(go);
return sb.ToString();
