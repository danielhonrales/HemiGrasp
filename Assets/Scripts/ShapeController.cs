using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Oculus.Interaction.Input;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShapeController : MonoBehaviour {

    // Shape states
    public enum Shape {
        SMALL,
        MEDIUM,
        LARGE,
        CONVEX,
        CONCAVE,
        SLOPE,
        CALIBRATION
    }

    [Header("Space: \t Go to next shape")]
    [Header("C: \t\t Calibrate")]
    [Header("R: \t\t Reset physical and visual to 62mm (fully retracted)")]
    [Header("F: \t\t Reset physical and visual to 82mm (fully extetnded)")]
    [Header("V: \t\t Toggle visual only mode")]
    [Header("1: \t\t Change shape to small")]
    [Header("2: \t\t Change shape to medium")]
    [Header("3: \t\t Change shape to large")]
    [Header("4: \t\t Change shape to convex")]
    [Header("5: \t\t Change shape to concave")]
    [Header("6: \t\t Change shape to shell")]
    [Header("0: \t\t Change shape to calibration sphere")]

    [Space(20)]

    [Header("NOTE: Might have to press R/F several times until it fully resets...")]

    [Space(20)]

    [Space(10), Header("Visual Only Mode")]
    public bool visualOnly;

    [Space(10), Header("Current State (mm)")]
    public Shape currentShape;
    public float currentTRadius;
    public float currentMRadius;
    public float currentLRadius;
    public float currentCalibrationRadius;

    [Space(10), Header("Target State (mm)")]
    public Shape targetShape;
    public float targetTRadius;
    public float targetMRadius;
    public float targetLRadius;

    [Space(10), Header("Speed Settings (mm/s)")]
    public bool isStatic;
    public float physicalTSpeed;
    public float physicalMSpeed;
    public float physicalLSpeed;

    [Space(10), Header("Calibration")]
    public float calibrationRadius;
    public bool tracking;
    public Vector3 calibrationOffset;
    public Vector3 homePosition;

    [Space(10), Header("References")]
    public GameObject small;
    public GameObject medium;
    public GameObject large;
    public GameObject convex;
    public GameObject concave;
    public GameObject slope;
    public GameObject calibrationSphere;
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
        if (Input.GetKeyDown(KeyCode.V)) { ToggleVisualOnly(); }
        if (Input.GetKeyDown(KeyCode.Alpha1)) { ChangeShape(Shape.SMALL); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { ChangeShape(Shape.MEDIUM); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { ChangeShape(Shape.LARGE); }
        if (Input.GetKeyDown(KeyCode.Alpha4)) { ChangeShape(Shape.CONVEX); }
        if (Input.GetKeyDown(KeyCode.Alpha5)) { ChangeShape(Shape.CONCAVE); }
        if (Input.GetKeyDown(KeyCode.Alpha6)) { ChangeShape(Shape.SLOPE); }
        if (Input.GetKeyDown(KeyCode.Alpha0)) { ChangeShape(Shape.CALIBRATION); }

        // Update sphere position if tracking is enabled
        if (tracking) {
            calibrationSphere.transform.position = new Vector3(hand.position.x + calibrationOffset.x,
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
        homePosition = calibrationSphere.transform.position;
    }

    // Instantly set physical and visual radius
    private void ManualReset(float radius) {
        calibrationRadius = radius;
        
        ChangeShape(Shape.CALIBRATION);
        SetPhysicalRadius(radius);
        SetVisualRadius(radius * visualSizeDifferenceMult);
    }

    // Toggle visual only mode
    private void ToggleVisualOnly() {
        visualOnly = !visualOnly;
    }

    // Change size of physical for calibration sphere
    private void SetPhysicalRadius(float radius) {
        currentTRadius = radius;
        currentMRadius = radius;
        currentLRadius = radius;

        serialController.SetSpeedMode(false);
        serialController.GoTo(MMtoDevice(radius), false);
    }

    // Change size of calibration sphere
    private void SetVisualRadius(float radius) {
        currentCalibrationRadius = radius;
        calibrationSphere.transform.localScale = new Vector3(MMtoUnityUnits(radius),
                                                             MMtoUnityUnits(radius),
                                                             MMtoUnityUnits(radius));

        // Offset visual position to match top of physical and visual
        float offset = MMtoUnityUnits((currentMRadius - currentCalibrationRadius) + offsetConstant) * offsetMultiplier;
        // Debug.Log($"PHYSICAL: {currentPhysicalRadius}  VISUAL: {currentVisualRadius}  DIFFERENCE: {currentPhysicalRadius - currentVisualRadius}  OFFSET: {offset}");
        calibrationSphere.transform.position = homePosition;
        calibrationSphere.transform.position = new Vector3(calibrationSphere.transform.position.x,
                                                           calibrationSphere.transform.position.y + offset,
                                                           calibrationSphere.transform.position.z);
    }

    // Dynamically (or instantly) change shape
    private void ChangeShape(Shape shape) {
        if (isStatic) {
            currentShape = shape;
            
            int shapeNum = 0;
            switch (shape) {
                case Shape.SMALL:
                    shapeNum = 0;
                    break;
                case Shape.MEDIUM:
                    shapeNum = 1;
                    break;
                case Shape.LARGE:
                    shapeNum = 2;
                    break;
                case Shape.CONVEX:
                    shapeNum = 3;
                    break;
                case Shape.CONCAVE:
                    shapeNum = 4;
                    break;
                case Shape.SLOPE:
                    shapeNum = 5;
                    break;
                default:
                    break;
            }

            serialController.ChangeShape(shapeNum);
        } else {
            int tLocation;
            int mLocation;
            int lLocation;
            switch (shape) {
                case Shape.SMALL:
                    targetTRadius = 62f;
                    targetMRadius = 62f;
                    targetLRadius = 62f;
                    
                    tLocation = 0;
                    mLocation = 0;
                    lLocation = 0;

                    break;
                case Shape.MEDIUM:
                    targetTRadius = 72f;
                    targetMRadius = 72f;
                    targetLRadius = 72f;
                    
                    tLocation = 50;
                    mLocation = 50;
                    lLocation = 50;
                    
                    break;
                case Shape.LARGE:
                    targetTRadius = 82f;
                    targetMRadius = 82f;
                    targetLRadius = 82f;
                    
                    tLocation = 100;
                    mLocation = 100;
                    lLocation = 100;
                    
                    break;
                case Shape.CONVEX:
                    targetTRadius = 62f;
                    targetMRadius = 82f;
                    targetLRadius = 62f;
                    
                    tLocation = 0;
                    mLocation = 100;
                    lLocation = 0;
                    
                    break;
                case Shape.CONCAVE:
                    targetTRadius = 82f;
                    targetMRadius = 62f;
                    targetLRadius = 82f;
                    
                    tLocation = 100;
                    mLocation = 0;
                    lLocation = 100;
                    
                    break;
                case Shape.SLOPE:
                    targetTRadius = 62f;
                    targetMRadius = 72f;
                    targetLRadius = 82f;
                    
                    tLocation = 0;
                    mLocation = 50;
                    lLocation = 100;
                    
                    break;
            }
        }
    }

    // Dynamically move visual and physical
    private IEnumerator DynamicAll() {
        // TODO: IMPLEMENT
        yield return null;
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
