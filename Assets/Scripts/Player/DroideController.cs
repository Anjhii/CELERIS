// ============================================================
// DroideController.cs  |  Assets/Scripts/Player/
//
// ESCENA: Crear un GameObject "Droide" (cubo placeholder
//         escalado 0.6,0.6,0.6) en GameplayScene y adjuntar
//         este script.
// INSPECTOR: Asignar referencia a ProceduralGridGenerator.
//            Los valores de timing tienen defaults listos.
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
        [Header("Referencia al Grid")]
        public ProceduralGridGenerator generator;

        [Header("Timing (segundos)")]
        public float moveDuration  = 0.30f;
        public float idleDuration  = 0.15f;

        [Header("Batería inicial")]
        public int startBattery = 20;

        // ── Estado público ────────────────────────────────────
        public DroideState State     { get; private set; } = DroideState.IdleBetweenTiles;
        public Vector2Int  GridCoord { get; private set; }
        public int         Battery   { get; private set; }

        // ── Eventos ───────────────────────────────────────────
        public event System.Action<DroideState>     OnStateChanged;
        public event System.Action<int>             OnBatteryChanged;   // nuevo valor
        public event System.Action<TileComponent>   OnTileEntered;

        // ── Privado ───────────────────────────────────────────
        private Vector2Int _direction = new(0, 1);   // Norte por defecto
        private bool       _running   = false;
        private int        _chargeClicksRequired = 3;
        private int        _chargeClicksReceived = 0;

        // ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (generator != null)
                generator.OnGridReady += Init;
        }

        private void OnDisable()
        {
            if (generator != null)
                generator.OnGridReady -= Init;
        }

        // ── Init: espera a que el grid esté listo ─────────────
        private void Init()
        {
            // Posición spawn = tile central del segmento Start
            transform.position = generator.StartWorldPos + Vector3.up * 1.1f;

            // Coordenada lógica inicial
            GridCoord = WorldToCoord(generator.StartWorldPos);
            Battery   = startBattery;

            SetState(DroideState.IdleBetweenTiles);
            StartCoroutine(MovementLoop());
        }

        // ── Loop principal: avanza tile a tile ───────────────
        private IEnumerator MovementLoop()
        {
            _running = true;
            while (_running)
            {
                yield return new WaitForSeconds(idleDuration);

                // Determinar próxima coordenada
                var currentTile = generator.GetTile(GridCoord);
                if (currentTile != null)
                    _direction = currentTile.GetExitDirection(_direction);

                Vector2Int nextCoord = GridCoord + _direction;
                var nextTile = generator.GetTile(nextCoord);

                // Sin tile o VoidTile → muerte
                if (nextTile == null || nextTile.tileType == TileType.VoidTile)
                {
                    yield return StartCoroutine(Die());
                    yield break;
                }

                // Mover
                yield return StartCoroutine(MoveToTile(nextCoord));

                // Consumir batería
                Battery = Mathf.Max(0, Battery - 1);
                OnBatteryChanged?.Invoke(Battery);
                if (Battery <= 0)
                {
                    yield return StartCoroutine(Die());
                    yield break;
                }

                // Reaccionar al tile de destino
                yield return StartCoroutine(HandleTileEffect(nextTile));
            }
        }

        // ── Interpolación de movimiento ───────────────────────
        private IEnumerator MoveToTile(Vector2Int target)
        {
            SetState(DroideState.Moving);

            Vector3 origin = transform.position;
            Vector3 dest   = generator.CoordToWorld(target) + Vector3.up * 1.1f;

            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(origin, dest, elapsed / moveDuration);
                yield return null;
            }
            transform.position = dest;
            GridCoord = target;
            OnTileEntered?.Invoke(generator.GetTile(target));
            SetState(DroideState.IdleBetweenTiles);
        }

        // ── Efectos según tipo de tile ────────────────────────
        private IEnumerator HandleTileEffect(TileComponent tile)
        {
            switch (tile.tileType)
            {
                case TileType.GoalTile:
                    yield return StartCoroutine(Victory());
                    break;

                case TileType.LaserTile when tile.isActive:
                    // Laser activo = muerte instantánea
                    yield return StartCoroutine(Die());
                    break;

                case TileType.ChargeTile:
                    yield return StartCoroutine(ChargingRoutine());
                    break;

                case TileType.ArrowTile:
                    // La dirección ya se actualizó en el loop; estado visual breve
                    SetState(DroideState.Moving);
                    yield return null;
                    break;
            }
        }

        // ── Carga: el droide se detiene hasta completar clicks ─
        private IEnumerator ChargingRoutine()
        {
            SetState(DroideState.Charging);
            _chargeClicksReceived = 0;
            _running              = false;   // pausa el loop

            // El input notificará RegisterChargeClick()
            yield return new WaitUntil(() =>
                _chargeClicksReceived >= _chargeClicksRequired);

            // Recargar batería (máximo = startBattery)
            Battery = Mathf.Min(startBattery, Battery + _chargeClicksRequired);
            OnBatteryChanged?.Invoke(Battery);

            _running = true;
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

        // ── API para el Input Handler ─────────────────────────

        /// <summary>Pulso: activa/desactiva tiles de Laser/Resonance a distancia Manhattan ≤1.</summary>
        public void TriggerElectricPulse()
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue;   // solo Manhattan=1
                var neighbor = generator.GetTile(GridCoord + new Vector2Int(dx, dy));
                if (neighbor == null) continue;
                if (neighbor.tileType == TileType.LaserTile)     neighbor.ToggleLaser();
                if (neighbor.tileType == TileType.ResonanceTile) neighbor.ToggleResonance();
            }
        }

        /// <summary>Registra un click de recarga (llamado desde MobileInputHandler).</summary>
        public void RegisterChargeClick() => _chargeClicksReceived++;

        /// <summary>Rota la flecha del tile actual (llamado desde MobileInputHandler).</summary>
        public void RotateCurrentArrow()
        {
            var tile = generator.GetTile(GridCoord);
            if (tile != null && tile.tileType == TileType.ArrowTile)
            {
                SetState(DroideState.RotatingArrow);
                tile.RotateArrow90Degrees();
            }
        }

        // ── Helpers ───────────────────────────────────────────
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
