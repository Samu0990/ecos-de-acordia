var views = @"
h1 1.2 1.0 -91.0 -2 2.2 -95 40
h2 -17 23 -128 4 8 -12 46
h3 1 1.8 -38 -1 3 -10 55
";
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;stage=1;yaw=60", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night"), 960, 540);
