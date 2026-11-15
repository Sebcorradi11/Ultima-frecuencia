using System;
using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Mecánica central del juego. Guarda la frecuencia activa y avisa a todos los objetos
    /// (capas de tiles, enemigos, HUD, audio) cuando cambia.
    /// Va en un GameObject vacío "Gestores" de la escena del nivel.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class GestorFrecuencia : MonoBehaviour
    {
        public static GestorFrecuencia Instancia { get; private set; }

        [SerializeField] Frecuencia frecuenciaInicial = Frecuencia.Ambar;

        [Tooltip("Zona 1 = 2 (Ámbar y Cian). La Onda Magenta se desbloquea en la Zona 3.")]
        [SerializeField, Range(1, 3)] int frecuenciasDesbloqueadas = 2;

        [Tooltip("Tiempo mínimo entre dos cambios (Tabla de físicas del GDD: 0,35 s).")]
        [SerializeField] float enfriamiento = 0.35f;

        public Frecuencia Activa { get; private set; }
        public int Desbloqueadas { get { return frecuenciasDesbloqueadas; } }

        /// <summary>Se dispara cada vez que cambia la frecuencia.</summary>
        public event Action<Frecuencia> AlCambiar;
        /// <summary>Se dispara cuando el cambio se bloquea porque el jugador quedaría dentro de una pared.</summary>
        public event Action AlBloquear;

        float proximoCambioPermitido;

        void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            Instancia = this;
            Activa = frecuenciaInicial;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        /// <summary>True si la frecuencia indicada es la activa. Sin gestor en escena, todo cuenta como activo.</summary>
        public static bool EstaActiva(Frecuencia f)
        {
            return Instancia == null || Instancia.Activa == f;
        }

        /// <summary>Capas sólidas en este momento: Base + frecuencia activa. Se usa para el chequeo de suelo y la línea de visión.</summary>
        public int MascaraSolidaActiva
        {
            get { return LayerMask.GetMask(FrecuenciaUtil.CapaBase) | FrecuenciaUtil.Mascara(Activa); }
        }

        /// <summary>Capas de frecuencias inactivas: el Rigidbody2D del jugador las excluye para atravesarlas.</summary>
        public int MascaraInactivas
        {
            get
            {
                int m = 0;
                for (int i = 0; i < FrecuenciaUtil.Cantidad; i++)
                {
                    Frecuencia f = (Frecuencia)i;
                    if (f != Activa) m |= FrecuenciaUtil.Mascara(f);
                }
                return m;
            }
        }

        /// <summary>
        /// Intenta pasar a la frecuencia siguiente (+1) o anterior (-1).
        /// Regla 3 del GDD: si el cuerpo del jugador se superpone con un sólido de la frecuencia
        /// de destino, el cambio se cancela para no quedar atrapado.
        /// </summary>
        public bool IntentarCambiar(int direccion, Collider2D cuerpoJugador)
        {
            if (Time.time < proximoCambioPermitido || frecuenciasDesbloqueadas < 2) return false;

            Frecuencia destino = FrecuenciaUtil.Desplazar(Activa, direccion, frecuenciasDesbloqueadas);

            if (cuerpoJugador != null)
            {
                Bounds b = cuerpoJugador.bounds;
                Collider2D choque = Physics2D.OverlapBox(b.center, b.size * 0.9f, 0f, FrecuenciaUtil.Mascara(destino));
                if (choque != null)
                {
                    if (AlBloquear != null) AlBloquear();
                    GestorAudio.Reproducir(Sonido.Bloqueo);
                    return false;
                }
            }

            Fijar(destino);
            proximoCambioPermitido = Time.time + enfriamiento;
            return true;
        }

        /// <summary>Cambia la frecuencia sin comprobaciones (para reinicios o eventos guionados).</summary>
        public void Fijar(Frecuencia f)
        {
            Activa = f;
            if (AlCambiar != null) AlCambiar(f);
            GestorAudio.ReproducirCambio(f);
        }

        public void Desbloquear(int cantidad)
        {
            frecuenciasDesbloqueadas = Mathf.Clamp(Mathf.Max(frecuenciasDesbloqueadas, cantidad), 1, FrecuenciaUtil.Cantidad);
        }
    }
}
