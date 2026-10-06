#!/bin/bash
# uso: Tools/cli/world_build.sh Valteria [Velaria ...]  |  all  |  dungeons  |  map  |  settings
# Gera as cenas do mundo de Elyndra pelo editor aberto (jobs destacados) e imprime o log de cada uma.
cd ~/Unity/ParkourLab
tmp=$(mktemp -d)
run() {
  id=$(unity --json command eval_file --file "$1" --timeout 2400 --detach 2>&1 | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["jobId"])' 2>/dev/null)
  [ -z "$id" ] && { echo "falhou ao submeter $1"; return 1; }
  while true; do
    sleep 8
    out=$(unity --json job status $id 2>&1)
    st=$(echo "$out" | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["state"])' 2>/dev/null)
    case "$st" in completed|failed|cancelled) break;; esac
  done
  echo "$out" | python3 -c '
import sys,json
d=json.load(sys.stdin)["data"]; r=d.get("result") or {}
print(r.get("result") if isinstance(r,dict) else r)
if d.get("error"): print("ERRO:", d["error"], str(d.get("errorDetails"))[:2000])'
}
for arg in "$@"; do
  case "$arg" in
    all) echo 'return Elyndra.WorldEditor.ElyndraBuild.BuildAll();' > $tmp/a.cs; run $tmp/a.cs;;
    prepare) echo 'return Elyndra.WorldEditor.ElyndraBuild.Prepare();' > $tmp/a.cs; run $tmp/a.cs;;
    dungeons) echo 'var s=""; foreach (var d in Elyndra.World.WorldCanon.Dungeons) s += Elyndra.WorldEditor.ElyndraBuild.BuildDungeon(d.id); return s;' > $tmp/a.cs; run $tmp/a.cs;;
    map) echo 'return Elyndra.WorldEditor.WorldMapBuilder.Build();' > $tmp/a.cs; run $tmp/a.cs;;
    settings) echo 'return Elyndra.WorldEditor.ElyndraBuild.UpdateBuildSettings();' > $tmp/a.cs; run $tmp/a.cs;;
    D_*) echo "return Elyndra.WorldEditor.ElyndraBuild.BuildDungeon(\"d_${arg#D_}\");" > $tmp/a.cs; run $tmp/a.cs;;
    *) sed "s/REGIAO/$arg/" Tools/cli/cs/world_region.cs > $tmp/a.cs; run $tmp/a.cs;;
  esac
done
rm -rf $tmp
