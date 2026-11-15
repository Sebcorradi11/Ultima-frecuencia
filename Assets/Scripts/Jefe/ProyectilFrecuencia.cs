using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Descarga que dispara La Bobina. Tiene una frecuencia: solo daña si esa frecuencia está
    /// activa, así que el jugador puede esquivarla cambiando de sintonía.
    /// Requiere: Rigidbody2D (Kinematic) + CircleCollider2D (Is Trigger) en la Layer "Enemigos".
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class ProyectilFrecuencia : MonoBehaviour
    {
        [SerializeField] float vida = 6f;
        [SerializeField] float radioImpacto = 0.2f;
        [SerializeField, Range(0f, 1f)] float alfaInactivo = 0.3f;
        [SerializeField] float velocidadGiro = 540f;

        Frecuencia frecuencia;
        Vector2 velocidad;
        Rigidbody2D rb;
        SpriteRenderer sprite;
        float muereEn;

        public Frecuencia Frecuencia { get { return frecuencia; } }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            sprite = GetComponentInChildren<SpriteRenderer>();
        }

        public void Inicializar(Frecuencia f, Vector2 vel)
        {
            frecuencia = f;
            velocidad = vel;
            muereEn = Time.time + vida;
            if (sprite != null) sprite.color = FrecuenciaUtil.ColorDe(f);
        }

        void Update()
        {
            if (sprite != null)
            {
                Color c = sprite.color;
                c.a = GestorFrecuencia.EstaActiva(frecuencia) ? 1f : alfaInactivo;
                sprite.color = c;
                sprite.transform.Rotate(0f, 0f, velocidadGiro * Time.deltaTime);
            }
            if (Time.time >= muereEn) Destroy(gameObject);
        }

        void FixedUpdate()
        {
            rb.MovePosition(rb.position + velocidad * Time.fixedDeltaTime);

            // Se destruye al chocar con la capa Base (paredes y suelo de la arena).
            if (Physics2D.OverlapCircle(rb.position, radioImpacto, LayerMask.GetMask(FrecuenciaUtil.CapaBase)) != null)
                Destroy(gameObject);
        }

        void OnTriggerStay2D(Collider2D otro)
        {
            if (!GestorFrecuencia.EstaActiva(frecuencia)) return;
            VidaJugador vidaJugador = otro.GetComponentInParent<VidaJugador>();
            if (vidaJugador == null) return;
            vidaJugador.RecibirDano(1, transform.position);
            Destroy(gameObject);
        }
    }
}
