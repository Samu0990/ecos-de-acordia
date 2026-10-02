#!/bin/bash
# compila o executável Linux da demo e espera terminar
cd ~/Unity/ParkourLab
unity --json command build --target StandaloneLinux64 --outputPath "Builds/EcosDeAcordia_Demo/EcosDeAcordia.x86_64" --confirm true >/dev/null 2>&1
for i in $(seq 1 60); do
  sleep 20
  s=$(unity --json command build_status 2>&1 | python3 -c "import sys,json; d=json.load(sys.stdin); r=d['data']['result']; r=json.loads(r) if isinstance(r,str) else r; print(r.get('status'), r.get('result',''), 'erros=%s'%r.get('totalErrors',''), '%ss'%(int(r.get('buildTimeMs') or 0)//1000))" 2>/dev/null)
  case "$s" in completed*) echo "$s"; exit 0;; esac
done
echo "timeout"
