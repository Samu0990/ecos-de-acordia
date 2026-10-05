var views = @"
r1 -14 14 -118 3 2 -50 48
r2 0 1.7 -95 0 3 -45 55
";
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;stage=1", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night"), 960, 540);
