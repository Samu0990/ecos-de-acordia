using UnityEditor;
using UnityEngine;

namespace Campanula.EditorTools
{
    /// <summary>
    /// Importação dos assets da vila: texturas 512 px (normal maps marcadas), modelos em
    /// metros sem câmeras/luzes/animação e materiais remapeados por nome (CMP_*).
    /// </summary>
    public class CampanulaImport : AssetPostprocessor
    {
        const string Kit = "Assets/Campanula/ThirdParty/FantasyProps/";

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Kit + "Textures"))
            {
                // trim sheets 2048 do kit: 1024 basta para props (UHD 620 divide a RAM com a GPU)
                var kt = (TextureImporter)assetImporter;
                kt.maxTextureSize = 1024;
                kt.mipmapEnabled = true;
                kt.anisoLevel = 4;
                kt.textureCompression = TextureImporterCompression.Compressed;
                bool normal = assetPath.EndsWith("_Normal.png");
                kt.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                kt.sRGBTexture = !normal && !assetPath.EndsWith("_ORM.png");
                return;
            }
            if (!assetPath.StartsWith("Assets/Campanula/Textures")) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 1024;   // 2026-10-02: texturas geradas em 1024 (512 borrava de perto)
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.Compressed;
            if (System.IO.Path.GetFileName(assetPath).StartsWith("detail_"))
            {
                // capim/trigo do terreno: alfa recortado, sem repetir nas bordas
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                return;
            }
            if (assetPath.EndsWith("_normal.png"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else if (assetPath.EndsWith("_mg.png") || assetPath.EndsWith("_ao.png"))
            {
                // dados das texturas Poly Haven (suavidade no alfa / oclusão): lineares
                ti.sRGBTexture = false;
                ti.alphaSource = assetPath.EndsWith("_mg.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = false;
            }
            else ti.sRGBTexture = true;
        }

        void OnPreprocessModel()
        {
            if (assetPath.StartsWith(Kit + "Models"))
            {
                var km = (ModelImporter)assetImporter;
                km.globalScale = 1f;
                km.useFileScale = true;
                km.bakeAxisConversion = true;
                km.importCameras = false;
                km.importLights = false;
                km.importAnimation = false;
                km.animationType = ModelImporterAnimationType.None;
                km.isReadable = true;             // o builder combina tudo em static batching no editor
                km.addCollider = false;
                km.generateSecondaryUV = false;   // o UV2 do kit é a máscara de emblema dos estandartes
                km.importNormals = ModelImporterNormals.Import;
                km.importTangents = ModelImporterTangents.CalculateMikk;
                km.importBlendShapes = false;
                km.meshCompression = ModelImporterMeshCompression.Low;
                km.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                km.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var name in FantasyPropsSetup.MaterialNames)
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/" + name + ".mat");
                    if (m != null) km.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), m);
                }
                return;
            }
            if (!assetPath.StartsWith("Assets/Campanula/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;   // raiz sem a rotação −90° do Blender: o builder pode girar só em Y
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = true;   // MeshCollider e NavMesh leem a malha
            mi.addCollider = false;
            mi.generateSecondaryUV = true;   // UV2 para as sombras assadas (lightmap)
            // árvores/arbustos (kit_trees.py): normais esféricas personalizadas na copa
            bool foliage = System.IO.Path.GetFileName(assetPath).StartsWith("Tree") || System.IO.Path.GetFileName(assetPath).StartsWith("Bush");
            mi.importNormals = foliage ? ModelImporterNormals.Import : ModelImporterNormals.Calculate;
            mi.normalSmoothingAngle = 40f;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPostprocessModel(GameObject g)
        {
            if (!assetPath.StartsWith("Assets/Campanula/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
        }
    }

    public static class CampanulaMaterials
    {
        const string Tex = "Assets/Campanula/Textures/";
        const string Mat = "Assets/Campanula/Materials/";

        static Material Make(string key, double smooth, double metal = 0, Color? tint = null, Color? emission = null, bool normal = true)
        {
            string path = Mat + "CMP_" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Standard");
            var al = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + key + "_albedo.png");
            var nm = normal ? AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + key + "_normal.png") : null;
            // fotos PBR do Poly Haven (Tools/texgen/fetch_polyhaven.py), quando existem para este material
            var ph = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/" + key + "_albedo.png");
            Texture2D mg = null, ao = null; float tiling = 1f;
            if (ph != null)
            {
                al = ph;
                nm = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/" + key + "_normal.png") ?? nm;
                mg = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/" + key + "_mg.png");
                ao = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/" + key + "_ao.png");
                tiling = PhTiling(key);
            }
            m.SetTexture("_MainTex", al);
            m.SetTextureScale("_MainTex", new Vector2(tiling, tiling));
            m.SetTexture("_MetallicGlossMap", mg);
            if (mg != null) { m.EnableKeyword("_METALLICGLOSSMAP"); m.SetFloat("_GlossMapScale", (float)Mathf.Clamp01((float)smooth * 4f + 0.35f)); m.SetFloat("_SmoothnessTextureChannel", 0f); }
            else m.DisableKeyword("_METALLICGLOSSMAP");
            m.SetTexture("_OcclusionMap", ao);
            m.SetFloat("_OcclusionStrength", ao != null ? 0.8f : 1f);
            m.color = tint ?? Color.white;
            if (nm != null) { m.SetTexture("_BumpMap", nm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            m.SetFloat("_Glossiness", (float)smooth);
            m.SetFloat("_Metallic", (float)metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Repetição calibrada do Poly Haven (ph_tiles.json: TILE do kit / tamanho real da foto).</summary>
        static float PhTiling(string key)
        {
            var path = Tex + "PH/ph_tiles.json";
            if (!System.IO.File.Exists(path)) return 1f;
            var json = System.IO.File.ReadAllText(path);
            var mt = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\{[^}]*\"tiling\"\\s*:\\s*([0-9.]+)");
            return mt.Success ? float.Parse(mt.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 1f;
        }

        /// <summary>Camadas do terreno com as fotos do Poly Haven (só albedo), no tamanho real.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, float> TerrainPhTile = new System.Collections.Generic.Dictionary<string, float>
            { { "grass", 2.6f }, { "dirt", 2.2f }, { "cobble", 2.0f } };

        [MenuItem("Campanula/Setup/Terreno com fotos (Poly Haven)")]
        public static string TerrainPH()
        {
            int n = 0;
            foreach (var kv in TerrainPhTile)
            {
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Mat + "TL_" + kv.Key + ".terrainlayer");
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/terrain_" + kv.Key + "_albedo.png");
                if (tl == null || tex == null) continue;
                tl.diffuseTexture = tex;
                tl.tileSize = new Vector2(kv.Value, kv.Value);
                EditorUtility.SetDirty(tl); n++;
            }
            AssetDatabase.SaveAssets();
            return "camadas do terreno: " + n;
        }

        [MenuItem("Campanula/Setup/Materiais")]
        public static string Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Campanula/Materials")) AssetDatabase.CreateFolder("Assets/Campanula", "Materials");
            Make("stone_wall", 0.12); Make("stone_dark", 0.12); Make("cobble", 0.18);
            Make("plaster", 0.06); Make("timber", 0.18); Make("planks", 0.14);
            Make("roof_tiles", 0.22); Make("roof_slate", 0.38);
            Make("cloth_red", 0.04); Make("cloth_blue", 0.04);
            Make("bronze", 0.5, 0.65); Make("foliage", 0.05); Make("bark", 0.08); Make("straw", 0.05);
            // folhagem com vento, oclusão por vértice e contraluz (Campanula/Foliage)
            var fol = AssetDatabase.LoadAssetAtPath<Material>(Mat + "CMP_foliage.mat");
            if (fol != null)
            {
                fol.shader = Shader.Find("Campanula/Foliage");
                fol.SetColor("_Color", new Color(0.95f, 1f, 0.9f));
                fol.enableInstancing = true;
                EditorUtility.SetDirty(fol);
            }
            Make("grass", 0.05); Make("dirt", 0.08);
            Make("dark", 0f, 0f, new Color(0.02f, 0.02f, 0.02f), null, false);
            Make("iron", 0.45, 0.6, new Color(0.18f, 0.18f, 0.2f), null, false);
            // janelas acesas: o pôr do sol de Campanula com casas iluminadas por dentro
            Make("glass", 0.6, 0f, new Color(0.25f, 0.15f, 0.08f), new Color(1.6f, 0.85f, 0.35f), false);
            AssetDatabase.SaveAssets();

            // reimporta modelos para pegar o remapeamento
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Campanula/Models" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
                mi.SaveAndReimport();
            }
            return "materiais ok\n";
        }
    }
}
