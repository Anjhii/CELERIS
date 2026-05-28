// ============================================================
// DroideController.cs  |  Assets/Scripts/Player/
//
// ESCENA: GameObject "Droide" (cubo 0.6,0.6,0.6) en GameplayScene.
// INSPECTOR: Asignar ProceduralGridGenerator.
//            Ajustar chargeDrainRate y batteryPenalty al gusto.
// ============================================================
using System.Collections;
using Celeris.Data;
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Player
{
    public class DroideController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Grid")]
        public ProceduralGridGenerator generator;

        [Header("Timing")]
        public float moveDuration      = 0.30f;
        public float idleDuration      = 0.15f;

        [Header("Batería")]
        [Tooltip("Valor inicial Y máximo. El 100% de carga equivale a este valor.")]
        public int startBattery        = 20;

        [Header("Fallo — Láser / Sin batería")]
        [Tooltip("Batería que se resta al activarse ResetLevel")]
        public int   batteryPenalty    = 5;
        [Tooltip("Duración de la animación de vuelta al inicio tras fallo")]
        public float resetMoveDuration = 0.60f;

        [Header("Tile de Carga (Stress Test)")]
        [Tooltip("Unidades de batería drenadas por segundo mientras se está en ChargeTile")]
        public float chargeDrainRate   = 3f;
        [Tooltip("Batería que suma cada tap del jugador durante la carga")]
        public int   chargeClickBoost  = 1;

        // ── Estado público ────────────────────────────────────
        public DroideState State      { get; private set; } = DroideState.IdleBetweenTiles;
        public Vector2Int  GridCoord  { get; private set; }
        public int         Battery    { get; private set; }
        public int         MaxBattery => startBattery;   // para normalizar UI

        // ── Eventos ───────────────────────────────────────────
        public event System.Action<DroideState>   OnStateChanged;
        public event System.Action<int>           OnBatteryChanged;   // valor nuevo
        public event System.Action<TileComponent> OnTileEntered;
        public event System.Action                OnLevelReset;       // flash rojo de fallo

        // ── Privado ───────────────────────────────────────────
        private Vector2Int _direction   = new(0, 1);   // Norte por defecto
        private bool       _running     = false;
        private bool       _isCharging      = false;
        private bool       _advanceRequested = false; // señal de salida desde ReadyToAdvance
        private Coroutine  _drainCoroutine  = null;  // referencia explícita para poder parar el drenaje
        private int        _loopId          = 0;     // ID de generación: impide loops zombi tras ResetLevel

        // ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (generator != null) generator.OnGridReady += Init;
        }

        private void OnDisable()
        {
            if (generator != null) generator.OnGridReady -= Init;
        }

        // ── Init ─────────────────────────────────────────────
        private void Init()
        {
            transform.position = generator.StartWorldPos + Vector3.up * 1.1f;
            GridCoord  = WorldToCoord(generator.StartWorldPos);
            Battery    = startBattery;
            _direction = new Vector2Int(0, 1);

            SetState(DroideState.IdleBetweenTiles);
            StartCoroutine(MovementLoop());
        }

        // ── Loop principal ────────────────────────────────────
        // _loopId evita que un loop antiguo (pausado dentro de una corrutina)
        // continúe ejecutándose después de que ResetLevel arranque uno nuevo.
        private IEnumerator MovementLoop()
        {
            _running = true;
            int myId = ++_loopId;

            while (_running && myId == _loopId)
            {
                yield return new WaitForSeconds(idleDuration);
                if (myId != _loopId) yield break;

                // ── Calcular próximo tile ──────────────────────
                var current = generator.GetTile(GridCoord);
                if (current != null)
                    _direction = current.GetExitDirection(_direction);

                Vector2Int nextCoord = GridCoord + _direction;
                var next = generator.GetTile(nextCoord);

                // Caída al vacío → muerte permanente
                if (next == null || next.tileType == TileType.VoidTile)
                {
                    yield return StartCoroutine(Die());
                    yield break;
                }

                // ── Mover ─────────────────────────────────────
                yield return StartCoroutine(MoveToTile(nextCoord));
                if (myId != _loopId) yield break;

                // ── Consumir batería por movimiento ───────────
                Battery = Mathf.Max(0, Battery - 1);
                OnBatteryChanged?.Invoke(Battery);
                if (Battery <= 0)
                {
                    yield return StartCoroutine(Die());
                    yield break;
                }

                // ── Efecto del tile ───────────────────────────
                ExitTileCleanup();   // limpieza obligatoria al salir del tile actual
                yield return StartCoroutine(HandleTileEffect(next));
                if (myId != _loopId) yield break;  // ResetLevel arrancó loop nuevo
            }
        }

        // ── Interpolación de movimiento ───────────────────────
        private IEnumerator MoveToTile(Vector2Int target)
        {
            SetState(DroideState.Moving);
            Vector3 origin = transform.position;
            Vector3 dest   = generator.CoordToWorld(target) + Vector3.up * 1.1f;
            yield return LerpPosition(origin, dest, moveDuration);
            GridCoord = target;
            OnTileEntered?.Invoke(generator.GetTile(target));
            SetState(DroideState.IdleBetweenTiles);
        }

        // ── Efectos por tipo de tile ──────────────────────────
        private IEnumerator HandleTileEffect(TileComponent tile)
        {
            switch (tile.tileType)
            {
                case TileType.GoalTile:
                    yield return StartCoroutine(Victory());
                    break;

                // Láser activo → no muerte, sino Reset con penalización
                case TileType.LaserTile when tile.isActive:
                    yield return StartCoroutine(ResetLevel());
                    break;

                case TileType.ChargeTile:
                    yield return StartCoroutine(ChargingRoutine());
                    break;

                case TileType.ArrowTile:
                    SetState(DroideState.Moving);
                    yield return null;
                    break;
            }
        }

        // ── Carga: Stress Test ────────────────────────────────
        // Batería se drena constantemente.
        // El jugador hace taps para compensar.
        // Meta: llenar al 100% (startBattery).
        // Fallo: batería llega a 0 → ResetLevel.
        private IEnumerator ChargingRoutine()
        {
            // ── Detener cualquier drenaje previo ──────────────
            // Cubre el caso de tiles de carga consecutivos:
            // la corrutina vieja se para explícitamente antes
            // de que la nueva empiece, eliminando drains paralelos.
            StopDrain();

            SetState(DroideState.Charging);
            _running = false;
            _isCharging = true;

            // Guardar referencia para poder detenerla cuando sea necesario
            _drainCoroutine = StartCoroutine(BatteryDrainRoutine());

            // State lock: el droide permanece en Charging hasta llenarse (éxito)
            // o vaciarse (fallo). Ningún otro camino puede sacar al droide de aquí.
            yield return new WaitUntil(() => Battery >= startBattery || Battery <= 0);

            // Limpieza garantizada antes de cualquier bifurcación
            StopDrain();

            if (Battery >= startBattery)
            {
                // ── Éxito: entrar en fase de espera ───────────
                // El jugador decide cuándo reanudar (tap, hold+pulso o timeout 1.5s).
                yield return StartCoroutine(ReadyToAdvanceRoutine());
            }
            else
            {
                // ── Fallo: resetear nivel ─────────────────────
                yield return StartCoroutine(ResetLevel());
            }
        }

        // ── Phase 2: Espera activa tras carga completa ────────
        // El Droide queda en pausa con feedback visual (BatteryUI cian).
        // Dos salidas posibles:
        //   • Hold 0.5s    → TriggerElectricPulseExtended() + ConfirmAdvance()
        //                    inmediato desde MobileInputHandler.HandleHold.
        //   • Timeout 1.5s → avance automático sin input del jugador.
        // El Tap simple NO hace nada aquí: el avance siempre es automático.
        private IEnumerator ReadyToAdvanceRoutine()
        {
            _advanceRequested = false;
            SetState(DroideState.ReadyToAdvance);

            float elapsed = 0f;
            const float autoAdvanceTimeout = 1.5f;

            while (!_advanceRequested && elapsed < autoAdvanceTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Salida limpia: ExitTileCleanup() ya llamó StopDrain en ChargingRoutine,
            // pero lo repetimos aquí para cubrir cualquier estado residual.
            ExitTileCleanup();

            _running = true;
            SetState(DroideState.IdleBetweenTiles);
            StartCoroutine(MovementLoop());
        }

        // Corrutina independiente: drena la batería mientras _isCharging == true
        private IEnumerator BatteryDrainRoutine()
        {
            float accumulated = 0f;
            while (_isCharging && Battery > 0)
            {
                accumulated += chargeDrainRate * Time.deltaTime;
                if (accumulated >= 1f)
                {
                    int drain  = Mathf.FloorToInt(accumulated);
                    accumulated -= drain;
                    Battery    = Mathf.Max(0, Battery - drain);
                    OnBatteryChanged?.Invoke(Battery);
                }
                yield return null;
            }
        }

        // ── Reset de nivel ────────────────────────────────────
        // Activa: láser sin desactivar, batería a 0 durante carga.
        // Efecto: penalización + vuelta al inicio + nuevo loop.
        private IEnumerator ResetLevel()
        {
            _running = false;
            StopDrain();          // seguridad: para el drenaje si venimos de ChargeTile
            SetState(DroideState.Dead);
            OnLevelReset?.Invoke();

            // Penalización
            Battery = Mathf.Max(0, Battery - batteryPenalty);
            OnBatteryChanged?.Invoke(Battery);

            if (Battery <= 0)
            {
                // Sin batería suficiente → muerte permanente
                yield break;
            }

            // Volver al inicio con animación
            Vector3 startPos = generator.StartWorldPos + Vector3.up * 1.1f;
            yield return LerpPosition(transform.position, startPos, resetMoveDuration);

            transform.position = startPos;
            GridCoord          = WorldToCoord(generator.StartWorldPos);
            _direction         = new Vector2Int(0, 1);

            SetState(DroideState.IdleBetweenTiles);
            StartCoroutine(MovementLoop()); // _loopId++ aquí → el loop antiguo se autodestruye
        }

        // ── Muerte y Victoria ─────────────────────────────────
        private IEnumerator Die()
        {
            _running = false;
            SetState(DroideState.Dead);
            yield return null;
        }

        private IEnumerator Victory()
        {
            _running = false;
            SetState(DroideState.Victory);
            yield return null;
        }

        // ── API pública (llamada desde MobileInputHandler) ────

        /// <summary>
        /// Pulso estándar (Rango 1): desactiva Laser tiles en los 4 vecinos
        /// inmediatos (Manhattan ≤ 1). Usado por Tap en movimiento normal.
        /// </summary>
        public void TriggerElectricPulse()
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue;
                ToggleLaserAt(GridCoord + new Vector2Int(dx, dy));
            }
        }

        /// <summary>
        /// Pulso extendido (Rango 2): rango 1 + tile a 2 pasos en la dirección
        /// de movimiento actual. Exclusivo del Hold durante ReadyToAdvance.
        /// </summary>
        public void TriggerElectricPulseExtended()
        {
            TriggerElectricPulse();                          // rango 1
            ToggleLaserAt(GridCoord + _direction * 2);       // + rango 2
        }

        /// <summary>
        /// Señal de avance desde el estado ReadyToAdvance.
        /// Llamar tras Tap corto o tras soltar un Hold en ese estado.
        /// </summary>
        public void ConfirmAdvance()
        {
            if (State != DroideState.ReadyToAdvance) return;
            _advanceRequested = true;
        }

        /// <summary>Suma batería durante el Stress Test de carga.</summary>
        public void RegisterChargeClick()
        {
            if (State != DroideState.Charging) return;
            Battery = Mathf.Min(startBattery, Battery + chargeClickBoost);
            OnBatteryChanged?.Invoke(Battery);
        }

        /// <summary>Rota la flecha del tile en la posición actual del droide.</summary>
        public void RotateCurrentArrow()
        {
            var tile = generator.GetTile(GridCoord);
            if (tile != null && tile.tileType == TileType.ArrowTile)
            {
                SetState(DroideState.RotatingArrow);
                tile.RotateArrow90Degrees();
            }
        }

        /// <summary>
        /// Limpieza obligatoria al abandonar un tile.
        /// Para cualquier drenaje activo y normaliza el estado si quedó
        /// colgado en Charging o RotatingArrow.
        /// </summary>
        public void ExitTileCleanup()
        {
            StopDrain();
            if (State == DroideState.Charging || State == DroideState.RotatingArrow)
                SetState(DroideState.IdleBetweenTiles);
        }

        /// <summary>
        /// True si hay un LaserTile activo en Manhattan ≤ 1 (4 vecinos inmediatos).
        /// Condición necesaria para que un Tap en movimiento normal dispare el pulso.
        /// </summary>
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

        /// <summary>
        /// True si hay un LaserTile activo en Manhattan ≤ 1 O a 2 pasos
        /// en la dirección de movimiento. Usado para el Hold en ReadyToAdvance.
        /// </summary>
        public bool HasLaserInLookahead()
        {
            if (HasLaserAtRangeOne()) return true;
            var ahead2 = generator.GetTile(GridCoord + _direction * 2);
            return ahead2 != null && ahead2.tileType == TileType.LaserTile && ahead2.isActive;
        }

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Alterna el estado de un LaserTile en la coordenada dada (null-safe).
        /// </summary>
        private void ToggleLaserAt(Vector2Int coord)
        {
            var tile = generator.GetTile(coord);
            if (tile != null && tile.tileType == TileType.LaserTile)
                tile.ToggleLaser();
        }

        /// <summary>
        /// Para el drenaje de batería de forma explícita y segura.
        /// Usar StopCoroutine en lugar de depender del flag para evitar
        /// que dos instancias de BatteryDrainRoutine corran en paralelo.
        /// </summary>
        private void StopDrain()
        {
            if (_drainCoroutine != null)
            {
                StopCoroutine(_drainCoroutine);
                _drainCoroutine = null;
            }
            _isCharging = false;
        }

        private IEnumerator LerpPosition(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            transform.position = to;
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
