using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Meta del nivel (condición de victoria micro). Solo funciona si el jefe ya fue derrotado.
    /// Requiere un BoxCollider2D (Is Trigger).
    /// </summary>
    public class AntenaSalida : MonoBehaviour
    {
        [SerializeField] bool requiereJefeDerrotado = true;
        [SerializeField, TextArea(2, 4)] string[] mensajeFinal =
        {
            "...aquí Rosa... si escuchás esto, la Subestación Norte vuelve a transmitir.",
            "Una frecuencia menos en silencio. Seguí, DIAL-7. El Barrio Antenas te espera."
        };

        bool usada;

        void OnTriggerEnter2D(Collider2D otro)
        {
            if (usada || otro.GetComponentInParent<ControladorJugador>() == null) return;
            GestorJuego g = GestorJuego.Instancia;
            if (requiereJefeDerrotado && (g == null || !g.JefeDerrotado))
            {
                HUD.MostrarMensaje("La señal todavía está bloqueada");
                return;
            }

            usada = true;
            GestorAudio.Reproducir(Sonido.Victoria);
            if (SistemaDialogo.Instancia != null && mensajeFinal != null && mensajeFinal.Length > 0)
                SistemaDialogo.Instancia.Mostrar("Rosa (transmisión)", mensajeFinal, Terminar);
            else
                Terminar();
        }

        void Terminar()
        {
            if (GestorJuego.Instancia != null) GestorJuego.Instancia.TerminarNivel();
        }
    }
}
