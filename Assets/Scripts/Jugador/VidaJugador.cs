using System;
using System.Collections;
using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Celdas de batería de DIAL-7 (reglas 5 y 6 del GDD): daño, invulnerabilidad temporal,
    /// caída al vacío, reaparición en la última antena de relevo y muerte.
    /// </summary>
    [RequireComponent(typeof(ControladorJugador))]
    public class VidaJugador : MonoBehaviour
    {
        [SerializeField] int celdasMaximas = 3;
        [SerializeField] float invulnerabilidad = 1f;
        [SerializeField] Vector2 empujeAlRecibirDano = new Vector2(6f, 9f);
        [SerializeField] float duracionEmpuje = 0.25f;
        [SerializeField] float esperaAntesDeReaparecer = 1f;

        public int Celdas { get; private set; }
        public int CeldasMaximas { get { return celdasMaximas; } }
        public bool Invulnerable { get { return Time.time < invulnerableHasta || reapareciendo; } }

        /// <summary>(celdas actuales, celdas máximas)</summary>
        public event Action<int, int> AlCambiarVida;
        /// <summary>Se dispara al perder todas las celdas (el jefe se reinicia con este evento).</summary>
        public event Action AlMorir;

        ControladorJugador controlador;
        Rigidbody2D rb;
        SpriteRenderer[] sprites;
        Coroutine parpadeo;
        Vector3 puntoReaparicion;
        float invulnerableHasta;
        bool reapareciendo;

        void Awake()
        {
            controlador = GetComponent<ControladorJugador>();
            rb = GetComponent<Rigidbody2D>();
            sprites = GetComponentsInChildren<SpriteRenderer>();
            Celdas = celdasMaximas;
            puntoReaparicion = transform.position;
        }

        void Start()
        {
            Notificar();
        }

        void Notificar()
        {
            if (AlCambiarVida != null) AlCambiarVida(Celdas, celdasMaximas);
        }

        public void FijarPuntoReaparicion(Vector3 posicion)
        {
            puntoReaparicion = posicion;
        }

        public void RecibirDano(int cantidad, Vector2 origen)
        {
            if (Invulnerable || cantidad <= 0) return;

            Celdas = Mathf.Max(0, Celdas - cantidad);
            Notificar();
            GestorAudio.Reproducir(Sonido.Dano);
            CamaraSeguimiento.Sacudir(0.15f, 0.12f);

            if (Celdas <= 0)
            {
                StartCoroutine(Morir());
                return;
            }

            float lado = transform.position.x >= origen.x ? 1f : -1f;
            controlador.Empujar(new Vector2(empujeAlRecibirDano.x * lado, empujeAlRecibirDano.y), duracionEmpuje);
            invulnerableHasta = Time.time + invulnerabilidad;
            IniciarParpadeo();
        }

        void IniciarParpadeo()
        {
            if (parpadeo != null) StopCoroutine(parpadeo);
            parpadeo = StartCoroutine(Parpadear(invulnerabilidad));
        }

        /// <summary>Caer al vacío: vuelve a la última antena con una celda menos.</summary>
        public void CaerAlVacio()
        {
            if (reapareciendo) return;
            if (GestorJuego.Instancia != null) GestorJuego.Instancia.RegistrarCaida();

            Celdas = Mathf.Max(0, Celdas - 1);
            Notificar();
            GestorAudio.Reproducir(Sonido.Dano);

            if (Celdas <= 0) StartCoroutine(Morir());
            else StartCoroutine(Reaparecer(false));
        }

        public void Curar(int cantidad)
        {
            Celdas = Mathf.Min(celdasMaximas, Celdas + cantidad);
            Notificar();
        }

        IEnumerator Morir()
        {
            if (GestorJuego.Instancia != null) GestorJuego.Instancia.RegistrarMuerte();
            if (AlMorir != null) AlMorir();
            yield return Reaparecer(true);
        }

        IEnumerator Reaparecer(bool restaurarCeldas)
        {
            reapareciendo = true;
            if (parpadeo != null) StopCoroutine(parpadeo);
            controlador.enabled = false;
            rb.simulated = false;
            Visible(false);
            yield return new WaitForSeconds(esperaAntesDeReaparecer);

            controlador.Teletransportar(puntoReaparicion);
            rb.simulated = true;
            if (restaurarCeldas)
            {
                Celdas = celdasMaximas;
                Notificar();
            }
            controlador.enabled = true;
            Visible(true);
            reapareciendo = false;
            invulnerableHasta = Time.time + invulnerabilidad;
            IniciarParpadeo();
        }

        IEnumerator Parpadear(float duracion)
        {
            float fin = Time.time + duracion;
            bool visible = true;
            while (Time.time < fin)
            {
                visible = !visible;
                Visible(visible);
                yield return new WaitForSeconds(0.08f);
            }
            Visible(true);
        }

        void Visible(bool v)
        {
            for (int i = 0; i < sprites.Length; i++) sprites[i].enabled = v;
        }
    }
}
