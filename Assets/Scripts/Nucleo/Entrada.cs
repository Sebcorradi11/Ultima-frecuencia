using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UltimaFrecuencia
{
    /// <summary>
    /// Lectura de controles (Tabla de controles del GDD). Funciona con el Input System nuevo
    /// (por defecto en Unity 6) y con el Input Manager clásico, sin configurar acciones.
    /// Teclado: A/D o flechas, Espacio, Q/E, W, Esc. Mando: stick/cruceta, A, LB/RB, Y, Start.
    /// </summary>
    public static class Entrada
    {
        const float zonaMuerta = 0.3f;

        public static float Horizontal()
        {
#if ENABLE_INPUT_SYSTEM
            float x = 0f;
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
            }
            Gamepad g = Gamepad.current;
            if (g != null)
            {
                float gx = g.leftStick.x.ReadValue();
                if (Mathf.Abs(gx) > zonaMuerta) x = Mathf.Sign(gx);
                if (g.dpad.left.isPressed) x = -1f;
                if (g.dpad.right.isPressed) x = 1f;
            }
            return Mathf.Clamp(x, -1f, 1f);
#else
            float x = Input.GetAxisRaw("Horizontal");
            return Mathf.Abs(x) > zonaMuerta ? Mathf.Sign(x) : 0f;
#endif
        }

        public static bool SaltoPresionado()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
#endif
        }

        public static bool SaltoMantenido()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
#else
            return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
#endif
        }

        public static bool FrecuenciaSiguiente()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton5);
#endif
        }

        public static bool FrecuenciaAnterior()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton4);
#endif
        }

        public static bool Interactuar()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.JoystickButton3);
#endif
        }

        /// <summary>Avanzar en un diálogo: Espacio, W, Enter o el botón A/Y del mando.</summary>
        public static bool Avanzar()
        {
#if ENABLE_INPUT_SYSTEM
            bool tecla = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
            return tecla || SaltoPresionado() || Interactuar();
#else
            return Input.GetKeyDown(KeyCode.Return) || SaltoPresionado() || Interactuar();
#endif
        }

        public static bool Pausa()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
        }
    }
}
