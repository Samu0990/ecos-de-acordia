var views = @"
n1_norte 0 1.7 -95 0 12 300 50
n2_fenda 0 1.7 -95 290 150 958 40
n3_leste 0 1.7 -95 2348 200 860 40
n4_grua 0 40 -135 0 0 120 55
n5_grua_leste -20 70 -110 2348 150 860 45
n6_aereo -400 700 -1100 300 0 600 60
n7_alto_norte 0 120 -420 300 300 3000 50
";
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/night"), 960, 540);
