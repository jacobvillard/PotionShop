/// <summary>
/// An object that can be secured to a cooking tray.
/// </summary>
public interface ICookable
{
    bool IsOnTray { get; }
    bool CanBePlacedOnTray { get; }
    bool PlaceOnTray(Tray tray);
    void RemoveFromTray();
}
