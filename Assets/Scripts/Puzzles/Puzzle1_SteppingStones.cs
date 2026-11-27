using UnityEngine;
using UnityEngine.Events;

namespace CuteDivinity.Prototype
{
    /// <summary>
    /// PUZZLE 1 — Piedras sobre el agua.
    ///
    /// Cada piedra acumula "estrés" (0..1):
    ///  - Cada Tap estando encima suma poco (la piedra tiembla un poco, gris -> amarillo).
    ///  - Mantener (Hold / correr) encima suma muchísimo por segundo; aterrizar corriendo suma extra.
    ///  - Con estrés 1 la piedra se hunde (rojo), hace "splash" (ruido fuerte) y el ojo pasa a Watching.
    ///    El jugador cae al agua -> killY -> respawn.
    ///  - Las piedras sin jugador encima se recuperan; las hundidas reaparecen tras resetDelay.
    /// Llegar a goalZone resuelve el puzzle.
    /// </summary>
    public class Puzzle1_SteppingStones : MonoBehaviour
    {
        class StoneState
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 restPosition;
            public float stress;
            public bool sunk;
            public float sunkTime;
            public float sinkOffset;
        }

        [Header("Referencias")]
        [SerializeField] PlayerPressureController player;
        [Tooltip("Opcional: el ojo que reacciona cuando una piedra se hunde.")]
        [SerializeField] CosmicEyeWatcher eye;
        [Tooltip("Las piedras (Cylinders con collider sólido), en cualquier orden.")]
        [SerializeField] Transform[] stones;

        [Header("Reglas de estrés")]
        [SerializeField] float tapStress = 0.1f;
        [SerializeField] float holdStressPerSecond = 2.5f;
        [Tooltip("Estrés extra al pisar una piedra mientras se mantiene el botón.")]
        [SerializeField] float holdLandingStress = 0.5f;
        [SerializeField] float recoverPerSecond = 0.35f;
        [SerializeField] float splashNoise = 1.5f;
        [SerializeField] bool forceEyeWatchOnSink = true;

        [Header("Hundimiento")]
        [SerializeField] float wobbleDepth = 0.12f;
        [SerializeField] float shakeAmplitude = 0.03f;
        [SerializeField] float sinkDepth = 2.5f;
        [SerializeField] float sinkSpeed = 4f;
        [SerializeField] float riseSpeed = 1.2f;
        [SerializeField] float resetDelay = 2.5f;

        [Header("Meta")]
        [Tooltip("BoxCollider (Is Trigger) al final del camino.")]
        [SerializeField] Collider goalZone;
        [SerializeField] Renderer goalRenderer;
        [SerializeField] Transform checkpointOnSolve;
        [SerializeField] UnityEvent onSolved;

        [Header("Colores")]
        [SerializeField] Color stoneCalm = new Color(0.55f, 0.55f, 0.6f);
        [SerializeField] Color stoneStressed = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color stoneSunk = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] Color goalIdle = new Color(0.8f, 0.8f, 0.9f);
        [SerializeField] Color goalSolved = new Color(0.2f, 1f, 0.35f);

        public bool IsSolved { get; private set; }

        StoneState[] states;
        int currentStone = -1;

        void Awake()
        {
            states = new StoneState[stones != null ? stones.Length : 0];
            for (int i = 0; i < states.Length; i++)
            {
                Transform t = stones[i];
                states[i] = new StoneState
                {
                    transform = t,
                    renderer = t != null ? t.GetComponentInChildren<Renderer>() : null,
                    restPosition = t != null ? t.position : Vector3.zero,
                };
            }

            if (player == null)
                Debug.LogError($"[{name}] Puzzle1 necesita la referencia al Player.", this);
        }

        void OnEnable()
        {
            if (player != null) player.PressReleased += OnPressReleased;
        }

        void OnDisable()
        {
            if (player != null) player.PressReleased -= OnPressReleased;
        }

        void OnPressReleased(PressInfo info)
        {
            if (info.WasInteraction || info.Type != PressType.Tap) return;
            if (currentStone >= 0) AddStress(currentStone, tapStress);
        }

        void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            bool running = player.IsHolding && !player.IsPumping;

            // ¿Sobre qué piedra está el jugador?
            int newStone = FindStoneUnderPlayer();
            if (newStone != currentStone)
            {
                if (newStone >= 0 && running) AddStress(newStone, holdLandingStress);
                currentStone = newStone;
            }
            if (currentStone >= 0 && running) AddStress(currentStone, holdStressPerSecond * dt);

            for (int i = 0; i < states.Length; i++)
                UpdateStone(i, states[i], dt);

            if (goalRenderer != null)
            {
                Color c = IsSolved ? goalSolved : Color.Lerp(goalIdle * 0.6f, goalIdle, 0.5f + 0.5f * Mathf.Sin(Time.time * 2f));
                ProtoVisuals.SetColor(goalRenderer, c);
            }

            if (!IsSolved && ProtoVisuals.Contains(goalZone, player.transform.position))
                Solve();
        }

        void UpdateStone(int index, StoneState s, float dt)
        {
            if (s.transform == null) return;

            if (s.sunk)
            {
                s.sinkOffset = Mathf.MoveTowards(s.sinkOffset, sinkDepth, sinkSpeed * dt);
                if (Time.time - s.sunkTime >= resetDelay && index != currentStone)
                {
                    s.sunk = false;
                    s.stress = 0f;
                }
            }
            else
            {
                s.sinkOffset = Mathf.MoveTowards(s.sinkOffset, 0f, riseSpeed * dt);
                if (index != currentStone)
                    s.stress = Mathf.MoveTowards(s.stress, 0f, recoverPerSecond * dt);
            }

            // Temblor proporcional al estrés: feedback in-world de "cuidado".
            float shake = s.sunk ? 0f : Mathf.Sin(Time.time * 40f + index) * shakeAmplitude * s.stress;
            Vector3 offset = new Vector3(shake, -(s.sinkOffset + s.stress * wobbleDepth), 0f);
            s.transform.position = s.restPosition + offset;

            Color c = s.sunk ? stoneSunk : Color.Lerp(stoneCalm, stoneStressed, s.stress);
            ProtoVisuals.SetColor(s.renderer, c);
        }

        int FindStoneUnderPlayer()
        {
            Collider ground = player.GroundCollider;
            if (ground == null) return -1;

            for (int i = 0; i < states.Length; i++)
            {
                Transform t = states[i].transform;
                if (t != null && (ground.transform == t || ground.transform.IsChildOf(t)))
                    return i;
            }
            return -1;
        }

        void AddStress(int index, float amount)
        {
            StoneState s = states[index];
            if (s.sunk) return;

            s.stress = Mathf.Min(1f, s.stress + amount);
            if (s.stress >= 1f) Sink(s);
        }

        void Sink(StoneState s)
        {
            s.sunk = true;
            s.sunkTime = Time.time;
            s.stress = 1f;

            NoiseBus.Emit(s.transform.position, splashNoise);
            if (forceEyeWatchOnSink && eye != null) eye.ForceWatch(s.transform.position);
        }

        void Solve()
        {
            IsSolved = true;
            if (checkpointOnSolve != null) player.SetCheckpoint(checkpointOnSolve);
            onSolved?.Invoke();
        }
    }
}
