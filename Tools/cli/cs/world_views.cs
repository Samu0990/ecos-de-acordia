// Renderiza vistas de uma cena do mundo (pontos de câmera de cinema + chegada) em Tools/cli/shots/world/
// Uso: editar SCENE abaixo (ou criar cópias) e rodar como job.
string SCENE = System.Environment.GetEnvironmentVariable("ELY_SCENE") ?? "Valteria";
string path = SCENE.StartsWith("D_") ? $"Assets/World/Scenes/Dungeons/{SCENE}.unity" : SCENE == "WorldMap" ? "Assets/World/Scenes/WorldMap.unity" : $"Assets/World/Scenes/Regions/{SCENE}.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
string outDir = "Tools/cli/shots/world"; System.IO.Directory.CreateDirectory(outDir);
var go = new UnityEngine.GameObject("CamPreview"); var cam = go.AddComponent<UnityEngine.Camera>();
cam.farClipPlane = 4000f; cam.nearClipPlane = 0.2f; cam.fieldOfView = 50f; cam.clearFlags = UnityEngine.CameraClearFlags.Skybox;
var rt = new UnityEngine.RenderTexture(1280, 720, 24, UnityEngine.RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
cam.targetTexture = rt;
var list = new System.Collections.Generic.List<(string, UnityEngine.Vector3, UnityEngine.Quaternion, float)>();
var spawn = UnityEngine.Object.FindAnyObjectByType<Elyndra.World.RegionRoot>();
if (spawn != null && spawn.defaultSpawn != null) { var s = spawn.defaultSpawn; list.Add(("chegada", s.position + UnityEngine.Vector3.up * 2.2f - s.forward * 5f, UnityEngine.Quaternion.LookRotation(s.forward + UnityEngine.Vector3.down * 0.08f), 55f)); }
foreach (var cp in UnityEngine.Object.FindObjectsByType<Elyndra.World.CinematicCameraPoint>(UnityEngine.FindObjectsSortMode.None))
    if (!cp.name.Contains("entrada") && !cp.name.Contains("alto")) list.Add((cp.label, cp.transform.position, cp.transform.rotation, cp.fov));
var route = UnityEngine.Object.FindAnyObjectByType<Elyndra.World.RegionRoute>();
if (route != null) for (int i = 1; i < route.points.Length; i += System.Math.Max(1, route.points.Length / 4)) { var a = route.points[i - 1]; var b = route.points[i]; list.Add(("rota" + i, a + UnityEngine.Vector3.up * 2f, UnityEngine.Quaternion.LookRotation((b - a).normalized + UnityEngine.Vector3.down * 0.05f), 60f)); }
UnityEngine.Shader.SetGlobalFloat("_ElyCorruption", 0.3f);
int n = 0; var sb = new System.Text.StringBuilder();
foreach (var (label, pos, rot, fov) in list)
{
    if (n >= 9) break;
    cam.transform.SetPositionAndRotation(pos, rot); cam.fieldOfView = fov;
    cam.Render();
    UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
    string f = $"{outDir}/{SCENE}_{n:00}.png";
    System.IO.File.WriteAllBytes(f, UnityEngine.ImageConversion.EncodeToPNG(tex));
    UnityEngine.Object.DestroyImmediate(tex);
    sb.Append(f + " — " + label + "\n"); n++;
}
UnityEngine.RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
return sb.ToString();
