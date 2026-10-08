using System.IO;
using UnityEditor;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Modelos escaneados do Poly Haven (CC0) reduzidos por ArtSource/WorldKit/scripts/wk_ph_reduce.py:
    /// regras de importação (sem materiais do FBX, texturas no formato do Standard), um material Standard por
    /// modelo e um prefab com LODGroup (LOD0, LOD1, some longe) e colisor (malha do LOD1 nas peças grandes,
    /// caixa nos objetos pequenos) em Assets/World/Prefabs/PolyHaven/&lt;id&gt;.prefab.
    /// </summary>
    public class WorldKitSetup : AssetPostprocessor
    {
        public const string ModelDir = "Assets/World/Models/PolyHaven";
        public const string PrefabDir = "Assets/World/Prefabs/PolyHaven";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelDir + "/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.globalScale = 1f;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ModelDir + "/")) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.jpg");
            bool linear = assetPath.EndsWith("_ms.png") || assetPath.EndsWith("_ao.jpg");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal && !linear;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.crunchedCompression = false;
            ti.anisoLevel = 4;
        }

        static bool Small(Vector3 size) => Mathf.Max(size.x, size.y, size.z) < 1.2f;

        [MenuItem("Elyndra/Setup/Modelos do Poly Haven")]
        public static string All()
        {
            var log = new System.Text.StringBuilder();
            if (!AssetDatabase.IsValidFolder("Assets/World/Prefabs")) AssetDatabase.CreateFolder("Assets/World", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/World/Prefabs", "PolyHaven");
            foreach (var dir in Directory.GetDirectories(ModelDir))
            {
                string id = Path.GetFileName(dir);
                string fbx = $"{ModelDir}/{id}/{id}.fbx";
                AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
                var meshes = AssetDatabase.LoadAllAssetsAtPath(fbx);
                Mesh lod0 = null, lod1 = null;
                foreach (var a in meshes)
                    if (a is Mesh m) { if (m.name.EndsWith("_LOD1")) lod1 = m; else lod0 = m; }
                if (lod0 == null) { log.Append($"{id}: sem malha\n"); continue; }
                var mat = Material(id, dir);
                var root = new GameObject(id);
                var g0 = Part(root, id + "_LOD0", lod0, mat);
                var g1 = lod1 != null ? Part(root, id + "_LOD1", lod1, mat) : null;
                var size = lod0.bounds.size;
                bool small = Small(size);
                var lg = root.AddComponent<LODGroup>();
                var r0 = new[] { g0.GetComponent<Renderer>() };
                lg.SetLODs(g1 != null
                    ? new[] { new LOD(small ? 0.12f : 0.35f, r0), new LOD(small ? 0.02f : 0.012f, new[] { g1.GetComponent<Renderer>() }) }
                    : new[] { new LOD(small ? 0.02f : 0.012f, r0) });
                lg.RecalculateBounds();
                if (small)
                {
                    var bc = root.AddComponent<BoxCollider>(); bc.center = lod0.bounds.center; bc.size = lod0.bounds.size;
                    foreach (Transform t in root.transform) t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    var mc = root.AddComponent<MeshCollider>(); mc.sharedMesh = lod1 != null ? lod1 : lod0;
                }
                GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{id}.prefab");
                Object.DestroyImmediate(root);
                log.Append($"{id}: {lod0.triangles.Length / 3} / {(lod1 != null ? lod1.triangles.Length / 3 : 0)} tri, {size.x:0.0}×{size.y:0.0}×{size.z:0.0} m\n");
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static GameObject Part(GameObject root, string n, Mesh mesh, Material mat)
        {
            var g = new GameObject(n);
            g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return g;
        }

        static Material Material(string id, string dir)
        {
            string p = $"{dir}/{id}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
            m.shader = Shader.Find("Standard");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>($"{dir}/{id}_albedo.jpg"));
            var nrm = AssetDatabase.LoadAssetAtPath<Texture>($"{dir}/{id}_normal.jpg");
            m.SetTexture("_BumpMap", nrm); if (nrm != null) m.EnableKeyword("_NORMALMAP");
            var ms = AssetDatabase.LoadAssetAtPath<Texture>($"{dir}/{id}_ms.png");
            m.SetTexture("_MetallicGlossMap", ms); if (ms != null) m.EnableKeyword("_METALLICGLOSSMAP");
            m.SetFloat("_GlossMapScale", 0.85f);
            var ao = AssetDatabase.LoadAssetAtPath<Texture>($"{dir}/{id}_ao.jpg");
            m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 0.8f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
