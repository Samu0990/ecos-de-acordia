#!/bin/bash
# uso: Tools/cli/world_views.sh Valteria [Velaria D_valteria WorldMap ...]  |  all
# Renderiza as vistas de cada cena do mundo pelo editor aberto em Tools/cli/shots/world/<Cena>_NN.png
cd ~/Unity/ParkourLab
tmp=$(mktemp -d)
run() {
  id=$(unity --json command eval_file --file "$1" --timeout 900 --detach 2>&1 | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["jobId"])' 2>/dev/null)
  [ -z "$id" ] && { echo "falhou ao submeter $1"; return 1; }
  while true; do
    sleep 5
    out=$(unity --json job status $id 2>&1)
    st=$(echo "$out" | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["state"])' 2>/dev/null)
    case "$st" in completed|failed|cancelled) break;; esac
  done
  echo "$out" | python3 -c '
import sys,json
d=json.load(sys.stdin)["data"]; r=d.get("result") or {}
print(r.get("result") if isinstance(r,dict) else r)
if d.get("error"): print("ERRO:", d["error"], str(d.get("errorDetails"))[:1500])'
}
scenes="$@"
[ "$1" = "all" ] && scenes="Valteria Velaria Miralume Orvalume Helion Sefra Nereth Granith CoroaDeCinza MarDeVidro Caliria Sombrafonte FronteiraMuda"
for s in $scenes; do
  sed "s/?? \"Valteria\"/?? \"$s\"/" Tools/cli/cs/world_views.cs > $tmp/v.cs
  echo "== $s"; run $tmp/v.cs
done
rm -rf $tmp
