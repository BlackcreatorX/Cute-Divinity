using System;
using UnityEngine;

namespace CuteDivinity.Prototype
{
    public enum EyeState { Dormant, Alert, Watching }

    /// <summary>
    /// Entidad vigilante (ojo cósmico).
    ///
    ///  Dormant  : párpado casi cerrado, sin luz. Acumula "sospecha" al oír ruido (NoiseBus).
    ///  Alert    : se abre a medias (amarillo), gira hacia el último ruido. Aviso para el jugador.
    ///  Watching : totalmente abierto (rojo), proyecta un cono (Spot Light) y barre la zona.
    ///             Si el jugador está en el cono con línea de visión directa (no tapado por
    ///             columnas/coberturas), se llena la barra de detección -> GetCaught().
    ///
    ///  Modo periódico (Puzzle 3): cada X segundos pasa a Alert -> Watching aunque no haya ruido.
    /// </summary>
    public class CosmicEyeWatcher : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] PlayerPressureController player;
        [Tooltip("Transform que rota para mirar (por defecto este objeto). Su eje Z+ es la mirada.")]
        [SerializeField] Transform eyePivot;
        [Tooltip("Renderer del globo ocular (se tiñe y se 'entrecierra' escalando su Y).")]
        [SerializeField] Renderer eyeRenderer;
        [SerializeField] Transform pupil;
        [SerializeField] Renderer pupilRenderer;
        [Tooltip("Spot Light hija del pivote apuntando hacia Z+. Representa el cono de visión.")]
        [SerializeField] Light visionLight;

        [Header("Oído")]
        [SerializeField] float hearingRadius = 25f;
        [SerializeField] float hearingSensitivity = 0.6f;
        [Tooltip("Sospecha que se pierde por segundo.")]
        [SerializeField] float suspicionDecay = 0.15f;
        [SerializeField] float alertThreshold = 0.3f;
        [SerializeField] float watchThreshold = 1f;
        [SerializeField] float maxSuspicion = 1.5f;

        [Header("Visión")]
        [SerializeField] float viewDistance = 20f;
        [SerializeField, Range(5f, 170f)] float viewAngle = 45f;
        [Tooltip("Capas que bloquean la visión. Quita Water, Ignore Raycast y UI.")]
        [SerializeField] LayerMask occlusionMask = ~0;
        [Tooltip("Distancia desde el centro del ojo donde empiezan los raycasts (radio del globo).")]
        [SerializeField] float rayStartOffset = 0.9f;
        [Tooltip("Segundos de visión continua necesarios para atrapar al jugador.")]
        [SerializeField] float detectionTime = 0.6f;
        [SerializeField] float seenSanityDrainPerSecond = 12f;

        [Header("Movimiento del ojo (grados/seg)")]
        [SerializeField] float dormantTurnSpeed = 20f;
        [SerializeField] float alertTurnSpeed = 60f;
        [SerializeField] float watchTurnSpeed = 45f;
        [SerializeField] float sweepAngle = 25f;
        [SerializeField] float sweepSpeed = 1.2f;

        [Header("Tiempos")]
        [SerializeField] float minAlertTime = 1.5f;
        [SerializeField] float watchDuration = 3.5f;

        [Header("Ciclo periódico (Puzzle 3)")]
        [SerializeField] bool periodicWatch;
        [Tooltip("Segundos dormido antes de abrirse por sí solo.")]
        [SerializeField] float periodicInterval = 6f;
        [Tooltip("Segundos en Alert (aviso) antes de abrir la visión.")]
        [SerializeField] float periodicWarning = 1.5f;
        [Tooltip("Hacia dónde mira en el ciclo periódico (p. ej. el altar).")]
        [SerializeField] Transform periodicLookTarget;

        [Header("Feedback visual")]
        [SerializeField] Color dormantColor = new Color(0.25f, 0.1f, 0.35f);
        [SerializeField] Color alertColor = new Color(1f, 0.85f, 0.15f);
        [SerializeField] Color watchColor = new Color(1f, 0.1f, 0.08f);
        [SerializeField] Color detectedColor = Color.white;
        [SerializeField] float alertLightIntensity = 6f;
        [SerializeField] float watchLightIntensity = 40f;
        [SerializeField] float alertPupilScale = 0.6f;
        [SerializeField] float watchPupilScale = 1.2f;

        // ---------- API pública ----------
        public EyeState State { get; private set; } = EyeState.Dormant;
        public float Suspicion01 => watchThreshold > 0f ? Mathf.Clamp01(suspicion / watchThreshold) : 0f;
        public float DetectionProgress => detection;
        public bool SeesPlayer { get; private set; }
        public event Action<EyeState> StateChanged;

        // ---------- Estado interno ----------
        float suspicion;
        float detection;
        float stateTimer;
        bool forcedCycle;
        Vector3 lastNoisePos;
        Quaternion restRotation;

        Vector3 eyeBaseScale = Vector3.one;
        Vector3 pupilBaseScale = Vector3.one;
        float lidOpen;
        float pupilFactor;
        float caughtFlash;
        Color currentColor;

        void Awake()
        {
            if (eyePivot == null) eyePivot = transform;
            restRotation = eyePivot.rotation;
            lastNoisePos = eyePivot.position + eyePivot.forward * 10f;
            if (eyeRenderer != null) eyeBaseScale = eyeRenderer.transform.localScale;
            if (pupil != null) pupilBaseScale = pupil.localScale;
            currentColor = dormantColor;

            if (visionLight != null)
            {
                visionLight.type = LightType.Spot;
                visionLight.spotAngle = viewAngle;
                visionLight.range = viewDistance;
                visionLight.intensity = 0f;
            }

            if (player == null)
                Debug.LogError($"[{name}] CosmicEyeWatcher necesita la referencia al Player.", this);
        }

        void OnEnable() => NoiseBus.NoiseEmitted += OnNoise;
        void OnDisable() => NoiseBus.NoiseEmitted -= OnNoise;

        void OnNoise(Vector3 position, float amount)
        {
            float dist = Vector3.Distance(position, eyePivot.position);
            if (dist > hearingRadius) return;

            float falloff = 1f - dist / hearingRadius;
            float added = amount * falloff * hearingSensitivity;
            suspicion = Mathf.Min(maxSuspicion, suspicion + added);
            if (added > 0.0005f) lastNoisePos = position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTimer += dt;
            suspicion = Mathf.Max(0f, suspicion - suspicionDecay * dt);

            switch (State)
            {
                case EyeState.Dormant:
                    eyePivot.rotation = Quaternion.RotateTowards(eyePivot.rotation, restRotation, dormantTurnSpeed * dt);
                    if (suspicion >= alertThreshold)
                    {
                        SetState(EyeState.Alert);
                    }
                    else if (periodicWatch && stateTimer >= periodicInterval)
                    {
                        forcedCycle = true;
                        SetState(EyeState.Alert);
                    }
                    break;

                case EyeState.Alert:
                    LookAt(CurrentLookPoint(), alertTurnSpeed, false, dt);
                    if (suspicion >= watchThreshold || (forcedCycle && stateTimer >= periodicWarning))
                        SetState(EyeState.Watching);
                    else if (!forcedCycle && stateTimer >= minAlertTime && suspicion < alertThreshold * 0.5f)
                        SetState(EyeState.Dormant);
                    break;

                case EyeState.Watching:
                    LookAt(CurrentLookPoint(), watchTurnSpeed, true, dt);
                    UpdateDetection(dt);
                    if (State == EyeState.Watching && stateTimer >= watchDuration)
                    {
                        forcedCycle = false;
                        // Baja a Alert; si el jugador sigue en silencio volverá a dormirse.
                        suspicion = Mathf.Min(suspicion, alertThreshold * 0.9f);
                        SetState(EyeState.Alert);
                    }
                    break;
            }

            if (State != EyeState.Watching)
            {
                SeesPlayer = false;
                detection = Mathf.MoveTowards(detection, 0f, dt);
            }

            UpdateVisuals(dt);
        }

        Vector3 CurrentLookPoint()
        {
            if (forcedCycle && periodicLookTarget != null) return periodicLookTarget.position;
            return lastNoisePos;
        }

        void LookAt(Vector3 point, float speed, bool sweep, float dt)
        {
            Vector3 dir = point - eyePivot.position;
            if (dir.sqrMagnitude < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            if (sweep)
                target = Quaternion.AngleAxis(Mathf.Sin(stateTimer * sweepSpeed) * sweepAngle, Vector3.up) * target;

            eyePivot.rotation = Quaternion.RotateTowards(eyePivot.rotation, target, speed * dt);
        }

        // ================== Visión / detección ==================

        void UpdateDetection(float dt)
        {
            SeesPlayer = player != null && CanSeePlayer();

            if (SeesPlayer)
            {
                detection += dt / Mathf.Max(0.01f, detectionTime);
                player.NotifyWatched(seenSanityDrainPerSecond * dt);
                lastNoisePos = player.transform.position; // se "engancha" al jugador visto
            }
            else
            {
                detection = Mathf.MoveTowards(detection, 0f, dt / Mathf.Max(0.01f, detectionTime) * 0.5f);
            }

            if (detection >= 1f)
            {
                caughtFlash = 1f;
                player.GetCaught();
                ResetToDormant();
            }
        }

        bool CanSeePlayer()
        {
            Vector3 origin = eyePivot.position;
            Vector3 toPlayer = player.CenterPosition - origin;

            if (toPlayer.magnitude > viewDistance) return false;
            if (Vector3.Angle(eyePivot.forward, toPlayer) > viewAngle * 0.5f) return false;

            // Visible si el centro O la cabeza tienen línea de visión directa.
            return HasLineOfSight(origin, player.CenterPosition) || HasLineOfSight(origin, player.HeadPosition);
        }

        bool HasLineOfSight(Vector3 origin, Vector3 target)
        {
            Vector3 dir = target - origin;
            float length = dir.magnitude;
            if (length <= rayStartOffset) return true;
            dir /= length;

            Vector3 start = origin + dir * rayStartOffset;
            if (Physics.Raycast(start, dir, out RaycastHit hit, length - rayStartOffset, occlusionMask, QueryTriggerInteraction.Ignore))
            {
                // Si lo primero que golpea es el propio jugador, lo ve.
                return hit.transform == player.transform || hit.transform.IsChildOf(player.transform);
            }
            return true;
        }

        // ================== API para puzzles ==================

        public void ForceAlert(Vector3 position)
        {
            lastNoisePos = position;
            suspicion = Mathf.Max(suspicion, alertThreshold);
            if (State == EyeState.Dormant) SetState(EyeState.Alert);
        }

        public void ForceWatch(Vector3 position)
        {
            lastNoisePos = position;
            suspicion = Mathf.Max(suspicion, watchThreshold);
            if (State != EyeState.Watching) SetState(EyeState.Watching);
        }

        public void ResetToDormant()
        {
            suspicion = 0f;
            detection = 0f;
            forcedCycle = false;
            SeesPlayer = false;
            SetState(EyeState.Dormant);
        }

        public void SetPeriodicWatch(bool enabled)
        {
            periodicWatch = enabled;
            if (!enabled) forcedCycle = false;
        }

        void SetState(EyeState newState)
        {
            State = newState;
            stateTimer = 0f;
            StateChanged?.Invoke(newState);
        }

        // ================== Feedback visual ==================

        void UpdateVisuals(float dt)
        {
            Color targetColor;
            float lidTarget, pupilTarget, lightTarget;

            switch (State)
            {
                case EyeState.Alert:
                    targetColor = alertColor;
                    lidTarget = 0.6f;
                    pupilTarget = alertPupilScale;
                    lightTarget = alertLightIntensity;
                    break;
                case EyeState.Watching:
                    targetColor = Color.Lerp(watchColor, detectedColor, detection);
                    lidTarget = 1f;
                    pupilTarget = watchPupilScale * (1f + 0.1f * Mathf.Sin(Time.time * 12f));
                    lightTarget = watchLightIntensity * (1f + detection);
                    break;
                default:
                    targetColor = dormantColor;
                    lidTarget = 0.12f;
                    pupilTarget = 0f;
                    lightTarget = 0f;
                    break;
            }

            if (caughtFlash > 0f)
            {
                targetColor = Color.Lerp(targetColor, detectedColor, caughtFlash);
                caughtFlash = Mathf.MoveTowards(caughtFlash, 0f, dt * 1.5f);
            }

            currentColor = Color.Lerp(currentColor, targetColor, dt * 8f);
            lidOpen = Mathf.MoveTowards(lidOpen, lidTarget, dt * 3f);
            pupilFactor = Mathf.MoveTowards(pupilFactor, pupilTarget, dt * 4f);

            if (eyeRenderer != null)
            {
                ProtoVisuals.SetColor(eyeRenderer, currentColor);
                eyeRenderer.transform.localScale = new Vector3(eyeBaseScale.x, eyeBaseScale.y * Mathf.Lerp(0.12f, 1f, lidOpen), eyeBaseScale.z);
            }

            if (pupil != null)
                pupil.localScale = pupilBaseScale * Mathf.Max(0.001f, pupilFactor);
            ProtoVisuals.SetColor(pupilRenderer, Color.black);

            if (visionLight != null)
            {
                visionLight.intensity = Mathf.MoveTowards(visionLight.intensity, lightTarget, dt * watchLightIntensity * 2f);
                visionLight.color = currentColor;
                visionLight.enabled = visionLight.intensity > 0.01f;
            }
        }

        void OnDrawGizmosSelected()
        {
            Transform pivot = eyePivot != null ? eyePivot : transform;
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(pivot.position, hearingRadius);

            Gizmos.color = Color.red;
            float half = viewAngle * 0.5f;
            Vector3[] dirs =
            {
                Quaternion.AngleAxis(half, pivot.up) * pivot.forward,
                Quaternion.AngleAxis(-half, pivot.up) * pivot.forward,
                Quaternion.AngleAxis(half, pivot.right) * pivot.forward,
                Quaternion.AngleAxis(-half, pivot.right) * pivot.forward,
            };
            foreach (Vector3 d in dirs) Gizmos.DrawRay(pivot.position, d * viewDistance);
        }
    }
}
