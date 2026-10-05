// vistas de perto (detalhe, oclusão assada, relevo da pedra)
var views = @"
c_fachada -3 2 -26 -9 4.5 -24 55
c_porta 3 1.7 -15 7 2.2 -16 50
c_rua 0 1.8 -36 0 4 -10 60
c_praca -6 2.2 6 -24 6 14 55
c_norte -12 2.5 26 -10 9 38 55
c_ponte 33 2.5 11 44 1 9.5 55
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/map");
return Aren.EditorTools.NightPreview.Render(views, "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
