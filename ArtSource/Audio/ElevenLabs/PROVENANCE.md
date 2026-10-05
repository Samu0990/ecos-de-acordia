# Sons gerados no ElevenLabs (Sound Effects v2)

Gerados em 2026-10-05 na conta do autor do jogo (elevenlabs.io → Sound Effects), 4 variações por
pedido, a pedido dele ("use o eleven labs que está aberto no Chrome"). `raw/` guarda só as variações
escolhidas (escolha pelo espectrograma: ataque, cauda, harmônicos); `prepare.py` corta o silêncio
inicial, ajusta a sonoridade (LUFS), limita o pico e grava os WAV usados pelo jogo:

- `Assets/Aren/Resources/Audio/Eleven/*.wav` — abertura (impacto distante, onda de choque, rajada,
  queda do Aren, levantar tossindo, meteoro, detritos, sino rachado, céu rasgando, braam do título,
  sucção da Fenda, os sete), laços (zumbido da Corrupção, vila em pânico ao longe, grilos) e a
  Corrupção na vila (gritos, multidão, transformação, desintegração, grito que vira coro desafinado,
  rosnados, fuga, janelas batendo). Carregados pelo `OpeningSound` com id `x_<nome>`.
- `Assets/Aren/Resources/Audio/Samples/el_*.wav` — combate (golpes, esquiva, contra-ataque, gemido
  do Aren, inimigo atingido/dissolvendo, pulso, golpe final, rosnados), camadas no `AudioRunner`.

Rodada 2 (2026-10-05, "faça sons de explosões cinematográficas"):
- `explosions.py` monta as explosões em CAMADAS (estalo → corpo de pressão → peso grave → detritos →
  espaço → rescaldo) sobre as gravações `reality_shatter`, `rock_burst`, `cine_explosion`,
  `distant_boom`, `sub_boom`, `presuck`: seno grave saturado (harmônicos audíveis em alto-falante de
  notebook), estalo de ruído agudo, resposta de impulso de vale (ecos das serras). Saídas:
  `reality_shatter`, `rock_burst`, `cine_explosion`, `distant_rumble`, `sub_boom`, `presuck`.
- `births.py`: os sete nascimentos com o sino gravado (`bell_near_4`) reafinado para as notas
  desafinadas de cada brilho (`birth0..6`, substituem os sintetizados).
- `prepare.py`: `choir_swell` (coro grave em crescendo, revelação da Fenda) e `sky_crack_run`
  (rachadura de gelo/vidro correndo pelo céu).

LICENÇA: no plano gratuito do ElevenLabs o uso comercial NÃO é permitido e pede atribuição
("elevenlabs.io", já nos créditos do jogo). Antes de vender o jogo: assinar um plano pago (que dá
licença comercial para o que foi gerado nele) ou trocar estes arquivos.
