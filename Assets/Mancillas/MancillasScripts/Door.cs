using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [Tooltip("Place this component on a transform located at the door hinge.")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationDuration = 0.6f;
    [SerializeField] private bool startsOpen;

    public string InteractionPrompt => isOpen ? "Cerrar puerta" : "Abrir puerta";

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;
    private Coroutine rotationCoroutine;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        isOpen = startsOpen;

        if (isOpen)
        {
            transform.localRotation = openRotation;
        }
    }

    public void Interact(ThirdPersonController player)
    {
        isOpen = !isOpen;

        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
        }

        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        if (rotationDuration <= 0f)
        {
            transform.localRotation = targetRotation;
            rotationCoroutine = null;
            return;
        }

        rotationCoroutine = StartCoroutine(RotateTo(targetRotation));
    }

    private IEnumerator RotateTo(Quaternion targetRotation)
    {
        Quaternion startRotation = transform.localRotation;
        float elapsed = 0f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                Mathf.Clamp01(elapsed / rotationDuration)
            );
            yield return null;
        }

        transform.localRotation = targetRotation;
        rotationCoroutine = null;
    }
}
