# Idee per TimerSala 2.0

Appunti delle proposte da realizzare. Ogni voce dice il problema che risolve e una prima idea di come farla.

## Da fare

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

### Profili per più congregazioni nella stessa sala
Molte Sale del Regno sono condivise da due o tre congregazioni, a volte di lingue diverse.
- Un profilo per congregazione: nome, orari delle adunanze, lingua del download da wol, stile dello schermo, messaggi pronti.
- Il programma sceglie da solo il profilo in base al giorno e all'ora; il nome della congregazione è ben visibile nel controller.
- Cambio manuale con un clic; schemi e impostazioni di ogni profilo restano separati.

### Settimane speciali
Le settimane che oggi vanno sistemate a mano, con un clic (o in automatico se wol lo indica), come per la visita del sorvegliante.
- Settimana dell'assemblea: nessuna adunanza; schermo e pagina web mostrano un avviso al posto del countdown.
- Commemorazione: schema dedicato con il suo orario; se cade in un giorno feriale, l'infrasettimanale di quella settimana non c'è.
- Discorso speciale: riconosciuto dallo schema di wol quando presente.

### Sicurezza in sala (da fare anche prima della 2.0)
- **Niente standby durante l'adunanza**: con un'adunanza in corso o imminente Windows non deve sospendere il PC né spegnere gli schermi (SetThreadExecutionState).
- **Monitor scollegato e ricollegato**: se lo schermo della sala sparisce e torna (cavo, TV riaccesa), la finestra del timer torna da sola sullo schermo scelto.
- **Un solo TimerSala aperto**: una seconda apertura riporta in primo piano quella già aperta invece di avviare un'altra copia (che si contenderebbe il server web).

### Tema chiaro per il controller
Per le sale in cui l'acustica è in un punto molto luminoso. Scuro, chiaro o automatico (come Windows); lo schermo del timer resta indipendente.

### Primo avvio guidato
Tre passi alla prima apertura: schermo della sala (con «Identifica»), orari delle adunanze, prova del telefono con il QR. Alla fine propone il collegamento sul desktop.

### Backup della configurazione
Esporta e importa in un unico file impostazioni, schemi modificati a mano e messaggi pronti: per cambiare PC o configurare un'altra sala allo stesso modo. Con i profili per congregazione, si può esportare anche un solo profilo.

### Controllo remoto più chiaro
- **Avvisi delle azioni remote**: quando qualcuno avvia, ferma, cambia parte o manda un messaggio dal telefono, il controller lo mostra per un attimo («Fermato dal telefono»).
- **Chi è collegato**: «2 telefoni collegati · 1 con il controllo».
- **Blocca i comandi remoti**: interruttore al volo nel controller, senza passare dalle impostazioni.

### Collegamento più semplice
- **Cartoncino da stampare**: PDF con il QR per seguire il timer e due righe di istruzioni, da lasciare al leggio o all'acustica.
- **Avviso se l'indirizzo cambia**: se l'indirizzo del PC è diverso dall'ultima volta, avvisare che i QR stampati non valgono più e suggerire il nome del PC.

### Parti con video
- Icona 🎬 nell'elenco del controller per le parti che nello schema di wol contengono un video, per ricordarsi di prepararlo in JW Library.
- Nell'editor il segno si aggiunge o si toglie a mano (per esempio per una parte locale).

### Guida dentro il programma
Pulsante «?» con una guida rapida: funzioni principali e scorciatoie, con le immagini del README.

### Sito: versione e novità
Numero dell'ultima versione accanto al pulsante Scarica e pagina «Novità» generata da CHANGELOG.md.

### Avviso di sforamento nell'editor
Se le durate dello schema superano la durata dell'adunanza impostata, l'editor lo dice chiaramente e di quanto:
«Lo schema dura 108 minuti: 3 minuti oltre, finirà alle 20:48». Aggiornato mentre si modificano le parti; anche un piccolo segno
nel controller sulla settimana interessata.

### Taratura automatica dell'avvio con la voce
Pulsante «Taratura» nella scheda Voce: 5 secondi di sala in silenzio, poi qualche secondo di voce al microfono;
la soglia viene impostata a metà strada tra i due livelli (con margine). Si rifà in un attimo se cambia l'impianto.

### Segnala un problema o lascia un suggerimento
Pulsante nel programma (e link nella guida) che prepara una segnalazione con versione, sistema, impostazioni principali
(senza PIN) ed errori recenti da `diagnostica\errori.log`, e apre la pagina per inviarla su GitHub. Due modalità:
«Qualcosa non funziona» e «Ho un'idea».

### Modalità libera (eventi fuori programma)
Per i rari eventi in sala oltre alle adunanze (discorso di matrimonio o funerale, adunanza per il servizio, ecc.).
Usata poche volte l'anno: niente in primo piano, solo una voce in un menu.
- Chiede titolo e ora d'inizio, poi quante parti ci sono (di solito una) e quanto dura ciascuna.
- Countdown, schermo e telefono funzionano come sempre; lo schema della settimana non viene toccato.
- Gli ultimi eventi restano tra i suggerimenti per riusarli.

### Controller: spazio a ciò che serve durante l'adunanza
- **Riquadro della settimana compatto**: a timer avviato si riduce a una riga («Mer 8 ott · Infrasettimanale · fine prevista 20:45»)
  e si riapre con un clic; lo spazio guadagnato va all'elenco delle parti.
- **L'elenco segue la parte in corso**: a ogni cambio scorre per tenere visibili la parte attuale e la successiva.
- **Modifica rapida**: doppio clic su una parte nell'elenco per cambiarne durata o titolo senza aprire l'editor
  (che resta per riordinare e aggiungere parti).

### Impostazioni riorganizzate (nuovo design)
Mockup approvato: [Schermo](docs/roadmap/impostazioni-schermo.png) · [Messaggi](docs/roadmap/impostazioni-messaggi.png) · [Adunanze](docs/roadmap/impostazioni-adunanze.png). Stessi colori del programma.
- **Barra laterale con icone** al posto delle schede, sette sezioni: Adunanze · Schermo · Countdown · Messaggi · Voce (beta) ·
  Rete e telefono · Programma. Il countdown (minuti, stile, Prova) finalmente tutto in un posto; «sempre in primo piano»,
  cartella dati e versione in Programma (dove andranno anche avvio con Windows, mini automatica, tema chiaro, backup, segnalazioni).
- **Modifiche applicate subito**: in fondo solo «Fatto» e «Annulla le modifiche» (al posto di Applica / Annulla / Salva).
- **Controlli adatti al dato**: interruttori per le funzioni (messaggi, voce, server web, controllo remoto), selettori d'orario,
  numeri con − / +, scelte a pulsanti (tema Scuro/Chiaro, layout con miniature). Niente più errori al salvataggio.
- **Schermo**: anteprima dal vivo a destra; tabella «Cosa mostrare» con colonne *Durante* e *A riposo* al posto delle caselle sparse.
- **Messaggi**: interruttore generale in cima; frasi pronte come elenco numerato da riordinare trascinando, con ✕ e «Aggiungi».
- **Adunanze**: giorno + orario per adunanza, durata con la fine prevista calcolata subito, studi adattivi con interruttore.
- Spiegazioni in una riga sotto l'opzione, dettagli in una «i»; opzioni rare dietro «Mostra opzioni avanzate».

### Avvio con Windows
Opzione per avviare TimerSala all'accensione del PC, con lo schermo della sala già acceso e lo schema della settimana scaricato.

### Passaggio automatico alla modalità mini
Opzione: quando si avvia la prima parte dell'adunanza, il controller passa da solo alla modalità mini (sempre in primo piano),
così resta visibile sopra JW Library e Zoom. Si torna al controller completo come oggi (tasto M o pulsante).

## Scartate (e perché)
- Ritmo dello studio a paragrafi: il conduttore può dedicare più tempo ai paragrafi più utili alla congregazione.
- Allarme «microfono chiuso»: c'è già un fratello dedicato all'audio e il riscontro è evidente.
- Fine parte dalla voce: durante una parte ci sono pause lunghe (per esempio per leggere una scrittura).
- Riconoscimento automatico da JW Library e passaggio automatico delle parti: serve sempre una persona.
- Pausa del timer.
- Versione per altri sistemi operativi: i PC dell'acustica restano su Windows per JW Library.
