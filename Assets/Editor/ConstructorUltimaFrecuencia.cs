using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UltimaFrecuencia.EditorTools
{
    /// <summary>
    /// Arma el prototipo completo por código, siguiendo Docs/Guia_Montaje_Unity_Ultima_Frecuencia.docx:
    /// Layers, importación del arte, tiles, animaciones, prefabs, las dos escenas y Build Settings.
    /// Es idempotente: se puede ejecutar todas las veces que haga falta; cada vez regenera los
    /// assets derivados y vuelve a construir las escenas desde cero.
    ///
    /// Menú:  Última Frecuencia > Construir todo
    /// Batch: Unity.exe -batchmode -nographics -projectPath "ruta"
    ///        -executeMethod UltimaFrecuencia.EditorTools.ConstructorUltimaFrecuencia.ConstruirTodo -quit
    /// </summary>
    public static class ConstructorUltimaFrecuencia
    {
        // ---------------- Rutas ----------------
        const string RutaArte = "Assets/Arte";
        const string RutaCuadros = "Assets/Arte/Cuadros";
        const string RutaPaleta = "Assets/Arte/Paleta";
        const string RutaAnimaciones = "Assets/Animaciones";
        const string RutaPrefabs = "Assets/Prefabs";
        const string RutaMateriales = "Assets/Materiales";
        const string RutaEscenas = "Assets/Escenas";
        const string RutaEfectos = "Assets/Audio/Efectos";
        const string RutaMusica = "Assets/Audio/Musica";
        const string EscenaNivel = "Assets/Escenas/Nivel1.unity";
        const string EscenaMenu = "Assets/Escenas/MenuPrincipal.unity";
        const string ArchivoMapa = "Docs/mapa_nivel_1-1.txt";
        const string AjustesTMP = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string FuenteTMP = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string RutaExe = "Build/Ultima Frecuencia.exe";

        // ---------------- Layers (paso 2 de la guía) ----------------
        const int CapaUI = 5;
        const int CapaBase = 6;
        const int CapaAmbar = 7;
        const int CapaCian = 8;
        const int CapaMagenta = 9;
        const int CapaJugador = 10;
        const int CapaEnemigos = 11;

        // ---------------- Colores de la paleta ----------------
        static readonly Color ColorAmbar = new Color(0.941f, 0.627f, 0.188f);   // #F0A030
        static readonly Color ColorCian = new Color(0.220f, 0.784f, 0.839f);    // #38C8D6
        static readonly Color ColorMagenta = new Color(0.839f, 0.251f, 0.627f); // #D640A0
        static readonly Color ColorFondoCamara = new Color(0.086f, 0.102f, 0.149f); // #161A26

        // ---------------- Textos (sección 16 de la guía) ----------------
        static readonly string[] MensajeCinta =
        {
            "Cinta uno. Si alguien encuentra esto... soy Rosa Ferrán, operadora de Radio Villa Onda.",
            "Esta noche ESTÁTICA partió la señal en frecuencias. La ciudad se está... desarmando.",
            "Dejé un robot de mantenimiento en la subestación. Si estás despierto, DIAL-7: girá el dial."
        };
        static readonly string[] MensajeJefe =
        {
            "Cuidado: la bobina principal está corrompida. Dispara en una sola frecuencia por vez.",
            "Cuando se sobrecarga, se esconde en su zócalo y deja el núcleo al aire. ¡Ahí!"
        };

        static int errores;
        static Dictionary<string, string> indicePng;
        static TMP_FontAsset fuente;

        // =====================================================================
        //  ENTRADAS PÚBLICAS
        // =====================================================================

        [MenuItem("Última Frecuencia/Construir todo")]
        public static void ConstruirTodo()
        {
            errores = 0;
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

                Debug.Log("[Constructor] === Inicio ===");
                CrearCarpetas();
                CrearLayers();
                ConfigurarColisiones();
                GenerarCuadros();
                ConfigurarArte();
                ConfigurarAudio();
                bool tmpListo = AsegurarTextMeshPro();

                Recursos r = CrearRecursos();
                ConstruirNivel(r, tmpListo);
                ConstruirMenu(tmpListo);
                RegistrarEscenas();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (errores == 0) Debug.Log("[Constructor] === CONSTRUCCION TERMINADA SIN ERRORES ===");
                else Debug.LogError("[Constructor] === CONSTRUCCION TERMINADA CON " + errores + " ERROR(ES): revisar los mensajes anteriores ===");
            }
            catch (Exception e)
            {
                Debug.LogError("[Constructor] EXCEPCION: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Última Frecuencia/Compilar Windows (.exe)")]
        public static void CompilarWindows()
        {
            try
            {
                if (!File.Exists(EscenaMenu) || !File.Exists(EscenaNivel))
                {
                    Debug.LogError("[Constructor] Faltan las escenas. Ejecutar primero 'Construir todo'.");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                // Paso 14 de la guía.
                PlayerSettings.productName = "Ultima Frecuencia";
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

                Directory.CreateDirectory(Path.GetDirectoryName(RutaExe));
                BuildPlayerOptions opciones = new BuildPlayerOptions
                {
                    scenes = new[] { EscenaMenu, EscenaNivel },
                    locationPathName = RutaExe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                BuildReport informe = BuildPipeline.BuildPlayer(opciones);
                BuildSummary resumen = informe.summary;
                if (resumen.result == BuildResult.Succeeded)
                {
                    Debug.Log("[Constructor] === BUILD OK: " + resumen.outputPath + " (" + (resumen.totalSize / (1024 * 1024)) + " MB) ===");
                }
                else
                {
                    Debug.LogError("[Constructor] === BUILD FALLIDO: " + resumen.result + ", errores: " + resumen.totalErrors + " ===");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Constructor] EXCEPCION EN BUILD: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // =====================================================================
        //  a. PROYECTO: carpetas, layers y matriz de colisiones
        // =====================================================================

        static void CrearCarpetas()
        {
            string[] carpetas = { RutaCuadros, RutaPaleta, RutaAnimaciones, RutaPrefabs, RutaMateriales, RutaEscenas };
            for (int i = 0; i < carpetas.Length; i++) AsegurarCarpeta(carpetas[i]);
        }

        static void AsegurarCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta)) return;
            string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            AsegurarCarpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        /// <summary>Escribe los nombres de las Layers 6 a 11 en ProjectSettings/TagManager.asset.</summary>
        static void CrearLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) { Error("No se pudo abrir ProjectSettings/TagManager.asset"); return; }

            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty capas = so.FindProperty("layers");
            if (capas == null) { Error("TagManager no tiene la propiedad 'layers'"); return; }

            string[] nombres = { "Base", "Ambar", "Cian", "Magenta", "Jugador", "Enemigos" };
            for (int i = 0; i < nombres.Length; i++)
            {
                int indice = CapaBase + i;
                SerializedProperty p = capas.GetArrayElementAtIndex(indice);
                if (!string.IsNullOrEmpty(p.stringValue) && p.stringValue != nombres[i])
                    Debug.LogWarning("[Constructor] La Layer " + indice + " se llamaba '" + p.stringValue + "'; se renombra a '" + nombres[i] + "'.");
                p.stringValue = nombres[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Paso 2.5 de la guía: los enemigos se mueven por código y solo necesitan detectar al jugador.</summary>
        static void ConfigurarColisiones()
        {
            int[] ignoradas = { CapaBase, CapaAmbar, CapaCian, CapaMagenta, CapaEnemigos };
            for (int i = 0; i < ignoradas.Length; i++) Physics2D.IgnoreLayerCollision(CapaEnemigos, ignoradas[i], true);

            Object[] ajustes = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset");
            if (ajustes != null && ajustes.Length > 0) EditorUtility.SetDirty(ajustes[0]);
        }

        // =====================================================================
        //  b. ARTE Y AUDIO
        // =====================================================================

        /// <summary>
        /// Los scripts y la guía trabajan con cuadros sueltos (dial7_0..7, tile_0..7, etc.).
        /// Si Assets/Arte/Cuadros no los tiene, se recortan de las hojas de sprites.
        /// </summary>
        static void GenerarCuadros()
        {
            RecortarHoja("dial7_hoja_16x20", "dial7", 16, 20);
            RecortarHoja("pip_hoja_12x8", "pip", 12, 8);
            RecortarHoja("chispa_hoja_16x16", "chispa", 16, 16);
            RecortarHoja("bobina_hoja_48x64", "bobina", 48, 64);
            RecortarHoja("antena_relevo_hoja_16x32", "antena", 16, 32);
            RecortarHoja("tiles_zona1_16x16", "tile", 16, 16);
            AssetDatabase.Refresh();
        }

        static void RecortarHoja(string hoja, string prefijo, int ancho, int alto)
        {
            string[] encontrados = Directory.GetFiles(RutaArte, hoja + ".png", SearchOption.AllDirectories);
            if (encontrados.Length == 0)
            {
                if (!File.Exists(RutaCuadros + "/" + prefijo + "_0.png")) Error("Falta la hoja de sprites " + hoja + ".png");
                return;
            }

            Texture2D origen = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!origen.LoadImage(File.ReadAllBytes(encontrados[0]))) { Error("No se pudo leer " + encontrados[0]); return; }
                int cantidad = origen.width / ancho;
                for (int i = 0; i < cantidad; i++)
                {
                    string destino = RutaCuadros + "/" + prefijo + "_" + i + ".png";
                    if (File.Exists(destino)) continue;

                    Texture2D cuadro = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
                    cuadro.SetPixels(origen.GetPixels(i * ancho, 0, ancho, Mathf.Min(alto, origen.height)));
                    cuadro.Apply();
                    File.WriteAllBytes(destino, cuadro.EncodeToPNG());
                    Object.DestroyImmediate(cuadro);
                }
            }
            finally
            {
                Object.DestroyImmediate(origen);
            }
        }

        /// <summary>Sprite (Single), 16 PPU, Point, sin compresión ni mipmaps. Pivot según la tabla de la guía.</summary>
        static void ConfigurarArte()
        {
            string[] archivos = Directory.GetFiles(RutaArte, "*.png", SearchOption.AllDirectories);
            indicePng = new Dictionary<string, string>();

            for (int i = 0; i < archivos.Length; i++)
            {
                string ruta = archivos[i].Replace('\\', '/');
                string nombre = Path.GetFileNameWithoutExtension(ruta);
                indicePng[nombre] = ruta;

                TextureImporter imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
                if (imp == null) { Error("Sin importador de textura para " + ruta); continue; }

                bool pivoteAbajo = nombre.StartsWith("dial7_") || nombre.StartsWith("bobina_") || nombre.StartsWith("antena_")
                                   || nombre.StartsWith("compuerta");

                TextureImporterSettings s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                s.textureType = TextureImporterType.Sprite;
                s.spriteMode = (int)SpriteImportMode.Single;
                s.spritePixelsPerUnit = 16f;
                s.spriteAlignment = (int)(pivoteAbajo ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                s.spriteMeshType = SpriteMeshType.FullRect; // necesario para Draw Mode = Tiled (suelo eléctrico)
                s.spriteBorder = nombre.StartsWith("panel_9slice") ? new Vector4(4f, 4f, 4f, 4f) : Vector4.zero;
                s.filterMode = FilterMode.Point;
                s.mipmapEnabled = false;
                s.alphaIsTransparency = true;
                s.wrapMode = TextureWrapMode.Clamp;
                imp.SetTextureSettings(s);
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.SaveAndReimport();
            }
        }

        /// <summary>Paso 3 de la guía: la música se carga por streaming.</summary>
        static void ConfigurarAudio()
        {
            if (!Directory.Exists(RutaMusica)) { Error("Falta la carpeta " + RutaMusica); return; }
            string[] archivos = Directory.GetFiles(RutaMusica, "*.wav");
            for (int i = 0; i < archivos.Length; i++)
            {
                AudioImporter imp = AssetImporter.GetAtPath(archivos[i].Replace('\\', '/')) as AudioImporter;
                if (imp == null) continue;
                AudioImporterSampleSettings s = imp.defaultSampleSettings;
                if (s.loadType == AudioClipLoadType.Streaming) continue;
                s.loadType = AudioClipLoadType.Streaming;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        /// <summary>TextMeshPro necesita sus "Essential Resources" (ajustes y fuente por defecto) dentro de Assets.</summary>
        static bool AsegurarTextMeshPro()
        {
            if (!File.Exists(AjustesTMP))
            {
                Debug.Log("[Constructor] Importando TMP Essential Resources...");
                try { ExtraerEsencialesTMP(); }
                catch (Exception e) { Error("No se pudo extraer TMP Essential Resources: " + e.Message); }
                AssetDatabase.Refresh();
            }
            if (!File.Exists(AjustesTMP))
            {
                Error("Falta TMP Essential Resources. Importarlo a mano: Window > TextMeshPro > Import TMP Essential Resources, y volver a ejecutar.");
                return false;
            }
            fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FuenteTMP);
            if (fuente == null) Error("No se encontró la fuente por defecto de TextMeshPro en " + FuenteTMP);
            return true;
        }

        /// <summary>
        /// El importador de paquetes de Unity trabaja en diferido y en modo batch no llega a ejecutarse
        /// antes de -quit. Un .unitypackage es un .tar.gz con una carpeta por asset (asset, asset.meta
        /// y pathname), así que se descomprime directamente dentro de Assets.
        /// </summary>
        static void ExtraerEsencialesTMP()
        {
            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            if (info == null) { Error("No está instalado el paquete com.unity.ugui (TextMeshPro)"); return; }
            string paquete = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(paquete)) { Error("No se encontró " + paquete); return; }

            Dictionary<string, byte[]> datos = new Dictionary<string, byte[]>();
            Dictionary<string, byte[]> metas = new Dictionary<string, byte[]>();
            Dictionary<string, string> rutas = new Dictionary<string, string>();

            using (FileStream archivo = File.OpenRead(paquete))
            using (System.IO.Compression.GZipStream gzip = new System.IO.Compression.GZipStream(archivo, System.IO.Compression.CompressionMode.Decompress))
            {
                byte[] cabecera = new byte[512];
                while (LeerBloque(gzip, cabecera, 512) && cabecera[0] != 0)
                {
                    string nombre = Encoding.ASCII.GetString(cabecera, 0, 100).TrimEnd('\0').Replace('\\', '/');
                    string textoTamano = Encoding.ASCII.GetString(cabecera, 124, 12).Trim('\0', ' ');
                    long tamano = textoTamano.Length > 0 ? Convert.ToInt64(textoTamano, 8) : 0;
                    char tipo = (char)cabecera[156];

                    byte[] contenido = new byte[tamano];
                    if (!LeerBloque(gzip, contenido, (int)tamano)) break;
                    int relleno = (int)((512 - tamano % 512) % 512);
                    if (relleno > 0 && !LeerBloque(gzip, new byte[relleno], relleno)) break;

                    if (tipo != '0' && tipo != '\0') continue; // carpetas y cabeceras extendidas
                    if (nombre.StartsWith("./")) nombre = nombre.Substring(2);
                    string[] partes = nombre.Split('/');
                    if (partes.Length != 2) continue;

                    if (partes[1] == "asset") datos[partes[0]] = contenido;
                    else if (partes[1] == "asset.meta") metas[partes[0]] = contenido;
                    else if (partes[1] == "pathname") rutas[partes[0]] = Encoding.UTF8.GetString(contenido).Split('\n')[0].Trim();
                }
            }

            string raiz = Path.GetDirectoryName(Application.dataPath);
            int escritos = 0;
            foreach (KeyValuePair<string, string> par in rutas)
            {
                if (!par.Value.StartsWith("Assets/")) continue;
                string destino = Path.Combine(raiz, par.Value);
                byte[] contenido;
                if (datos.TryGetValue(par.Key, out contenido))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destino));
                    if (!File.Exists(destino)) { File.WriteAllBytes(destino, contenido); escritos++; }
                }
                else Directory.CreateDirectory(destino);

                byte[] meta;
                if (metas.TryGetValue(par.Key, out meta) && !File.Exists(destino + ".meta")) File.WriteAllBytes(destino + ".meta", meta);
            }
            Debug.Log("[Constructor] TMP Essential Resources: " + escritos + " archivos extraídos.");
        }

        static bool LeerBloque(Stream s, byte[] buffer, int cantidad)
        {
            int leidos = 0;
            while (leidos < cantidad)
            {
                int n = s.Read(buffer, leidos, cantidad - leidos);
                if (n <= 0) return false;
                leidos += n;
            }
            return true;
        }

        // =====================================================================
        //  c. RECURSOS: tiles, material, animaciones y prefabs
        // =====================================================================

        class Recursos
        {
            public Tile[] tiles = new Tile[7];
            public PhysicsMaterial2D sinFriccion;
            public RuntimeAnimatorController animJugador, animDron, animPip;
            public GameObject prefabChatarra, prefabProyectil;
        }

        static Recursos CrearRecursos()
        {
            Recursos r = new Recursos();

            // Tiles con colisión de celda completa.
            for (int i = 0; i < r.tiles.Length; i++)
            {
                Tile t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = Spr("tile_" + i);
                t.colliderType = Tile.ColliderType.Grid;
                r.tiles[i] = GuardarAsset(t, RutaPaleta + "/tile_" + i + ".asset");
            }

            // Material sin fricción: el personaje no se pega a las paredes.
            PhysicsMaterial2D mat = new PhysicsMaterial2D("SinFriccion");
            mat.friction = 0f;
            mat.bounciness = 0f;
            r.sinFriccion = GuardarAsset(mat, RutaMateriales + "/SinFriccion.physicsMaterial2D");

            // Animaciones del jugador (paso 6 de la guía).
            AnimationClip quieto = CrearClip("DIAL7_Quieto", 4f, true, Spr("dial7_0"), Spr("dial7_1"));
            AnimationClip corre = CrearClip("DIAL7_Corre", 12f, true, Spr("dial7_2"), Spr("dial7_3"), Spr("dial7_4"), Spr("dial7_5"));
            AnimationClip salta = CrearClip("DIAL7_Salta", 12f, true, Spr("dial7_6"));
            AnimationClip cae = CrearClip("DIAL7_Cae", 12f, true, Spr("dial7_7"));
            r.animJugador = CrearAnimatorJugador(quieto, corre, salta, cae);

            // Animaciones en bucle del dron y de Pip.
            r.animDron = CrearAnimatorSimple("Chispa", CrearClip("Chispa_Vuelo", 8f, true, Spr("chispa_0"), Spr("chispa_1")));
            r.animPip = CrearAnimatorSimple("Pip", CrearClip("Pip_Aleteo", 8f, true, Spr("pip_0"), Spr("pip_1")));

            r.prefabChatarra = CrearPrefabChatarra();
            r.prefabProyectil = CrearPrefabProyectil();
            return r;
        }

        /// <summary>Clip que cambia el sprite del SpriteRenderer cuadro a cuadro.</summary>
        static AnimationClip CrearClip(string nombre, float fps, bool bucle, params Sprite[] cuadros)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = nombre;
            clip.frameRate = fps;

            EditorCurveBinding enlace = new EditorCurveBinding();
            enlace.type = typeof(SpriteRenderer);
            enlace.path = "";
            enlace.propertyName = "m_Sprite";

            // Una clave por cuadro más una final repetida, para que el último cuadro también dure 1/fps.
            ObjectReferenceKeyframe[] claves = new ObjectReferenceKeyframe[cuadros.Length + 1];
            for (int i = 0; i < claves.Length; i++)
            {
                claves[i] = new ObjectReferenceKeyframe();
                claves[i].time = i / fps;
                claves[i].value = cuadros[Mathf.Min(i, cuadros.Length - 1)];
            }
            AnimationUtility.SetObjectReferenceCurve(clip, enlace, claves);

            AnimationClipSettings ajustes = AnimationUtility.GetAnimationClipSettings(clip);
            ajustes.loopTime = bucle;
            AnimationUtility.SetAnimationClipSettings(clip, ajustes);

            return GuardarAsset(clip, RutaAnimaciones + "/" + nombre + ".anim");
        }

        static AnimatorController NuevoControlador(string nombre)
        {
            string ruta = RutaAnimaciones + "/" + nombre + ".controller";
            if (AssetDatabase.LoadMainAssetAtPath(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            return AnimatorController.CreateAnimatorControllerAtPath(ruta);
        }

        static AnimatorController CrearAnimatorSimple(string nombre, AnimationClip clip)
        {
            AnimatorController c = NuevoControlador(nombre);
            AnimatorState estado = c.layers[0].stateMachine.AddState("Bucle");
            estado.motion = clip;
            c.layers[0].stateMachine.defaultState = estado;
            EditorUtility.SetDirty(c);
            return c;
        }

        /// <summary>
        /// Máquina de estados del jugador. ControladorJugador escribe VelX, VelY y EnSuelo en cada cuadro
        /// y las transiciones (sin exit time, duración 0) eligen el clip.
        /// </summary>
        static AnimatorController CrearAnimatorJugador(AnimationClip quieto, AnimationClip corre, AnimationClip salta, AnimationClip cae)
        {
            AnimatorController c = NuevoControlador("DIAL7");
            c.AddParameter("VelX", AnimatorControllerParameterType.Float);
            c.AddParameter("VelY", AnimatorControllerParameterType.Float);
            c.AddParameter("EnSuelo", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = c.layers[0].stateMachine;
            AnimatorState eQuieto = sm.AddState("Quieto"); eQuieto.motion = quieto;
            AnimatorState eCorre = sm.AddState("Corre"); eCorre.motion = corre;
            AnimatorState eSalta = sm.AddState("Salta"); eSalta.motion = salta;
            AnimatorState eCae = sm.AddState("Cae"); eCae.motion = cae;
            sm.defaultState = eQuieto;

            Inmediata(eQuieto.AddTransition(eCorre)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "VelX");
            Inmediata(eCorre.AddTransition(eQuieto)).AddCondition(AnimatorConditionMode.Less, 0.1f, "VelX");

            AnimatorStateTransition aSalta = Inmediata(sm.AddAnyStateTransition(eSalta));
            aSalta.canTransitionToSelf = false;
            aSalta.AddCondition(AnimatorConditionMode.IfNot, 0f, "EnSuelo");
            aSalta.AddCondition(AnimatorConditionMode.Greater, 0.1f, "VelY");

            AnimatorStateTransition aCae = Inmediata(sm.AddAnyStateTransition(eCae));
            aCae.canTransitionToSelf = false;
            aCae.AddCondition(AnimatorConditionMode.IfNot, 0f, "EnSuelo");
            aCae.AddCondition(AnimatorConditionMode.Less, -0.1f, "VelY");

            Inmediata(eSalta.AddTransition(eCae)).AddCondition(AnimatorConditionMode.Less, -0.1f, "VelY");
            Inmediata(eCae.AddTransition(eQuieto)).AddCondition(AnimatorConditionMode.If, 0f, "EnSuelo");
            // Extra respecto de la guía: aterrizar justo en el punto más alto del salto (sin pasar por Cae).
            Inmediata(eSalta.AddTransition(eQuieto)).AddCondition(AnimatorConditionMode.If, 0f, "EnSuelo");

            EditorUtility.SetDirty(c);
            return c;
        }

        static AnimatorStateTransition Inmediata(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.hasFixedDuration = true;
            t.duration = 0f;
            t.offset = 0f;
            return t;
        }

        static GameObject CrearPrefabChatarra()
        {
            GameObject go = new GameObject("Chatarra");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("chatarra_8x8");
            sr.sortingOrder = 5;
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;
            Coleccionable c = go.AddComponent<Coleccionable>();
            Asignar(c, "tipo", (int)TipoColeccionable.Chatarra);
            Asignar(c, "valor", 10);
            return GuardarPrefab(go, RutaPrefabs + "/Chatarra.prefab");
        }

        static GameObject CrearPrefabProyectil()
        {
            GameObject go = new GameObject("Proyectil");
            go.layer = CapaEnemigos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("proyectil_8x8");
            sr.sortingOrder = 7;
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.25f;
            go.AddComponent<ProyectilFrecuencia>();
            return GuardarPrefab(go, RutaPrefabs + "/Proyectil.prefab");
        }

        static GameObject GuardarPrefab(GameObject temporal, string ruta)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, ruta);
            Object.DestroyImmediate(temporal);
            if (prefab == null) Error("No se pudo guardar el prefab " + ruta);
            return prefab;
        }

        // =====================================================================
        //  d. ESCENA NIVEL1
        // =====================================================================

        static void ConstruirNivel(Recursos r, bool tmpListo)
        {
            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            int ancho, alto;
            char[,] mapa = LeerMapa(out ancho, out alto);

            // ----- Gestores (paso 5) -----
            GameObject gestores = new GameObject("Gestores");
            GestorFrecuencia gf = gestores.AddComponent<GestorFrecuencia>();
            Asignar(gf, "frecuenciaInicial", (int)Frecuencia.Ambar);
            Asignar(gf, "frecuenciasDesbloqueadas", 2);
            GestorJuego gj = gestores.AddComponent<GestorJuego>();
            Asignar(gj, "nombreNivel", "1-1 Primer Contacto");
            Asignar(gj, "escenaMenu", "MenuPrincipal");
            ConfigurarGestorAudio(gestores.AddComponent<GestorAudio>());

            // ----- Tilemaps (paso 4) -----
            GameObject grid = new GameObject("Grid");
            grid.AddComponent<Grid>();
            Tilemap tmBase = CrearTilemap("Tilemap_Base", grid.transform, CapaBase, 0);
            Tilemap tmAmbar = CrearTilemap("Tilemap_Ambar", grid.transform, CapaAmbar, 1);
            Tilemap tmCian = CrearTilemap("Tilemap_Cian", grid.transform, CapaCian, 1);
            Asignar(tmAmbar.gameObject.AddComponent<CapaFrecuencia>(), "frecuencia", (int)Frecuencia.Ambar);
            Asignar(tmCian.gameObject.AddComponent<CapaFrecuencia>(), "frecuencia", (int)Frecuencia.Cian);

            // Los tiles se cargan desde disco en este momento: las referencias en memoria a assets creados
            // antes de abrir la escena nueva pueden quedar inválidas y SetTile no pintaría nada.
            Tile superficie = CargarTile(0), relleno = CargarTile(1), tileAmbar = CargarTile(4), tileCian = CargarTile(5);
            if (mapa != null)
            {
                for (int y = 0; y < alto; y++)
                {
                    for (int x = 0; x < ancho; x++)
                    {
                        Vector3Int celda = new Vector3Int(x, y, 0);
                        char c = mapa[x, y];
                        if (c == '#')
                        {
                            bool arribaVacio = y + 1 >= alto || mapa[x, y + 1] != '#';
                            tmBase.SetTile(celda, arribaVacio ? superficie : relleno);
                        }
                        else if (c == 'a') tmAmbar.SetTile(celda, tileAmbar);
                        else if (c == 'c') tmCian.SetTile(celda, tileCian);
                    }
                }
            }
            RegenerarColision(tmBase);
            RegenerarColision(tmAmbar);
            RegenerarColision(tmCian);

            // ----- ZonaMuerte (paso 4.7) -----
            GameObject zona = new GameObject("ZonaMuerte");
            zona.transform.position = new Vector3(56f, -2f, 0f);
            BoxCollider2D colZona = zona.AddComponent<BoxCollider2D>();
            colZona.isTrigger = true;
            colZona.size = new Vector2(130f, 2f);
            zona.AddComponent<ZonaMuerte>();

            // ----- Objetos del plano -----
            Transform raizObjetos = new GameObject("Objetos").transform;
            Transform raizChatarra = new GameObject("Chatarra").transform;
            raizChatarra.SetParent(raizObjetos);
            Transform raizEnemigos = new GameObject("Enemigos").transform;

            GameObject jugador = null, cinta = null, pip = null, compuertaEntrada = null, compuertaFinal = null;
            JefeBobina jefe = null;
            Vector3 posActivador = Vector3.zero;
            bool hayActivador = false;
            int antenas = 0;

            if (mapa != null)
            {
                for (int x = 0; x < ancho; x++)
                {
                    for (int y = 0; y < alto; y++)
                    {
                        Vector3 centro = new Vector3(x + 0.5f, y + 0.5f, 0f);
                        Vector3 pie = new Vector3(x + 0.5f, y, 0f);
                        switch (mapa[x, y])
                        {
                            case 'P': if (jugador == null) jugador = CrearJugador(pie, r); break;
                            case 'N': if (pip == null) pip = CrearPip(centro, r, tmpListo); break;
                            case 'R': CrearAntenaRelevo(pie, raizObjetos, ++antenas); break;
                            case 'o':
                                if (r.prefabChatarra != null)
                                {
                                    GameObject ch = (GameObject)PrefabUtility.InstantiatePrefab(r.prefabChatarra, raizChatarra);
                                    ch.transform.position = centro;
                                }
                                break;
                            case 'T': if (cinta == null) cinta = CrearCinta(centro, raizObjetos); break;
                            case 'B': CrearBateria(centro, raizObjetos); break;
                            case 'D': CrearDron("DronChispa_Ambar", centro, Frecuencia.Ambar, r, raizEnemigos); break;
                            case 'd': CrearDron("DronChispa_Cian", centro, Frecuencia.Cian, r, raizEnemigos); break;
                            case 'K': posActivador = new Vector3(x + 0.5f, y + 2f, 0f); hayActivador = true; break;
                            case 'J': if (jefe == null) jefe = CrearJefe(pie, r); break;
                            case 'S': CrearAntenaSalida(pie, raizObjetos); break;
                            // Las compuertas ocupan 3 celdas: se crea un objeto en la celda más baja.
                            case 'G': if (compuertaEntrada == null) compuertaEntrada = CrearCompuerta("CompuertaEntrada", pie, raizObjetos); break;
                            case 'X': if (compuertaFinal == null) compuertaFinal = CrearCompuerta("CompuertaFinal", pie, raizObjetos); break;
                        }
                    }
                }
            }

            if (jugador == null) Error("El mapa no tiene jugador (P)");
            if (jefe == null) Error("El mapa no tiene jefe (J)");
            if (pip == null) Error("El mapa no tiene a Pip (N)");
            if (cinta == null) Error("El mapa no tiene Cinta de Rosa (T)");
            if (compuertaEntrada == null || compuertaFinal == null) Error("El mapa no tiene las dos compuertas (G y X)");
            if (!hayActivador) Error("El mapa no tiene activador del jefe (K)");

            // Pip señala la cinta después de la primera charla (paso 9.3).
            if (pip != null && cinta != null) Asignar(pip.GetComponent<PipNPC>(), "puntoSecreto", cinta.transform);

            // ----- Jefe: referencias a la arena (paso 10) -----
            if (jefe != null)
            {
                Vector3 posSuelo = new Vector3(84.5f, 4.25f, 0f);
                GameObject suelo = CrearFranjaSuelo("SueloElectrico", posSuelo, Spr("suelo_electrico_16x8"));
                BoxCollider2D colSuelo = suelo.AddComponent<BoxCollider2D>();
                colSuelo.isTrigger = true;
                colSuelo.size = new Vector2(20f, 0.6f);
                suelo.AddComponent<Peligro>();
                GameObject aviso = CrearFranjaSuelo("AvisoSuelo", posSuelo, Spr("aviso_suelo_16x8"));

                Asignar(jefe, "sueloElectrico", suelo);
                Asignar(jefe, "avisoSuelo", aviso.GetComponent<SpriteRenderer>());
                Asignar(jefe, "desactivarAlDerrotar", compuertaFinal != null ? new Object[] { compuertaFinal } : new Object[0]);
                Asignar(jefe, "activarAlDerrotar", new Object[0]);

                if (hayActivador)
                {
                    GameObject activador = new GameObject("ActivadorJefe");
                    activador.transform.position = posActivador;
                    BoxCollider2D colAct = activador.AddComponent<BoxCollider2D>();
                    colAct.isTrigger = true;
                    colAct.size = new Vector2(1f, 6f);
                    ActivadorJefe aj = activador.AddComponent<ActivadorJefe>();
                    Asignar(aj, "jefe", jefe);
                    Asignar(aj, "compuertaEntrada", compuertaEntrada);
                    Asignar(aj, "mensajeRosa", MensajeJefe);
                }
            }

            // ----- Cámara, luz y parallax (paso 7) -----
            Camera camara = CrearCamara(new Vector3(10f, 5.625f, -10f), ColorFondoCamara, true);
            CamaraSeguimiento seguimiento = camara.gameObject.AddComponent<CamaraSeguimiento>();
            if (jugador != null) Asignar(seguimiento, "objetivo", jugador.transform);
            Asignar(seguimiento, "usarLimites", true);
            Asignar(seguimiento, "limiteMin", new Vector2(0f, 0f));
            Asignar(seguimiento, "limiteMax", new Vector2(112f, 22f));
            CrearLuzGlobal();
            CrearParallax(camara.transform);

            // ----- Interfaz (paso 11) -----
            if (tmpListo) CrearInterfazNivel();
            else Error("No se construyó el Canvas del nivel porque falta TextMeshPro.");
            CrearEventSystem(null);

            GuardarEscena(escena, EscenaNivel);
        }

        // ---------------- Mapa ASCII ----------------

        /// <summary>Lee el plano. Devuelve mapa[x, y] con y = 0 en la última fila del archivo.</summary>
        static char[,] LeerMapa(out int ancho, out int alto)
        {
            ancho = 0; alto = 0;
            string ruta = Path.Combine(Path.GetDirectoryName(Application.dataPath), ArchivoMapa);
            if (!File.Exists(ruta)) { Error("Falta el plano del nivel: " + ruta); return null; }

            string[] lineas = File.ReadAllLines(ruta, Encoding.UTF8);

            // El encabezado (título y leyenda) termina en la primera línea en blanco.
            int inicio = 0;
            while (inicio < lineas.Length && !string.IsNullOrWhiteSpace(lineas[inicio])) inicio++;
            inicio++;
            int fin = lineas.Length - 1;
            while (fin >= inicio && string.IsNullOrWhiteSpace(lineas[fin])) fin--;
            if (fin < inicio) { Error("El plano del nivel está vacío"); return null; }

            alto = fin - inicio + 1;
            for (int i = inicio; i <= fin; i++) ancho = Mathf.Max(ancho, lineas[i].TrimEnd().Length);

            char[,] mapa = new char[ancho, alto];
            for (int y = 0; y < alto; y++)
            {
                string fila = lineas[fin - y];
                for (int x = 0; x < ancho; x++) mapa[x, y] = x < fila.Length ? fila[x] : ' ';
            }
            Debug.Log("[Constructor] Plano leído: " + ancho + " x " + alto + " celdas.");
            return mapa;
        }

        // ---------------- Tilemaps ----------------

        static Tilemap CrearTilemap(string nombre, Transform grid, int capa, int orden)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(grid, false);
            go.layer = capa;
            Tilemap tm = go.AddComponent<Tilemap>();
            TilemapRenderer render = go.AddComponent<TilemapRenderer>();
            render.sortingOrder = orden;

            TilemapCollider2D col = go.AddComponent<TilemapCollider2D>();
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D comp = go.AddComponent<CompositeCollider2D>();
            // Polygons y no Outlines: OverlapBox tiene que detectar el interior del sólido para el
            // chequeo de bloqueo de GestorFrecuencia (con Outlines solo existen los bordes).
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
            comp.generationType = CompositeCollider2D.GenerationType.Synchronous;
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
            return tm;
        }

        static Tile CargarTile(int indice)
        {
            string ruta = RutaPaleta + "/tile_" + indice + ".asset";
            Tile t = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
            if (t == null) Error("No se pudo cargar el tile " + ruta);
            return t;
        }

        static void RegenerarColision(Tilemap tm)
        {
            tm.CompressBounds();
            TilemapCollider2D col = tm.GetComponent<TilemapCollider2D>();
            if (col != null) col.ProcessTilemapChanges();
            CompositeCollider2D comp = tm.GetComponent<CompositeCollider2D>();
            if (comp != null) comp.GenerateGeometry();

            // Autocomprobación: un Tilemap vacío o sin colisión deja el nivel injugable.
            int celdas = 0;
            foreach (Vector3Int p in tm.cellBounds.allPositionsWithin) if (tm.HasTile(p)) celdas++;
            int formas = comp != null ? comp.shapeCount : 0;
            if (celdas == 0 || formas == 0) Error(tm.name + " quedó con " + celdas + " tiles y " + formas + " formas de colisión");
            else Debug.Log("[Constructor] " + tm.name + ": " + celdas + " tiles, " + formas + " formas de colisión.");
        }

        // ---------------- Gestor de audio ----------------

        static void ConfigurarGestorAudio(GestorAudio ga)
        {
            Asignar(ga, "musicaAmbar", Clip(RutaMusica + "/musica_z1_ambar.wav"));
            Asignar(ga, "musicaCian", Clip(RutaMusica + "/musica_z1_cian.wav"));
            Asignar(ga, "musicaMagenta", Clip(RutaMusica + "/musica_z1_magenta.wav"));
            Asignar(ga, "musicaJefe", Clip(RutaMusica + "/musica_jefe_bobina.wav"));

            // Un elemento por valor del enum Sonido; el WAV se llama igual en minúsculas con guiones bajos
            // (CambioCian -> cambio_cian.wav).
            SerializedObject so = new SerializedObject(ga);
            SerializedProperty efectos = so.FindProperty("efectos");
            if (efectos == null) { Error("GestorAudio no tiene el campo 'efectos'"); return; }

            Array valores = Enum.GetValues(typeof(Sonido));
            efectos.arraySize = valores.Length;
            for (int i = 0; i < valores.Length; i++)
            {
                Sonido s = (Sonido)valores.GetValue(i);
                SerializedProperty e = efectos.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("sonido").intValue = (int)s;
                e.FindPropertyRelative("clip").objectReferenceValue = Clip(RutaEfectos + "/" + NombreArchivo(s.ToString()) + ".wav");
                e.FindPropertyRelative("volumen").floatValue = s == Sonido.Dialogo ? 0.3f : 0.8f;
                e.FindPropertyRelative("variacionTono").floatValue = 0.05f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>"CambioCian" -> "cambio_cian", "UI" -> "ui".</summary>
        static string NombreArchivo(string nombreEnum)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < nombreEnum.Length; i++)
            {
                char c = nombreEnum[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(nombreEnum[i - 1])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // ---------------- Jugador ----------------

        static GameObject CrearJugador(Vector3 pos, Recursos r)
        {
            GameObject go = new GameObject("DIAL-7");
            go.transform.position = pos;
            go.layer = CapaJugador;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            // Sin esto el cuerpo se duerme al quedarse quieto y los triggers que se activan debajo
            // (suelo electrificado del jefe) no lo detectan.
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            CapsuleCollider2D col = go.AddComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(0.7f, 1.15f);
            col.offset = new Vector2(0f, 0.58f);
            col.sharedMaterial = r.sinFriccion;

            ControladorJugador control = go.AddComponent<ControladorJugador>();
            go.AddComponent<VidaJugador>();

            GameObject visual = Hijo("Visual", go.transform, Vector3.zero, CapaJugador);
            GameObject cuerpo = Hijo("Cuerpo", visual.transform, Vector3.zero, CapaJugador);
            SpriteRenderer srCuerpo = cuerpo.AddComponent<SpriteRenderer>();
            srCuerpo.sprite = Spr("dial7_0");
            srCuerpo.sortingOrder = 10;
            Animator anim = cuerpo.AddComponent<Animator>();
            anim.runtimeAnimatorController = r.animJugador;

            GameObject luces = Hijo("Luces", visual.transform, Vector3.zero, CapaJugador);
            SpriteRenderer srLuces = luces.AddComponent<SpriteRenderer>();
            srLuces.sprite = Spr("dial7_luces");
            srLuces.sortingOrder = 11;
            srLuces.color = ColorAmbar;

            Asignar(control, "visual", visual.transform);
            Asignar(control, "partesConColorDeFrecuencia", new Object[] { srLuces });
            return go;
        }

        // ---------------- NPC y enemigos ----------------

        static GameObject CrearPip(Vector3 pos, Recursos r, bool tmpListo)
        {
            GameObject go = new GameObject("Pip");
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("pip_0");
            sr.sortingOrder = 9;
            go.AddComponent<Animator>().runtimeAnimatorController = r.animPip;
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 2f;
            PipNPC pip = go.AddComponent<PipNPC>();
            Asignar(pip, "sprite", sr);

            if (tmpListo)
            {
                GameObject aviso = Hijo("Aviso", go.transform, new Vector3(0f, 0.9f, 0f), 0);
                TextMeshPro t = aviso.AddComponent<TextMeshPro>();
                if (fuente != null) t.font = fuente;
                t.text = "W";
                t.fontSize = 3f;
                t.alignment = TextAlignmentOptions.Center;
                t.color = Color.white;
                t.rectTransform.sizeDelta = new Vector2(2f, 1f);
                MeshRenderer mr = aviso.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 20;
                Asignar(pip, "avisoInteraccion", aviso);
            }
            return go;
        }

        static void CrearDron(string nombre, Vector3 pos, Frecuencia f, Recursos r, Transform padre)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre);
            go.transform.position = pos;
            go.layer = CapaEnemigos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("chispa_0");
            sr.sortingOrder = 8;
            sr.color = f == Frecuencia.Ambar ? ColorAmbar : ColorCian;
            go.AddComponent<Animator>().runtimeAnimatorController = r.animDron;
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;

            GameObject a = Hijo("PuntoA", go.transform, new Vector3(-4f, 0f, 0f), CapaEnemigos);
            GameObject b = Hijo("PuntoB", go.transform, new Vector3(4f, 0f, 0f), CapaEnemigos);

            DronChispa dron = go.AddComponent<DronChispa>();
            Asignar(dron, "frecuencia", (int)f);
            Asignar(dron, "puntosPatrulla", new Object[] { a.transform, b.transform });
            Asignar(dron, "prefabChatarra", r.prefabChatarra);
        }

        // ---------------- Objetos del nivel (paso 8) ----------------

        static void CrearAntenaRelevo(Vector3 pos, Transform padre, int numero)
        {
            GameObject go = new GameObject("AntenaRelevo_" + numero);
            go.transform.SetParent(padre);
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("antena_0");
            sr.sortingOrder = 2;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 2f);
            col.offset = new Vector2(0f, 1f);
            AntenaRelevo antena = go.AddComponent<AntenaRelevo>();
            Asignar(antena, "sprite", sr);
            Asignar(antena, "spriteApagada", Spr("antena_0"));
            Asignar(antena, "spriteEncendida", Spr("antena_1"));
        }

        static void CrearAntenaSalida(Vector3 pos, Transform padre)
        {
            GameObject go = new GameObject("AntenaSalida");
            go.transform.SetParent(padre);
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("antena_salida_32x48");
            sr.sortingOrder = 2;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 3f);
            col.offset = new Vector2(0f, 1.5f);
            Asignar(go.AddComponent<AntenaSalida>(), "requiereJefeDerrotado", true);
        }

        static GameObject CrearCinta(Vector3 pos, Transform padre)
        {
            GameObject go = new GameObject("CintaRosa");
            go.transform.SetParent(padre);
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("cinta_rosa_12x9");
            sr.sortingOrder = 5;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.75f, 0.6f);
            Coleccionable c = go.AddComponent<Coleccionable>();
            Asignar(c, "tipo", (int)TipoColeccionable.Cinta);
            Asignar(c, "idCinta", "1-1_A");
            Asignar(c, "mensajeCinta", MensajeCinta);
            return go;
        }

        static void CrearBateria(Vector3 pos, Transform padre)
        {
            GameObject go = new GameObject("Bateria");
            go.transform.SetParent(padre);
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("bateria_10x12");
            sr.sortingOrder = 5;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.65f, 0.75f);
            Coleccionable c = go.AddComponent<Coleccionable>();
            Asignar(c, "tipo", (int)TipoColeccionable.Bateria);
            Asignar(c, "valor", 1);
        }

        static GameObject CrearCompuerta(string nombre, Vector3 pos, Transform padre)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre);
            go.transform.position = pos;
            go.layer = CapaBase;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr("compuerta_16x48");
            sr.sortingOrder = 1;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 3f);
            col.offset = new Vector2(0f, 1.5f);
            return go;
        }

        // ---------------- Jefe (paso 10) ----------------

        static JefeBobina CrearJefe(Vector3 pos, Recursos r)
        {
            GameObject go = new GameObject("LaBobina");
            go.transform.position = pos;
            go.layer = CapaEnemigos;
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            JefeBobina jefe = go.AddComponent<JefeBobina>();

            // Order -5: queda detrás del Tilemap_Base, así el suelo lo tapa cuando baja a su zócalo.
            GameObject cuerpo = Hijo("Cuerpo", go.transform, Vector3.zero, CapaEnemigos);
            SpriteRenderer srCuerpo = cuerpo.AddComponent<SpriteRenderer>();
            srCuerpo.sprite = Spr("bobina_0");
            srCuerpo.sortingOrder = -5;

            GameObject zona = Hijo("ZonaDano", go.transform, Vector3.zero, CapaEnemigos);
            BoxCollider2D colZona = zona.AddComponent<BoxCollider2D>();
            colZona.isTrigger = true;
            colZona.size = new Vector2(2.2f, 2.4f);
            colZona.offset = new Vector2(0f, 1.2f);
            zona.AddComponent<Peligro>();

            GameObject nucleo = Hijo("Nucleo", go.transform, new Vector3(0f, 3.6f, 0f), CapaEnemigos);
            SpriteRenderer srNucleo = nucleo.AddComponent<SpriteRenderer>();
            srNucleo.sprite = Spr("nucleo_16x16");
            srNucleo.sortingOrder = 6;
            BoxCollider2D colNucleo = nucleo.AddComponent<BoxCollider2D>();
            colNucleo.isTrigger = true;
            colNucleo.size = new Vector2(1.4f, 0.8f);
            PuntoDebil punto = nucleo.AddComponent<PuntoDebil>();

            GameObject disparo = Hijo("PuntoDisparo", go.transform, new Vector3(0f, 3.6f, 0f), CapaEnemigos);

            Asignar(jefe, "cuerpo", srCuerpo);
            Asignar(jefe, "nucleo", punto);
            Asignar(jefe, "puntoDisparo", disparo.transform);
            Asignar(jefe, "prefabProyectil", r.prefabProyectil != null ? r.prefabProyectil.GetComponent<ProyectilFrecuencia>() : null);
            return jefe;
        }

        /// <summary>Franja de 20 × 0,5 con el sprite repetido (Draw Mode = Tiled).</summary>
        static GameObject CrearFranjaSuelo(string nombre, Vector3 pos, Sprite sprite)
        {
            GameObject go = new GameObject(nombre);
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(20f, 0.5f);
            sr.sortingOrder = 3;
            return go;
        }

        // ---------------- Cámara, luz y parallax ----------------

        static Camera CrearCamara(Vector3 pos, Color fondo, bool pixelPerfect)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = pos;
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.625f; // 180 px / 2 / 16 PPU
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fondo;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            go.AddComponent<AudioListener>();

            // Los componentes de URP se agregan por reflexión para no depender de sus ensamblados.
            Type datosUrp = BuscarTipo("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (datosUrp != null && go.GetComponent(datosUrp) == null) go.AddComponent(datosUrp);

            if (pixelPerfect)
            {
                Type tipo = BuscarTipo("UnityEngine.Rendering.Universal.PixelPerfectCamera");
                if (tipo == null) Error("No se encontró PixelPerfectCamera de URP");
                else
                {
                    Component ppc = go.AddComponent(tipo);
                    Asignar(ppc, "m_AssetsPPU", 16);
                    Asignar(ppc, "m_RefResolutionX", 320);
                    Asignar(ppc, "m_RefResolutionY", 180);
                    Asignar(ppc, "m_GridSnapping", 2); // GridSnapping.UpscaleRenderTexture
                }
            }
            return cam;
        }

        static void CrearLuzGlobal()
        {
            Type tipo = BuscarTipo("UnityEngine.Rendering.Universal.Light2D");
            if (tipo == null) { Debug.LogWarning("[Constructor] No hay Light2D (URP 2D): se omite la luz global."); return; }
            GameObject go = new GameObject("Global Light 2D");
            Component luz = go.AddComponent(tipo);
            Asignar(luz, "m_LightType", 4); // Light2D.LightType.Global
            Asignar(luz, "m_Intensity", 1f);
            Asignar(luz, "m_Color", Color.white);
        }

        static void CrearParallax(Transform camara)
        {
            Transform raiz = new GameObject("Parallax").transform;
            string[] fondos = { "z1_0_cielo", "z1_1_torres_lejanas", "z1_2_transformadores", "z1_3_primer_plano" };
            float[] fx = { 0.95f, 0.8f, 0.55f, 0.3f };
            float[] fy = { 0.9f, 0.6f, 0.4f, 0.2f };
            for (int i = 0; i < fondos.Length; i++)
            {
                GameObject go = new GameObject(fondos[i]);
                go.transform.SetParent(raiz);
                go.transform.position = new Vector3(10f, 9f, 0f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr(fondos[i]);
                sr.sortingOrder = -40 + i * 10;
                Parallax p = go.AddComponent<Parallax>();
                Asignar(p, "factorX", fx[i]);
                Asignar(p, "factorY", fy[i]);
                Asignar(p, "repetirHorizontal", true);
                Asignar(p, "camara", camara);
            }
        }

        // =====================================================================
        //  INTERFAZ DEL NIVEL: HUD, diálogo, pausa y resultados
        // =====================================================================

        static void CrearInterfazNivel()
        {
            GameObject canvas = CrearCanvas("Canvas");
            Transform raiz = canvas.transform;
            Sprite panel9 = Spr("panel_9slice_24");

            HUD hud = canvas.AddComponent<HUD>();
            SistemaDialogo dialogo = canvas.AddComponent<SistemaDialogo>();
            MenuPausa pausa = canvas.AddComponent<MenuPausa>();
            PantallaResultados resultados = canvas.AddComponent<PantallaResultados>();

            // ----- Batería (arriba a la izquierda) -----
            RectTransform bateria = NodoUI("Bateria", raiz);
            Anclar(bateria, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(340f, 54f));
            Image[] celdas = new Image[3];
            for (int i = 0; i < celdas.Length; i++)
            {
                celdas[i] = Imagen("Celda_" + (i + 1), bateria, Spr("celda_llena"), Color.white);
                Anclar(celdas[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 112f, 0f), new Vector2(102f, 54f));
            }

            // ----- Dial de frecuencias (arriba al centro) -----
            RectTransform dial = NodoUI("Dial", raiz);
            Anclar(dial, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(330f, 140f));
            Image fondoDial = Imagen("Fondo", dial, panel9, Color.white);
            fondoDial.type = Image.Type.Sliced;
            Estirar(fondoDial.rectTransform, 0f);
            Image[] indicadores = new Image[3];
            string[] nombresFrecuencia = { "Ambar", "Cian", "Magenta" };
            Color[] coloresFrecuencia = { ColorAmbar, ColorCian, ColorMagenta };
            for (int i = 0; i < indicadores.Length; i++)
            {
                indicadores[i] = Imagen("Indicador_" + nombresFrecuencia[i], dial, Spr("indicador_frecuencia"), coloresFrecuencia[i]);
                Anclar(indicadores[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f + i * 90f, 18f), new Vector2(56f, 56f));
            }
            TextMeshProUGUI textoFrecuencia = Texto("TextoFrecuencia", dial, "ÁMBAR", 30f, ColorAmbar, TextAlignmentOptions.Center);
            Anclar(textoFrecuencia.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(300f, 40f));

            // ----- Contadores (arriba a la derecha) -----
            RectTransform contadores = NodoUI("Contadores", raiz);
            Anclar(contadores, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(360f, 180f));
            Vector2 arribaIzq = new Vector2(0f, 1f);
            Image iconoChatarra = Imagen("IconoChatarra", contadores, Spr("icono_chatarra"), Color.white);
            Anclar(iconoChatarra.rectTransform, arribaIzq, arribaIzq, new Vector2(3f, 0f), new Vector2(48f, 48f));
            TextMeshProUGUI textoChatarra = Texto("TextoChatarra", contadores, "x 0", 36f, Color.white, TextAlignmentOptions.Left);
            Anclar(textoChatarra.rectTransform, arribaIzq, arribaIzq, new Vector2(70f, 0f), new Vector2(290f, 48f));
            Image iconoCinta = Imagen("IconoCinta", contadores, Spr("icono_cinta"), Color.white);
            Anclar(iconoCinta.rectTransform, arribaIzq, arribaIzq, new Vector2(0f, -64f), new Vector2(54f, 40f));
            TextMeshProUGUI textoCintas = Texto("TextoCintas", contadores, "Cintas 0/1", 36f, Color.white, TextAlignmentOptions.Left);
            Anclar(textoCintas.rectTransform, arribaIzq, arribaIzq, new Vector2(70f, -60f), new Vector2(290f, 48f));
            TextMeshProUGUI textoTiempo = Texto("TextoTiempo", contadores, "00:00.0", 32f, new Color(0.8f, 0.85f, 0.95f), TextAlignmentOptions.Left);
            Anclar(textoTiempo.rectTransform, arribaIzq, arribaIzq, new Vector2(70f, -120f), new Vector2(290f, 48f));

            // ----- Barra del jefe (abajo al centro) -----
            RectTransform panelJefe = NodoUI("PanelJefe", raiz);
            Anclar(panelJefe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 84f));
            TextMeshProUGUI nombreJefe = Texto("NombreJefe", panelJefe, "LA BOBINA", 34f, Color.white, TextAlignmentOptions.Center);
            Anclar(nombreJefe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 44f));
            Image fondoBarra = Imagen("FondoBarra", panelJefe, Spr("pixel_blanco"), new Color(0.05f, 0.06f, 0.1f, 0.9f));
            Anclar(fondoBarra.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(900f, 32f));
            Image barraJefe = Imagen("Barra", fondoBarra.transform, Spr("pixel_blanco"), ColorMagenta);
            Estirar(barraJefe.rectTransform, 4f);
            barraJefe.type = Image.Type.Filled;
            barraJefe.fillMethod = Image.FillMethod.Horizontal;
            barraJefe.fillOrigin = (int)Image.OriginHorizontal.Left;
            barraJefe.fillAmount = 1f;

            // ----- Mensajes (tercio superior) -----
            TextMeshProUGUI textoMensaje = Texto("TextoMensaje", raiz, "", 42f, Color.white, TextAlignmentOptions.Center);
            Anclar(textoMensaje.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1500f, 70f));

            // ----- Diálogo (panel inferior de 1500 × 260) -----
            Image panelDialogo = Imagen("PanelDialogo", raiz, panel9, Color.white);
            panelDialogo.type = Image.Type.Sliced;
            Anclar(panelDialogo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1500f, 260f));
            TextMeshProUGUI textoNombre = Texto("TextoNombre", panelDialogo.transform, "Pip", 38f, ColorCian, TextAlignmentOptions.Left);
            Anclar(textoNombre.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -30f), new Vector2(900f, 50f));
            TextMeshProUGUI textoCuerpo = Texto("TextoCuerpo", panelDialogo.transform, "", 36f, Color.white, TextAlignmentOptions.TopLeft);
            RectTransform rtCuerpo = textoCuerpo.rectTransform;
            rtCuerpo.anchorMin = Vector2.zero; rtCuerpo.anchorMax = Vector2.one;
            rtCuerpo.offsetMin = new Vector2(48f, 44f); rtCuerpo.offsetMax = new Vector2(-48f, -92f);
            // "▼" si la fuente lo tiene; si no, una "v" para no mostrar un cuadrado vacío.
            string flecha = fuente != null && fuente.HasCharacter('▼', true, true) ? "▼" : "v";
            TextMeshProUGUI indicador = Texto("IndicadorContinuar", panelDialogo.transform, flecha, 34f, ColorCian, TextAlignmentOptions.Center);
            Anclar(indicador.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 28f), new Vector2(50f, 50f));

            // ----- Pausa (pantalla completa, negro al 70 %) -----
            Image panelPausa = Imagen("PanelPausa", raiz, null, new Color(0f, 0f, 0f, 0.7f));
            panelPausa.raycastTarget = true;
            Estirar(panelPausa.rectTransform, 0f);
            TextMeshProUGUI tituloPausa = Texto("Titulo", panelPausa.transform, "PAUSA", 90f, ColorAmbar, TextAlignmentOptions.Center);
            Anclar(tituloPausa.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1000f, 120f));
            Button btnContinuar = Boton("BotonContinuar", panelPausa.transform, "Continuar", new Vector2(0f, 70f));
            Button btnReiniciar = Boton("BotonReiniciar", panelPausa.transform, "Reiniciar", new Vector2(0f, -40f));
            Button btnMenu = Boton("BotonMenu", panelPausa.transform, "Salir al menú", new Vector2(0f, -150f));
            UnityEventTools.AddPersistentListener(btnContinuar.onClick, new UnityAction(pausa.Continuar));
            UnityEventTools.AddPersistentListener(btnReiniciar.onClick, new UnityAction(pausa.Reiniciar));
            UnityEventTools.AddPersistentListener(btnMenu.onClick, new UnityAction(pausa.IrAlMenu));

            // ----- Resultados -----
            Image panelResultados = Imagen("PanelResultados", raiz, null, new Color(0.03f, 0.04f, 0.08f, 0.88f));
            panelResultados.raycastTarget = true;
            Estirar(panelResultados.rectTransform, 0f);
            TextMeshProUGUI tituloRes = Texto("TextoTitulo", panelResultados.transform, "SEÑAL RESTAURADA", 72f, ColorAmbar, TextAlignmentOptions.Center);
            Anclar(tituloRes.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1500f, 200f));
            TextMeshProUGUI detalleRes = Texto("TextoDetalle", panelResultados.transform, "", 42f, Color.white, TextAlignmentOptions.Center);
            Anclar(detalleRes.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 280f));
            Image medalla = Imagen("MedallaSenalLimpia", panelResultados.transform, Spr("medalla_senal_limpia"), Color.white);
            Anclar(medalla.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(128f, 128f));
            TextMeshProUGUI textoMedalla = Texto("Texto", medalla.transform, "Señal limpia: +50 de chatarra", 30f, ColorCian, TextAlignmentOptions.Center);
            Anclar(textoMedalla.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(900f, 40f));
            Button btnReintentar = Boton("BotonReintentar", panelResultados.transform, "Reintentar", new Vector2(-240f, -390f));
            Button btnMenuRes = Boton("BotonMenu", panelResultados.transform, "Menú principal", new Vector2(240f, -390f));
            UnityEventTools.AddPersistentListener(btnReintentar.onClick, new UnityAction(resultados.Reintentar));
            UnityEventTools.AddPersistentListener(btnMenuRes.onClick, new UnityAction(resultados.IrAlMenu));

            // ----- Referencias de los scripts -----
            Asignar(hud, "celdas", celdas);
            Asignar(hud, "celdaLlena", Spr("celda_llena"));
            Asignar(hud, "celdaVacia", Spr("celda_vacia"));
            Asignar(hud, "indicadores", indicadores);
            Asignar(hud, "dial", dial);
            Asignar(hud, "textoFrecuencia", textoFrecuencia);
            Asignar(hud, "textoChatarra", textoChatarra);
            Asignar(hud, "textoCintas", textoCintas);
            Asignar(hud, "textoTiempo", textoTiempo);
            Asignar(hud, "panelJefe", panelJefe.gameObject);
            Asignar(hud, "barraJefe", barraJefe);
            Asignar(hud, "nombreJefe", nombreJefe);
            Asignar(hud, "textoMensaje", textoMensaje);

            Asignar(dialogo, "panel", panelDialogo.gameObject);
            Asignar(dialogo, "textoNombre", textoNombre);
            Asignar(dialogo, "textoCuerpo", textoCuerpo);
            Asignar(dialogo, "indicadorContinuar", indicador.gameObject);

            Asignar(pausa, "panel", panelPausa.gameObject);
            Asignar(pausa, "primerBoton", btnContinuar.gameObject);

            Asignar(resultados, "panel", panelResultados.gameObject);
            Asignar(resultados, "textoTitulo", tituloRes);
            Asignar(resultados, "textoDetalle", detalleRes);
            Asignar(resultados, "medallaSenalLimpia", medalla.gameObject);
            Asignar(resultados, "primerBoton", btnReintentar.gameObject);

            // Los scripts los ocultan al arrancar; se guardan ocultos para que no tapen la vista en el Editor.
            panelJefe.gameObject.SetActive(false);
            textoMensaje.gameObject.SetActive(false);
            panelDialogo.gameObject.SetActive(false);
            panelPausa.gameObject.SetActive(false);
            panelResultados.gameObject.SetActive(false);
        }

        // =====================================================================
        //  e. ESCENA MENUPRINCIPAL
        // =====================================================================

        static void ConstruirMenu(bool tmpListo)
        {
            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CrearCamara(new Vector3(0f, 0f, -10f), ColorFondoCamara, false);

            if (!tmpListo)
            {
                Error("No se construyó el Canvas del menú porque falta TextMeshPro.");
                CrearEventSystem(null);
                GuardarEscena(escena, EscenaMenu);
                return;
            }

            GameObject canvas = CrearCanvas("Canvas");
            Transform raiz = canvas.transform;
            MenuPrincipal menu = canvas.AddComponent<MenuPrincipal>();
            Sprite panel9 = Spr("panel_9slice_24");
            Vector2 centro = new Vector2(0.5f, 0.5f);

            Image fondo = Imagen("Fondo", raiz, Spr("z1_0_cielo"), Color.white);
            fondo.preserveAspect = false;
            Estirar(fondo.rectTransform, 0f);
            Image torres = Imagen("FondoTorres", raiz, Spr("z1_1_torres_lejanas"), Color.white);
            torres.preserveAspect = false;
            Estirar(torres.rectTransform, 0f);

            TextMeshProUGUI titulo = Texto("Titulo", raiz, "ÚLTIMA FRECUENCIA", 120f, ColorAmbar, TextAlignmentOptions.Center);
            titulo.fontStyle = FontStyles.Bold;
            Anclar(titulo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1700f, 160f));
            TextMeshProUGUI subtitulo = Texto("Subtitulo", raiz, "Nivel 1-1 «Primer Contacto»", 40f, ColorCian, TextAlignmentOptions.Center);
            Anclar(subtitulo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(1500f, 60f));

            // ----- Panel principal -----
            RectTransform panelPrincipal = NodoUI("PanelPrincipal", raiz);
            Estirar(panelPrincipal, 0f);
            Button btnJugar = Boton("BotonJugar", panelPrincipal, "Jugar", new Vector2(0f, -30f));
            Button btnOpciones = Boton("BotonOpciones", panelPrincipal, "Opciones", new Vector2(0f, -140f));
            Button btnCreditos = Boton("BotonCreditos", panelPrincipal, "Créditos", new Vector2(0f, -250f));
            Button btnSalir = Boton("BotonSalir", panelPrincipal, "Salir", new Vector2(0f, -360f));
            TextMeshProUGUI ayuda = Texto("Controles", panelPrincipal, "A/D mover  ·  Espacio saltar  ·  Q/E cambiar de frecuencia  ·  W hablar  ·  Esc pausa", 28f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            Anclar(ayuda.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1800f, 50f));

            // ----- Panel de opciones -----
            RectTransform panelOpciones = NodoUI("PanelOpciones", raiz);
            Estirar(panelOpciones, 0f);
            Image marcoOpciones = Imagen("Marco", panelOpciones, panel9, Color.white);
            marcoOpciones.type = Image.Type.Sliced;
            Anclar(marcoOpciones.rectTransform, centro, centro, new Vector2(0f, -150f), new Vector2(900f, 420f));
            TextMeshProUGUI etiquetaVol = Texto("EtiquetaVolumen", panelOpciones, "Volumen general", 40f, Color.white, TextAlignmentOptions.Center);
            Anclar(etiquetaVol.rectTransform, centro, centro, new Vector2(0f, -30f), new Vector2(800f, 60f));
            Slider slider = CrearSlider("SliderVolumen", panelOpciones, new Vector2(0f, -120f), new Vector2(640f, 40f));
            Button btnVolverOpc = Boton("BotonVolver", panelOpciones, "Volver", new Vector2(0f, -270f));

            // ----- Panel de créditos -----
            RectTransform panelCreditos = NodoUI("PanelCreditos", raiz);
            Estirar(panelCreditos, 0f);
            Image marcoCreditos = Imagen("Marco", panelCreditos, panel9, Color.white);
            marcoCreditos.type = Image.Type.Sliced;
            Anclar(marcoCreditos.rectTransform, centro, centro, new Vector2(0f, -150f), new Vector2(1300f, 620f));
            TextMeshProUGUI textoCreditos = Texto("TextoCreditos", panelCreditos,
                "<b>ÚLTIMA FRECUENCIA</b>\n" +
                "Integrante 1: [nombre]  ·  Integrante 2: [nombre]\n" +
                "Cátedra: Diseño y Desarrollo de Videojuegos (AE4)\n\n" +
                "Motor: Unity 6 (URP 2D, Tilemaps, Input System, TextMeshPro)\n" +
                "Arte pixel art, efectos y música: generados por código para este proyecto\n" +
                "Fuente: Liberation Sans (SIL OFL)",
                32f, Color.white, TextAlignmentOptions.Center);
            Anclar(textoCreditos.rectTransform, centro, centro, new Vector2(0f, -90f), new Vector2(1200f, 420f));
            Button btnVolverCre = Boton("BotonVolver", panelCreditos, "Volver", new Vector2(0f, -380f));

            // ----- Botones (OnClick persistentes, como si se conectaran en el Inspector) -----
            UnityEventTools.AddPersistentListener(btnJugar.onClick, new UnityAction(menu.Jugar));
            UnityEventTools.AddPersistentListener(btnOpciones.onClick, new UnityAction(menu.AbrirOpciones));
            UnityEventTools.AddPersistentListener(btnCreditos.onClick, new UnityAction(menu.AbrirCreditos));
            UnityEventTools.AddPersistentListener(btnSalir.onClick, new UnityAction(menu.Salir));
            UnityEventTools.AddPersistentListener(btnVolverOpc.onClick, new UnityAction(menu.Volver));
            UnityEventTools.AddPersistentListener(btnVolverCre.onClick, new UnityAction(menu.Volver));
            if (slider != null) UnityEventTools.AddPersistentListener(slider.onValueChanged, new UnityAction<float>(menu.CambiarVolumen));

            Asignar(menu, "escenaNivel", "Nivel1");
            Asignar(menu, "panelPrincipal", panelPrincipal.gameObject);
            Asignar(menu, "panelOpciones", panelOpciones.gameObject);
            Asignar(menu, "panelCreditos", panelCreditos.gameObject);
            Asignar(menu, "sliderVolumen", slider);
            Asignar(menu, "primerBotonPrincipal", btnJugar.gameObject);
            Asignar(menu, "primerBotonOpciones", slider != null ? slider.gameObject : btnVolverOpc.gameObject);
            Asignar(menu, "botonVolverCreditos", btnVolverCre.gameObject);

            panelOpciones.gameObject.SetActive(false);
            panelCreditos.gameObject.SetActive(false);

            CrearEventSystem(btnJugar.gameObject);
            GuardarEscena(escena, EscenaMenu);
        }

        // =====================================================================
        //  f. BUILD SETTINGS
        // =====================================================================

        static void RegistrarEscenas()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(EscenaMenu, true),
                new EditorBuildSettingsScene(EscenaNivel, true)
            };
        }

        static void GuardarEscena(Scene escena, string ruta)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena, ruta)) Error("No se pudo guardar la escena " + ruta);
            else Debug.Log("[Constructor] Escena guardada: " + ruta);
        }

        // =====================================================================
        //  AYUDAS DE UI
        // =====================================================================

        static GameObject CrearCanvas(string nombre)
        {
            GameObject go = new GameObject(nombre, typeof(RectTransform));
            go.layer = CapaUI;
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler escala = go.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            escala.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static void CrearEventSystem(GameObject primerSeleccionado)
        {
            GameObject go = new GameObject("EventSystem");
            EventSystem es = go.AddComponent<EventSystem>();
            es.firstSelectedGameObject = primerSeleccionado;
#if ENABLE_INPUT_SYSTEM
            // Al habilitarse sin acciones asignadas, el módulo carga solo las acciones de UI por defecto.
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static RectTransform NodoUI(string nombre, Transform padre)
        {
            GameObject go = new GameObject(nombre, typeof(RectTransform));
            go.layer = CapaUI;
            go.transform.SetParent(padre, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Ancla puntual: posición y tamaño fijos respecto de un punto del padre.</summary>
        static void Anclar(RectTransform rt, Vector2 ancla, Vector2 pivote, Vector2 posicion, Vector2 tamano)
        {
            rt.anchorMin = ancla;
            rt.anchorMax = ancla;
            rt.pivot = pivote;
            rt.sizeDelta = tamano;
            rt.anchoredPosition = posicion;
        }

        /// <summary>Ocupa todo el padre, con un margen opcional.</summary>
        static void Estirar(RectTransform rt, float margen)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(margen, margen);
            rt.offsetMax = new Vector2(-margen, -margen);
        }

        static Image Imagen(string nombre, Transform padre, Sprite sprite, Color color)
        {
            RectTransform rt = NodoUI(nombre, padre);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Texto(string nombre, Transform padre, string contenido, float tamano, Color color, TextAlignmentOptions alineacion)
        {
            RectTransform rt = NodoUI(nombre, padre);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (fuente != null) t.font = fuente;
            t.text = contenido;
            t.fontSize = tamano;
            t.color = color;
            t.alignment = alineacion;
            t.raycastTarget = false;
            return t;
        }

        static Button Boton(string nombre, Transform padre, string etiqueta, Vector2 posicion)
        {
            Vector2 centro = new Vector2(0.5f, 0.5f);
            Image img = Imagen(nombre, padre, Spr("panel_9slice_24"), Color.white);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            Anclar(img.rectTransform, centro, centro, posicion, new Vector2(420f, 90f));

            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock colores = b.colors;
            colores.normalColor = new Color(0.72f, 0.72f, 0.78f);
            colores.highlightedColor = Color.white;
            colores.selectedColor = new Color(1f, 0.82f, 0.45f); // botón elegido con teclado o mando
            colores.pressedColor = new Color(0.55f, 0.55f, 0.6f);
            colores.fadeDuration = 0.05f;
            b.colors = colores;

            TextMeshProUGUI t = Texto("Texto", img.transform, etiqueta, 38f, Color.white, TextAlignmentOptions.Center);
            Estirar(t.rectTransform, 0f);
            return b;
        }

        static Slider CrearSlider(string nombre, Transform padre, Vector2 posicion, Vector2 tamano)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            if (go == null) { Error("No se pudo crear el Slider"); return null; }
            go.name = nombre;
            go.transform.SetParent(padre, false);
            PonerCapa(go.transform, CapaUI);
            Vector2 centro = new Vector2(0.5f, 0.5f);
            Anclar((RectTransform)go.transform, centro, centro, posicion, tamano);

            Slider s = go.GetComponent<Slider>();
            s.minValue = 0f;
            s.maxValue = 1f;
            s.wholeNumbers = false;
            s.value = 1f;

            Colorear(go.transform.Find("Background"), new Color(0.08f, 0.1f, 0.16f, 1f));
            Colorear(go.transform.Find("Fill Area/Fill"), ColorAmbar);
            Colorear(go.transform.Find("Handle Slide Area/Handle"), Color.white);
            return s;
        }

        static void Colorear(Transform t, Color c)
        {
            Image img = t != null ? t.GetComponent<Image>() : null;
            if (img != null) img.color = c;
        }

        static void PonerCapa(Transform t, int capa)
        {
            t.gameObject.layer = capa;
            for (int i = 0; i < t.childCount; i++) PonerCapa(t.GetChild(i), capa);
        }

        // =====================================================================
        //  AYUDAS GENERALES
        // =====================================================================

        static GameObject Hijo(string nombre, Transform padre, Vector3 posicionLocal, int capa)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicionLocal;
            go.layer = capa;
            return go;
        }

        /// <summary>Busca un sprite de Assets/Arte por nombre de archivo (sin extensión).</summary>
        static Sprite Spr(string nombre)
        {
            string ruta;
            if (indicePng == null || !indicePng.TryGetValue(nombre, out ruta))
            {
                Error("Falta el sprite '" + nombre + ".png' en " + RutaArte);
                return null;
            }
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            if (s == null) Error("'" + ruta + "' no se importó como Sprite");
            return s;
        }

        static AudioClip Clip(string ruta)
        {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
            if (c == null) Error("Falta el audio " + ruta);
            return c;
        }

        /// <summary>Crea el asset; si ya existía lo reemplaza (las escenas se reconstruyen después, así que no quedan referencias rotas).</summary>
        static T GuardarAsset<T>(T objeto, string ruta) where T : Object
        {
            if (AssetDatabase.LoadMainAssetAtPath(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(objeto, ruta);
            return objeto;
        }

        static Type BuscarTipo(string nombreCompleto)
        {
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(nombreCompleto);
                if (t != null) return t;
            }
            return null;
        }

        static void Error(string mensaje)
        {
            errores++;
            Debug.LogError("[Constructor] " + mensaje);
        }

        /// <summary>
        /// Asigna un campo serializado (aunque sea privado con [SerializeField]) igual que lo haría el
        /// Inspector: SerializedObject + FindProperty + ApplyModifiedPropertiesWithoutUndo.
        /// Acepta int, float, bool, string, enum (como int), Vector2, Color, referencias y arrays.
        /// </summary>
        static void Asignar(Object objetivo, string campo, object valor)
        {
            if (objetivo == null) { Error("Se intentó asignar '" + campo + "' en un objeto nulo"); return; }
            SerializedObject so = new SerializedObject(objetivo);
            SerializedProperty p = so.FindProperty(campo);
            if (p == null) { Error("El campo '" + campo + "' no existe en " + objetivo.GetType().Name); return; }
            Poner(p, valor, objetivo.GetType().Name + "." + campo);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Poner(SerializedProperty p, object valor, string descripcion)
        {
            if (p.isArray && p.propertyType != SerializedPropertyType.String)
            {
                IList lista = valor as IList;
                p.arraySize = lista != null ? lista.Count : 0;
                for (int i = 0; i < p.arraySize; i++) Poner(p.GetArrayElementAtIndex(i), lista[i], descripcion + "[" + i + "]");
                return;
            }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    p.intValue = Convert.ToInt32(valor);
                    break;
                case SerializedPropertyType.Float:
                    p.floatValue = Convert.ToSingle(valor);
                    break;
                case SerializedPropertyType.Boolean:
                    p.boolValue = (bool)valor;
                    break;
                case SerializedPropertyType.String:
                    p.stringValue = (string)valor;
                    break;
                case SerializedPropertyType.Vector2:
                    p.vector2Value = (Vector2)valor;
                    break;
                case SerializedPropertyType.Color:
                    p.colorValue = (Color)valor;
                    break;
                case SerializedPropertyType.ObjectReference:
                    Object referencia = valor as Object;
                    p.objectReferenceValue = referencia;
                    if (referencia == null) Error("Referencia vacía para " + descripcion);
                    else if (p.objectReferenceValue == null) Error("Tipo incompatible al asignar " + descripcion + " (" + referencia.GetType().Name + ")");
                    break;
                default:
                    Error("Tipo de campo no soportado en " + descripcion + ": " + p.propertyType);
                    break;
            }
        }
    }
}
