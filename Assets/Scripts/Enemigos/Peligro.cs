using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Daña al jugador mientras lo toca (pinchos, cables pelados, suelo electrificado del jefe).
    /// Puede depender de una frecuencia: solo daña cuando esa frecuencia está activa.
    /// Requiere un Collider2D con Is Trigger.
    /// </summary>
    public class Peligro : MonoBehaviour
    {
        [SerializeField] int dano = 1;
        [SerializeField] bool dependeDeFrecuencia = false;
        [SerializeField] Frecuencia frecuencia = Frecuencia.Ambar;

        void OnTriggerStay2D(Collider2D otro)
        {
            // Los mensajes de física llegan aunque el componente esté desactivado: se filtra a mano.
            if (!isActiveAndEnabled) return;
            if (dependeDeFrecuencia && !GestorFrecuencia.EstaActiva(frecuencia)) return;

            VidaJugador vida = otro.GetComponentInParent<VidaJugador>();
            if (vida != null) vida.RecibirDano(dano, transform.position);
        }
    }
}
