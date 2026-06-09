using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Oculus.Interaction.Input;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class DynamicController : MonoBehaviour {

    [Header("Space: \t Start physical and visual grow/shrink")]
    [Header("C: \t\t Calibrate")]
    [Header("R: \t\t Reset physical and visual to 62mm (for grow)")]
    [Header("F: \t\t Reset physical and visual to 82mm (for shrink)")]

    [Space(20)]

    [Header("NOTE: Might have to press R/F several times until it fully resets...")]
    [Header("not sure why. Otherwise just need to press C after sphere is in the")]
    [Header("right spot, set visual/physical speed with the sliders, press R/F to")]
    [Header("reset everything for grow/shrink, then press space to start movement.")]

    [Space(20)]

    [Header("Use speed control?")]
    public bool speedControl;

    [Space(10), Header("Current State (mm)")]
    public float currentVisualRadius;
    public float currentPhysicalRadius;

    [Space(10), Header("Target State (mm)")]
    public float targetVisualRadius;
    public float targetPhysicalRadius;

    [Space(10), Header("Speed Settings (mm/s)")]
    public float visualSpeed;
    public float physicalSpeed;

    [Space(10), Header("Calibration")]
    public bool tracking;
    public Vector3 calibrationOffset;
    public Vector3 homePosition;

    [Space(10), Header("References")]
    public GameObject sphere;
    public SerialController serialController;

    private float originalOffsetY;
    private Transform hand;

    [Space(10), Header("Misc.")]
    public float visualSizeDifferenceMult;
    public float offsetMultiplier;
    public float offsetConstant;

    void Start() {
        // Find hand (middle finger) object
        hand = GameObject.Find("[BuildingBlock] Hand Tracking right").transform
                         .Find("Bones")
                         .Find("XRHand_Wrist")
                         .Find("XRHand_MiddleMetacarpal")
                         .Find("XRHand_MiddleProximal");

        // Set initial offset value
        originalOffsetY = calibrationOffset.y;
    }

    void Update() {
        // Check for keyboard input
        if (Input.GetKeyDown(KeyCode.Space)) { StartCoroutine(DynamicAll()); }
        if (Input.GetKeyDown(KeyCode.C)) { CalibrateVisual(); }
        if (Input.GetKeyDown(KeyCode.R)) { ManualReset(62f); }
        if (Input.GetKeyDown(KeyCode.F)) { ManualReset(82f); }

        // Update sphere position if tracking is enabled
        if (tracking) {
            sphere.transform.position = new Vector3(hand.position.x + calibrationOffset.x,
                                                    hand.position.y + calibrationOffset.y,
                                                    hand.position.z + calibrationOffset.z);
        }
    }

    private void OnApplicationQuit() {
        serialController.GoTo(0, false);
        serialController.GoTo(0, true);
        Thread.Sleep(1000);
        serialController.StopPID();
    }

    // Save the current sphere location as its home position
    private void CalibrateVisual() {
        homePosition = sphere.transform.position;
    }

    // Instantly set physical and visual radius
    private void ManualReset(float radius) {
        SetPhysicalRadius(radius);
        SetVisualRadius(radius * visualSizeDifferenceMult);
    }

    // Change size of physical
    private void SetPhysicalRadius(float radius, bool skipGoTo = false) {
        currentPhysicalRadius = radius;

        if (!skipGoTo) {
            serialController.SetSpeedMode(false);
            serialController.GoTo(MMtoDevice(radius), false);
        }
    }

    // Change size of sphere
    private void SetVisualRadius(float radius) {
        currentVisualRadius = radius;
        sphere.transform.localScale = new Vector3(MMtoUnityUnits(radius),
                                                  MMtoUnityUnits(radius),
                                                  MMtoUnityUnits(radius));

        // Offset visual position to match top of physical and visual
        float offset = MMtoUnityUnits((currentPhysicalRadius - currentVisualRadius) + offsetConstant) * offsetMultiplier;
        // Debug.Log($"PHYSICAL: {currentPhysicalRadius}  VISUAL: {currentVisualRadius}  DIFFERENCE: {currentPhysicalRadius - currentVisualRadius}  OFFSET: {offset}");
        sphere.transform.position = homePosition;
        sphere.transform.position = new Vector3(sphere.transform.position.x,
                                                sphere.transform.position.y + offset,
                                                sphere.transform.position.z);
    }

    // Dyanmically move visual and physical
    private IEnumerator DynamicAll() {
        if (Mathf.Abs(physicalSpeed) < 1f) {
            Debug.LogError("Physical speed not valid!");
            yield break;
        }

        float timeElapsed = 0.1f; // Try to help get momentum for slow speeds by skipping 0.1s of positions

        float duration = 20f / Mathf.Abs(physicalSpeed);

        float initialPhysicalRadius = currentPhysicalRadius;
        targetPhysicalRadius = currentPhysicalRadius + (physicalSpeed * duration);

        float initialVisualRadius = currentVisualRadius;
        targetVisualRadius = currentVisualRadius + (visualSpeed * duration);
            
        int targetPosition = (int)Mathf.Lerp(0, 100, Mathf.InverseLerp(62, 82, targetPhysicalRadius));

        // Call speed control command
        if (speedControl) {    
            int targetSpeed = 0;
            if (Mathf.Abs(physicalSpeed) == 10f) {
                targetSpeed = 20;
            } else if (Mathf.Abs(physicalSpeed) == 20f) {
                targetSpeed = 28;
            } else if (Mathf.Abs(physicalSpeed) == 30f) {
                targetSpeed = 40;
            } else if (Mathf.Abs(physicalSpeed) == 40f) {
                targetSpeed = 70;
            }

            serialController.SetSpeedMode(true);
            serialController.SpeedCommand(targetPosition, targetSpeed);
        } else {
            if (targetPosition < 50) {
                targetPosition = 0;
            } else {
                targetPosition = 100;
            }

            serialController.DynamicCommand(targetPosition, (int)Mathf.Abs(physicalSpeed));
        }

        while (timeElapsed < duration) {
            float t = timeElapsed / duration;
            
            SetPhysicalRadius(Mathf.Lerp(initialPhysicalRadius, targetPhysicalRadius, t), true);
            SetVisualRadius(Mathf.Lerp(initialVisualRadius, targetVisualRadius, t));

            timeElapsed += Time.deltaTime;
            yield return null;
        }
    }
    
    // Convert from millimeters to Unity units
    private float MMtoUnityUnits(float mm) {
        return mm * 0.002f;
    }

    // Convert from millimeters to device 0-100
    private int MMtoDevice(float mm) {
        return (int)(Mathf.InverseLerp(62f, 82f, mm) * 100);
    }
}
