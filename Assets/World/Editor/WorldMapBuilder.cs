using System.Linq;
using Elyndra.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Cena WorldMap: o continente de Elyndra em miniatura (1 km = 20 m): costa, serras de Granith, o vulcão da
    /// Coroa, o mar de vidro, as ilhas de Calíria, a Fronteira Muda cinza e a Fenda ao norte; pinos clicáveis
    /// com o nome de cada reino, rotas (abertas/trancadas/secretas) e Campânula marcada.
    /// </summary>
    public static class WorldMapBuilder
    {
        public const string ScenePath = "Assets/World/Scenes/WorldMap.unity";
        const float K = 20f;   // unidades por km

        static float LandMask(float kx, float kz)
        {
            float m = 0f;
            foreach (var r in WorldCanon.Regions)
            {
                float rad = r.radiusKm * (r.id == RegionId.MarDeVidro ? 0.4f : r.id == RegionId.Caliria ? 0.7f : 1.7f);
                float d = Vector2.Distance(new Vector2(kx, kz), r.km) / rad;
                m = Mathf.Max(m, 1f - d * d);
            }
            m += (Mathf.PerlinNoise(kx * 0.08f, kz * 0.08f) - 0.5f) * 0.5f;
            return m;
        }

        static float Height(float kx, float kz)
        {
            float land = LandMask(kx, kz);
            if (land < 0.05f) return -4f + land * 20f;
            float h = 4f + 6f * Mathf.PerlinNoise(kx * 0.15f + 3, kz * 0.15f) + 10f * RegionRecipes.Ridge(kx * K, kz * K, 400f, 3, 2f) * Mathf.Clamp01(land);
            h += RegionRecipes.Bump(kx, kz, 12, 40, 9, 85f) + RegionRecipes.Bump(kx, kz, 16, 44, 5, 50f) + RegionRecipes.Bump(kx, kz, 22, 30, 7, 35f);   // Granith + serra central
            h += RegionRecipes.Bump(kx, kz, 36, 56, 4.5f, 95f);   // vulcão
            h += RegionRecipes.Bump(kx, kz, 64, 62, 7, 45f) + RegionRecipes.Bump(kx, kz, 58, 42, 6, 22f);
            h += RegionRecipes.Bump(kx, kz, 48, 76, 10, 18f) * (0.6f + Mathf.PerlinNoise(kx * 0.5f, kz * 0.5f));
            return h * Mathf.Clamp01(land * 3f);
        }

        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Mapa de Elyndra").transform;
            float x0 = -100f, z0 = -100f, size = 1900f;
            int res = 513;
            var H = new float[res, res];
            float mn = -6f, mx = 140f;
            for (int iz = 0; iz < res; iz++) for (int ix = 0; ix < res; ix++)
                {
                    float x = x0 + size * ix / (res - 1), z = z0 + size * iz / (res - 1);
                    H[iz, ix] = (Height(x / K, z / K) - mn) / (mx - mn);
                }
            var td = new TerrainData { heightmapResolution = res };
            td.alphamapResolution = 256; td.size = new Vector3(size, mx - mn, size);
            // asset antes de pintar (senão o splatmap não é salvo e o mapa reabre todo verde)
            System.IO.Directory.CreateDirectory("Assets/World/Data/Terrain");
            AssetDatabase.DeleteAsset("Assets/World/Data/Terrain/WorldMap_Terreno.asset");
            AssetDatabase.CreateAsset(td, "Assets/World/Data/Terrain/WorldMap_Terreno.asset");
            td.SetHeights(0, 0, H);
            td.terrainLayers = new[] { "TL_grass", "TL_dirt", "TL_cobble", "TL_field" }.Select(n => AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Campanula/Materials/" + n + ".terrainlayer")).ToArray();
            int ar = 256; var a = new float[ar, ar, 4];
            for (int iz = 0; iz < ar; iz++) for (int ix = 0; ix < ar; ix++)
                {
                    float x = x0 + size * (ix + 0.5f) / ar, z = z0 + size * (iz + 0.5f) / ar;
                    float kx = x / K, kz = z / K, h = Height(kx, kz);
                    float snow = Mathf.Clamp01((h - 55f) / 25f);
                    float ash = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(kx, kz), new Vector2(36, 56)) / 9f) + Mathf.Clamp01(1f - Vector2.Distance(new Vector2(kx, kz), new Vector2(48, 76)) / 12f);
                    float sand = h < 1.5f ? 1f : 0f;
                    float sum = snow + ash + sand; if (sum > 1f) { snow /= sum; ash /= sum; sand /= sum; sum = 1f; }
                    a[iz, ix, 0] = 1f - sum; a[iz, ix, 1] = sand; a[iz, ix, 2] = Mathf.Clamp01(ash); a[iz, ix, 3] = snow;
                }
            td.SetAlphamaps(0, 0, a);
            EditorUtility.SetDirty(td);
            var tgo = Terrain.CreateTerrainGameObject(td); tgo.name = "Continente"; tgo.transform.SetParent(root, false); tgo.transform.position = new Vector3(x0, mn, z0);
            var terrain = tgo.GetComponent<Terrain>();
            terrain.materialTemplate = WorldMats.Terrain("WorldMap", new LayoutTextures { baseNear = "camp:gnd_meadow", baseFar = "camp:gnd_meadow", path = "sand", cobble = "ash", field = "snow", rock = "darkrock", nearTile = 12f, farTile = 60f });
            terrain.basemapDistance = 5000; terrain.heightmapPixelError = 3;
            float Y(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + mn;

            var sea = RegionBuilder.Object("Mar", ProcMesh.Box(4000, 0.2f, 4000), WorldMats.Water("WorldMap", new Color(0.03f, 0.1f, 0.14f, 0.9f), new Color(0.5f, 0.55f, 0.65f)), new Vector3(800, -0.3f, 800), Quaternion.identity, Vector3.one, root, false);
            RegionBuilder.Object("Mar de Vidro", ProcMesh.Box(420, 0.2f, 300), WorldMats.GlassSea(), new Vector3(52 * K, -0.1f, 6 * K), Quaternion.identity, Vector3.one, root, false);

            // luz e céu
            var sun = new GameObject("Luz").AddComponent<Light>(); sun.transform.SetParent(root, false); sun.type = LightType.Directional; sun.intensity = 1.0f; sun.color = new Color(1f, 0.85f, 0.7f); sun.transform.rotation = Quaternion.Euler(40, 150, 0); sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.42f, 0.5f); RenderSettings.ambientEquatorColor = new Color(0.4f, 0.35f, 0.33f); RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.1f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.0006f; RenderSettings.fogColor = new Color(0.35f, 0.3f, 0.36f);
            var prof = ScriptableObject.CreateInstance<RegionProfile>(); prof.region = RegionId.Valteria; prof.skyTop = new Color(0.06f, 0.05f, 0.12f); prof.skyHorizon = new Color(0.5f, 0.3f, 0.35f); prof.fogColor = RenderSettings.fogColor; prof.sunColor = sun.color; prof.sunIntensity = 1f;
            var skyMat = WorldMats.Sky(prof, Vector3.forward, 0.8f); RenderSettings.skybox = skyMat;
            var ls = new LightingSettings { name = "WorldMap_Luz", bakedGI = false, realtimeGI = false };
            AssetDatabase.DeleteAsset("Assets/World/Data/Lighting/WorldMap.lighting"); AssetDatabase.CreateAsset(ls, "Assets/World/Data/Lighting/WorldMap.lighting"); Lightmapping.lightingSettings = ls;

            // pinos dos reinos
            var camGo = new GameObject("Câmera do mapa"); camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 6000f; cam.fieldOfView = 45f; cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Aren.World.RenderScaler>();
            var ctl = camGo.AddComponent<WorldMapController>(); ctl.cam = cam; ctl.kmToUnits = K;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Aren/Resources/UI/Fonts/Cinzel.ttf");
            var pinMat = WorldMats.Stone("camp:ashlar", new Color(0.85f, 0.8f, 0.72f), 2f);
            foreach (var r in WorldCanon.Regions)
            {
                var p = new Vector3(r.km.x * K, 0, r.km.y * K); p.y = Y(p.x, p.z);
                var pin = new GameObject("Pino — " + r.name).transform; pin.SetParent(root, false); pin.position = p;
                RegionBuilder.Object("Pedestal", ProcMesh.Prism(6, 6f, 4f, 10f), pinMat, p, Quaternion.identity, Vector3.one, pin, false);
                Color c = r.noteId != null ? new Color(1.6f, 0.7f, 0.4f) : r.id == RegionId.FronteiraMuda ? new Color(1.1f, 0.4f, 1.5f) : new Color(1.1f, 1.1f, 1.3f);
                RegionBuilder.Object("Chama do reino", ProcMesh.Sphere(10), WorldMats.Glow(c, 0.3f, 1.4f), p + Vector3.up * 13f, Quaternion.identity, Vector3.one * 4f, pin, false);
                var col = pin.gameObject.AddComponent<BoxCollider>(); col.center = Vector3.up * 8f; col.size = new Vector3(24, 30, 24);
                var lab = new GameObject("Nome").AddComponent<TextMesh>(); lab.transform.SetParent(pin, false); lab.transform.localPosition = Vector3.up * 26f;
                lab.text = r.name.ToUpperInvariant(); lab.characterSize = 2.2f; lab.fontSize = 60; lab.anchor = TextAnchor.MiddleCenter; lab.alignment = TextAlignment.Center; lab.color = new Color(1f, 0.92f, 0.8f);
                if (font != null) { lab.font = font; lab.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
                lab.transform.rotation = Quaternion.Euler(52f, 0, 0);
                ctl.pins.Add(new WorldMapController.Pin { id = r.id, t = pin });
            }
            // Campânula
            {
                var p = new Vector3(WorldCanon.CampanulaKm.x * K - 30f, 0, WorldCanon.CampanulaKm.y * K - 10f); p.y = Y(p.x, p.z);
                RegionBuilder.Kit("GTower_A", new Vector3(p.x, 0, p.z), 0, root, 1.6f, false, false).transform.position = p;
                var lab = new GameObject("Campânula").AddComponent<TextMesh>(); lab.transform.SetParent(root, false); lab.transform.position = p + Vector3.up * 34f;
                lab.text = "Campânula"; lab.characterSize = 1.6f; lab.fontSize = 50; lab.anchor = TextAnchor.MiddleCenter; lab.color = new Color(1f, 0.8f, 0.55f);
                if (font != null) { lab.font = font; lab.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
                lab.transform.rotation = Quaternion.Euler(52f, 0, 0);
            }
            // rotas
            var lineMat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.DeleteAsset("Assets/World/Materials/map_route.mat"); AssetDatabase.CreateAsset(lineMat, "Assets/World/Materials/map_route.mat");
            foreach (var rt in WorldCanon.Routes)
            {
                var A = WorldCanon.Region(rt.a).km * K; var B = WorldCanon.Region(rt.b).km * K;
                var go = new GameObject("Rota — " + rt.name); go.transform.SetParent(root, false);
                var lr = go.AddComponent<LineRenderer>();
                int n = 24; lr.positionCount = n; lr.widthMultiplier = rt.kind == RouteKind.Estrada ? 4f : 2.6f; lr.sharedMaterial = lineMat;
                var perp = new Vector2(-(B - A).y, (B - A).x).normalized * (Mathf.PerlinNoise(A.x * 0.01f, B.y * 0.01f) - 0.5f) * 120f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (n - 1f);
                    var q = Vector2.Lerp(A, B, t) + perp * Mathf.Sin(t * Mathf.PI);
                    lr.SetPosition(i, new Vector3(q.x, Mathf.Max(0.5f, Y(q.x, q.y)) + 3f, q.y));
                }
                Color c = rt.secret ? new Color(0.7f, 0.4f, 1f, 0.6f) : string.IsNullOrEmpty(rt.requires) ? new Color(1f, 0.8f, 0.45f, 0.95f) : new Color(0.8f, 0.25f, 0.25f, 0.85f);
                lr.startColor = lr.endColor = c;
            }
            // a Fenda ao norte
            var rift = GameObject.CreatePrimitive(PrimitiveType.Quad); rift.name = "A Fenda do Contracanto";
            Object.DestroyImmediate(rift.GetComponent<Collider>()); rift.transform.SetParent(root, false);
            rift.transform.position = new Vector3(WorldCanon.FendaKm.x * K, 160f, WorldCanon.FendaKm.y * K); rift.transform.rotation = Quaternion.Euler(0, 180, 0); rift.transform.localScale = new Vector3(140, 300, 1);
            rift.GetComponent<MeshRenderer>().sharedMaterial = WorldMats.Rift(); rift.AddComponent<Campanula.RiftPulse>();
            // miniaturas que identificam os reinos de longe
            RegionBuilder.Object("Vulcão (miniatura)", ProcMesh.Sphere(10, 0f, 0, true), WorldMats.Glow(new Color(2.4f, 0.7f, 0.2f), 0.3f, 1f), new Vector3(36 * K, Y(36 * K, 56 * K) + 4f, 56 * K), Quaternion.identity, new Vector3(12, 4, 12), root, false);
            for (int i = 0; i < 4; i++) RegionBuilder.Object("Torre de vidro (miniatura)", ProcMesh.Prism(6, 3f, 1f, 40f), WorldMats.Crystal(new Color(0.6f, 0.7f, 0.8f, 0.6f), new Color(1.1f, 1.3f, 1.5f), new Color(0.4f, 0.5f, 0.7f)), new Vector3(26 * K + i * 14f - 20f, Y(26 * K, 36 * K), 36 * K + (i % 2) * 12f), Quaternion.identity, Vector3.one, root, false);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return "mapa salvo: " + ScenePath + "\n";
        }
    }
}
