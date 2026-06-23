// ============================================================
// ModularDecorationSpawner.cs  |  Assets/Scripts/Escenario/
//
// MIGRACIÓN Fase 2: Vector2Int → Vector3Int.
//   Todos los usos de Vector2Int para coordenadas de grid
//   han sido actualizados a Vector3Int (Y = 0 siempre).
//   gridGenerator.TileMap y GetTile() ahora aceptan Vector3Int.
//
//   SpawnPiece usa coord.x / coord.z para la posición world
//   (coord.y siempre 0 hasta Fase 3/4 / wall-walking).
//
// NAMESPACE: Celeris.Core — mantener hasta Fase 5 (cleanup).
//   En Fase 5 mover a Celeris.Escenario.
// ============================================================
using System.Collections.Generic;
using Celeris.Data;
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Escenario
{
    public class ModularDecorationSpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private ProceduralGridGenerator gridGenerator;

        [Header("Piezas modulares")]
        [SerializeField] private GameObject cuboAlargado01;
        [SerializeField] private GameObject cuboAlargado02;
        [SerializeField] private GameObject esquinaL;
        [SerializeField] private GameObject escalera;

        [Header("Configuración")]
        [Range(1, 5)]
        [SerializeField] private int piecesAroundStart = 3;
        [Range(1, 5)]
        [SerializeField] private int piecesAroundGoal = 3;
        [SerializeField] private float decorationY = 0f;
        [SerializeField] private float decorationScale = 25f;

        private System.Random _rng;

        private void OnEnable()
        {
            if (gridGenerator != null)
                gridGenerator.OnGridReady += OnGridReady;
        }

        private void OnDisable()
        {
            if (gridGenerator != null)
                gridGenerator.OnGridReady -= OnGridReady;
        }

        private void OnGridReady()
        {
            int seed = gridGenerator.config != null ? gridGenerator.config.proceduralSeed : 0;
            _rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);

            // Inicio: Droide sale al Norte (+Z), decoraciones al Sur y esquinas traseras
            var startOffsets = new List<Vector3Int>
            {
                new( 0, 0, -1),   // Sur (detrás del inicio)
                new(-1, 0, -1),   // Esquina trasera izquierda
                new( 1, 0, -1),   // Esquina trasera derecha
                new(-1, 0,  0),   // Lado izquierdo
                new( 1, 0,  0),   // Lado derecho
            };
            SpawnDecorationsAround(Vector3Int.zero, startOffsets, piecesAroundStart);

            // Goal: decoraciones al lado opuesto de donde llega el Droide
            Vector3Int goalCoord   = FindGoalCoord();
            Vector3Int arrivalDir  = FindArrivalDirection(goalCoord);
            Vector3Int oppositeDir = -arrivalDir;

            var goalOffsets = BuildGoalOffsets(oppositeDir);
            SpawnDecorationsAround(goalCoord, goalOffsets, piecesAroundGoal);
        }

        private Vector3Int FindGoalCoord()
        {
            foreach (var kvp in gridGenerator.TileMap)
                if (kvp.Value.tileType == TileType.GoalTile)
                    return kvp.Key;
            return Vector3Int.zero;
        }

        /// <summary>
        /// Busca el tile anterior al Goal y usa su dirección de salida
        /// para saber desde dónde llega el Droide al Goal.
        /// </summary>
        private Vector3Int FindArrivalDirection(Vector3Int goalCoord)
        {
            // Los 4 vecinos en plano XZ (Y = 0 hasta Fase 3/4)
            Vector3Int[] dirs =
            {
                new( 0, 0,  1),
                new( 0, 0, -1),
                new( 1, 0,  0),
                new(-1, 0,  0)
            };

            foreach (var dir in dirs)
            {
                Vector3Int neighborCoord = goalCoord - dir;
                var neighbor = gridGenerator.GetTile(neighborCoord);
                if (neighbor == null) continue;

                // Si el vecino apunta hacia el Goal, esa es la dirección de llegada
                Vector3Int neighborExit = DirToVector(neighbor.arrowDirection);
                if (neighborExit == dir)
                    return dir;
            }

            // Fallback: asumir que llega del Sur
            return new Vector3Int(0, 0, -1);
        }

        /// <summary>
        /// Construye los offsets del lado opuesto a la llegada + esquinas laterales.
        /// Opera en XZ (Y = 0). En Fase 3/4 se revisará para planos alternativos.
        /// </summary>
        private List<Vector3Int> BuildGoalOffsets(Vector3Int backDir)
        {
            // Perpendiculares al backDir en el plano XZ
            var perp1 = new Vector3Int(-backDir.z, 0,  backDir.x);
            var perp2 = new Vector3Int( backDir.z, 0, -backDir.x);

            return new List<Vector3Int>
            {
                backDir,                     // Directo detrás
                backDir + perp1,             // Esquina trasera izquierda
                backDir + perp2,             // Esquina trasera derecha
                perp1,                       // Lado izquierdo
                perp2,                       // Lado derecho
            };
        }

        private void SpawnDecorationsAround(Vector3Int center, List<Vector3Int> offsets, int count)
        {
            var candidates = new List<Vector3Int>();

            foreach (var offset in offsets)
            {
                Vector3Int pos = center + offset;
                if (gridGenerator.GetTile(pos) == null)
                    candidates.Add(pos);
            }

            Shuffle(candidates);

            int spawned = 0;
            foreach (var coord in candidates)
            {
                if (spawned >= count) break;
                SpawnPiece(coord);
                spawned++;
            }
        }

        private void SpawnPiece(Vector3Int coord)
        {
            GameObject[] pieces  = { cuboAlargado01, cuboAlargado02, esquinaL, escalera };
            GameObject   prefab  = pieces[_rng.Next(pieces.Length)];
            if (prefab == null) return;

            // coord.y siempre 0 en Fase 1/2. En Fase 3/4 se usa coord.y para paredes/rampas.
            Vector3    worldPos = new Vector3(coord.x, decorationY, coord.z);
            Quaternion rotation = Quaternion.Euler(0f, _rng.Next(4) * 90f, 0f);

            GameObject instance = Instantiate(prefab, worldPos, rotation, transform);
            instance.transform.localScale = Vector3.one * decorationScale;
        }

        /// <summary>
        /// Convierte MoveDirection a Vector3Int en plano XZ.
        /// Equivalente a TileComponent.DirectionToVector — duplicado aquí
        /// para mantener este spawner independiente del namespace Grid.
        /// En Fase 5 (cleanup) se puede unificar en un helper compartido.
        /// </summary>
        private static Vector3Int DirToVector(MoveDirection dir) => dir switch
        {
            MoveDirection.North => new Vector3Int( 0, 0,  1),
            MoveDirection.South => new Vector3Int( 0, 0, -1),
            MoveDirection.East  => new Vector3Int( 1, 0,  0),
            MoveDirection.West  => new Vector3Int(-1, 0,  0),
            _                   => new Vector3Int( 0, 0,  1)
        };

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
