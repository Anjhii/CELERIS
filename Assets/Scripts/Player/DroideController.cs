// ============================================================
// DroideController.cs  |  Assets/Scripts/Player/
//
// v5 — Movimiento continuo por Rigidbody, sin corrutinas.
//
// MOVIMIENTO:
//   Input.GetMouseButton(0) presionado → rb.velocity = transform.forward * speed
//   Soltar                             → rb.velocity = Vector3.zero
//
// CHARGE TILE (imán):
//   Al pisar ChargeTile → isStuckInCharge = true
//     • Velocidad al 10% (chargeSpeedMultiplier)
//     • Batería drena con Time.deltaTime (chargeDrainRate)
//     • Cada GetMouseButtonDown(0) suma tapCount
//     • tapCount >= tapEscapeThreshold → isStuckInCharge = false, velocidad normal
//   Estado Charging → DroideAnimator usa isReadyToAdvance como feedback visual
//
// PORTAL:
//   ExitPortal() → estado Normal, isReadyToAdvance = true brevemente,
//                  desplaza una unidad hacia adelante para salir del tile
//
// DETECCIÓN DE TILES:
//   Se basa en coordenadas de grid (sin triggers adicionales).
//   WorldToCoord() determina qué tile ocupa el Droide cada frame.
//   Cuando el coord cambia, se procesa el efecto del nuevo tile.
//
// ANIMATOR (parámetros reales):
//   isMoving, idleVariant, isDead, isVictory, isReadyToAdvance, deathType
//   DroideAnimator mapea Charging/AtPortal → isReadyToAdvance.
//   NUNCA se usan isCharging ni isAtPortal.
// ============================================================
using System;
using Celeris.Core;
using Celeris.Data;
using Celeris.Grid;
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
        public int   batteryLimit       = 100;
        [Tooltip("Batería drenada por cada tile que el Droide cruza")]
        public int   batteryDrainPerTile = 5;

        [Header("ChargeTile — Imán")]
        [Tooltip("Multiplicador de velocidad mientras isStuckInCharge (0.1 = 10%)")]
        [Range(0.01f, 0.3f)]
        public float chargeSpeedMultiplier = 0.10f;
        [Tooltip("Batería drenada por segundo dentro del ChargeTile")]
        public float chargeDrainRate       = 10f;
        [Tooltip("Número de taps necesarios para escapar del ChargeTile")]
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
        [Tooltip("Segundos de penalización antes de volver al inicio")]
        public float resetDelay            = 0.8f;

        [Header("Pulso Eléctrico")]
        public float pulseCooldown = 3f;

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
        public event Action<DroideState>             OnStateChanged;
        public event Action<int>                     OnBatteryChanged;
        public event Action<TileComponent>           OnTileEntered;
        public event Action                          OnLevelReset;
        public event Action<TileComponent, Vector2Int> OnPortalEntered;

        // ── Privado — física ──────────────────────────────────
        private Rigidbody  _rb;

        // ── Privado — grid ────────────────────────────────────
        private Vector2Int _direction       = new(0, 1);
        private Vector2Int _lastProcessedCoord;

        // ── Privado — ChargeTile ──────────────────────────────
        private bool  _isStuckInCharge    = false;
        private int   _tapCount           = 0;
        private float _chargeBatteryAcc   = 0f;

        // ── Privado — hold para rotar ─────────────────────────
        private float _holdTimer          = 0f;
        private bool  _holdRotateFired    = false;
        private const float HOLD_THRESHOLD = 0.45f;

        // ── Privado — pulso ───────────────────────────────────
        private float _lastPulseTime      = -999f;

        // ── Privado — reset delay ─────────────────────────────
        private bool  _inResetDelay       = false;
        private float _resetTimer         = 0f;

        // ── Privado — state machine ───────────────────────────
        private IPlayerState _currentState;
        private bool         _shouldMove           = false;
        private float        _moveDurationOverride = -1f;

        // ─────────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            _rb.useGravity     = false;   // movimiento en plano XZ, Y fijo
            _rb.constraints    = RigidbodyConstraints.FreezePositionY
                               | RigidbodyConstraints.FreezeRotation;
        }

        private void OnEnable()
        {
            if (generator != null) generator.OnGridReady += Init;
        }

        private void OnDisable()
        {
            if (generator != null) generator.OnGridReady -= Init;
        }

        // ── Inicialización ────────────────────────────────────
        private void Init()
        {
            transform.position    = generator.StartWorldPos + Vector3.up * 1.1f;
            transform.rotation    = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            GridCoord             = WorldToCoord(generator.StartWorldPos);
            _lastProcessedCoord   = GridCoord;
            _direction            = new Vector2Int(0, 1);

            Battery               = batteryLimit;
            _isStuckInCharge      = false;
            _tapCount             = 0;
            _chargeBatteryAcc     = 0f;
            _inResetDelay         = false;
            _resetTimer           = 0f;
            _holdTimer            = 0f;
            _holdRotateFired      = false;
            LastDeathCause        = DeathCause.None;

            _rb.velocity          = Vector3.zero;
            OnBatteryChanged?.Invoke(Battery);
            SetState(DroideState.IdleBetweenTiles);

            Debug.Log("[DroideController] Inicializado. Batería=" + Battery);
        }

        // ── Update principal ──────────────────────────────────
        private void Update()
        {
            if (GameStateManager.IsPaused) { _rb.velocity = Vector3.zero; return; }

            // Delay de reset post-láser (droide muerto brevemente)
            if (_inResetDelay)
            {
                _resetTimer -= Time.deltaTime;
                if (_resetTimer <= 0f) FinishReset();
                return;
            }

            if (State == DroideState.Dead    ||
                State == DroideState.Victory ||
                State == DroideState.AtPortal)
            {
                _rb.velocity = Vector3.zero;
                return;
            }

            HandleInput();
            ApplyVelocity();
            DetectTileEntry();

            if (_isStuckInCharge)
                TickChargeDrain();
        }

        // ── Input ─────────────────────────────────────────────
        private void HandleInput()
        {
            bool held    = UnityEngine.Input.GetMouseButton(0);
            bool pressed = UnityEngine.Input.GetMouseButtonDown(0);
            bool released= UnityEngine.Input.GetMouseButtonUp(0);

            // ── Hold para rotar flecha ────────────────────────
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

            // ── Multi-tap para escapar del ChargeTile ─────────
            if (_isStuckInCharge && pressed)
            {
                _tapCount++;
                if (_tapCount >= tapEscapeThreshold)
                {
                    _isStuckInCharge = false;
                    _tapCount        = 0;
                    _chargeBatteryAcc = 0f;
                    SetState(DroideState.IdleBetweenTiles);
                    Debug.Log("[DroideController] Escapó del ChargeTile.");
                }
            }

            // ── Pulso eléctrico en tap normal ─────────────────
            if (pressed && !_isStuckInCharge && !_holdRotateFired)
            {
                if (HasLaserAtRangeOne() &&
                    Time.unscaledTime - _lastPulseTime >= pulseCooldown)
                {
                    _lastPulseTime = Time.unscaledTime;
                    TriggerElectricPulse();
                }
            }
        }

        // ── Velocidad de Rigidbody ────────────────────────────
        private void ApplyVelocity()
        {
            bool held = UnityEngine.Input.GetMouseButton(0) && !_holdRotateFired;

            if (held)
            {
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
                // Fuera del camino: caída
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

            // Coste de batería por tile cruzado
            Battery = Mathf.Max(0, Battery - batteryDrainPerTile);
            OnBatteryChanged?.Invoke(Battery);
            if (Battery <= 0)
            {
                LastDeathCause = DeathCause.Battery;
                TriggerDeath();
                return;
            }

            ProcessTileEffect(tile);
        }

        // ── Efectos por tipo de tile ──────────────────────────
        private void ProcessTileEffect(TileComponent tile)
        {
            switch (tile.tileType)
            {
                // Flecha: redirige transform.forward y la velocidad
                case TileType.ArrowTile:
                    Vector2Int d = TileComponent.DirectionToVector(tile.arrowDirection);
                    _direction = d;
                    transform.forward = new Vector3(d.x, 0f, d.y).normalized;
                    // Mantener velocidad, cambiar dirección
                    if (_rb.velocity.magnitude > 0.01f)
                        _rb.velocity = transform.forward * _rb.velocity.magnitude;
                    break;

                // Láser activo: penalización y reset
                case TileType.LaserTile when tile.isActive:
                    LastDeathCause = DeathCause.Laser;
                    TriggerLaserReset();
                    break;

                // ChargeTile: atrapar al droide
                case TileType.ChargeTile:
                    if (!_isStuckInCharge)
                    {
                        _isStuckInCharge  = true;
                        _tapCount         = 0;
                        _chargeBatteryAcc = 0f;
                        SetState(DroideState.Charging);   // → animator usa isReadyToAdvance
                        Debug.Log("[DroideController] Atrapado en ChargeTile. Multi-tap para escapar.");
                    }
                    break;

                // Meta
                case TileType.GoalTile:
                    _rb.velocity = Vector3.zero;
                    SetState(DroideState.Victory);
                    break;

                // Portal
                case TileType.PortalTile:
                    var portalComp = tile.GetComponent<PortalTileComponent>();
                    if (portalComp == null || !portalComp.IsCompleted)
                    {
                        _rb.velocity = Vector3.zero;
                        SetState(DroideState.AtPortal);   // → animator usa isReadyToAdvance
                        OnPortalEntered?.Invoke(tile, _direction);
                    }
                    break;
            }
        }

        // ── Drenaje de batería en ChargeTile ──────────────────
        private void TickChargeDrain()
        {
            _chargeBatteryAcc += chargeDrainRate * Time.deltaTime;
            if (_chargeBatteryAcc < 1f) return;

            int drain         = Mathf.FloorToInt(_chargeBatteryAcc);
            _chargeBatteryAcc -= drain;
            Battery            = Mathf.Max(0, Battery - drain);
            OnBatteryChanged?.Invoke(Battery);

            if (Battery <= 0)
            {
                LastDeathCause    = DeathCause.Battery;
                _isStuckInCharge  = false;
                TriggerDeath();
            }
        }

        // ── Reset por láser (penalización + volver al inicio) ─
        private void TriggerLaserReset()
        {
            _rb.velocity     = Vector3.zero;
            _isStuckInCharge = false;
            _tapCount        = 0;

            Battery = Mathf.Max(0, Battery - batteryPenalty);
            OnBatteryChanged?.Invoke(Battery);

            SetState(DroideState.Dead);
            OnLevelReset?.Invoke();

            if (Battery <= 0)
            {
                // Game Over real: no hay batería para volver
                return;
            }

            // Espera breve y vuelve al inicio
            _inResetDelay = true;
            _resetTimer   = resetDelay;
        }

        private void FinishReset()
        {
            _inResetDelay = false;

            transform.position  = generator.StartWorldPos + Vector3.up * 1.1f;
            transform.rotation  = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            GridCoord           = WorldToCoord(generator.StartWorldPos);
            _lastProcessedCoord = GridCoord;
            _direction          = new Vector2Int(0, 1);
            LastDeathCause      = DeathCause.None;

            // Restaura batería completa al reiniciar
            Battery = batteryLimit;
            OnBatteryChanged?.Invoke(Battery);

            _rb.velocity = Vector3.zero;
            SetState(DroideState.IdleBetweenTiles);
        }

        // ── Muerte final ──────────────────────────────────────
        private void TriggerDeath()
        {
            _rb.velocity     = Vector3.zero;
            _isStuckInCharge = false;
            _tapCount        = 0;
            SetState(DroideState.Dead);
        }

        // ── Portal: ExitPortal ────────────────────────────────
        /// <summary>
        /// Restaura el Droide tras regresar del minijuego de portal.
        /// • Fuerza estado Normal (IdleBetweenTiles).
        /// • Emite ReadyToAdvance brevemente para feedback del Animator.
        /// • Desplaza una unidad hacia adelante para salir del tile de portal.
        /// </summary>
        public void ExitPortal()
        {
            _isStuckInCharge  = false;
            _tapCount         = 0;
            _chargeBatteryAcc = 0f;
            LastDeathCause    = DeathCause.None;
            _rb.velocity      = Vector3.zero;

            // Desplazar una unidad hacia adelante para liberar del tile
            transform.position     += transform.forward * 1f;
            _lastProcessedCoord     = WorldToCoord(transform.position);
            GridCoord               = _lastProcessedCoord;

            // Señal visual breve de "listo" antes de volver a Idle
            SetState(DroideState.ReadyToAdvance);   // → isReadyToAdvance = true
            SetState(DroideState.IdleBetweenTiles); // → isReadyToAdvance = false, listo para input

            Debug.Log($"[DroideController] ExitPortal(). Nueva posición: {transform.position}");
        }

        /// <summary>
        /// Versión completa usada por GameFlowManager al volver de portal:
        /// reposiciona en coord exacta y llama ExitPortal().
        /// </summary>
        public void RestoreFromPortal(Vector2Int coord, Vector2Int direction)
        {
            _direction          = direction;
            transform.position  = generator.CoordToWorld(coord) + Vector3.up * 1.1f;
            transform.forward   = new Vector3(direction.x, 0f, direction.y).normalized;
            _lastProcessedCoord = coord;
            GridCoord           = coord;
            ExitPortal();
        }

        // ── API pública ───────────────────────────────────────

        /// <summary>El input externo (MobileInputHandler) notifica press → delega al estado activo.</summary>
        public void OnPressStart()
        {
            _currentState?.OnPressStart(this);
        }

        /// <summary>El input externo (MobileInputHandler) notifica release → delega al estado activo.</summary>
        public void OnPressEnd()
        {
            _currentState?.OnPressEnd(this);
        }

        /// <summary>Rota la flecha del tile actual si el Droide está sobre un ArrowTile.</summary>
        public void RotateCurrentArrow() => TryRotateArrow();

        /// <summary>Activa o desactiva el movimiento continuo (usado por los estados).</summary>
        public void SetShouldMove(bool value) => _shouldMove = value;

        /// <summary>Sobreescribe la duración del paso. Pasar -1f para restaurar el valor base.</summary>
        public void SetMoveDurationOverride(float value) => _moveDurationOverride = value;

        /// <summary>Resta batería sin matar al droide.</summary>
        public void TakeBatteryHit(int amount)
        {
            Battery = Mathf.Max(0, Battery - amount);
            OnBatteryChanged?.Invoke(Battery);
        }

        /// <summary>Mata al droide con la causa indicada.</summary>
        public void ForceKill(DeathCause cause)
        {
            LastDeathCause = cause;
            TriggerDeath();
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
            if (tile != null && tile.tileType == TileType.ArrowTile)
            {
                SetState(DroideState.RotatingArrow);
                tile.RotateArrow90Degrees();
                SetState(DroideState.IdleBetweenTiles);
            }
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
