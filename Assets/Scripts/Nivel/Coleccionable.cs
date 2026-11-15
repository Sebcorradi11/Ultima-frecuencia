using UnityEngine;

namespace UltimaFrecuencia
{
    public enum TipoColeccionable { Chatarra, Cinta, Bateria }

    /// <summary>
    /// Objetos recogibles del sistema de recompensas: chatarra (moneda), Cintas de Rosa
    /// (coleccionable narrativo) y celdas de batería (curan una celda).
    /// Requiere un Collider2D (Is Trigger).
    /// </summary>
    public class Coleccionable : MonoBehaviour
    {
        [SerializeField] TipoColeccionable tipo = TipoColeccionable.Chatarra;
        [Tooltip("Chatarra: piezas que suma. Batería: celdas que cura.")]
        [SerializeField] int valor = 10;
        [Tooltip("Solo para cintas: identificador único (por ejemplo \"1-1_A\").")]
        [SerializeField] string idCinta = "1-1_A";
        [Tooltip("Solo para cintas: mensaje de Rosa que se reproduce al recogerla.")]
        [SerializeField, TextArea(2, 4)] string[] mensajeCinta;

        [Header("Animación")]
        [SerializeField] float amplitud = 0.12f;
        [SerializeField] float frecuenciaFlotacion = 2.5f;

        public TipoColeccionable Tipo { get { return tipo; } }

        Vector3 posicionInicial;
        float fase;
        bool recogido;

        void Awake()
        {
            posicionInicial = transform.position;
            fase = posicionInicial.x * 0.7f;
        }

        void Update()
        {
            transform.position = posicionInicial + Vector3.up * Mathf.Sin(Time.time * frecuenciaFlotacion + fase) * amplitud;
        }

        void OnTriggerEnter2D(Collider2D otro)
        {
            if (recogido) return;
            VidaJugador vida = otro.GetComponentInParent<VidaJugador>();
            if (vida == null) return;
            if (tipo == TipoColeccionable.Bateria && vida.Celdas >= vida.CeldasMaximas) return;

            recogido = true;
            GestorJuego g = GestorJuego.Instancia;
            switch (tipo)
            {
                case TipoColeccionable.Chatarra:
                    if (g != null) g.SumarChatarra(valor);
                    GestorAudio.Reproducir(Sonido.Chatarra);
                    break;
                case TipoColeccionable.Cinta:
                    if (g != null) g.RegistrarCinta(idCinta);
                    GestorAudio.Reproducir(Sonido.Cinta);
                    HUD.MostrarMensaje("¡Cinta de Rosa recuperada!");
                    if (mensajeCinta != null && mensajeCinta.Length > 0 && SistemaDialogo.Instancia != null)
                        SistemaDialogo.Instancia.Mostrar("Rosa (grabación)", mensajeCinta, null);
                    break;
                case TipoColeccionable.Bateria:
                    vida.Curar(valor);
                    GestorAudio.Reproducir(Sonido.Cinta);
                    HUD.MostrarMensaje("Celda de batería recuperada");
                    break;
            }
            Destroy(gameObject);
        }
    }
}
