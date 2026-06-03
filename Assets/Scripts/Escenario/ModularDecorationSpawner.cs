using System.Collections.Generic;
using Celeris.Data;
using Celeris.Grid;
using UnityEngine;

namespace Celeris.Core
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

            // Inicio: Droide sale al Norte (0,1), decoraciones al Sur y esquinas traseras
            var startOffsets = new List<Vector2Int>
            {
                new( 0, -1),  // Sur (detrás del inicio)
                new(-1, -1),  // Esquina trasera izquierda
                new( 1, -1),  // Esquina trasera derecha
                new(-1,  0),  // Lado izquierdo
                new( 1,  0),  // Lado derecho
            };
            SpawnDecorationsAround(Vector2Int.zero, startOffsets, piecesAroundStart);

            // Goal: decoraciones al lado opuesto de donde llega el Droide
            Vector2Int goalCoord = FindGoalCoord();
            Vector2Int arrivalDir = FindArrivalDirection(goalCoord);
            Vector2Int oppositeDir = -arrivalDir;

            var goalOffsets = BuildGoalOffsets(oppositeDir);
            SpawnDecorationsAround(goalCoord, goalOffsets, piecesAroundGoal);
        }

        private Vector2Int FindGoalCoord()
        {
            foreach (var kvp in gridGenerator.TileMap)
                if (kvp.Value.tileType == TileType.GoalTile)
                    return kvp.Key;
            return Vector2Int.zero;
        }

        // Busca el tile anterior al Goal y usa su dirección de salida
        // para saber desde dónde llega el Droide al Goal
        private Vector2Int FindArrivalDirection(Vector2Int goalCoord)
        {
            // Los 4 vecinos posibles
            Vector2Int[] dirs = { new(0,1), new(0,-1), new(1,0), new(-1,0) };

            foreach (var dir in dirs)
            {
                Vector2Int neighborCoord = goalCoord - dir;
                var neighbor = gridGenerator.GetTile(neighborCoord);
                if (neighbor == null) continue;

                // Si el vecino apunta hacia el Goal, esa es la dirección de llegada
                Vector2Int neighborExit = DirToVector(neighbor.arrowDirection);
                if (neighborExit == dir)
                    return dir;
            }

            // Fallback: asumir que llega del Sur
            return new Vector2Int(0, -1);
        }

        // Construye los offsets del lado opuesto a la llegada + esquinas laterales
        private List<Vector2Int> BuildGoalOffsets(Vector2Int backDir)
        {
            // Perpendiculares al backDir
            Vector2Int perp1 = new(-backDir.y,  backDir.x);
            Vector2Int perp2 = new( backDir.y, -backDir.x);

            return new List<Vector2Int>
            {
                backDir,                    // Directo detrás
                backDir + perp1,            // Esquina trasera izquierda
                backDir + perp2,            // Esquina trasera derecha
                perp1,                      // Lado izquierdo
                perp2,                      // Lado derecho
            };
        }

        private void SpawnDecorationsAround(Vector2Int center, List<Vector2Int> offsets, int count)
        {
            var candidates = new List<Vector2Int>();

            foreach (var offset in offsets)
            {
                Vector2Int pos = center + offset;
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

        private void SpawnPiece(Vector2Int coord)
        {
            GameObject[] pieces = { cuboAlargado01, cuboAlargado02, esquinaL, escalera };
            GameObject prefab = pieces[_rng.Next(pieces.Length)];
            if (prefab == null) return;

            Vector3 worldPos = new Vector3(coord.x, decorationY, coord.y);
            Quaternion rotation = Quaternion.Euler(0f, _rng.Next(4) * 90f, 0f);

            GameObject instance = Instantiate(prefab, worldPos, rotation, transform);
            instance.transform.localScale = Vector3.one * decorationScale;
        }

        private static Vector2Int DirToVector(MoveDirection dir)
        {
            return dir switch
            {
                MoveDirection.North => new Vector2Int( 0,  1),
                MoveDirection.South => new Vector2Int( 0, -1),
                MoveDirection.East  => new Vector2Int( 1,  0),
                MoveDirection.West  => new Vector2Int(-1,  0),
                _ => new Vector2Int(0, 1)
            };
        }

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