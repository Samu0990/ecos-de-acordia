var views = @"
gr_campo 0 1.7 -60 -20 0.5 -85 60
gr_trigo 25 1.6 -80 40 0.8 -95 60
gr_grama 10 1.2 -70 14 0.2 -74 55
gr_baixo -30 0.8 -100 -34 0.5 -120 60
gr_leste 60 2 10 110 3 40 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/grass");
var a = Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
var b = Aren.EditorTools.NightPreview.Render("gd_trigo 25 1.6 -80 40 0.8 -95 60\ngd_baixo -30 0.8 -100 -34 0.5 -120 60", "day=1", dir, 960, 540);
return a + b;
