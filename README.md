# TimerSala

Timer per le adunanze di congregazione, per Windows 10/11. Fa quello che fa OnlyT, con alcune aggiunte:

- **Schemi da wol.jw.org**: scarica automaticamente l'adunanza infrasettimanale (parti, durate, cantici) e l'articolo di studio della Torre di Guardia della settimana.
- **Controller compatto** per lo schermo dell'acustica: una finestra stretta con tutto a portata di mano.
- **Schermo del timer selezionabile**: a tutto schermo sul monitor che scegli (di solito il terzo), leggibile da lontano.
- **Adunanze modificabili**: editor per cambiare titoli, durate, ordine; pulsante **Sorvegliante** per la settimana della visita.
- **Server web integrato**: il timer si può vedere da telefoni, tablet o altri PC della rete.

## Installazione

1. Apri la pagina **Actions** (o **Releases**) del repository e scarica `TimerSala.exe`.
2. Copialo dove vuoi (es. `C:\TimerSala`) e avvialo. Non serve installare .NET: è tutto dentro l'eseguibile.
3. Al primo avvio Windows chiede se consentire l'accesso alla rete a TimerSala: **consenti sulle reti private**. Serve al server web.

> Se Windows SmartScreen mostra "PC protetto", fai clic su *Ulteriori informazioni → Esegui comunque* (l'eseguibile non è firmato digitalmente).

## Uso

### Controller (schermo dell'acustica)

| Comando | Tasto |
|---|---|
| Avvia / ferma la parte selezionata | `Spazio` o `Invio` |
| Parte successiva / precedente | `→` `↓` / `←` `↑` |
| Aggiungi / togli un minuto | `+` / `−` |
| Modifica lo schema | `E` |
| Modalità mini / completa | `M` |

- Fai clic su una parte dell'elenco per selezionarla. Quando fermi il timer, il tempo effettivo viene registrato (verde in orario, rosso se sforato) e si passa alla parte successiva.
- Se fermi una parte per errore, riavviala: il conteggio riprende da dove era. Per azzerarla usa il tasto destro sulla parte.
- In alto vedi l'ora e il **ritardo/anticipo** accumulato sull'adunanza.
- `◀ ▶` cambiano settimana; clic sulla data per tornare alla settimana corrente; il pulsante di download riscarica lo schema da wol.jw.org.

### Orari e countdown prima dell'inizio

Nelle impostazioni (scheda *Adunanze*) indichi giorno e ora di inizio dell'infrasettimanale e del fine settimana. All'avvio TimerSala propone l'adunanza di oggi (o la prossima) e, qualche minuto prima dell'inizio (5 per impostazione), lo schermo mostra **"L'adunanza inizia tra 04:59"** con la prima parte. Il countdown sparisce all'orario di inizio o appena avvii la prima parte. Nel controller trovi anche l'orario di inizio e la **fine prevista** (che tiene conto del ritardo).

### Modalità mini

Con un solo monitor la finestra completa può essere ingombrante: il pulsante in alto (o il tasto `M`) la riduce a una piccola finestra sempre in primo piano con il timer, il titolo della parte, avvia/ferma, parte precedente/successiva e ±1 min. Si sposta trascinandola dalla barra in alto; il pulsante in alto a destra riapre la finestra completa. TimerSala ricorda la modalità e la posizione.

### Schermo del timer

In Impostazioni → *Schermo* scegli il **monitor** del timer (*Identifica* mostra il numero su ogni schermo) e spunta **Mostra**. La scelta viene ricordata.

- Verde: tempo regolare. Giallo: ultimo minuto (configurabile). Rosso con `+`: tempo sforato, lo sfondo diventa rosso scuro.
- Quando il timer è fermo mostra l'orologio e la prossima parte.
- Stile personalizzabile (Impostazioni → *Schermo*): tema scuro o chiaro, carattere a scelta, layout **Classico**, **Clessidra orizzontale** (lo sfondo colorato si accorcia verso sinistra man mano che il tempo passa) o **Solo cifre**; puoi nascondere titolo, sezione, barra, parte successiva, ora; cifre colorate o neutre; lampeggio allo scadere. Con *Applica* vedi subito l'effetto senza chiudere le impostazioni. La pagina web usa lo stesso stile.
- Le cifre occupano circa il 70% dell'altezza dello schermo: su un monitor da 22" (Full HD) sono alte circa 10 cm, ben leggibili a 20 metri.

### Visita del sorvegliante

Attiva **Sorvegliante**: nell'infrasettimanale lo studio biblico di congregazione viene sostituito dai commenti conclusivi + discorso di servizio (30 min); nel fine settimana lo studio Torre di Guardia passa a 30 min ed è seguito dal discorso di servizio (30 min). Puoi comunque ritoccare tutto con l'editor. Disattivandolo si torna allo schema scaricato.

### Modificare un'adunanza

Pulsante matita (o tasto `E`). A sinistra l'elenco delle parti: trascinale per cambiarne l'ordine (o usa ↑↓, `Alt+↑/↓`), `Canc` elimina; *Parte* e *Cantico* ne aggiungono una dopo quella selezionata. A destra modifichi la parte selezionata: titolo, tipo (parte cronometrata o cantico), durata con −/+ o con i pulsanti rapidi, sezione (pulsanti colorati) e note. In alto vedi il totale dei minuti. Le modifiche valgono per quella settimana e vengono salvate con *Salva*. *Ripristina scaricato* torna allo schema di wol.jw.org.

### Messaggi all'oratore

Sotto i pulsanti del timer c'è la riga dei messaggi: scegli una frase pronta dall'elenco (o scrivine una) e premi `Invio` o il pulsante di invio. Il messaggio compare in una fascia gialla grande in basso sullo schermo del timer (e nella pagina web) per 15 secondi, oppure finché non lo togli con ✕. Le frasi pronte e la durata si cambiano in Impostazioni → *Messaggi*, dove c'è anche l'interruttore generale: se i messaggi non ti servono, disattivali e spariscono dal controller, dalla modalità mini e dalla pagina web. La riga dei messaggi c'è anche nella modalità mini.

### Timer in rete

In basso nel controller è indicato l'indirizzo (es. `192.168.1.20:8090`; clic per copiarlo, doppio clic per aprirlo). TimerSala usa la scheda di rete principale e ignora quelle virtuali (VirtualBox, Hyper-V, VPN…); se serve, in Impostazioni → *Rete* puoi scegliere un altro indirizzo o il nome del PC. Aprilo da qualsiasi dispositivo collegato alla stessa rete: vedrai il timer in tempo reale. *Programma* mostra l'elenco delle parti con i tempi effettivi; *Schermo intero* nasconde la barra del browser. Porta e attivazione si cambiano nelle impostazioni.

Il pulsante **Collega telefono** accanto all'indirizzo mostra due codici da inquadrare con il telefono:

- **Vedi il timer** — apre la pagina in sola visualizzazione.
- **Controlla il timer** — apre la pagina con il pannello di controllo: il timer resta visibile (sopra sul telefono, a sinistra su tablet e PC) e accanto ci sono avvia/ferma, parte precedente/successiva, ±1 min e due schede: *Programma* (tocca una parte per selezionarla) e *Messaggi* (frasi pronte, testo libero, messaggio attualmente sullo schermo). Questo codice è nascosto finché non premi *Mostra codice di controllo*, perché contiene già il PIN; chi apre l'indirizzo a mano tocca *Controllo* e inserisce il PIN.

Il PIN (generato al primo avvio) si vede e si cambia in Impostazioni → *Rete*, dove si può anche disattivare il controllo remoto. Dopo 5 PIN errati il controllo si blocca per un minuto.

## Impostazioni e dati

Impostazioni (ingranaggio), divise in schede: *Adunanze* (orari, durata, countdown, lingua della guida), *Timer* (preavviso giallo, controller in primo piano), *Schermo* (stile del timer), *Messaggi* (frasi pronte e durata) e *Rete* (server web, porta, controllo remoto e PIN).

I dati sono in `%AppData%\TimerSala`:

- `impostazioni.json`
- `settimane\AAAA-MM-GG.json` — uno schema per settimana (lunedì)
- `diagnostica\` — copia dell'ultima pagina scaricata da wol.jw.org

Se un giorno il download non funziona più (il sito può cambiare struttura), lo schema predefinito resta utilizzabile e modificabile; i file in `diagnostica` servono a correggere il programma.

## Sviluppo

Soluzione .NET 10:

- `src/TimerSala.Core` — modelli, lettura di wol.jw.org (AngleSharp), motore del timer, server web (Kestrel)
- `src/TimerSala.App` — interfaccia WPF
- `tests/TimerSala.Tests` — test xUnit

```
dotnet test tests/TimerSala.Tests
dotnet publish src/TimerSala.App -c Release -r win-x64 -o publish
```

La GitHub Action `Build` esegue i test e produce `TimerSala.exe` a ogni push. Per pubblicare una release: Actions → Build → *Run workflow* indicando la versione (es. `v1.1.0`), oppure crea un tag `v…`.
