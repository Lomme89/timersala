# Novità

## v2.3.0-beta.2 — 7 ottobre 2026 (anteprima)

Quinta anteprima: rifiniture dell'interfaccia, provate finestra per finestra su Windows. Si può sempre tornare alla v1.14.0 senza perdere nulla.

**Tema chiaro e scuro**
- Il tema chiaro ora vale davvero per tutto: prima molti pulsanti, caselle e riquadri restavano scuri. Il cambio si applica subito, anche alle finestre già aperte.
- Barra del titolo nel colore del tema, senza l'icona generica di Windows nelle finestre secondarie (impostazioni, editor, dialoghi).
- Nel tema scuro la data accanto all'adunanza era quasi invisibile: ora si legge.
- Con l'interfaccia ingrandita (*Impostazioni → Programma*) si ingrandiscono anche i menu del tasto destro e i suggerimenti.

**Controller**
- Un po' più largo all'apertura, così la pastiglia di anticipo o ritardo non finisce sotto le icone; se lo spazio manca si accorcia. L'orologio non la fa più «ballare» a ogni secondo.
- Con più congregazioni, il nome di quella attuale ha una riga sua; nel menu per cambiarla è spuntata quella in uso.
- A riposo la scheda mostra «Inizio» e «Fine prevista» affiancati.
- Il riquadro della rete si apre allineato sotto il suo pulsante.
- In fondo non resta più una striscia vuota; il programma sfuma invece di finire tagliato.

**Modalità mini**
- Il passaggio dal controller alla mini e ritorno è diretto, senza l'immagine che si deformava.
- La casella del messaggio è alta quanto i pulsanti accanto.

**Impostazioni**
- *Schermo*: il nome del monitor non va più a capo lettera per lettera; i colori dell'avviso giallo stanno a tutta larghezza.
- *Countdown*: le anteprime degli stili sono miniature dello schermo vero.
- Le schede sono allineate e tutte le pagine hanno la stessa larghezza.
- Chiudendo la finestra non si riapplicano più per errore valori di prima (per esempio la dimensione dell'interfaccia).
- Se le impostazioni non si possono salvare (file bloccato, disco pieno) il controller lo dice, invece di perderle in silenzio.

**Telefono e tablet**
- In orizzontale il titolo della parte non finisce più sotto i pulsanti in alto.

## v2.3.0-beta.1 — 6 ottobre 2026 (anteprima)

Quarta anteprima: contiene la 2.2 beta e la tappa 2.3 della roadmap (adunanze particolari e rifiniture dei tempi). Si può sempre tornare alla v1.14.0 senza perdere nulla.

**Più congregazioni nella stessa sala**
- In *Impostazioni → Adunanze → Congregazioni* si aggiunge un'altra congregazione: parte dalle impostazioni di quella attuale, poi ha orari, schemi, stile, frasi pronte e settimane particolari suoi.
- All'apertura TimerSala sceglie da solo la congregazione dell'adunanza di adesso. Il nome compare in alto nel controller e con un clic si cambia.
- Schermo della sala, finestre, microfono, telecomando, rete, PIN e telefoni autorizzati restano quelli del PC, uguali per tutte.

**Settimane particolari**, in un unico elenco in *Impostazioni → Adunanze*:
- **visita del sorvegliante**, come prima;
- **assemblea**: niente adunanze e niente countdown; schermo e telefono scrivono «Settimana dell'assemblea»;
- **Commemorazione**, con giorno e ora: prende il posto dell'adunanza di quella parte della settimana, con il suo schema (cantico, discorso, cantico) che si può sistemare nell'editor;
- **discorso speciale**: il discorso pubblico diventa «Discorso speciale».

**Evento fuori programma**
- Dal menu della parte (tasto destro): titolo, ora d'inizio e parti, per esempio un discorso di matrimonio o di funerale. Countdown, schermo e telefono funzionano come sempre e lo schema della settimana non viene toccato. «Torna all'adunanza» riporta tutto com'era. Gli ultimi eventi restano come suggerimenti.

**Tempi**
- **Avviso giallo per tipo di parte** (*Impostazioni → Schermo*): parti degli studenti, discorsi e altre parti, ognuna in secondi fissi o in percentuale della parte. Chi non cambia nulla ha tutto come prima.
- **Parti con video**: le parti che nello schema di wol hanno un video sono segnate con un'icona nel controller e sul telefono, per ricordarsi di prepararlo in JW Library. Nell'editor il segno si mette o si toglie a mano.
- **Taratura dell'avvio con la voce** (*Impostazioni → Voce*): 5 secondi di sala in silenzio, poi 5 secondi di voce; la soglia si imposta da sola, a metà strada. Se voce e silenzio sono troppo vicini lo dice invece di impostare una soglia inaffidabile.

## v2.2.0-beta.1 — 6 ottobre 2026 (anteprima)

Terza anteprima: contiene la 2.1 beta e la tappa 2.2 della roadmap (telefono, rete e comandi). Si può sempre tornare alla v1.14.0 senza perdere nulla.

**Telefoni autorizzati**
- Dopo il primo PIN il telefono riceve un **codice suo** e non chiede più il PIN, nemmeno se il PIN cambia. Sul PC resta solo un'impronta del codice.
- Nell'icona della rete c'è l'elenco dei telefoni autorizzati, con l'ultimo uso: **✕** ne revoca uno, «Revoca tutti» li revoca tutti. Un telefono revocato deve reinserire il PIN.
- Nello stesso riquadro, l'interruttore **Comandi dal telefono** blocca al volo i comandi: i telefoni continuano a vedere il timer.
- Il riquadro dice anche quanti dispositivi sono collegati e quanti «con il controllo».

**Azioni dal telefono**
- Quando qualcuno avvia, cambia parte, aggiunge un minuto o manda un messaggio dal telefono, il controller lo mostra per qualche secondo («Avviata dal telefono: …»). La fermata dal telefono ha già la sua barra con «Annulla».

**Collegamento più semplice**
- **Stampa il cartoncino** (icona della rete): un foglio A4 con due copie da ritagliare, con il QR per *seguire* il timer e due righe di istruzioni. Mai il PIN.
- Se l'indirizzo del PC cambia rispetto all'ultima volta, un avviso dice che i QR stampati non valgono più e suggerisce di usare il nome del PC, che non cambia.

**Pagina web**
- **Aspetto per dispositivo** (nuovo pulsante con lo schermo in alto): come lo schermo della sala, solo le cifre, classico, oppure con il programma accanto. Ogni telefono o tablet ricorda il suo.
- Sul telefono la barra in alto mostra solo le icone, così ci stanno tutte.

**Telecomando per presentazioni**
- **Pagina giù** («avanti» sui telecomandi) avvia o ferma come il pulsante Avvia, compresa l'attesa della voce. **Pagina su** («indietro») è sempre **Annulla**, mai la parte precedente: un colpo per sbaglio si rimedia. Le frecce continuano a scegliere la parte.
- In *Impostazioni → Programma* il telecomando si può **associare**: da lì funziona anche con JW Library o Zoom in primo piano. Se per sbaglio si associa una tastiera vera, appena si scrive una lettera l'associazione si annulla da sola.

## v2.1.0-beta.1 — 6 ottobre 2026 (anteprima)

Seconda anteprima: contiene tutta la 2.0 beta.1 e la tappa 2.1 della roadmap (preparazione, aiuto e Windows). Come prima, si può tornare alla v1.14.0 senza perdere nulla.

**Prima dell'adunanza**
- **Lista di controllo:** compare nel controller durante il countdown d'inizio, o con il pulsante ☑ in alto. Microfoni, Zoom, video in JW Library, schermo della sala: si spunta con un clic e si azzera a ogni adunanza. Se alla prima parte manca qualcosa, un avviso discreto, mai bloccante. Le voci si cambiano in *Impostazioni → Adunanze*.
- **Schema troppo lungo:** se le parti, con i cantici, superano la durata dell'adunanza, accanto alla settimana compare «3 min oltre». Nell'editor c'è anche l'orario di fine previsto.

**Editor**
- **Annulla e Ripeti** (Ctrl+Z e Ctrl+Y, o i pulsanti in basso): una parte eliminata o spostata per sbaglio torna subito. Mentre scrivi un titolo, una parola alla volta.

**Esporta e importa**
- **Backup** in *Impostazioni → Programma*: impostazioni, frasi pronte e schemi modificati a mano in un solo file `.timersala`, per cambiare PC o preparare un'altra sala uguale. Importandolo, schermo della sala, finestre e microfono restano quelli del PC.
- **Una settimana:** dal menu del download, «Esporta questa settimana…» la salva in un file da aprire sul PC della sala con «Importa da file…». Chi coordina la prepara con calma a casa.

**Aiuto**
- **Configurazione guidata** al primo avvio: schermo della sala (con «Identifica»), orari, prova del telefono con il QR e collegamento sul desktop. Si rifà da *Impostazioni → Programma*.
- **Guida rapida** con il pulsante ? in alto o con F1: le funzioni principali e i tasti.
- **Cosa c'è di nuovo:** dopo un aggiornamento una finestra mostra le novità della versione, una volta sola.
- **Segnalazioni:** «Qualcosa non funziona» e «Ho un'idea» aprono GitHub con la segnalazione già preparata: versione, sistema ed errori recenti. Il PIN non viene mai incluso.
- **Modalità addestramento** (dalla guida o da *Impostazioni → Programma*): l'adunanza della settimana con le parti dieci volte più brevi, per insegnare il programma a un nuovo fratello dell'acustica. Schermo della sala e telefoni mostrano «PROVA · ADDESTRAMENTO», niente viene salvato, e con «Esci» si torna esattamente com'era.

**Windows**
- **Apri all'avvio di Windows** e **Passa alla mini alla prima parte**, in *Impostazioni → Programma*.
- **Avanzamento sull'icona della barra delle applicazioni:** si riempie in verde, giallo e rosso con la parte in corso, anche con il controller coperto da Zoom.
- **Menu dell'icona** (tasto destro sulla barra): mostra o nascondi lo schermo della sala, modalità mini, collega un telefono.

**Sito**
- Accanto a «Scarica» c'è il numero dell'ultima versione, e c'è la nuova pagina **Novità** con tutte le versioni.

## v2.0.0-beta.1 — 6 ottobre 2026 (anteprima)

Versione di anteprima della 2.0, da provare in sala per qualche settimana prima del rilascio. Impostazioni, schemi salvati (anche quelli modificati a mano), frasi pronte e visite del sorvegliante della 1.x passano intatti. Se qualcosa non va, si può tornare alla v1.14.0 senza perdere nulla.

**Impostazioni riorganizzate**
- Barra laterale con sette sezioni al posto delle schede: Adunanze, Schermo, Countdown, Messaggi, Voce (beta), Rete e telefono, Programma.
- **Le modifiche si applicano subito.** In fondo restano solo «Fatto» e «Annulla le modifiche», che riporta tutto com'era all'apertura.
- **Niente più errori al salvataggio:** interruttori per le funzioni, orari che si scrivono o si regolano con le frecce, numeri con − e +. Un valore non valido torna semplicemente quello di prima.
- *Schermo*: anteprima dal vivo, «durante la parte» o «a riposo»; tema a pulsanti, layout con le miniature; tabella «Cosa mostrare» con le colonne *Durante la parte* e *A riposo*.
- *Countdown*: minuti, stile (con le miniature) e Prova, finalmente in un posto solo.
- *Messaggi*: interruttore generale in cima; frasi pronte in un elenco da riordinare trascinando, con ✕ e «Aggiungi una frase».
- *Adunanze*: fine prevista calcolata subito accanto alla durata; le visite del sorvegliante stanno sotto «Settimane particolari».
- Spiegazioni brevi sotto ogni opzione, i dettagli nella «i»; porta di rete e regolazioni fini della voce dietro «Mostra opzioni avanzate».
- La versione del programma compare nelle impostazioni.

**Controller riorganizzato**
- **Intestazione:** ora, «In orario» o ritardo in una pastiglia colorata, e le icone di rete, mini, editor e impostazioni. L'icona della rete ha un pallino verde quando il timer in rete è attivo; un clic apre il riquadro con indirizzo, QR e numero di dispositivi collegati. Sparisce la riga «Timer in rete» in fondo.
- **Settimana in una riga** («Dom 4 ott · Fine settimana · Geremia 38-39»): la freccia apre il riquadro completo con le settimane, il download, infrasettimanale/fine settimana e il sorvegliante. Si chiude da solo quando parte una parte.
- **Riquadro del tempo più ricco:** cifre più grandi e, sotto, oltre ad assegnato e trascorso, la parte successiva, l'orario d'inizio e la fine prevista.
- **Messaggi in una riga:** campo, invio e tutto schermo; il messaggio in onda compare in una pastiglia gialla con «Togli».
- **Programma:** l'elenco è più alto e mostra l'orario previsto d'inizio di ogni parte; la parte in corso è più grande ed evidenziata, quelle già fatte si compattano mostrando il tempo effettivo, e lo scorrimento tiene al centro la parte corrente.

**Menu della parte** (tasto destro o «⋯»), per gli imprevisti senza aprire l'editor
- **Modifica:** titolo e durata, per esempio il tema del discorso pubblico o il nome dell'oratore ospite. Vale anche per la parte in corso.
- **Sposta dopo la prossima:** scambia l'ordine se un oratore non è ancora pronto.
- **Salta:** per esempio uno studente assente. Si passa alla successiva e il ritardo ne tiene conto.
- Sposta e Salta chiedono conferma; dopo ogni azione c'è «Annulla» per 10 secondi (anche con Esc), nella stessa barra di «Annulla» dopo Ferma.

**Modalità mini**
- La barra in alto dice cosa sta succedendo: durante l'attesa della voce il titolo lascia il posto a «In attesa di una pausa…» (giallo, con il pallino che pulsa), poi «In ascolto: parte alla prima voce»; quando la parte parte torna il titolo.
- Messaggi più puliti: una riga con campo, invio e tutto schermo; il messaggio in onda diventa una pastiglia gialla «Sullo schermo: …» con «Togli», che sparisce quando non c'è nulla sullo schermo.

**Movimento** (solo dove aiuta a capire, mai continuo)
- **Controller ↔ mini:** la finestra si restringe nella mini (e si allarga tornando indietro) invece di saltare.
- **Cambi di colore più leggibili:** al passaggio verde → giallo → rosso le cifre fanno un solo leggero «respiro», sullo schermo della sala e nel controller.
- **Pulsante Avvia/Ferma:** il colore sfuma e l'icona entra con un piccolo scatto.
- **Avvio senza lampo bianco:** lo schermo della sala parte nero e il contenuto sfuma; sul proiettore mai un flash o una finestra vuota.
- **Riduci movimento:** se in Windows gli effetti di animazione sono spenti (o sul telefono è attivo «riduci movimento»), tutto questo si spegne, compresi il lampeggio allo scadere e l'onda del countdown «Marea».

**Tema e dimensione dell'interfaccia** (*Impostazioni → Programma*)
- **Tema chiaro** per controller, mini, editor e impostazioni, utile se l'acustica è in un punto molto luminoso: scuro, chiaro o «come Windows» (segue il tema delle app di Windows anche mentre il programma è aperto). Lo schermo della sala resta con il suo tema.
- **Dimensione dell'interfaccia** dal 90 al 150 %, per portatili piccoli o schermi ad alta risoluzione. Le finestre aperte si adattano subito.

## v1.14.0 — 6 ottobre 2026

**Sicurezza in sala**
- **Annulla dopo Ferma:** per 5 secondi dopo Ferma compare «Annulla» (anche nella mini, oppure **Esc**). Il timer riprende come se non fosse mai stato fermato, contando anche quei secondi. Vale anche per una fermata dal telefono.
- **Chiusura protetta:** TimerSala chiede conferma prima di chiudersi anche durante il countdown d'inizio e quando una parte è in attesa della voce, non solo con il timer in funzione.
- **Porta di rete occupata:** se un altro programma usa la porta del timer in rete, TimerSala prova le successive e dice quale sta usando, invece di lasciare il timer in rete spento.
- **Orologio del PC sbagliato:** se l'ora del PC si discosta di oltre un minuto da quella di internet, il controller lo segnala: countdown d'inizio e fine prevista dipendono da quell'ora.
- **Finestre ricordate:** controller, mini, editor e impostazioni si riaprono dove e grandi come erano, anche su un altro monitor. Se quel monitor non è collegato si aprono nella posizione solita.

## v1.13.2 — 4 ottobre 2026

- **PC sempre acceso durante l'adunanza:** da mezz'ora prima dell'inizio fino a mezz'ora dopo la fine prevista, e comunque mentre una parte è in corso, Windows non sospende il PC e non spegne gli schermi. Anche il salvaschermo resta spento.
- **Schermo della sala ricollegato:** se il cavo si stacca o la TV si spegne, il timer si nasconde invece di finire sopra il controller. Quando lo schermo torna, il timer ci ritorna da solo, anche dopo la sospensione del PC.
- **Una sola copia aperta:** se apri TimerSala quando è già aperto, viene in primo piano la finestra esistente invece di partire una seconda copia, che avrebbe conteso il timer in rete alla prima.

## v1.13.1 — 4 ottobre 2026

- Pagina web aggiunta alla schermata Home (iPad, iPhone): titolo, pulsanti e cifre non finiscono più sotto la barra di stato in alto o sotto l'indicatore Home in basso.

## v1.13.0 — 4 ottobre 2026

**Visite del sorvegliante pianificate** (*Impostazioni → Adunanze*)
- Segna in anticipo le settimane della visita, anche a mesi di distanza. Si tolgono con ✕.
- Quelle settimane vengono scaricate già adattate alla visita, anche con «Scarica tutte le prossime settimane».
- Scegli in che giorno si tiene l'infrasettimanale durante la visita (per esempio il martedì) e, se serve, un orario diverso. Countdown d'inizio e fine prevista seguono il giorno scelto.
- Il pulsante della visita nell'editor aggiorna anche l'elenco, e viceversa. Il sottotitolo della settimana indica «visita del sorvegliante».

## v1.12.0 — 4 ottobre 2026

**Messaggi a tutto schermo**
- Un nuovo pulsante accanto a «Mostra», oppure **Ctrl+Invio**, manda il messaggio a tutto schermo:
  - testo giallo grande quanto possibile, con il tempo piccolo in un angolo;
  - dopo qualche secondo (6, regolabili in *Impostazioni → Messaggi*) passa nella solita fascia.
- È utile per richiamare l'oratore, ma anche per mandargli a distanza il numero di un cantico o il nome di chi ha alzato la mano da casa.
- Dal telefono c'è l'interruttore **A tutto schermo** nel pannello dei messaggi. Vale anche per le frasi pronte.

## v1.11.0 — 3 ottobre 2026

**Countdown d'inizio, sette stili a scelta** (*Impostazioni → Schermo*):
- **Marea** (nuovo predefinito): lo schermo si riempie di blu dal basso fino all'inizio;
- **Anello**: blu notte, un anello che si chiude;
- **Blocchi**: cifre grandi e un blocco per ogni minuto;
- **Orologio**: l'ora attuale in grande, l'inizio sotto;
- **Quadrante**: i minuti al centro, i secondi sulle tacche;
- **A parole**: «si comincia tra 5 minuti», i secondi solo nell'ultimo minuto;
- **Classico**: come prima, uguale a una parte.

Gli stili nuovi non usano verde, giallo e rosso, così il countdown non sembra una parte. Il pulsante **Prova** mostra lo stile scelto per 12 secondi, anche nella pagina web.

## v1.10.2 — 3 ottobre 2026

- Avvio con la voce: dopo Avvia bisogna sempre aspettare una pausa intera, anche se prima il microfono era muto. Quello che si sente subito dopo la pressione conta come voce del presidente. Prima, con il microfono muto da un po', il timer si metteva subito in ascolto e partiva alla prima parola.

## v1.10.1 — 3 ottobre 2026

- Avvio con la voce: la pausa si misura sempre da quando premi Avvia. Se premi durante un breve silenzio del presidente, quel silenzio non conta più come parte della pausa. Se premi tardi, quando la pausa è già lunga quanto quella impostata, il timer si mette subito in ascolto.

## v1.10.0 — 3 ottobre 2026

**Avvio con la voce (sperimentale, spento di default)**
- Premi Avvia mentre il presidente presenta la parte: il pulsante diventa **In attesa**. Dopo una pausa, la prima voce fa partire il timer, contando dall'istante in cui la voce è iniziata.
- Premi di nuovo Avvia per partire subito. **Esc** annulla l'attesa o, nei primi secondi, un avvio sbagliato (si torna in attesa).
- Colpi, tosse e regolazioni del microfono vengono ignorati: conta solo il parlato abbastanza lungo e nelle frequenze della voce.
- Nuova scheda **Impostazioni → Voce**:
  - scelta dell'ingresso audio, per esempio lo stesso del mixer usato per Zoom;
  - barra di prova con la soglia;
  - durata della pausa e della voce regolabili.
- Opzione per mettere in attesa la parte successiva subito dopo Ferma, quando non c'è un cantico in mezzo.
- Dal telefono l'avvio resta immediato.

## v1.9.0 — 3 ottobre 2026

**Pagina web sui tablet**
- Si può aggiungere alla schermata Home con la sua icona. Su iPad si apre senza barre del browser.
- Nuovo pulsante **Schermo sempre acceso**. Funziona anche sulla rete locale, senza HTTPS, e la scelta viene ricordata.
- Il pulsante **Schermo intero** si illumina quando è attivo e si nasconde dove il browser non lo supporta (iPhone).

## v1.8.0 — 2 ottobre 2026

- **Ripristino dopo un riavvio:** se il PC o il programma si chiudono durante l'adunanza, alla riapertura puoi scegliere tra:
  - riprendere contando il tempo trascorso;
  - riprendere da dove era fermo;
  - ricominciare.
- **Studio adattivo** (opzionale, per lo studio biblico e per la Torre di Guardia):
  - in caso di ritardo lo studio si accorcia per finire in orario, al massimo della metà;
  - non si allunga mai;
  - la durata adattata è in arancione.

## v1.7.2 — 1 ottobre 2026

- Modalità mini:
  - cifre più grandi e centrate con i comandi;
  - la dimensione non cambia al cambiare dei numeri;
  - i pulsanti −1 e +1 sono larghi quanto la fila di icone.

## v1.7.1 — 1 ottobre 2026

- Modalità mini: tolta la riga sotto il tempo, cifre più grandi.

## v1.7.0 — 1 ottobre 2026

- Modalità mini ridisegnata:
  - titolo della parte in alto;
  - cifre grandi;
  - barra di avanzamento sul bordo inferiore;
  - bordo rosso allo sforamento.

## v1.6.2 — 1 ottobre 2026

- Impostazioni più compatte:
  - la scheda Timer è unita a Schermo;
  - «Sempre in primo piano» e «Avviso giallo» sono nella scheda Schermo.

## v1.6.1 — 1 ottobre 2026

- Editor più snello:
  - vengono mostrate solo le sezioni dell'adunanza in modifica;
  - minuti rapidi su una riga;
  - Duplica ed Elimina accanto al titolo.
- Editor e impostazioni si aprono più alti, adattati allo schermo.

## v1.6.0 — 1 ottobre 2026

- **Scarica tutte le prossime settimane** già pubblicate, in un colpo solo:
  - gli schemi modificati a mano non vengono sovrascritti;
  - la visita del sorvegliante viene riapplicata.

## v1.5.1 — 1 ottobre 2026

- Scelta del monitor del timer spostata in Impostazioni → Schermo.
- L'indirizzo web usa la scheda di rete principale e ignora quelle virtuali (VirtualBox, Hyper‑V, VPN). In alternativa puoi scegliere il nome del PC o un indirizzo preciso.
- Nuova riga «Timer in rete» nel controller, con il pulsante «Collega telefono».

## v1.5.0 — 1 ottobre 2026

- Pagina web riprogettata:
  - visualizzazione, programma e controllo ben separati;
  - icone;
  - pannello dedicato ai messaggi.
- Interruttore generale per attivare o disattivare i messaggi all'oratore.

## v1.4.1 — 1 ottobre 2026

- Editor: trascinamento animato delle parti. La riga si solleva e segue il mouse, le altre le fanno spazio.

## v1.4.0 — 1 ottobre 2026

- Editor dello schema riprogettato:
  - elenco delle parti a sinistra, pannello di modifica a destra;
  - totale dei minuti aggiornato in tempo reale.

## v1.3.0 — 1 ottobre 2026

- Barra del titolo scura, intonata al programma.
- Il codice QR di controllo, che contiene il PIN, compare solo su richiesta.
- Animazioni:
  - colori che sfumano;
  - messaggi che entrano dal basso;
  - finestre in dissolvenza.
- Nuova icona.

## v1.2.1 — 1 ottobre 2026

- Corretto il testo scuro, illeggibile, in alcune caselle delle impostazioni e nell'elenco delle parti.

## v1.2.0 — 1 ottobre 2026

- **Messaggi all'oratore** in una fascia gialla, con frasi pronte personalizzabili o testo libero.
- **Controllo dal telefono** protetto da PIN, con blocco dopo 5 tentativi errati.
- Codici QR per aprire il timer o il controllo dal telefono.

## v1.1.1 — 1 ottobre 2026

- Tutti i controlli delle impostazioni sono leggibili, indipendentemente dal tema di Windows.
- Rimossi i pulsanti Countdown (ora è automatico) e Consiglio.

## v1.1.0 — 1 ottobre 2026

- Giorno e ora delle adunanze, con **countdown automatico** prima dell'inizio e fine prevista nel controller.
- **Modalità mini** sempre in primo piano.
- Stili dello schermo:
  - tema chiaro o scuro;
  - carattere;
  - layout classico, clessidra o solo cifre;
  - elementi nascondibili.

## v1.0.1 — 1 ottobre 2026

- Corretto l'errore all'avvio («connectionId»).

## v1.0.0 — 1 ottobre 2026

Prima versione:
- schemi da wol.jw.org;
- controller compatto;
- timer a tutto schermo sul monitor scelto;
- editor dello schema con la visita del sorvegliante;
- timer visibile in rete dal browser.
