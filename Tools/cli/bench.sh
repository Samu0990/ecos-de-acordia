#!/bin/bash
# benchmark do executável (tela cheia ~90 s). uso: bench.sh [qualidade 0|1|2] [escala]
# padrão: Média + 80% (o que a demo usa de fábrica); não altera as configurações salvas
Q=${1:-1}; S=${2:-0.8}; SH=${3:-0}
mkdir -p ~/EcosBench; rm -f ~/EcosBench/bench.txt
cd ~/Unity/ParkourLab/Builds/EcosDeAcordia_Demo
timeout 240 ./EcosDeAcordia.x86_64 -eda-benchmark -eda-quality $Q -eda-scale $S -eda-shadows $SH -logFile ~/EcosBench/player.log >/dev/null 2>&1
echo "qualidade=$Q escala=$S sombras=$SH"; grep "BENCH " ~/EcosBench/bench.txt 2>/dev/null || grep -E "BENCH|Exception" ~/EcosBench/player.log | head
