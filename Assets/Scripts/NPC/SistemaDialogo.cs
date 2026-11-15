using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Caja de diálogo con efecto máquina de escribir. Va en el Canvas del HUD.
    /// Mientras está abierta, el jugador no se mueve (GestorJuego.ControlesBloqueados).
    /// Avanzar: Espacio, W, Enter o A/Y en el mando.
    /// </summary>
    public class SistemaDialogo : MonoBehaviour
    {
        public static SistemaDialogo Instancia { get; private set; }
        /// <summary>True con el diálogo abierto y también en el cuadro en que se cierra (para que esa tecla no haga saltar).</summary>
        public static bool Activo { get { return Instancia != null && (Instancia.abierto || Instancia.cuadroCierre == Time.frameCount); } }

        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text textoNombre;
        [SerializeField] TMP_Text textoCuerpo;
        [Tooltip("Indicador \"▼\" que aparece cuando la línea terminó de escribirse.")]
        [SerializeField] GameObject indicadorContinuar;
        [SerializeField] float caracteresPorSegundo = 45f;

        bool abierto;
        bool listo;
        int cuadroCierre = -1;
        string[] lineas;
        int indice;
        bool escribiendo;
        Action alTerminar;
        Coroutine escritura;

        void Awake()
        {
            Instancia = this;
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        public void Mostrar(string nombre, string[] contenido, Action callback)
        {
            if (contenido == null || contenido.Length == 0)
            {
                if (callback != null) callback();
                return;
            }
            lineas = contenido;
            indice = 0;
            alTerminar = callback;
            abierto = true;
            listo = false;
            if (panel != null) panel.SetActive(true);
            if (textoNombre != null) textoNombre.text = nombre;
            StartCoroutine(EmpezarEnElSiguienteCuadro());
        }

        IEnumerator EmpezarEnElSiguienteCuadro()
        {
            // Evita que la misma tecla que abrió el diálogo lo haga avanzar.
            yield return null;
            listo = true;
            EscribirLinea();
        }

        void EscribirLinea()
        {
            if (escritura != null) StopCoroutine(escritura);
            escritura = StartCoroutine(Escribir(lineas[indice]));
        }

        IEnumerator Escribir(string linea)
        {
            escribiendo = true;
            if (indicadorContinuar != null) indicadorContinuar.SetActive(false);
            textoCuerpo.text = linea;
            textoCuerpo.maxVisibleCharacters = 0;
            int total = linea.Length;
            float visibles = 0f;
            int ultimoSonido = -1;
            while (visibles < total)
            {
                visibles += caracteresPorSegundo * Time.unscaledDeltaTime;
                int n = Mathf.Min(total, Mathf.FloorToInt(visibles));
                textoCuerpo.maxVisibleCharacters = n;
                if (n / 3 != ultimoSonido)
                {
                    ultimoSonido = n / 3;
                    GestorAudio.Reproducir(Sonido.Dialogo, 0.35f);
                }
                yield return null;
            }
            TerminarLinea();
        }

        void TerminarLinea()
        {
            escribiendo = false;
            textoCuerpo.maxVisibleCharacters = int.MaxValue;
            if (indicadorContinuar != null) indicadorContinuar.SetActive(true);
        }

        void Update()
        {
            if (!abierto || !listo || lineas == null || !Entrada.Avanzar()) return;

            if (escribiendo)
            {
                if (escritura != null) StopCoroutine(escritura);
                TerminarLinea();
                return;
            }

            indice++;
            if (indice < lineas.Length) EscribirLinea();
            else Cerrar();
        }

        void Cerrar()
        {
            abierto = false;
            listo = false;
            lineas = null;
            cuadroCierre = Time.frameCount;
            if (panel != null) panel.SetActive(false);
            Action cb = alTerminar;
            alTerminar = null;
            if (cb != null) cb();
        }
    }
}
