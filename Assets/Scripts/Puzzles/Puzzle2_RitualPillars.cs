using System;
using UnityEngine;
using UnityEngine.Events;

namespace CuteDivinity.Prototype
{
    /// <summary>Datos de un pilar del ritual (se configura en el Inspector).</summary>
    [Serializable]
    public class RitualPillar
    {
        [Tooltip("BoxCollider (Is Trigger) delante del pilar: dónde debe estar el jugador.")]
        public Collider zone;
        [Tooltip("Cylinder hijo de un 'MeterRoot' vacío (escala 1). Se escala en Y como el agua.")]
        public Transform meterFill;
        public Renderer meterRenderer;
        [Tooltip("Cubo fino hijo del mismo MeterRoot: marca la altura objetivo.")]
        public Transform targetMarker;
        public Renderer orbRenderer;
        public Light orbLight;
        [Min(0.05f)] public float targetHoldTime = 1f;

        [NonSerialized] public float fill;
        [NonSerialized] public bool solved;
        [NonSerialized] public bool overflowed;
        [NonSerialized] public float flashTimer;
        [NonSerialized] public Vector3 meterBaseScale;
    }

    /// <summary>
    /// PUZZLE 2 — Pilares rituales.
    ///
    /// Tres pilares, cada uno con un tiempo de pulsación objetivo (corto / medio / largo).
    /// El jugador se coloca en la zona del pilar, SIN pulsar dirección, y mantiene el botón:
    ///  - El medidor de agua sube con el tiempo de pulsación (escala Y).
    ///  - La marca amarilla indica la altura objetivo; el medidor se vuelve amarillo dentro de la ventana.
    ///  - Soltar dentro de la ventana (±tolerance) => pilar resuelto (medidor y orbe verdes, luz encendida).
    ///  - Soltar fuera / pasarse del máximo => flash rojo, el agua se vacía, splash de ruido.
    /// Con los 3 resueltos la puerta se hunde en el suelo.
    /// </summary>
    public class Puzzle2_RitualPillars : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] PlayerPressureController player;
        [SerializeField] RitualPillar[] pillars =
        {
            new RitualPillar { targetHoldTime = 0.6f },
            new RitualPillar { targetHoldTime = 1.5f },
            new RitualPillar { targetHoldTime = 2.8f },
        };

        [Header("Reglas")]
        [Tooltip("Segundos de pulsación que llenan el medidor al 100%.")]
        [SerializeField] float meterMaxTime = 3.5f;
        [Tooltip("Margen (±seg) alrededor del objetivo.")]
        [SerializeField] float tolerance = 0.3f;
        [Tooltip("Si es true, hay que resolverlos en el orden de la lista.")]
        [SerializeField] bool requireOrder;
        [SerializeField] float failNoise = 0.6f;

        [Header("Medidor (visual)")]
        [Tooltip("Altura en unidades del medidor lleno.")]
        [SerializeField] float meterFullHeight = 2f;
        [Tooltip("Altura del mesh a escala 1 (Cylinder = 2, Cube = 1).")]
        [SerializeField] float meterMeshHeight = 2f;
        [Tooltip("Fracción del medidor que se vacía por segundo.")]
        [SerializeField] float drainSpeed = 0.8f;
        [SerializeField] float failFlashTime = 0.6f;
        [SerializeField] float orbLightIntensity = 3f;

        [Header("Puerta")]
        [SerializeField] Transform door;
        [SerializeField] Renderer doorRenderer;
        [SerializeField] Vector3 doorOpenOffset = new Vector3(0f, -4.2f, 0f);
        [SerializeField] float doorOpenTime = 2f;
        [SerializeField] Transform checkpointOnSolve;
        [SerializeField] UnityEvent onSolved;

        [Header("Colores")]
        [SerializeField] Color meterIdle = new Color(0.2f, 0.45f, 1f);
        [SerializeField] Color meterInWindow = new Color(1f, 0.9f, 0.2f);
        [SerializeField] Color meterFail = new Color(1f, 0.15f, 0.1f);
        [SerializeField] Color meterSolved = new Color(0.2f, 1f, 0.35f);
        [SerializeField] Color orbOff = new Color(0.15f, 0.15f, 0.18f);
        [SerializeField] Color doorClosed = new Color(0.35f, 0.25f, 0.45f);
        [SerializeField] Color doorOpen = new Color(0.2f, 1f, 0.35f);

        public bool IsSolved { get; private set; }

        Vector3 doorClosedPosition;
        float doorProgress;

        void Awake()
        {
            foreach (RitualPillar p in pillars)
            {
                if (p.meterFill != null) p.meterBaseScale = p.meterFill.localScale;
                if (p.targetMarker != null)
                {
                    Vector3 lp = p.targetMarker.localPosition;
                    lp.y = Mathf.Clamp01(p.targetHoldTime / meterMaxTime) * meterFullHeight;
                    p.targetMarker.localPosition = lp;
                }
            }

            if (door != null) doorClosedPosition = door.localPosition;

            if (player == null)
                Debug.LogError($"[{name}] Puzzle2 necesita la referencia al Player.", this);
        }

        void OnEnable()
        {
            if (player != null) player.PressReleased += OnPressReleased;
        }

        void OnDisable()
        {
            if (player != null) player.PressReleased -= OnPressReleased;
        }

        void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;

            for (int i = 0; i < pillars.Length; i++)
            {
                RitualPillar p = pillars[i];
                bool inside = !p.solved && ProtoVisuals.Contains(p.zone, player.transform.position);
                player.SetInteractionFocus(this, i, inside);

                bool pumping = player.IsPumpingFor(this, i);
                if (p.solved)
                {
                    p.fill = Mathf.Clamp01(p.targetHoldTime / meterMaxTime);
                }
                else if (pumping)
                {
                    float d = player.PressDuration;
                    p.fill = Mathf.Clamp01(d / meterMaxTime);
                    if (!p.overflowed && d > meterMaxTime)
                    {
                        p.overflowed = true;
                        Fail(p);
                    }
                }
                else if (p.flashTimer <= 0f)
                {
                    p.fill = Mathf.MoveTowards(p.fill, 0f, drainSpeed * dt);
                }

                p.flashTimer -= dt;
                UpdatePillarVisual(p, pumping);
            }

            UpdateDoor(dt);
        }

        void OnPressReleased(PressInfo info)
        {
            if (info.Owner != this || info.Slot < 0 || info.Slot >= pillars.Length) return;

            RitualPillar p = pillars[info.Slot];
            if (p.solved) return;
            if (p.overflowed)
            {
                p.overflowed = false; // ya falló al pasarse; solo limpiamos
                return;
            }

            bool timingOk = Mathf.Abs(info.Duration - p.targetHoldTime) <= tolerance;
            bool orderOk = !requireOrder || PreviousSolved(info.Slot);

            if (timingOk && orderOk) SolvePillar(p);
            else Fail(p);
        }

        bool PreviousSolved(int index)
        {
            for (int i = 0; i < index; i++)
                if (!pillars[i].solved) return false;
            return true;
        }

        void SolvePillar(RitualPillar p)
        {
            p.solved = true;
            player.SetInteractionFocus(this, Array.IndexOf(pillars, p), false);

            foreach (RitualPillar other in pillars)
                if (!other.solved) return;

            IsSolved = true;
            if (checkpointOnSolve != null) player.SetCheckpoint(checkpointOnSolve);
            onSolved?.Invoke();
        }

        void Fail(RitualPillar p)
        {
            p.flashTimer = failFlashTime;
            Vector3 pos = p.zone != null ? p.zone.bounds.center : transform.position;
            NoiseBus.Emit(pos, failNoise);
        }

        void UpdatePillarVisual(RitualPillar p, bool pumping)
        {
            // Medidor: escala Y anclada a la base (el MeterRoot marca el fondo).
            if (p.meterFill != null)
            {
                float h = Mathf.Max(0.001f, p.fill * meterFullHeight);
                Vector3 s = p.meterBaseScale;
                s.y = h / meterMeshHeight;
                p.meterFill.localScale = s;

                Vector3 lp = p.meterFill.localPosition;
                lp.y = h * 0.5f;
                p.meterFill.localPosition = lp;
            }

            Color meterColor;
            if (p.solved) meterColor = meterSolved;
            else if (p.flashTimer > 0f) meterColor = meterFail;
            else if (pumping && Mathf.Abs(player.PressDuration - p.targetHoldTime) <= tolerance) meterColor = meterInWindow;
            else meterColor = meterIdle;
            ProtoVisuals.SetColor(p.meterRenderer, meterColor);

            Color orbColor = p.solved ? meterSolved : Color.Lerp(orbOff, meterColor, p.fill * 0.8f);
            ProtoVisuals.SetColor(p.orbRenderer, orbColor);

            if (p.orbLight != null)
            {
                p.orbLight.color = orbColor;
                p.orbLight.intensity = p.solved ? orbLightIntensity : p.fill * orbLightIntensity * 0.5f;
                p.orbLight.enabled = p.orbLight.intensity > 0.01f;
            }
        }

        void UpdateDoor(float dt)
        {
            if (door == null) return;

            if (IsSolved)
                doorProgress = Mathf.MoveTowards(doorProgress, 1f, dt / Mathf.Max(0.01f, doorOpenTime));

            door.localPosition = doorClosedPosition + doorOpenOffset * Mathf.SmoothStep(0f, 1f, doorProgress);
            ProtoVisuals.SetColor(doorRenderer, Color.Lerp(doorClosed, doorOpen, doorProgress));
        }
    }
}
