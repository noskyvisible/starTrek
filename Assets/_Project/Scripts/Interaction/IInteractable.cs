namespace StarTrek.Interaction
{
    /// <summary>Anything the player can look at and use: consoles, buttons, doors, pickups.</summary>
    public interface IInteractable
    {
        /// <summary>Short verb phrase shown on the HUD, e.g. "Red Alert" or "Turbolift offline".</summary>
        string Prompt { get; }

        /// <summary>False hides the prompt and ignores presses (e.g. a button on cooldown).</summary>
        bool CanInteract { get; }

        void Interact(Interactor interactor);
    }
}
