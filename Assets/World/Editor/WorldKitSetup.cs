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
            if (assetPath.EndsWith("_albedo.png")) { ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.45f; }
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
                string kid = Path.GetFileName(dir);
                // um modelo = <id>.fbx; um kit separado (o forte) = <id>_pNN.fbx, todos com o material do <id>
                var fbxs = new System.Collections.Generic.List<string>();
                if (File.Exists($"{dir}/{kid}.fbx")) fbxs.Add($"{ModelDir}/{kid}/{kid}.fbx");
                foreach (var f in Directory.GetFiles(dir, kid + "_p*.fbx")) fbxs.Add($"{ModelDir}/{kid}/{Path.GetFileName(f)}");
                foreach (var fbx in fbxs)
                {
                string id = Path.GetFileNameWithoutExtension(fbx);
                AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
                var meshes = AssetDatabase.LoadAllAssetsAtPath(fbx);
                Mesh lod0 = null, lod1 = null;
                foreach (var a in meshes)
                    if (a is Mesh m) { if (m.name.EndsWith("_LOD1")) lod1 = m; else lod0 = m; }
                if (lod0 == null) { log.Append($"{id}: sem malha\n"); continue; }
                var mat = Material(kid, dir);
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
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>Folha de conferência: cada prefab de 3/4, com uma pessoa de 1,8 m (cápsula) para escala, e
        /// uma seta +Z (para onde a peça "olha"). Saída: Tools/cli/shots/worldkit_ph.png.</summary>
        public static string Preview()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.GetFiles(PrefabDir, "*.prefab")) ids.Add(Path.GetFileNameWithoutExtension(f));
            const int W = 320, H = 240, cols = 5;
            int rows = (ids.Count + cols - 1) / cols;
            var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGB24, false);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var made = new System.Collections.Generic.List<GameObject>();
            GameObject Put(GameObject g) { UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(g, scene); made.Add(g); return g; }
            try
            {
                var sun = Put(new GameObject("sol")); var l = sun.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(40, 140, 0);
                var cam = Put(new GameObject("cam")).AddComponent<Camera>(); cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.62f, 0.72f); cam.fieldOfView = 35f;
                var man = Put(GameObject.CreatePrimitive(PrimitiveType.Capsule)); man.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                var arrow = Put(GameObject.CreatePrimitive(PrimitiveType.Cube));
                arrow.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = Color.red };
                for (int i = 0; i < ids.Count; i++)
                {
                    var g = Put((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{ids[i]}.prefab")));
                    var b = new Bounds(g.transform.position, Vector3.zero);
                    foreach (var rr in g.GetComponentsInChildren<Renderer>()) b.Encapsulate(rr.bounds);
                    float size = Mathf.Max(b.size.x, b.size.y, b.size.z, 2f);
                    man.transform.position = b.center + new Vector3(b.extents.x + 0.6f, 0, 0); man.transform.position = new Vector3(man.transform.position.x, 0.9f, man.transform.position.z);
                    arrow.transform.position = new Vector3(b.center.x, 0.05f, b.max.z + size * 0.08f); arrow.transform.localScale = new Vector3(size * 0.03f, 0.1f, size * 0.15f);
                    cam.transform.position = b.center + new Vector3(-0.8f, 0.55f, -1f).normalized * size * 1.9f;
                    cam.transform.LookAt(b.center);
                    var rt = RenderTexture.GetTemporary(W, H, 24); cam.targetTexture = rt; cam.aspect = W / (float)H; cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, W, H), (i % cols) * W, (rows - 1 - i / cols) * H);
                    RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
                    Object.DestroyImmediate(g); made.Remove(g);
                }
            }
            finally
            {
                foreach (var g in made) if (g != null) Object.DestroyImmediate(g);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            sheet.Apply();
            File.WriteAllBytes("Tools/cli/shots/worldkit_ph.png", sheet.EncodeToPNG());
            return string.Join(", ", ids);
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
            var alb = AssetDatabase.LoadAssetAtPath<Texture>($"{dir}/{id}_albedo.jpg");
            var albCut = AssetDatabase.LoadAssetAtPath<Texture>($"{dir}/{id}_albedo.png");   // plantas: folhas recortadas pelo alfa
            m.SetTexture("_MainTex", albCut != null ? albCut : alb);
            if (albCut != null)
            {
                m.SetFloat("_Mode", 1f); m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetInt("_SrcBlend", 1); m.SetInt("_DstBlend", 0); m.SetInt("_ZWrite", 1);
                m.renderQueue = 2450;
            }
            else { m.SetFloat("_Mode", 0f); m.DisableKeyword("_ALPHATEST_ON"); m.SetOverrideTag("RenderType", ""); m.renderQueue = -1; }
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
