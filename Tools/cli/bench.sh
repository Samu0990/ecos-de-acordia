#!/bin/bash
# roda o executável em modo benchmark (abre em tela cheia ~90 s) e mostra o resultado
mkdir -p ~/EcosBench; rm -f ~/EcosBench/*
cd ~/Unity/ParkourLab/Builds/EcosDeAcordia_Demo
timeout 240 ./EcosDeAcordia.x86_64 -eda-benchmark -logFile ~/EcosBench/player.log >/dev/null 2>&1
grep "BENCH " ~/EcosBench/bench.txt 2>/dev/null || grep -E "BENCH|Exception" ~/EcosBench/player.log | head
