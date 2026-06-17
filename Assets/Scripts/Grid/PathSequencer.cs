// ============================================================
// PathSequencer.cs  |  Assets/Scripts/Grid/
//
// PASO A de la generación: construye la secuencia lógica del
// nivel como lista plana de TileDescriptor, SIN geometría.
//
// PIPELINE OBLIGATORIO (Reglas de Negocio, inamovible):
//
//   [BaseTile inicio]
//   ── × 3 (para cada portal) ──────────────────────────────
//   [Buffer EmptyTile: 1-5 BaseTile]
//   [ObstacleBlock: Laser[1-5] ó Charge[3-7] ó Ambos con buffer]
//   [Buffer EmptyTile: 1-5 BaseTile]
//   [PortalTile N]
//   ────────────────────────────────────────────────────────
//   [Buffer EmptyTile: 1-5 BaseTile]
//   [GoalTile]
//
// REGLAS ESTRICTAS:
//   1. Buffer entre CUALQUIER par de hitos: [1, BUFFER_MAX] BaseTiles.
//   2. Buffer entre dos obstáculos del mismo acto: [1, BUFFER_MAX].
//   3. Cluster Laser: [LASER_MIN, LASER_MAX] tiles consecutivos.
//   4. Cluster Charge: [CHARGE_MIN, CHARGE_MAX] tiles consecutivos.
//   5. Nunca dos categorías de obstáculo pegadas (sin buffer).
//   6. GoalTile siempre precedido por al menos 1 BaseTile.
//
// RESPONSABILIDAD ÚNICA:
//   Esta clase NO sabe nada de geometría, prefabs, posiciones
//   ni Unity API. Es testeable fuera de Play Mode.
//
// EXTENSIBILIDAD (OCP):
//   Para añadir un nuevo obstáculo:
//     1. Crear clase que implemente IObstacleDefinition.
//     2. Pasarla en el array 'obstaclePool' del constructor.
//   PathSequencer no se modifica.
// ============================================================
using System.Collections.Generic;
using Celeris.Data;

namespace Celeris.Grid
{
    public class PathSequencer
    {
        // ── Constantes de Reglas de Negocio ───────────────────
        private const int BUFFER_MIN  = 1;
        private const int BUFFER_MAX  = 5;
        private const int PORTAL_COUNT = 3;

        // ── Pool de obstáculos ────────────────────────────────
        // OCP: PathSequencer no conoce los tipos concretos.
        // El generador inyecta las definiciones disponibles.
        private readonly IObstacleDefinition[] _obstaclePool;
        private readonly System.Random         _rng;

        // ── Pesos para elegir obstáculo del pool ──────────────
        private readonly int[] _weights;

        /// <summary>
        /// Constructor. Recibe el pool de obstáculos disponibles y sus
        /// pesos de selección aleatoria, y el generador de números aleatorios
        /// con semilla determinista del nivel.
        /// </summary>
        /// <param name="obstaclePool">Definiciones de obstáculos disponibles.</param>
        /// <param name="weights">Peso de selección por índice (mismo orden que pool).</param>
        /// <param name="rng">RNG con semilla del nivel para determinismo.</param>
        public PathSequencer(IObstacleDefinition[] obstaclePool, int[] weights, System.Random rng)
        {
            _obstaclePool = obstaclePool;
            _weights      = weights;
            _rng          = rng;
        }

        // ══════════════════════════════════════════════════════
        //  API PÚBLICA
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Construye y devuelve la secuencia completa del nivel.
        /// La lista resultante es la única fuente de verdad sobre
        /// QUÉ tiles existen en el nivel, en qué orden y cuántos.
        /// PathGeometryTracer consumirá esta lista para asignar posiciones.
        /// </summary>
        public List<TileDescriptor> BuildSequence()
        {
            var seq = new List<TileDescriptor>();

            // ── Tile de inicio (posición fija, sin buffer previo) ──
            seq.Add(new TileDescriptor(TileType.BaseTile));

            // ── 3 actos: obstáculos → buffer → portal ─────────────
            for (int portalIdx = 0; portalIdx < PORTAL_COUNT; portalIdx++)
            {
                BuildAct(seq, portalIdx);
            }

            // ── Epílogo: buffer final + GoalTile ──────────────────
            AppendBuffer(seq);
            seq.Add(new TileDescriptor(TileType.GoalTile));

            return seq;
        }

        // ══════════════════════════════════════════════════════
        //  CONSTRUCCIÓN DE ACTO
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Construye un acto completo:
        ///   [Buffer] [ObstacleBlock(s)] [Buffer] [Portal]
        /// </summary>
        private void BuildAct(List<TileDescriptor> seq, int portalIdx)
        {
            // Buffer de entrada al acto
            AppendBuffer(seq);

            // Decidir estructura de obstáculos: solo uno o ambos tipos
            bool useTwo = (_rng.Next(2) == 0);

            if (useTwo && _obstaclePool.Length >= 2)
            {
                // Dos bloques de obstáculos con buffer entre ellos
                var defA = PickObstacleDefinition();
                AppendObstacleCluster(seq, defA);

                // Buffer obligatorio entre los dos obstáculos (Regla 2)
                AppendBuffer(seq);

                // Elegir un obstáculo diferente para el segundo bloque
                var defB = PickObstacleDefinitionOtherThan(defA);
                AppendObstacleCluster(seq, defB);
            }
            else
            {
                // Un único bloque de obstáculo
                AppendObstacleCluster(seq, PickObstacleDefinition());
            }

            // Buffer de salida antes del portal
            AppendBuffer(seq);

            // Portal
            seq.Add(new TileDescriptor(TileType.PortalTile, portalIndex: portalIdx));
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS DE CONSTRUCCIÓN
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Añade un bloque de obstáculo: el cluster completo de tiles
        /// del mismo tipo, consecutivos, en cantidad definida por la
        /// implementación de IObstacleDefinition.
        /// </summary>
        private void AppendObstacleCluster(List<TileDescriptor> seq, IObstacleDefinition def)
        {
            int size = def.GetClusterSize(_rng);
            for (int i = 0; i < size; i++)
                seq.Add(new TileDescriptor(def));
        }

        /// <summary>
        /// Añade un buffer de BaseTiles de longitud aleatoria en [BUFFER_MIN, BUFFER_MAX].
        /// Garantiza que nunca hay 0 tiles vacíos entre dos hitos.
        /// </summary>
        private void AppendBuffer(List<TileDescriptor> seq)
        {
            // Next(min, max) → [min, max-1]. Para [BUFFER_MIN, BUFFER_MAX] inclusivo:
            int count = _rng.Next(BUFFER_MIN, BUFFER_MAX + 1);
            for (int i = 0; i < count; i++)
                seq.Add(new TileDescriptor(TileType.BaseTile));
        }

        /// <summary>
        /// Selecciona un IObstacleDefinition del pool según los pesos configurados.
        /// </summary>
        private IObstacleDefinition PickObstacleDefinition()
        {
            int total = 0;
            foreach (int w in _weights) total += w;

            int roll = _rng.Next(total);
            int acc  = 0;
            for (int i = 0; i < _obstaclePool.Length; i++)
            {
                acc += _weights[i];
                if (roll < acc) return _obstaclePool[i];
            }
            return _obstaclePool[0];
        }

        /// <summary>
        /// Selecciona un obstáculo del pool diferente al excluido.
        /// Si el pool solo tiene 1 elemento, devuelve ese mismo
        /// (el acto tendrá dos bloques del mismo tipo, lo cual es válido).
        /// </summary>
        private IObstacleDefinition PickObstacleDefinitionOtherThan(IObstacleDefinition excluded)
        {
            if (_obstaclePool.Length <= 1) return _obstaclePool[0];

            // Construir sub-pool sin el excluido
            int totalWeight = 0;
            for (int i = 0; i < _obstaclePool.Length; i++)
            {
                if (_obstaclePool[i].TileType != excluded.TileType)
                    totalWeight += _weights[i];
            }

            int roll = _rng.Next(totalWeight);
            int acc  = 0;
            for (int i = 0; i < _obstaclePool.Length; i++)
            {
                if (_obstaclePool[i].TileType == excluded.TileType) continue;
                acc += _weights[i];
                if (roll < acc) return _obstaclePool[i];
            }

            // Fallback seguro (no debería alcanzarse)
            return _obstaclePool[0];
        }
    }
}
