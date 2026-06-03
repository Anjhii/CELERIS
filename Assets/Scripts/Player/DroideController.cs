// ============================================================
// DroideController.cs  |  Assets/Scripts/Player/
//
// v7 — Bugs de doble-input, doble-drenaje y doble-portal corregidos.
//
// CAMBIOS v7 (sobre v6):
//
//   DOBLE INPUT CORREGIDO:
//     HandleInput() renombrado a HandleHoldAndPulse().
//     Ya NO delega OnPressStart/End al state machine.
//     La delegación es EXCLUSIVA de MobileInputHandler (IPointerDown/Up).
//     Fallback automático: si no hay MobileInputHandler en escena,
//     el Update() lee raw input como respaldo (útil en test scenes).
//
//   DOBLE DRENAJE CORREGIDO:
//     TickChargeDrain() eliminado de Update().
//     FrictionMovementState.Tick() es el ÚNICO responsable del
//     drenaje en ChargeTile. No hay doble contabilidad.
//
//   DOBLE RESTORE-FROM-PORTAL CORREGIDO:
//     DroideController YA NO suscribe a TerminalHackManager.OnTerminalExited.
//     GameFlowManager es el único que llama RestoreFromPortal().
//     (GameFlowManager suscribe a OnTerminalExited y orquesta todo.)
//
//   DroideState.Charging EMITIDO CORRECTAMENTE:
//     ProcessTileEffect(ChargeTile) llama SetState(Charging) después
//     de TransitionToState(FrictionMovementState) para que el
//     DroideAnimator active isReadyToAdvance.
//
//   FrictionMovementState INTEGRADO:
//     ProcessTileEffect(ChargeTile) ahora está guardado con
//     !IsStuckInCharge (que FrictionMovementState.Enter() setea).
//
//   INPUT DUAL (Mouse + Touch):
//     GetInputDown/Up/Held() cubren ambas plataformas.
//     Para desktop sin UI button: el fallback raw-input del Update
//     garantiza que la escena sea jugable.
// ============================================================
using System;
using Celeris.Core;
using Celeris.Data;
using Celeris.Grid;
using Celeris.Input;
using UnityEngine;

namespace Celeris.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class DroideController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Grid")]
        public ProceduralGridGenerator generator;

        [Header("Movimiento")]
        [Tooltip("Velocidad de traslación en unidades/segundo")]
        public float speed = 5f;

        [Header("Batería")]
        public int   batteryLimit        = 100;
        [Tooltip("OBSOLETO — no se usa. El drenaje es responsabilidad de FrictionMovementState (ChargeTile) y TriggerLaserReset (LaserTile). Mantener a 0.")]
        public int   batteryDrainPerTile = 0;

        [Header("ChargeTile — Imán")]
        [Tooltip("Multiplicador de velocidad mientras IsStuckInCharge (0.1 = 10%)")]
        [Range(0.01f, 0.3f)]
        public float chargeSpeedMultiplier = 0.10f;
        [Tooltip("Batería drenada por segundo dentro del ChargeTile (usada por FrictionMovementState)")]
        public float chargeDrainRate       = 10f;
        [Tooltip("Número de taps necesarios para escapar del ChargeTile (legacy)")]
        public int   tapEscapeThreshold    = 8;

        [Header("ChargeTile — Fricción (state machine)")]
        [Tooltip("Velocidad mínima normalizada para que el Droide se mueva [0-1]")]
        [Range(0f, 1f)]
        public float chargeMinSpeedToMove  = 0.10f;
        [Tooltip("Impulso de velocidad por tap [0-1]")]
        [Range(0f, 1f)]
        public float chargeTapImpulse      = 0.25f;
        [Tooltip("Tasa de pérdida de velocidad por segundo")]
        [Range(0f, 5f)]
        public float chargeFrictionDecay   = 0.50f;
        [Tooltip("Velocidad mínima sostenida para escapar del ChargeTile [0-1]")]
        [Range(0f, 1f)]
        public float chargeEscapeSpeed     = 0.80f;

        [Header("Movimiento — Estado Normal")]
        [Tooltip("Duración base de un paso en segundos")]
        public float normalMoveDuration    = 0.20f;

        [Header("Fallo por Láser")]
        [Tooltip("Batería que se resta al impacto de láser")]
        public int   batteryPenalty        = 20;
        [Tooltip("Fuerza del impulso de retroceso al recibir un impacto de láser")]
        public float knockbackForce        = 8f;
        [Tooltip("Segundos de penalización antes de volver al inicio (obsoleto — mantenido por compatibilidad)")]
        public float resetDelay            = 0.8f;

        [Header("Pulso Eléctrico")]
        public float pulseCooldown = 3f;

        [Header("Efectos")]
        public LightPulse lightPulse;
        public ShockwaveEffect shockwave;

        [Header("Tile de Carga (Tap rápido)")]
        [Tooltip("Batería que suma cada tap del jugador durante la carga")]
        public int chargeClickBoost = 1;

        // ── Estado público ────────────────────────────────────
        public DroideState State          { get; private set; } = DroideState.IdleBetweenTiles;
        public DeathCause  LastDeathCause { get; private set; } = DeathCause.None;
        public Vector2Int  GridCoord      { get; private set; }
        public int         Battery        { get; private set; }
        public int         MaxBattery     => batteryLimit;
        public bool        IsStuckInCharge => _isStuckInCharge;

        // ── Propiedades del state machine ─────────────────────
        public float NormalMoveDuration   => normalMoveDuration;
        public float ChargeDrainRate      => chargeDrainRate;
        public float ChargeMinSpeedToMove => chargeMinSpeedToMove;
        public float ChargeTapImpulse     => chargeTapImpulse;
        public float ChargeFrictionDecay  => chargeFrictionDecay;
        public float ChargeEscapeSpeed    => chargeEscapeSpeed;

        // ── Eventos ───────────────────────────────────────────
        public event Action<DroideState>               OnStateChanged;
        public event Action<int>                       OnBatteryChanged;
        public event Action<TileComponent>             OnTileEntered;
        public event Action                            OnLevelReset;
        public event Action<TileComponent, Vector2Int> OnPortalEntered;

        // ── Privado — física ──────────────────────────────────
        private Rigidbody _rb;

        // ── Privado — grid ────────────────────────────────────
        private Vector2Int _direction          = new(0, 1);
        private Vector2Int _lastProcessedCoord;

        // ── Privado — ChargeTile ──────────────────────────────
        // Gestionado por FrictionMovementState vía SetIsStuckInCharge().
        private bool _isStuckInCharge = false;

        // ── Privado — tipo de tile anterior ──────────────────
        // Usado por ProcessTileEffect para evitar reiniciar la animación
        // de scan/forcejeo al pisar tiles consecutivos del mismo clúster.
        private TileType _lastProcessedTileType = TileType.BaseTile;

        // ── Privado — hold / pulso ────────────────────────────
        private float _holdTimer       = 0f;
        private bool  _holdRotateFired = false;
        private const float HOLD_THRESHOLD = 0.45f;
        private float _lastPulseTime   = -999f;

        // ── Privado — reset delay ─────────────────────────────
        private bool  _inResetDelay = false;
        private float _resetTimer   = 0f;

        // ── Privado — state machine ───────────────────────────
        private IPlayerState _currentState;
        private bool         _shouldMove           = false;
        private float        _moveDurationOverride = -1f;

        // ── Privado — animador ────────────────────────────────
        private DroideAnimator _droideAnimator;

        // ── Privado — input fallback ──────────────────────────
        // Si no hay MobileInputHandler en escena (test scenes),
        // Update() usa raw input para OnPressStart/End.
        private MobileInputHandler _mobileHandler;

        // ─────────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            _rb.useGravity     = false;
            _rb.constraints    = RigidbodyConstraints.FreezePositionY
                               | RigidbodyConstraints.FreezeRotation;
        }

        private void Start()
        {
            // Detectar MobileInputHandler. Si está presente, él maneja
            // todo el press/release. Sin él, Update() usa raw input.
            _mobileHandler = FindObjectOfType<MobileInputHandler>();
            if (_mobileHandler == null)
                Debug.LogWarning("[DroideController] No hay MobileInputHandler. " +
                                 "Usando raw input (modo test).");

            // Cachear el animador para llamadas directas desde estados
            _droideAnimator = FindObjectOfType<DroideAnimator>();
        }

        private void OnEnable()
        {
            if (generator != null) generator.OnGridReady += Init;
            // NOTA: NO suscribo TerminalHackManager.OnTerminalExited aquí.
            // GameFlowManager es el responsable de RestoreFromPortal().
            // Suscribirlo aquí causaría doble RestoreFromPortal.
            LaserController.OnLaserActivated += HandleLaserActivated;
        }

        private void OnDisable()
        {
            if (generator != null) generator.OnGridReady -= Init;
            LaserController.OnLaserActivated -= HandleLaserActivated;
        }

        /// <summary>
        /// Detecta láseres que se encienden mientras el droide está parado sobre ellos.
        /// Cubre el caso donde DetectTileEntry() no dispara (misma coord que la última procesada).
        /// </summary>
        private void HandleLaserActivated(Vector2Int laserCoord)
        {
            if (laserCoord != GridCoord) return;
            if (State == DroideState.Dead || State == DroideState.Victory ||
                State == DroideState.AtPortal || _inResetDelay) return;

            TakeDamage(batteryPenalty);
        }

        // ── Inicialización ────────────────────────────────────
        private void Init()
        {
            transform.position  = generator.StartWorldPos + Vector3.up * 1.1f;
            transform.rotation  = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            GridCoord           = WorldToCoord(generator.StartWorldPos);
            _lastProcessedCoord = GridCoord;
            _direction          = new Vector2Int(0, 1);

            Battery                  = batteryLimit;
            _isStuckInCharge         = false;
            _lastProcessedTileType   = TileType.BaseTile;
            _inResetDelay            = false;
            _resetTimer         = 0f;
            _holdTimer          = 0f;
            _holdRotateFired    = false;
            _shouldMove         = false;
            LastDeathCause      = DeathCause.None;

            _rb.velocity = Vector3.zero;
            OnBatteryChanged?.Invoke(Battery);

            // Reiniciar contador de terminales al empezar nivel
            TerminalHackManager.ResetHackedCount();

            // Inicializar state machine
            TransitionToState(new NormalMovementState());
            SetState(DroideState.IdleBetweenTiles);

            Debug.Log("[DroideController] Init. Batería=" + Battery);
        }

        // ── Update principal ──────────────────────────────────
        private void Update()
        {
            if (GameStateManager.IsPaused) { _rb.velocity = Vector3.zero; return; }

            if (State == DroideState.Dead    ||
                State == DroideState.Victory ||
                State == DroideState.AtPortal)
            {
                _rb.velocity = Vector3.zero;
                return;
            }

            // ── 1. State machine: lógica por frame ────────────
            _currentState?.Tick(this);

            // ── 2. Input raw: solo hold-to-rotate y pulso ─────
            HandleHoldAndPulse();

            // ── 3. Aplicar velocidad al Rigidbody ─────────────
            ApplyVelocity();

            // ── 4. Detección de tile ──────────────────────────
            DetectTileEntry();
        }

        // ── Input unificado (Mouse + Touch) ───────────────────

        // La forma correcta usando la clase nativa de Unity:

        private static bool GetInputDown()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0)) return true;
            foreach (var t in UnityEngine.Input.touches)
                if (t.phase == TouchPhase.Began) return true;
            return false;
        }

        private static bool GetInputUp()
        {
            // Solo checkeamos el mouse
            if (UnityEngine.Input.GetMouseButtonUp(0)) return true;
            
            // Solo checkeamos fases específicas del touch
            foreach (var t in UnityEngine.Input.touches)
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) return true;
                
            return false;
        }

        private static bool GetInputHeld()
        {
            if (UnityEngine.Input.GetMouseButton(0)) return true;
            foreach (var t in UnityEngine.Input.touches)
                // Stationary = dedo quieto, Moved = dedo deslizándose
                if (t.phase == TouchPhase.Stationary || t.phase == TouchPhase.Moved) return true;
            return false;
        }
        // ── Hold-to-rotate y pulso eléctrico ─────────────────
        // MobileInputHandler gestiona OnPressStart/End y TryFireElectricPulse.
        // Este método SOLO maneja el hold timer, el rotate y el fallback
        // de raw input cuando no hay MobileInputHandler en escena.
        private void HandleHoldAndPulse()
        {
            bool pressed  = GetInputDown();
            bool released = GetInputUp();
            bool held     = GetInputHeld();

            // ── Fallback: raw input sin MobileInputHandler ────
            // (modo test, escenas sin UI). Con MobileInputHandler presente,
            // él llama OnPressStart/End directamente y este bloque no actúa.
            if (_mobileHandler == null)
            {
                if (pressed)  OnPressStart();
                if (released) OnPressEnd();
            }

            // ── Hold para rotar flecha ─────────────────────────
            // DISEÑO: el droide NO se detiene al rotar. TryRotateArrow()
            // redirige rb.velocity manteniendo la inercia actual.
            if (held)
            {
                _holdTimer += Time.deltaTime;
                if (!_holdRotateFired && _holdTimer >= HOLD_THRESHOLD)
                {
                    _holdRotateFired = true;
                    TryRotateArrow();   // rota y redirige velocidad — sin Stop
                }
            }
            if (released) { _holdTimer = 0f; _holdRotateFired = false; }

            // ── Pulso eléctrico (fallback) ────────────────────
            // MobileInputHandler llama TriggerElectricPulse() en su propio OnPointerDown.
            // Aquí solo actúa sin MobileInputHandler.
            if (_mobileHandler == null && pressed &&
                !_isStuckInCharge && !_holdRotateFired &&
                HasLaserAtRangeOne() &&
                Time.unscaledTime - _lastPulseTime >= pulseCooldown)
            {
                _lastPulseTime = Time.unscaledTime;
                TriggerElectricPulse();
            }
        }

        // ── Velocidad de Rigidbody ────────────────────────────
        // _shouldMove es seteado por el IPlayerState activo (NormalMovementState o FrictionMovementState).
        // _holdRotateFired ya NO bloquea el movimiento: el droide gira manteniendo inercia.
        private void ApplyVelocity()
        {
            bool canMove = _shouldMove;

            if (canMove)
            {
                // SÍN 5: si el tile siguiente no existe y no estamos en fricción,
                // detener el droide para evitar que "empuje" contra bordes sin tile.
                if (!_isStuckInCharge && generator != null &&
                    generator.GetTile(GridCoord + _direction) == null)
                {
                    _rb.velocity = Vector3.zero;
                    return;
                }

                // FrictionMovementState.Enter() setea _isStuckInCharge = true,
                // lo que aplica el multiplicador de velocidad reducida.
                float effectiveSpeed = _isStuckInCharge
                    ? speed * chargeSpeedMultiplier
                    : speed;

                _rb.velocity = transform.forward * effectiveSpeed;

                if (!_isStuckInCharge && State != DroideState.Moving)
                    SetState(DroideState.Moving);
            }
            else
            {
                _rb.velocity = Vector3.zero;

                if (!_isStuckInCharge && State == DroideState.Moving)
                    SetState(DroideState.IdleBetweenTiles);
            }
        }

        // ── Detección de tile al cruzar coordenadas ───────────
        private void DetectTileEntry()
        {
            Vector2Int current = WorldToCoord(transform.position);
            if (current == _lastProcessedCoord) return;

            var tile = generator.GetTile(current);
            if (tile == null)
            {
                if (State != DroideState.Dead)
                {
                    LastDeathCause = DeathCause.Fall;
                    TriggerDeath();
                }
                return;
            }

            _lastProcessedCoord = current;
            GridCoord           = current;
            OnTileEntered?.Invoke(tile);

            ProcessTileEffect(tile);

            // Actualizar DESPUÉS de procesar para que ProcessTileEffect
            // pueda comparar contra el tipo del tile ANTERIOR.
            _lastProcessedTileType = tile.tileType;
        }

        // ── Efectos por tipo de tile ──────────────────────────
        private void ProcessTileEffect(TileComponent tile)
        {
            switch (tile.tileType)
            {
                case TileType.ArrowTile:
                    ApplyArrowDirection(tile);
                    break;

                case TileType.LaserTile when tile.isActive:
                    TakeDamage(batteryPenalty);
                    break;

                // ChargeTile: transición a FrictionMovementState.
                // FrictionMovementState.Enter() llama SetIsStuckInCharge(true)
                // y gestiona TODOS los drenes de batería — no hay TickChargeDrain() paralelo.
                case TileType.ChargeTile:
                    if (!_isStuckInCharge)
                    {
                        TransitionToState(new FrictionMovementState());
                        SetState(DroideState.Charging);
                    }
                    // Regla 5: la animación de scan solo se dispara al entrar al PRIMER
                    // tile del clúster. Si el tile anterior ya era ChargeTile, el droide
                    // ya está en estado Charging y la animación no debe reiniciarse.
                    if (_lastProcessedTileType != TileType.ChargeTile)
                        SetScanAnimation();

                    // Activar feedback visual del ChargeTile
                    var chargeEffect = tile.GetComponentInChildren<EnergiaTileEffect>();
                    chargeEffect?.StartDraining();
                    break;

                // GoalTile: requiere las 3 terminales hackeadas
                case TileType.GoalTile:
                    if (TerminalHackManager.HackedTerminalsCount >= TerminalHackManager.RequiredHacks)
                    {
                        _rb.velocity = Vector3.zero;
                        SetState(DroideState.Victory);
                    }
                    else
                    {
                        int faltantes = TerminalHackManager.RequiredHacks
                                      - TerminalHackManager.HackedTerminalsCount;
                        Debug.Log($"[DroideController] Acceso denegado — " +
                                  $"faltan {faltantes} terminal(es) por hackear.");
                        // Rebote suave para no quedar pegado en la meta
                        _rb.velocity = -transform.forward * speed * 0.5f;
                    }
                    break;

                // Portal: guardar estado y notificar a GameFlowManager
                case TileType.PortalTile:
                    var portalComp = tile.GetComponent<PortalTileComponent>();
                    if (portalComp == null || !portalComp.IsCompleted)
                    {
                        _rb.velocity = Vector3.zero;
                        SetState(DroideState.AtPortal);
                        OnPortalEntered?.Invoke(tile, _direction);
                        // GameFlowManager recibe OnPortalEntered, pausa el juego y carga la escena.
                        // Al terminar, GameFlowManager llama droide.RestoreFromPortal().
                    }
                    break;
            }
        }

        // ── Daño con retroceso físico ─────────────────────────
        /// <summary>
        /// Aplica daño de batería y un impulso de retroceso físico.
        /// Si la batería llega a 0, dispara la muerte real.
        /// No reinicia la posición — el droide retrocede y continúa jugando.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (State == DroideState.Dead || State == DroideState.Victory) return;

            _isStuckInCharge = false;
            Battery          = Mathf.Max(0, Battery - amount);
            OnBatteryChanged?.Invoke(Battery);

            // Impulso de retroceso: desplaza al droide hacia atrás sin resetear posición
            _rb.AddForce(-transform.forward * knockbackForce, ForceMode.Impulse);

            if (Battery <= 0)
            {
                LastDeathCause = DeathCause.Laser;
                TriggerDeath();
                return;
            }

            LastDeathCause = DeathCause.Laser;
            SetState(DroideState.IdleBetweenTiles);
        }

        // ── Muerte final ──────────────────────────────────────
        private void TriggerDeath()
        {
            _rb.velocity     = Vector3.zero;
            _isStuckInCharge = false;
            SetState(DroideState.Dead);
        }

        // ── Portal: retorno desde minijuego ───────────────────

        /// <summary>
        /// Restaura el Droide tras regresar del minijuego de portal.
        /// Llamado exclusivamente por GameFlowManager. No suscribir a
        /// TerminalHackManager.OnTerminalExited desde aquí — GameFlowManager
        /// orquesta todo para evitar doble RestoreFromPortal.
        /// </summary>
        /// <summary>
        /// Llamado por RestoreFromPortal tras reposicionar al droide.
        /// NO avanza físicamente al droide: _lastProcessedCoord queda en el
        /// tile del portal, por lo que el portal no se re-dispara. El primer
        /// tile post-portal se procesará limpiamente por DetectTileEntry cuando
        /// el jugador empiece a moverse.
        ///
        /// CORRECCIÓN de bug anterior: el antiguo "advance 1f" colocaba al droide
        /// sobre el tile siguiente y lo marcaba como ya procesado
        /// (_lastProcessedCoord = tile_post_portal). Si ese tile era un ArrowTile,
        /// su efecto de dirección nunca se ejecutaba → el droide continuaba recto
        /// o invertía dirección de forma erronea.
        /// </summary>
        private void ExitPortal()
        {
            _isStuckInCharge = false;
            LastDeathCause   = DeathCause.None;
            _rb.velocity     = Vector3.zero;

            // El droide permanece sobre el tile de portal.
            // _lastProcessedCoord ya fue asignado al coord del portal por
            // RestoreFromPortal → DetectTileEntry no re-dispara el portal
            // (current == _lastProcessedCoord mientras el droide no se mueva).

            SetState(DroideState.ReadyToAdvance);
            TransitionToState(new NormalMovementState());
            SetState(DroideState.IdleBetweenTiles);

            Debug.Log($"[DroideController] ExitPortal(). GridCoord={GridCoord} Dir={_direction}");
        }

        /// <summary>
        /// Reposiciona al droide en la coord del portal con la dirección correcta
        /// y llama ExitPortal(). Único punto de restauración post-minijuego.
        /// </summary>
        public void RestoreFromPortal(Vector2Int coord, Vector2Int direction)
        {
            _direction    = direction;
            var dir3      = new Vector3(direction.x, 0f, direction.y).normalized;
            var targetRot = Quaternion.LookRotation(dir3, Vector3.up);

            transform.position = generator.CoordToWorld(coord) + Vector3.up * 1.1f;
            transform.rotation = targetRot;
            _rb.MoveRotation(targetRot);   // sync rotación interna del Rigidbody

            _lastProcessedCoord = coord;
            GridCoord           = coord;

            ExitPortal();
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// MobileInputHandler llama esto en OnPointerDown.
        /// Delega al estado activo.
        /// </summary>
        public void OnPressStart() => _currentState?.OnPressStart(this);

        /// <summary>
        /// MobileInputHandler llama esto en OnPointerUp.
        /// Delega al estado activo.
        /// </summary>
        public void OnPressEnd() => _currentState?.OnPressEnd(this);

        /// <summary>Activa o desactiva el movimiento continuo (usado por los estados).</summary>
        public void SetShouldMove(bool value) => _shouldMove = value;

        /// <summary>
        /// FrictionMovementState.Enter/Exit llaman esto para marcar el estado de atasco.
        /// Controla si ApplyVelocity() aplica chargeSpeedMultiplier.
        /// </summary>
        public void SetIsStuckInCharge(bool value)
        {
            _isStuckInCharge = value;
        }

        /// <summary>Sobreescribe la duración del paso. Pasar -1f para restaurar el valor base.</summary>
        public void SetMoveDurationOverride(float value) => _moveDurationOverride = value;

        /// <summary>Rota la flecha del tile actual si el Droide está sobre un ArrowTile.</summary>
        public void RotateCurrentArrow() => TryRotateArrow();

        /// <summary>Resta batería sin matar al droide (usada por FrictionMovementState).</summary>
        public void TakeBatteryHit(int amount)
        {
            Battery = Mathf.Max(0, Battery - amount);
            OnBatteryChanged?.Invoke(Battery);

            float ratio = Battery / (float)MaxBattery;
            var tile    = generator.GetTile(GridCoord);
            var effect  = tile?.GetComponentInChildren<EnergiaTileEffect>();
            effect?.UpdateBatteryColor(ratio);
        }

        /// <summary>Mata al droide con la causa indicada (usada por FrictionMovementState).</summary>
        public void ForceKill(DeathCause cause)
        {
            LastDeathCause = cause;
            TriggerDeath();
        }

        /// <summary>Suma batería por tap en ChargeTile con feedback visual.</summary>
        public void RegisterChargeClick()
        {
            Battery = Mathf.Min(batteryLimit, Battery + chargeClickBoost);
            OnBatteryChanged?.Invoke(Battery);

            var tile   = generator.GetTile(GridCoord);
            var effect = tile?.GetComponentInChildren<EnergiaTileEffect>();
            effect?.TriggerTapBurst();
        }

        public void TriggerLightPulse()
        {
            if (lightPulse != null) lightPulse.Pulse();
            if (shockwave != null) shockwave.Trigger();
            PulseAdjacentTiles();
        }

        public void PulseAdjacentTiles()
        {
            Vector2Int[] neighbors =
            {
                GridCoord + new Vector2Int( 1,  0),
                GridCoord + new Vector2Int(-1,  0),
                GridCoord + new Vector2Int( 0,  1),
                GridCoord + new Vector2Int( 0, -1)
            };
            foreach (var coord in neighbors)
            {
                var tile = generator.GetTile(coord);
                tile?.PulseEmission();
            }
        }

        /// <summary>
        /// Solicita al DroideAnimator que active la animación de scan/forcejeo.
        /// Llamado por FrictionMovementState.Enter() para romper la animación de caminata
        /// mientras el droide está atrapado en un ChargeTile.
        /// </summary>
        public void SetScanAnimation()
        {
            if (_droideAnimator == null)
                _droideAnimator = FindObjectOfType<DroideAnimator>();
            _droideAnimator?.ForceScanAnimation();
        }

        /// <summary>Cambia el estado activo del state machine.</summary>
        public void TransitionToState(IPlayerState newState)
        {
            _currentState?.Exit(this);
            _currentState = newState;
            _currentState?.Enter(this);
        }

        public void TriggerElectricPulse()
        {
            TriggerLightPulse();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue;
                ToggleLaserAt(GridCoord + new Vector2Int(dx, dy));
            }
        }

        public void TryRotateArrow()
        {
            var tile = generator.GetTile(GridCoord);
            if (tile == null || tile.tileType != TileType.ArrowTile) return;

            // No permitir rotación si haría que la flecha apunte hacia atrás
            var nextDir = (MoveDirection)(((int)tile.arrowDirection + 1) % 4);
            var nextVec = TileComponent.DirectionToVector(nextDir);
            if (nextVec == -_direction) return;

            tile.RotateArrow90Degrees();
            ApplyArrowDirection(tile);
        }

        /// <summary>
        /// Aplica la dirección de un ArrowTile al droide con precisión física.
        ///
        /// CORRECCIONES vs versión anterior:
        ///   1. Axis-snap: al girar, la posición del Rigidbody se alinea al centro del tile
        ///      en el eje perpendicular al nuevo movimiento. Elimina la deriva lateral que
        ///      causaba que el droide "se saliera del carril" al tomar una curva.
        ///   2. MoveRotation: sincroniza la rotación interna del Rigidbody con el transform.
        ///      Setear transform.forward directamente no actualiza rb.rotation, lo que
        ///      provoca que en el siguiente FixedUpdate la física use la rotación antigua.
        /// </summary>
        private void ApplyArrowDirection(TileComponent tile)
        {
            Vector2Int d    = TileComponent.DirectionToVector(tile.arrowDirection);
            Vector3    dir3 = new Vector3(d.x, 0f, d.y).normalized;

            // Seguridad anti-loop: si la flecha apunta al tile anterior, ignorarla
            if (GridCoord + d == _lastProcessedCoord) return;

            // ── Axis-snap al centro del tile ──────────────────
            // Alinear el eje perpendicular a la nueva dirección para eliminar
            // la deriva lateral acumulada por el movimiento del Rigidbody.
            Vector3 p = transform.position;
            if (d.x != 0)   // nueva dirección E/W → fijar Z al centro del tile
                p.z = tile.gridCoord.y;
            else             // nueva dirección N/S → fijar X al centro del tile
                p.x = tile.gridCoord.x;
            _rb.MovePosition(p);

            // ── Actualizar dirección lógica y rotación física ─
            _direction = d;
            var targetRot = Quaternion.LookRotation(dir3, Vector3.up);
            transform.rotation = targetRot;          // visual inmediato
            _rb.MoveRotation(targetRot);             // sync Rigidbody para FixedUpdate

            // ── Redirigir velocidad sin perder inercia ────────
            float spd = _rb.velocity.magnitude;
            if (spd > 0.01f)
                _rb.velocity = dir3 * spd;
        }

        public bool HasLaserAtRangeOne()
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue;
                var t = generator.GetTile(GridCoord + new Vector2Int(dx, dy));
                if (t != null && t.tileType == TileType.LaserTile && t.isActive)
                    return true;
            }
            return false;
        }

        // ── Helpers ───────────────────────────────────────────
        private void ToggleLaserAt(Vector2Int coord)
        {
            var tile = generator.GetTile(coord);
            if (tile != null && tile.tileType == TileType.LaserTile)
                tile.ToggleLaser();
        }

        private void SetState(DroideState s)
        {
            if (State == s) return;
            State = s;
            OnStateChanged?.Invoke(s);
        }

        private Vector2Int WorldToCoord(Vector3 world) =>
            new(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }
}
