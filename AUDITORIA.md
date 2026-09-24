# Auditoria — ParkourLab

Auditoria de todos os scripts, do Animator Controller, do prefab Player e da cena
Testing, feita lendo o código-fonte inteiro e testando ao vivo em Play mode (input
real via Input System, teleporte + reflection pra inspecionar estado interno,
screenshots antes/depois). Ordenada por gravidade.

## 1. [CRÍTICO — CORRIGIDO] IK da escalada corrompendo o corpo inteiro

**Arquivo:** `Scripts/System Controllers/ClimbController.cs`, método `IKSolver()`.

`IKSolver()` é chamado a cada frame a partir de `ClimbMovement()` (via `Update()`
normal, **fora** do callback `OnAnimatorIK`). Duas linhas dentro dele chamavam
`animator.SetIKPositionWeight(...)` diretamente. Chamar APIs de IK do Mecanim fora do
callback correto (`OnAnimatorIK`/`OnStateIK`) é proibido pela própria Unity e corrompe
o estado interno do solver de IK do personagem inteiro — não só as chamadas erradas
davam problema, o resto do sistema de IK (que RODA no lugar certo) parava de
funcionar direito também.

**Sintomas confirmados (reportados pelo usuário, reproduzidos e confirmados por
mim):**
- Mãos flutuando acima da borda no Freehang Idle.
- Corpo inteiro invisível no Free Hang — só as mãos apareciam flutuando na tela
  (pior do que o usuário percebeu; ele viu "mãos flutuando", eu vi "o personagem
  sumiu quase inteiro").
- Warning `Setting and getting Body Position/Rotation, IK Goals, Lookat and
  BoneLocalRotation should only be done in OnAnimatorIK or OnStateIK` repetindo
  todo frame enquanto pendurado.

**Fix:** removidas as duas chamadas de dentro de `IKSolver()`. O código dentro do
`onAnimatorIK` de verdade (rodando no contexto certo, chamado via
`VaultingController.OnAnimatorIK -> VaultClimbLedge.OnAnimatorIK`) já cobre o caso de
zerar o peso do pé quando não há parede — não foi preciso adicionar nada, só tirar o
que estava no lugar errado.

**Confirmado por:** contadores de chamada instrumentados temporariamente + leitura de
`GetIKPositionWeight` via reflection + screenshots em Braced Hang e Free Hang antes e
depois do fix.

**Resíduo:** o warning ainda aparece um punhado de vezes (não mais continuamente)
durante a transição inicial de agarrar a borda — meu palpite é `SetMatchTarget`/
`SetTarget` em `AnimationCharacterController.cs`, chamado de `ClimbUpdate()` (fora do
IK pass). Segundo a documentação da Unity, `MatchTarget` é OK de chamar fora de
`OnAnimatorIK` (ao contrário de `SetIKPosition*`), então isso é bem menos grave e não
mexi mais nisso por ora. Se sobrar tempo, vale investigar.

## 2. [CONFIRMADO — CORRIGIDO] Divisão por zero no primeiro Slide

**Arquivo:** `Scripts/Vaulting Actions/VaultSlide.cs`, método `CheckAction()`.

```csharp
dis = 4 / Vector3.Distance(startPos, targetPos);   // calculado ANTES
...
startPos = controller.transform.position;           // só atualizado DEPOIS
targetPos = hit2.point;
```

`dis` era calculado usando os valores de `startPos`/`targetPos` da execução
**anterior** — na primeiríssima vez que o jogador desliza, ambos são `Vector3.zero`
(valor padrão), então `Vector3.Distance(zero, zero) = 0` e `dis = 4/0 = Infinity`.
Isso alimentava `SetFloat("AnimSpeed", dis)` e o cálculo de `vaultTime` em `Update()`,
o que podia pular a animação inteira num único frame (o slide "não acontecia",
teleportando direto pro fim).

**Fix:** reordenado para calcular `dis` depois de `startPos`/`targetPos` serem
atualizados para a execução atual. Testado: slide roda suave, sem erros no console,
mesmo sendo a primeira execução da sessão.

## 3. [SUSPEITO — CORRIGIDO] `FindAheadPoints` não filtrava por direção

**Arquivo:** `Scripts/System Controllers/DetectionCharacterController.cs`.

```csharp
if (Vector3.Dot(item.transform.position, transform.position) > 0)
```

Isso compara duas **posições absolutas no mundo**, não uma direção relativa ao
personagem — não filtra "o que está à frente" de verdade (dá positivo quase sempre,
não importa pra onde o personagem esteja olhando). Só é usado em
`JumpPredictionController.CheckJump()`, que depois faz sua própria filtragem por
direção usando o input — então o impacto prático hoje é baixo (o filtro downstream
mascarava o bug), mas a Etapa C pede pra eu **estender exatamente esse sistema** pra
prever bordas, então vale a pena já estar correto.

**Fix:** trocado para `Vector3.Dot((item.pos - transform.position).normalized,
transform.forward) > 0` — agora compara a direção até o ponto com a direção que o
personagem está olhando, que é o que "à frente" deveria significar.

## 4. [BAIXA PRIORIDADE — NÃO CORRIGIDO] `IsGrounded(stepHeight)` ignora o parâmetro

**Arquivo:** `Scripts/System Controllers/DetectionCharacterController.cs:214`.

```csharp
public bool IsGrounded(float stepHeight) {
    ...
    return Physics.Raycast(transform.position + new Vector3(0, 0.3f, 0), Vector3.down, out hit, 0.7f);
}
```

O parâmetro `stepHeight` nunca é usado — os valores `0.3f`/`0.7f` são fixos.
`ThirdPersonController.cs` chama isso passando o `stepHeight` configurável do
Inspector (`public float stepHeight = 0.8f`), então hoje mudar esse valor no
Inspector **não tem efeito nenhum** na detecção de chão.

**Por que não corrigi:** essa checagem roda todo frame e é usada pra decidir se o
personagem está no chão — mexer errado aqui pode quebrar o jogo inteiro (queda
infinita ou "no chão" falso-positivo), e não tenho como testar exaustivamente todas
as superfícies do lab a tempo. Prefiro deixar documentado e funcionando do que
arriscar. Se quiser que eu corrija, funciona hoje (com o valor fixo 0.8 nos
bastidores) — só o campo do Inspector que é enganoso.

## 5. [MENOR — CORRIGIDO] `Destroy` em vez de `DestroyImmediate` em ferramenta de edição

**Arquivo:** `Scripts/Helpers/Climbing/HandlePoints.cs:109` (`DeletePrevious`).

Esse script (`[ExecuteInEditMode]`) é uma ferramenta de autoria usada só no Editor
(checkboxes no Inspector pra gerar pontos de escalada ao longo de uma borda), não
roda durante o jogo. `DeletePrevious` usava `Destroy()` em vez de `DestroyImmediate()`
— fora do Play mode isso gera warning "Destroy may not be called from edit mode".
O método irmão `DeteleAll()`, logo abaixo, já usa `DestroyImmediate` corretamente.
Ajustei pra consistência. Não afeta gameplay (só quem usa a ferramenta de nível).

## 6. [MENOR — NÃO CORRIGIDO] `CreatePoints` pode perder o ponto mais à direita

**Arquivo:** `Scripts/Helpers/Climbing/HandlePoints.cs:114-147`.

Se a distância entre `furthestLeft` e `furthestRight` for menor que `posInterval`
(padrão 0.5m), `pointCount` vira 0, o loop nunca roda, e `furthestRight` nunca é
adicionado em `pointsInOrder` (só é adicionado dentro do loop). Ferramenta de
autoria, edge case (borda muito curta), não observei isso acontecer nos objetos atuais
do lab. Não corrigi por ser baixo impacto e eu não ter certeza de qual seria o
comportamento "certo" nesse caso sem saber a intenção original do autor.

## 7. [ESCLARECIMENTO, NÃO BUG] HUD "Vault: sim" durante escalada — CORRIGIDO no HUD

Já coberto na sessão anterior: `isVaulting` é uma flag de exclusão mútua
compartilhada por todas as `VaultAction` (incluindo escalar). Correto por design.
Só ajustei o HUD próprio do projeto (`ParkourLabHUD.cs`) pra não mostrar "Vault: sim"
quando na verdade é escalada (já existe indicador "Escalada" dedicado).

## 8. ["Personagem trava ao pendurar"] NÃO CONFIRMADO COMO BUG

Testado com input real simulado em Play mode: agarrar, pendurar em Braced e Free
Hang, pular pra outra borda (W+Espaço), soltar (C) — tudo respondeu normalmente.
O que parecia travamento era o personagem parado esperando input, que é o
comportamento esperado. Shimmy (A/D) não moveu numa borda de 0.8m ("Small Ledge") —
provavelmente não há espaço físico pra shimmy numa borda tão curta, mas fica em
aberto: **preciso que você teste numa borda mais larga e confirme se anda ou não.**

## Sem problemas encontrados

`VaultObstacle.cs`, `VaultOver.cs`, `VaultDown.cs`, `VaultJumpPrediction.cs`,
`JumpPredictionController.cs`, `VaultReach.cs` (chamadas de IK corretamente dentro de
`OnAnimatorIK`), `DrawLine.cs`, `DrawLineIndividual.cs`, `HandlePointConnection.cs`,
`Point.cs`, `CameraController.cs` (um `Lerp` que reusa o valor já interpolado em vez
de um valor de origem fixo, produzindo uma curva ease-out em vez de linear — parece
intencional, vou revisitar na Etapa B.3 de qualquer forma). Confirmado também que
`rb.velocity` foi migrado por completo pra `rb.linearVelocity` no projeto inteiro
(nenhum resíduo da migração pro Unity 6 nesse ponto).

## Testes de Play mode realizados

- Escalada (Braced Hang e Free Hang): agarrar, pendurar, pular entre bordas, soltar —
  OK depois do fix. Shimmy inconclusivo (ver item 8).
- Vault sobre obstáculo baixo (correr + Espaço): OK, personagem passa por cima
  corretamente.
- Slide por baixo de obstáculo (correr + C): OK depois do fix do item 2.
- Console limpo (0 erros) em todos os testes acima.
- FPS: leitura pontual de ~30 durante teste pesado (várias screenshots/eval
  simultâneos) — não é uma medição confiável de FPS real de jogo; preciso medir de
  novo em condições normais (sem toda a instrumentação de debug rodando junto).
