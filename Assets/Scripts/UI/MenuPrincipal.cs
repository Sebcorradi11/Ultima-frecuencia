using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Menú principal (escena "MenuPrincipal"). Botones (OnClick):
    /// Jugar → Jugar · Opciones → AbrirOpciones · Créditos → AbrirCreditos · Volver → Volver · Salir → Salir.
    /// El slider de volumen llama a CambiarVolumen (On Value Changed, Dynamic float).
    /// </summary>
    public class MenuPrincipal : MonoBehaviour
    {
        [SerializeField] string escenaNivel = "Nivel1";
        [SerializeField] GameObject panelPrincipal;
        [SerializeField] GameObject panelOpciones;
        [SerializeField] GameObject panelCreditos;
        [SerializeField] Slider sliderVolumen;
        [SerializeField] GameObject primerBotonPrincipal;
        [SerializeField] GameObject primerBotonOpciones;
        [SerializeField] GameObject botonVolverCreditos;

        void Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            float vol = PlayerPrefs.GetFloat("volumenGeneral", 1f);
            AudioListener.volume = vol;
            if (sliderVolumen != null) sliderVolumen.SetValueWithoutNotify(vol);
            Volver();
        }

        public void Jugar()
        {
            SceneManager.LoadScene(escenaNivel);
        }

        public void AbrirOpciones() { Mostrar(panelOpciones, primerBotonOpciones); }
        public void AbrirCreditos() { Mostrar(panelCreditos, botonVolverCreditos); }
        public void Volver() { Mostrar(panelPrincipal, primerBotonPrincipal); }

        void Mostrar(GameObject panel, GameObject seleccionar)
        {
            if (panelPrincipal != null) panelPrincipal.SetActive(panel == panelPrincipal);
            if (panelOpciones != null) panelOpciones.SetActive(panel == panelOpciones);
            if (panelCreditos != null) panelCreditos.SetActive(panel == panelCreditos);
            if (seleccionar != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(seleccionar);
        }

        public void CambiarVolumen(float valor)
        {
            AudioListener.volume = valor;
            PlayerPrefs.SetFloat("volumenGeneral", valor);
        }

        public void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
