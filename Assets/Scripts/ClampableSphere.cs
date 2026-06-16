using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Add to any sphere GameObject that has a SphereCollider.
/// Exposes world-space centre and radius so SphereSurfaceClamper can query
/// them every frame, automatically picking up any scale changes (grow/shrink).
///
/// Optionally call AutoRegister() if you want the sphere to self-register with
/// nearby SphereSurfaceClampers instead of wiring them manually in the Inspector.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ClampableSphere : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Optional auto-registration
    // -----------------------------------------------------------------------

    [Header("Auto-registration (optional)")]
    [Tooltip("If true, this sphere will find all SphereSurfaceClampers in the scene " +
             "on Start and register itself automatically. " +
             "Useful for dynamically spawned spheres.")]
    [SerializeField] private bool _autoRegister = false;

    // -----------------------------------------------------------------------
    // Internal
    // -----------------------------------------------------------------------

    private SphereCollider _col;

    // -----------------------------------------------------------------------
    // World-space properties — recomputed every access, so they stay accurate
    // as the object moves, rotates, or is scaled.
    // -----------------------------------------------------------------------

    /// <summary>World-space centre of the sphere (respects the collider's local centre offset).</summary>
    public Vector3 WorldCenter => transform.TransformPoint(_col.center);

    /// <summary>
    /// World-space radius.
    /// Uses the largest lossy-scale axis to be conservative; works correctly
    /// for uniform scale. For heavily non-uniform scale, prefer a uniform-scale sphere.
    /// </summary>
    public float WorldRadius =>
        _col.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z));

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _col = GetComponent<SphereCollider>();
    }

    private void Start()
    {
        if (_autoRegister)
            AutoRegister();
    }

    private void OnDestroy()
    {
        // Clean up references if we auto-registered.
        if (_autoRegister)
            AutoUnregister();
    }

    // -----------------------------------------------------------------------
    // Auto-registration helpers
    // -----------------------------------------------------------------------

    private void AutoRegister()
    {
        foreach (var clamper in FindObjectsByType<SphereSurfaceClamper>(FindObjectsSortMode.None))
            clamper.Register(this);
    }

    private void AutoUnregister()
    {
        foreach (var clamper in FindObjectsByType<SphereSurfaceClamper>(FindObjectsSortMode.None))
            clamper.Unregister(this);
    }

    // -----------------------------------------------------------------------
    // Debug visualisation
    // -----------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw the world-space sphere so you can verify scale is correct in the editor.
        if (_col == null) _col = GetComponent<SphereCollider>();
        if (_col == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.DrawSphere(WorldCenter, WorldRadius);
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(WorldCenter, WorldRadius);
    }
#endif
}