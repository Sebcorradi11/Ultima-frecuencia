using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Trigger alargado debajo de los vacíos: al tocarlo, el jugador vuelve a la última antena
    /// de relevo con una celda menos.
    /// </summary>
    public class ZonaMuerte : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D otro)
        {
            VidaJugador vida = otro.GetComponentInParent<VidaJugador>();
            if (vida != null) vida.CaerAlVacio();
        }

        void OnDrawGizmos()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawCube(transform.TransformPoint(box.offset), Vector3.Scale(box.size, transform.lossyScale));
        }
    }
}
