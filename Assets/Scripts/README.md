# Celeris — Scripts Reference

Referencia completa de todos los archivos C# del proyecto. 78 archivos organizados por carpeta.

---

## Background
Efecto visual de oscuridad radial (URP).

| Archivo | Función |
|---|---|
| `RadialDarknessEffect.cs` | Define el volumen del efecto de oscuridad radial. Parámetros configurables: radio, suavidad, color. |
| `RadialDarknessController.cs` | MonoBehaviour que controla el efecto en runtime: actualiza posición y radio según la escena activa. |
| `RadialDarknessRendererFeature.cs` | Renderer Feature de URP. Inyecta el efecto de oscuridad en el pipeline de render. Busca el shader `RadialDarkness` en `Create()`. |
| `RadialLightSync.cs` | Sincroniza la posición del efecto de luz radial con el Droide para que la oscuridad lo siga. |

---

## Config
Configuración de niveles.

| Archivo | Función |
|---|---|
| `ProceduralLevelConfig.cs` | ScriptableObject con todos los parámetros de un nivel: longitud del path, probabilidad de giros, cantidad de obstáculos, índice de nivel. Expone `GetScaledDifficulty()` que retorna un `RuntimeDifficulty`. |

---

## Core
Lógica central del juego.

| Archivo | Función |
|---|---|
| `GameFlowManager.cs` | Orquestador principal. Escucha eventos del Droide y del minijuego y coordina victoria, portal y game over. Delega en servicios especializados. |
| `GameStateManager.cs` | Singleton. Mantiene el estado global: pausado/activo, si hay retorno de portal pendiente, coordenadas de retorno, terminales hackeados. |
| `LevelManager.cs` | Singleton. Gestiona el progreso de nivel (avanzar, reintentar, resetear). Inyecta `IDifficultyDirector` y `IPlayerProgressStore`. Persiste progreso por usuario. |
| `DifficultyDirectorImpl.cs` | Implementación real del director de dificultad. Calcula el valor D (0-1) basado en historial de partidas y lo aplica al `ProceduralLevelConfig`. |
| `DifficultyModel.cs` | Modelo matemático puro. Calcula D a partir de métricas de `PerformanceTracker` (win rate, tiempo, batería, muertes consecutivas). |
| `DifficultyActuator.cs` | Toma el valor D y lo traduce en parámetros concretos del nivel (más láseres, menos buffers, etc.) escritos en `ProceduralLevelConfig`. |
| `NullDifficultyDirector.cs` | Director nulo para QA/debug. Mantiene D fijo en el valor configurado, sin cambios adaptativos. |
| `PerformanceTracker.cs` | Observador pasivo del Droide. Registra resultados de niveles en ventana deslizante de 5 y calcula métricas (win rate, tiempo promedio, causa de muerte dominante). |
| `PlayerProgressStore.cs` | Implementación de `IPlayerProgressStore`. Persiste progreso en `PlayerPrefs` con flush diferido en pausa/quit. |
| `DroidRestoreService.cs` | Restaura el estado del Droide (posición en grid, dirección) después de que el jugador completa o falla el minijuego de portal. |
| `MiniGameSceneService.cs` | Carga y descarga la escena del minijuego de forma aditiva (`LoadSceneMode.Additive`). |
| `IDifficultyDirector.cs` | Interfaz: `Apply(config)`, `RecordLevelResult(...)`, `CurrentDifficulty`. |
| `IDroidRestoreService.cs` | Interfaz: `RestoreAfterPortal()`. |
| `IMiniGameSceneService.cs` | Interfaz: `Load()`, `Unload()`, `IsLoaded`. |
| `IPlayerProgressStore.cs` | Interfaz: `GetLevelIndex`, `SetLevelIndex`, `DeleteKey`, `FlushIfDirty`. |

---

## Data
Tipos de datos compartidos entre sistemas.

| Archivo | Función |
|---|---|
| `GameStructs.cs` | Todos los enums y structs del proyecto: `TileType`, `MoveDirection`, `DeathCause`, `DroideState`, `LevelResult`, `RuntimeDifficulty`, `PlayerData`, `SegmentType`. |
| `HackSessionData.cs` | ScriptableObject con el estado de una sesión de hackeo: intentos restantes, si fue exitoso, dígito extraído, recompensa de puntos. |

---

## Escenario
Decoración visual de la escena de juego.

| Archivo | Función |
|---|---|
| `BackgroundDecorationSpawner.cs` | Spawna decoraciones de fondo (árboles, rocas, etc.) alrededor del path generado proceduralmente. |
| `ModularDecorationSpawner.cs` | Spawna decoraciones modulares encima o al lado de tiles específicos según su tipo. |
| `EnergiaTileEffect.cs` | Efecto visual sobre los `ChargeTile`: pulso de energía, partículas, color de advertencia cuando la batería es baja. |

---

## Grid
Generación procedural y comportamiento del tablero.

Pipeline de generación en orden estricto:
`PathSequencer` → `PathGeometryTracer` → `TileFactory` → `TileWaveAnimator`

| Archivo | Función |
|---|---|
| `ProceduralGridGenerator.cs` | Coordinador del pipeline. Llama los 4 pasos en orden. Expone `TileMap`, `StartWorldPos`, `OnGridReady`. |
| `PathSequencer.cs` | PASO 1. Construye la secuencia lógica del nivel (qué tiles y en qué orden) sin geometría. Reglas: buffers, clusters de obstáculos, portales, goal. |
| `PathGeometryTracer.cs` | PASO 2. Asigna coordenadas 3D a la secuencia lógica, generando giros aleatorios con semilla determinista. |
| `TileFactory.cs` | PASO 3. Instancia y configura los GameObjects de cada tile. Patrón OCP: nuevos obstáculos se registran con `Register()` sin modificar la clase. |
| `TileWaveAnimator.cs` | PASO 4. Anima la aparición de los tiles con efecto de ola (suben desde Y=-5 en secuencia). |
| `TileComponent.cs` | MonoBehaviour de cada tile en escena. Guarda `TileType`, `MoveDirection`, `gridCoord`. Maneja visual, emisión y `GetExitDirection()`. |
| `TileDescriptor.cs` | Struct inmutable que describe un tile lógico: tipo, definición de obstáculo, índice de portal. |
| `PlacedTile.cs` | Struct que combina `TileDescriptor` + coordenada 3D asignada por `PathGeometryTracer`. |
| `TileModelRegistry.cs` | Singleton con Script Execution Order -100. Implementa `ITileModelProvider`: almacena prefabs de modelos especiales (flecha, láser, charge, goal). |
| `LaserController.cs` | Gestiona el ciclo activo/inactivo de un `LaserTile` con corrutina. Dispara evento estático `OnLaserActivated` al encenderse. |
| `LaserObstacleDefinition.cs` | `IObstacleDefinition` para láseres. Define cluster size, `TileType.LaserTile`, y configura `LaserController` en el tile. |
| `ChargeObstacleDefinition.cs` | `IObstacleDefinition` para charge tiles. Define cluster size y `TileType.ChargeTile`. |
| `PortalTileComponent.cs` | MonoBehaviour del tile portal. Detecta cuando el Droide lo pisa y dispara el evento de entrada al portal. |
| `ITileModelProvider.cs` | Interfaz: propiedades `ArrowPrefab`, `LaserPrefab`, `ChargePrefab`, `GoalPrefab`. |
| `IObstacleDefinition.cs` | Interfaz: `TileType`, `GetClusterSize(rng)`, `ConfigureComponent(go, ...)`. |
| `IGravityProfile.cs` | Interfaz para perfiles de gravedad (Fase 4 — wall-walking). Actualmente sin implementación. |

---

## HackMinigame
Minijuego de hackeo del terminal.

| Archivo | Función |
|---|---|
| `TerminalHackManager.cs` | Orquestador del minijuego. Flujo: `BeginAttempt → Play → Validate → Resolve`. Dispara `OnTerminalExited` y `OnHackGameOver`. |
| `HackSequenceController.cs` | Genera y reproduce la secuencia de glifos que el jugador debe memorizar. Dispara `OnSequenceComplete` al terminar. |
| `HackInputValidator.cs` | Valida los clicks del jugador contra la secuencia correcta. Dispara `OnAttemptResult(bool)`. |
| `AlienGlyph.cs` | MonoBehaviour de cada glifo UI. Maneja estado visual (activo, correcto, error) e interactividad. |

---

## Input

| Archivo | Función |
|---|---|
| `MobileInputHandler.cs` | Traduce toques de pantalla (tap, press, release) en llamadas a `DroideCore`: `OnPressStart`, `OnPressEnd`, `RegisterChargeClick`. |

---

## Leaderboard
Sistema online con Supabase.

| Archivo | Función |
|---|---|
| `SupabaseManager.cs` | Cliente HTTP hacia Supabase. Operaciones: submit score, get top scores, get rank. Usa `UnityWebRequest`. |
| `AuthManager.cs` | Gestiona autenticación con Supabase: sign up, sign in, sign out, refresh token, crear perfil. |
| `ScoreManager.cs` | Singleton. Acumula puntos durante la partida, calcula score final con multiplicadores, llama a `SupabaseManager` para persistir. |
| `LeaderboardUI.cs` | Muestra la tabla de clasificación. Consume `SupabaseManager` para obtener top scores. |
| `AuthUI.cs` | Pantalla de login/registro. Delega en `AuthManager` y navega a `LeaderboardUI` en éxito. |

---

## MiniGame

| Archivo | Función |
|---|---|
| `MiniGameSimulator.cs` | Simulador legacy del minijuego (desactivado en producción). Permite probar el flujo de portal sin `TerminalHackManager`. Llama `GameFlowManager.OnPortalComplete()` directamente. |

---

## Player
El Droide (personaje jugador).

| Archivo | Función |
|---|---|
| `DroideCore.cs` | Núcleo principal. Único MonoBehaviour con Rigidbody. Gestiona física, estado observable, batería y eventos de ciclo de vida. Implementa `IDroideContext`. |
| `DroideBootstrapper.cs` | Crea e inyecta todos los sub-componentes del Droide en `Awake` (StateMachine, MovementDecider, BatteryController, PortalHandler, VFX). |
| `DroideStateMachine.cs` | Máquina de estados de movimiento. Gestiona transiciones entre `IPlayerState` (Normal, Friction). |
| `DroideMovementDecider.cs` | Decide el siguiente tile al que moverse basándose en `GridCoord`, dirección actual y `TileComponent.GetExitDirection()`. |
| `DroideBatteryController.cs` | Gestiona la batería: drenaje por tiempo y por obstáculos, eventos `OnBatteryChanged`. |
| `DroidePortalHandler.cs` | Detecta cuando el Droide llega a un `PortalTile` y dispara `OnPortalEntered`. |
| `DroideAnimator.cs` | Puente entre `DroideState` y el `Animator` de Unity. Mapea cada estado lógico a parámetros del Animator Controller. |
| `DroideLightController.cs` | Controla la luz del Droide: intensidad y color según estado (normal, charging, dead). |
| `DroideVFX.cs` | Gestiona efectos de partículas del Droide: muerte, pulso eléctrico, shockwave. |
| `NormalMovementState.cs` | `IPlayerState` de movimiento estándar. Mueve el Droide tile a tile con la estrategia de tap. |
| `FrictionMovementState.cs` | `IPlayerState` del `ChargeTile`. El Droide queda atrapado, drena batería, y se libera acumulando velocidad con taps. |
| `TapMovementStrategy.cs` | `IMovementStrategy` de tap: el Droide avanza un tile por cada tap del jugador. |
| `TileDetector.cs` | Detecta colisiones del Droide con tiles usando raycasts o triggers, e informa a `DroideCore`. |
| `LightPulse.cs` | Efecto de pulso de luz al aterrizar en un tile o al activar el pulso eléctrico. |
| `IDroideContext.cs` | Interfaz que expone al Droide hacia los `IPlayerState` (ISP). Incluye el enum `PlayerStateType`. |
| `IDroideAnimator.cs` | Interfaz del animator: `ForceScanAnimation()`. |
| `IMovementStrategy.cs` | Interfaz de estrategia de movimiento: `OnPressStart`, `OnPressEnd`. |
| `IPlayerState.cs` | Interfaz de estado del jugador: `Enter`, `Exit`, `Tick`, `OnPressStart`, `OnPressEnd`. |

---

## UI
Interfaz de usuario.

| Archivo | Función |
|---|---|
| `BatteryUI.cs` | Slider de batería. Cambia color según estado: verde (normal), naranja (warning), rojo (charging). |
| `GameOverTrigger.cs` | Escucha `DroideCore.OnDied` y activa la pantalla de game over. Singleton con `EnsureExists()`. |
| `GameOverPresenter.cs` | Presenta el panel de game over: causa de muerte, score, estrellas. Botones de retry y menú. |
| `LevelSelectController.cs` | Pantalla de selección de nivel. Genera botones dinámicamente con `LevelButtonUI`. |
| `LevelButtonUI.cs` | Botón individual de nivel: número, estrellas obtenidas, estado bloqueado/desbloqueado. |
| `MainMenuController.cs` | Controlador del menú principal: botones de jugar, leaderboard, opciones. |

---

## Utils

| Archivo | Función |
|---|---|
| `CameraFollower.cs` | Sigue al Droide con suavizado (Lerp). Mantiene offset configurable. |
| `ShockwaveEffect.cs` | Efecto de shockwave radial (escala y fade) al activar el pulso eléctrico del Droide. |
