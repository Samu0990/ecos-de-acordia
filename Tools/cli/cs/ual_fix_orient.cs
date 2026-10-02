// UAL2: a orientação "Original" dos clipes está girada 180° em relação aos avatares do projeto
// (o tronco fica de costas para o "frente" do objeto). Offset de 180° no Root Transform Rotation.
var path = "Assets/Aren/ThirdParty/Quaternius_UAL2/UAL2_Standard.fbx";
var mi = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
var clips = mi.clipAnimations;
int n = 0;
foreach (var c in clips) { if (UnityEngine.Mathf.Abs(c.rotationOffset - 180f) > 0.01f) { c.rotationOffset = 180f; n++; } }
mi.clipAnimations = clips;
mi.SaveAndReimport();
return "clipes ajustados: " + n + " de " + clips.Length;
