using UnityEngine;
using UnityEngine.Tilemaps;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Se agrega a cada Tilemap de frecuencia (Tilemap_Ambar, Tilemap_Cian) o a cualquier
    /// plataforma suelta con SpriteRenderer. Ajusta la capa física y dibuja la "silueta fantasma"
    /// (transparencia) cuando la frecuencia no está activa.
    /// La colisión NO se apaga acá: el jugador la ignora mediante Rigidbody2D.excludeLayers
    /// (ver ControladorJugador). Así el chequeo de bloqueo puede seguir detectando estos sólidos.
    /// </summary>
    public class CapaFrecuencia : MonoBehaviour
    {
        [SerializeField] Frecuencia frecuencia = Frecuencia.Ambar;

        [Tooltip("Opacidad de la silueta cuando la frecuencia está inactiva.")]
        [SerializeField, Range(0f, 1f)] float alfaInactiva = 0.22f;

        [Tooltip("Velocidad del fundido entre sólido y silueta.")]
        [SerializeField] float velocidadFundido = 18f;

        public Frecuencia Frecuencia { get { return frecuencia; } }

        Tilemap tilemap;
        SpriteRenderer[] sprites;
        float alfaObjetivo = 1f;
        float alfaActual = 1f;

        void Awake()
        {
            tilemap = GetComponent<Tilemap>();
            sprites = GetComponentsInChildren<SpriteRenderer>();

            int capa = LayerMask.NameToLayer(FrecuenciaUtil.NombreCapa(frecuencia));
            if (capa < 0)
                Debug.LogError("Falta crear la Layer '" + FrecuenciaUtil.NombreCapa(frecuencia) + "' en Project Settings > Tags and Layers.", this);
            else
                AsignarCapa(transform, capa);
        }

        void Start()
        {
            if (GestorFrecuencia.Instancia != null)
            {
                GestorFrecuencia.Instancia.AlCambiar += Aplicar;
                Aplicar(GestorFrecuencia.Instancia.Activa);
                alfaActual = alfaObjetivo;
                Pintar(alfaActual);
            }
        }

        void OnDestroy()
        {
            if (GestorFrecuencia.Instancia != null) GestorFrecuencia.Instancia.AlCambiar -= Aplicar;
        }

        void Aplicar(Frecuencia activa)
        {
            alfaObjetivo = activa == frecuencia ? 1f : alfaInactiva;
        }

        void Update()
        {
            if (Mathf.Approximately(alfaActual, alfaObjetivo)) return;
            alfaActual = Mathf.MoveTowards(alfaActual, alfaObjetivo, velocidadFundido * Time.deltaTime);
            Pintar(alfaActual);
        }

        void Pintar(float alfa)
        {
            if (tilemap != null)
            {
                Color c = tilemap.color;
                c.a = alfa;
                tilemap.color = c;
            }
            for (int i = 0; i < sprites.Length; i++)
            {
                Color c = sprites[i].color;
                c.a = alfa;
                sprites[i].color = c;
            }
        }

        static void AsignarCapa(Transform t, int capa)
        {
            t.gameObject.layer = capa;
            for (int i = 0; i < t.childCount; i++) AsignarCapa(t.GetChild(i), capa);
        }
    }
}
