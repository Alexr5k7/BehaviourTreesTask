# 🎭 BTree — Simulación de Robo con IA

> Proyecto Unity que combina **Behaviour Trees**, **Finite State Machine**, **NavMesh (A\*)** y **Waypoints** para simular un ladrón autónomo que planea y ejecuta un robo en una galería de arte.

---

## 🕹️ Cómo se juega

El ladrón comienza esperando en WP4, junto a la furgoneta. El jugador elige **qué objeto robar** mediante teclado o botones de UI. A partir de ahí, el ladrón actúa de forma completamente autónoma: entra a la galería, roba el objeto y vuelve a la furgoneta.

Una vez de vuelta, **el jugador puede mandar al ladrón a por otro objeto**. Esto se repite tantas veces como se quiera hasta haber robado los 9 objetos (8 cuadros + 1 gema). El objetivo es vaciar la galería completamente.

### Controles

| Acción | Teclado | UI |
|---|---|---|
| Ir a por la **Gema** | `0` | Botón "Diamante" |
| Ir a por la **Mona Lisa** | `1` | Botón "Mona Lisa" |
| Ir a por el **Gothic** | `2` | Botón "Gothic" |
| ... | ... | ... |
| Ir a por el **Cafe** | `8` | Botón "Cafe" |

> ⚠️ Solo se puede dar una orden cuando el ladrón está esperando en WP4. Si el ladrón ya está en misión, la orden se ignora.

> ⚠️ Si intentas seleccionar un objeto **ya robado**, aparecerá un aviso en rojo en la parte inferior de la pantalla indicando su nombre.

---

## 🔁 Ciclo de juego

```
Jugador elige objetivo
         ↓
  Ladrón entra por FrontDoor
         ↓
  Roba el objeto
         ↓
  Escapa por BackDoor
         ↓
  Llega a la furgoneta  ──→  ¿Quedan objetos? ──→ SÍ → Ladrón espera en WP4
         ↓                                                        ↑
       NO                                          Jugador elige otro objetivo
         ↓
  Panel de VICTORIA
```

El ladrón **nunca puede llevar dos objetos a la vez**. Cada misión es un objeto. El jugador decide el orden.

---

## 🤖 Comportamiento del ladrón (flujo detallado)

1. **Idle** — Espera en WP4 junto a la furgoneta a que el jugador elija un objetivo.
2. **Entrar a la galería** — Se dirige a la FrontDoor a **velocidad exterior**. Al llegar, la puerta **desaparece**. Entra y avanza hasta WP1 (interior). La FrontDoor **reaparece 2 segundos** después.
3. **Robar el objetivo** — Se mueve hacia el objeto a **velocidad de sigilo**. Al estar suficientemente cerca (incluso si el objeto está elevado en un estante), lo recoge y desaparece.
4. **Escapar** — Cambia a **velocidad de escape** y sigue la ruta `BackDoor → WP2 → WP3 → Furgoneta`. La BackDoor desaparece al pasar y reaparece 2 segundos después.
5. **Entrega en la furgoneta** — Al llegar, el objeto se marca como robado (verde en la lista de UI) y el ladrón se dirige a WP4 a **velocidad exterior**.
6. **Espera nueva orden** — Vuelve al estado Idle. El jugador puede elegir el siguiente objetivo.
7. **Victoria** — Cuando el ladrón entrega el **último objeto** en la furgoneta, aparece el panel de victoria y el juego se pausa.

---

## 🖥️ Interfaz de usuario (UI)

La UI muestra en todo momento el estado del robo:

| Elemento | Descripción |
|---|---|
| **Texto "Objetivo: X"** | Esquina inferior derecha. Muestra el nombre del objeto actual. Cuando está en espera muestra "Objetivo: ninguno". |
| **Lista de objetos** | Muestra los 9 objetos de la galería. En **rojo** los que siguen en la galería, en **verde** los ya entregados en la furgoneta. Cambia a verde en el momento exacto en que el ladrón llega a la van, no al recogerlo. |
| **Aviso "ya robado"** | Texto rojo en la parte inferior. Aparece 2 segundos si se intenta seleccionar un objeto ya entregado, mostrando su nombre exacto. |
| **Panel de victoria** | Panel central que aparece al completar el robo. Pausa el juego y muestra un botón para reiniciar la escena. |

---

## 🏆 Panel de victoria

Aparece cuando el ladrón entrega el **último objeto en la furgoneta** (no al recogerlo). El juego se pausa (`Time.timeScale = 0`) y aparece un botón **"Volver a empezar"** que recarga la escena completa desde cero.

---

## 🗺️ Waypoints

| Waypoint | Uso |
|---|---|
| **WP1** | Punto de entrada interior de la galería |
| **WP2** | Primera parada de la ruta de escape |
| **WP3** | Segunda parada de la ruta de escape |
| **WP4** | Punto de espera junto a la furgoneta (Idle) |

---

## 🧠 Arquitectura técnica

El proyecto integra cuatro técnicas de IA para videojuegos:

```
Input del jugador
       ↓
Finite State Machine  ←──────────────────────┐
       ↓                                      │
Behaviour Trees  (StealTarget)                │
       ↓                                      │
NavMesh Agent — A* (Unity built-in)           │
       ↓                                      │
  Escena (puertas, objetos, waypoints)        │
       ↓                                      │
  LlegarAVan() ───────────────────────────────┘
```

### Finite State Machine (FSM)

Controla el estado general del ladrón. Cada estado implementa `IState` con `OnEnter`, `OnUpdate` y `OnExit`. Las transiciones se disparan mediante flags booleanos que cada estado activa al completarse. Cada estado limpia su propio flag en `OnExit` para evitar estados sucios entre ciclos.

| Estado | Descripción | Flag de salida |
|---|---|---|
| `Idle` | Esperando orden del jugador en WP4 | `HasOrder = true` |
| `EnteringGallery` | Va a FrontDoor, la destruye y entra a WP1 | `EnteredGallery = true` |
| `StealingTarget` | Se acerca al objetivo y lo recoge | `TargetStolen = true` |
| `Escaping` | Sigue la ruta BackDoor → WP2 → WP3 → Van → WP4 | `Escaped = true` |
| `Done` | Fin del juego, todos los objetos entregados | — |

La transición desde `Escaping` es doble:
- `Escaped = true` + quedan objetos → **Idle** (nueva misión disponible)
- `Escaped = true` + no quedan objetos → **Done** (victoria)

### Behaviour Trees (BT)

Estructuran la lógica de robo en `StealingTarget`. Cada árbol se compone de `BehaviourTree`, `Sequence` y `Leaf` con estrategias `IStrategy`.

```
StealTarget BT
  └─ Sequence
       ├─ Leaf: MoveToTarget   (se acerca al objeto en XZ, ignorando altura)
       └─ Leaf: Steal          (desactiva el GameObject y lo registra como robado)
```

Los estados `EnteringGallery` y `Escaping` controlan el `NavMeshAgent` directamente frame a frame (sin BT) para mayor fiabilidad en rutas con múltiples puntos.

### NavMesh + A\*

Unity calcula automáticamente la ruta óptima (A\*) mediante `NavMeshAgent.SetDestination()`. El agente siempre toma el camino más corto disponible. La rotación se gestiona manualmente con `Quaternion.Slerp` sobre `Agent.velocity` para evitar el efecto de patinaje en curvas a alta velocidad.

### Waypoints

Cuatro `Transform` asignados en el inspector que definen los puntos clave del recorrido. WP1 marca la entrada interior, WP2 y WP3 la ruta de escape, y WP4 el punto de espera exterior.

---

## 📁 Scripts

| Script | Responsabilidad |
|---|---|
| `RobberController.cs` | MonoBehaviour principal. Gestiona flags, velocidades, puertas, registro de objetos robados, input de teclado, construcción de la FSM y los Behaviour Trees. |
| `StateMachine.cs` | FSM genérica reutilizable. Gestiona estados `IState` y transiciones con condiciones `Func<bool>`. Soporta transiciones por estado y transiciones globales (`AddAnyTransition`). |
| `RobberStates.cs` | Implementación de los cinco estados: `IdleState`, `EnteringGalleryState`, `StealingTargetState`, `EscapingState` y `DoneState`. |
| `Node.cs` | Clases base del Behaviour Tree: `Node`, `BehaviourTree`, `Sequence`, `Selector`, `Leaf`, `Inverter`, `PrioritySelector`, `UntilFail`. |
| `Strategies.cs` | Estrategias `IStrategy` usadas en los `Leaf`: `ActionStrategy`, `Condition`, `PatrolStrategy`, `MoveToTarget`, `MoveToPosition`, `WaitStrategy`. |
| `RobberUI.cs` | Gestiona toda la UI: texto de objetivo actual, lista de objetos con colores, aviso de objeto ya robado y panel de victoria con reinicio de escena. |

---

## ⚙️ Parámetros configurables (Inspector)

Todos los valores son ajustables desde el inspector de `RobberController` sin tocar código.

| Parámetro | Descripción | Valor por defecto |
|---|---|---|
| `Arrival Threshold` | Distancia mínima para considerar que llegó a un punto | `1.5` |
| `Outside Speed` | Velocidad en el exterior (entrada desde WP4 y vuelta) | `4` |
| `Stealth Speed` | Velocidad dentro de la galería yendo al objetivo | `3` |
| `Escape Speed` | Velocidad huyendo tras el robo | `7` |
| `Steal Reach` | Radio de agarre en XZ para recoger objetos elevados | `2.5` |
| `Angular Speed` | Velocidad máxima de giro (evita patinar en curvas) | `180` |
| `Acceleration` | Aceleración y frenada del agente | `12` |

---

## 🔧 Setup de escena

1. El GameObject del ladrón necesita `RobberController` y `NavMeshAgent`.
2. Asignar en el inspector de `RobberController`: `FrontDoor`, `BackDoor`, `VanTransform`, `Gem`, lista de `Paintings` (8 cuadros) y lista de `Waypoints` (WP1 → WP4 en orden).
3. La escena debe tener el **NavMesh horneado** (`Window → AI → Navigation → Bake`).
4. Para la UI: crear un **Canvas** (Screen Space - Overlay) con el componente `RobberUI` y asignar:
   - `Warning Text` → `Text (TMP)` en la parte inferior, color rojo
   - `Objetivo Text` → `Text (TMP)` en la esquina inferior derecha
   - `Lista Container` → GameObject con `Vertical Layout Group` para la lista de objetos
   - `Color Pendiente` / `Color Robado` → colores de la lista (rojo/verde por defecto)
   - `Victory Panel` → Panel con imagen oscura, oculto al inicio
   - `Victory Button` → Botón dentro del panel de victoria
