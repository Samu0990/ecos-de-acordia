UnityEditor.AssetDatabase.Refresh();
var sb = new System.Text.StringBuilder();
foreach (var n in new[] { "clouds_a", "clouds_b" })
{
    var p = "Assets/Aren/Resources/VFX/" + n + ".png";
    var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(p);
    if (ti == null) { sb.Append("faltando " + p + "\n"); continue; }
    ti.sRGBTexture = false;
    ti.alphaSource = UnityEditor.TextureImporterAlphaSource.FromInput;
    ti.alphaIsTransparency = false;
    ti.mipmapEnabled = true;
    ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    ti.maxTextureSize = 1024;
    ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    ti.SaveAndReimport();
    sb.Append(n + " ok\n");
}
return sb.ToString();
