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

# PHASE 5 — Rig do Aren (Blender headless)
Ver `ArtSource/Aren/scripts/` (aren_01..05). Esqueleto Humanoid (52 ossos mapeados, dedos de
3 falanges, twists), secundários (capuz, 4 abas do tabardo, corrente), FluteHolster/FluteSocket,
pesos por regra + transferência nos acessórios, stress test de 12 poses. Import Humanoid com
T-pose forçada (`ArenCharacterSetup`). Erika trocada pelo Aren no Player. Commit `9dc7fcb`.

# PEDIDO DO USUÁRIO (2026-10-01): "faça o mundo medieval, quero UI, as habilidades e o combo"
Escopo ampliado pelo próprio usuário (o orquestrador dizia "só o jogador"). Interpretação de
"doco" = combo (os 4 golpes da flauta); mira automática no alvo cobre também "foco".

## Combate (Phases 7–13 condensadas)
- `ArenCombat`: máquina de estados única — combo M1-1..4 (UAL2 CC0: Sword_Regular_A, B,
  2º golpe do Sword_Regular_Combo, giro do Sword_Regular_C), avanço magnético com velocidade
  limitada (nunca teleporta), esquiva com i-frames e ponte de combo, counter/parry, dano, morte.
- Tempos de contato medidos pela velocidade da mão direita a 60 Hz (AnimationMode):
  A 0.23 s · B 0.25 s · Combo 0.23/0.72/1.53 s · C 0.63 s · Melee_Hook 0.23 s · Zombie_Scratch 0.55 s.
  `animSpeed = (contato − offset) / startup` → o contato cai exatamente no fim do startup.
- Integração com o DPS sem reescrever: `allowMovement`/`stopMotion` desligados durante ações,
  `VaultingController.blockNewActions` (novo), Esc não fecha mais o jogo (`OnExitPressed`).
- `TargetResolver` (cone da intenção + histerese), `GameFeel` (dono único do timeScale: pausa,
  hitstop, câmera lenta; tremor por trauma numa extensão do Cinemachine).

## Habilidades (doc de remake §17)
Pulso de Ressonância (Q, 25), Lâmina de Frequência (E, 15), Eco Fantasma (R, 35, 10 s),
Contracanto (segurar ataque, 3 níveis 20/35/50). Recurso Ressonância (+5 por acerto × cadência,
+10 counter, +8 esquiva perfeita, +1.2/s).

## Testes automatizados (robô de input, cena Testing)
| teste | resultado |
|---|---|
| combo 4 golpes vs 3 Ecos | 4 contatos, alvo correto, finalizador 360° derrubou 2 |
| Pulso (Q) | 3 inimigos atordoados de uma vez |
| Lâmina (E) | acertou alvo a ~6 m |
| Eco Fantasma (R) | repetições com atraso (cadência +2 por golpe) |
| esquiva + counter automático no aviso | counter → knockdown |
| Contracanto segurando | nível 2 com super-armadura, matou o alvo |
| sem recurso | habilidade negada (som + anel vermelho), sem gastar |

## Mundo: Campanula, Vila dos Doze Sinos (Bíblia de Lore)
Kit modelado por script (`ArtSource/Campanula/scripts`), 17 texturas geradas (`Tools/texgen`),
montagem por `CampanulaBuilder` (terreno 320 m com riacho e morros, ruas calçadas, campos,
pôr do sol, neblina, céu procedural, a Fenda no céu, NavMesh). Bordas de escalada orientadas por
raycast. Medidas do DPS respeitadas: borda alcançável a 1.6–2.5 m acima dos pés, bordas de
escalada a cada 1.2–1.4 m (andaime da torre a cada 2.2 m + pedras salientes a cada 1.3 m).
Problemas: FBX com geometria Z-up + rotação na raiz (resolvido com contêiner yaw-only + meia
volta); splatmap perdido ao criar o asset depois de pintar (resolvido criando antes).

## UI e fluxo
Menu sobre a vila ao vivo, HUD, pausa, configurações persistentes, controles, créditos, morte,
fim com estatísticas. Roteiro: estrada → 13ª badalada → mercado → praça → torre → ponte →
Campo da Fenda (Cervo Corrompido: Tripo 176k → 11k tris, rig Humanoid por script, 3 ataques,
2ª fase) → fim.

## Limitações honestas
- Não existe animação de flauta: golpes são clipes de espada CC0 com a flauta na mão.
- O cervo foi riggado por script com pesos por distância: deformação aceitável para criatura
  corrompida, não é rig de artista (chifres podem esticar em poses extremas).
- Escalada e vault dependem das medidas do DPS; rotas foram desenhadas nessas medidas.

## Desempenho (executável, Dell Latitude 3490 / Intel UHD 620, 1920×1080, qualidade Média)
| trecho | antes | depois das otimizações |
|---|---|---|
| estrada | 46.8 (1% 33.9) | 56.0 (1% 49.8) |
| mercado com luta | 43.6 (1% 26.4) | 50.8 (1% 45.4) |
| praça com ondas | 50.7 (1% 35.7) | 52.3 (1% 44.1) |
| campo com o chefe | 52.6 (1% 33.9) | 53.3 (1% 41.9) |
| menu (vista aérea) | 31.5 (1% 13.4) | 41.4 (1% 31.5) |
Mudanças: escala de renderização 3D (80% na Média, UI nativa), sem VSync + limite 60, camadas de
distância (props 70 m, árvores 180 m), occlusion culling, sombras 30 m, terreno sem normal map,
luz de flash só no Alto. Medido com `./EcosDeAcordia.x86_64 -eda-benchmark` (editor fechado).

## Testes no executável (`-eda-test`, teclado/mouse virtuais, velocidade real)
| teste | resultado |
|---|---|
| muro baixo (vault) | passa (antecipação 0.5 s + buffer 0.6 s) |
| fardos (deep jump) | passa |
| viga (slide) | passa |
| muralha: 4 pedras + remate → topo | passa (desce para dentro da vila) |
| torre: 14 pedras + parapeito → sineira | passa (termina a 19 m dentro da sineira) |
| encontros mercado / praça / campo | 2 / 4 / chefe + 2 Ecos nascem |
Problemas resolvidos no caminho: grafo de salto do DPS não existia nas bordas novas
(HandlePointConnection agora roda no construtor); frente das bordas invertida (DPS espera para
dentro da parede); pedras de 30 cm faziam o DPS alternar pés-na-parede/pendurado; andaime com
plataformas a 2.2 m não é escalável pelo DPS (virou decoração); `runInBackground` desligado
congelava o Play do editor fora de foco.

## Sombras (decisão final)
Sombras em tempo real no built-in custam ~10 FPS fixos no Intel UHD (passe extra de profundidade
da cena inteira — encurtar a distância não muda nada). Testei sombras assadas (lightmap CPU,
Subtractive, 1.0 e 2.5 texels/m): paredes manchadas/escuras, pior que a luz em tempo real.
Final: Baixa/Média sem sombra em tempo real + sombra blob nos personagens; Alta liga as sombras
completas (também há a opção "Sombras em tempo real" nas configurações).
Benchmark final (notebook já quente, Média 80%): estrada 44, mercado 43, praça 44, chefe 47 FPS
(1% baixo 35–39). Alta 100% com sombras: ~23 FPS (não recomendado neste notebook).
