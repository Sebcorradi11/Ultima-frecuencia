using UnityEngine;
using UnityEngine.EventSystems;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Menú de pausa (Esc / Start). Conectar los botones desde el Inspector (OnClick):
    /// Continuar → MenuPausa.Continuar · Reiniciar → MenuPausa.Reiniciar · Salir → MenuPausa.IrAlMenu.
    /// </summary>
    public class MenuPausa : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [Tooltip("Botón que queda seleccionado al abrir (para navegar con teclado o mando).")]
        [SerializeField] GameObject primerBoton;

        void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            GestorJuego g = GestorJuego.Instancia;
            if (g == null || g.NivelTerminado || SistemaDialogo.Activo) return;
            if (Entrada.Pausa()) Alternar(!g.EnPausa);
        }

        void Alternar(bool pausar)
        {
            GestorJuego.Instancia.Pausar(pausar);
            if (panel != null) panel.SetActive(pausar);
            if (pausar && primerBoton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(primerBoton);
            GestorAudio.Reproducir(Sonido.UI);
        }

        public void Continuar() { Alternar(false); }
        public void Reiniciar() { GestorJuego.Instancia.Reiniciar(); }
        public void IrAlMenu() { GestorJuego.Instancia.IrAlMenu(); }
    }
}
