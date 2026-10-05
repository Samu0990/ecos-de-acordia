var views = @"
top 0 260 10 0 0 11 60
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night");
return Aren.EditorTools.NightPreview.Render(views, "day=1;cine=0", dir, 900, 900);
