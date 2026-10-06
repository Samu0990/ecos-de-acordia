// Reimporta as texturas do kit da natureza (atlas + fotos) e cria/atualiza os materiais NAT_* (reimporta os Nature_*.fbx)
var paths = new System.Collections.Generic.List<string>();
foreach (var a in new[] { "a", "b", "c" })
    foreach (var k in new[] { "normal", "mask" }) paths.Add("Assets/Campanula/Textures/nature_" + a + "_" + k + ".png");
foreach (var n in new[] { "nat_rock", "nat_moss", "nat_bark" })
    foreach (var k in new[] { "albedo", "normal", "mg", "ao", "height" }) paths.Add("Assets/Campanula/Textures/PH/" + n + "_" + k + ".png");
UnityEditor.AssetDatabase.StartAssetEditing();
try { foreach (var p in paths) if (System.IO.File.Exists(p)) UnityEditor.AssetDatabase.ImportAsset(p, UnityEditor.ImportAssetOptions.ForceUpdate); }
finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
return Campanula.EditorTools.CampanulaMaterials.NatureRocks();
