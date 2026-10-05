// cacos da Fenda: tempo depois do estilhaço (shards=T) em teleobjetiva e no plano aberto
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night");
var r = "";
foreach (var T in new[] { "0.25", "0.6", "1.2", "3", "12" })
    r += Aren.EditorTools.NightPreview.Render("s_tele_" + T + " 0 1.7 -95 290 160 958 30", "fenda=" + (float.Parse(T, System.Globalization.CultureInfo.InvariantCulture) < 1 ? "0.7" : "1") + ";pulse=0.5;burst=" + (T == "0.25" ? "1" : "0") + ";shards=" + T, dir, 960, 540) + "\n";
r += Aren.EditorTools.NightPreview.Render("s_aberto 0 40 -135 290 120 958 50\ns_norte 0 1.7 -95 0 12 300 50", "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
return r;
