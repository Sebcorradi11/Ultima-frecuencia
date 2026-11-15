using System.Collections;
using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Enemigo "Chispa": dron que pertenece a una frecuencia. IA con máquina de estados:
    /// Patrulla → Alerta → Persecución → Regreso. Detecta al jugador por distancia y línea de visión.
    /// Si su frecuencia no está activa se vuelve una silueta inofensiva (regla 4 del GDD).
    /// Se derrota pisándolo desde arriba.
    /// Requiere: Rigidbody2D (Kinematic) + CircleCollider2D (Is Trigger) en la Layer "Enemigos".
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class DronChispa : MonoBehaviour
    {
        public enum Estado { Patrulla, Alerta, Persecucion, Regreso, Destruido }

        [SerializeField] Frecuencia frecuencia = Frecuencia.Ambar;

        [Header("Patrulla")]
        [Tooltip("Puntos de patrulla (hijos vacíos). Se desacoplan del dron al iniciar.")]
        [SerializeField] Transform[] puntosPatrulla;
        [SerializeField] float velocidadPatrulla = 2f;
        [SerializeField] float esperaEnPunto = 0.6f;

        [Header("Detección y persecución")]
        [SerializeField] float radioDeteccion = 5f;
        [SerializeField] float radioPerdida = 7.5f;
        [Tooltip("Distancia máxima que se aleja de su zona antes de volver.")]
        [SerializeField] float correaMaxima = 8f;
        [SerializeField] float velocidadPersecucion = 3.6f;
        [SerializeField] float tiempoAlerta = 0.45f;

        [Header("Combate")]
        [SerializeField] int dano = 1;
        [SerializeField] float reboteAlPisar = 14f;
        [SerializeField] GameObject prefabChatarra;
        [SerializeField] int piezasAlDestruir = 2;

        [Header("Visual")]
        [SerializeField] float amplitudFlotacion = 0.12f;
        [SerializeField, Range(0f, 1f)] float alfaInactivo = 0.25f;

        public Estado EstadoActual { get; private set; }

        Rigidbody2D rb;
        SpriteRenderer sprite;
        Transform jugador;
        Vector2 origen;
        Vector2 posicionLogica;
        int indicePunto;
        float esperaHasta;
        float finAlerta;
        float fase;

        bool Activo { get { return GestorFrecuencia.EstaActiva(frecuencia); } }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            sprite = GetComponentInChildren<SpriteRenderer>();
            origen = transform.position;
            posicionLogica = origen;
            fase = Random.value * 10f;

            if (puntosPatrulla != null)
                for (int i = 0; i < puntosPatrulla.Length; i++)
                    if (puntosPatrulla[i] != null) puntosPatrulla[i].SetParent(null, true);
        }

        void Start()
        {
            ControladorJugador c = FindAnyObjectByType<ControladorJugador>();
            if (c != null) jugador = c.transform;
            if (sprite != null) sprite.color = FrecuenciaUtil.ColorDe(frecuencia);
        }

        void Update()
        {
            if (sprite == null || EstadoActual == Estado.Destruido) return;
            Color c = sprite.color;
            c.a = Mathf.MoveTowards(c.a, Activo ? 1f : alfaInactivo, 6f * Time.deltaTime);
            if (EstadoActual == Estado.Alerta && Activo) c = Color.Lerp(c, Color.white, Mathf.PingPong(Time.time * 12f, 1f));
            else { Color f = FrecuenciaUtil.ColorDe(frecuencia); c.r = f.r; c.g = f.g; c.b = f.b; }
            sprite.color = c;
        }

        void FixedUpdate()
        {
            if (EstadoActual == Estado.Destruido) return;

            switch (EstadoActual)
            {
                case Estado.Patrulla: Patrullar(); break;
                case Estado.Alerta: Alertar(); break;
                case Estado.Persecucion: Perseguir(); break;
                case Estado.Regreso: Regresar(); break;
            }

            float flotacion = Mathf.Sin((Time.time + fase) * 3f) * amplitudFlotacion;
            rb.MovePosition(posicionLogica + Vector2.up * flotacion);
        }

        // ---------------- Estados ----------------

        void Patrullar()
        {
            if (PuedeVerAlJugador(radioDeteccion))
            {
                EstadoActual = Estado.Alerta;
                finAlerta = Time.time + tiempoAlerta;
                GestorAudio.Reproducir(Sonido.Alerta);
                return;
            }
            if (puntosPatrulla == null || puntosPatrulla.Length == 0 || Time.time < esperaHasta) return;

            Transform destino = puntosPatrulla[indicePunto];
            if (destino == null) return;
            if (MoverHacia(destino.position, velocidadPatrulla))
            {
                indicePunto = (indicePunto + 1) % puntosPatrulla.Length;
                esperaHasta = Time.time + esperaEnPunto;
            }
        }

        void Alertar()
        {
            if (!Activo) { EstadoActual = Estado.Patrulla; return; }
            if (Time.time >= finAlerta) EstadoActual = Estado.Persecucion;
        }

        void Perseguir()
        {
            bool lejosDeCasa = Vector2.Distance(posicionLogica, origen) > correaMaxima;
            if (!Activo || lejosDeCasa || !PuedeVerAlJugador(radioPerdida))
            {
                EstadoActual = Estado.Regreso;
                return;
            }
            MoverHacia(jugador.position, velocidadPersecucion);
        }

        void Regresar()
        {
            if (PuedeVerAlJugador(radioDeteccion) && Vector2.Distance(posicionLogica, origen) < correaMaxima * 0.5f)
            {
                EstadoActual = Estado.Persecucion;
                return;
            }
            if (MoverHacia(origen, velocidadPatrulla)) EstadoActual = Estado.Patrulla;
        }

        // ---------------- Utilidades ----------------

        /// <summary>Avanza hacia el destino. Devuelve true al llegar.</summary>
        bool MoverHacia(Vector2 destino, float velocidad)
        {
            posicionLogica = Vector2.MoveTowards(posicionLogica, destino, velocidad * Time.fixedDeltaTime);
            if (sprite != null && Mathf.Abs(destino.x - posicionLogica.x) > 0.05f)
                sprite.flipX = destino.x < posicionLogica.x;
            return Vector2.Distance(posicionLogica, destino) < 0.05f;
        }

        bool PuedeVerAlJugador(float radio)
        {
            if (jugador == null || !Activo || !jugador.gameObject.activeInHierarchy) return false;
            Vector2 desde = posicionLogica;
            Vector2 hasta = jugador.position;
            if (Vector2.Distance(desde, hasta) > radio) return false;

            int mascara = GestorFrecuencia.Instancia != null
                ? GestorFrecuencia.Instancia.MascaraSolidaActiva
                : LayerMask.GetMask(FrecuenciaUtil.CapaBase);
            return Physics2D.Linecast(desde, hasta, mascara).collider == null;
        }

        // ---------------- Contacto con el jugador ----------------

        void OnTriggerStay2D(Collider2D otro)
        {
            if (EstadoActual == Estado.Destruido || !Activo) return;

            VidaJugador vida = otro.GetComponentInParent<VidaJugador>();
            if (vida == null) return;
            ControladorJugador control = vida.GetComponent<ControladorJugador>();

            bool cayendo = control.Velocidad.y < 0.1f;
            bool desdeArriba = control.Cuerpo.bounds.min.y > transform.position.y;
            if (cayendo && desdeArriba)
            {
                control.Rebotar(reboteAlPisar);
                StartCoroutine(Destruir());
            }
            else
            {
                vida.RecibirDano(dano, transform.position);
            }
        }

        IEnumerator Destruir()
        {
            EstadoActual = Estado.Destruido;
            GestorAudio.Reproducir(Sonido.Pisoton);
            CamaraSeguimiento.Sacudir(0.1f, 0.08f);

            Collider2D[] cols = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            for (int i = 0; i < piezasAlDestruir && prefabChatarra != null; i++)
            {
                Vector3 p = transform.position + new Vector3((i - (piezasAlDestruir - 1) * 0.5f) * 0.6f, 0.3f, 0f);
                Instantiate(prefabChatarra, p, Quaternion.identity);
            }

            float t = 0f;
            Vector3 escala = transform.localScale;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(escala, new Vector3(escala.x * 1.4f, escala.y * 0.2f, 1f), t / 0.25f);
                yield return null;
            }
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 centro = Application.isPlaying ? (Vector3)origen : transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radioDeteccion);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(centro, correaMaxima);
        }
    }
}
