using System;
using UnityEngine;

namespace CuteDivinity.Prototype
{
    /// <summary>
    /// Bus global de ruido. Cualquier sistema (jugador, piedras que se hunden, altares)
    /// emite "unidades de ruido" en una posición; las entidades que escuchan (CosmicEyeWatcher)
    /// se suscriben y acumulan sospecha según la distancia.
    ///
    /// Convención:
    ///  - Ruido continuo: emitir (intensidad * Time.deltaTime) cada frame.
    ///  - Ruido puntual (splash, fallo de ritual): emitir un valor directo (0.5 - 2).
    /// </summary>
    public static class NoiseBus
    {
        public static event Action<Vector3, float> NoiseEmitted;

        public static void Emit(Vector3 position, float amount)
        {
            if (amount <= 0f) return;
            NoiseEmitted?.Invoke(position, amount);
        }

        // Limpia suscriptores si "Enter Play Mode Options" tiene desactivado el Domain Reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => NoiseEmitted = null;
    }

    /// <summary>
    /// Utilidades de feedback visual para greyboxing. Usa MaterialPropertyBlock para
    /// teñir primitivas sin duplicar materiales (funciona con URP/Lit "_BaseColor"
    /// y con shaders Built-in "_Color").
    /// </summary>
    public static class ProtoVisuals
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static MaterialPropertyBlock block;

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// ¿Está el punto dentro de la zona? Se usa en lugar de OnTriggerEnter para que
        /// los puzzles no dependan de Rigidbodies ni del orden de eventos físicos.
        /// La zona debe ser un BoxCollider alineado a los ejes (sin rotación).
        /// </summary>
        public static bool Contains(Collider zone, Vector3 point)
        {
            return zone != null && zone.enabled && zone.bounds.Contains(point);
        }
    }
}
