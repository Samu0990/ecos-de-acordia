var views = @"
p_feno 3 1.5 -66 0 0.6 -62 60
p_viga 0 1.6 -57 0 1.2 -62 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/hq2");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5", dir, 960, 540) + Aren.EditorTools.NightPreview.Render("pd_feno 3 1.5 -66 0 0.6 -62 60", "day=1", dir, 960, 540);
