// ============================================================
// DroideBatteryController.cs  |  Assets/Scripts/Player/
//
// RESPONSABILIDAD ÚNICA:
//   Ser la única fuente de verdad de la batería del Droide.
//   Gestionar drenaje, carga y emisión de eventos de batería.
//
// LO QUE NO HACE (SRP):
//   No sabe nada de física ni de tiles.
//   No sabe nada de estados de movimiento.
//   No decide qué pasa cuando la batería llega a cero —
//   emite OnBatteryDepleted y quien corresponde (DroideCore) reacciona.
//
// USO:
//   DroideCore inyecta este componente y escucha OnBatteryDepleted.
//   FrictionMovementState llama ctx.TakeBatteryHit() que pasa por aquí.
//   DroideCore expone Battery como proxy de CurrentBattery.
//
// NOTA:
//   Esta clase existe como fuente canónica de datos de batería.
//   En el Bloque 1, DroideCore aún gestiona el campo _battery
//   internamente para mantener compatibilidad con IDroideContext.Battery.
//   La integración completa se consolida si se decide mover toda
//   la lógica de batería aquí y eliminar _battery de DroideCore.
// ============================================================
using System;
using UnityEngine;

namespace Celeris.Player
{
    public class DroideBatteryController
    {
        // ── Estado ────────────────────────────────────────────
        public int CurrentBattery { get; private set; }
        public int MaxBattery     { get; private set; }

        // ── Eventos ───────────────────────────────────────────
        public event Action<int> OnBatteryChanged;
        public event Action      OnBatteryDepleted;

        // ─────────────────────────────────────────────────────
        public void Init(int maxBattery)
        {
            MaxBattery     = maxBattery;
            CurrentBattery = maxBattery;
        }

        /// <summary>Drena energía. Emite OnBatteryDepleted si llega a cero.</summary>
        public void Drain(int amount)
        {
            if (amount <= 0) return;
            CurrentBattery = Mathf.Max(0, CurrentBattery - amount);
            OnBatteryChanged?.Invoke(CurrentBattery);

            if (CurrentBattery <= 0)
                OnBatteryDepleted?.Invoke();
        }

        /// <summary>Añade energía hasta el máximo. Emite OnBatteryChanged.</summary>
        public void Charge(int amount)
        {
            if (amount <= 0) return;
            CurrentBattery = Mathf.Min(MaxBattery, CurrentBattery + amount);
            OnBatteryChanged?.Invoke(CurrentBattery);
        }

        /// <summary>Fuerza el valor de batería (para restauración de estado).</summary>
        public void Set(int value)
        {
            CurrentBattery = Mathf.Clamp(value, 0, MaxBattery);
            OnBatteryChanged?.Invoke(CurrentBattery);
        }
    }
}
