using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elyndra.World
{
    /// <summary>Testes/atalhos do executável: "-eda-region=Valteria" (ou D_valteria, WorldMap) começa direto naquela cena.</summary>
    public static class WorldBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("-eda-region="))
                {
                    string scene = a.Substring(12);
                    if (Application.CanStreamedLevelBeLoaded(scene)) { WorldState.Load(); SceneManager.LoadScene(scene); }
                    else Debug.LogError("[WorldBoot] cena não está no build: " + scene);
                    return;
                }
            if (WorldAutoTest.Requested && WorldAutoTest.Instance == null) new GameObject("Teste do mundo").AddComponent<WorldAutoTest>();
        }
    }
}
