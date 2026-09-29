# AREN REMAKE — EXECUTION LOG

# PHASE 0 — Proteção + auditoria

Status: CONCLUÍDA

## What changed
- Branch `remake-aren` criada a partir de `main` (`c4c085a`). `main` fica como backup.
- Criados `ARENA_PLAYER_MASTER_AUDIT.md`, `AREN_REMAKE_EXECUTION_PLAN.md`,
  `AREN_REMAKE_BACKLOG.md`, este log.

## Files changed
Somente documentos na raiz do projeto.

## Tests
Nenhum teste de gameplay (fase sem código).

## Problems found
Ver audit: sem pulo livre, sem combate, sem VFX, sem áudio, Aren sem rig, modelo sem
flauta/capa, documento "Player Master" ausente.

## Next phase
Fase 1 — Input.

# PHASE 1 — Input

Status: CONCLUÍDA

## What changed
- `Assets/Aren/Scripts/Input/ArenInput.cs`: ações Attack / Counter / Dodge / Ability
  (teclado+mouse e gamepad) criadas em código — o `PlayerControls.cs` gerado do DPS não
  foi tocado. `ActionBuffer`: 1 ação na fila (Freeflow §16), janela 0.2 s, prioridade
  Counter > Dodge > Attack > Ability (defesa não é engolida por ataque apertado logo
  depois), histórico de 8 eventos para debug. Vetor de intenção relativo à câmera com
  deadzone 0.2 e "grace" de 0.12 s para teclado.
- UAL2 (Quaternius, CC0) importada como Humanoid por `ArenModelPostprocessor` (43
  clipes, avatar válido).

## Tests
Play mode, input simulado: ataque bufferizado com direção (1,0,0) ao segurar D;
Counter substituiu Attack; Attack logo depois NÃO substituiu o Counter; consumo esvazia.
Gamepad: bindings padrão, sem controle físico para testar (não testado).

# PHASE 2 — Core movement  (+ PHASE 3 forgiveness, PHASE 4 regressão de parkour)

Status: CONCLUÍDA (teste automatizado; sensação precisa do seu playtest — Checkpoint A)

## What changed
| Item | Antes | Depois |
|---|---|---|
| Pulo livre | **não existia** (Espaço só fazia ações contextuais) | `ArenJump`: 1.0 m segurando (medido 0.97 m), mínimo 0.46 m em toque rápido, ~0.75 s no ar parado; correndo: 0.90 m / 3.36 m de distância / 0.7 s |
| Apex assist | — | gravidade ×0.55 com \|v.y\| < 1.2 m/s |
| Coyote / buffer do pulo | — | 0.1 s / 0.15 s |
| Buffer de ações contextuais (vault, reach, agarrar borda) | 0.15 s só no agarrão | 0.4 s em vault/reach/agarrão (`InputCharacterController.contextualBufferWindow`) |
| Antecipação | não existia | se há obstáculo de parkour à frente em `0.5 + v·0.35` m, Espaço espera o vault/agarrão em vez de virar pulo (antes, apertar 2.4 m antes da caixa fazia o Aren pular EM CIMA dela) |
| Controle aéreo | velocidade no ar = input (4.5) **+ empurrão** `forward*walkSpeed` (≈6.9 m/s), sempre na direção do corpo | mantém o momento, esterça com 8 m/s², arrasto 0.5 m/s² sem input |
| Atrito da cápsula principal | sem material (atrito padrão 0.6): corrida real **4.21 m/s** (alvo 4.5); no pouso a velocidade horizontal zerava por 1 passo | material sem atrito do DPS: corrida **4.50 m/s**, pouso mantém 4.50 |
| Rotação do corpo | `rb.rotation` só atualizava a cada **~0.1 s** (interpolação sobrescrevia a transform) → giro em degraus de ~20° (~10 Hz) | contínua por frame (MoveRotation); 180° em ~0.35 s |
| Inversão correndo | velocidade invertia de +4.5 para −4.5 em 1 passo (corpo "andando de costas") | `ArenPivot`: derrapada 0.14 s (~0.5 m), reacelera a 90% em 0.28 s (0.45 s do input a 90%) |
| Pouso | 1 tipo (só por velocidade horizontal), detectado ~0.12 s atrasado no meio de transição | 3 níveis pela velocidade de impacto estimada: leve < 8 m/s (volta direto à locomoção/Run + afundamento procedural do quadril), médio 8–12.5 (clipes do DPS), pesado > 12.5 (NinjaJump_Land + trava 0.38 s); pouso interrompe a transição na hora |
| Queda sem ser pelo DPS (empurrão/beirada/teleporte) | ficava sem animação de queda nem pouso | detectada (`v.y < −1.5`), entra em "Aren Fall" |
| Inclinação do tronco | não existia | `ArenLean`: lateral ±12° (v·ω), frente até 7°, trás até 12° (derrapada), mola 0.12 s |
| Fast fall | não existia | segurar C no ar: +1.6 g (8 m em ~0.41 s contra 0.73 s) |
| Vault / Deep Jump / Slide no ar | disparavam só com a tecla segurada, sem checar chão | exigem `isGrounded` |

Animator (via `ArenAnimatorSetup`, idempotente): params `LandType`, `AirVelY`; estados
`Aren Jump` (NinjaJump_Start), `Aren Fall` (mesmo clipe do Fall Idle, **sem** tag Root),
`Aren Land Heavy` (NinjaJump_Land ×1.35). Transições do DPS alteradas: `Fall Idle → Fall`
e `Fall Idle → Fall A Land To Run Forward` passaram a exigir `LandType==1`; novas
`Fall Idle → Walk` (leve) e `→ Aren Land Heavy` (pesado).

## Files changed
- Novos: `Assets/Aren/Scripts/Movement/ArenJump.cs`, `ArenPivot.cs`, `ArenLean.cs`,
  `Assets/Aren/Scripts/Debug/ArenTestProbe.cs`, `Assets/Aren/Editor/ArenAnimatorSetup.cs`.
- DPS (mínimo, comentado no código): `MovementCharacterController.cs` (controle aéreo,
  remoção do empurrão no ar), `ThirdPersonController.cs` (rotação no Rigidbody),
  `InputCharacterController.cs` (buffer contextual), `ClimbController.cs` (usa buffer
  contextual), `VaultObstacle.cs`/`VaultOver.cs`/`VaultReach.cs` (buffer contextual,
  consome o press, chão), `VaultSlide.cs` (chão), `ParkourLabHUD.cs` (linhas novas),
  `Player.prefab` (componentes + material sem atrito na cápsula principal),
  `Animator Controller.controller`.

## Tests (Play mode, `ArenTestProbe` — input simulado dentro do loop do jogo)
- Pulo parado segurado / toque / correndo; queda de 8 m (pesado); inversão 180° correndo;
  curva correndo com screenshot (inclinação visível, nada quebrado).
- **Regressão do parkour:** vault da caixa com Espaço apertado 2.4 m antes ✔, vault da
  cerca com toque ✔, slide ✔, agarrar borda → pendurar → soltar (C) ✔, pulo previsto
  entre postes ✔.
- Console: sem erros/warnings do jogo (só timeouts do servidor da CLI enquanto o editor
  entra em Play — ruído da ferramenta de teste).
- FPS (editor, vsync 60): 60.1 com os componentes do Aren, 59.6 sem → custo não
  mensurável.

## Problems found / fixed
Os 4 bugs pré-existentes da tabela (rotação em degraus, atrito, empurrão no ar, pouso
atrasado) + roubo do vault pelo pulo livre (corrigido com antecipação).

## Remaining
- Sem clipe de pivot/skid e de pulo "de bardo": pivot é procedural (freio + inclinação),
  pulo usa NinjaJump (encolhe as pernas, estilizado). Clipes Mixamo opcionais depois.
- Não há agarrar borda a partir do pulo livre no ar (DPS exige chão/coyote) → backlog.
- Corner/head-bump correction: não apareceu problema no teste 3D → não implementado.

## Next phase
Fase 5 — Rig do Aren.
