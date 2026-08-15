namespace ClickAssistant.Core.Models;

public enum StepActionType
{
    MouseClick,
    KeyPress,
    /// <summary>Přesune kurzor na souřadnice bodu (stejně jako MouseClick) a tam stiskne klávesu místo
    /// kliknutí - pro UI, které reaguje na hover (vyžaduje, aby kurzor byl skutečně nad daným místem).</summary>
    KeyPressAtPosition
}
