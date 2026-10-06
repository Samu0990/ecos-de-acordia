// Vistas das rochas da natureza (kit_nature.py). Alturas RELATIVAS ao chão (câmera e alvo): nome x h z lx lh lz fov.
// As v_* repetem as câmeras das capturas hq2 (antes/depois).
string Rel(string src)
{
    var sb = new System.Text.StringBuilder();
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    foreach (var line in src.Split('\n'))
    {
        var a = line.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (a.Length < 8) continue;
        float F(int i) => float.Parse(a[i], ci);
        float y = F(2) + Campanula.EditorTools.CampanulaBuilder.GroundY(F(1), F(3));
        float ly = F(5) + Campanula.EditorTools.CampanulaBuilder.GroundY(F(4), F(6));
        sb.Append(string.Format(ci, "{0} {1} {2} {3} {4} {5} {6} {7}\n", a[0], F(1), y, F(3), F(4), ly, F(6), F(7)));
    }
    return sb.ToString();
}
var views = Rel(@"
v_estrada 0 1.8 -88 2 3 -60 60
v_morro 20 1.6 -95 40 8 -140 60
v_ponte 33 2.5 11 44 1 9.5 55
r_morro2 -20 2 -70 -60 6 -140 60
r_canion 41 2 -20 42 4 10 62
r_oeste -95 2 0 -140 4 20 60
r_leste 100 2 14 145 4 30 60
r_muro 16 1.7 -52 6 0.6 -44 60
r_estrada2 -2 1.7 -112 1 0.4 -96 60
r_bosque -80 1.8 -30 -95 0.5 -50 60
");
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/rocks");
var day = Rel(@"
d_morro 20 1.6 -95 40 8 -140 60
d_morro2 -20 2 -70 -60 6 -140 60
d_oeste -95 2 0 -140 4 20 60
d_leste 100 2 14 145 4 30 60
d_canion 41 2 -20 42 4 10 62
d_muro 16 1.7 -52 6 0.6 -44 60
d_bosque -80 1.8 -30 -95 0.5 -50 60
d_alto 0 60 -150 0 0 0 55
");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540) + "\n" + Aren.EditorTools.NightPreview.Render(day, "day=1", dir, 960, 540);
