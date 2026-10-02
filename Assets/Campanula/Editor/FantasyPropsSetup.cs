using UnityEditor;
using UnityEngine;

namespace Campanula.EditorTools
{
    /// <summary>
    /// Materiais do Fantasy Props MegaKit (Quaternius, CC0) no shader Campanula/Trim e
    /// reimportação dos modelos com o remapeamento por nome (MI_*).
    /// </summary>
    public static class FantasyPropsSetup
    {
        const string Kit = "Assets/Campanula/ThirdParty/FantasyProps/";
        public static readonly string[] MaterialNames =
            { "MI_Trim_Furniture", "MI_Trim_Metal", "MI_Trim_Props", "MI_Trim_Cloth", "MI_Trim_Props_Vertex", "MI_Trim_Metal_Vertex", "MI_Banner", "MI_Page_Empty" };

        static Material Make(string name, string trim, bool vc = false, bool emblem = false, float smoothMul = 1f, Color? tint = null)
        {
            string path = Kit + "Materials/" + name + ".mat";
            var sh = Shader.Find("Campanula/Trim");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            T2(m, "_MainTex", trim == null ? null : Kit + "Textures/T_Trim_" + trim + "_BaseColor.png");
            T2(m, "_BumpMap", trim == null ? null : Kit + "Textures/T_Trim_" + trim + "_Normal.png");
            T2(m, "_ORM", trim == null ? null : Kit + "Textures/T_Trim_" + trim + "_ORM.png");
            m.SetColor("_Color", tint ?? Color.white);
            m.SetFloat("_SmoothMul", smoothMul);
            m.SetFloat("_UseVC", vc ? 1 : 0);
            m.SetFloat("_UseEmblem", emblem ? 1 : 0);
            if (vc) m.EnableKeyword("_TRIM_VC"); else m.DisableKeyword("_TRIM_VC");
            if (emblem) m.EnableKeyword("_TRIM_EMBLEM"); else m.DisableKeyword("_TRIM_EMBLEM");
            // estandartes e toldos são panos de face única: visíveis dos dois lados
            m.SetFloat("_Cull", emblem ? 0f : 2f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void T2(Material m, string prop, string path)
        {
            m.SetTexture(prop, path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        [MenuItem("Campanula/Setup/Props do kit (Quaternius)")]
        public static string Setup()
        {
            if (!AssetDatabase.IsValidFolder(Kit + "Materials")) AssetDatabase.CreateFolder(Kit.TrimEnd('/'), "Materials");
            // o pôr do sol rasante deixa o metal do kit espelhado demais: brilho um pouco contido
            Make("MI_Trim_Furniture", "Furniture", false, false, 0.8f);
            Make("MI_Trim_Metal", "Metal", false, false, 0.85f);
            Make("MI_Trim_Props", "Props", false, false, 0.9f);
            Make("MI_Trim_Cloth", "Cloth", false, false, 0.6f);
            Make("MI_Trim_Props_Vertex", "Props", true, false, 0.9f);
            Make("MI_Trim_Metal_Vertex", "Metal", true, false, 0.85f);
            Make("MI_Banner", "Cloth", true, true, 0.5f);
            Make("MI_Page_Empty", null, false, false, 0.2f, new Color(0.86f, 0.78f, 0.6f));
            AssetDatabase.SaveAssets();
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Kit + "Models" }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                n++;
            }
            return "materiais do kit ok, modelos reimportados: " + n + "\n";
        }
    }
}
