# Progresso — ParkourLab

Projeto: `~/Unity/ParkourLab` (Unity 6000.3.22f1), base Dynamic Parkour System (MIT).
Notebook do usuário: Dell Latitude 3490, Linux Mint — meta de performance: ≥45 FPS em Play mode.

Este arquivo é o ponto de retomada caso a sessão seja interrompida. Sempre atualizar ao
fim de cada etapa/subetapa, com o que foi feito, valores antigos/novos e o que falta.

## Etapa 1-2 (plano inicial, já concluídas antes da Etapa A/B/C/D)

- Extração e merge dos 3 zips em `ParkourLab/` — OK.
- Projeto aberto e migrado para 6000.3.22f1 — compila sem erros.
- Cena `Testing.unity` roda, HUD de debug funciona, ~59-60 FPS.
- Animator: corrigidas 3 transições com duração ZERO (corte instantâneo):
  - `Hanging Movement -> Braced To FreeHang`: 0 → 0.15s
  - `Hanging Movement -> Free Hang To Braced`: 0 → 0.15s
  - `Freehang Drop -> Fall Idle`: 0 → 0.12s
- Blend Tree "Walk" recalibrada (thresholds não batiam com a velocidade real do clipe,
  medida via `AnimationClip.averageSpeed`):
  - Walk: threshold 1.5 → 1.372 (velocidade real do clipe)
  - Jog Forward: threshold 3.0 → 2.395 (era o pior caso, 25% de erro)
- `walkSpeed` no Player prefab: 3.0 → **2.4** m/s (a pedido do usuário, elimina o
  deslize residual que sobrava entre 2.4 e 3.0 m/s após a recalibração acima)
- Corrida (`Run`): clipe não tem root motion (loop in-place, `averageSpeed=0`), não dá
  pra recalibrar por threshold. Estendido o padrão já usado em `VaultSlide.cs`
  (parâmetro `AnimSpeed`): estado `Run` agora usa `speedParameter=AnimSpeed`, e
  `AnimationCharacterController.cs` seta `AnimSpeed = velocidade_real / RunSpeed`
  (clamp 0.6–1.4) só durante o estado Run.
- Personagem novo do usuário (`~/Unity/Personagem-para-Mixamo.zip` → `personagem.fbx`):
  malha estática ~61k vértices, sem esqueleto, visual de assassino com capuz. Pose:
  braços colados ao corpo (não T/A-pose), túnica fundida entre as pernas. Decisão do
  usuário: rig customizado feito no Blender (Etapa D), não usar auto-rig do Mixamo.

## Etapa A — Auditoria e bugs conhecidos (EM ANDAMENTO)

### Bugs reportados pelo usuário e status:

1. **Mãos flutuando no Freehang Idle (IK não aplicado)** — ✅ CORRIGIDO.
   Causa raiz: `ClimbController.IKSolver()` chamava `animator.SetIKPositionWeight(...)`
   de dentro de `Update()` (fora do callback `OnAnimatorIK`), corrompendo o pipeline de
   IK do Mecanim pro personagem inteiro. Isso não só deixava as mãos flutuando como
   fazia o **corpo inteiro desaparecer** no Free Hang (só as mãos ficavam visíveis —
   bug mais grave do que o relatado). Fix: removidas as 2 chamadas de
   `SetIKPositionWeight` de dentro de `IKSolver()`; a lógica real (dentro do
   `onAnimatorIK` já existente, chamado corretamente via
   `VaultingController.OnAnimatorIK -> VaultClimbLedge.OnAnimatorIK`) já cobre esse
   caso. Confirmado visualmente (screenshot antes/depois) em Braced Hang e Free Hang.

2. **Warning "Setting and getting Body Position/Rotation, IK Goals... should only be
   done in OnAnimatorIK or OnStateIK"** — ✅ MAJORITARIAMENTE CORRIGIDO (mesma causa
   do item 1). Antes: repetia todo frame enquanto pendurado. Depois do fix: caiu para
   um punhado de ocorrências isoladas durante a transição inicial de agarrar a borda
   (provavelmente `SetMatchTarget` em `ClimbUpdate()`, chamado fora do IK pass também
   — impacto bem menor, não é contínuo). Não persegui esse resíduo mais a fundo por
   ora; ficou registrado como item de baixa prioridade.

3. **HUD mostra "Vault: sim" durante a escalada** — ✅ CORRIGIDO (mas é um esclarecimento,
   não um bug de lógica). `isVaulting` é uma flag de exclusão mútua compartilhada por
   TODAS as VaultActions (`VaultObstacle`, `VaultOver`, `VaultSlide`, `VaultReach`,
   `VaultClimbLedge`, `VaultJumpPrediction`, `VaultDown`) — escalar (`Climb_Ledge`) é
   uma delas. Isso é arquitetura correta (impede vaultar uma caixa enquanto pendurado,
   por exemplo), não um bug. O HUD (`ParkourLabHUD.cs`, script próprio do projeto, não
   do asset original) foi ajustado pra não mostrar "Vault: sim" quando na verdade é
   escalada, já que existe indicador "Escalada" dedicado logo abaixo.

4. **Personagem trava na pose de pendurado** — ⚠️ NÃO CONFIRMADO COMO BUG. Testado com
   input real simulado (Input System) em Play mode: agarrar borda (Space), pendurar,
   pular para outra borda (W+Space), soltar (C) — tudo funcionou e o personagem
   respondeu normalmente a cada comando. O que parecia "travado" era só o personagem
   parado esperando input (comportamento normal). Shimmy (A/D) não moveu numa borda
   de 0.8m ("Small Ledge") — provavelmente não há espaço físico pra shimmy nessa
   borda específica (é bem curta), mas fica em aberto: **preciso que o usuário teste
   pessoalmente numa borda mais larga e me diga se realmente não anda**.

### Varredura ampla de scripts — CONCLUÍDA, ver `AUDITORIA.md`

Achados adicionais corrigidos:
- `VaultSlide.cs`: divisão por zero (`Infinity`) na primeira execução de um slide na
  sessão (`dis` calculado antes de `startPos`/`targetPos` serem atualizados). Testado
  em Play mode: slide roda suave agora, sem erro.
- `DetectionCharacterController.FindAheadPoints`: comparava posições absolutas em vez
  de direção relativa ao personagem (não filtrava "à frente" de verdade). Corrigido —
  relevante pra Etapa C, que estende esse mesmo sistema.
- `HandlePoints.cs`: `Destroy` -> `DestroyImmediate` (ferramenta de editor, não afeta
  gameplay).

Documentado mas **não corrigido** (baixo risco/prioridade, ver `AUDITORIA.md` para
detalhes): `IsGrounded(stepHeight)` ignora o parâmetro (usa valores fixos) — risco
alto de regressão se eu mexer sem testar exaustivamente; `HandlePoints.CreatePoints`
pode perder o ponto mais à direita em bordas muito curtas (edge case de ferramenta
de editor).

Testes de Play mode: escalada (Braced/Free Hang, pular entre bordas, soltar) OK;
vault sobre obstáculo OK; slide OK depois do fix. Console limpo em todos. Shimmy
(A/D) inconclusivo numa borda de 0.8m — precisa reteste do usuário numa borda maior.

Commits: `Etapa A: corrige bug crítico de IK na escalada`, mais um commit com
VaultSlide/DetectionCharacterController/HandlePoints + AUDITORIA.md.

## Etapa B — Fluidez (velocidade, animação, câmera) — EM ANDAMENTO

### B.1 — Velocidade e resposta (CONCLUÍDA)

- **Aceleração** (`MovementCharacterController.ApplyInputMovement`): medi ao vivo
  (Play mode, sampling por frame via hook) o tempo pra chegar na velocidade de
  caminhada partindo do zero. Com o fator original (`Time.fixedDeltaTime * 2`),
  levava **mais de 1.1s** pra chegar perto (90%) da velocidade alvo (2.4 m/s) —
  pesado/lento pra um parkour ágil. Troquei o fator pra `* 8`: agora chega a 90%
  em **~0.29s**, confirmado por nova medição ao vivo (ainda suave, não é
  arrancada instantânea). **Valor antigo: fator 2. Valor novo: fator 8.**
- **Desaceleração**: já convergia em ~0.1s (fator `* 20`, `SmoothStep`) — não
  parecia "gelo", não mexi.
- **Rotação**: já usa `SmoothDampAngle` com `turnSmoothTime = 0.1f` — exatamente
  na faixa pedida (0.1-0.15s). Não precisou de ajuste.
- **Velocidade física vs. animação**: já coberto nas etapas anteriores (Blend
  Tree recalibrada por `averageSpeed`, `AnimSpeed` sincronizado na corrida).

### B.2 — Animação (CONCLUÍDA a parte segura; resto documentado)

- **Bug real encontrado**: `MovementCharacterController.DisableFeetIK()` fazia
  `enableFeetIK = true` (deveria ser `false` - cópia e cola de `EnableFeetIK()` ao
  lado). Isso é chamado em `Fall()` pra desligar o IK de pé enquanto o personagem
  está no ar (sem chão pra alcançar) - mas nunca desligava de verdade. **Corrigido.**
  **Valor antigo: `true`. Valor novo: `false`.**
- **Foot IK durante a corrida**: já está ativo (não tem nenhum código que desliga
  especificamente durante o estado "Run" - só desligava, incorretamente, na queda,
  pelo bug acima). Não precisou de mudança adicional aqui.
- **Transições de aterrissagem com tranco**: `Fall Idle -> Fall A Land To Run
  Forward` e `Predicted Jump -> Fall A Land To Run Forward` tinham só 0.06s/0.059s
  de duração - praticamente um corte instantâneo entre a pose de queda e a pose de
  aterrissagem/corrida. Aumentei ambas para 0.15s. **Valores antigos: 0.060 e
  0.059. Valores novos: 0.15 cada.**
- **"Trava o controle ao aterrissar"**: não encontrei nenhum lugar que trave
  `allowMovement`/`dummy` na aterrissagem em si. `ApplyInputMovement()` só é
  pulado durante o estado "Fall" (o clipe de impacto final, curto) - parece
  intencional (comprometer com a pose de pouso) e não achei evidência de que
  seja a causa do "travamento" percebido. Testei queda/pouso sintético (teleporte
  no ar) sem erros no console. **Não consigo confirmar isso sem seu feedback de
  jogo real** - se depois de testar ainda sentir que trava, me diga em qual
  situação específica (pulando de onde, pra onde) que eu procuro de novo.
- **Inclinação procedural do corpo** (lean nas curvas/acelerações): não
  implementei ainda - decidi priorizar câmera, predição de movimento e o rig no
  Blender primeiro (mais volumosos e essa é uma feature nova, não um bug).
  Fica registrada como pendência pra retomar se sobrar tempo.

## Etapa C — Predição de movimento — PENDENTE

## Etapa D — Rig do personagem no Blender — PENDENTE
Aguardando Blender aberto com MCP conectado (usuário mencionou ter, mas não estava
aberto quando tentei usar antes). Vou reavisar quando chegar nessa etapa.

## Regras gerais sendo seguidas
- Não reescrever o sistema de parkour, só ajustar/estender.
- Documentar valores antigos/novos de tudo que for alterado (ver acima).
- Commit git ao fim de cada etapa/subetapa grande.
- Não usar animações do Unreal Game Animation Sample (licença).
- Rodar Play mode e checar console + FPS ao fim de cada etapa.
