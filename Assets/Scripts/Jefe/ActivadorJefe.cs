using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Trigger a la entrada de la arena. Cierra la compuerta, empieza la pelea y la reinicia
    /// si el jugador pierde todas las celdas (el jugador reaparece en la antena previa a la arena).
    /// </summary>
    public class ActivadorJefe : MonoBehaviour
    {
        [SerializeField] JefeBobina jefe;
        [Tooltip("Compuerta que se cierra detrás del jugador al entrar a la arena.")]
        [SerializeField] GameObject compuertaEntrada;
        [Tooltip("Mensaje de Rosa antes de la pelea (opcional).")]
        [SerializeField, TextArea] string[] mensajeRosa;

        bool activado;
        bool mensajeMostrado;
        VidaJugador vidaJugador;

        void Start()
        {
            if (compuertaEntrada != null) compuertaEntrada.SetActive(false);
            vidaJugador = FindAnyObjectByType<VidaJugador>();
            if (vidaJugador != null) vidaJugador.AlMorir += AlMorirJugador;
        }

        void OnDestroy()
        {
            if (vidaJugador != null) vidaJugador.AlMorir -= AlMorirJugador;
        }

        void OnTriggerEnter2D(Collider2D otro)
        {
            if (activado || jefe == null || jefe.Derrotado) return;
            if (otro.GetComponentInParent<ControladorJugador>() == null) return;

            activado = true;
            if (compuertaEntrada != null) compuertaEntrada.SetActive(true);

            if (!mensajeMostrado && mensajeRosa != null && mensajeRosa.Length > 0 && SistemaDialogo.Instancia != null)
            {
                mensajeMostrado = true;
                SistemaDialogo.Instancia.Mostrar("Rosa (grabación)", mensajeRosa, jefe.Iniciar);
            }
            else
            {
                jefe.Iniciar();
            }
        }

        void AlMorirJugador()
        {
            if (!activado || jefe == null || jefe.Derrotado) return;
            activado = false;
            jefe.Reiniciar();
            if (compuertaEntrada != null) compuertaEntrada.SetActive(false);
        }
    }
}
