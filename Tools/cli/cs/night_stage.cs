var views = @"
u1 0.6 1.4 -93.7 4.9 1.99 -90.17 36
";
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;stage=1;yaw=50;impact=0.9;flash=0.2", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night"), 960, 540);
