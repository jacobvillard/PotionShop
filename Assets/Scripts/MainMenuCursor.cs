using UnityEngine;

/// <summary>Applies the menu's custom cursor while this scene is active.</summary>
public class MainMenuCursor : MonoBehaviour
{
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Vector2 hotspot = Vector2.zero;
    [Tooltip("Disable this when the component lives on the shared gameplay Canvas. Menus then call SetCursorActive.")]
    [SerializeField] private bool applyOnEnable = true;

    private void OnEnable()
    {
        if (applyOnEnable)
            SetCursorActive(true);
    }

    /// <summary>Lets every gameplay menu use the cursor service on the root Canvas.</summary>
    public void SetCursorActive(bool active)
    {
        if (!active)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            return;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }

    private void OnDisable()
    {
        if (applyOnEnable)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
