// aplica o terreno v2 na cena aberta (sem reconstruir tudo) e renderiza as vistas do chão
var sh = UnityEngine.Shader.Find("Campanula/Terrain");
var msgs = UnityEditor.ShaderUtil.GetShaderMessages(sh);
var sb = new System.Text.StringBuilder();
foreach (var m in msgs) sb.Append(m.severity + ": " + m.message + " @" + m.line + "\n");
if (msgs.Length > 0) return sb.ToString();
var t = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Terrain>();
var mi = typeof(Campanula.EditorTools.CampanulaBuilder).GetMethod("TerrainMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
UnityEditor.AssetDatabase.DeleteAsset("Assets/Campanula/Materials/Terrain.mat");
t.materialTemplate = (UnityEngine.Material)mi.Invoke(null, null);
t.basemapDistance = 4000f; t.heightmapPixelError = 4f;
var views = @"
g_campo 0 1.7 -60 -20 0.5 -85 60
g_campo2 25 1.6 -80 40 0.8 -95 60
g_horizonte 0 20 -20 0 15 -200 60
g_encosta -60 3 30 -110 10 60 60
h_close 10 12 -95 18 22 -135 60
g_ponte 33 2.5 11 40 -8 -20 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/ground2");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
