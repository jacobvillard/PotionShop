using UnityEngine;

public class Staff : MonoBehaviour, IUseable
{
    [SerializeField] private float cooldown = 0.5f;

    private float nextUseTime;

    public void Use()
    {
        if (Time.time < nextUseTime)
            return;

        nextUseTime = Time.time + cooldown;

        Debug.Log("Staff used");

        // Lightning blast later
    }
}