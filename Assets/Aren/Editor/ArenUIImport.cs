using UnityEditor;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>Sprites da UI: tipo Sprite, sem mipmap, bordas 9-slice nos painéis e barras.</summary>
    public class ArenUIImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Aren/Resources/UI/Sprites")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;   // UI nítida (são poucas e pequenas)
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            if (n == "ui_panel") ti.spriteBorder = new Vector4(16, 16, 16, 16);
            else if (n == "ui_bar" || n == "ui_bar_frame") ti.spriteBorder = new Vector4(6, 6, 6, 6);
            else if (n == "ui_button") ti.spriteBorder = new Vector4(60, 0, 60, 0);
            else ti.spriteBorder = Vector4.zero;
        }
    }
}
