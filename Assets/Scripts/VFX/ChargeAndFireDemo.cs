using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Demo driver: press a key (or auto-loop) to play the VoxelChargeUp wind-up, and when it
/// releases, flash the ChainLightning bolt for a moment. Shows the full charge -> release
/// sequence end to end.
/// </summary>
public class ChargeAndFireDemo : MonoBehaviour
{
    public VoxelChargeUp charge;
    public ChainLightning bolt;

    [Header("Trigger")]
    public Key triggerKey = Key.Space;
    public bool autoLoop = true;
    public float autoInterval = 2.5f;

    [Header("Bolt")]
    [Tooltip("How long the released bolt stays visible.")]
    public float boltDuration = 0.4f;

    bool wired;
    float autoTimer;

    void Awake()
    {
        if (bolt) bolt.gameObject.SetActive(false); // hidden until released
    }

    void OnEnable()
    {
        if (charge && !wired) { charge.OnReleased += FireBolt; wired = true; }
    }

    void OnDisable()
    {
        if (charge && wired) { charge.OnReleased -= FireBolt; wired = false; }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[triggerKey].wasPressedThisFrame) TriggerCharge();

        if (autoLoop)
        {
            autoTimer += Time.deltaTime;
            bool busy = (charge && charge.IsCharging) || (bolt && bolt.gameObject.activeSelf);
            if (autoTimer >= autoInterval && !busy) { autoTimer = 0f; TriggerCharge(); }
        }
    }

    public void TriggerCharge()
    {
        if (charge == null) { FireBolt(); return; }
        if (charge.IsCharging) return;
        charge.StartCharge();
    }

    void FireBolt()
    {
        if (bolt == null) return;
        StopAllCoroutines();
        StartCoroutine(BoltRoutine());
    }

    IEnumerator BoltRoutine()
    {
        bolt.gameObject.SetActive(true);
        bolt.Fire();
        yield return new WaitForSeconds(boltDuration);
        bolt.gameObject.SetActive(false);
    }
}
