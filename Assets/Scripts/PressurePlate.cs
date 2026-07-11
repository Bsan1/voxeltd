using System.Collections.Generic;
using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [SerializeField] Transform targetDoor;
    [SerializeField] float openHeight = 3f;
    [SerializeField] float slideDuration = 0.4f;

    readonly HashSet<int> playerCollidersInside = new HashSet<int>();
    Vector3 closedPosition;
    Vector3 openPosition;
    Rigidbody targetRigidbody;
    bool originalIsKinematic;
    bool originalUseGravity;

    void Awake()
    {
        if (!targetDoor)
        {
            GameObject foundDoor = GameObject.Find("SimpleBox");
            if (foundDoor) targetDoor = foundDoor.transform;
        }

        if (!targetDoor)
        {
            Debug.LogWarning($"{nameof(PressurePlate)} could not find target door 'SimpleBox'.", this);
            return;
        }

        closedPosition = targetDoor.position;
        openPosition = closedPosition + Vector3.up * openHeight;

        targetRigidbody = targetDoor.GetComponent<Rigidbody>();
        if (targetRigidbody)
        {
            originalIsKinematic = targetRigidbody.isKinematic;
            originalUseGravity = targetRigidbody.useGravity;
            targetRigidbody.isKinematic = true;
            targetRigidbody.useGravity = false;
        }
    }

    void Update()
    {
        if (!targetRigidbody)
        {
            MoveDoor(Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        if (targetRigidbody)
        {
            MoveDoor(Time.fixedDeltaTime);
        }
    }

    void MoveDoor(float deltaTime)
    {
        if (!targetDoor) return;

        Vector3 targetPosition = playerCollidersInside.Count > 0 ? openPosition : closedPosition;
        float slideSpeed = openHeight / Mathf.Max(0.01f, slideDuration);
        Vector3 nextPosition = Vector3.MoveTowards(
            targetDoor.position,
            targetPosition,
            slideSpeed * deltaTime);

        if (targetRigidbody)
        {
            targetRigidbody.MovePosition(nextPosition);
        }
        else
        {
            targetDoor.position = nextPosition;
        }
    }

    void OnDisable()
    {
        if (!targetRigidbody) return;

        targetRigidbody.isKinematic = originalIsKinematic;
        targetRigidbody.useGravity = originalUseGravity;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            playerCollidersInside.Add(other.GetInstanceID());
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            playerCollidersInside.Remove(other.GetInstanceID());
        }
    }

    static bool IsPlayerCollider(Collider other)
    {
        if (!other) return false;

        Rigidbody body = other.GetComponentInParent<Rigidbody>();
        CharacterController controller = other.GetComponentInParent<CharacterController>();
        return body && controller;
    }
}
