using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Jefe de la Zona 1: "La Bobina", una bobina de alta tensión corrompida por ESTÁTICA.
    /// Patrón por ciclos (máquina de estados con corrutinas):
    ///   1. Ráfaga: dispara descargas de una frecuencia → el jugador las esquiva cambiando de sintonía.
    ///   2. (Fase 2) Descarga de suelo: electrifica el suelo Base → hay que subirse a una plataforma de frecuencia.
    ///   3. Sobrecarga: baja y expone el núcleo unos segundos → el jugador lo pisa para dañarlo.
    /// 6 puntos de vida; a la mitad pasa a la Fase 2 (más rápida, alterna frecuencias y agrega la descarga de suelo).
    /// </summary>
    public class JefeBobina : MonoBehaviour
    {
        public enum Estado { Inactivo, Rafaga, DescargaSuelo, Sobrecarga, Derrotado }

        [Header("Vida")]
        [SerializeField] int vidaMaxima = 6;
        [SerializeField] string nombreVisible = "LA BOBINA";

        [Header("Partes")]
        [Tooltip("Sprite principal (se tiñe al cargar cada disparo).")]
        [SerializeField] SpriteRenderer cuerpo;
        [Tooltip("Hijo con el script PuntoDebil y su trigger. Solo está activo durante la sobrecarga.")]
        [SerializeField] PuntoDebil nucleo;
        [SerializeField] Transform puntoDisparo;

        [Header("Ráfaga")]
        [SerializeField] ProyectilFrecuencia prefabProyectil;
        [SerializeField] float velocidadProyectil = 6f;
        [SerializeField] int disparosFase1 = 3;
        [SerializeField] int disparosFase2 = 5;
        [SerializeField] float intervaloFase1 = 0.75f;
        [SerializeField] float intervaloFase2 = 0.45f;
        [SerializeField] float tiempoCarga = 0.35f;
        [SerializeField] float dispersion = 6f;

        [Header("Descarga de suelo (Fase 2)")]
        [Tooltip("Objeto con Collider2D (trigger) + Peligro que cubre el suelo Base de la arena.")]
        [SerializeField] GameObject sueloElectrico;
        [Tooltip("Sprite de aviso que parpadea antes de la descarga.")]
        [SerializeField] SpriteRenderer avisoSuelo;
        [SerializeField] float tiempoAviso = 1.1f;
        [SerializeField] float duracionDescarga = 2f;

        [Header("Sobrecarga")]
        [Tooltip("Cuánto baja la bobina para que el núcleo quede al alcance de un salto.")]
        [SerializeField] float descenso = 3f;
        [SerializeField] float tiempoMovimiento = 0.5f;
        [SerializeField] float vulnerableFase1 = 3f;
        [SerializeField] float vulnerableFase2 = 2.2f;

        [Header("Derrota")]
        [Tooltip("Objetos que se desactivan al vencer (por ejemplo, la compuerta hacia la antena de salida).")]
        [SerializeField] GameObject[] desactivarAlDerrotar;
        [Tooltip("Objetos que se activan al vencer (por ejemplo, la antena de salida).")]
        [SerializeField] GameObject[] activarAlDerrotar;
        [SerializeField] GameObject efectoExplosion;

        public Estado EstadoActual { get; private set; }
        public int Vida { get; private set; }
        public int VidaMaxima { get { return vidaMaxima; } }
        public string NombreVisible { get { return nombreVisible; } }
        public bool Vulnerable { get; private set; }
        public bool Derrotado { get { return EstadoActual == Estado.Derrotado; } }
        bool EnFase2 { get { return Vida <= vidaMaxima / 2; } }

        public event Action<JefeBobina> AlIniciar;
        public event Action<int, int> AlCambiarVida;
        public event Action AlDerrotar;
        public event Action AlReiniciar;

        Vector3 posicionArriba;
        Transform jugador;
        Frecuencia frecuenciaRafaga = Frecuencia.Ambar;
        Color colorBase = Color.white;
        readonly List<GameObject> proyectiles = new List<GameObject>();
        Coroutine combate;

        void Awake()
        {
            posicionArriba = transform.position;
            Vida = vidaMaxima;
            if (cuerpo != null) colorBase = cuerpo.color;
            if (nucleo != null) nucleo.Configurar(this);
            PrepararEscena();
        }

        void Start()
        {
            ControladorJugador c = FindAnyObjectByType<ControladorJugador>();
            if (c != null) jugador = c.transform;
        }

        void PrepararEscena()
        {
            Vulnerable = false;
            if (nucleo != null) nucleo.gameObject.SetActive(false);
            if (sueloElectrico != null) sueloElectrico.SetActive(false);
            if (avisoSuelo != null) avisoSuelo.enabled = false;
        }

        // ---------------- API pública ----------------

        public void Iniciar()
        {
            if (EstadoActual != Estado.Inactivo) return;
            if (AlIniciar != null) AlIniciar(this);
            if (AlCambiarVida != null) AlCambiarVida(Vida, vidaMaxima);
            GestorAudio.CambiarAMusicaJefe(true);
            combate = StartCoroutine(Combate());
        }

        /// <summary>Se llama cuando el jugador muere: la pelea vuelve a empezar desde cero.</summary>
        public void Reiniciar()
        {
            if (Derrotado) return;
            if (combate != null) StopCoroutine(combate);
            StopAllCoroutines();
            LimpiarProyectiles();
            Vida = vidaMaxima;
            EstadoActual = Estado.Inactivo;
            transform.position = posicionArriba;
            frecuenciaRafaga = Frecuencia.Ambar;
            if (cuerpo != null) cuerpo.color = colorBase;
            PrepararEscena();
            GestorAudio.CambiarAMusicaJefe(false);
            if (AlReiniciar != null) AlReiniciar();
        }

        /// <summary>Lo llama PuntoDebil cuando el jugador pisa el núcleo expuesto.</summary>
        public void RecibirGolpe()
        {
            if (!Vulnerable || Derrotado) return;
            Vulnerable = false;
            Vida--;
            if (AlCambiarVida != null) AlCambiarVida(Vida, vidaMaxima);
            GestorAudio.Reproducir(Sonido.GolpeJefe);
            CamaraSeguimiento.Sacudir(0.25f, 0.2f);
            StartCoroutine(Destello(Color.white, 0.15f));

            if (Vida <= 0)
            {
                StopAllCoroutines();
                StartCoroutine(SecuenciaDerrota());
            }
        }

        // ---------------- Máquina de estados ----------------

        IEnumerator Combate()
        {
            yield return new WaitForSeconds(1.2f);
            while (Vida > 0)
            {
                // 1. Ráfaga
                EstadoActual = Estado.Rafaga;
                int disparos = EnFase2 ? disparosFase2 : disparosFase1;
                float intervalo = EnFase2 ? intervaloFase2 : intervaloFase1;
                for (int i = 0; i < disparos; i++)
                {
                    Frecuencia f = EnFase2 ? (i % 2 == 0 ? frecuenciaRafaga : Otra(frecuenciaRafaga)) : frecuenciaRafaga;
                    yield return Cargar(f);
                    Disparar(f);
                    yield return new WaitForSeconds(intervalo);
                }
                frecuenciaRafaga = Otra(frecuenciaRafaga);
                yield return new WaitForSeconds(0.5f);

                // 2. Descarga de suelo (solo Fase 2)
                if (EnFase2 && sueloElectrico != null)
                {
                    EstadoActual = Estado.DescargaSuelo;
                    yield return AvisarSuelo();
                    sueloElectrico.SetActive(true);
                    GestorAudio.Reproducir(Sonido.Descarga);
                    CamaraSeguimiento.Sacudir(duracionDescarga, 0.05f);
                    yield return new WaitForSeconds(duracionDescarga);
                    sueloElectrico.SetActive(false);
                    yield return new WaitForSeconds(0.4f);
                }

                // 3. Sobrecarga: núcleo expuesto
                EstadoActual = Estado.Sobrecarga;
                GestorAudio.Reproducir(Sonido.Sobrecarga);
                yield return MoverA(posicionArriba + Vector3.down * descenso);
                Vulnerable = true;
                if (nucleo != null) nucleo.gameObject.SetActive(true);

                float fin = Time.time + (EnFase2 ? vulnerableFase2 : vulnerableFase1);
                while (Time.time < fin && Vulnerable) yield return null;

                Vulnerable = false;
                if (nucleo != null) nucleo.gameObject.SetActive(false);
                if (Vida <= 0) yield break;
                yield return new WaitForSeconds(0.3f);
                yield return MoverA(posicionArriba);
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator Cargar(Frecuencia f)
        {
            GestorAudio.Reproducir(Sonido.CargaJefe);
            Color destino = FrecuenciaUtil.ColorDe(f);
            float t = 0f;
            while (t < tiempoCarga)
            {
                t += Time.deltaTime;
                if (cuerpo != null) cuerpo.color = Color.Lerp(colorBase, destino, t / tiempoCarga);
                yield return null;
            }
        }

        void Disparar(Frecuencia f)
        {
            if (cuerpo != null) cuerpo.color = colorBase;
            if (prefabProyectil == null || puntoDisparo == null) return;

            Vector2 origen = puntoDisparo.position;
            Vector2 objetivo = jugador != null ? (Vector2)jugador.position : origen + Vector2.left;
            Vector2 dir = (objetivo - origen).normalized;
            dir = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-dispersion, dispersion)) * dir;

            ProyectilFrecuencia p = Instantiate(prefabProyectil, origen, Quaternion.identity);
            p.Inicializar(f, dir * velocidadProyectil);
            proyectiles.Add(p.gameObject);
            GestorAudio.Reproducir(Sonido.DisparoJefe);
        }

        IEnumerator AvisarSuelo()
        {
            if (avisoSuelo == null) { yield return new WaitForSeconds(tiempoAviso); yield break; }
            float fin = Time.time + tiempoAviso;
            GestorAudio.Reproducir(Sonido.Alerta);
            while (Time.time < fin)
            {
                avisoSuelo.enabled = !avisoSuelo.enabled;
                yield return new WaitForSeconds(0.12f);
            }
            avisoSuelo.enabled = false;
        }

        IEnumerator MoverA(Vector3 destino)
        {
            Vector3 inicio = transform.position;
            float t = 0f;
            while (t < tiempoMovimiento)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / tiempoMovimiento);
                transform.position = Vector3.Lerp(inicio, destino, k);
                yield return null;
            }
            transform.position = destino;
        }

        IEnumerator Destello(Color color, float duracion)
        {
            if (cuerpo == null) yield break;
            cuerpo.color = color;
            yield return new WaitForSeconds(duracion);
            cuerpo.color = colorBase;
        }

        IEnumerator SecuenciaDerrota()
        {
            EstadoActual = Estado.Derrotado;
            Vulnerable = false;
            LimpiarProyectiles();
            PrepararEscena();
            GestorAudio.CambiarAMusicaJefe(false);

            // Parpadeo y temblor antes de explotar.
            Vector3 pos = transform.position;
            float fin = Time.time + 1.6f;
            while (Time.time < fin)
            {
                transform.position = pos + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.12f);
                if (cuerpo != null) cuerpo.color = cuerpo.color == Color.white ? colorBase : Color.white;
                yield return new WaitForSeconds(0.06f);
            }
            transform.position = pos;

            GestorAudio.Reproducir(Sonido.Explosion);
            CamaraSeguimiento.Sacudir(0.5f, 0.35f);
            if (efectoExplosion != null) Instantiate(efectoExplosion, transform.position, Quaternion.identity);

            for (int i = 0; i < desactivarAlDerrotar.Length; i++) if (desactivarAlDerrotar[i] != null) desactivarAlDerrotar[i].SetActive(false);
            for (int i = 0; i < activarAlDerrotar.Length; i++) if (activarAlDerrotar[i] != null) activarAlDerrotar[i].SetActive(true);

            if (GestorJuego.Instancia != null) GestorJuego.Instancia.RegistrarJefeDerrotado();
            if (AlDerrotar != null) AlDerrotar();
            gameObject.SetActive(false);
        }

        void LimpiarProyectiles()
        {
            for (int i = 0; i < proyectiles.Count; i++) if (proyectiles[i] != null) Destroy(proyectiles[i]);
            proyectiles.Clear();
        }

        static Frecuencia Otra(Frecuencia f)
        {
            return f == Frecuencia.Ambar ? Frecuencia.Cian : Frecuencia.Ambar;
        }
    }
}
