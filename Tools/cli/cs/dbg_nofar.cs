var views = @"
h_close 10 12 -95 18 22 -135 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/ground3");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;farlands=0", dir, 800, 450);
