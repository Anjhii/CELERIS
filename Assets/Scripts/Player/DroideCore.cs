// ============================================================
// DroideCore.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Ser el único MonoBehaviour con Rigidbody del Droide.
//   Gestionar física (MovePosition/MoveRotation en FixedUpdate).
//   Exponer el estado observable del Droide (GridCoord, Direction,
//   Battery, State) y los eventos del ciclo de vida del jugador.
//
// LO QUE NO HACE (SRP):
//   No decide cómo moverse (→ DroideMovementDecider).
//   No gestiona estados de input (→ DroideStateMachine).
//   No gestiona batería (→ DroideBatteryController).
//   No gestiona portales (→ DroidePortalHandler).
//   No gestiona VFX (→ DroideVFX).
//   No instancia sub-componentes (→ DroideBootstrapper).
//
// INVARIANTE DE FÍSICA:
//   Toda llamada a MovePosition/MoveRotation ocurre ÚNICAMENTE
//   en FixedUpdate, via EnqueueMove/EnqueueRotation.
//   Romper esta regla puede causar giros >90° o deriva lateral.
//
// IMPLEMENTA IDroideContext:
//   Los estados de movimiento (IPlayerState) reciben IDroideContext,
//   no DroideCore directamente (principio ISP).
// ============================================================
using System;
using Celeris.Core;
using Celeris.Data;
using Celeris.Escenario;
using Celeris.Grid;
using Celeris.Input;
using UnityEngine;

namespace Celeris.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class DroideCore : MonoBehaviour, IDroideContext
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Grid")]
        public ProceduralGridGenerator generator;

        [Header("Movimiento")]
        [Tooltip("Velocidad de traslación en unidades/segundo")]
        public float speed = 5f;

        [Header("ChargeTile — Imán")]
        [Range(0.01f, 0.3f)]
        public float chargeSpeedMultiplier = 0.10f;
        public float chargeDrainRate       = 10f;
        public int   tapEscapeThreshold    = 8;

        [Header("ChargeTile — Fricción")]
        [Range(0f, 1f)]
        public float chargeMinSpeedToMove  = 0.10f;
        [Range(0f, 1f)]
        public float chargeTapImpulse      = 0.25f;
        [Range(0f, 5f)]
        public float chargeFrictionDecay   = 0.50f;
        [Range(0f, 1f)]
        public float chargeEscapeSpeed     = 0.80f;

        [Header("Movimiento — Estado Normal")]
        public float normalMoveDuration    = 0.20f;

        [Header("Daño por Láser")]
        public int   batteryPenalty        = 20;
        public float knockbackForce        = 8f;

        [Header("Batería")]
        public int batteryLimit            = 100;
        public int chargeClickBoost        = 1;

        [Header("Pulso Eléctrico")]
        public float pulseCooldown = 3f;

        // ── Estado observable (read-only) ─────────────────────
        public DroideState State          { get; private set; } = DroideState.IdleBetweenTiles;
        public DeathCause  LastDeathCause { get; private set; } = DeathCause.None;
        public Vector3Int  GridCoord      { get; private set; }
        public Vector3Int  Direction      { get; private set; } = new(0, 0, 1);

        // ── IDroideContext: parámetros (read-only) ────────────
        public float NormalMoveDuration   => normalMoveDuration;
        public float ChargeDrainRate      => chargeDrainRate;
        public float ChargeMinSpeedToMove => chargeMinSpeedToMove;
        public float ChargeTapImpulse     => chargeTapImpulse;
        public float ChargeFrictionDecay  => chargeFrictionDecay;
        public float ChargeEscapeSpeed    => chargeEscapeSpeed;

        // ── F1-T1: Battery es proxy puro de DroideBatteryController ──
        // DroideCore ya NO posee el estado de batería.
        // Toda lectura pasa por _batteryCtrl.CurrentBattery.
        // Fallback a 0 si _batteryCtrl aún no fue inyectado (antes de Init).
        public int   Battery              => _batteryCtrl?.CurrentBattery ?? 0;
        public int   MaxBattery           => batteryLimit;
        public bool  IsInputHeld          => GetInputHeld();

        // ── Eventos del ciclo de vida ─────────────────────────
        public event Action<DroideState>               OnStateChanged;
        public event Action<DeathCause>                OnDied;
        public event Action                            OnVictory;
        public event Action<TileComponent, Vector3Int> OnPortalEntered;
        public event Action<int>                       OnBatteryChanged;
        public event Action                            OnLevelReset;

        // ── Física ────────────────────────────────────────────
        private Rigidbody _rb;

        // ── Pending-move pattern (escritura en Update, aplicación en FixedUpdate) ─
        private bool       _hasPendingPosition;
        private Vector3    _pendingPosition;
        private bool       _hasPendingRotation;
        private Quaternion _pendingRotation;

        // ── Estado interno ────────────────────────────────────
        // _battery ELIMINADO (F1-T1): DroideBatteryController es la única
        // fuente de verdad. Leer Battery (property) en lugar de _battery.
        private bool  _isStuckInCharge;
        private bool  _shouldMove;
        private float _moveDurationOverride = -1f;

        // ── Hold / pulso ──────────────────────────────────────
        private float _holdTimer       = 0f;
        private bool  _holdRotateFired = false;
        private const float HOLD_THRESHOLD = 0.45f;

        // F2-T2: _lastPulseTime vive AQUÍ como única fuente de verdad del cooldown.
        // MobileInputHandler ya NO tiene su propio _lastPulseTime.
        private float _lastPulseTime   = -999f;

        // ── Sub-componentes inyectados por DroideBootstrapper ─
        private DroideStateMachine      _stateMachine;
        private DroideBatteryController _batteryCtrl;
        private DroideMovementDecider   _movementDecider;
        private DroidePortalHandler     _portalHandler;
        private TileDetector            _tileDetector;

        // F2-T1: IDroideAnimator inyectado (DIP).
        // DroideCore no conoce DroideAnimator concreto.
        private IDroideAnimator _droideAnimator;

        // ── Input fallback (se suscribe por evento, no FindObjectOfType) ─
        private MobileInputHandler _mobileHandler;

        private const float SNAP_CORRECTION_THRESHOLD = 0.1f;

        // ─────────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            _rb.useGravity     = false;
            _rb.constraints    = RigidbodyConstraints.FreezePositionY
                               | RigidbodyConstraints.FreezeRotation;
        }

        private void OnEnable()
        {
            if (generator != null) generator.OnGridReady += Init;
            LaserController.OnLaserActivated += HandleLaserActivated;
        }

        private void OnDisable()
        {
            if (generator != null) generator.OnGridReady -= Init;
            LaserController.OnLaserActivated -= HandleLaserActivated;
        }

        // ═════════════════════════════════════════════════════
        //  CICLOS UNITY
        // ═════════════════════════════════════════════════════

        private void FixedUpdate()
        {
            if (_hasPendingPosition)
            {
                _rb.MovePosition(_pendingPosition);
                _hasPendingPosition = false;
            }
            if (_hasPendingRotation)
            {
                _rb.MoveRotation(_pendingRotation);
                _hasPendingRotation = false;
            }
        }

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

            _stateMachine?.Tick(this);
            HandleHoldAndPulse();
            ApplyVelocity();
            _tileDetector?.Tick();
        }

        // ═════════════════════════════════════════════════════
        //  INICIALIZACIÓN
        // ═════════════════════════════════════════════════════

        private void Init()
        {
            transform.position = generator.StartWorldPos + Vector3.up * 1.1f;
            transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            GridCoord = WorldToCoord(generator.StartWorldPos);
            Direction = new Vector3Int(0, 0, 1);

            if (_tileDetector == null)
            {
                _tileDetector = new TileDetector(transform, generator, GridCoord);
                _tileDetector.OnTileEntered += HandleTileEntered;
                _tileDetector.OnTileMissed  += HandleTileMissed;
                _portalHandler?.SetTileDetector(_tileDetector);
            }
            else
            {
                _tileDetector.Reset(GridCoord);
            }

            _isStuckInCharge      = false;
            _shouldMove           = false;
            _moveDurationOverride = -1f;
            LastDeathCause        = DeathCause.None;
            _hasPendingPosition   = false;
            _hasPendingRotation   = false;
            _holdTimer            = 0f;
            _holdRotateFired      = false;
            _rb.velocity          = Vector3.zero;

            // F1-T1: _batteryCtrl es la única fuente de verdad.
            // Init() resetea a batteryLimit y emite OnBatteryChanged vía su propio evento.
            _batteryCtrl?.Init(batteryLimit);

            GameStateManager.Instance?.ResetTerminalsHacked();

            _stateMachine?.ResetToInitialState(this);
            SetState(DroideState.IdleBetweenTiles);
            OnLevelReset?.Invoke();

            Debug.Log("[DroideCore] Init. Batería=" + Battery);
        }

        // ═════════════════════════════════════════════════════
        //  INYECCIÓN DE SUB-COMPONENTES (llamado por DroideBootstrapper)
        // ═════════════════════════════════════════════════════

        public void InjectStateMachine(DroideStateMachine sm)       => _stateMachine    = sm;
        public void InjectMovementDecider(DroideMovementDecider md) => _movementDecider = md;
        public void InjectPortalHandler(DroidePortalHandler ph)    => _portalHandler   = ph;
        public void InjectMobileHandler(MobileInputHandler mh)     => _mobileHandler   = mh;

        /// <summary>
        /// F2-T1: Inyecta el animador del Droide (DIP).
        /// DroideCore llama _droideAnimator.ForceScanAnimation() directamente
        /// en lugar de emitir un evento que el animador escucha.
        /// </summary>
        public void InjectAnimator(IDroideAnimator animator)        => _droideAnimator  = animator;

        /// <summary>
        /// Inyecta el controlador de batería y suscribe sus eventos a DroideCore.
        /// F1-T1: DroideBatteryController es la única fuente de verdad de batería.
        /// OnBatteryChanged retransmite el evento público de DroideCore.
        /// OnBatteryDepleted dispara la muerte por batería (elimina la duplicación
        /// de la lógica de muerte que antes existía en TakeDamage y TakeBatteryHit).
        /// </summary>
        public void InjectBattery(DroideBatteryController bc)
        {
            _batteryCtrl = bc;
            _batteryCtrl.OnBatteryChanged  += value => OnBatteryChanged?.Invoke(value);
            _batteryCtrl.OnBatteryDepleted += HandleBatteryDepleted;
        }

        private void HandleBatteryDepleted()
        {
            if (State == DroideState.Dead || State == DroideState.Victory) return;
            LastDeathCause = DeathCause.Battery;
            TriggerDeath();
        }

        // ═════════════════════════════════════════════════════
        //  DETECCIÓN DE TILES
        // ═════════════════════════════════════════════════════

        private void HandleTileEntered(TileComponent tile, Vector3Int previousCoord)
        {
            GridCoord = _tileDetector.LastProcessedCoord;
            ProcessTileEffect(tile, previousCoord);
        }

        private void HandleTileMissed()
        {
            if (State == DroideState.Dead) return;
            LastDeathCause = DeathCause.Fall;
            TriggerDeath();
        }

        private void ProcessTileEffect(TileComponent tile, Vector3Int previousCoord)
        {
            switch (tile.tileType)
            {
                case TileType.ArrowTile:
                    _movementDecider?.ApplyArrowDirection(tile, previousCoord);
                    break;

                case TileType.LaserTile when tile.isActive:
                    TakeDamage(batteryPenalty);
                    break;

                case TileType.ChargeTile:
                    _stateMachine?.RequestTransition(PlayerStateType.Friction, this);
                    SetState(DroideState.Charging);
                    // F2-T1: llamada directa a IDroideAnimator (DIP). Sin evento intermedio.
                    _droideAnimator?.ForceScanAnimation();
                    var chargeEffect = tile.GetComponentInChildren<EnergiaTileEffect>();
                    chargeEffect?.StartDraining();
                    break;

                case TileType.GoalTile:
                    if (GameStateManager.Instance != null &&
                        GameStateManager.Instance.TerminalsHackedThisRun >= GameStateManager.RequiredTerminalHacks)
                    {
                        _rb.velocity = Vector3.zero;
                        SetState(DroideState.Victory);
                        OnVictory?.Invoke();
                    }
                    else
                    {
                        _rb.velocity = -transform.forward * speed * 0.5f;
                    }
                    break;

                case TileType.PortalTile:
                    var portalComp = tile.GetComponent<PortalTileComponent>();
                    if (portalComp == null || !portalComp.IsCompleted)
                    {
                        _rb.velocity = Vector3.zero;
                        SetState(DroideState.AtPortal);
                        OnPortalEntered?.Invoke(tile, Direction);
                    }
                    break;
            }
        }

        // ═════════════════════════════════════════════════════
        //  VELOCIDAD
        // ═════════════════════════════════════════════════════

        private void ApplyVelocity()
        {
            if (!_shouldMove)
            {
                _rb.velocity = Vector3.zero;
                if (!_isStuckInCharge && State == DroideState.Moving)
                    SetState(DroideState.IdleBetweenTiles);
                return;
            }

            if (!_isStuckInCharge && generator != null &&
                generator.GetTile(GridCoord + Direction) == null)
            {
                _rb.velocity = Vector3.zero;
                return;
            }

            float effectiveSpeed = _isStuckInCharge
                ? speed * chargeSpeedMultiplier
                : speed;

            _rb.velocity = transform.forward * effectiveSpeed;

            if (!_isStuckInCharge && State != DroideState.Moving)
                SetState(DroideState.Moving);
        }

        // ═════════════════════════════════════════════════════
        //  INPUT
        // ═════════════════════════════════════════════════════

        private static bool GetInputDown()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0)) return true;
            foreach (var t in UnityEngine.Input.touches)
                if (t.phase == TouchPhase.Began) return true;
            return false;
        }

        private static bool GetInputUp()
        {
            if (UnityEngine.Input.GetMouseButtonUp(0)) return true;
            foreach (var t in UnityEngine.Input.touches)
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) return true;
            return false;
        }

        private static bool GetInputHeld()
        {
            if (UnityEngine.Input.GetMouseButton(0)) return true;
            foreach (var t in UnityEngine.Input.touches)
                if (t.phase == TouchPhase.Stationary || t.phase == TouchPhase.Moved) return true;
            return false;
        }

        private void HandleHoldAndPulse()
        {
            bool pressed  = GetInputDown();
            bool released = GetInputUp();
            bool held     = GetInputHeld();

            if (_mobileHandler == null)
            {
                if (pressed)  _stateMachine?.OnPressStart(this);
                if (released) _stateMachine?.OnPressEnd(this);
            }

            if (held)
            {
                _holdTimer += Time.deltaTime;
                if (!_holdRotateFired && _holdTimer >= HOLD_THRESHOLD)
                {
                    _holdRotateFired = true;
                    TryRotateArrow();
                }
            }
            if (released) { _holdTimer = 0f; _holdRotateFired = false; }

            // F2-T2: TryFireElectricPulse() centraliza cooldown.
            // Solo actúa en path raw-input (sin MobileInputHandler).
            if (_mobileHandler == null && pressed && !_isStuckInCharge && !_holdRotateFired)
                TryFireElectricPulse();
        }

        // ═════════════════════════════════════════════════════
        //  DAÑO Y MUERTE
        // ═════════════════════════════════════════════════════

        private void HandleLaserActivated(Vector3Int laserCoord)
        {
            if (laserCoord != GridCoord) return;
            if (State == DroideState.Dead || State == DroideState.Victory ||
                State == DroideState.AtPortal) return;
            TakeDamage(batteryPenalty);
        }

        private void TakeDamage(int amount)
        {
            if (State == DroideState.Dead || State == DroideState.Victory) return;
            _isStuckInCharge = false;
            LastDeathCause   = DeathCause.Laser;

            // F1-T1: Drain delega en _batteryCtrl.
            // Si llega a 0, _batteryCtrl emite OnBatteryDepleted
            // → HandleBatteryDepleted() → TriggerDeath(). Sin duplicación.
            _rb.AddForce(-transform.forward * knockbackForce, ForceMode.Impulse);
            _batteryCtrl?.Drain(amount);

            // Solo cambiar a Idle si el Drain no disparó muerte.
            if (State != DroideState.Dead)
                SetState(DroideState.IdleBetweenTiles);
        }

        private void TriggerDeath()
        {
            _rb.velocity     = Vector3.zero;
            _isStuckInCharge = false;
            SetState(DroideState.Dead);
            OnDied?.Invoke(LastDeathCause);
        }

        // ═════════════════════════════════════════════════════
        //  API PÚBLICA — IDroideContext
        // ═════════════════════════════════════════════════════

        public void SetShouldMove(bool value)              => _shouldMove = value;
        public void SetIsStuckInCharge(bool value)         => _isStuckInCharge = value;
        public void SetMoveDurationOverride(float value)   => _moveDurationOverride = value;

        public void TakeBatteryHit(int amount)
        {
            // F1-T1: Drain emite OnBatteryChanged vía _batteryCtrl.OnBatteryChanged.
            // OnBatteryDepleted disparará HandleBatteryDepleted si llega a 0.
            _batteryCtrl?.Drain(amount);

            float ratio = Battery / (float)batteryLimit;
            var tile    = generator.GetTile(GridCoord);
            var effect  = tile?.GetComponentInChildren<EnergiaTileEffect>();
            effect?.UpdateBatteryColor(ratio);
        }

        public void ForceKill(DeathCause cause)
        {
            LastDeathCause = cause;
            TriggerDeath();
        }

        public void RequestStateTransition(PlayerStateType stateType)
        {
            _stateMachine?.RequestTransition(stateType, this);
        }

        // ═════════════════════════════════════════════════════
        //  API PÚBLICA — otros sistemas
        // ═════════════════════════════════════════════════════

        /// <summary>Llamado por MobileInputHandler vía evento.</summary>
        public void OnPressStart() => _stateMachine?.OnPressStart(this);
        /// <summary>Llamado por MobileInputHandler vía evento.</summary>
        public void OnPressEnd()   => _stateMachine?.OnPressEnd(this);

        public void RegisterChargeClick()
        {
            // F1-T1: Charge emite OnBatteryChanged vía _batteryCtrl.OnBatteryChanged.
            _batteryCtrl?.Charge(chargeClickBoost);
            var tile   = generator.GetTile(GridCoord);
            var effect = tile?.GetComponentInChildren<EnergiaTileEffect>();
            effect?.TriggerTapBurst();
        }

        /// <summary>
        /// Reposiciona al droide tras regresar del minijuego de portal.
        /// Llamado exclusivamente por DroidePortalHandler (→ GameFlowManager).
        /// </summary>
        public void RestoreFromPortal(Vector3Int coord, Vector3Int direction)
        {
            Direction = direction;
            var dir3      = new Vector3(direction.x, 0f, direction.z).normalized;
            var targetRot = Quaternion.LookRotation(dir3, Vector3.up);

            transform.position  = ProceduralGridGenerator.CoordToWorld(coord) + Vector3.up * 1.1f;
            transform.rotation  = targetRot;
            _pendingRotation    = targetRot;
            _hasPendingRotation = true;

            GridCoord = coord;
            _tileDetector?.Reset(coord, direction);

            ExitPortal();
        }

        /// <summary>Inyecta la posición pendiente para FixedUpdate (llamado por DroideMovementDecider).</summary>
        public void EnqueuePosition(Vector3 pos)
        {
            _pendingPosition    = pos;
            _hasPendingPosition = true;
        }

        /// <summary>Inyecta la rotación pendiente para FixedUpdate (llamado por DroideMovementDecider).</summary>
        public void EnqueueRotation(Quaternion rot)
        {
            _pendingRotation    = rot;
            _hasPendingRotation = true;
        }

        /// <summary>Actualiza la dirección lógica del droide (llamado por DroideMovementDecider).</summary>
        public void SetDirection(Vector3Int dir) => Direction = dir;

        /// <summary>Expone el TileDetector para que DroidePortalHandler pueda llamar Reset().</summary>
        public TileDetector GetTileDetector() => _tileDetector;

        // ── Pulso de luz (sin toggle de láseres) ─────────────
        /// <summary>
        /// Solo dispara el efecto de luz (LightPulse + Shockwave).
        /// No alterna láseres. Llamado por MobileInputHandler en tap/release.
        /// </summary>
        public void TriggerLightPulse() => OnVFXPulseRequested?.Invoke();

        // ── Pulso eléctrico ───────────────────────────────────

        /// <summary>
        /// F2-T2: Única fuente de verdad del cooldown del pulso.
        /// SRP: quien dispara (MobileInputHandler, HandleHoldAndPulse) no
        /// decide si puede dispararse — esa es responsabilidad del Droide.
        /// DRY: un solo timer _lastPulseTime, una sola verificación.
        /// Retorna true si el pulso se disparó, false si aún está en cooldown.
        /// </summary>
        public bool TryFireElectricPulse()
        {
            if (_isStuckInCharge) return false;
            if (!HasLaserAtRangeOne()) return false;
            if (Time.unscaledTime - _lastPulseTime < pulseCooldown) return false;

            _lastPulseTime = Time.unscaledTime;
            TriggerElectricPulse();
            return true;
        }

        /// <summary>
        /// F2-T2: Expone el estado del cooldown para feedback visual en MobileInputHandler.
        /// MobileInputHandler ya no necesita su propio _lastPulseTime.
        /// </summary>
        public bool IsElectricPulseReady =>
            Time.unscaledTime - _lastPulseTime >= pulseCooldown;

        public void TriggerElectricPulse()
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dz) > 1) continue;
                ToggleLaserAt(GridCoord + new Vector3Int(dx, 0, dz));
            }
            OnVFXPulseRequested?.Invoke();
        }

        public event Action OnVFXPulseRequested;

        public void PulseAdjacentTiles()
        {
            Vector3Int[] neighbors =
            {
                GridCoord + new Vector3Int( 1, 0,  0),
                GridCoord + new Vector3Int(-1, 0,  0),
                GridCoord + new Vector3Int( 0, 0,  1),
                GridCoord + new Vector3Int( 0, 0, -1)
            };
            foreach (var coord in neighbors)
                generator.GetTile(coord)?.PulseEmission();
        }

        // ── Flecha ────────────────────────────────────────────
        public void TryRotateArrow()
        {
            var tile = generator.GetTile(GridCoord);
            if (tile == null || tile.tileType != TileType.ArrowTile) return;

            var oldDir  = tile.arrowDirection;
            var nextDir = (MoveDirection)(((int)oldDir + 1) % 4);
            var nextVec = TileComponent.DirectionToVector(nextDir);

            var prevCoord = _tileDetector?.PreviousCoord ?? GridCoord - Direction;
            if (GridCoord + nextVec == prevCoord)
            {
                Debug.Log($"[ROTATE] Bloqueado: {oldDir}→{nextDir} apunta a prevCoord={prevCoord}");
                return;
            }

            tile.RotateArrow90Degrees();
            _movementDecider?.ApplyArrowDirection(tile, prevCoord, fromPlayerRotation: true);
        }

        // ── Láser ─────────────────────────────────────────────
        public bool HasLaserAtRangeOne()
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dz) > 1) continue;
                var t = generator.GetTile(GridCoord + new Vector3Int(dx, 0, dz));
                if (t != null && t.tileType == TileType.LaserTile && t.isActive)
                    return true;
            }
            return false;
        }

        // F2-T1: OnScanAnimationRequested eliminado.
        // DroideCore llama _droideAnimator.ForceScanAnimation() directamente (DIP).
        // DroideAnimator ya no necesita suscribirse a un evento intermedio.

        // ═════════════════════════════════════════════════════
        //  HELPERS PRIVADOS
        // ═════════════════════════════════════════════════════

        private void ExitPortal()
        {
            _isStuckInCharge    = false;
            LastDeathCause      = DeathCause.None;
            _rb.velocity        = Vector3.zero;
            _hasPendingPosition = false;

            _stateMachine?.ResetToInitialState(this);
            SetState(DroideState.IdleBetweenTiles);
        }

        private void ToggleLaserAt(Vector3Int coord)
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

        private static Vector3Int WorldToCoord(Vector3 world) =>
            new(Mathf.RoundToInt(world.x), 0, Mathf.RoundToInt(world.z));
    }
}
