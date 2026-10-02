using System.Collections;
using UnityEngine;

public class DoorInteraction : MonoBehaviour, IInteractable
{
    [Header("Door")]
    [SerializeField] private Transform doorTransform;

    [Header("Rotation")]
    [SerializeField] private Vector3 closedRotation;
    [SerializeField] private Vector3 openRotation = new Vector3(0f, 90f, 0f);

    [Header("Animation")]
    [SerializeField] private float animationDuration = 1f;
    
    [Header("Sounds")]
    [SerializeField] AudioClip openSound;
    [SerializeField] AudioClip closeSound;
    [SerializeField] float volume = 1f;


    [SerializeField]
    private AnimationCurve animationCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool isOpen;
    private bool isAnimating;

    public void Interact()
    {
        
        if (isAnimating)
            return;
        
        if(!isOpen)
        {
            AudioManager.Instance.PlayOneShotAtLocation(openSound, transform.position, volume);
        }
 

        StartCoroutine(AnimateDoor(!isOpen));
    }

    private IEnumerator AnimateDoor(bool open)
    {
        isAnimating = true;
        bool hasplayedCloseSound = false;

        Quaternion startRotation = doorTransform.localRotation;

        Quaternion targetRotation = Quaternion.Euler(
            open ? openRotation : closedRotation
        );

        float timer = 0f;

        while (timer < animationDuration)
        {
            timer += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(timer / animationDuration);

            float curveValue =
                animationCurve.Evaluate(normalizedTime);

            doorTransform.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    curveValue
                );
            
            if((animationDuration - timer) < 0.1f && isOpen && !hasplayedCloseSound)
            {
                AudioManager.Instance.PlayOneShotAtLocation(closeSound, transform.position, volume);
                hasplayedCloseSound = true;
            }
  

            yield return null;
        }

        doorTransform.localRotation = targetRotation;
        
       

        isOpen = open;
        isAnimating = false;
    }
}