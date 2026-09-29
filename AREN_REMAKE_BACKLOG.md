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
