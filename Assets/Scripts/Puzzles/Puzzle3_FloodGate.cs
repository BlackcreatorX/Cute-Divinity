using UnityEngine;
using UnityEngine.Events;

namespace CuteDivinity.Prototype
{
    /// <summary>
    /// PUZZLE 3 — La compuerta inundada.
    ///
    /// El jugador se coloca junto al altar central (sin pulsar dirección) y MANTIENE el botón:
    /// el agua del estanque sube (escala Y de un Cube anclado al fondo). Al soltar, tras
    /// drainDelay, el agua baja poco a poco. Cuando el agua alcanza la marca de la compuerta,
    /// la compuerta se abre y el agua queda fija.
    ///
    /// Tensión: el CosmicEyeWatcher (modo periódico activado) se abre cada pocos segundos mirando
    /// al altar, y el bombeo hace ruido que lo acelera. El altar se pone amarillo (aviso) y rojo
    /// (peligro) según el estado del ojo: hay que soltar y esconderse tras las columnas (capa Cover).
    /// </summary>
    public class Puzzle3_FloodGate : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] PlayerPressureController player;
        [SerializeField] CosmicEyeWatcher eye;
        [Tooltip("BoxCollider (Is Trigger) alrededor del altar.")]
        [SerializeField] Collider altarZone;
        [SerializeField] Renderer altarRenderer;

        [Header("Agua")]
        [Tooltip("Cube del agua (raíz o hijo de un padre SIN escala). Su base queda fija; se escala en Y.")]
        [SerializeField] Transform water;
        [SerializeField] Renderer waterRenderer;
        [SerializeField] float startLevel = 0.3f;
        [Tooltip("Altura del agua (desde la base) que abre la compuerta.")]
        [SerializeField] float gateLevel = 3.5f;
        [SerializeField] float riseSpeed = 0.45f;
        [SerializeField] float drainSpeed = 0.3f;
        [Tooltip("Segundos tras soltar antes de que el agua empiece a bajar.")]
        [SerializeField] float drainDelay = 0.8f;
        [Tooltip("0 = sin trinquete. N>0 = el agua no baja de cada 1/N del recorrido ya alcanzado.")]
        [SerializeField, Min(0)] int ratchetSegments;
        [SerializeField] float waveAmplitude = 0.05f;
        [SerializeField] float waveFrequency = 5f;
        [Tooltip("Ruido extra emitido en el altar por segundo de bombeo (además del ruido del jugador).")]
        [SerializeField] float extraPumpNoise = 0.25f;

        [Header("Compuerta")]
        [SerializeField] Transform gate;
        [SerializeField] Renderer gateRenderer;
        [SerializeField] Vector3 gateOpenOffset = new Vector3(0f, 6f, 0f);
        [SerializeField] float gateOpenTime = 2.5f;
        [Tooltip("Cubo fino que marca la altura objetivo; el script lo coloca automáticamente.")]
        [SerializeField] Transform levelMarker;
        [SerializeField] Renderer levelMarkerRenderer;

        [Header("Resolución")]
        [SerializeField] bool stopEyeCycleOnSolve = true;
        [SerializeField] Transform checkpointOnSolve;
        [SerializeField] UnityEvent onSolved;

        [Header("Colores")]
        [SerializeField] Color altarIdle = new Color(0.5f, 0.5f, 0.55f);
        [SerializeField] Color altarPumping = new Color(0.2f, 0.5f, 1f);
        [SerializeField] Color altarWarning = new Color(1f, 0.85f, 0.15f);
        [SerializeField] Color altarDanger = new Color(1f, 0.1f, 0.08f);
        [SerializeField] Color altarSolved = new Color(0.2f, 1f, 0.35f);
        [SerializeField] Color waterLow = new Color(0.08f, 0.2f, 0.45f, 0.75f);
        [SerializeField] Color waterHigh = new Color(0.3f, 0.75f, 1f, 0.75f);
        [SerializeField] Color gateClosed = new Color(0.35f, 0.25f, 0.45f);
        [SerializeField] Color gateOpenColor = new Color(0.2f, 1f, 0.35f);
        [SerializeField] Color markerIdle = new Color(1f, 0.85f, 0.15f);

        public bool IsSolved { get; private set; }
        public float Level01 => gateLevel > 0f ? level / gateLevel : 0f;

        float level;
        float ratchetFloor;
        float lastPumpTime = -999f;
        float waterBottomY;
        Vector3 gateClosedPosition;
        float gateProgress;

        void Awake()
        {
            level = startLevel;
            ratchetFloor = startLevel;

            if (water != null)
                waterBottomY = water.position.y - water.localScale.y * 0.5f;

            if (levelMarker != null)
            {
                Vector3 p = levelMarker.position;
                p.y = waterBottomY + gateLevel;
                levelMarker.position = p;
            }

            if (gate != null) gateClosedPosition = gate.localPosition;

            if (player == null)
                Debug.LogError($"[{name}] Puzzle3 necesita la referencia al Player.", this);
        }

        void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;

            bool inside = !IsSolved && ProtoVisuals.Contains(altarZone, player.transform.position);
            player.SetInteractionFocus(this, 0, inside);
            bool pumping = player.IsPumpingFor(this, 0) && player.IsHolding;

            if (!IsSolved)
            {
                if (pumping)
                {
                    level += riseSpeed * dt;
                    lastPumpTime = Time.time;
                    Vector3 noisePos = altarZone != null ? altarZone.bounds.center : transform.position;
                    NoiseBus.Emit(noisePos, extraPumpNoise * dt);
                }
                else if (Time.time - lastPumpTime >= drainDelay)
                {
                    level -= drainSpeed * dt;
                }

                if (ratchetSegments > 0)
                {
                    float segment = gateLevel / ratchetSegments;
                    ratchetFloor = Mathf.Max(ratchetFloor, Mathf.Floor(level / segment) * segment);
                }

                level = Mathf.Clamp(level, Mathf.Max(startLevel, ratchetFloor), gateLevel);
                if (level >= gateLevel) Solve();
            }

            UpdateWater();
            UpdateGate(dt);
            UpdateAltar(pumping);
        }

        void Solve()
        {
            IsSolved = true;
            level = gateLevel;
            player.SetInteractionFocus(this, 0, false);

            if (stopEyeCycleOnSolve && eye != null)
            {
                eye.SetPeriodicWatch(false);
                eye.ResetToDormant();
            }

            if (checkpointOnSolve != null) player.SetCheckpoint(checkpointOnSolve);
            onSolved?.Invoke();
        }

        void UpdateWater()
        {
            if (water == null) return;

            // El oleaje del jugador agita la superficie.
            float wave = Mathf.Sin(Time.time * waveFrequency) * waveAmplitude * (0.3f + player.CurrentNoise);
            float h = Mathf.Max(0.01f, level + wave);

            Vector3 s = water.localScale;
            s.y = h;
            water.localScale = s;

            Vector3 p = water.position;
            p.y = waterBottomY + h * 0.5f;
            water.position = p;

            ProtoVisuals.SetColor(waterRenderer, Color.Lerp(waterLow, waterHigh, Level01));
        }

        void UpdateGate(float dt)
        {
            if (IsSolved)
                gateProgress = Mathf.MoveTowards(gateProgress, 1f, dt / Mathf.Max(0.01f, gateOpenTime));

            if (gate != null)
                gate.localPosition = gateClosedPosition + gateOpenOffset * Mathf.SmoothStep(0f, 1f, gateProgress);

            ProtoVisuals.SetColor(gateRenderer, Color.Lerp(gateClosed, gateOpenColor, gateProgress));

            Color marker = IsSolved
                ? altarSolved
                : Color.Lerp(markerIdle * 0.5f, markerIdle, 0.5f + 0.5f * Mathf.Sin(Time.time * 3f));
            ProtoVisuals.SetColor(levelMarkerRenderer, marker);
        }

        void UpdateAltar(bool pumping)
        {
            Color c;
            if (IsSolved) c = altarSolved;
            else if (eye != null && eye.State == EyeState.Watching)
                c = Color.Lerp(altarDanger, Color.black, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
            else if (eye != null && eye.State == EyeState.Alert) c = altarWarning;
            else if (pumping) c = Color.Lerp(altarIdle, altarPumping, 0.6f + 0.4f * Mathf.Sin(Time.time * 10f));
            else c = altarIdle;

            ProtoVisuals.SetColor(altarRenderer, c);
        }
    }
}
