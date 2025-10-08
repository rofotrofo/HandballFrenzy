namespace Gameplay.Courts
{
    using UnityEngine;

    [RequireComponent(typeof(Collider2D))]
    public class SurfaceZone2D : MonoBehaviour
    {
        [Header("Players")]
        [Tooltip("Multiplicador de velocidad para jugadores dentro de la zona. 1 = normal; 0.6 = 40% más lento")]
        [Range(0.1f, 2f)]
        public float playerSpeedMultiplier = 1f;

        [Header("Ball (optional)")]
        [Tooltip("Factor por segundo para frenar la pelota. 1 = no afecta; 0.85 = ~15% menos por segundo")]
        [Range(0.1f, 1f)]
        public float ballVelocityFactorPerSecond = 1f;

        Collider2D col;

        void Awake()
        {
            col = GetComponent<Collider2D>();
            if (col && !col.isTrigger) col.isTrigger = true; // es una zona, no colisiona sólida
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("Jugador entró a la arena");
                var pc = other.GetComponent<PlayerController>();
                if (pc) pc.surfaceMultiplier = playerSpeedMultiplier; // aplica multiplicador
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                var pc = other.GetComponent<PlayerController>();
                if (pc) pc.surfaceMultiplier = 1f; // restablece al salir
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            // Frena la pelota mientras esté dentro (si eliges <1)
            var ball = other.GetComponentInParent<BallController>();
            if (!ball) return;

            var rb = ball.GetComponent<Rigidbody2D>();
            if (!rb || rb.bodyType != RigidbodyType2D.Dynamic) return;

            float f = Mathf.Clamp01(ballVelocityFactorPerSecond);
            float frameFactor = Mathf.Pow(f, Time.deltaTime);
            rb.linearVelocity *= frameFactor;
        }
        
        
    }
}