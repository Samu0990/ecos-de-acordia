# AREN REMAKE — BACKLOG

Problemas encontrados fora do momento. Classificação: BLOCKER / HIGH / MEDIUM / LOW / FUTURE.

| # | Prioridade | Item | Origem |
|---|---|---|---|
| 1 | LOW | `DetectionCharacterController.IsGrounded(stepHeight)` ignora o parâmetro (usa 0.3/0.7 fixos) | AUDITORIA.md #4 |
| 2 | LOW | `HandlePoints.CreatePoints` caso-limite de borda curta | AUDITORIA.md #6 |
| 3 | MEDIUM | Shimmy não confirmado em borda larga (a de 0.8 m é curta demais) | AUDITORIA.md #8 |
| 4 | LOW | Warning residual na transição do agarrão (provável MatchTarget) | Etapa A |
| 5 | FUTURE | Pulo previsto (`JumpPredictionController`) pousando em borda (Ledge) | PROGRESSO Etapa C |
| 6 | FUTURE | Wall slide / wall jump — sem level design para isso na cena | Audit |
| 7 | FUTURE | Leap of Faith, viewpoint, hold-to-run/climb (pedidos antigos, Etapa 5 original) — fora do escopo "só o jogador/combate" desta fase | pedidos anteriores |
| 8 | FUTURE | Trocar flauta por Alaúde Quebrado (lore) — prop é trocável | Audit conflito 1 |
| 9 | MEDIUM | Agarrar borda a partir do pulo livre no ar (o `ClimbCheck` do DPS exige chão ou coyote) | Fase 2 |
| 10 | LOW | Clipes Mixamo de "Running Turn 180" e pulo mais sóbrio para substituir o pivot procedural / NinjaJump | Fase 2 |

## Depois da demo (2026-10-02)
11. Animação de flauta de verdade (hoje: clipes de espada CC0 com a flauta na mão).
12. Rig do cervo por artista (pesos por distância esticam os chifres em poses extremas).
13. Rotas extras de telhados (os anexos/sacadas têm bordas, mas não há circuito desenhado).
14. Mixamo: clipes de esquiva/rolamento e golpes mais "musicais" (precisa do login do usuário).
15. Gamepad testado em hardware real (só teclado/mouse foram testados).

## Depois dos assets novos (2026-10-02, tarde)
16. Câmera de combate: no mercado estreito ela bate em paredes/props e fica atrás de objetos (capturas do `-eda-test`).
17. Orientação: 0–3% dos quadros de golpe com o tronco >90° do alvo quando o alvo morre no meio do golpe — redirecionar o golpe para o novo alvo ou medir pela direção do golpe.
18. Passos dos Ecos e do cervo com as amostras de passo (hoje só o Aren tem passos gravados).
19. Interior da taverna (o kit tem cama, estante, velas, caldeirão, lustre) — conteúdo novo barato.
20. Pacotes ainda não usados em ~/Downloads (Dark VFX, StatusFX, Sword Slashes, DarkAgesUi, Soulslike UI Kit): conferir licença antes.
21. Menu principal a ~45 FPS (o resto a 60): a câmera orbitando mostra a vila inteira; reduzir a distância de desenho só no menu.
