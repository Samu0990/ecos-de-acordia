using UnityEditor;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>Sprites da UI: tipo Sprite, sem mipmap, bordas 9-slice nos painéis e barras.</summary>
    public class ArenUIImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Aren/Resources/UI/Title/")) { TitleTexture(); return; }
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

        /// <summary>
        /// Tela inicial em pixel art (ArtSource/Menu/scripts/build_title.py): tudo sem
        /// compressão (a arte é pequena e compressão em bloco borra os pixels), sem mipmap.
        /// Fundo e máscara como textura comum; placas, textos e brilho como sprites.
        /// O brilho e a névoa são pixel art de verdade: filtro ponto.
        /// </summary>
        void TitleTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool sprite = n.StartsWith("btn_") || n.StartsWith("lbl_") || n == "glow";
            ti.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            bool data = n.StartsWith("motion_");   // mapas de movimento: dados, não cor
            ti.alphaIsTransparency = n != "title_bg" && !data;   // (com alfa-transparência o Unity mexe no RGB onde A = 0)
            ti.sRGBTexture = !data;
            ti.alphaSource = n == "title_bg" ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.filterMode = n == "glow" || n == "fog" || n == "bat" || n.StartsWith("chain_") ? FilterMode.Point : FilterMode.Bilinear;
            ti.wrapModeU = n == "fog" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            if (sprite)
            {
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                s.spriteAlignment = (int)SpriteAlignment.Center;
                ti.SetTextureSettings(s);
                ti.spriteBorder = Vector4.zero;
            }
        }
    }
}
