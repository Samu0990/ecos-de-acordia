# Ferramentas de teste pela CLI do Unity (usadas pelo remake do Aren)

Rodam contra o editor aberto (`unity status` = ready, pacote com.unity.pipeline).

- `ue arquivo.cs` — executa C# no editor (limite de 5 s por operação).
- `uej arquivo.cs` — mesmo, como job destacado (operações longas: importação, reimport).
- `rc` — recompila e espera terminar.
- `cons [n]` — contagem e últimas entradas de erro/warning do Console.
- `play` — sai do Play, recompila, entra em Play e espera o jogo rodar.
- `cap nome` — captura a Game view (câmera) em `shots/nome.png`.
- `lib.sh` — `tp x y z yaw` (reset limpo do jogador), `probe "<timeline>" dur "<setup>"`,
  `probe_bg`, `summarize`, `fpsfromprobe` — robô `ArenTestProbe` (input simulado dentro do loop).
