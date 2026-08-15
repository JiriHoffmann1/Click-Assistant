namespace ClickAssistant.Core.Models;

public enum MouseButtonType
{
    Left,
    Right,
    Middle,
    /// <summary>Boční tlačítko "zpět" (X1) - dostupné jen na myších s 4+ tlačítky.</summary>
    Back,
    /// <summary>Boční tlačítko "vpřed" (X2) - dostupné jen na myších s 5 tlačítky.</summary>
    Forward
}
