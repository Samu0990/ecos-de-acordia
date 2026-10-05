// S2 (bardo) e S3a (close) com outros fundos
var views = @"
w1 -2.0 1.35 -91.5 0.2 1.4 -92.0 32
w2 -1.6 1.3 -90.4 0.2 1.4 -92.1 30
w3 0.7 1.12 -92.6 -0.1 1.5 -91.9 26
w4 0.9 1.2 -92.9 -0.2 1.55 -91.8 28
";
return Aren.EditorTools.NightPreview.Render(views, "fenda=0;stage=1;yaw=0", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night"), 960, 540);
