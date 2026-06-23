using UnityEngine;
using UnityEngine.UI;

namespace Celeris.HackMinigame
{

    public class AlienGlyph : MonoBehaviour
    {
        [Header("Componentes Visuales")]
        [SerializeField] private Image glyphImage;
        [SerializeField] private Button glyphButton;
    
        [Header("Configuración de Feedback")]
        [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.15f, 1f); // Gris oscuro apagado
        [SerializeField] private Color glowColor = new Color(0f, 1f, 1f, 1f); // Cian neón brillante
        [SerializeField] private AudioClip glyphSound;

        // Evento nativo de C#. Cuando el jugador pulse el glifo, este avisará al Manager indicando su número de índice.
        public System.Action<int> OnGlyphClicked; 
        private int glyphIndex;

        /// <summary>
        /// Inicializa el glifo asignándole su posición en la matriz y limpiando listeners.
        /// </summary>
        public void Initialize(int index)
        {
            glyphIndex = index;
            glyphImage.color = normalColor;
        
            // Limpiamos mecánicas previas para evitar duplicación de llamadas en memoria RAM
            glyphButton.onClick.RemoveAllListeners();
            glyphButton.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            // El operador ?. asegura que si nadie está escuchando el evento, el juego no tire un Crash NullReference
            OnGlyphClicked?.Invoke(glyphIndex);
        }

        /// <summary>
        /// Enciende el glifo con el color neón de emisión alienígena.
        /// </summary>
        public void ActivateGlow()
        {
            glyphImage.color = glowColor;
        }

        /// <summary>
        /// Apaga el glifo regresándolo a su estado metálico/pasivo.
        /// </summary>
        public void DeactivateGlow()
        {
            glyphImage.color = normalColor;
        }

        /// <summary>
        /// Bloquea o desbloquea la interacción del botón para evitar trampas mientras se muestra la secuencia.
        /// </summary>
        public void SetInteractable(bool state)
        {
            glyphButton.interactable = state;
        }

        /// <summary>
        /// Expone el clip de audio para que el AudioSource centralizado lo reproduzca de forma óptima.
        /// </summary>
        public AudioClip GetSound()
        {
            return glyphSound;
        }
    }
}
