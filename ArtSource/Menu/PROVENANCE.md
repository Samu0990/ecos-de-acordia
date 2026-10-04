# Arte da tela inicial

- Origem: imagem de referência da tela inicial (1672x941, WebP) enviada diretamente pelo autor do
  jogo em 2026-10-04, com o pedido de que a tela inicial fosse "exatamente assim".
- SHA-256 de `title_reference.webp`: `3243373a96714eb0f36da69ac8633f6bb273f115366e7d80a7f0708b42d3a52a`.
- Uso no jogo: a própria imagem é o fundo da tela inicial. `scripts/build_title.py` recorta dela os
  botões (placa apagada e placa vermelha selecionada), os textos JOGAR / CONFIGURAÇÕES / SAIR e a
  máscara do título, e reconstrói o painel atrás dos botões com a textura do próprio painel. Os
  detalhes animados (halos das velas, brasas, poeira, névoa, joias, reflexo do título) são gerados
  por código/script para a demo.
- Licença: os direitos da imagem não foram informados (aparenta ser arte gerada por IA). Permitido
  no protótipo local por pedido do autor; confirmar os direitos antes de distribuição comercial.
