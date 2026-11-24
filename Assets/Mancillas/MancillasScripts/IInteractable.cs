public interface IInteractable
{
    string InteractionPrompt { get; }
    void Interact(ThirdPersonController player);
}