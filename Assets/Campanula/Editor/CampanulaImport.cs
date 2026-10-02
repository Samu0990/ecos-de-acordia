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
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Campanula/Textures")) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 512;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.Compressed;
            if (assetPath.EndsWith("_normal.png"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else ti.sRGBTexture = true;
        }

        void OnPreprocessModel()
        {
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
            mi.importNormals = ModelImporterNormals.Calculate;
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
            m.SetTexture("_MainTex", al);
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

        [MenuItem("Campanula/Setup/Materiais")]
        public static string Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Campanula/Materials")) AssetDatabase.CreateFolder("Assets/Campanula", "Materials");
            Make("stone_wall", 0.12); Make("stone_dark", 0.12); Make("cobble", 0.18);
            Make("plaster", 0.06); Make("timber", 0.18); Make("planks", 0.14);
            Make("roof_tiles", 0.22); Make("roof_slate", 0.38);
            Make("cloth_red", 0.04); Make("cloth_blue", 0.04);
            Make("bronze", 0.5, 0.65); Make("foliage", 0.05); Make("bark", 0.08); Make("straw", 0.05);
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
