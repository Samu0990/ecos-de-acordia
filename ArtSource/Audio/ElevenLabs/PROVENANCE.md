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

LICENÇA: no plano gratuito do ElevenLabs o uso comercial NÃO é permitido e pede atribuição
("elevenlabs.io", já nos créditos do jogo). Antes de vender o jogo: assinar um plano pago (que dá
licença comercial para o que foi gerado nele) ou trocar estes arquivos.
