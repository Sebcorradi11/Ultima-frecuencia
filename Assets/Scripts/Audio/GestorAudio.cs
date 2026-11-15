using System;
using UnityEngine;

namespace UltimaFrecuencia
{
    public enum Sonido
    {
        Salto, Aterrizaje, CambioAmbar, CambioCian, CambioMagenta, Bloqueo, Dano,
        Chatarra, Cinta, Antena, Pisoton, Alerta, Pip, Dialogo, UI,
        CargaJefe, DisparoJefe, GolpeJefe, Sobrecarga, Descarga, Explosion, Victoria
    }

    /// <summary>
    /// Audio del nivel (Dirección de Audio del GDD). La música tiene tres variantes, una por
    /// frecuencia, que suenan sincronizadas: al cambiar de sintonía solo cambia el volumen
    /// (crossfade), así la canción no se corta. Los efectos se reproducen por nombre desde
    /// cualquier script con GestorAudio.Reproducir(Sonido.X).
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class GestorAudio : MonoBehaviour
    {
        [Serializable]
        public class EntradaSonido
        {
            public Sonido sonido;
            public AudioClip clip;
            [Range(0f, 1f)] public float volumen = 0.8f;
            [Tooltip("Variación aleatoria de tono para que no suene repetitivo.")]
            [Range(0f, 0.3f)] public float variacionTono = 0.05f;
        }

        static GestorAudio instancia;

        [Header("Música por frecuencia (mismo tempo y duración)")]
        [SerializeField] AudioClip musicaAmbar;
        [SerializeField] AudioClip musicaCian;
        [SerializeField] AudioClip musicaMagenta;
        [SerializeField] AudioClip musicaJefe;
        [SerializeField, Range(0f, 1f)] float volumenMusica = 0.55f;
        [SerializeField] float velocidadFundido = 2.5f;

        [Header("Efectos")]
        [SerializeField, Range(0f, 1f)] float volumenEfectos = 0.9f;
        [SerializeField] EntradaSonido[] efectos;

        AudioSource[] pistas;
        AudioSource pistaJefe;
        AudioSource fuenteEfectos;
        AudioSource fuenteTono;
        bool modoJefe;

        void Awake()
        {
            instancia = this;
            AudioClip[] clips = { musicaAmbar, musicaCian, musicaMagenta };
            pistas = new AudioSource[clips.Length];
            for (int i = 0; i < clips.Length; i++) pistas[i] = CrearFuente(clips[i], true);
            pistaJefe = CrearFuente(musicaJefe, true);
            fuenteEfectos = CrearFuente(null, false);
            fuenteTono = CrearFuente(null, false);

            float volGuardado = PlayerPrefs.GetFloat("volumenGeneral", 1f);
            AudioListener.volume = volGuardado;
        }

        void Start()
        {
            double inicio = AudioSettings.dspTime + 0.1;
            for (int i = 0; i < pistas.Length; i++)
            {
                if (pistas[i].clip == null) continue;
                pistas[i].volume = 0f;
                pistas[i].PlayScheduled(inicio);
            }
            if (pistaJefe.clip != null)
            {
                pistaJefe.volume = 0f;
                pistaJefe.Play();
            }
        }

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }

        AudioSource CrearFuente(AudioClip clip, bool bucle)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = bucle;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        void Update()
        {
            int activa = GestorFrecuencia.Instancia != null ? (int)GestorFrecuencia.Instancia.Activa : 0;
            for (int i = 0; i < pistas.Length; i++)
            {
                float objetivo = (!modoJefe && i == activa) ? volumenMusica : 0f;
                pistas[i].volume = Mathf.MoveTowards(pistas[i].volume, objetivo, velocidadFundido * Time.unscaledDeltaTime);
            }
            float objetivoJefe = modoJefe ? volumenMusica : 0f;
            pistaJefe.volume = Mathf.MoveTowards(pistaJefe.volume, objetivoJefe, velocidadFundido * Time.unscaledDeltaTime);
        }

        EntradaSonido Buscar(Sonido s)
        {
            if (efectos == null) return null;
            for (int i = 0; i < efectos.Length; i++) if (efectos[i] != null && efectos[i].sonido == s) return efectos[i];
            return null;
        }

        // ---------------- API estática ----------------

        public static void Reproducir(Sonido s)
        {
            Reproducir(s, 1f);
        }

        public static void Reproducir(Sonido s, float multiplicadorVolumen)
        {
            if (instancia == null) return;
            EntradaSonido e = instancia.Buscar(s);
            if (e == null || e.clip == null) return;

            float vol = e.volumen * instancia.volumenEfectos * multiplicadorVolumen;
            if (e.variacionTono > 0f)
            {
                instancia.fuenteTono.pitch = 1f + UnityEngine.Random.Range(-e.variacionTono, e.variacionTono);
                instancia.fuenteTono.PlayOneShot(e.clip, vol);
            }
            else
            {
                instancia.fuenteEfectos.PlayOneShot(e.clip, vol);
            }
        }

        public static void ReproducirCambio(Frecuencia f)
        {
            switch (f)
            {
                case Frecuencia.Ambar: Reproducir(Sonido.CambioAmbar); break;
                case Frecuencia.Cian: Reproducir(Sonido.CambioCian); break;
                default: Reproducir(Sonido.CambioMagenta); break;
            }
        }

        public static void CambiarAMusicaJefe(bool activar)
        {
            if (instancia == null) return;
            instancia.modoJefe = activar && instancia.pistaJefe.clip != null;
            if (instancia.modoJefe) instancia.pistaJefe.time = 0f;
        }
    }
}
