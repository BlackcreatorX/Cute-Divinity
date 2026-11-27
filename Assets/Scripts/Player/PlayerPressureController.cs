using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace CuteDivinity.Prototype
{
    public enum PressType { None, Tap, Hold }

    /// <summary>Datos de una pulsación finalizada (se envían en PlayerPressureController.PressReleased).</summary>
    public readonly struct PressInfo
    {
        public readonly float Duration;
        public readonly PressType Type;
        /// <summary>Interactuable que "capturó" la pulsación (pilar, altar) o null si fue movimiento.</summary>
        public readonly Object Owner;
        /// <summary>Índice dentro del interactuable (p. ej. qué pilar de los 3).</summary>
        public readonly int Slot;

        public PressInfo(float duration, PressType type, Object owner, int slot)
        {
            Duration = duration;
            Type = type;
            Owner = owner;
            Slot = slot;
        }

        public bool WasInteraction => Owner != null;
    }

    /// <summary>
    /// Controlador del jugador basado en la DURACIÓN de pulsación de un único botón.
    ///
    ///  - Botón "Presión": Espacio / Click izquierdo / Gamepad Sur (A / X).
    ///  - Dirección: WASD / Flechas / Stick izquierdo (solo orienta; sin botón no hay desplazamiento).
    ///
    ///  Pulsación &lt; holdThreshold  (Tap):  paso sigiloso, ruido mínimo, no drena cordura.
    ///  Pulsación &gt;= holdThreshold (Hold): carrera / bombeo, mucho ruido, drena cordura.
    ///
    ///  Si el jugador está dentro de la zona de un interactuable (pilar/altar) y NO está
    ///  pulsando dirección al empezar la pulsación, la pulsación se convierte en "bombeo":
    ///  el personaje se queda quieto y el interactuable lee PressDuration / IsHolding.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerPressureController : MonoBehaviour
    {
        enum PressPhase { Idle, Pending, Holding }

        [Header("Pulsación")]
        [Tooltip("Segundos a partir de los cuales una pulsación pasa de Tap a Hold.")]
        [SerializeField, Min(0.05f)] float holdThreshold = 0.25f;

        [Header("Movimiento")]
        [Tooltip("Velocidad mientras la pulsación aún es corta (y durante el deslizamiento tras un Tap).")]
        [SerializeField] float sneakSpeed = 1.6f;
        [Tooltip("Tiempo que el personaje sigue avanzando sigilosamente tras soltar un Tap.")]
        [SerializeField] float tapGlideTime = 0.18f;
        [Tooltip("Velocidad al mantener (Hold).")]
        [SerializeField] float runSpeed = 5.5f;
        [SerializeField] float acceleration = 25f;
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float gravity = -20f;
        [Tooltip("Cámara para movimiento relativo. Si está vacío usa Camera.main.")]
        [SerializeField] Transform cameraTransform;

        [Header("Ruido / oleaje")]
        [SerializeField] float sneakNoise = 0.05f;
        [SerializeField] float holdNoise = 1f;
        [Tooltip("Ruido al bombear (Hold dentro de un pilar/altar).")]
        [SerializeField] float pumpNoise = 0.8f;
        [SerializeField] float noiseRiseRate = 3f;
        [SerializeField] float noiseFallRate = 2f;

        [Header("Cordura (Sanity)")]
        [SerializeField] float maxSanity = 100f;
        [Tooltip("Cordura perdida por segundo mientras se mantiene el botón (Hold).")]
        [SerializeField] float holdSanityDrain = 6f;
        [SerializeField] float regenRate = 4f;
        [Tooltip("Segundos sin estrés antes de empezar a regenerar.")]
        [SerializeField] float regenDelay = 1.5f;
        [Tooltip("Cordura perdida al ser atrapado por el ojo.")]
        [SerializeField] float caughtPenalty = 35f;
        [Tooltip("Cordura con la que reapareces si llegas a 0.")]
        [SerializeField] float respawnSanity = 60f;

        [Header("Respawn")]
        [SerializeField] Transform checkpoint;
        [Tooltip("Si el jugador cae por debajo de esta altura (al agua), reaparece.")]
        [SerializeField] float killY = -1.5f;

        [Header("Feedback visual (sin texto)")]
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Color calmColor = new Color(1f, 0.62f, 0.82f);
        [SerializeField] Color insaneColor = new Color(0.22f, 0.18f, 0.28f);
        [SerializeField] Color seenColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("Esfera pequeña sobre la cabeza: crece mientras pulsas. Cian = Tap, Naranja = Hold.")]
        [SerializeField] Transform chargeIndicator;
        [SerializeField] Renderer chargeRenderer;
        [SerializeField] Color tapChargeColor = new Color(0.4f, 0.95f, 1f);
        [SerializeField] Color holdChargeColor = new Color(1f, 0.5f, 0.1f);
        [SerializeField] float chargeMaxScale = 0.35f;
        [Tooltip("Disco plano a los pies: su radio = ruido/oleaje actual.")]
        [SerializeField] Transform wakeDisc;
        [SerializeField] float wakeMaxScale = 3f;
        [Tooltip("Controla la densidad de la niebla global según la cordura.")]
        [SerializeField] bool driveFog = true;
        [SerializeField] float fogDensityCalm = 0.01f;
        [SerializeField] float fogDensityInsane = 0.09f;

        // ---------- API pública ----------
        public float Sanity { get; private set; }
        public float Sanity01 => maxSanity > 0f ? Sanity / maxSanity : 0f;
        /// <summary>Ruido/oleaje actual 0..1 (aprox.).</summary>
        public float CurrentNoise { get; private set; }
        public bool IsPressed => phase != PressPhase.Idle;
        public bool IsHolding => phase == PressPhase.Holding;
        public float PressDuration => IsPressed ? Time.time - pressStartTime : 0f;
        public float HoldThreshold => holdThreshold;
        /// <summary>True si la pulsación actual está capturada por un interactuable (pilar/altar).</summary>
        public bool IsPumping => IsPressed && pressOwner != null;
        /// <summary>Collider sobre el que está apoyado (null si está en el aire).</summary>
        public Collider GroundCollider { get; private set; }
        public Vector3 CenterPosition => transform.TransformPoint(controller.center);
        public Vector3 HeadPosition => transform.TransformPoint(controller.center + Vector3.up * (controller.height * 0.5f - controller.radius * 0.5f));

        public event Action<PressInfo> PressReleased;
        public event Action HoldStarted;
        public event Action Respawned;
        public event Action Caught;

        // ---------- Estado interno ----------
        CharacterController controller;
        InputAction pressAction;
        InputAction moveAction;
        Vector2 moveInput;

        PressPhase phase = PressPhase.Idle;
        float pressStartTime;
        Object pressOwner;
        int pressSlot = -1;
        Object focusOwner;
        int focusSlot = -1;

        float glideUntil;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        Collider frameGround;

        float lastStressTime = -999f;
        float seenFlash;
        float chargeScale;
        Vector3 wakeBaseScale = Vector3.one;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            Sanity = maxSanity;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            if (wakeDisc != null) wakeBaseScale = wakeDisc.localScale;
            CreateInputActions();

            if (driveFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
            }
        }

        void CreateInputActions()
        {
            pressAction = new InputAction("Pressure", InputActionType.Button);
            pressAction.AddBinding("<Keyboard>/space");
            pressAction.AddBinding("<Mouse>/leftButton");
            pressAction.AddBinding("<Gamepad>/buttonSouth");

            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");
        }

        void OnEnable()
        {
            pressAction.Enable();
            moveAction.Enable();
        }

        void OnDisable()
        {
            pressAction.Disable();
            moveAction.Disable();
        }

        void OnDestroy()
        {
            pressAction?.Dispose();
            moveAction?.Dispose();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            moveInput = moveAction.ReadValue<Vector2>();

            UpdatePress();
            UpdateMovement(dt);
            UpdateNoise(dt);
            UpdateSanity(dt);
            UpdateVisuals(dt);

            if (transform.position.y < killY) Respawn();
        }

        // ================== Pulsación (Tap vs Hold) ==================

        void UpdatePress()
        {
            if (phase == PressPhase.Idle && pressAction.WasPressedThisFrame())
            {
                phase = PressPhase.Pending;
                pressStartTime = Time.time;
                // La pulsación pertenece al interactuable solo si el jugador no intenta moverse.
                bool wantsToMove = moveInput.sqrMagnitude > 0.04f;
                pressOwner = (focusOwner != null && !wantsToMove) ? focusOwner : null;
                pressSlot = pressOwner != null ? focusSlot : -1;
            }

            if (phase == PressPhase.Pending && Time.time - pressStartTime >= holdThreshold)
            {
                phase = PressPhase.Holding;
                HoldStarted?.Invoke();
            }

            if (phase != PressPhase.Idle && (pressAction.WasReleasedThisFrame() || !pressAction.IsPressed()))
            {
                float duration = Time.time - pressStartTime;
                PressType type = phase == PressPhase.Holding ? PressType.Hold : PressType.Tap;
                var info = new PressInfo(duration, type, pressOwner, pressSlot);
                bool wasInteraction = pressOwner != null;

                phase = PressPhase.Idle;
                pressOwner = null;
                pressSlot = -1;

                if (type == PressType.Tap && !wasInteraction)
                    glideUntil = Time.time + tapGlideTime;

                PressReleased?.Invoke(info);
            }
        }

        // ================== Movimiento ==================

        void UpdateMovement(float dt)
        {
            Vector3 wishDir = CameraRelative(moveInput);
            bool hasDir = wishDir.sqrMagnitude > 0.0025f;
            bool pumping = IsPumping;

            if (hasDir && !pumping)
            {
                Quaternion targetRot = Quaternion.LookRotation(wishDir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * dt);
            }

            float targetSpeed = 0f;
            if (!pumping)
            {
                if (phase == PressPhase.Holding) targetSpeed = runSpeed;
                else if (phase == PressPhase.Pending || Time.time < glideUntil) targetSpeed = sneakSpeed;
            }

            Vector3 moveDir = hasDir ? wishDir.normalized : transform.forward;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, moveDir * targetSpeed, acceleration * dt);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity += gravity * dt;

            frameGround = null;
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            GroundCollider = controller.isGrounded ? frameGround : null;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f) frameGround = hit.collider;
        }

        Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0025f) return Vector3.zero;

            Transform cam = cameraTransform != null ? cameraTransform
                          : (Camera.main != null ? Camera.main.transform : null);
            Vector3 forward = cam != null ? cam.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        // ================== Ruido ==================

        void UpdateNoise(float dt)
        {
            float target = 0f;
            if (phase == PressPhase.Holding) target = IsPumping ? pumpNoise : holdNoise;
            else if (phase == PressPhase.Pending || Time.time < glideUntil) target = sneakNoise;

            float rate = target > CurrentNoise ? noiseRiseRate : noiseFallRate;
            CurrentNoise = Mathf.MoveTowards(CurrentNoise, target, rate * dt);

            if (CurrentNoise > 0.001f) NoiseBus.Emit(transform.position, CurrentNoise * dt);
        }

        // ================== Cordura ==================

        void UpdateSanity(float dt)
        {
            if (phase == PressPhase.Holding)
            {
                Sanity -= holdSanityDrain * dt;
                lastStressTime = Time.time;
            }
            else if (Time.time - lastStressTime >= regenDelay)
            {
                Sanity += regenRate * dt;
            }

            Sanity = Mathf.Clamp(Sanity, 0f, maxSanity);
            if (Sanity <= 0f)
            {
                Sanity = respawnSanity;
                Respawn();
            }
        }

        /// <summary>Llamado por el ojo cada frame que ve al jugador.</summary>
        public void NotifyWatched(float sanityDamage)
        {
            Sanity = Mathf.Max(0f, Sanity - sanityDamage);
            lastStressTime = Time.time;
            seenFlash = 1f;
        }

        /// <summary>Llamado por el ojo cuando la detección se completa.</summary>
        public void GetCaught()
        {
            Sanity -= caughtPenalty;
            if (Sanity <= 0f) Sanity = respawnSanity;
            Caught?.Invoke();
            Respawn();
        }

        // ================== Interactuables / Checkpoints ==================

        /// <summary>
        /// Los puzzles llaman a esto cada frame indicando si el jugador está en su zona.
        /// Solo limpia el foco si pertenece al mismo owner/slot (varios pilares conviven).
        /// </summary>
        public void SetInteractionFocus(Object owner, int slot, bool inside)
        {
            if (inside)
            {
                focusOwner = owner;
                focusSlot = slot;
            }
            else if (focusOwner == owner && focusSlot == slot)
            {
                focusOwner = null;
                focusSlot = -1;
            }
        }

        /// <summary>¿Está la pulsación actual capturada por este owner (y slot, si slot &gt;= 0)?</summary>
        public bool IsPumpingFor(Object owner, int slot = -1)
        {
            return IsPumping && pressOwner == owner && (slot < 0 || slot == pressSlot);
        }

        public void SetCheckpoint(Transform newCheckpoint)
        {
            if (newCheckpoint != null) checkpoint = newCheckpoint;
        }

        public void Respawn()
        {
            phase = PressPhase.Idle;
            pressOwner = null;
            pressSlot = -1;
            glideUntil = 0f;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            CurrentNoise = 0f;

            Vector3 pos = checkpoint != null ? checkpoint.position : spawnPosition;
            Quaternion rot = checkpoint != null ? checkpoint.rotation : spawnRotation;

            // El CharacterController sobrescribe la posición si está activo: se desactiva para teletransportar.
            controller.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            controller.enabled = true;

            Respawned?.Invoke();
        }

        // ================== Feedback visual ==================

        void UpdateVisuals(float dt)
        {
            // Cuerpo: rosa "cute" -> gris oscuro según cordura; parpadeo con cordura baja; rojo al ser visto.
            Color body = Color.Lerp(insaneColor, calmColor, Sanity01);
            if (Sanity01 < 0.3f)
                body = Color.Lerp(body, insaneColor, Mathf.PerlinNoise(Time.time * 8f, 0f) * 0.7f);
            body = Color.Lerp(body, seenColor, seenFlash);
            seenFlash = Mathf.MoveTowards(seenFlash, 0f, dt * 3f);
            ProtoVisuals.SetColor(bodyRenderer, body);

            // Indicador de carga: crece hasta el umbral; al pasar a Hold cambia de color y late.
            if (chargeIndicator != null)
            {
                float target = 0f;
                if (IsPressed)
                {
                    target = Mathf.Clamp01(PressDuration / holdThreshold) * chargeMaxScale;
                    if (IsHolding) target *= 1f + 0.15f * Mathf.Sin(Time.time * 20f);
                }
                chargeScale = Mathf.MoveTowards(chargeScale, target, dt * 4f);
                chargeIndicator.localScale = Vector3.one * Mathf.Max(0.001f, chargeScale);
                ProtoVisuals.SetColor(chargeRenderer, IsHolding ? holdChargeColor : tapChargeColor);
            }

            // Estela/oleaje: radio proporcional al ruido.
            if (wakeDisc != null)
            {
                float s = Mathf.Max(0.001f, CurrentNoise * wakeMaxScale);
                wakeDisc.localScale = new Vector3(s, wakeBaseScale.y, s);
            }

            if (driveFog)
                RenderSettings.fogDensity = Mathf.Lerp(fogDensityInsane, fogDensityCalm, Sanity01);
        }
    }
}
