namespace ClickAssistant.Core.Engine;

public interface IMouseInfoProvider
{
    /// <summary>Počet tlačítek, které OS hlásí pro aktuálně připojenou myš (typicky 2-5). Použito k tomu,
    /// aby appka v editoru kroku nabízela jen tlačítka, která myš skutečně má.</summary>
    int GetButtonCount();
}
