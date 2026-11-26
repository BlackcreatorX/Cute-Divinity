using UnityEngine.InputSystem;
using UnityEngine;

public class ScreamerTrigger : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            PlayScreamer("1");
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            PlayScreamer("2");
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            PlayScreamer("3");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayScreamer("1");
        }
    }

    private void PlayScreamer(string screamerID)
    {
        if (ScreamerManager.Instance == null)
        {
            Debug.LogWarning(
                $"Cannot play screamer '{screamerID}': no ScreamerManager is active.",
                this
            );
            return;
        }

        ScreamerManager.Instance.PlayScreamer(screamerID);
    }
}
