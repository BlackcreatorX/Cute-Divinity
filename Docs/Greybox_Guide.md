# Tales of Water, Falling Tears — Guía de greyboxing (prototipo de 3 puzzles)

Unity **6000.3.7f1 / 6000.3.10f1** · URP 17.3 · Input System 1.18 · solo primitivas nativas.
Tiempo objetivo: **menos de 30 minutos** siguiendo los pasos en orden.

> Todas las posiciones están en **coordenadas de mundo**. Si un objeto es hijo de otro, la posición se da como **local** y se indica.
> Regla de oro: **todo objeto puramente visual (ojos del personaje, orbes, medidores, marcas, agua) va SIN collider.** Selecciónalo → en el componente *Collider* → ⋮ → **Remove Component**.

---

## 0. Scripts (ya en el proyecto)

| Archivo | Ruta |
|---|---|
| `PlayerPressureController.cs` | `Assets/Scripts/Player/` |
| `SimpleFollowCamera.cs` | `Assets/Scripts/Player/` |
| `CosmicEyeWatcher.cs` | `Assets/Scripts/AI/` |
| `Puzzle1_SteppingStones.cs` | `Assets/Scripts/Puzzles/` |
| `Puzzle2_RitualPillars.cs` | `Assets/Scripts/Puzzles/` |
| `Puzzle3_FloodGate.cs` | `Assets/Scripts/Puzzles/` |
| `PrototypeShared.cs` (NoiseBus + ProtoVisuals) | `Assets/Scripts/Manager/` |

Todo está en el namespace `CuteDivinity.Prototype`. El input usa **Input System (nuevo)** con acciones creadas por código: no hace falta ningún asset `.inputactions`.

**Controles**

| Acción | Teclado/Ratón | Gamepad |
|---|---|---|
| Dirección (solo orienta) | WASD / Flechas | Stick izquierdo |
| Botón de presión | Espacio / Click izq. | Botón Sur (A / ✕) |

- **Tap (< 0.25 s):** paso sigiloso. Ruido mínimo, no drena cordura.
- **Hold (≥ 0.25 s):** carrera. Mucho ruido, drena cordura.
- **Dentro de un pilar/altar y sin pulsar dirección:** la pulsación se convierte en **bombeo** (el personaje no se mueve).

---

## 1. Configuración del proyecto (3 min)

1. **Input:** *Edit → Project Settings → Player → Other Settings → Active Input Handling* = **Input System Package (New)** (ya está así en este proyecto). "Both" también funciona.
2. **Capas:** *Edit → Project Settings → Tags and Layers*:
   - User Layer 6 = **`Player`**
   - User Layer 7 = **`Cover`**
   - (La capa `Water`, número 4, ya existe.)
3. **Sombras de luces adicionales** (para que el cono del ojo se vea cortado por las columnas): selecciona el URP Asset en uso (*Project Settings → Graphics → Default Render Pipeline*, normalmente `Assets/Settings/PC_RPAsset`) → **Lighting → Additional Lights = Per Pixel** y **Cast Shadows ✔**.
4. **Escena nueva:** *File → New Scene → Basic (URP)* → guardar como `Assets/Scenes/Greybox_Puzzles.unity`.
5. **Ambiente oscuro:** selecciona la *Directional Light* → Intensity **0.3**, color azul grisáceo (`#6C7A99`). *Window → Rendering → Lighting → Environment* → Environment Lighting Source = **Color**, Ambient Color gris oscuro (`#1E1E28`). La niebla la controla el script del jugador.

## 2. Materiales (3 min)

`Assets/Art/Materials/` → crea una carpeta **`Greybox`** → *Create → Material* (shader **Universal Render Pipeline/Lit**). Los scripts tiñen por código (MaterialPropertyBlock), así que el color base es solo el de partida.

| Material | Base Color | Notas |
|---|---|---|
| `M_Floor` | gris oscuro `#3A3A44` | |
| `M_Wall` | gris violáceo `#4A4458` | |
| `M_Cover` | gris piedra `#6E6A60` | columnas / muros de cobertura |
| `M_Stone` | gris claro `#8C8C99` | piedras del puzzle 1 |
| `M_Water` | azul `#1F4FA0`, alfa 180 | **Surface Type = Transparent** |
| `M_Player` | rosa `#FF9ED1` | |
| `M_Black` | negro | ojitos del personaje, pupila |
| `M_Eye` | blanco | globo del ojo cósmico |
| `M_Meter` | azul `#3373FF` | medidores de los pilares |
| `M_Marker` | amarillo `#FFD926` | marcas objetivo |
| `M_Orb` | gris `#26262E` | orbes de carga |
| `M_Door` | morado `#59406E` | puerta / compuerta |
| `M_Glow` | blanco | marca de meta, indicador de carga |

> Para asignar: arrastra el material desde el Project al objeto en la Scene o al slot *Materials* del Mesh Renderer.

---

## 3. Jugador (4 min)

1. *GameObject → 3D Object → **Capsule*** → nombre **`Player`**. Position **(0, 1.1, 0)**. Material `M_Player`.
2. **Quita el `Capsule Collider`** (el CharacterController ya es un collider).
3. *Add Component → **Character Controller***: Center (0,0,0), Radius 0.5, Height 2, Step Offset 0.3, Slope Limit 45.
4. **Tag = `Player`**, **Layer = `Player`** (responde "Yes, change children" cuando lo pregunte, después de crear los hijos o vuelve a aplicarlo al final).
5. Hijos (todos **sin collider**):
   - **`EyeL`** Sphere · local (-0.2, 0.45, 0.42) · scale (0.15, 0.15, 0.15) · `M_Black`
   - **`EyeR`** Sphere · local (0.2, 0.45, 0.42) · scale (0.15, 0.15, 0.15) · `M_Black`
   - **`ChargeOrb`** Sphere · local (0, 1.4, 0) · scale (0.01, 0.01, 0.01) · `M_Glow` (crece al pulsar: cian = Tap, naranja = Hold)
   - **`WakeDisc`** Cylinder · local (0, -0.97, 0) · scale (0.01, 0.01, 0.01) · `M_Water` (radio = ruido/oleaje)
6. *Add Component → **Player Pressure Controller***:

| Campo | Valor |
|---|---|
| Hold Threshold | 0.25 |
| Camera Transform | *(vacío → usa Main Camera)* |
| Checkpoint | `CP_Start` (paso 4) |
| Kill Y | **-1.5** |
| Body Renderer | el **Mesh Renderer** del propio `Player` |
| Charge Indicator / Charge Renderer | `ChargeOrb` (Transform / Mesh Renderer) |
| Wake Disc | `WakeDisc` |
| Drive Fog | ✔ |

7. **Main Camera** → *Add Component → **Simple Follow Camera*** → Target = `Player`, Offset (0, 9, -7). Position inicial (0, 10, -7).

## 4. Checkpoints (1 min)

*GameObject → Create Empty*, con rotación (0,0,0):

| Nombre | Position |
|---|---|
| `CP_Start` | (0, 1.1, 0) |
| `CP_P2` | (0, 1.1, 21.5) |
| `CP_P3` | (0, 1.1, 42) |

---

## 5. Nivel base (3 min)

Todo **Cube** con `Box Collider` (por defecto). Crea un vacío **`Level`** en (0,0,0) como carpeta.

| Nombre | Position | Scale | Material |
|---|---|---|---|
| `Floor_Start` | (0, -0.5, 0) | (8, 1, 8) | M_Floor |
| `Floor_P2` | (0, -0.5, 29.8) | (14, 1, 20.4) | M_Floor |
| `Floor_P3` | (0, -0.5, 50) | (20, 1, 20) | M_Floor |
| `Wall_P2_L` | (-4.5, 2, 36) | (5, 4, 1) | M_Wall |
| `Wall_P2_R` | (4.5, 2, 36) | (5, 4, 1) | M_Wall |
| `Wall_P3_Back_L` | (-6, 3, 60.25) | (8, 6, 0.5) | M_Wall |
| `Wall_P3_Back_R` | (6, 3, 60.25) | (8, 6, 0.5) | M_Wall |
| `Wall_P3_Side_L` | (-10, 2, 50) | (0.5, 4, 20) | M_Wall |
| `Wall_P3_Side_R` | (10, 2, 50) | (0.5, 4, 20) | M_Wall |

Entre `Floor_Start` (termina en z = 4) y `Floor_P2` (empieza en z = 19.6) queda el **hueco de agua** del puzzle 1.

---

## 6. Ojo cósmico — prefab reutilizable (3 min)

1. *Create Empty* → **`CosmicEye_A`** · Position **(0, 6, 22)** · Rotation **(15, 180, 0)** (mira hacia los escalones, Z−).
2. Hijos:
   - **`EyeBall`** Sphere · local (0,0,0) · scale (1.6, 1.6, 1.6) · `M_Eye` · **sin collider**
   - **`Pupil`** Sphere, **hijo de `EyeBall`** · local (0, 0, 0.45) · scale (0.4, 0.4, 0.15) · `M_Black` · **sin collider**
   - **`VisionLight`**: *Light → Spot Light*, hijo de `CosmicEye_A` · local (0, 0, 0.9) · rotation (0,0,0) · **Shadow Type = Soft Shadows**
3. *Add Component → **Cosmic Eye Watcher*** en `CosmicEye_A`:

| Campo | Valor |
|---|---|
| Player | `Player` |
| Eye Pivot | `CosmicEye_A` (o vacío = él mismo) |
| Eye Renderer | `EyeBall` |
| Pupil / Pupil Renderer | `Pupil` |
| Vision Light | `VisionLight` |
| Hearing Radius | 25 |
| View Distance | 20 |
| View Angle | 45 |
| **Occlusion Mask** | **Default + Cover** (desmarca Player, Water, Ignore Raycast, UI) |
| Ray Start Offset | 0.9 |
| Periodic Watch | ☐ (desactivado) |

4. Arrastra `CosmicEye_A` a `Assets/Prefabs/` para crear un prefab (lo reutilizaremos en el puzzle 3).

**Lectura visual:** morado y entrecerrado = dormido · amarillo, medio abierto, luz tenue = alerta · rojo, abierto, cono de luz = vigilando · se vuelve blanco a medida que te detecta.

---

## 7. PUZZLE 1 — Piedras sobre el agua (4 min)

**Objetivo de diseño:** cruzar con toques. Correr encima de una piedra la hunde, hace "splash" y despierta al ojo.

1. *Create Empty* → **`Puzzle1`** en (0,0,0).
2. **Agua:** *3D Object → **Plane*** → `P1_Water` · Position (0, -0.4, 11.8) · Scale (0.8, 1, 1.56) · `M_Water` · **Layer = Water** · **quita el Mesh Collider** (si no, el jugador no cae).
3. **Piedras:** 9 × *3D Object → **Cylinder*** (hijos de `Puzzle1`), Scale **(1.6, 0.15, 1.6)**, material `M_Stone`.
   **Importante:** el Cylinder trae un `Capsule Collider` que, aplastado, se convierte en una cúpula. **Quítalo y añade *Mesh Collider* con *Convex* ✔.**

| Nombre | Position |
|---|---|
| `Stone_01` | (0, -0.15, 5.0) |
| `Stone_02` | (0.6, -0.15, 6.7) |
| `Stone_03` | (0, -0.15, 8.4) |
| `Stone_04` | (-0.6, -0.15, 10.1) |
| `Stone_05` | (0, -0.15, 11.8) |
| `Stone_06` | (0.6, -0.15, 13.5) |
| `Stone_07` | (0, -0.15, 15.2) |
| `Stone_08` | (-0.6, -0.15, 16.9) |
| `Stone_09` | (0, -0.15, 18.6) |

   (La cara superior queda a y = 0, al nivel del suelo; huecos de 0.2 m que el CharacterController pisa sin saltar.)
4. **Meta:**
   - `P1_GoalZone` Cube · (0, 1, 21) · scale (4, 2, 2) · **Box Collider → Is Trigger ✔** · **desactiva el Mesh Renderer**.
   - `P1_GoalMarker` Cylinder · (0, 0.02, 21) · scale (1.5, 0.02, 1.5) · `M_Glow` · **sin collider**.
5. En `Puzzle1` → *Add Component → **Puzzle1_Stepping Stones***:

| Campo | Valor |
|---|---|
| Player | `Player` |
| Eye | `CosmicEye_A` |
| Stones (size 9) | `Stone_01` … `Stone_09` (puedes bloquear el Inspector con 🔒 y arrastrar las 9 a la vez sobre el nombre de la lista) |
| Goal Zone | `P1_GoalZone` (Box Collider) |
| Goal Renderer | `P1_GoalMarker` |
| Checkpoint On Solve | `CP_P2` |

**Feedback:** piedra gris = estable · amarilla y temblando = estresada · roja y hundiéndose = perdida (reaparece a los 2.5 s). La marca de meta pulsa y se vuelve verde al llegar.

---

## 8. PUZZLE 2 — Pilares rituales (6 min)

**Objetivo de diseño:** mantener el botón un tiempo corto / medio / largo en cada pilar. Cada intento largo hace ruido que oye `CosmicEye_A`; equivocarse dos veces seguidas suele despertarlo del todo.

1. *Create Empty* → **`Puzzle2`** en (0,0,0).
2. Para **cada pilar** (X = **-4**, **0**, **4**; nombres `Pillar_Short`, `Pillar_Medium`, `Pillar_Long`) crea un vacío `Pillar_X` en **(X, 0, 28)** con estos hijos (posiciones **locales**):

| Hijo | Primitiva | Local Position | Scale | Material | Collider |
|---|---|---|---|---|---|
| `Body` | Cylinder | (0, 1, 0) | (1, 1, 1) | M_Wall | se queda (Capsule) |
| `Orb` | Sphere | (0, 2.4, 0) | (0.6, 0.6, 0.6) | M_Orb | **quitar** |
| `Orb/Light` | Point Light (hijo de `Orb`) | (0,0,0) | — | — | Range 4, Intensity 0 |
| `MeterRoot` | Empty | (0, 0, -0.8) | (1, 1, 1) | — | — |
| `MeterRoot/Fill` | Cylinder | (0, 0.01, 0) | (0.3, 0.01, 0.3) | M_Meter | **quitar** |
| `MeterRoot/Target` | Cube | (0, 0, 0) | (0.5, 0.05, 0.5) | M_Marker | **quitar** |
| `Zone` | Cube | (0, 1, -1.5) | (2, 2, 2) | — | **Is Trigger ✔**, Mesh Renderer **desactivado** |
| `ZonePad` | Cylinder | (0, 0.01, -1.5) | (1.6, 0.01, 1.6) | M_Glow | **quitar** |

   > `MeterRoot` **debe tener escala 1**: es la base del medidor. El script escala `Fill` en Y desde ahí (como un nivel de agua) y coloca `Target` a la altura objetivo automáticamente.
   > Consejo: monta `Pillar_Short` completo, luego **Ctrl+D** dos veces y cambia solo la X.

3. **Puerta:** `P2_Door` Cube · (0, 2, 36) · scale (4, 4, 1) · `M_Door` · Box Collider se queda (bloquea el paso).
4. En `Puzzle2` → *Add Component → **Puzzle2_Ritual Pillars***. La lista **Pillars** ya viene con 3 elementos (0.6 s / 1.5 s / 2.8 s). Rellena cada elemento:

| Campo del elemento | Asignar |
|---|---|
| Zone | `Pillar_X/Zone` (Box Collider) |
| Meter Fill | `Pillar_X/MeterRoot/Fill` |
| Meter Renderer | `Pillar_X/MeterRoot/Fill` |
| Target Marker | `Pillar_X/MeterRoot/Target` |
| Orb Renderer | `Pillar_X/Orb` |
| Orb Light | `Pillar_X/Orb/Light` |
| Target Hold Time | 0.6 (Short) · 1.5 (Medium) · 2.8 (Long) |

   Resto: Player = `Player` · Meter Max Time 3.5 · Tolerance 0.3 · Meter Full Height 2 · Meter Mesh Height **2** (Cylinder) · Door = `P2_Door` · Door Renderer = `P2_Door` · Door Open Offset (0, -4.2, 0) · Checkpoint On Solve = `CP_P3`.

**Cómo se juega:** entra en el disco del pilar, **suelta la dirección**, mantén el botón. El agua del medidor sube; cuando el medidor se pone **amarillo** estás dentro de la ventana → suelta. Verde + orbe encendido = correcto. Rojo = fallo (se vacía y hace ruido). Con los tres verdes la puerta se hunde en el suelo.

---

## 9. PUZZLE 3 — La compuerta inundada (6 min)

**Objetivo de diseño:** mantener en el altar para subir el agua hasta la marca, pero el ojo se abre periódicamente mirando al altar: hay que soltar y esconderse tras una columna antes de que su cono te alcance. El agua baja poco a poco mientras no bombeas.

1. *Create Empty* → **`Puzzle3`** en (0,0,0).
2. **Altar:**
   - `P3_Altar` Cylinder · (0, 0.6, 50) · scale (0.8, 0.6, 0.8) · `M_Wall` · collider se queda.
   - `P3_AltarZone` Cube · (0, 1, 50) · scale (3.5, 2, 3.5) · **Is Trigger ✔** · Mesh Renderer **desactivado**.
3. **Coberturas** (Cylinder, **Layer = Cover**, collider se queda, `M_Cover`):

| Nombre | Primitiva | Position | Scale |
|---|---|---|---|
| `Cover_Column_L` | Cylinder | (-2.5, 2, 48) | (1.2, 2, 1.2) |
| `Cover_Column_R` | Cylinder | (2.5, 2, 48) | (1.2, 2, 1.2) |
| `Cover_EntryWall` | Cube | (0, 2, 44) | (4, 4, 0.6) |

   `Cover_EntryWall` protege el punto de reaparición `CP_P3` de la vista del ojo. Escóndete en la cara **Z−** (la que da al jugador) de cada columna.
4. **Estanque y compuerta:**
   - `P3_BasinRim` Cube · (0, 0.5, 54) · scale (10, 1, 0.4) · `M_Wall` (borde del estanque, 1 m: no se puede subir).
   - `P3_Water` Cube · **raíz de la escena o hijo de `Puzzle3` (escala 1)** · Position (0, 0.15, 57) · Scale (10, 0.3, 6) · `M_Water` · **Layer = Water** · **quita el Box Collider**. Base del agua = y 0.
   - `P3_LevelMarker` Cube · (0, 3.5, 59.9) · scale (10, 0.08, 0.1) · `M_Marker` · **sin collider** (el script lo coloca a la altura objetivo).
   - `P3_Gate` Cube · (0, 3, 60.25) · scale (4, 6, 0.5) · `M_Door` (rellena el hueco de la pared del fondo).
5. **Ojo B:** arrastra el prefab `CosmicEye_A` a la escena → renómbralo **`CosmicEye_B`** · Position **(0, 7, 59)** · Rotation **(25, 180, 0)**. En su `Cosmic Eye Watcher` cambia:

| Campo | Valor |
|---|---|
| Hearing Radius | 20 |
| **Periodic Watch** | **✔** |
| Periodic Interval | 6 |
| Periodic Warning | 1.5 |
| Periodic Look Target | `P3_Altar` |
| Watch Duration | 3.5 |

6. En `Puzzle3` → *Add Component → **Puzzle3_Flood Gate***:

| Campo | Valor |
|---|---|
| Player | `Player` |
| Eye | `CosmicEye_B` |
| Altar Zone | `P3_AltarZone` |
| Altar Renderer | `P3_Altar` |
| Water / Water Renderer | `P3_Water` |
| Start Level | 0.3 |
| Gate Level | 3.5 |
| Rise Speed / Drain Speed / Drain Delay | 0.45 / 0.3 / 0.8 |
| Ratchet Segments | 0 (pon 4 si resulta demasiado duro) |
| Gate / Gate Renderer | `P3_Gate` |
| Gate Open Offset | (0, 6, 0) |
| Level Marker / Level Marker Renderer | `P3_LevelMarker` |
| Stop Eye Cycle On Solve | ✔ |

**Lectura visual del altar:** gris = libre · azul pulsante = bombeando · **amarillo = el ojo va a abrirse (¡suelta y corre!)** · rojo parpadeante = el ojo vigila. El agua se aclara al subir y tiembla con tu oleaje. Al llegar a la marca: marca y altar verdes, la compuerta sube.

---

## 10. Prueba rápida (checklist)

Pulsa **Play**:

- [ ] **Movimiento:** con toques el personaje da pasitos (orbe cian pequeño, sin disco de agua). Manteniendo, corre (orbe naranja latiendo, disco azul grande).
- [ ] **Cordura:** mantener mucho rato oscurece el cuerpo rosa y aumenta la niebla; quieto 1.5 s, se recupera.
- [ ] **Puzzle 1:** cruzar con toques → piedras amarillean un poco pero aguantan. Correr → la piedra se pone roja, se hunde, caes y reapareces; el ojo A se pone rojo. Al llegar a la meta, la marca se vuelve verde.
- [ ] **Puzzle 2:** en cada disco, sin dirección, mantener hasta que el medidor esté amarillo y soltar → verde. Los tres verdes → la puerta baja.
- [ ] **Puzzle 3:** bombear en el altar sube el agua. Cuando el altar se pone amarillo, suelta y ve detrás de una columna; el cono rojo no te detecta. Si te quedas expuesto, el ojo se vuelve blanco y reapareces en `CP_P3` con menos cordura.

## 11. Problemas frecuentes

| Síntoma | Causa / solución |
|---|---|
| `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class` | Algún otro script usa `Input.*` antiguo. Estos scripts no; cambia Active Input Handling a "Both" o corrige ese script. |
| El jugador "flota" o se atasca | Hay colliders en hijos visuales (`EyeL`, `ChargeOrb`, `WakeDisc`…) o falta quitar el Capsule Collider del Player. |
| Las piedras parecen cúpulas, el jugador resbala | Cylinder con Capsule Collider: reemplázalo por Mesh Collider *Convex*. |
| No caigo al agua del puzzle 1 | El `P1_Water` (Plane) aún tiene Mesh Collider. |
| El ojo me ve a través de las columnas | La columna no está en una capa incluida en *Occlusion Mask* (Default + Cover). |
| El ojo nunca me ve | *Occlusion Mask* incluye `Water` y el agua tiene collider, o el `EyeBall` conserva su collider y *Ray Start Offset* es menor que su radio (0.8). |
| El cono de luz atraviesa las columnas | Activa sombras de luces adicionales en el URP Asset y *Soft Shadows* en `VisionLight`. |
| No puedo bombear en un pilar/altar | Estás pulsando dirección al empezar la pulsación (eso camina). Suelta la dirección y vuelve a pulsar. |
| El medidor crece "hacia abajo" o desde el centro | `MeterRoot` tiene escala distinta de 1 o *Meter Mesh Height* no coincide (Cylinder = 2, Cube = 1). |
| El agua del puzzle 3 crece desde el centro | `P3_Water` es hijo de un objeto escalado; ponlo en la raíz o bajo un padre con escala 1. |

## 12. Valores para ajustar (tuning rápido)

| Sensación | Dónde |
|---|---|
| Tap vs Hold más estricto | `PlayerPressureController.holdThreshold` |
| Piedras más indulgentes | `Puzzle1.holdStressPerSecond` ↓, `holdLandingStress` ↓ |
| Pilares más fáciles | `Puzzle2.tolerance` ↑ |
| Ojo más sordo / más sensible | `CosmicEyeWatcher.hearingSensitivity`, `suspicionDecay` |
| Más tiempo para esconderse | `CosmicEyeWatcher.periodicWarning` ↑, `detectionTime` ↑ |
| Puzzle 3 menos castigador | `Puzzle3.drainSpeed` ↓, `ratchetSegments` = 4 |

## 13. Arquitectura (resumen)

- **`PlayerPressureController`** es la única fuente de verdad del input: expone `IsPressed`, `IsHolding`, `PressDuration`, `CurrentNoise`, `Sanity`, y eventos `PressReleased(PressInfo)`, `HoldStarted`, `Caught`, `Respawned`.
- **Interacción:** cada puzzle llama cada frame a `player.SetInteractionFocus(this, slot, dentroDeLaZona)`. Si una pulsación empieza dentro de la zona y sin dirección, queda "capturada" (`IsPumpingFor(owner, slot)`) y el jugador no se mueve.
- **Ruido:** `NoiseBus.Emit(posición, cantidad)` es estático; cualquier sistema puede emitir y cualquier ojo escucha con atenuación por distancia. Ruido continuo = intensidad × `deltaTime`; ruido puntual = valor directo.
- **Zonas:** se comprueban con `Collider.bounds.Contains` (sin `OnTriggerEnter` ni Rigidbodies): usa siempre **Box Colliders sin rotación** para las zonas.
- **Feedback visual:** `ProtoVisuals.SetColor` usa MaterialPropertyBlock (`_BaseColor`), así que los materiales compartidos no se duplican. Cuando llegue el arte final, sustituye los Renderers asignados por los del arte; la lógica no cambia.
