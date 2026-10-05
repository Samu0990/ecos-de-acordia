UnityEditor.AssetDatabase.ImportAsset("Assets/Aren/Resources/UI/Loading/loading_fenda.jpg", UnityEditor.ImportAssetOptions.ForceUpdate);
var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Aren/Resources/UI/Loading/loading_fenda.jpg");
return ti.textureType + " mip=" + ti.mipmapEnabled + " comp=" + ti.textureCompression + " npot=" + ti.npotScale;
