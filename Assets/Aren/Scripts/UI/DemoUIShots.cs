using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Aren.UI
{
    /// <summary>
    /// -eda-ui-shot: entra no jogo, grava o HUD, abre a pausa, abre as Configurações ao lado (como na
    /// referência do autor), rola a lista, fecha com Esc e confere que o jogo continua pausado no menu
    /// de pausa. Capturas em ~/EcosBench/ui_*.png e resultado em ~/EcosBench/ui.txt.
    /// </summary>
    public class DemoUIShots : MonoBehaviour
    {
        public static bool Requested
        {
            get { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-ui-shot") return true; return false; }
        }

        string dir;
        IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "ui_" + name + ".png"));
            yield return null; yield return null;   // a captura sai no fim do quadro: não mexer na UI antes
        }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
            System.IO.Directory.CreateDirectory(dir);
            var outp = new StringBuilder();
            yield return new WaitForSecondsRealtime(4f);
            var flow = World.GameFlow.Instance;
            var menus = GameMenus.Instance;
            flow.DebugJump(2, new Vector3(0, 0, -33f), 0f);
            World.Notas.Reset();
            World.Notas.Add(1250);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("hud");
            yield return new WaitForSecondsRealtime(0.5f);

            flow.PauseFromTest();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("pause");
            outp.AppendLine("pausa: menu=" + menus.Current + " jogo=" + flow.Current);
            yield return new WaitForSecondsRealtime(0.4f);

            menus.OpenSettingsFromTest();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("settings");
            outp.AppendLine("configurações: menu=" + menus.Current);
            // desce até o fim da lista (rolagem automática pela seleção)
            var nav = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down };
            for (int i = 0; i < 14; i++)
            {
                var sel = EventSystem.current.currentSelectedGameObject;
                if (sel != null) ExecuteEvents.Execute(sel, nav, ExecuteEvents.moveHandler);
                yield return new WaitForSecondsRealtime(0.12f);
            }
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("settings_scroll");
            var last = EventSystem.current.currentSelectedGameObject;
            outp.AppendLine("selecionado no fim da lista: " + (last != null ? last.name : "(nada)"));

            menus.CloseSettingsFromTest();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("pause_back");
            bool ok = menus.Current == GameMenus.Screen.Pause && flow.Current == World.GameFlow.State.Paused;
            outp.AppendLine("fechar configurações volta para a pausa (jogo ainda pausado): " + (ok ? "OK" : "FALHOU") + " menu=" + menus.Current + " jogo=" + flow.Current);
            Debug.Log("UISHOT " + outp.ToString().Replace("\n", " | "));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "ui.txt"), outp.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
