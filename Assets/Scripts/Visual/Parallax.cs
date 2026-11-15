using UnityEngine;

namespace UltimaFrecuencia
{
    /// <summary>
    /// Parallax scrolling para un fondo (backdrop). Se pone en cada capa de fondo con un SpriteRenderer.
    /// factor = 1 → se mueve igual que la cámara (parece infinitamente lejos, por ejemplo el cielo).
    /// factor = 0 → no se mueve (está en el plano del nivel).
    /// Valores sugeridos: cielo 0,95 · torres lejanas 0,8 · estructuras medias 0,55 · primer plano 0,3.
    /// Con "repetir" crea dos copias a los costados para que el fondo no se termine.
    /// </summary>
    public class Parallax : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float factorX = 0.5f;
        [SerializeField, Range(0f, 1f)] float factorY = 0.2f;
        [SerializeField] bool repetirHorizontal = true;
        [Tooltip("Si se deja vacío usa Camera.main.")]
        [SerializeField] Transform camara;

        float anchoSprite;
        Vector3 inicio;
        Vector3 camaraInicio;
        float desplazamientoBucle;

        void Start()
        {
            if (camara == null && Camera.main != null) camara = Camera.main.transform;
            if (camara == null) { enabled = false; return; }

            inicio = transform.position;
            camaraInicio = camara.position;

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) anchoSprite = sr.bounds.size.x;

            if (repetirHorizontal && sr != null && anchoSprite > 0f)
            {
                CrearCopia(sr, -anchoSprite);
                CrearCopia(sr, anchoSprite);
            }
        }

        void CrearCopia(SpriteRenderer original, float offsetX)
        {
            GameObject copia = new GameObject(original.name + (offsetX < 0 ? "_Izq" : "_Der"));
            copia.transform.SetParent(transform, false);
            copia.transform.localPosition = new Vector3(offsetX / Mathf.Max(0.0001f, transform.lossyScale.x), 0f, 0f);
            SpriteRenderer sr = copia.AddComponent<SpriteRenderer>();
            sr.sprite = original.sprite;
            sr.color = original.color;
            sr.sortingLayerID = original.sortingLayerID;
            sr.sortingOrder = original.sortingOrder;
            sr.flipX = original.flipX;
        }

        void LateUpdate()
        {
            Vector3 delta = camara.position - camaraInicio;
            float x = inicio.x + desplazamientoBucle + delta.x * factorX;
            float y = inicio.y + delta.y * factorY;
            transform.position = new Vector3(x, y, inicio.z);

            if (!repetirHorizontal || anchoSprite <= 0f) return;

            // Cuando la cámara se aleja más de un ancho del centro del fondo, se recentra.
            float relativo = camara.position.x - transform.position.x;
            if (relativo > anchoSprite) desplazamientoBucle += anchoSprite;
            else if (relativo < -anchoSprite) desplazamientoBucle -= anchoSprite;
        }
    }
}
