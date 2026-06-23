// ============================================================
// IMiniGameSceneService.cs  |  Assets/Scripts/Core/
//
// PROPÓSITO (DIP):
//   GameFlowManager depende de esta abstracción para cargar y
//   descargar MiniGameScene. La implementación concreta
//   (MiniGameSceneService) puede cambiarse sin tocar el orquestador.
//
// CONTRATO:
//   Load()    — carga MiniGameScene en modo aditivo y la activa.
//   Unload()  — descarga MiniGameScene y reactiva GameplayScene.
//   IsLoaded  — true entre Load() y Unload().
//
// EVENTOS:
//   OnMiniGameLoaded   — emitido al terminar la carga aditiva.
//   OnMiniGameUnloaded — emitido al terminar la descarga.
// ============================================================
using System;
using System.Collections;

namespace Celeris.Core
{
    public interface IMiniGameSceneService
    {
        bool IsLoaded { get; }

        event Action OnMiniGameLoaded;
        event Action OnMiniGameUnloaded;

        /// <summary>
        /// Carga MiniGameScene de forma aditiva.
        /// Devuelve un IEnumerator para que el llamador pueda yield sobre él.
        /// </summary>
        IEnumerator Load();

        /// <summary>
        /// Descarga MiniGameScene y restaura GameplayScene como activa.
        /// Devuelve un IEnumerator para que el llamador pueda yield sobre él.
        /// </summary>
        IEnumerator Unload();
    }
}
