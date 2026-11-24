using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Input Actions (Unity 6 Input System)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference aimAction;
    [SerializeField] private InputActionReference interactAction;

    [Header("Ajustes de Movimiento")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 5.2f;
    [SerializeField] private float aimWalkSpeed = 1.6f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float gravity = -19.62f;

    [Header("Ajustes de Cámara (Sobre el Hombro)")]
    [SerializeField] private Transform cameraPivot; // Empty hijo del Player a la altura de la cabeza
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 65f;
    
    [Space(5)]
    [SerializeField] private Vector3 normalOffset = new Vector3(0.55f, 0.2f, -2.4f);
    [SerializeField] private Vector3 aimOffset = new Vector3(0.65f, 0.15f, -1.2f);
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float aimFOV = 42f;
    [SerializeField] private float aimTransitionSpeed = 10f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private float cameraRadius = 0.2f;

    [Header("Sistema de Interacción")]
    [SerializeField] private float interactDistance = 2.5f;
    [SerializeField] private LayerMask interactLayer;
    
    // Estado público útil para Animator o UI
    public bool IsAiming { get; private set; }
    public bool IsSprinting { get; private set; }
    public IInteractable CurrentInteractable { get; private set; }

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float cameraYaw;
    private float cameraPitch;
    private Vector3 currentCamOffset;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentCamOffset = normalOffset;

        if (playerCamera == null)
            playerCamera = Camera.main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
        lookAction?.action.Enable();
        sprintAction?.action.Enable();
        aimAction?.action.Enable();
        
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        lookAction?.action.Disable();
        sprintAction?.action.Disable();
        aimAction?.action.Disable();

        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
            interactAction.action.Disable();
        }
    }

    private void Update()
    {
        ReadStates();
        HandleMovement();
        CheckForInteractable();
    }

    private void LateUpdate()
    {
        HandleCamera();
    }

    private void ReadStates()
    {
        IsAiming = aimAction != null && aimAction.action.IsPressed();
        IsSprinting = !IsAiming && sprintAction != null && sprintAction.action.IsPressed();
    }

    private void HandleMovement()
    {
        Vector2 input = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        // Dirección relativa a la cámara
        Vector3 camForward = Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.forward;
        Vector3 camRight = Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.right;
        Vector3 moveDir = (camForward * input.y + camRight * input.x).normalized;

        float currentSpeed = IsAiming ? aimWalkSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

        // Rotación del personaje
        if (IsAiming)
        {
            Quaternion targetRot = Quaternion.Euler(0f, cameraYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * 1.5f * Time.deltaTime);
        }
        else if (moveDir.sqrMagnitude > 0.01f)
        {
            // Al caminar/correr libre, rota hacia la dirección del movimiento
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }
        verticalVelocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = (moveDir * currentSpeed) + verticalVelocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    private void HandleCamera()
    {
        if (cameraPivot == null || playerCamera == null) return;

        Vector2 lookInput = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;

        cameraYaw += lookInput.x * mouseSensitivity;
        cameraPitch -= lookInput.y * mouseSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);

        cameraPivot.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);

        // Transición suave entre cámara normal y apuntado
        Vector3 targetOffset = IsAiming ? aimOffset : normalOffset;
        currentCamOffset = Vector3.Lerp(currentCamOffset, targetOffset, aimTransitionSpeed * Time.deltaTime);

        float targetFOV = IsAiming ? aimFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, aimTransitionSpeed * Time.deltaTime);

        // Posición deseada y prevención de colisiones con paredes
        Vector3 desiredWorldPos = cameraPivot.TransformPoint(currentCamOffset);
        Vector3 direction = desiredWorldPos - cameraPivot.position;
        float distance = direction.magnitude;

        if (Physics.SphereCast(cameraPivot.position, cameraRadius, direction.normalized, out RaycastHit hit, distance, cameraCollisionMask))
        {
            playerCamera.transform.position = cameraPivot.position + direction.normalized * Mathf.Max(hit.distance - 0.1f, 0.3f);
        }
        else
        {
            playerCamera.transform.position = desiredWorldPos;
        }

        playerCamera.transform.LookAt(cameraPivot.position + cameraPivot.forward * 10f);
    }

    private void CheckForInteractable()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance + Mathf.Abs(currentCamOffset.z), interactLayer))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
            {
                CurrentInteractable = interactable;
                return;
            }
        }

        CurrentInteractable = null;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (CurrentInteractable != null)
        {
            CurrentInteractable.Interact(this);
        }
    }
}