using UnityEngine;

namespace Celeris.Utils
{

    public class CameraFollow : MonoBehaviour
    {
        public Transform player;
        public float smoothTime = 0.1f;

        private Vector3 offset;
        private Vector3 velocity = Vector3.zero;

        void Start()
        {
            // Calculamos la distancia inicial y la rotación inicial relativa
            offset = transform.position - player.position;
        }

        void LateUpdate()
        {
            if (player == null) return;

            // Calculamos la posición donde debería estar la cámara
            // Basándonos en la rotación actual del jugador y el offset original
            Vector3 targetPosition = player.position + (player.rotation * offset);

            // Aplicamos suavizado al movimiento
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref velocity,
                smoothTime
            );

            // IMPORTANTE: Para mantener la rotación inicial, 
            // simplemente hacemos que la cámara mire al jugador o mantenga su rotación fija.
            // Si quieres que siempre mire al jugador:
            transform.LookAt(player);
        }
    }
}
