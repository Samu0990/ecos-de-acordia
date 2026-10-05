UnityEditor.AssetDatabase.Refresh();
// cria/atualiza CMP_leafcards e remapeia só árvores e arbustos
var setup = typeof(Campanula.EditorTools.CampanulaMaterials);
const string Mat = "Assets/Campanula/Materials/", Tex = "Assets/Campanula/Textures/";
var lc = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(Mat + "CMP_leafcards.mat");
if (lc == null) { lc = new UnityEngine.Material(UnityEngine.Shader.Find("Campanula/LeafCards")); UnityEditor.AssetDatabase.CreateAsset(lc, Mat + "CMP_leafcards.mat"); }
lc.shader = UnityEngine.Shader.Find("Campanula/LeafCards");
lc.SetTexture("_MainTex", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(Tex + "leaf_atlas.png"));
lc.SetTexture("_BumpMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(Tex + "leaf_atlas_normal.png"));
lc.SetColor("_Color", new UnityEngine.Color(1.05f, 1.08f, 1f));
lc.enableInstancing = true;
UnityEditor.EditorUtility.SetDirty(lc);
UnityEditor.AssetDatabase.SaveAssets();
int n = 0;
foreach (var nm in new[]{"Tree_Oak","Tree_Oak_LOD1","Tree_Oak2","Tree_Oak2_LOD1","Tree_Pine","Tree_Pine_LOD1","Bush","Bush_LOD1"}) {
  var p = "Assets/Campanula/Models/" + nm + ".fbx";
  var mi = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(p);
  if (mi == null) continue;
  mi.SearchAndRemapMaterials(UnityEditor.ModelImporterMaterialName.BasedOnMaterialName, UnityEditor.ModelImporterMaterialSearch.Everywhere);
  mi.SaveAndReimport(); n++;
}
return "árvores reimportadas: " + n;
