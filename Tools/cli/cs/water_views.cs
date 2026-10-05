var views = @"
w_ponte 38 1.5 4 43 -14 -20 60
w_canion 41 -13.5 -10 42 -15 -40 60
w_vitral 4 2 -2 9.6 20 8.3 60
w_rosa -6 2.2 6 -24 6 14 55
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/hq2");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
