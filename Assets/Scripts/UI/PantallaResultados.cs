using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Pantalla de resultados al llegar a la antena de salida: tiempo, chatarra, cintas,
    /// caídas y medalla "Señal limpia". Botones: Reintentar → Reintentar · Menú → IrAlMenu.
    /// </summary>
    public class PantallaResultados : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text textoTitulo;
        [SerializeField] TMP_Text textoDetalle;
        [SerializeField] GameObject medallaSenalLimpia;
        [SerializeField] GameObject primerBoton;

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (GestorJuego.Instancia != null) GestorJuego.Instancia.AlTerminarNivel += Mostrar;
        }

        void OnDestroy()
        {
            if (GestorJuego.Instancia != null) GestorJuego.Instancia.AlTerminarNivel -= Mostrar;
        }

        void Mostrar()
        {
            GestorJuego g = GestorJuego.Instancia;
            if (panel != null) panel.SetActive(true);
            if (textoTitulo != null) textoTitulo.text = "SEÑAL RESTAURADA\n" + g.NombreNivel;
            if (textoDetalle != null)
            {
                textoDetalle.text =
                    "Tiempo: " + GestorJuego.FormatearTiempo(g.Tiempo) + "   (mejor: " + GestorJuego.FormatearTiempo(g.MejorTiempo()) + ")\n" +
                    "Chatarra: " + g.Chatarra + "\n" +
                    "Cintas de Rosa: " + g.CintasRecogidas + "/" + g.CintasTotales + "\n" +
                    "Caídas: " + g.Caidas + "   Reinicios: " + g.Muertes;
            }
            if (medallaSenalLimpia != null) medallaSenalLimpia.SetActive(g.SenalLimpia);
            if (primerBoton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primerBoton);
        }

        public void Reintentar() { GestorJuego.Instancia.Reiniciar(); }
        public void IrAlMenu() { GestorJuego.Instancia.IrAlMenu(); }
    }
}
