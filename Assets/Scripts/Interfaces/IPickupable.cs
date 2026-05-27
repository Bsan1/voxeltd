using UnityEngine;

public interface IPickupable
{
    void Pickup(Transform hand);
    void Drop();
    void Throw(Vector3 direction, float force);
}
