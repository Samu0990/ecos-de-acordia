// prancha de conferência dos props do kit: grade em (1000,0,1000), câmera olhando para +Z
var kit = "Assets/Campanula/ThirdParty/FantasyProps/Models";
var guids = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { kit });
var rootGo = new UnityEngine.GameObject("kit_sheet");
var names = new System.Collections.Generic.List<string>();
int cols = 8; int idx = 0;
var only = new System.Collections.Generic.HashSet<string> { "Stall_Empty", "Stall_Cart_Empty", "Banner_1", "Banner_2", "Banner_1_Cloth", "Lantern_Wall", "Torch_Metal", "Bench", "Chair_1", "Barrel_Apples", "FarmCrate_Apple", "FarmCrate_Carrot", "Crate_Wooden", "Bag", "Dummy", "WeaponStand", "Anvil", "Workbench", "Cauldron", "Chest_Wood", "Shield_Wooden", "Potion_2", "Table_Large", "Bucket_Wooden_1" };
foreach (var g in guids) {
  var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
  var n = System.IO.Path.GetFileNameWithoutExtension(p);
  if (!only.Contains(n)) continue;
  var src = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p);
  var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(src, rootGo.transform);
  go.transform.position = new UnityEngine.Vector3(1000 + (idx % cols) * 3.4f, 0, 1000 + (idx / cols) * 4.5f);
  names.Add(n + "@" + (idx % cols) + "," + (idx / cols) + " rot=" + src.transform.localEulerAngles);
  idx++;
}
var camGo = new UnityEngine.GameObject("sheetcam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.fieldOfView = 40; cam.farClipPlane = 200; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.35f, 0.38f, 0.42f);
var lgo = new UnityEngine.GameObject("sheetlight"); var l = lgo.AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = 1.1f; lgo.transform.rotation = UnityEngine.Quaternion.Euler(40, 20, 0);
var rt = new UnityEngine.RenderTexture(1600, 900, 24); cam.targetTexture = rt;
var tex = new UnityEngine.Texture2D(1600, 900, UnityEngine.TextureFormat.RGB24, false);
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/");
foreach (var (pos, look, name) in new[] {
  (new UnityEngine.Vector3(1011.9f, 7.5f, 990.5f), new UnityEngine.Vector3(1011.9f, 0.5f, 1004.5f), "kit_front"),
  (new UnityEngine.Vector3(1011.9f, 7.5f, 1018.5f), new UnityEngine.Vector3(1011.9f, 0.5f, 1004.5f), "kit_back") }) {
  cam.transform.position = pos; cam.transform.LookAt(look);
  cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
  System.IO.File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
}
UnityEngine.RenderTexture.active = null; cam.targetTexture = null;
UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(lgo); UnityEngine.Object.DestroyImmediate(rootGo); rt.Release();
return string.Join("\n", names);
