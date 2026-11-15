using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Cámara que sigue al jugador con suavizado, anticipación en la dirección de la mirada,
    /// límites del nivel y sacudida (screen shake). Va en la Main Camera.
    /// Si usan Cinemachine, pueden reemplazarla; la sacudida estática igual funciona sin efecto.
    /// </summary>
    public class CamaraSeguimiento : MonoBehaviour
    {
        static CamaraSeguimiento instancia;

        [SerializeField] Transform objetivo;
        [SerializeField] Vector2 desplazamiento = new Vector2(0f, 1.5f);
        [SerializeField] float anticipacion = 1.5f;
        [SerializeField] float suavizado = 0.15f;

        [Header("Límites del nivel (en unidades)")]
        [SerializeField] bool usarLimites = true;
        [SerializeField] Vector2 limiteMin = new Vector2(0f, -2f);
        [SerializeField] Vector2 limiteMax = new Vector2(120f, 20f);

        ControladorJugador controlador;
        Camera cam;
        Vector3 velocidad;
        Vector3 posicionBase;
        float anticipacionActual;
        float finSacudida;
        float intensidadSacudida;

        void Awake()
        {
            instancia = this;
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (objetivo == null)
            {
                ControladorJugador c = FindAnyObjectByType<ControladorJugador>();
                if (c != null) objetivo = c.transform;
            }
            if (objetivo != null)
            {
                controlador = objetivo.GetComponent<ControladorJugador>();
                transform.position = Destino();
            }
            posicionBase = transform.position;
        }

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }

        public static void Sacudir(float duracion, float intensidad)
        {
            if (instancia == null) return;
            instancia.finSacudida = Mathf.Max(instancia.finSacudida, Time.time + duracion);
            instancia.intensidadSacudida = Mathf.Max(intensidad, Time.time < instancia.finSacudida ? instancia.intensidadSacudida : 0f);
        }

        Vector3 Destino()
        {
            float lado = controlador != null && !controlador.MiraDerecha ? -1f : 1f;
            anticipacionActual = Mathf.Lerp(anticipacionActual, anticipacion * lado, 2.5f * Time.deltaTime);
            Vector3 d = new Vector3(objetivo.position.x + desplazamiento.x + anticipacionActual,
                                    objetivo.position.y + desplazamiento.y,
                                    transform.position.z);

            if (usarLimites && cam != null && cam.orthographic)
            {
                float alto = cam.orthographicSize;
                float ancho = alto * cam.aspect;
                d.x = Mathf.Clamp(d.x, limiteMin.x + ancho, Mathf.Max(limiteMin.x + ancho, limiteMax.x - ancho));
                d.y = Mathf.Clamp(d.y, limiteMin.y + alto, Mathf.Max(limiteMin.y + alto, limiteMax.y - alto));
            }
            return d;
        }

        void LateUpdate()
        {
            if (objetivo == null) return;
            Vector3 destino = Destino();
            posicionBase = Vector3.SmoothDamp(posicionBase, destino, ref velocidad, suavizado);
            Vector3 pos = posicionBase;

            if (Time.time < finSacudida)
            {
                Vector2 r = Random.insideUnitCircle * intensidadSacudida;
                pos += new Vector3(r.x, r.y, 0f);
            }
            transform.position = pos;
        }

        void OnDrawGizmosSelected()
        {
            if (!usarLimites) return;
            Gizmos.color = Color.cyan;
            Vector3 c = new Vector3((limiteMin.x + limiteMax.x) * 0.5f, (limiteMin.y + limiteMax.y) * 0.5f, 0f);
            Gizmos.DrawWireCube(c, new Vector3(limiteMax.x - limiteMin.x, limiteMax.y - limiteMin.y, 0f));
        }
    }
}
