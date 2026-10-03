#!/bin/bash
# compila o executável Linux da demo e espera ESTE build terminar (confere o buildId:
# antes o script lia o status do build anterior e dizia "Succeeded" sem ter compilado nada)
cd ~/Unity/ParkourLab
for i in $(seq 1 100); do
  unity --json command recompile_status 2>&1 | grep -q 'compiling\|triggered' || break
  sleep 3
done
id=""
for try in 1 2 3 4 5 6 7 8; do
  raw=$(unity --json command build --target StandaloneLinux64 --outputPath "Builds/EcosDeAcordia_Demo/EcosDeAcordia.x86_64" --confirm true 2>&1)
  id=$(echo "$raw" | python3 -c "import sys,json; d=json.load(sys.stdin); r=(d.get('data') or {}).get('result') or {}; r=json.loads(r) if isinstance(r,str) else r; print(r.get('buildId',''))" 2>/dev/null)
  [ -n "$id" ] && break
  sleep 15   # editor ainda importando/compilando depois do refresh
done
[ -z "$id" ] && { echo "build não foi aceito pelo editor:"; echo "$raw" | head -c 600; exit 1; }
wait_build() {
  for i in $(seq 1 90); do
    sleep 10
    s=$(unity --json command build_status 2>&1 | python3 -c "import sys,json; d=json.load(sys.stdin); r=d['data']['result']; r=json.loads(r) if isinstance(r,str) else r; print(r.get('buildId'), r.get('status'), r.get('result',''), 'erros=%s'%r.get('totalErrors',''), '%ss'%(int(r.get('buildTimeMs') or 0)//1000))" 2>/dev/null)
    case "$s" in "$1 completed"*) echo "${s#* }"; return 0;; esac
  done
  echo "timeout esperando $1"; return 1
}
wait_build "$id"
