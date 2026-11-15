using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Estado de la partida dentro del nivel: chatarra, Cintas de Rosa, tiempo, caídas, pausa,
    /// jefe derrotado y fin del nivel. Va en el GameObject "Gestores" junto a GestorFrecuencia.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public class GestorJuego : MonoBehaviour
    {
        public static GestorJuego Instancia { get; private set; }

        [SerializeField] string nombreNivel = "1-1 Primer Contacto";
        [SerializeField] string escenaMenu = "MenuPrincipal";

        public int Chatarra { get; private set; }
        public int CintasRecogidas { get { return cintas.Count; } }
        public int CintasTotales { get; private set; }
        public int Caidas { get; private set; }
        public int Muertes { get; private set; }
        public float Tiempo { get; private set; }
        public bool JefeDerrotado { get; private set; }
        public bool NivelTerminado { get; private set; }
        public bool EnPausa { get; private set; }
        public string NombreNivel { get { return nombreNivel; } }

        public event Action<int> AlCambiarChatarra;
        public event Action<int, int> AlCambiarCintas;
        public event Action AlDerrotarJefe;
        public event Action AlTerminarNivel;
        public event Action<bool> AlPausar;

        readonly HashSet<string> cintas = new HashSet<string>();

        /// <summary>True cuando el jugador no debe moverse (pausa, diálogo, fin de nivel).</summary>
        public static bool ControlesBloqueados
        {
            get
            {
                if (Instancia == null) return false;
                return Instancia.EnPausa || Instancia.NivelTerminado || SistemaDialogo.Activo;
            }
        }

        void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            Instancia = this;
            Time.timeScale = 1f;
        }

        void Start()
        {
            Coleccionable[] todos = FindObjectsByType<Coleccionable>(FindObjectsSortMode.None);
            int total = 0;
            for (int i = 0; i < todos.Length; i++)
                if (todos[i].Tipo == TipoColeccionable.Cinta) total++;
            CintasTotales = total;
            Notificar();
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        void Update()
        {
            if (!NivelTerminado && !EnPausa) Tiempo += Time.deltaTime;
        }

        void Notificar()
        {
            if (AlCambiarChatarra != null) AlCambiarChatarra(Chatarra);
            if (AlCambiarCintas != null) AlCambiarCintas(CintasRecogidas, CintasTotales);
        }

        public void SumarChatarra(int cantidad)
        {
            Chatarra += cantidad;
            if (AlCambiarChatarra != null) AlCambiarChatarra(Chatarra);
        }

        public void RegistrarCinta(string id)
        {
            if (cintas.Add(id) && AlCambiarCintas != null) AlCambiarCintas(CintasRecogidas, CintasTotales);
        }

        public void RegistrarCaida() { Caidas++; }
        public void RegistrarMuerte() { Muertes++; }

        public void RegistrarJefeDerrotado()
        {
            if (JefeDerrotado) return;
            JefeDerrotado = true;
            if (AlDerrotarJefe != null) AlDerrotarJefe();
        }

        /// <summary>Medalla "Señal limpia" del sistema de recompensas: terminar sin caer ni morir.</summary>
        public bool SenalLimpia { get { return Caidas == 0 && Muertes == 0; } }

        public void TerminarNivel()
        {
            if (NivelTerminado) return;
            NivelTerminado = true;
            int premio = SenalLimpia ? 50 : 0;
            if (premio > 0) SumarChatarra(premio);

            string clave = "mejorTiempo_" + nombreNivel;
            float mejor = PlayerPrefs.GetFloat(clave, float.MaxValue);
            if (Tiempo < mejor) PlayerPrefs.SetFloat(clave, Tiempo);
            PlayerPrefs.SetInt("nivelCompletado_" + nombreNivel, 1);
            PlayerPrefs.Save();

            if (AlTerminarNivel != null) AlTerminarNivel();
        }

        public float MejorTiempo()
        {
            return PlayerPrefs.GetFloat("mejorTiempo_" + nombreNivel, Tiempo);
        }

        public void Pausar(bool pausar)
        {
            if (NivelTerminado || EnPausa == pausar) return;
            EnPausa = pausar;
            Time.timeScale = pausar ? 0f : 1f;
            AudioListener.pause = pausar;
            if (AlPausar != null) AlPausar(pausar);
        }

        public void Reiniciar()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void IrAlMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(escenaMenu);
        }

        public static string FormatearTiempo(float segundos)
        {
            int m = Mathf.FloorToInt(segundos / 60f);
            float s = segundos - m * 60f;
            return string.Format("{0:00}:{1:00.0}", m, s);
        }
    }
}
