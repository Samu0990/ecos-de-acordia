using System.Globalization;
using System.IO;
using Aren.World;
using Aren.World.Night;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>
    /// Prévia da noite da Ruptura no editor, sem Play mode (que roda a ~1 FPS nesta máquina):
    /// aplica a noite na cena aberta, renderiza vistas com o pós-processamento do jogo e desfaz
    /// tudo (nada é salvo). Usado pela CLI: Tools/cli/cs/night_views.cs.
    /// Vistas: uma por linha, "nome px py pz lx ly lz fov" (posição e ponto olhado, mundo).
    /// Parâmetros: "fenda=1;pulse=0.5;flash=0;impact=0;shock1=0;shockI1=0;shock2=0;shockI2=0;time=10".
    /// </summary>
    public static class NightPreview
    {
        public static string Render(string views, string parms, string outDir, int w = 1280, int h = 720)
        {
            var log = new System.Text.StringBuilder();
            var P = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var kv in parms.Split(';'))
            {
                var t = kv.Split('=');
                if (t.Length == 2) P[t[0].Trim()] = float.Parse(t[1], CultureInfo.InvariantCulture);
            }
            float Get(string k, float d) => P.TryGetValue(k, out var v) ? v : d;

            // guarda o estado da cena
            var skybox = RenderSettings.skybox;
            var ambMode = RenderSettings.ambientMode;
            Color aS = RenderSettings.ambientSkyColor, aE = RenderSettings.ambientEquatorColor, aG = RenderSettings.ambientGroundColor;
            Color fogC = RenderSettings.fogColor; float fogD = RenderSettings.fogDensity; float refl = RenderSettings.reflectionIntensity;
            var sun = RenderSettings.sun;
            string sunName = sun != null ? sun.name : null; Color sunC = sun != null ? sun.color : Color.white;
            float sunI = sun != null ? sun.intensity : 1f; Quaternion sunR = sun != null ? sun.transform.rotation : Quaternion.identity;
            float sunS = sun != null ? sun.shadowStrength : 1f;
            var prevShadows = QualitySettings.shadows;
            float c0 = RenderScaler.Contrast, s0 = RenderScaler.Saturation, v0 = RenderScaler.Vignette, e0 = RenderScaler.Exposure;
            Color st0 = RenderScaler.ShadowTint, ht0 = RenderScaler.HighTint;
            bool bloom0 = RenderScaler.PostBloom;

            GameObject camGo = null;
            RenderTexture rt = null, outRt = null;
            try
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                NightSetup.Teardown();
                NightSetup.Apply(true);
                var sky = NightSetup.Sky;
                if (sky != null)
                {
                    sky.SetFloat("_FendaOpen", Get("fenda", 0f));
                    sky.SetFloat("_FendaPulse", Get("pulse", 0.5f));
                    sky.SetFloat("_Flash", Get("flash", 0f));
                    sky.SetFloat("_FendaInhale", Get("inhale", 0f));
                    sky.SetFloat("_FendaBurst", Get("burst", 0f));
                    sky.SetFloat("_FendaWave", Get("wave", 0f));
                }
                Shader.SetGlobalColor("_FendaLight", NightSetup.FendaGlow * Get("fenda", 0f) * 0.55f);
                Shader.SetGlobalColor("_ImpactLight", new Color(1f, 0.72f, 0.4f) * Get("impact", 0f));
                Shader.SetGlobalVector("_Shock", new Vector4(Get("shock1", 0f), Get("shockI1", 0f), Get("shock2", 0f), Get("shockI2", 0f)));
                RenderScaler.PostBloom = Get("bloom", 1f) > 0.5f;
                RenderScaler.Cinematic = Get("cine", 1f) > 0.5f;
                RenderScaler.ShaftIntensity = Get("shaft", 0f);
                RenderScaler.ShaftPos = new Vector2(Get("shaftx", 0.5f), Get("shafty", 0.6f));
                Directory.CreateDirectory(outDir);

                camGo = new GameObject("NightPreviewCam") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = 0.2f; cam.farClipPlane = NightSetup.FarClip;
                cam.clearFlags = CameraClearFlags.Skybox; cam.useOcclusionCulling = false;
                cam.allowHDR = RenderScaler.Cinematic;
                rt = new RenderTexture(w, h, 24, RenderScaler.Cinematic ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                outRt = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
                cam.targetTexture = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                foreach (var line in views.Split('\n'))
                {
                    var a = line.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (a.Length < 8) continue;
                    float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
                    var pos = new Vector3(F(1), F(2), F(3));
                    var look = new Vector3(F(4), F(5), F(6));
                    cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                    cam.fieldOfView = F(7);
                    cam.Render();
                    RenderScaler.Composite(rt, outRt);
                    RenderTexture.active = outRt;
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, a[0] + ".png"), tex.EncodeToPNG());
                    log.Append(a[0]).Append(' ');
                }
                RenderTexture.active = null;
                Object.DestroyImmediate(tex);
                log.Append("| impacto em ").Append(FarLands.ImpactPoint);
            }
            catch (System.Exception e) { log.Append("ERRO: ").Append(e); }
            finally
            {
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (outRt != null) { outRt.Release(); Object.DestroyImmediate(outRt); }
                NightSetup.Teardown();
                RenderSettings.skybox = skybox; RenderSettings.ambientMode = ambMode;
                RenderSettings.ambientSkyColor = aS; RenderSettings.ambientEquatorColor = aE; RenderSettings.ambientGroundColor = aG;
                RenderSettings.fogColor = fogC; RenderSettings.fogDensity = fogD; RenderSettings.reflectionIntensity = refl;
                if (sun != null) { sun.name = sunName; sun.color = sunC; sun.intensity = sunI; sun.transform.rotation = sunR; sun.shadowStrength = sunS; }
                QualitySettings.shadows = prevShadows;
                RenderScaler.Contrast = c0; RenderScaler.Saturation = s0; RenderScaler.Vignette = v0; RenderScaler.Exposure = e0;
                RenderScaler.ShadowTint = st0; RenderScaler.HighTint = ht0; RenderScaler.PostBloom = bloom0;
                RenderScaler.Cinematic = false; RenderScaler.ShaftIntensity = 0f;
            }
            return log.ToString();
        }
    }
}
