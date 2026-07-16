# **📋 Relazione Tecnica: Architettura Ecosistema IoT & Telemetria**

## **1\. Panoramica del Sistema**

L'architettura attuale implementa un sistema di gestione e visualizzazione della telemetria IoT basato su un paradigma **Serverless e orientato agli eventi (Event-Driven)** su piattaforma Microsoft Azure. Il sistema è progettato per disaccoppiare completamente la fase di ingestione massiva dei dati inviati dai dispositivi sul campo dalla fase di consultazione e visualizzazione da parte dell'utente finale.

## **2\. Flussi Dati e Componenti Architetturali**

Il flusso si divide nettamente in due direttrici indipendenti (Scrittura Asincrona e Lettura On-Demand):

 BINARIO 1: SCRITTURA (Ingestione dati)  
 \[ DISPOSITIVI \] ──(MQTT)──► \[ Azure IoT Hub \] ──► \[ Coda \] ──► \[ Azure Function \] ──► \[ Cosmos DB \]  
                                                                     (SalvaMisureIot)       (Misure)  
                                                                                                ▲  
 BINARIO 2: LETTURA (Visualizzazione)                                                           │  
 \[ gigi-iot-frontend2 \] ──────────(Chiamata HTTP FETCH)──────────► \[ Azure Function HTTP \] ─────┘  
                             \[CORS Blindato sull'URL del sito\]          (GetMisure)

### **Descrizione dei Componenti:**

1. **Strato di Ingestione (Azure IoT Hub):** Riceve la telemetria dai dispositivi tramite protocollo **MQTT**. Agisce come un buffer (ammortizzatore di carico). Se il backend o il database sono offline, l'Hub conserva i dati in una coda interna (ritenzione di default: 24 ore) evitando qualsiasi perdita di dati sul campo.  
2. **Strato di Elaborazione (Azure Function \- Modello .NET Isolated):** Il backend esegue la logica in un processo isolato dall'host di Azure (migliore stabilità e gestione delle dipendenze).  
   * La funzione SalvaMisureIot si attiva tramite \[EventHubTrigger\], processa i messaggi in blocco (batch) per massimizzare le performance e implementa la clonazione degli oggetti JSON per evitare corruzioni di memoria.  
3. **Strato di Persistenza (Azure Cosmos DB):** Configurato in modalità **Serverless** (paga solo per le effettive letture/scritture). Memorizza i file JSON nativi nel container Misure garantendo tempi di risposta inferiori ai 10ms.  
4. **Strato di API & Interfaccia (Azure Function HTTP):** Espone un endpoint REST (\[HttpTrigger\]) chiamato GetMisure che fa da ponte sicuro verso il database. Quando interrogato, esegue una query su Cosmos DB e restituisce i dati formattati in JSON.  
5. **Strato di Visualizzazione (gigi-iot-frontend2):** L'applicazione frontend che interroga direttamente il backend tramite API standard del browser (fetch) per renderizzare grafici e tabelle in base alla telemetria memorizzata.

## **3\. Punti di Forza dell'Infrastruttura Attuale**

* **Disaccoppiamento Totale:** I sensori possono trasmettere dati in MQTT anche se il backend C\# o il frontend sono spenti o in manutenzione.  
* **Efficienza dei Costi:** Utilizzando Azure Functions (Flex Consumption) e Cosmos DB Serverless, l'infrastruttura ha un costo fisso quasi nullo; la fatturazione scala linearmente solo in base ai messaggi ricevuti.  
* **CORS Blindato e Sicuro:** La comunicazione tra Frontend e Backend è già protetta a livello di browser. Non sono utilizzate wildcard (\*); le chiamate HTTP fetch sono accettate ed elaborate dal backend **esclusivamente se provengono dall'URL ufficiale e autorizzato di gigi-iot-frontend2**.

## **4\. Limiti Attuali e Debito Tecnico (Roadmap Prossimi Sprint)**

Per l'allineamento del team, prima del rilascio in produzione reale (Go-Live), è necessario pianificare i seguenti interventi correttivi:

* **Gestione della Sicurezza (Hardcoded Keys):** Le stringhe di connessione contenenti le password e le chiavi dei servizi di Azure sono attualmente scritte in chiaro nei file di configurazione (local.settings.json).  
  * *Soluzione:* Configurare le **Managed Identities (Identità Gestite)** su Azure ed eliminare del tutto le password dal codice.  
* **Esposizione di Rete (Firewall Pubblici):** Cosmos DB, IoT Hub e lo Storage Account accettano connessioni da qualsiasi IP pubblico su Internet.  
  * *Soluzione:* Sfruttare la funzionalità di **VNet Integration** nativa del piano Flex Consumption per chiudere le risorse dietro una rete privata virtuale, rendendole invisibili all'esterno.  
* **Mancanza di Ambiente di Stage:** Attualmente i test in locale (F5) puntano alle stesse risorse cloud (Hub e DB) utilizzate dall'applicazione. Sarà necessario isolare un Resource Group di "Sviluppo/Test" da quello di "Produzione".