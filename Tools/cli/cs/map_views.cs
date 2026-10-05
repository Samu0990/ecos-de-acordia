// vistas do mapa (antes/depois da reforma no estilo da referência)
var views = @"
m_cima 0 210 -40 0 0 5 60
m_obliqua -95 70 -150 5 0 10 50
m_portao 0 2.2 -60 0 5 0 55
m_mercado 0 2 -38 0 3 0 60
m_praca -14 3 -2 0 8 30 60
m_leste 10 4 4 60 2 12 60
m_ponte 30 3 -24 44 -4 10 55
m_canion 43 -10 -40 43 -6 20 60
m_cabeceira 44 3 30 44 -6 62 60
m_ref 20 18 -30 50 2 30 50
m_norte -14 3 28 -10 14 78 60
m_aqued -88 14 48 -40 12 78 55
";
var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Unity/ParkourLab/Tools/cli/shots/map");
var tag = System.IO.File.Exists("/tmp/claude-map-tag") ? System.IO.File.ReadAllText("/tmp/claude-map-tag").Trim() : "a";
return Aren.EditorTools.NightPreview.Render(views.Replace("m_", tag + "_"), "fenda=1;pulse=0.5;shards=12", dir, 960, 540);
