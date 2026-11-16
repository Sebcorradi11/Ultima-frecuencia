using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace UltimaFrecuencia.EditorTools
{
    /// <summary>
    /// Prueba automática ("smoke test"): entra en Play Mode en Nivel1 durante unos segundos y comprueba
    /// lo básico sin intervención humana. No reemplaza jugar el nivel: detecta referencias rotas,
    /// excepciones al arrancar y fallas de la mecánica central.
    ///
    /// Batch (sin -quit, la prueba cierra Unity sola):
    ///   Unity.exe -batchmode -nographics -projectPath "ruta"
    ///     -executeMethod UltimaFrecuencia.EditorTools.PruebaHumoUltimaFrecuencia.Ejecutar -logFile humo_log.txt
    /// </summary>
    public static class PruebaHumoUltimaFrecuencia
    {
        const string Clave = "UF_PruebaHumo";
        const string ClaveInicio = "UF_PruebaHumoInicio";
        const string EscenaNivel = "Assets/Escenas/Nivel1.unity";

        static int fase;
        static int fallas;
        static int ticksEstables;
        static readonly List<string> erroresDeConsola = new List<string>();

        [MenuItem("Última Frecuencia/Prueba de humo (Play automático)")]
        public static void Ejecutar()
        {
            SessionState.SetBool(Clave, true);
            SessionState.SetFloat(ClaveInicio, (float)EditorApplication.timeSinceStartup);
            Enganchar(); // Tick entra en Play cuando Unity termina de compilar e importar
        }

        // Al entrar en Play Mode Unity recarga el dominio: hay que volver a engancharse.
        [InitializeOnLoadMethod]
        static void AlCargar()
        {
            if (SessionState.GetBool(Clave, false)) Enganchar();
        }

        static void Enganchar()
        {
            fase = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= AlRegistrar;
            Application.logMessageReceived += AlRegistrar;
        }

        static void AlRegistrar(string mensaje, string pila, LogType tipo)
        {
            if (mensaje.StartsWith("[Humo]")) return;
            if (mensaje.StartsWith("No graphic device")) return; // propio de -nographics, no del juego
            if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
                erroresDeConsola.Add(tipo + ": " + mensaje);
        }

        static void Comprobar(bool condicion, string descripcion)
        {
            if (condicion) Debug.Log("[Humo] OK    " + descripcion);
            else { fallas++; Debug.LogError("[Humo] FALLA " + descripcion); }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Clave, false)) return;

            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(ClaveInicio, 0f) > 180.0)
            {
                Debug.LogError("[Humo] FALLA tiempo agotado (fase " + fase + ")");
                Terminar(2);
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                // Entrar en Play con una compilación pendiente provoca una recarga de dominio en pleno
                // juego, que borra las variables estáticas (Instancia) y falsea la prueba.
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { ticksEstables = 0; return; }
                if (++ticksEstables < 30) return;
                ticksEstables = 0;
                EditorSceneManager.OpenScene(EscenaNivel, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
                return;
            }

            float t = Time.timeSinceLevelLoad;
            ControladorJugador jugador = Object.FindAnyObjectByType<ControladorJugador>();
            GestorFrecuencia gf = GestorFrecuencia.Instancia;

            if (fase == 0 && t > 1.5f)
            {
                fase = 1;
                Comprobar(SceneManager.GetActiveScene().name == "Nivel1", "la escena activa es Nivel1");
                Comprobar(gf != null && GestorJuego.Instancia != null, "existen GestorFrecuencia y GestorJuego");
                Comprobar(jugador != null, "existe el jugador");
                if (jugador == null || gf == null) { Terminar(1); return; }

                Comprobar(jugador.EnSuelo, "el jugador está apoyado en el suelo (y = " + jugador.transform.position.y.ToString("0.00") + ")");
                Comprobar(Mathf.Abs(jugador.transform.position.y - 4f) < 0.1f, "el jugador no atravesó el suelo");
                Comprobar(gf.Activa == Frecuencia.Ambar && gf.Desbloqueadas == 2, "frecuencia inicial Ámbar con 2 desbloqueadas");
                Comprobar(GestorJuego.Instancia.CintasTotales == 1, "hay 1 Cinta de Rosa en el nivel");

                foreach (Tilemap tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                {
                    CompositeCollider2D comp = tm.GetComponent<CompositeCollider2D>();
                    Comprobar(comp != null && comp.shapeCount > 0 && comp.geometryType == CompositeCollider2D.GeometryType.Polygons,
                        tm.name + ": layer " + LayerMask.LayerToName(tm.gameObject.layer) + ", composite con polígonos (" + (comp != null ? comp.shapeCount : 0) + " formas)");
                }

                Rigidbody2D rb = jugador.GetComponent<Rigidbody2D>();
                Comprobar((rb.excludeLayers.value & LayerMask.GetMask("Cian")) != 0 && (rb.excludeLayers.value & LayerMask.GetMask("Ambar")) == 0,
                    "en Ámbar el jugador ignora la capa Cian y choca con Ámbar");

                // Regla 3 del GDD: parado dentro de una silueta Cian (puente de la sección B) el cambio se bloquea.
                Vector3 original = jugador.transform.position;
                jugador.Teletransportar(new Vector3(18.5f, 3.0f, 0f));
                Physics2D.SyncTransforms();
                Comprobar(!gf.IntentarCambiar(+1, jugador.Cuerpo) && gf.Activa == Frecuencia.Ambar, "el cambio se bloquea dentro de un sólido Cian");
                jugador.Teletransportar(original);
                Physics2D.SyncTransforms();

                Comprobar(gf.IntentarCambiar(+1, jugador.Cuerpo) && gf.Activa == Frecuencia.Cian, "el cambio a Cian funciona en un lugar libre");
                Comprobar((rb.excludeLayers.value & LayerMask.GetMask("Ambar")) != 0, "en Cian el jugador ignora la capa Ámbar");
            }
            else if (fase == 1 && t > 2.5f)
            {
                fase = 2;
                Comprobar(SistemaDialogo.Instancia != null, "existe SistemaDialogo");
                if (SistemaDialogo.Instancia != null)
                {
                    SistemaDialogo.Instancia.Mostrar("Prueba", new[] { "Línea de prueba con tildes: señal, ámbar, ¡ñandú!" }, null);
                    Comprobar(SistemaDialogo.Activo && GestorJuego.ControlesBloqueados, "el diálogo se abre y bloquea los controles");
                }
            }
            else if (fase == 2 && t > 4f)
            {
                fase = 3;
                JefeBobina jefe = Object.FindAnyObjectByType<JefeBobina>();
                Comprobar(jefe != null, "existe La Bobina");
                if (jefe != null)
                {
                    jefe.Iniciar();
                    Comprobar(jefe.EstadoActual == JefeBobina.Estado.Inactivo || jefe.EstadoActual == JefeBobina.Estado.Rafaga, "el jefe inicia el combate");
                }
            }
            else if (fase == 3 && t > 8f)
            {
                fase = 4;
                JefeBobina jefe = Object.FindAnyObjectByType<JefeBobina>();
                Comprobar(jefe != null && jefe.EstadoActual == JefeBobina.Estado.Rafaga, "el jefe está en ráfaga");
                Comprobar(Object.FindObjectsByType<ProyectilFrecuencia>(FindObjectsSortMode.None).Length > 0, "el jefe disparó proyectiles");
                Comprobar(jugador != null && jugador.transform.position.y > 3.5f, "el jugador sigue sobre el suelo");

                // Botones del nivel: cada uno con su OnClick persistente.
                string[] botonesNivel = { "BotonContinuar", "BotonReiniciar", "BotonMenu", "BotonReintentar" };
                for (int i = 0; i < botonesNivel.Length; i++)
                {
                    Button b = BuscarBoton(botonesNivel[i]);
                    Comprobar(b != null && b.onClick.GetPersistentEventCount() == 1 && b.onClick.GetPersistentTarget(0) != null,
                        "Nivel1: " + botonesNivel[i] + " tiene su OnClick conectado");
                }

                // Pausa → "Salir al menú": tiene que cargar MenuPrincipal por Build Settings.
                GestorJuego.Instancia.Pausar(true);
                Comprobar(Time.timeScale == 0f, "la pausa detiene el tiempo");
                Button salir = BuscarBoton("BotonMenu");
                if (salir != null) salir.onClick.Invoke();
            }
            else if (fase == 4 && SceneManager.GetActiveScene().name == "MenuPrincipal" && Time.timeSinceLevelLoad > 1f)
            {
                fase = 5;
                Comprobar(true, "el botón de pausa 'Salir al menú' cargó MenuPrincipal");
                Comprobar(Time.timeScale == 1f, "el menú restablece la escala de tiempo");
                Comprobar(Activo("PanelPrincipal") && !Activo("PanelOpciones") && !Activo("PanelCreditos"), "el menú arranca en el panel principal");

                Pulsar("BotonOpciones");
                Comprobar(Activo("PanelOpciones") && !Activo("PanelPrincipal"), "Opciones abre su panel");
                Slider slider = Object.FindAnyObjectByType<Slider>();
                Comprobar(slider != null && slider.onValueChanged.GetPersistentEventCount() == 1, "el Slider de volumen está conectado");
                if (slider != null)
                {
                    float previo = slider.value;
                    slider.value = 0.4f;
                    Comprobar(Mathf.Abs(AudioListener.volume - 0.4f) < 0.001f, "el Slider cambia el volumen general");
                    slider.value = previo; // deja la preferencia guardada como estaba
                }
                Pulsar("BotonVolver");
                Comprobar(Activo("PanelPrincipal"), "Volver regresa al panel principal");
                Pulsar("BotonCreditos");
                Comprobar(Activo("PanelCreditos"), "Créditos abre su panel");
                Pulsar("BotonVolver");
                Button botonSalir = BuscarBoton("BotonSalir");
                Comprobar(botonSalir != null && botonSalir.onClick.GetPersistentEventCount() == 1, "Salir está conectado");
                Pulsar("BotonJugar");
            }
            else if (fase == 5 && SceneManager.GetActiveScene().name == "Nivel1" && Time.timeSinceLevelLoad > 1.5f)
            {
                fase = 6;
                Comprobar(true, "Jugar cargó Nivel1");
                Comprobar(gf != null && GestorJuego.Instancia != null && SistemaDialogo.Instancia != null, "los gestores existen después de recargar la escena");
                Comprobar(jugador != null && jugador.EnSuelo, "el jugador está en el suelo después de recargar");
                Comprobar(gf != null && gf.Activa == Frecuencia.Ambar, "la frecuencia vuelve a Ámbar al recargar");

                Comprobar(erroresDeConsola.Count == 0, "sin errores ni excepciones en la consola durante el Play (" + erroresDeConsola.Count + ")");
                for (int i = 0; i < erroresDeConsola.Count && i < 20; i++) Debug.LogError("[Humo]   consola -> " + erroresDeConsola[i]);

                Debug.Log(fallas == 0 ? "[Humo] === PRUEBA DE HUMO SUPERADA ===" : "[Humo] === PRUEBA DE HUMO CON " + fallas + " FALLA(S) ===");
                Terminar(fallas == 0 ? 0 : 1);
            }
        }

        /// <summary>Busca un botón por nombre entre los activos (y si no hay, entre los inactivos).</summary>
        static Button BuscarBoton(string nombre)
        {
            Button inactivo = null;
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b.name != nombre) continue;
                if (b.gameObject.activeInHierarchy) return b;
                inactivo = b;
            }
            return inactivo;
        }

        static void Pulsar(string nombre)
        {
            Button b = BuscarBoton(nombre);
            Comprobar(b != null && b.gameObject.activeInHierarchy, "el botón " + nombre + " está visible");
            if (b != null) b.onClick.Invoke();
        }

        static bool Activo(string nombre)
        {
            foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rt.name == nombre) return rt.gameObject.activeInHierarchy;
            return false;
        }

        static void Terminar(int codigo)
        {
            SessionState.SetBool(Clave, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= AlRegistrar;
            if (Application.isBatchMode) EditorApplication.Exit(codigo);
            else EditorApplication.ExitPlaymode();
        }
    }
}
