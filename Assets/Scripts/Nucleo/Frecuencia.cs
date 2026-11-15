using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Las tres frecuencias del mundo. La capa "Base" no es una frecuencia: siempre es sólida.
    /// </summary>
    public enum Frecuencia
    {
        Ambar = 0,
        Cian = 1,
        Magenta = 2
    }

    /// <summary>
    /// Datos fijos de cada frecuencia: nombre de la capa física (Layer) y color de la paleta del GDD.
    /// </summary>
    public static class FrecuenciaUtil
    {
        public const int Cantidad = 3;
        public const string CapaBase = "Base";

        static readonly string[] nombresCapa = { "Ambar", "Cian", "Magenta" };
        static readonly string[] nombresVisibles = { "ÁMBAR", "CIAN", "MAGENTA" };

        // Paleta del Art Concept: #F0A030, #38C8D6, #D640A0
        static readonly Color[] colores =
        {
            new Color(0.941f, 0.627f, 0.188f),
            new Color(0.220f, 0.784f, 0.839f),
            new Color(0.839f, 0.251f, 0.627f)
        };

        public static string NombreCapa(Frecuencia f) { return nombresCapa[(int)f]; }
        public static string NombreVisible(Frecuencia f) { return nombresVisibles[(int)f]; }
        public static Color ColorDe(Frecuencia f) { return colores[(int)f]; }

        /// <summary>Máscara de la capa física de una frecuencia (por ejemplo, solo "Cian").</summary>
        public static int Mascara(Frecuencia f) { return LayerMask.GetMask(nombresCapa[(int)f]); }

        /// <summary>Devuelve la frecuencia siguiente o anterior dentro de las desbloqueadas.</summary>
        public static Frecuencia Desplazar(Frecuencia actual, int direccion, int desbloqueadas)
        {
            int n = Mathf.Clamp(desbloqueadas, 1, Cantidad);
            int i = ((int)actual + direccion) % n;
            if (i < 0) i += n;
            return (Frecuencia)i;
        }
    }
}
