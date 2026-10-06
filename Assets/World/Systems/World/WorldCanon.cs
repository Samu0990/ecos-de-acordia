using System.Collections.Generic;
using UnityEngine;

namespace Elyndra.World
{
    // =====================================================================================================
    //  Cânone do mundo de ELYNDRA (Bíblia Definitiva v2.0 — Ecos do Contracanto). Fonte única dos nomes,
    //  posições no continente, rotas, Notas, Relíquias, Vórtices, masmorras e chefes. O construtor de cenas
    //  (Assets/World/Editor/ElyndraWorldBuilder.cs), o mapa do mundo e o WorldState leem daqui.
    //
    //  Coordenadas do continente em QUILÔMETROS: x = leste, z = norte. Campânula fica em Valtéria (30, 20);
    //  a Fenda do Contracanto fica a ~63 km, a nor-nordeste (azimute ~17°, o mesmo céu da abertura).
    //
    //  Regra da Corrupção usada no mundo (pedido do autor, 2026-10-05): só SERES VIVOS são hospedeiros
    //  (pessoas, animais, plantas). Objetos e construções mudam por causa da Ressonância de um hospedeiro
    //  vivo por perto (ex.: o sino que repete vozes é efeito do sineiro corrompido, não o hospedeiro).
    // =====================================================================================================

    public enum RegionId
    {
        Valteria, Velaria, Miralume, Orvalume, Helion, Sefra, Nereth,
        Granith, CoroaDeCinza, MarDeVidro, Caliria, Sombrafonte, FronteiraMuda
    }

    public enum Distortion { Nenhuma, Loop, Inversao, Saturacao, Roubo, Estouro, Ausencia, Fenda }

    public enum RouteKind { Estrada, PassoDeMontanha, Trilha, Rio, Mar, Caverna, PonteSuspensa }

    /// <summary>Uma das Sete Notas Corrompidas (chefe de escala regional).</summary>
    public class NoteDef
    {
        public string id, name, epithet, fundamento, rule, contramotivo, arena;
        public RegionId region;
    }

    /// <summary>Relíquia de Vael (faceta do Acorde-Matriz) — não é uma "chave": tem Custódio, preço e política.</summary>
    public class RelicDef
    {
        public string id, name, faceta, capacidade, preco, custodio, site;
        public RegionId region;
    }

    /// <summary>Vórtice: lugar cujo corpo verdadeiro é uma regra (Motivo → Regra → Núcleo → Contramotivo).</summary>
    public class VortexDef
    {
        public string id, name, motivo, regra, nucleo, contramotivo;
        public Distortion distortion;
        public RegionId region;
    }

    /// <summary>Masmorra: entrada → exploração → combate → mecânica → atalho → miniboss → final.</summary>
    public class DungeonDef
    {
        public string id, name, theme, mechanic, miniboss, reward;
        public RegionId region;
        public DungeonLayout layout;
    }

    public enum DungeonLayout { Descida, Torre, Anel, Galhos, Labirinto, Eixo }

    public class RouteDef
    {
        public RegionId a, b;
        public string name;
        public RouteKind kind;
        public float km;
        /// <summary>Condição do WorldState para abrir (vazio = aberta). Ex.: "nota:do", "flag:barco".</summary>
        public string requires;
        public bool secret;
    }

    public class RegionDef
    {
        public RegionId id;
        public string scene, name, epithet, identity, tradition, conflict;
        public Vector2 km;            // centro no continente
        public float radiusKm;        // tamanho aproximado do reino
        public string noteId;         // null = reino sem Nota
        public string relicId;        // null = sem Relíquia
        public string[] minibosses;
        public string[] vortices;
        public string dungeonId;
        public string[] enemies;      // bestiário típico (ids do EnemyCatalog), do mais fraco ao mais forte
        public int dangerTier;        // 1 (início) .. 5 (fim de jogo)
        public string mainSettlement;
        public string[] settlements;
        public string[] landmarks;
        public string[] factions;
    }

    public static class WorldCanon
    {
        /// <summary>Onde fica a Fenda do Contracanto no continente (km). Sempre MUITO longe de Campânula.</summary>
        public static readonly Vector2 FendaKm = new Vector2(49f, 82f);
        /// <summary>Campânula dentro de Valtéria (km).</summary>
        public static readonly Vector2 CampanulaKm = new Vector2(30f, 20f);
        public const string CampanulaScene = "Campanula";
        public const string WorldMapScene = "WorldMap";

        public static readonly NoteDef[] Notes =
        {
            new NoteDef { id = "do", name = "Dó Partido", epithet = "O Peso que Canta", fundamento = "Estabilidade, matéria, compromisso", region = RegionId.Valteria,
                rule = "Estabilidade vira prisão: o peso aumenta, portas recusam mudar de estado, promessas viram correntes.",
                contramotivo = "Provar que estabilidade sem escolha é cárcere: mover o que ele prende.", arena = "Cratera do Primeiro Peso" },
            new NoteDef { id = "re", name = "Ré Reverso", epithet = "A Marcha ao Contrário", fundamento = "Movimento, sequência, retorno", region = RegionId.Velaria,
                rule = "Caminhos repetem a origem; a consequência chega antes da causa.",
                contramotivo = "Quebrar a marcha que toda a região segue usando contratempo consciente.", arena = "Cortejo Inverso" },
            new NoteDef { id = "mi", name = "Mi Sem-Rosto", epithet = "O Roubo das Vozes", fundamento = "Identidade, memória, reconhecimento", region = RegionId.Miralume,
                rule = "Rostos, vozes e histórias são retirados dos donos; documentos continuam certos, pessoas perdem sentido.",
                contramotivo = "Escolher o que lembrar sem transformar memória em propriedade.", arena = "Arquivo que Esquece" },
            new NoteDef { id = "fa", name = "Fá Selvagem", epithet = "A Raiz que Grita", fundamento = "Vida, crescimento, adaptação", region = RegionId.Orvalume,
                rule = "A vida cresce sem aceitar limite nem morte.",
                contramotivo = "Limite, poda e aceitação do ciclo — não destruir a vida.", arena = "Jardim sem Inverno" },
            new NoteDef { id = "sol", name = "Sol Oco", epithet = "O Meio-Dia sem Luz", fundamento = "Luz, coragem, revelação", region = RegionId.Helion,
                rule = "Aparência e atenção viram energia: o que é observado fica mais forte.",
                contramotivo = "Lutar sem depender de reconhecimento.", arena = "Teatro do Aplauso" },
            new NoteDef { id = "la", name = "Lá Faminto", epithet = "O Sonho que Devora", fundamento = "Desejo, sonho, emoção", region = RegionId.Sefra,
                rule = "Todo desejo cresce até nada ser suficiente.",
                contramotivo = "Desejar algo que preserve a liberdade de outra pessoa.", arena = "Banquete da Última Vontade" },
            new NoteDef { id = "si", name = "Si Infinito", epithet = "A Nota que Não Termina", fundamento = "Limite, encerramento, passagem", region = RegionId.Nereth,
                rule = "Nada consegue terminar: feridos não morrem nem curam, despedidas não acabam.",
                contramotivo = "Concluir algo de modo irreversível.", arena = "Escadaria Depois do Fim" },
        };

        public static readonly RelicDef[] Relics =
        {
            new RelicDef { id = "calice", name = "Cálice de Sutura", faceta = "Vida e estabilização", region = RegionId.Caliria, custodio = "Lumen Aris (curadora itinerante)",
                capacidade = "Amplifica Cura, interrompe hemorragias e repara fissuras de ressonância.", preco = "Curar demais acelera crescimento errado e pode preservar Corrupção.", site = "Clínica-Templo do Sal" },
            new RelicDef { id = "braseiro", name = "Braseiro da Brasa-Mãe", faceta = "Calor e energia", region = RegionId.CoroaDeCinza, custodio = "Bront da Forja Baixa",
                capacidade = "Produz Brasa pura, armazena calor e alimenta encantamentos térmicos.", preco = "Desidrata o portador; amplifica raiva e impulsividade.", site = "Altar da Forja Baixa" },
            new RelicDef { id = "ampulheta", name = "Ampulheta das Marés", faceta = "Água e fluxo", region = RegionId.MarDeVidro, custodio = "Tessara Venn (cronista e navegadora)",
                capacidade = "Controla pressão, névoa, resfriamento e adaptação de campo.", preco = "Desorganiza o senso de ritmo interno.", site = "Farol Submerso" },
            new RelicDef { id = "no", name = "Nó de Basalto", faceta = "Massa e vínculo", region = RegionId.Granith, custodio = "Casa de Granith (conselho)",
                capacidade = "Aumenta peso, ergue barreiras, ancora corpos e sela passagens.", preco = "Rigidez física e emocional cresce com o abuso.", site = "Salão do Conselho de Pedra" },
            new RelicDef { id = "agulha", name = "Agulha do Vendaval", faceta = "Ar e trajetória", region = RegionId.Velaria, custodio = "Koru (guerreiro cego)",
                capacidade = "Manipula pressão, impulso, velocidade e mudança de direção.", preco = "Vertigem, perda de equilíbrio e microfissuras ao redor.", site = "Mirante dos Ventos Cruzados" },
            new RelicDef { id = "prisma", name = "Prisma do Encanto", faceta = "Memória e persistência", region = RegionId.Miralume, custodio = "Mair Selen (arquivista)",
                capacidade = "Grava notas, mensagens, ilusões, marcas e condições em objetos.", preco = "Gravações fortes carregam emoções e memórias indesejadas.", site = "Câmara Fosca do Arquivo" },
            new RelicDef { id = "sino", name = "Sino Mudo", faceta = "Silêncio e limite", region = RegionId.Nereth, custodio = "Sibil (monge sem voz)",
                capacidade = "Interrompe ressonâncias, oculta assinaturas e abre brechas contra Vórtices.", preco = "Silêncio prolongado apaga sensação, memória ou vínculo.", site = "Claustro do Último Toque" },
        };

        public static readonly VortexDef[] Vortices =
        {
            new VortexDef { id = "sino_amanhas", name = "Sino que Lembra Amanhãs", region = RegionId.Valteria, distortion = Distortion.Inversao,
                motivo = "Os moradores da Ermida esperavam o toque do sino para tudo (trabalho, reza, refeição).",
                regra = "Os moradores reagem a frases e ataques que ainda não aconteceram.",
                nucleo = "O sineiro (Regente Desfeito de Campânula) — vivo, corrompido, rege os outros pelo toque.",
                contramotivo = "Improvisar uma resposta que o padrão não previu (ritmo fora do compasso)." },
            new VortexDef { id = "ponte_margens", name = "Ponte de Duas Margens", region = RegionId.Velaria, distortion = Distortion.Fenda,
                motivo = "Caravaneiros que juraram chegar ao mesmo tempo nas duas pontas.", regra = "As extremidades ocupam posições incompatíveis.",
                nucleo = "A Mestra de Caravana que não decide de que lado está.", contramotivo = "Sincronizar duas versões do mesmo evento." },
            new VortexDef { id = "mesmo_corredor", name = "Casa do Mesmo Corredor", region = RegionId.Miralume, distortion = Distortion.Loop,
                motivo = "Copistas que recitavam o mesmo registro sem parar.", regra = "Toda porta retorna ao salão central.",
                nucleo = "O Bibliotecário Sem Nome.", contramotivo = "Variar o ritmo e usar silêncio para o prédio deixar de ouvir o passo." },
            new VortexDef { id = "bosque_escuta", name = "Bosque que Escuta Demais", region = RegionId.Orvalume, distortion = Distortion.Saturacao,
                motivo = "Árvores cultivadas por canto que nunca pararam de ouvir.", regra = "Plantas crescem para cada som do jogador.",
                nucleo = "O Cervo-Raiz.", contramotivo = "Alternar silêncio e rajadas deliberadas para enganar o crescimento." },
            new VortexDef { id = "teatro_aplauso", name = "Teatro do Aplauso", region = RegionId.Helion, distortion = Distortion.Saturacao,
                motivo = "Uma plateia que não consegue parar de aplaudir.", regra = "O que é observado fica mais forte.",
                nucleo = "O Ídolo de Vidro (cantor vivo coberto de vidro).", contramotivo = "Agir onde ninguém está olhando." },
            new VortexDef { id = "mercado_desejo", name = "Mercado Sem Desejo", region = RegionId.Sefra, distortion = Distortion.Roubo,
                motivo = "Mercadores que venderam a própria vontade.", regra = "Cada compra remove uma vontade do comprador.",
                nucleo = "O Colecionador de Promessas.", contramotivo = "Recusar a transação e devolver algo sem receber nada." },
            new VortexDef { id = "escadaria_fim", name = "Escadaria Depois do Fim", region = RegionId.Nereth, distortion = Distortion.Loop,
                motivo = "Enlutados que não conseguem terminar a despedida.", regra = "Sempre existe mais um degrau.",
                nucleo = "O Monge que Não Termina.", contramotivo = "Concluir algo de modo irreversível." },
            new VortexDef { id = "coro_sem_cantores", name = "Coro sem Cantores", region = RegionId.Granith, distortion = Distortion.Ausencia,
                motivo = "Cantores de muralha que perderam a voz e continuaram tentando.", regra = "A fortaleza chama e obriga visitantes a responder.",
                nucleo = "O Mestre de Muralha emudecido.", contramotivo = "Usar timbre instrumental sem assinatura vocal para quebrar a chamada." },
            new VortexDef { id = "forja_ultimo", name = "Forja do Último Golpe", region = RegionId.CoroaDeCinza, distortion = Distortion.Estouro,
                motivo = "Ferreiros que não conseguem parar de bater.", regra = "Todo impacto fica armazenado e volta no final.",
                nucleo = "O Ferreiro de Estouro.", contramotivo = "Vencer sem acumular uma sequência previsível de força." },
            new VortexDef { id = "farol_submerso", name = "Farol Submerso", region = RegionId.MarDeVidro, distortion = Distortion.Fenda,
                motivo = "Faroleiros que guiavam navios que já tinham afundado.", regra = "Reflexos trocam de lugar com quem os olha.",
                nucleo = "O Náufrago Prismático.", contramotivo = "Seguir o reflexo em vez do corpo." },
            new VortexDef { id = "carne_perfeita", name = "Coro da Carne Perfeita", region = RegionId.Caliria, distortion = Distortion.Saturacao,
                motivo = "Curadores que não aceitam cicatriz nenhuma.", regra = "Toda ferida é 'corrigida' para uma forma ideal.",
                nucleo = "A Primeira Curadora do Coro.", contramotivo = "Aceitar a emenda — consertar mostrando onde quebrou (lição de Odo)." },
            new VortexDef { id = "mil_respostas", name = "Mina de Mil Respostas", region = RegionId.Sombrafonte, distortion = Distortion.Loop,
                motivo = "Mineradores que perguntaram demais às cavernas.", regra = "Cada som volta mil vezes e as respostas viram passagens.",
                nucleo = "O Oráculo Repetido.", contramotivo = "Fazer uma pergunta que ninguém nunca fez." },
            new VortexDef { id = "ausencia", name = "Vórtices de Ausência", region = RegionId.FronteiraMuda, distortion = Distortion.Ausencia,
                motivo = "Sobreviventes que pararam de responder ao mundo.", regra = "Som, cor e profundidade deixam de existir.",
                nucleo = "As Vozes de Vharos.", contramotivo = "Responder com timbre, não com voz." },
        };

        public static readonly DungeonDef[] Dungeons =
        {
            new DungeonDef { id = "d_valteria", name = "Galerias do Aqueduto", region = RegionId.Valteria, layout = DungeonLayout.Descida, theme = "pedra molhada, canais e comportas de bronze",
                mechanic = "Comportas que só abrem no ritmo da água (abrir/fechar para mudar o nível).", miniboss = "Lavadeira de Loop", reward = "Lâmina de Segunda Batida (artefato menor)" },
            new DungeonDef { id = "d_velaria", name = "Estação das Caravanas Paradas", region = RegionId.Velaria, layout = DungeonLayout.Eixo, theme = "plataformas móveis, trilhos e pedágios rítmicos",
                mechanic = "Pontes que respondem a cadências (andar no contratempo).", miniboss = "Cavaleiro do Passo Repetido", reward = "Mapa de Contratempo" },
            new DungeonDef { id = "d_miralume", name = "Arquivo que Esquece", region = RegionId.Miralume, layout = DungeonLayout.Torre, theme = "torres de vidro fosco e estantes de vozes",
                mechanic = "Registros que se apagam quando observados; ler de costas.", miniboss = "Bibliotecário Sem Nome", reward = "Lanterna de Voz Guardada" },
            new DungeonDef { id = "d_orvalume", name = "Raiz-Mãe", region = RegionId.Orvalume, layout = DungeonLayout.Galhos, theme = "dentro de uma árvore gigante oca",
                mechanic = "Raízes que crescem em direção ao som (silêncio abre caminho).", miniboss = "Cervo-Raiz", reward = "Frasco de Névoa Cantada" },
            new DungeonDef { id = "d_helion", name = "Bastidores do Teatro", region = RegionId.Helion, layout = DungeonLayout.Labirinto, theme = "espelhos, refletores e cordas de palco",
                mechanic = "Feixes de luz que fortalecem o que tocam.", miniboss = "Ídolo de Vidro", reward = "Vela de Luz Reversa" },
            new DungeonDef { id = "d_sefra", name = "Galeria dos Sonhos Gravados", region = RegionId.Sefra, layout = DungeonLayout.Anel, theme = "vitrines de sonhos, cortinas e lanternas",
                mechanic = "Salas que oferecem atalhos em troca de um desejo.", miniboss = "Colecionador de Promessas", reward = "Moeda do Juramento" },
            new DungeonDef { id = "d_nereth", name = "Ossuário do Último Sino", region = RegionId.Nereth, layout = DungeonLayout.Descida, theme = "ossários, velas e escadas sem fim",
                mechanic = "Portas que nunca terminam de abrir até algo ser concluído.", miniboss = "Monge que Não Termina", reward = "Anel de Último Eco" },
            new DungeonDef { id = "d_granith", name = "Pedreira Afinada", region = RegionId.Granith, layout = DungeonLayout.Descida, theme = "blocos de basalto afinados e guindastes",
                mechanic = "Blocos que ficam mais pesados a cada nota repetida.", miniboss = "Colosso de Pedra Oca (gigante vivo)", reward = "Martelo de Pedra-Canto" },
            new DungeonDef { id = "d_coroa", name = "Forja do Último Golpe", region = RegionId.CoroaDeCinza, layout = DungeonLayout.Eixo, theme = "fornalhas, correntes e metal vivo",
                mechanic = "Cada golpe é guardado e devolvido no fim da sala.", miniboss = "Ferreiro de Estouro", reward = "Fragmento de Metal Vivo" },
            new DungeonDef { id = "d_mar", name = "Farol Submerso", region = RegionId.MarDeVidro, layout = DungeonLayout.Torre, theme = "vidro do mar, corais cristalizados, água parada",
                mechanic = "O mar cristaliza sob certas notas: caminhos temporários.", miniboss = "Náufrago Prismático", reward = "Rota cantada (navegação)" },
            new DungeonDef { id = "d_caliria", name = "Clínica do Sal", region = RegionId.Caliria, layout = DungeonLayout.Anel, theme = "enfermarias brancas, jardins de sal, vapor",
                mechanic = "Curar ou não curar: cada sala muda conforme a escolha.", miniboss = "Coro da Carne Perfeita", reward = "Fio de Sutura Azul" },
            new DungeonDef { id = "d_sombrafonte", name = "Mina de Mil Respostas", region = RegionId.Sombrafonte, layout = DungeonLayout.Labirinto, theme = "cristais de eco, trilhos de mina, escuro",
                mechanic = "Sons ecoam e abrem passagens onde voltam.", miniboss = "Oráculo Repetido", reward = "Máscara do Coral Ausente" },
            new DungeonDef { id = "d_fronteira", name = "Cicatriz do Vazio", region = RegionId.FronteiraMuda, layout = DungeonLayout.Eixo, theme = "terra sem som, cinza, rasgos no céu",
                mechanic = "Salas onde música, efeitos e avisos sonoros somem um a um.", miniboss = "Voz de Vharos", reward = "Caminho até a Fenda" },
        };

        public static readonly RegionDef[] Regions =
        {
            new RegionDef { id = RegionId.Valteria, scene = "Valteria", name = "Valtéria", epithet = "Vales, oficinas, pontes e cidades corais",
                identity = "Vales férteis, aquedutos, moinhos e oficinas; Campânula canta o dia inteiro.", tradition = "Cantos de trabalho, reparo, vento e agricultura.",
                conflict = "Berço da campanha; debate sobre instrumentos proibidos.", km = new Vector2(30, 20), radiusKm = 9, noteId = "do",
                minibosses = new[] { "Regente Desfeito de Campânula", "Cervo de Contratempo (alfa)" }, vortices = new[] { "sino_amanhas" }, dungeonId = "d_valteria",
                enemies = new[] { "sussurrante", "cervo_contratempo", "lobo_desafinado", "corvo_repetidor", "lobo_refrao", "regente_desfeito" }, dangerTier = 1,
                mainSettlement = "Campânula", settlements = new[] { "Vila do Moinho", "Ermida do Sino" },
                landmarks = new[] { "Grande Aqueduto", "Cratera do Primeiro Peso", "Ermida do Sino" }, factions = new[] { "Oficinas Clandestinas", "Afinadores de Campo" } },
            new RegionDef { id = RegionId.Velaria, scene = "Velaria", name = "Velária", epithet = "Estradas suspensas, caravanas e cidades móveis",
                identity = "Distância medida em ritmo; pontes que respondem a cadências.", tradition = "Cadências de marcha, navegação terrestre e coordenação.",
                conflict = "Rotas mudam conforme o ritmo, não só o espaço.", km = new Vector2(48, 26), radiusKm = 10, noteId = "re", relicId = "agulha",
                minibosses = new[] { "Cavaleiro do Passo Repetido" }, vortices = new[] { "ponte_margens" }, dungeonId = "d_velaria",
                enemies = new[] { "passante_invertido", "lobo_contratempo", "javali_impacto", "corvo_fenda" }, dangerTier = 2,
                mainSettlement = "Cidade-Caravana de Passo Largo", settlements = new[] { "Pedágio do Compasso" },
                landmarks = new[] { "Estradas Suspensas", "Mirante dos Ventos Cruzados" }, factions = new[] { "Filhos da Partilha" } },
            new RegionDef { id = RegionId.Miralume, scene = "Miralume", name = "Miralume", epithet = "Torres de vidro, arquivos de voz e genealogias cantadas",
                identity = "Contratos, testemunhos e canções familiares guardados em torres de vidro fosco.", tradition = "Memória, assinatura, registro civil e juramentos.",
                conflict = "Quem tem o direito de restaurar lembranças?", km = new Vector2(26, 36), radiusKm = 8, noteId = "mi", relicId = "prisma",
                minibosses = new[] { "Bibliotecário Sem Nome" }, vortices = new[] { "mesmo_corredor" }, dungeonId = "d_miralume",
                enemies = new[] { "morador_sem_palavra", "sussurrante_loop", "coruja_velada", "copista_fenda" }, dangerTier = 2,
                mainSettlement = "Miralume (cidade dos arquivos)", settlements = new[] { "Vila dos Copistas" },
                landmarks = new[] { "Torres de Vidro Fosco", "Câmara Fosca do Arquivo" }, factions = new[] { "Casas de Juramento", "Conservatório das Grandes Vozes" } },
            new RegionDef { id = RegionId.Orvalume, scene = "Orvalume", name = "Orvalume", epithet = "Florestas gigantes e aldeias móveis",
                identity = "Canto como conversa com o ecossistema; trilhas vivas.", tradition = "Cura, Raiz, Maré e harmonias com ecossistemas.",
                conflict = "Nem toda mutação é hostil; algumas comunidades dependem dela.", km = new Vector2(12, 14), radiusKm = 11, noteId = "fa",
                minibosses = new[] { "Cervo-Raiz" }, vortices = new[] { "bosque_escuta" }, dungeonId = "d_orvalume",
                enemies = new[] { "raiz_cantante", "cervo_erguido", "lobo_saturado", "abutre_saturado" }, dangerTier = 2,
                mainSettlement = "Aldeia Suspensa de Orvalume", settlements = new[] { "Clareira dos Cultivadores" },
                landmarks = new[] { "Árvores-Catedral", "Jardim sem Inverno" }, factions = new[] { "Afinadores de Campo" } },
            new RegionDef { id = RegionId.Helion, scene = "Helion", name = "Helion", epithet = "Cidades solares, palcos públicos e política de reputação",
                identity = "Sacadas, praças e palcos desenhados para amplificar coros públicos.", tradition = "Luz, coragem, propaganda, revelação e espetáculo.",
                conflict = "A resistência precisa de símbolos sem alimentar culto à imagem.", km = new Vector2(58, 42), radiusKm = 9, noteId = "sol",
                minibosses = new[] { "Ídolo de Vidro" }, vortices = new[] { "teatro_aplauso" }, dungeonId = "d_helion",
                enemies = new[] { "corista_suspenso", "sussurrante", "afinador_profano", "coruja_ausencia" }, dangerTier = 3,
                mainSettlement = "Helion, a Cidade do Meio-Dia", settlements = new[] { "Bairro das Sacadas" },
                landmarks = new[] { "Torres Solares", "Teatro do Aplauso" }, factions = new[] { "Conservatório das Grandes Vozes" } },
            new RegionDef { id = RegionId.Sefra, scene = "Sefra", name = "Sefra", epithet = "Metrópoles noturnas, sonhos gravados e mercados emocionais",
                identity = "Experiências sonoras, memórias e sonhos à venda.", tradition = "Encanto, desejo, memória de sonho e contratos.",
                conflict = "Desejos podem ser comprados, herdados ou explorados.", km = new Vector2(64, 22), radiusKm = 9, noteId = "la",
                minibosses = new[] { "Colecionador de Promessas" }, vortices = new[] { "mercado_desejo" }, dungeonId = "d_sefra",
                enemies = new[] { "peregrino_estouro", "partido_em_dois", "confessor_sem_eco", "abutre_estouro" }, dangerTier = 3,
                mainSettlement = "Sefra, a Cidade que Não Dorme", settlements = new[] { "Porto das Lanternas" },
                landmarks = new[] { "Mercado Noturno", "Banquete da Última Vontade" }, factions = new[] { "Casas de Juramento", "Coro da Única Voz" } },
            new RegionDef { id = RegionId.Nereth, scene = "Nereth", name = "Nereth", epithet = "Penhascos, mosteiros, cemitérios e ritos de despedida",
                identity = "Cultura construída em torno do encerramento: sinos, pausas, funerais.", tradition = "Silêncio, limite, passagem e memória dos mortos.",
                conflict = "O reino precisa reaprender a permitir finais.", km = new Vector2(64, 62), radiusKm = 8, noteId = "si", relicId = "sino",
                minibosses = new[] { "Monge que Não Termina" }, vortices = new[] { "escadaria_fim" }, dungeonId = "d_nereth",
                enemies = new[] { "sussurrante_oco", "cervo_oco", "coruja_ausencia", "voz_vharos" }, dangerTier = 4,
                mainSettlement = "Mosteiro do Último Toque", settlements = new[] { "Vila dos Enlutados" },
                landmarks = new[] { "Penhascos das Despedidas", "Escadaria Depois do Fim" }, factions = new[] { "Filhos da Partilha" } },
            new RegionDef { id = RegionId.Granith, scene = "Granith", name = "Granith", epithet = "Fortalezas montanhosas e arquitetura cantada",
                identity = "Cidades em montanhas afinadas para suportar peso.", tradition = "Rocha, Âncora, Juramento e defesa.",
                conflict = "Política rígida e disputa sucessória pelo Nó de Basalto.", km = new Vector2(12, 40), radiusKm = 10, relicId = "no",
                minibosses = new[] { "Colosso de Pedra Oca (gigante vivo)", "Mestre de Muralha emudecido" }, vortices = new[] { "coro_sem_cantores" }, dungeonId = "d_granith",
                enemies = new[] { "lobo_carga", "javali_impacto", "cantor_corrente", "regente_desfeito" }, dangerTier = 3,
                mainSettlement = "Cidadela de Granith", settlements = new[] { "Pedreira Alta" },
                landmarks = new[] { "Muralha Cantada", "Salão do Conselho de Pedra" }, factions = new[] { "Casas de Juramento" } },
            new RegionDef { id = RegionId.CoroaDeCinza, scene = "CoroaDeCinza", name = "Coroa de Cinza", epithet = "Terras vulcânicas, forjas e cidades em crateras",
                identity = "Ferreiros trabalham metal ressonante com Brasa e percussão.", tradition = "Brasa, metal ressonante e percussão industrial.",
                conflict = "Armas melhores abrem microfendas na Pauta do Céu.", km = new Vector2(36, 56), radiusKm = 9, relicId = "braseiro",
                minibosses = new[] { "Ferreiro de Estouro" }, vortices = new[] { "forja_ultimo" }, dungeonId = "d_coroa",
                enemies = new[] { "portador_estouro", "javali_impacto", "abutre_estouro", "lobo_carga" }, dangerTier = 4,
                mainSettlement = "Cidade-Cratera de Forja Baixa", settlements = new[] { "Acampamento dos Carvoeiros" },
                landmarks = new[] { "Vulcão da Coroa", "Altar da Forja Baixa" }, factions = new[] { "Oficinas Clandestinas" } },
            new RegionDef { id = RegionId.MarDeVidro, scene = "MarDeVidro", name = "Mar de Vidro", epithet = "Ilhas e oceano que cristaliza sob certas frequências",
                identity = "Navios com rotas cantadas; faróis que são grandes artefatos.", tradition = "Maré, Sopro, navegação coral e encantos de farol.",
                conflict = "Reflexos e vozes podem trocar de lugar com tripulantes.", km = new Vector2(52, 6), radiusKm = 12, relicId = "ampulheta",
                minibosses = new[] { "Náufrago Prismático" }, vortices = new[] { "farol_submerso" }, dungeonId = "d_mar",
                enemies = new[] { "partido_em_dois", "corvo_fenda", "copista_fenda" }, dangerTier = 3,
                mainSettlement = "Ilha do Farol Velho", settlements = new[] { "Vila dos Cantores de Proa" },
                landmarks = new[] { "Mar Cristalizado", "Farol Submerso" }, factions = new[] { "Afinadores de Campo" } },
            new RegionDef { id = RegionId.Caliria, scene = "Caliria", name = "Calíria", epithet = "Arquipélago de clínicas-templo e jardins de sal",
                identity = "As escolas de Cura mais avançadas de Elyndra.", tradition = "Cura, Sutura, Pulso e harmonias comunitárias.",
                conflict = "Quando a cura deixa de ser cuidado e vira imposição de uma forma ideal?", km = new Vector2(30, 4), radiusKm = 7, relicId = "calice",
                minibosses = new[] { "Coro da Carne Perfeita", "Guardião do Cálice" }, vortices = new[] { "carne_perfeita" }, dungeonId = "d_caliria",
                enemies = new[] { "sussurrante", "morador_sem_palavra", "corista_suspenso" }, dangerTier = 2,
                mainSettlement = "Clínica-Templo do Sal", settlements = new[] { "Jardins de Sal" },
                landmarks = new[] { "Clínica-Templo do Sal", "Jardins de Sal" }, factions = new[] { "Conservatório das Grandes Vozes" } },
            new RegionDef { id = RegionId.Sombrafonte, scene = "Sombrafonte", name = "Sombrafonte", epithet = "Cidades subterrâneas em cavernas que repetem vozes por dias",
                identity = "Mineradores e arquivistas usam cristais de Eco e Véu.", tradition = "Eco, Véu, mineração de cristais sonoros.",
                conflict = "Pistas sobre o Nome ausente de Aren e o Prisma do Encanto.", km = new Vector2(20, 30), radiusKm = 6,
                minibosses = new[] { "Oráculo Repetido" }, vortices = new[] { "mil_respostas" }, dungeonId = "d_sombrafonte",
                enemies = new[] { "sussurrante_oco", "coruja_velada", "lobo_sem_faro" }, dangerTier = 3,
                mainSettlement = "Cidade Baixa de Sombrafonte", settlements = new[] { "Posto dos Mineradores" },
                landmarks = new[] { "Caverna das Mil Vozes", "Mina de Mil Respostas" }, factions = new[] { "Oficinas Clandestinas" } },
            new RegionDef { id = RegionId.FronteiraMuda, scene = "FronteiraMuda", name = "Fronteira Muda", epithet = "Terras destruídas perto da cicatriz do Vazio Mudo",
                identity = "Regiões inteiras perdem propriedades sonoras; a Fenda domina o céu.", tradition = "Quase nenhuma; sobreviventes usam sinais visuais.",
                conflict = "Acesso final à Fenda e ao Regente do Contracanto.", km = new Vector2(48, 76), radiusKm = 9,
                minibosses = new[] { "Voz de Vharos" }, vortices = new[] { "ausencia" }, dungeonId = "d_fronteira",
                enemies = new[] { "voz_vharos", "cervo_bifurcado", "lobo_partido", "coruja_ausencia" }, dangerTier = 5,
                mainSettlement = "Acampamento dos Sinais", settlements = new string[0],
                landmarks = new[] { "A Fenda do Contracanto", "A Cicatriz" }, factions = new[] { "Coro da Única Voz" } },
        };

        public static readonly RouteDef[] Routes =
        {
            new RouteDef { a = RegionId.Valteria, b = RegionId.Velaria, name = "Estrada das Caravanas", kind = RouteKind.Estrada, km = 18, requires = "nota:do" },
            new RouteDef { a = RegionId.Valteria, b = RegionId.Miralume, name = "Passo do Arquivo", kind = RouteKind.PassoDeMontanha, km = 17, requires = "nota:do" },
            new RouteDef { a = RegionId.Valteria, b = RegionId.Orvalume, name = "Trilha das Raízes", kind = RouteKind.Trilha, km = 19, requires = "nota:do" },
            new RouteDef { a = RegionId.Valteria, b = RegionId.Sombrafonte, name = "Gruta do Eco Longo", kind = RouteKind.Caverna, km = 14, requires = "flag:gruta_eco", secret = true },
            new RouteDef { a = RegionId.Valteria, b = RegionId.Caliria, name = "Descida do Rio Claro", kind = RouteKind.Rio, km = 16, requires = "nota:do" },
            new RouteDef { a = RegionId.Miralume, b = RegionId.Granith, name = "Escadaria de Pedra", kind = RouteKind.PassoDeMontanha, km = 15 },
            new RouteDef { a = RegionId.Miralume, b = RegionId.Sombrafonte, name = "Poço dos Copistas", kind = RouteKind.Caverna, km = 9, secret = true, requires = "flag:poco_copistas" },
            new RouteDef { a = RegionId.Miralume, b = RegionId.Helion, name = "Estrada das Sacadas", kind = RouteKind.Estrada, km = 33 },
            new RouteDef { a = RegionId.Velaria, b = RegionId.Helion, name = "Pontes Suspensas do Norte", kind = RouteKind.PonteSuspensa, km = 19 },
            new RouteDef { a = RegionId.Velaria, b = RegionId.Sefra, name = "Rota do Pedágio", kind = RouteKind.Estrada, km = 16 },
            new RouteDef { a = RegionId.Velaria, b = RegionId.Caliria, name = "Estrada da Costa", kind = RouteKind.Estrada, km = 29 },
            new RouteDef { a = RegionId.Orvalume, b = RegionId.Caliria, name = "Mangues Cantados", kind = RouteKind.Trilha, km = 21 },
            new RouteDef { a = RegionId.Orvalume, b = RegionId.Granith, name = "Vale das Raízes Altas", kind = RouteKind.Trilha, km = 26 },
            new RouteDef { a = RegionId.Caliria, b = RegionId.MarDeVidro, name = "Travessia de Vidro", kind = RouteKind.Mar, km = 22, requires = "flag:barco" },
            new RouteDef { a = RegionId.Sefra, b = RegionId.MarDeVidro, name = "Porto das Lanternas", kind = RouteKind.Mar, km = 20, requires = "flag:barco" },
            new RouteDef { a = RegionId.Helion, b = RegionId.CoroaDeCinza, name = "Estrada da Cinza", kind = RouteKind.Estrada, km = 26 },
            new RouteDef { a = RegionId.Granith, b = RegionId.CoroaDeCinza, name = "Passo Alto", kind = RouteKind.PassoDeMontanha, km = 30 },
            new RouteDef { a = RegionId.Helion, b = RegionId.Nereth, name = "Caminho dos Penhascos", kind = RouteKind.Trilha, km = 21 },
            new RouteDef { a = RegionId.Sefra, b = RegionId.Nereth, name = "Rio dos Enlutados", kind = RouteKind.Rio, km = 40 },
            new RouteDef { a = RegionId.Nereth, b = RegionId.FronteiraMuda, name = "Ponte do Último Toque", kind = RouteKind.PonteSuspensa, km = 22, requires = "nota:si" },
            new RouteDef { a = RegionId.CoroaDeCinza, b = RegionId.FronteiraMuda, name = "Garganta Muda", kind = RouteKind.PassoDeMontanha, km = 24, requires = "nota:si" },
        };

        // ------------------------------------------------------------ consultas

        public static RegionDef Region(RegionId id) { foreach (var r in Regions) if (r.id == id) return r; return null; }
        public static RegionDef RegionByScene(string scene) { foreach (var r in Regions) if (r.scene == scene) return r; return null; }
        public static NoteDef Note(string id) { foreach (var n in Notes) if (n.id == id) return n; return null; }
        public static RelicDef Relic(string id) { foreach (var n in Relics) if (n.id == id) return n; return null; }
        public static VortexDef Vortex(string id) { foreach (var n in Vortices) if (n.id == id) return n; return null; }
        public static DungeonDef Dungeon(string id) { foreach (var n in Dungeons) if (n.id == id) return n; return null; }
        public static string DungeonScene(string id) => "D_" + id.Substring(2);

        public static IEnumerable<RouteDef> RoutesOf(RegionId id)
        {
            foreach (var r in Routes) if (r.a == id || r.b == id) yield return r;
        }

        public static RegionId Other(RouteDef r, RegionId from) => r.a == from ? r.b : r.a;

        /// <summary>Direção (no plano, normalizada, x = leste, z = norte) de um reino para outro.</summary>
        public static Vector3 Direction(RegionId from, RegionId to)
        {
            var d = Region(to).km - Region(from).km;
            return new Vector3(d.x, 0, d.y).normalized;
        }

        public static Vector3 FendaDirection(RegionId from)
        {
            var d = FendaKm - Region(from).km;
            return new Vector3(d.x, 0, d.y).normalized;
        }

        public static float FendaDistanceKm(RegionId from) => (FendaKm - Region(from).km).magnitude;
    }
}
