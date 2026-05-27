using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerLookCaster : MonoBehaviour
{
    public Camera cam;
    public Image crosshair;
    public Transform ignoreRoot;
    public LayerMask interactableMask;
    public float maxDistance = 4f;
    public float checkInterval = 0.05f;

    ILookInteractable current;
    ILookInteractable hitTarget;
    public IInteractable hitInteractable;

    void Awake()
    {
        if (!cam) Debug.LogError($"There is no camera assigned on PlayerLookCaster {this.name}");
        if (!ignoreRoot) ignoreRoot = transform.root;
    }

    void OnEnable() => StartCoroutine(CheckLoop());
    void OnDisable() => StopAllCoroutines();

    IEnumerator CheckLoop()
    {
        while (true)
        {
            DoCheck();
            yield return new WaitForSeconds(checkInterval); ;
        }
    }
    private void CurrentLookEnter()
    {
        current?.LookEnter();
        crosshair.color = Color.green;
    }    
    private void CurrentLookExit()
    {
        current?.LookExit();
        crosshair.color = Color.black;
    }

    void DoCheck()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.magenta, checkInterval);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            if (!(ignoreRoot && hit.transform.root == ignoreRoot))
            {
                hitTarget = hit.transform.GetComponentInParent<ILookInteractable>();
                hitInteractable = hit.transform.GetComponentInParent<IInteractable>();
            }

            if(hitTarget != null)
                if(current == null)
                {
                    current = hitTarget;
                    CurrentLookEnter();
                }
                else
                {
                    //if player looks on different target
                    if (hitTarget != current)
                    {
                        CurrentLookExit();
                        current = hitTarget;
                        CurrentLookEnter();
                    }
                }
        }
        else
        {
            CurrentLookExit();
            current = null;
            hitInteractable = null;
        }
        
        
    }
}
