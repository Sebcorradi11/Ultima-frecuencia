using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Movimiento de DIAL-7: carrera con aceleración, salto de altura variable, coyote time,
    /// búfer de salto, caída más pesada que la subida y cambio de frecuencia.
    /// Los valores por defecto salen de la tabla de físicas del GDD convertidos a unidades
    /// de Unity (16 píxeles = 1 unidad).
    /// Requiere: Rigidbody2D (Dynamic, Interpolate, Freeze Rotation Z) y un Collider2D
    /// (recomendado CapsuleCollider2D) en la Layer "Jugador".
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class ControladorJugador : MonoBehaviour
    {
        [Header("Movimiento horizontal")]
        [Tooltip("90 px/s = 5,6 u/s")]
        [SerializeField] float velocidadMaxima = 5.6f;
        [Tooltip("600 px/s² = 37,5 u/s²")]
        [SerializeField] float aceleracion = 37.5f;
        [Tooltip("900 px/s² = 56 u/s²")]
        [SerializeField] float frenado = 56f;
        [Tooltip("Porcentaje de control en el aire.")]
        [SerializeField, Range(0f, 1f)] float controlAereo = 0.8f;

        [Header("Salto")]
        [Tooltip("3 tiles = 3 unidades")]
        [SerializeField] float alturaSalto = 3f;
        [Tooltip("Gravedad de subida: 900 px/s² = 56,25 u/s²")]
        [SerializeField] float gravedad = 56.25f;
        [Tooltip("La caída usa 1,5 veces la gravedad (1350 px/s²).")]
        [SerializeField] float multiplicadorCaida = 1.5f;
        [Tooltip("Al soltar el botón, la velocidad vertical se reduce a este porcentaje.")]
        [SerializeField, Range(0f, 1f)] float corteSalto = 0.5f;
        [SerializeField] float velocidadCaidaMaxima = 16f;
        [SerializeField] float tiempoCoyote = 0.10f;
        [SerializeField] float tiempoBuferSalto = 0.12f;

        [Header("Suelo")]
        [SerializeField] float altoChequeoSuelo = 0.08f;

        [Header("Visual")]
        [Tooltip("Hijo que contiene los sprites (cuerpo + luces). Se espeja con la escala para girar al personaje.")]
        [SerializeField] Transform visual;
        [Tooltip("SpriteRenderers que se tiñen con la frecuencia activa (visor y dial del pecho).")]
        [SerializeField] SpriteRenderer[] partesConColorDeFrecuencia;

        Rigidbody2D rb;
        Collider2D cuerpo;
        SpriteRenderer sprite;
        Animator animator;
        bool tieneAnimator;

        float entradaX;
        float contadorCoyote;
        float contadorBufer;
        bool enSuelo;
        bool estabaEnSuelo;
        bool saltando;
        float bloqueoEmpujeHasta;

        public bool EnSuelo { get { return enSuelo; } }
        public Vector2 Velocidad { get { return rb.linearVelocity; } }
        public Collider2D Cuerpo { get { return cuerpo; } }
        public bool MiraDerecha { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            cuerpo = GetComponent<Collider2D>();
            sprite = GetComponentInChildren<SpriteRenderer>();
            animator = GetComponentInChildren<Animator>();
            tieneAnimator = animator != null && animator.runtimeAnimatorController != null;
            rb.freezeRotation = true;
            MiraDerecha = true;
        }

        void Start()
        {
            if (GestorFrecuencia.Instancia != null)
            {
                GestorFrecuencia.Instancia.AlCambiar += AlCambiarFrecuencia;
                AlCambiarFrecuencia(GestorFrecuencia.Instancia.Activa);
            }
        }

        void OnDestroy()
        {
            if (GestorFrecuencia.Instancia != null) GestorFrecuencia.Instancia.AlCambiar -= AlCambiarFrecuencia;
        }

        void AlCambiarFrecuencia(Frecuencia f)
        {
            // Clave de la mecánica: el cuerpo del jugador deja de chocar con las capas inactivas.
            rb.excludeLayers = GestorFrecuencia.Instancia.MascaraInactivas;
            Color c = FrecuenciaUtil.ColorDe(f);
            if (partesConColorDeFrecuencia == null) return;
            for (int i = 0; i < partesConColorDeFrecuencia.Length; i++)
                if (partesConColorDeFrecuencia[i] != null) partesConColorDeFrecuencia[i].color = c;
        }

        void Update()
        {
            if (GestorJuego.ControlesBloqueados)
            {
                entradaX = 0f;
                contadorBufer = 0f;
                ActualizarAnimacion();
                return;
            }

            entradaX = Entrada.Horizontal();

            if (Entrada.SaltoPresionado()) contadorBufer = tiempoBuferSalto;
            else contadorBufer -= Time.deltaTime;

            // Salto variable: soltar el botón corta la subida.
            if (saltando && !Entrada.SaltoMantenido() && rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * corteSalto);
                saltando = false;
            }

            if (GestorFrecuencia.Instancia != null)
            {
                if (Entrada.FrecuenciaSiguiente()) GestorFrecuencia.Instancia.IntentarCambiar(+1, cuerpo);
                else if (Entrada.FrecuenciaAnterior()) GestorFrecuencia.Instancia.IntentarCambiar(-1, cuerpo);
            }

            if (entradaX > 0.01f) MiraDerecha = true;
            else if (entradaX < -0.01f) MiraDerecha = false;
            if (visual != null) visual.localScale = new Vector3(MiraDerecha ? 1f : -1f, 1f, 1f);
            else if (sprite != null) sprite.flipX = !MiraDerecha;

            ActualizarAnimacion();
        }

        void FixedUpdate()
        {
            ComprobarSuelo();

            if (enSuelo) contadorCoyote = tiempoCoyote;
            else contadorCoyote -= Time.fixedDeltaTime;

            Vector2 v = rb.linearVelocity;

            // Horizontal con aceleración y frenado.
            if (Time.time >= bloqueoEmpujeHasta)
            {
                float objetivo = entradaX * velocidadMaxima;
                float tasa = Mathf.Abs(objetivo) > 0.01f ? aceleracion : frenado;
                if (!enSuelo) tasa *= controlAereo;
                v.x = Mathf.MoveTowards(v.x, objetivo, tasa * Time.fixedDeltaTime);
            }

            // Salto: búfer + coyote time.
            if (contadorBufer > 0f && contadorCoyote > 0f)
            {
                v.y = VelocidadDeSalto();
                contadorBufer = 0f;
                contadorCoyote = 0f;
                saltando = true;
                enSuelo = false;
                GestorAudio.Reproducir(Sonido.Salto);
            }

            // Gravedad propia: más fuerte al caer.
            float g = gravedad * (v.y < 0f ? multiplicadorCaida : 1f);
            float gUnity = Mathf.Abs(Physics2D.gravity.y);
            rb.gravityScale = gUnity > 0.001f ? g / gUnity : 0f;

            if (v.y < -velocidadCaidaMaxima) v.y = -velocidadCaidaMaxima;
            if (v.y <= 0f) saltando = false;

            rb.linearVelocity = v;
        }

        /// <summary>v = √(2·g·h): velocidad inicial necesaria para alcanzar la altura de salto.</summary>
        float VelocidadDeSalto()
        {
            return Mathf.Sqrt(2f * gravedad * alturaSalto);
        }

        void ComprobarSuelo()
        {
            int mascara = GestorFrecuencia.Instancia != null
                ? GestorFrecuencia.Instancia.MascaraSolidaActiva
                : LayerMask.GetMask(FrecuenciaUtil.CapaBase);

            Bounds b = cuerpo.bounds;
            Vector2 centro = new Vector2(b.center.x, b.min.y - altoChequeoSuelo * 0.5f);
            Vector2 tamano = new Vector2(b.size.x * 0.85f, altoChequeoSuelo);
            enSuelo = rb.linearVelocity.y <= 0.05f && Physics2D.OverlapBox(centro, tamano, 0f, mascara) != null;

            if (enSuelo && !estabaEnSuelo) GestorAudio.Reproducir(Sonido.Aterrizaje);
            estabaEnSuelo = enSuelo;
        }

        void ActualizarAnimacion()
        {
            if (!tieneAnimator) return;
            animator.SetFloat("VelX", Mathf.Abs(rb.linearVelocity.x));
            animator.SetFloat("VelY", rb.linearVelocity.y);
            animator.SetBool("EnSuelo", enSuelo);
        }

        /// <summary>Impulso hacia arriba al pisar un enemigo o el núcleo del jefe.</summary>
        public void Rebotar(float velocidadVertical)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocidadVertical);
            saltando = false;
        }

        /// <summary>Retroceso al recibir daño.</summary>
        public void Empujar(Vector2 velocidad, float duracion)
        {
            rb.linearVelocity = velocidad;
            bloqueoEmpujeHasta = Time.time + duracion;
            saltando = false;
        }

        public void Teletransportar(Vector3 posicion)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = posicion;
            transform.position = posicion;
            contadorBufer = 0f;
        }

        void OnDrawGizmosSelected()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c == null) return;
            Bounds b = c.bounds;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - altoChequeoSuelo * 0.5f, 0f),
                                new Vector3(b.size.x * 0.85f, altoChequeoSuelo, 0f));
        }
    }
}
