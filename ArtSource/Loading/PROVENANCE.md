# Tela de carregamento

`Assets/Aren/Resources/UI/Loading/loading_fenda.jpg` — recorte do painel principal do storyboard
do autor ("Ecos de Acordia: A Fenda dos Sete Brilhos", `storyboard_fenda_sete_brilhos.png`, o mesmo
usado como referência da abertura v2), sem os textos, ampliado 2x (Lanczos + nitidez leve):

    convert storyboard_fenda_sete_brilhos.png -crop 1034x392+638+2 +repage -crop 1014x392+20+0 +repage \
            -filter Lanczos -resize 200% -unsharp 0x1.2+0.6+0.02 -quality 92 loading_fenda.jpg

Arte fornecida pelo dono do projeto.
