using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UltimaFrecuencia
{
    /// <summary>
    /// HUD de la maqueta del GDD: celdas de batería (arriba a la izquierda), dial de frecuencias
    /// (arriba al centro), chatarra y cintas (arriba a la derecha), barra del jefe y mensajes.
    /// Va en el Canvas (Screen Space - Overlay, Canvas Scaler con 1920×1080 de referencia).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        static HUD instancia;

        [Header("Batería")]
        [SerializeField] Image[] celdas;
        [SerializeField] Sprite celdaLlena;
        [SerializeField] Sprite celdaVacia;

        [Header("Dial de frecuencias (Ámbar, Cian, Magenta en ese orden)")]
        [SerializeField] Image[] indicadores;
        [SerializeField] RectTransform dial;
        [SerializeField] TMP_Text textoFrecuencia;

        [Header("Contadores")]
        [SerializeField] TMP_Text textoChatarra;
        [SerializeField] TMP_Text textoCintas;
        [SerializeField] TMP_Text textoTiempo;

        [Header("Jefe")]
        [SerializeField] GameObject panelJefe;
        [Tooltip("Image con Image Type = Filled, Fill Method = Horizontal.")]
        [SerializeField] Image barraJefe;
        [SerializeField] TMP_Text nombreJefe;

        [Header("Mensajes")]
        [SerializeField] TMP_Text textoMensaje;
        [SerializeField] float duracionMensaje = 2.2f;

        VidaJugador vida;
        JefeBobina jefe;
        Coroutine rutinaMensaje;
        Coroutine rutinaTemblor;
        Vector2 posDial;

        void Awake()
        {
            instancia = this;
            if (panelJefe != null) panelJefe.SetActive(false);
            if (textoMensaje != null) textoMensaje.gameObject.SetActive(false);
            if (dial != null) posDial = dial.anchoredPosition;
        }

        void Start()
        {
            vida = FindAnyObjectByType<VidaJugador>();
            if (vida != null)
            {
                vida.AlCambiarVida += ActualizarVida;
                ActualizarVida(vida.Celdas, vida.CeldasMaximas);
            }

            GestorFrecuencia gf = GestorFrecuencia.Instancia;
            if (gf != null)
            {
                gf.AlCambiar += ActualizarFrecuencia;
                gf.AlBloquear += Temblor;
                ActualizarFrecuencia(gf.Activa);
            }

            GestorJuego gj = GestorJuego.Instancia;
            if (gj != null)
            {
                gj.AlCambiarChatarra += ActualizarChatarra;
                gj.AlCambiarCintas += ActualizarCintas;
                ActualizarChatarra(gj.Chatarra);
                ActualizarCintas(gj.CintasRecogidas, gj.CintasTotales);
            }

            jefe = FindAnyObjectByType<JefeBobina>();
            if (jefe != null)
            {
                jefe.AlIniciar += MostrarJefe;
                jefe.AlCambiarVida += ActualizarJefe;
                jefe.AlDerrotar += OcultarJefe;
                jefe.AlReiniciar += OcultarJefe;
            }
        }

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
            if (vida != null) vida.AlCambiarVida -= ActualizarVida;
            if (GestorFrecuencia.Instancia != null)
            {
                GestorFrecuencia.Instancia.AlCambiar -= ActualizarFrecuencia;
                GestorFrecuencia.Instancia.AlBloquear -= Temblor;
            }
            if (GestorJuego.Instancia != null)
            {
                GestorJuego.Instancia.AlCambiarChatarra -= ActualizarChatarra;
                GestorJuego.Instancia.AlCambiarCintas -= ActualizarCintas;
            }
            if (jefe != null)
            {
                jefe.AlIniciar -= MostrarJefe;
                jefe.AlCambiarVida -= ActualizarJefe;
                jefe.AlDerrotar -= OcultarJefe;
                jefe.AlReiniciar -= OcultarJefe;
            }
        }

        void Update()
        {
            if (textoTiempo != null && GestorJuego.Instancia != null)
                textoTiempo.text = GestorJuego.FormatearTiempo(GestorJuego.Instancia.Tiempo);
        }

        // ---------------- Actualizaciones ----------------

        void ActualizarVida(int actuales, int maximas)
        {
            if (celdas == null) return;
            for (int i = 0; i < celdas.Length; i++)
            {
                celdas[i].gameObject.SetActive(i < maximas);
                if (celdaLlena != null && celdaVacia != null) celdas[i].sprite = i < actuales ? celdaLlena : celdaVacia;
                else celdas[i].color = i < actuales ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        void ActualizarFrecuencia(Frecuencia activa)
        {
            int desbloqueadas = GestorFrecuencia.Instancia != null ? GestorFrecuencia.Instancia.Desbloqueadas : 3;
            if (indicadores != null)
            {
                for (int i = 0; i < indicadores.Length; i++)
                {
                    Color c = FrecuenciaUtil.ColorDe((Frecuencia)i);
                    bool esActiva = i == (int)activa;
                    bool bloqueada = i >= desbloqueadas;
                    c.a = bloqueada ? 0.12f : (esActiva ? 1f : 0.35f);
                    indicadores[i].color = c;
                    indicadores[i].rectTransform.localScale = Vector3.one * (esActiva ? 1.2f : 1f);
                }
            }
            if (textoFrecuencia != null)
            {
                textoFrecuencia.text = FrecuenciaUtil.NombreVisible(activa);
                textoFrecuencia.color = FrecuenciaUtil.ColorDe(activa);
            }
        }

        void ActualizarChatarra(int cantidad)
        {
            if (textoChatarra != null) textoChatarra.text = "x " + cantidad;
        }

        void ActualizarCintas(int recogidas, int totales)
        {
            if (textoCintas != null) textoCintas.text = "Cintas " + recogidas + "/" + totales;
        }

        void MostrarJefe(JefeBobina j)
        {
            if (panelJefe != null) panelJefe.SetActive(true);
            if (nombreJefe != null) nombreJefe.text = j.NombreVisible;
            ActualizarJefe(j.Vida, j.VidaMaxima);
        }

        void ActualizarJefe(int actual, int maxima)
        {
            if (barraJefe != null) barraJefe.fillAmount = maxima > 0 ? (float)actual / maxima : 0f;
        }

        void OcultarJefe()
        {
            if (panelJefe != null) panelJefe.SetActive(false);
        }

        void Temblor()
        {
            if (dial == null) return;
            if (rutinaTemblor != null) StopCoroutine(rutinaTemblor);
            rutinaTemblor = StartCoroutine(TemblarDial());
        }

        IEnumerator TemblarDial()
        {
            float fin = Time.unscaledTime + 0.25f;
            while (Time.unscaledTime < fin)
            {
                dial.anchoredPosition = posDial + new Vector2(Random.Range(-8f, 8f), 0f);
                yield return null;
            }
            dial.anchoredPosition = posDial;
        }

        // ---------------- Mensajes ----------------

        public static void MostrarMensaje(string texto)
        {
            if (instancia == null || instancia.textoMensaje == null) return;
            if (instancia.rutinaMensaje != null) instancia.StopCoroutine(instancia.rutinaMensaje);
            instancia.rutinaMensaje = instancia.StartCoroutine(instancia.Mensaje(texto));
        }

        IEnumerator Mensaje(string texto)
        {
            textoMensaje.text = texto;
            textoMensaje.gameObject.SetActive(true);
            textoMensaje.alpha = 1f;
            yield return new WaitForSeconds(duracionMensaje);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                textoMensaje.alpha = 1f - t / 0.4f;
                yield return null;
            }
            textoMensaje.gameObject.SetActive(false);
        }
    }
}
