var views = @"
n_estrada 0 1.8 -88 2 20 -60 65
n_alto 0 45 -150 0 60 0 60
n_fenda 0 3 -40 30 60 60 60
n_oeste 0 6 0 -100 60 -40 70
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/hq2");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
