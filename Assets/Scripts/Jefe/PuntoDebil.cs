using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Núcleo expuesto de La Bobina. Va en un hijo del jefe con un BoxCollider2D (Is Trigger)
    /// arriba del cuerpo. Si el jugador cae sobre él mientras el jefe es vulnerable, lo daña.
    /// </summary>
    public class PuntoDebil : MonoBehaviour
    {
        [SerializeField] float rebote = 16f;
        [SerializeField] float pulso = 0.15f;

        JefeBobina jefe;
        Vector3 escalaBase;

        public void Configurar(JefeBobina j)
        {
            jefe = j;
            escalaBase = transform.localScale;
        }

        void Update()
        {
            float s = 1f + Mathf.Sin(Time.time * 14f) * pulso;
            transform.localScale = new Vector3(escalaBase.x * s, escalaBase.y * s, 1f);
        }

        void OnTriggerEnter2D(Collider2D otro)
        {
            if (jefe == null || !jefe.Vulnerable) return;
            ControladorJugador control = otro.GetComponentInParent<ControladorJugador>();
            if (control == null || control.Velocidad.y > 0.5f) return;
            // Solo cuenta si los pies del jugador están por encima del centro del núcleo (pisotón, no roce lateral).
            if (control.Cuerpo.bounds.min.y < transform.position.y) return;

            control.Rebotar(rebote);
            jefe.RecibirGolpe();
        }
    }
}
