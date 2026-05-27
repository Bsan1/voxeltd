using System;
using UnityEngine;

public class SimpleDoor : MonoBehaviour, ILookInteractable, IInteractable, IPickupable
{
    Rigidbody rb;
    bool isHeld;
    Transform holdParent;
    Color originalColor;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    //temp must be discarded later
    private bool EnterBehav()
    {
        Debug.Log($"Looking at {name}");
        return true;
    }

    public void Interact()
    {
        Debug.Log($"Interacted with {name}");
    }

    public void Pickup(Transform hand)
    {
        if (hand == null) return;

        isHeld = true;
        holdParent = hand;

        rb.isKinematic = true;
        transform.SetParent(holdParent);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Drop()
    {
        isHeld = false;
        holdParent = null;

        transform.SetParent(null);
        rb.isKinematic = false;
    }

    public void Throw(Vector3 direction, float force)
    {
        isHeld = false;
        holdParent = null;

        transform.SetParent(null);
        rb.isKinematic = false;

        rb.AddForce(direction * force, ForceMode.Impulse);
    }

    // --- Look feedback (optional) ---
    public bool LookEnter()
    {
        if (!isHeld)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer) { originalColor = renderer.material.color; renderer.material.color = Color.green; }
            return EnterBehav();
        }
        else return false;
    }

    public void LookExit()
    {
        if(!isHeld)
        {
            Debug.Log($"Stopped looking at {name}");
            Renderer renderer = GetComponent<Renderer>();
            if (renderer) renderer.material.color = originalColor;
        }
    }

    public GameObject ReturnGameobject()
    {
        return gameObject;
    }
}
