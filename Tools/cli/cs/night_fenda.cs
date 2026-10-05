var views = @"
f1_norte 0 1.7 -95 0 12 300 50
f2_fenda 0 1.7 -95 290 150 958 40
f3_tele 0 1.7 -95 290 140 958 18
f4_grua 0 40 -135 290 120 958 55
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night");
var r = Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5", dir, 960, 540);
r += Aren.EditorTools.NightPreview.Render("o1_ponto 0 1.7 -95 290 140 958 18", "fenda=0.08", dir, 960, 540);
r += Aren.EditorTools.NightPreview.Render("o2_racha 0 1.7 -95 290 140 958 18", "fenda=0.35", dir, 960, 540);
r += Aren.EditorTools.NightPreview.Render("o3_estilhaca 0 1.7 -95 290 140 958 18", "fenda=0.6;wave=0.3", dir, 960, 540);
return r;
