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
            string fn = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            // oclusão de ambiente assada (kit_gothic.py): dado linear, atlas grande
            if (fn.StartsWith("ao_atlas"))
            {
                ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.alphaSource = TextureImporterAlphaSource.None;
                ti.maxTextureSize = fn == "ao_atlas" || fn == "ao_atlas_town" ? 4096 : 2048; ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Compressed;
                return;
            }
            // pedra e telhado vistos de perto: albedo e normal em 2048 (fetch_polyhaven.py --2k)
            if (assetPath.Contains("/PH/") && (fn.EndsWith("_albedo") || fn.EndsWith("_normal")) && !fn.StartsWith("terrain_")) ti.maxTextureSize = 2048;
            if (fn == "leaf_atlas" || fn == "leaf_atlas_normal")
            {
                // atlas das folhas (leaf_atlas.py): 2048, recorte pelo alfa que não some de longe
                ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 2; ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Compressed;
                if (fn == "leaf_atlas")
                {
                    ti.sRGBTexture = true; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = true;
                    ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.45f;
                }
                else { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
                return;
            }
            // rochas da natureza (kit_nature.py): atlas assado do relevo (normal) e máscara linear (R oclusão, G arestas, B fendas, A tom)
            if (fn.StartsWith("nature_"))
            {
                ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Compressed;
                if (fn.EndsWith("_normal")) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
                else { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false; }
                return;
            }
            if (fn == "macro_noise") { ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapEnabled = true; ti.maxTextureSize = 512; ti.textureCompression = TextureImporterCompression.Uncompressed; return; }
            if (fn.EndsWith("_height")) { ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapEnabled = true; ti.anisoLevel = 4; ti.textureCompression = TextureImporterCompression.Compressed; return; }
            ti.mipmapEnabled = true;
            ti.anisoLevel = fn.StartsWith("gnd_") ? 8 : 4;   // terreno v2: o chão é visto rasante
            ti.textureCompression = TextureImporterCompression.Compressed;
            if (System.IO.Path.GetFileName(assetPath).StartsWith("detail_"))
            {
                // capim/trigo do terreno: alfa recortado, sem repetir nas bordas
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                return;
            }
            if (assetPath.EndsWith("_normal.png") || assetPath.EndsWith("_normal.jpg"))
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
            // UV2 para as sombras assadas (lightmap) — menos nos modelos góticos: o UV2 deles é o do atlas
            // de oclusão de ambiente assada no Blender (kit_gothic.py), que o Unity não pode sobrescrever
            string mname = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool bakedAO = mname.StartsWith("GHouse_") || mname.StartsWith("GTavern") || mname.StartsWith("GTower_")
                || mname == "Aqueduct" || mname == "Great_Aqueduct" || mname == "Gorge_Wall" || mname == "Bell_Pavilion"
                || mname.StartsWith("THouse_") || mname.StartsWith("TGuild") || mname.StartsWith("TCornerTower")
                || mname.StartsWith("TCatedral") || mname.StartsWith("TArch_") || mname.StartsWith("TPassage_") || mname.StartsWith("TCage");   // casas variadas (kit_town.py)
            // rochas da natureza (kit_nature.py): o normal map foi assado sobre as normais exportadas — o Unity
            // não pode recalculá-las (nem gerar UV2: elas não entram em lightmap)
            bool nature = mname.StartsWith("Nature_");
            mi.generateSecondaryUV = !bakedAO && !nature;
            // árvores/arbustos (kit_trees.py): normais esféricas personalizadas na copa
            bool foliage = System.IO.Path.GetFileName(assetPath).StartsWith("Tree") || System.IO.Path.GetFileName(assetPath).StartsWith("Bush");
            mi.importNormals = foliage || nature ? ModelImporterNormals.Import : ModelImporterNormals.Calculate;
            if (nature) mi.importTangents = ModelImporterTangents.CalculateMikk;
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
            // Standard + a luz da cidade à noite (Campanula/CityLit; de dia é igual ao Standard)
            m.shader = Shader.Find("Campanula/CityLit") ?? Shader.Find("Standard");
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
            // relevo: mapa de altura do Poly Haven (paralaxe no Campanula/CityLit)
            var hm = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/" + key + "_height.png");
            m.SetTexture("_ParallaxMap", hm);
            m.SetFloat("_Parallax", key == "rock" ? 0.05f : key.StartsWith("roof") ? 0.025f : 0.035f);
            if (hm != null) m.EnableKeyword("_PARALLAXMAP"); else m.DisableKeyword("_PARALLAXMAP");
            // oclusão de ambiente assada nos modelos góticos (atlas de perto e de longe)
            m.SetTexture("_AOAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "ao_atlas.png"));
            m.SetTexture("_AOAtlasFar", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "ao_atlas_far.png"));
            m.SetTexture("_AOAtlasTown", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "ao_atlas_town.png"));
            m.SetTexture("_AOAtlasTownFar", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "ao_atlas_town_far.png"));
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

        /// <summary>
        /// Materiais das rochas da natureza (NAT_rock_a/b, shader Campanula/NatureRock): atlas assado do kit_nature.py
        /// + fotos do Poly Haven (lichen_rock, mossy_rock) na escala real. Reimporta os Nature_* para pegarem o material.
        /// </summary>
        [MenuItem("Campanula/Setup/Rochas da natureza")]
        public static string NatureRocks()
        {
            int n = 0;
            foreach (var atlas in new[] { "a", "b", "c" })
            {
                bool wood = atlas == "c";   // troncos e tocos: casca pelo 2º UV
                string path = Mat + (wood ? "NAT_wood" : "NAT_rock_" + atlas) + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(Shader.Find("Campanula/NatureRock")); AssetDatabase.CreateAsset(m, path); }
                m.shader = Shader.Find("Campanula/NatureRock");
                m.SetTexture("_Atlas", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "nature_" + atlas + "_normal.png"));
                m.SetTexture("_Mask", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "nature_" + atlas + "_mask.png"));
                string photo = wood ? "nat_bark" : "nat_rock";
                m.SetTexture("_Rock", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/" + photo + "_albedo.png"));
                m.SetTexture("_RockN", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/" + photo + "_normal.png"));
                m.SetTexture("_Moss", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "PH/nat_moss_albedo.png"));
                m.SetTexture("_Macro", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "macro_noise.png"));
                m.SetFloat("_RockTile", PhSize(photo, 2f));
                m.SetFloat("_Wood", wood ? 1f : 0f);
                if (wood) { m.EnableKeyword("_WOOD"); m.SetFloat("_Desat", 0.3f); m.SetColor("_RockTint", new Color(0.86f, 0.84f, 0.8f)); }
                else m.DisableKeyword("_WOOD");
                m.SetFloat("_MossTile", PhSize("nat_moss", 3f));
                // entulho e pedras soltas (atlas b): menos musgo que os afloramentos dos morros
                m.SetFloat("_MossAmount", atlas == "b" ? 0.45f : 0.7f);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            foreach (var guid in AssetDatabase.FindAssets("Nature_ t:Model", new[] { "Assets/Campanula/Models" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
                mi.SaveAndReimport();
            }
            return "rochas: " + n + " materiais\n";
        }

        /// <summary>Tamanho real (m) de uma foto do Poly Haven (ph_tiles.json).</summary>
        static float PhSize(string key, float fallback)
        {
            var path = Tex + "PH/ph_tiles.json";
            if (!System.IO.File.Exists(path)) return fallback;
            var mt = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(path), "\"" + key + "\"\\s*:\\s*\\{[^}]*\"size_m\"\\s*:\\s*([0-9.]+)");
            return mt.Success ? float.Parse(mt.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
        }

        [MenuItem("Campanula/Setup/Materiais")]
        public static string Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Campanula/Materials")) AssetDatabase.CreateFolder("Assets/Campanula", "Materials");
            Make("stone_wall", 0.12); Make("stone_dark", 0.12); Make("cobble", 0.18);
            Make("plaster", 0.06); Make("timber", 0.18); Make("planks", 0.14);
            Make("roof_tiles", 0.22); Make("roof_slate", 0.38);
            Make("cloth_red", 0.04); Make("cloth_blue", 0.04);
            Make("banner", 0.05, 0, null, null, false);
            Make("ashlar", 0.14); Make("trim", 0.12); Make("rock", 0.1);   // Campânula gótica (Poly Haven)   // estandarte com a clave de sol (kit_gothic.py; UV 0..1 no pano)
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
            // árvores v2 (kit_trees.py): cartões de folhas com o atlas do Blender (Campanula/LeafCards)
            var lc = AssetDatabase.LoadAssetAtPath<Material>(Mat + "CMP_leafcards.mat");
            if (lc == null) { lc = new Material(Shader.Find("Campanula/LeafCards")); AssetDatabase.CreateAsset(lc, Mat + "CMP_leafcards.mat"); }
            lc.shader = Shader.Find("Campanula/LeafCards");
            lc.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "leaf_atlas.png"));
            lc.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "leaf_atlas_normal.png"));
            lc.SetColor("_Color", new Color(1.05f, 1.08f, 1f));
            lc.enableInstancing = true;
            EditorUtility.SetDirty(lc);
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
