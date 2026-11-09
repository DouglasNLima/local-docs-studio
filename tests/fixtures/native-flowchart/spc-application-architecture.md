# 4. Power Platform Application Architecture

**SPC | Logical components and integration boundaries**

```mermaid
flowchart TB
    Staff["SEAI Administrators<br/><small>Internal staff</small>"]
    Identity["Microsoft Entra ID<br/><small>Internal authentication</small>"]

    subgraph SPC["POWER PLATFORM — SPC Application"]
        direction TB

        App["Model-driven Power App<br/><small>Forms, Views & BPF</small>"]
        PCF["Custom Pages & PCF<br/><small>Workflow Designer & specialised UI</small>"]
        DV["Microsoft Dataverse<br/><small>Projects, Workflows, IAM & Timeline</small>"]
        Logic[".NET Plug-ins & Custom APIs<br/><small>Rules, validation & workflow operations</small>"]
        Automation["Power Automate<br/><small>Notifications & asynchronous processes</small>"]

        App -->|Extends| PCF
        App <--> DV
        PCF <--> DV
        DV <--> Logic
        DV --> Automation
    end

    Portal["SPC Portal<br/><small>.NET API & Worker</small>"]
    Bus["Azure Service Bus<br/><small>Business event messaging</small>"]

    Staff --> App
    Identity -.->|Authentication| App
    DV <-->|Dataverse Web API| Portal
    DV -->|Business events| Bus

    classDef users fill:#0C4A7F,color:#FFFFFF,stroke:#0C4A7F,stroke-width:1px;
    classDef identity fill:#176361,color:#FFFFFF,stroke:#176361,stroke-width:1px;
    classDef portal fill:#175B80,color:#FFFFFF,stroke:#175B80,stroke-width:1px;
    classDef bus fill:#2C5874,color:#FFFFFF,stroke:#2C5874,stroke-width:1px;
    classDef interface fill:#2A80C4,color:#FFFFFF,stroke:#2A80C4,stroke-width:1px;
    classDef data fill:#1168BD,color:#FFFFFF,stroke:#1168BD,stroke-width:1px;
    classDef logic fill:#245F89,color:#FFFFFF,stroke:#245F89,stroke-width:1px;
    classDef automation fill:#248580,color:#FFFFFF,stroke:#248580,stroke-width:1px;

    class Staff users;
    class Identity identity;
    class Portal portal;
    class Bus bus;
    class App,PCF interface;
    class DV data;
    class Logic logic;
    class Automation automation;
    style SPC fill:#F5F9FD,stroke:#8BA8BD,stroke-width:2px,stroke-dasharray:6 4,color:#143A59;
```

> **Workflow clocks and SLA timers:** The execution strategy is specified separately from this logical view.

*SPC · SEAI · Application architecture · Logical view*
