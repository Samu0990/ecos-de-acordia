var views = @"
w1_perto 3.2 1.8 -21 6.5 2.6 -19.5 55
w2_casa -3.5 1.7 -25 -6 3 -22 50
w3_rua 1 1.8 -38 -1 3 -10 55
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5", dir, 960, 540);
