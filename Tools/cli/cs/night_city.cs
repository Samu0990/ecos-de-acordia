var views = @"
k1_estrada 0 1.7 -95 0 14 40 50
k2_grua -17 23 -128 4 14 30 46
k3_praca 0 2 10 0 22 86 60
k4_alto -60 60 -60 0 20 86 50
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5", dir, 960, 540);
