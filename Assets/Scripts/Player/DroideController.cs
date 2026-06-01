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
        public DroideState State          { get; private set; } = DroideState.IdleBetweenTiles;
        public DeathCause  LastDeathCause { get; private set; } = DeathCause.None;
        public Vector2Int  GridCoord      { get; private set; }
        public int         Battery        { get; private set; }
        public int         MaxBattery     => startBattery;

        // ── Eventos ───────────────────────────────────────────
        public event System.Action<DroideState>   OnStateChanged;
        public event System.Action<int>           OnBatteryChanged;
        public event System.Action<TileComponent> OnTileEntered;
        public event System.Action                OnLevelReset;

        // ── Privado ───────────────────────────────────────────
        private Vector2Int _direction        = new(0, 1);
        private bool       _running          = false;
        private bool       _isCharging       = false;
        private bool       _advanceRequested = false;
        private Coroutine  _drainCoroutine   = null;
        private int        _loopId           = 0;

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
            GridCoord          = WorldToCoord(generator.StartWorldPos);
            Battery            = startBattery;
            _direction         = new Vector2Int(0, 1);
            LastDeathCause     = DeathCause.None;

            SetState(DroideState.IdleBetweenTiles);
            StartCoroutine(MovementLoop());
        }

        // ── Loop principal ────────────────────────────────────
        private IEnumerator MovementLoop()
        {
            _running = true;
            int myId = ++_loopId;

            while (_running && myId == _loopId)
            {
                yield return new WaitForSeconds(idleDuration);
                if (myId != _loopId) yield break;

                var current = generator.GetTile(GridCoord);
                if (current != null)
                    _direction = current.GetExitDirection(_direction);

                Vector2Int nextCoord = GridCoord + _direction;
                var next = generator.GetTile(nextCoord);

                if (next == null || next.tileType == TileType.VoidTile)
                {
                    LastDeathCause = DeathCause.Fall;
                    yield return StartCoroutine(Die());
                    yield break;
                }

                yield return StartCoroutine(MoveToTile(nextCoord));
                if (myId != _loopId) yield break;

                Battery = Mathf.Max(0, Battery - 1);
                OnBatteryChanged?.Invoke(Battery);
                if (Battery <= 0)
                {
                    LastDeathCause = DeathCause.Battery;
                    yield return StartCoroutine(Die());
                    yield break;
                }

                ExitTileCleanup();
                yield return StartCoroutine(HandleTileEffect(next));
                if (myId != _loopId) yield break;
            }
        }

        // ── Interpolación de movimiento + rotación ───────────
        private IEnumerator MoveToTile(Vector2Int target)
        {
            SetState(DroideState.Moving);

            Vector3    origin    = transform.position;
            Vector3    dest      = generator.CoordToWorld(target) + Vector3.up * 1.1f;
            Quaternion startRot  = transform.rotation;
            // Calcular rotación objetivo en Y según _direction (plano XZ)
            Quaternion targetRot = Quaternion.LookRotation(
                new Vector3(_direction.x, 0f, _direction.y), Vector3.up);

            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed            += Time.deltaTime;
                float t             = Mathf.Clamp01(elapsed / moveDuration);
                transform.position  = Vector3.Lerp(origin, dest, t);
                transform.rotation  = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            transform.position = dest;
            transform.rotation = targetRot;

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

        // ── Carga ─────────────────────────────────────────────
        private IEnumerator ChargingRoutine()
        {
            StopDrain();
            SetState(DroideState.Charging);
            _running    = false;
            _isCharging = true;

            _drainCoroutine = StartCoroutine(BatteryDrainRoutine());
            yield return new WaitUntil(() => Battery >= startBattery || Battery <= 0);
            StopDrain();

            if (Battery >= startBattery)
                yield return StartCoroutine(ReadyToAdvanceRoutine());
            else
            {
                LastDeathCause = DeathCause.Battery;
                yield return StartCoroutine(ResetLevel());
            }
        }

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

            ExitTileCleanup();
            _running = true;
            SetState(DroideState.IdleBetweenTiles);
            StartCoroutine(MovementLoop());
        }

        private IEnumerator BatteryDrainRoutine()
        {
            float accumulated = 0f;
            while (_isCharging && Battery > 0)
            {
                accumulated += chargeDrainRate * Time.deltaTime;
                if (accumulated >= 1f)
                {
                    int drain   = Mathf.FloorToInt(accumulated);
                    accumulated -= drain;
                    Battery     = Mathf.Max(0, Battery - drain);
                    OnBatteryChanged?.Invoke(Battery);
                }
                yield return null;
            }
        }

        // ── Reset de nivel ────────────────────────────────────
        private IEnumerator ResetLevel()
        {
            _running = false;
            StopDrain();
            if (LastDeathCause == DeathCause.None)
                LastDeathCause = DeathCause.Laser;

            Battery = Mathf.Max(0, Battery - batteryPenalty);
            OnBatteryChanged?.Invoke(Battery);

            SetState(DroideState.Dead);
            OnLevelReset?.Invoke();

            if (LastDeathCause == DeathCause.Battery)
                yield return new WaitForSeconds(2.0f);

            if (Battery <= 0)
                yield break;

            Vector3 startPos = generator.StartWorldPos + Vector3.up * 1.1f;
            yield return LerpPosition(transform.position, startPos, resetMoveDuration);

            transform.position = startPos;
            transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); // Norte
            GridCoord          = WorldToCoord(generator.StartWorldPos);
            _direction         = new Vector2Int(0, 1);
            LastDeathCause     = DeathCause.None;

            SetState(DroideState.IdleBetweenTiles);
            StartCoroutine(MovementLoop());
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

        // ── API pública ───────────────────────────────────────
        public void TriggerElectricPulse()
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue;
                ToggleLaserAt(GridCoord + new Vector2Int(dx, dy));
            }
        }

        public void TriggerElectricPulseExtended()
        {
            TriggerElectricPulse();
            ToggleLaserAt(GridCoord + _direction * 2);
        }

        public void ConfirmAdvance()
        {
            if (State != DroideState.ReadyToAdvance) return;
            _advanceRequested = true;
        }

        public void RegisterChargeClick()
        {
            if (State != DroideState.Charging) return;
            Battery = Mathf.Min(startBattery, Battery + chargeClickBoost);
            OnBatteryChanged?.Invoke(Battery);
        }

        public void RotateCurrentArrow()
        {
            var tile = generator.GetTile(GridCoord);
            if (tile != null && tile.tileType == TileType.ArrowTile)
            {
                SetState(DroideState.RotatingArrow);
                tile.RotateArrow90Degrees();
            }
        }

        public void ExitTileCleanup()
        {
            StopDrain();
            if (State == DroideState.Charging || State == DroideState.RotatingArrow)
                SetState(DroideState.IdleBetweenTiles);
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

        public bool HasLaserInLookahead()
        {
            if (HasLaserAtRangeOne()) return true;
            var ahead2 = generator.GetTile(GridCoord + _direction * 2);
            return ahead2 != null && ahead2.tileType == TileType.LaserTile && ahead2.isActive;
        }

        // ── Helpers ───────────────────────────────────────────
        private void ToggleLaserAt(Vector2Int coord)
        {
            var tile = generator.GetTile(coord);
            if (tile != null && tile.tileType == TileType.LaserTile)
                tile.ToggleLaser();
        }

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