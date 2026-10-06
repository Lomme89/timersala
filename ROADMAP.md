# Idee per TimerSala 2.0

Appunti delle proposte da realizzare, divisi in tappe. Ogni voce dice il problema che risolve e una prima idea di come farla.
Le tappe sono indicative: l'ordine dentro una tappa non è una priorità.

## Principi (valgono per tutte le voci)
- **Decide sempre una persona**: niente passaggi automatici di parte né rilevamenti da JW Library.
- **Niente azioni accidentali durante l'adunanza**: ciò che cambia lo schema o il tempo in corso passa da un menu o una conferma
  e ha sempre un «Annulla» subito disponibile. Niente doppi clic o trascinamenti «nudi» sul controller.
- **Lo schermo della sala non deve distrarre**: movimento solo dove aiuta a capire, mai continuo; tutto si spegne con
  «riduci movimento» (Windows o telefono), compresi gli stili animati del countdown.
- **Funziona sui PC vecchi** che stanno spesso in sala: le animazioni non devono rallentare lo schermo del timer.
- Gli studi adattivi si accorciano soltanto; nessuna pausa del timer.

---

## 2.0 · Nuovo aspetto e sala sicura

### Controller riorganizzato (nuovo design) — fatto
Mockup approvato: [prima e dopo](docs/roadmap/controller-prima-dopo.png). Stesso stile delle nuove impostazioni.
- **Intestazione**: ora, etichetta «In orario» / ritardo, icone mini · editor · impostazioni e **icona della rete**
  (pallino verde; un clic apre il riquadro della rete: indirizzo, QR, dispositivi collegati). Sparisce la riga «Timer in rete» in fondo.
- **Settimana in una riga** («Dom 4 ott · Fine settimana · Geremia 38-39»): la freccia apre il riquadro completo con frecce
  delle settimane, download, infrasettimanale/fine settimana e sorvegliante. Durante l'adunanza resta chiuso.
- **Riquadro del tempo più ricco**: cifre più grandi; sotto, oltre ad assegnato e trascorso, la parte successiva, l'orario
  d'inizio e la fine prevista.
- **Messaggi in una riga**: campo, invio, tutto schermo; si allarga solo quando un messaggio è sullo schermo.
- **Elenco delle parti**: più alto; la parte in corso è più grande ed evidenziata, le passate si compattano mostrando il tempo
  effettivo, lo scorrimento tiene al centro la parte corrente. Facoltativo: orario previsto d'inizio di ogni parte.
- **Menu della parte** (tasto destro o «⋯»), per gli imprevisti senza aprire l'editor, con conferma e «Annulla»:
  - *Modifica*: durata e titolo (per esempio il tema del discorso pubblico o il nome dell'oratore ospite);
  - *Sposta dopo la prossima*: scambia l'ordine se un oratore non è ancora pronto;
  - *Salta*: per esempio uno studente assente; si passa alla successiva e il ritardo ne tiene conto.

### Modalità mini: ritocchi — fatto
Mockup approvato: [prima e dopo](docs/roadmap/mini-prima-dopo.png). Niente di stravolto: cifre, pulsanti, barra e cornice restano.
- **Stato della voce nella barra in alto**: durante l'attesa il titolo lascia il posto a «In attesa di una pausa…» (giallo, pallino
  che pulsa), poi «In ascolto: parte alla prima voce»; quando la parte parte torna il titolo.
- **Messaggi più puliti**: riga con campo, invio e tutto schermo; il messaggio in onda diventa una pastiglia gialla
  «Sullo schermo: …» con «Togli», che sparisce quando non c'è nulla in onda.
- Stessi angoli, icone e colori del nuovo controller.

### Impostazioni riorganizzate (nuovo design) — fatto
Mockup approvato: [Schermo](docs/roadmap/impostazioni-schermo.png) · [Messaggi](docs/roadmap/impostazioni-messaggi.png) · [Adunanze](docs/roadmap/impostazioni-adunanze.png). Stessi colori del programma.
- **Barra laterale con icone** al posto delle schede, sette sezioni: Adunanze · Schermo · Countdown · Messaggi · Voce (beta) ·
  Rete e telefono · Programma.
- **Dove vanno le opzioni nuove**:
  - *Adunanze*: giorno e orario per adunanza, durata con la fine prevista calcolata subito, studi adattivi con interruttore,
    **settimane particolari** (vedi 2.3; oggi solo la visita del sorvegliante, v1.13.0) con giorno e orario dell'infrasettimanale
    durante la visita;
  - *Schermo*: anteprima dal vivo a destra; tabella «Cosa mostrare» con colonne *Durante* e *A riposo*; avviso giallo
    (anche per tipo di parte, vedi 2.3);
  - *Countdown*: minuti, stile e Prova, finalmente in un posto solo;
  - *Messaggi*: interruttore generale in cima; frasi pronte come elenco da riordinare trascinando, con ✕ e «Aggiungi»;
  - *Programma*: tema del controller, scala dell'interfaccia, sempre in primo piano, avvio con Windows, mini automatica,
    backup, segnalazioni, cartella dati e versione.
- **Modifiche applicate subito**: in fondo solo «Fatto» e «Annulla le modifiche» (al posto di Applica / Annulla / Salva).
- **Controlli adatti al dato**: interruttori per le funzioni, selettori d'orario, numeri con − / +, scelte a pulsanti
  (tema Scuro/Chiaro, layout con miniature). Niente più errori al salvataggio.
- Spiegazioni in una riga sotto l'opzione, dettagli in una «i»; opzioni rare dietro «Mostra opzioni avanzate».

### Tema e scala — fatto
- **Tema chiaro per il controller**, per le acustiche in un punto molto luminoso: scuro, chiaro o automatico (come Windows).
  Lo schermo del timer resta indipendente.
- **Scala dell'interfaccia** (per esempio 90–150 %), per portatili piccoli o schermi ad alta risoluzione.

### Movimento — fatto (avvisi: un'unica barra «Annulla» per fermate e modifiche; le conferme restano le finestre di Windows)
- **Controller ↔ mini animato**: la finestra si restringe e le cifre restano al loro posto rimpicciolendosi, invece di saltare.
- **Cambi di colore più leggibili**: al passaggio verde → giallo → rosso un solo leggero «respiro» delle cifre, oltre alla sfumatura.
- **Pulsante Avvia/Ferma**: l'icona passa da ▶ a ■ con un'animazione e il colore sfuma, senza scatti.
- **Avvisi e conferme uniformi**: stessa posizione, animazione e tono dei testi in tutto il programma. Un solo sistema per
  «Annulla» (vedi sotto) e per gli avvisi delle azioni dal telefono (2.2).
- **Avvio senza lampo bianco**: lo schermo della sala parte nero e sfuma; sul proiettore mai un flash o una finestra vuota.
- **Rispetto di «riduci movimento»** (vedi Principi).

### Sicurezza in sala
Uscita tutta prima della 2.0: v1.13.2 e v1.14.0 (vedi «Fatte»). Nella 2.0 resta da portare «Annulla» nel sistema di avvisi
uniforme (vedi Movimento).

### Passaggio dalla 1.x
- Impostazioni, schemi salvati (anche quelli modificati a mano), frasi pronte e visite del sorvegliante passano intatti.
- Le opzioni che cambiano posto mantengono il loro valore; nessuna domanda all'utente se non serve.

### Prova sul campo
- Versione di anteprima (beta) da usare in sala per qualche settimana prima del rilascio, su un canale separato dalle stabili.
- Verifica della fluidità su un PC vecchio, con schermo della sala, pagina web e voce attivi insieme.

---

## 2.1 · Preparazione, aiuto e Windows

### Lista di controllo prima dell'adunanza
Un controllo veloce prima di iniziare, per non dimenticare nulla anche quando all'acustica c'è chi è meno esperto.
- Compare nel controller durante il countdown d'inizio (o con un pulsante), con voci personalizzabili nelle impostazioni:
  batterie dei microfoni, Zoom avviato con l'audio condiviso, cantici e video scaricati in JW Library, schermo della sala acceso…
- Si spunta con un clic; si azzera a ogni adunanza.
- Se all'inizio dell'adunanza manca qualche voce, un avviso discreto (mai bloccante).

### Modalità addestramento
Per spiegare il programma al volo a un nuovo fratello dell'acustica senza toccare l'adunanza vera.
- Un'adunanza simulata a velocità accelerata (per esempio ×10), con gli stessi comandi del controller.
- Schermo del timer e pagina web restano separati (o mostrano chiaramente «PROVA»): niente rischi in sala.
- Tempi, ritardi e messaggi non vengono salvati; alla chiusura si torna esattamente com'era.

### Editor
- **Avviso di sforamento**: se le durate superano la durata dell'adunanza, l'editor lo dice e di quanto
  («Lo schema dura 108 minuti: 3 minuti oltre, finirà alle 20:48»), aggiornato mentre si modifica; un piccolo segno anche
  nel controller sulla settimana interessata.
- **Annulla e ripeti** (Ctrl+Z / Ctrl+Y): una parte eliminata o spostata per sbaglio si recupera subito.

### Esporta e importa
Un solo formato di file per tre usi:
- **backup completo**: impostazioni, schemi modificati a mano e frasi pronte, per cambiare PC o configurare un'altra sala uguale;
- **una settimana**: preparare lo schema da casa e aprirlo sul PC della sala (anche mandandolo via rete);
- **un profilo** di congregazione, quando ci saranno i profili (2.3).

### Primo avvio, aiuto e novità
- **Primo avvio guidato**: schermo della sala (con «Identifica»), orari delle adunanze, prova del telefono con il QR;
  alla fine propone il collegamento sul desktop.
- **Guida dentro il programma**: pulsante «?» con funzioni principali e scorciatoie, con le immagini del README.
- **«Cosa c'è di nuovo»**: al primo avvio dopo un aggiornamento, una piccola finestra con le novità principali.
- **Sito**: numero dell'ultima versione accanto a Scarica e pagina «Novità». Programma e sito leggono lo stesso CHANGELOG.md.
- **Segnala un problema o lascia un suggerimento**: prepara una segnalazione con versione, sistema, impostazioni principali
  (senza PIN) ed errori recenti da `diagnostica\errori.log`, e apre la pagina su GitHub. Due modalità: «Qualcosa non funziona»
  e «Ho un'idea». Raggiungibile da *Impostazioni → Programma* e dalla guida.

### Windows
- **Avvio con Windows**: TimerSala parte all'accensione del PC, con lo schermo della sala acceso e lo schema già scaricato.
- **Passaggio automatico alla mini**: opzione; all'avvio della prima parte il controller passa alla mini (sempre in primo piano),
  così resta visibile sopra JW Library e Zoom. Si torna al controller come oggi (tasto M o pulsante).
- **Avanzamento sull'icona della barra delle applicazioni**: l'icona si riempie in verde, giallo e rosso con la parte in corso.
- **Menu dell'icona** (tasto destro): mostra/nascondi schermo sala, modalità mini, collega telefono.

---

## 2.2 · Telefono, rete e comandi

### Controllo remoto più chiaro
- **Azioni dal telefono evidenziate**: quando qualcuno avvia, ferma, cambia parte o manda un messaggio dal telefono, il controller
  lo mostra per un attimo («Fermato dal telefono»), con lo stesso sistema di avvisi del resto del programma.
- **Chi è collegato**, nel riquadro della rete del nuovo controller: «2 schermi · 1 con il controllo».
- **Blocca i comandi remoti**: interruttore al volo nel riquadro della rete, senza passare dalle impostazioni.
- **Dispositivo fidato**: dopo il primo PIN il telefono resta autorizzato finché non lo si revoca dal PC (elenco nel riquadro
  della rete, con «Revoca»). Niente PIN a ogni adunanza, sicurezza invariata.

### Collegamento più semplice
- **Cartoncino da stampare**: PDF con il QR per *seguire* il timer (mai quello col PIN) e due righe di istruzioni, da lasciare
  al leggio o all'acustica.
- **Avviso se l'indirizzo cambia**: se l'indirizzo del PC è diverso dall'ultima volta, avvisare che i QR stampati non valgono più
  e suggerire il nome del PC.

### Pagina web
- **Aspetto per dispositivo**: ogni telefono o tablet sceglie e ricorda il suo aspetto (solo cifre, classico, con il programma).
  Per esempio il tablet sul podio solo cifre, il telefono del presidente il programma.
- **Vibrazione** alla pressione di Avvia/Ferma (solo Android: iPhone non la consente dal browser).

### Telecomando per presentazioni
I telecomandi USB per PowerPoint (Pagina giù/su) comandano il timer lontano dal PC, senza telefono.
- Un tasto fa esattamente quello che fa Avvia/Ferma nel controller, compresa l'attesa della voce se attiva.
- L'altro tasto è «Annulla» (lo stesso gesto di Esc), non «parte precedente»: un colpo per sbaglio si rimedia sempre.

---

## 2.3 · Adunanze particolari e rifiniture dei tempi

### Settimane particolari
Estende l'elenco delle visite del sorvegliante (v1.13.0) a un unico elenco di settimane con un tipo, pianificabili in anticipo:
- **visita del sorvegliante** (come oggi, con giorno e orario dell'infrasettimanale durante la visita);
- **assemblea**: nessuna adunanza; schermo e pagina web mostrano un avviso al posto del countdown;
- **Commemorazione**: schema dedicato con il suo orario; se cade in un giorno feriale, quella settimana non c'è l'infrasettimanale;
- **discorso speciale**: riconosciuto dallo schema di wol quando presente.

### Modalità libera (eventi fuori programma)
Per i rari eventi in sala oltre alle adunanze (discorso di matrimonio o funerale, adunanza per il servizio, ecc.).
Usata poche volte l'anno: solo una voce nel menu del controller, niente in primo piano.
- Chiede titolo e ora d'inizio, poi quante parti ci sono (di solito una) e quanto dura ciascuna.
- Countdown, schermo e telefono funzionano come sempre; lo schema della settimana non viene toccato.
- Gli ultimi eventi restano tra i suggerimenti per riusarli.

### Profili per più congregazioni nella stessa sala
Molte Sale del Regno sono condivise da due o tre congregazioni, a volte di lingue diverse.
- Un profilo per congregazione: nome, orari, lingua del download da wol, stile dello schermo, frasi pronte, settimane particolari.
- Il programma sceglie da solo il profilo in base al giorno e all'ora; il nome della congregazione è ben visibile nel controller.
- Cambio manuale con un clic; schemi e impostazioni di ogni profilo restano separati. Si esporta anche un solo profilo.
- È la voce più grande della 2.x: tocca impostazioni, schemi e backup. Se pesa troppo può slittare.

### Tempi
- **Avviso giallo per tipo di parte**: soglie diverse (per esempio studenti e discorsi), in secondi fissi come oggi oppure
  in percentuale della parte.
- **Parti con video**: icona 🎬 nell'elenco per le parti che nello schema di wol contengono un video, per ricordarsi di prepararlo
  in JW Library; nell'editor il segno si aggiunge o si toglie a mano.
- **Taratura automatica dell'avvio con la voce**: pulsante «Taratura» nella sezione Voce; 5 secondi di sala in silenzio, poi
  qualche secondo di voce; la soglia va a metà strada tra i due livelli (con margine).

---

## Più avanti (2.5 o 3.0)
- **Interfaccia in altre lingue** (inglese, spagnolo, rumeno…), per le sale condivise con gruppi o congregazioni di altra lingua.
  Lo schema da wol si sceglie già per lingua: mancherebbe l'interfaccia. Va di pari passo con i profili.
- **Programma firmato digitalmente**, per togliere l'avviso di SmartScreen al primo avvio. Da tentare senza insistere:
  provare le opzioni gratuite o economiche per i progetti open source (per esempio SignPath Foundation).

## Fatte
- **Sicurezza in sala, seconda parte** (v1.14.0): «Annulla» per 5 secondi dopo Ferma (anche con Esc e dal telefono); conferma
  alla chiusura anche durante il countdown e l'attesa della voce; porta di rete alternativa se quella scelta è occupata;
  avviso se l'orologio del PC è sbagliato di oltre un minuto; controller, mini, editor e impostazioni ricordano posizione e
  dimensioni, anche su più monitor.
- **Sicurezza in sala** (v1.13.2): PC e schermi sempre accesi con un'adunanza in corso o imminente; schermo della sala che si
  nasconde se scollegato e torna da solo quando si ricollega; una sola copia del programma aperta.
- **Visite del sorvegliante pianificate** (v1.13.0): settimane segnate in anticipo in *Impostazioni → Adunanze*, scaricate già
  adattate; giorno (e ora facoltativa) dell'infrasettimanale durante la visita. Il layout va riallineato al nuovo design delle
  impostazioni e diventerà parte delle «Settimane particolari».

## Scartate (e perché)
- Ritmo dello studio a paragrafi: il conduttore può dedicare più tempo ai paragrafi più utili alla congregazione.
- Allarme «microfono chiuso»: c'è già un fratello dedicato all'audio e il riscontro è evidente.
- Fine parte dalla voce: durante una parte ci sono pause lunghe (per esempio per leggere una scrittura).
- Riconoscimento automatico da JW Library e passaggio automatico delle parti: serve sempre una persona.
- Pausa del timer.
- Versione per altri sistemi operativi: i PC dell'acustica restano su Windows per JW Library.
- Modifica rapida con doppio clic e trascinamento delle parti nel controller: troppo facili da attivare per sbaglio
  durante l'adunanza; sostituiti dal menu della parte.
