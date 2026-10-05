var views = @"
v1_estrada 0 1.7 -95 0 8 0 50
v2_grua -17 23 -128 4 8 -12 46
v3_alto 0 70 -170 0 5 10 50
v4_praca 0 2 -10 0 6 30 60
";
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night"), 960, 540);
