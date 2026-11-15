using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Punto de control (checkpoint). Al tocarla se enciende y pasa a ser el lugar de reaparición.
    /// Requiere un BoxCollider2D (Is Trigger).
    /// </summary>
    public class AntenaRelevo : MonoBehaviour
    {
        [Tooltip("Dónde reaparece el jugador. Si se deja vacío, usa la posición de la antena.")]
        [SerializeField] Transform puntoReaparicion;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Sprite spriteApagada;
        [SerializeField] Sprite spriteEncendida;
        [Tooltip("Luz o partículas que se activan al encenderla (opcional).")]
        [SerializeField] GameObject efectoEncendida;

        bool encendida;

        void Awake()
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null && spriteApagada != null) sprite.sprite = spriteApagada;
            if (efectoEncendida != null) efectoEncendida.SetActive(false);
        }

        void OnTriggerEnter2D(Collider2D otro)
        {
            if (encendida) return;
            VidaJugador vida = otro.GetComponentInParent<VidaJugador>();
            if (vida == null) return;

            encendida = true;
            vida.FijarPuntoReaparicion(puntoReaparicion != null ? puntoReaparicion.position : transform.position + Vector3.up * 0.5f);
            if (sprite != null && spriteEncendida != null) sprite.sprite = spriteEncendida;
            if (efectoEncendida != null) efectoEncendida.SetActive(true);
            GestorAudio.Reproducir(Sonido.Antena);
            HUD.MostrarMensaje("Antena de relevo activada");
        }
    }
}
