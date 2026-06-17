// ============================================================
// TileWaveAnimator.cs  |  Assets/Scripts/Grid/
//
// RESPONSABILIDAD ÚNICA: animar la aparición de tiles en ola.
//
// COMPORTAMIENTO:
//   Los tiles se generan con Y = -5f (bajo el suelo).
//   TileWaveAnimator los hace subir en secuencia, uno a uno
//   con un retraso de 'tileDelay' segundos entre cada uno.
//   Cada tile sube individualmente durante 5f/waveSpeed segundos.
//   La corrutina completa cuando todos los tiles han subido.
//
// API:
//   PlayWave(placed, tileMap, tileDelay, waveSpeed)
//   Devuelve IEnumerator para que el coordinador pueda hacer
//   yield return StartCoroutine(animator.PlayWave(...)).
//
// ESCALABILIDAD:
//   Para añadir nuevas animaciones (ej. explosión, teleport),
//   solo se añaden métodos nuevos aquí. El coordinador llama
//   el método por nombre. No modifica otras clases.
//
// NOTA — MonoBehaviour:
//   TileWaveAnimator hereda de MonoBehaviour únicamente porque
//   necesita StartCoroutine. Lo hace como componente adjunto al
//   mismo GameObject que ProceduralGridGenerator, o como helper
//   interno instanciado por el coordinador.
//   ProceduralGridGenerator lo crea con gameObject.AddComponent<>().
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Celeris.Grid
{
    public class TileWaveAnimator : MonoBehaviour
    {
        // ── API pública ───────────────────────────────────────

        /// <summary>
        /// Ejecuta la animación de ola sobre los tiles instanciados.
        /// Los tiles se levantan del suelo en secuencia.
        /// yield return StartCoroutine(PlayWave(...)) en el coordinador.
        /// </summary>
        /// <param name="placed">Lista ordenada de PlacedTile (define el orden de animación).</param>
        /// <param name="tileMap">Mapa de tiles para buscar el Transform por coordenada.</param>
        /// <param name="tileDelay">Segundos de espera entre el inicio de cada tile.</param>
        /// <param name="waveSpeed">Velocidad de subida. Duración por tile = 5f / waveSpeed.</param>
        public IEnumerator PlayWave(
            IReadOnlyList<PlacedTile>                           placed,
            IReadOnlyDictionary<Vector3Int, TileComponent>      tileMap,
            float                                               tileDelay,
            float                                               waveSpeed)
        {
            float riseDuration = 5f / Mathf.Max(0.1f, waveSpeed);

            foreach (var pt in placed)
            {
                if (tileMap.TryGetValue(pt.Coord, out var tile) && tile != null)
                    StartCoroutine(RiseTile(tile.transform, riseDuration));

                yield return new WaitForSeconds(tileDelay);
            }

            // Esperar a que el último tile termine de subir
            yield return new WaitForSeconds(riseDuration + 0.1f);
        }

        // ── Animación individual ──────────────────────────────

        // F3-T4: eliminado modificador 'static'. El método se invoca con StartCoroutine()
        // sobre la instancia — static era engañoso sin aportar ninguna ventaja.
        private IEnumerator RiseTile(Transform t, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed   += Time.deltaTime;
                var p      = t.position;
                p.y        = Mathf.Lerp(-5f, 0f, Mathf.Clamp01(elapsed / duration));
                t.position = p;
                yield return null;
            }

            // Snap final para eliminar cualquier error de precisión flotante
            var fp = t.position;
            fp.y   = 0f;
            t.position = fp;
        }
    }
}
