using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// NPC "Pip": dron con forma de pájaro, compañero de DIAL-7. Su propósito en el nivel es
    /// dar contexto y señalar secretos (la ruta alta con la Cinta de Rosa).
    /// Comportamiento autónomo con estados:
    ///   Deambular → revolotea alrededor de su nido con ruido Perlin.
    ///   Atento    → el jugador está cerca: lo mira y muestra el aviso "W".
    ///   Hablando  → muestra su diálogo con SistemaDialogo.
    ///   Guiando   → después de hablar vuela hasta el secreto, lo marca y vuelve.
    /// Requiere: Rigidbody2D (Kinematic) + CircleCollider2D (Is Trigger, radio ≈ 2) como zona de interacción.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PipNPC : MonoBehaviour
    {
        public enum Estado { Deambular, Atento, Hablando, Guiando, Volviendo }

        [Header("Diálogo")]
        [SerializeField] string nombre = "Pip";
        [SerializeField, TextArea(2, 4)] string[] primeraConversacion =
        {
            "¡Pii-pii! ¡DIAL-7, despertaste! Rosa dejó grabaciones por toda la subestación.",
            "La ciudad está partida en frecuencias. Girá tu dial con Q y E: lo que ves transparente se vuelve sólido.",
            "Psst... arriba de la escalera de colores hay una cinta escondida. ¡Seguime!"
        };
        [SerializeField, TextArea(2, 4)] string[] conversacionRepetida =
        {
            "¡Si algo parece imposible, probá con otra frecuencia!"
        };

        [Header("Movimiento autónomo")]
        [SerializeField] float radioDeambular = 1.2f;
        [SerializeField] float velocidadDeambular = 0.35f;
        [SerializeField] float velocidadVuelo = 4f;
        [Tooltip("Lugar que Pip señala después de la primera conversación (por ejemplo, la Cinta de Rosa).")]
        [SerializeField] Transform puntoSecreto;
        [SerializeField] float tiempoSenalando = 2f;

        [Header("Visual")]
        [Tooltip("Aviso \"W\" que aparece sobre Pip cuando el jugador está cerca.")]
        [SerializeField] GameObject avisoInteraccion;
        [SerializeField] SpriteRenderer sprite;

        public Estado EstadoActual { get; private set; }

        Rigidbody2D rb;
        Transform jugador;
        Vector2 nido;
        Vector2 posicion;
        float semilla;
        bool jugadorCerca;
        bool yaHablo;
        float finSenalando;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            nido = transform.position;
            posicion = nido;
            semilla = Random.value * 100f;
            if (avisoInteraccion != null) avisoInteraccion.SetActive(false);
        }

        void Update()
        {
            bool puedeHablar = jugadorCerca && (EstadoActual == Estado.Deambular || EstadoActual == Estado.Atento);
            if (avisoInteraccion != null) avisoInteraccion.SetActive(puedeHablar && !SistemaDialogo.Activo);

            if (puedeHablar && !GestorJuego.ControlesBloqueados && Entrada.Interactuar() && SistemaDialogo.Instancia != null)
            {
                EstadoActual = Estado.Hablando;
                string[] texto = yaHablo ? conversacionRepetida : primeraConversacion;
                bool guiar = !yaHablo && puntoSecreto != null;
                yaHablo = true;
                SistemaDialogo.Instancia.Mostrar(nombre, texto, delegate
                {
                    EstadoActual = guiar ? Estado.Guiando : Estado.Atento;
                });
            }
        }

        void FixedUpdate()
        {
            switch (EstadoActual)
            {
                case Estado.Deambular:
                case Estado.Atento:
                {
                    float t = Time.time * velocidadDeambular;
                    Vector2 desvio = new Vector2(Mathf.PerlinNoise(semilla, t) - 0.5f, Mathf.PerlinNoise(t, semilla) - 0.5f) * 2f * radioDeambular;
                    posicion = Vector2.Lerp(posicion, nido + desvio, 2f * Time.fixedDeltaTime);
                    if (EstadoActual == Estado.Atento && jugador != null) Mirar(jugador.position.x);
                    else Mirar(nido.x + desvio.x);
                    if (!jugadorCerca && EstadoActual == Estado.Atento) EstadoActual = Estado.Deambular;
                    if (jugadorCerca && EstadoActual == Estado.Deambular) EstadoActual = Estado.Atento;
                    break;
                }
                case Estado.Hablando:
                    if (jugador != null) Mirar(jugador.position.x);
                    break;
                case Estado.Guiando:
                {
                    // Si el jugador recogió la cinta antes de que Pip llegue, el objeto ya no existe.
                    if (puntoSecreto == null)
                    {
                        finSenalando = 0f;
                        EstadoActual = Estado.Volviendo;
                        break;
                    }
                    Vector2 destino = (Vector2)puntoSecreto.position + Vector2.up * 1.2f;
                    if (Volar(destino))
                    {
                        if (finSenalando <= 0f)
                        {
                            finSenalando = Time.time + tiempoSenalando;
                            GestorAudio.Reproducir(Sonido.Pip);
                        }
                        posicion.y += Mathf.Sin(Time.time * 18f) * 0.01f;
                        if (Time.time >= finSenalando)
                        {
                            finSenalando = 0f;
                            EstadoActual = Estado.Volviendo;
                        }
                    }
                    break;
                }
                case Estado.Volviendo:
                    if (Volar(nido)) EstadoActual = jugadorCerca ? Estado.Atento : Estado.Deambular;
                    break;
            }
            rb.MovePosition(posicion);
        }

        bool Volar(Vector2 destino)
        {
            Mirar(destino.x);
            posicion = Vector2.MoveTowards(posicion, destino, velocidadVuelo * Time.fixedDeltaTime);
            return Vector2.Distance(posicion, destino) < 0.05f;
        }

        void Mirar(float x)
        {
            if (sprite != null && Mathf.Abs(x - posicion.x) > 0.05f) sprite.flipX = x < posicion.x;
        }

        void OnTriggerEnter2D(Collider2D otro)
        {
            ControladorJugador c = otro.GetComponentInParent<ControladorJugador>();
            if (c == null) return;
            jugador = c.transform;
            jugadorCerca = true;
        }

        void OnTriggerExit2D(Collider2D otro)
        {
            if (otro.GetComponentInParent<ControladorJugador>() != null) jugadorCerca = false;
        }
    }
}
