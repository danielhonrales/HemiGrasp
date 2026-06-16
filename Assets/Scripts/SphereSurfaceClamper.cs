using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clamps the entire hand to a sphere's surface once the wrist enters a
/// proximity threshold, keeping the hand hugging the surface until it pulls
/// far enough away to release.
///
/// ── Behaviour ────────────────────────────────────────────────────────────────
///
///  FREE state  (wrist farther than snapRadius from sphere center)
///    Hand moves freely with tracking. No modification to bone transforms.
///
///  CLAMPED state  (wrist within snapRadius)
///    The wrist is anchored to the sphere surface. The entire hand moves as a
///    rigid unit: the tracked-to-surface delta is computed and applied to the
///    wrist transform, which Unity propagates to all children automatically.
///    After the wrist is repositioned, per-finger surface projection runs to
///    flatten any fingers that still end up inside the sphere.
///
///    The hand stays clamped until the wrist tracking position exceeds
///    (snapRadius + releaseHysteresis), preventing rapid snap-in/snap-out.
///
///    When the sphere grows/shrinks the wrist anchor slides along the surface
///    automatically each frame, so the whole hand follows.
///
/// ── Processing order each LateUpdate ─────────────────────────────────────────
///   1. Find the nearest sphere within snapRadius of the wrist.
///   2. Compute wristSurfacePoint = sphere center + (wristTracked dir) * radius.
///   3. Shift the wrist transform by (wristSurfacePoint - wristTracked).
///      All children (palm, metacarpals, fingers) inherit this shift for free.
///   4. Per-finger chain projection: walk proximal → distal, clamping any joint
///      that still ends up inside the sphere after the rigid-body shift.
///
/// ── Setup ────────────────────────────────────────────────────────────────────
///   1. Add SphereSurfaceClamper to the OVRHandPrefab root (one per hand).
///   2. Assign _skeleton (OVRSkeleton on the same prefab).
///   3. Add ClampableSphere to every grabbable sphere and assign to _spheres,
///      or enable Auto Register on ClampableSphere for runtime-spawned objects.
///   4. Tune snapRadius: should be roughly sphere radius + hand half-width.
/// </summary>
[DefaultExecutionOrder(10000)]
public class SphereSurfaceClamper : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Hand")]
    [Tooltip("OVRSkeleton on the hand prefab.")]
    [SerializeField] private OVRSkeleton _skeleton;

    [Header("Spheres")]
    [Tooltip("All ClampableSphere objects this hand can grab.")]
    [SerializeField] private List<ClampableSphere> _spheres = new();

    [Header("Proximity Snapping")]
    [Tooltip("Distance from sphere CENTER at which the hand snaps to the surface. " +
             "Set this to roughly sphere-radius + ~0.06 m (half hand width) so the " +
             "hand snaps as the palm first makes contact.")]
    [SerializeField] private float _snapRadius = 0.12f;

    [Tooltip("Extra distance beyond snapRadius the wrist must travel before releasing. " +
             "Prevents jitter at the snap boundary.")]
    [SerializeField, Range(0f, 0.05f)] private float _releaseHysteresis = 0.02f;

    [Tooltip("Outward offset (metres) applied when anchoring the wrist to the surface. " +
             "Needs to be larger to account for palm thickness between the wrist bone and the skin.")]
    [SerializeField, Range(0f, 0.2f)] private float _palmSurfaceOffset = 0.1f;

    [Tooltip("Outward offset (metres) for individual finger joints. " +
             "Fingers are thin so this can stay at or near 0.")]
    [SerializeField, Range(0f, 0.05f)] private float _fingerSurfaceOffset = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Bone ID tables  (XRHand / OpenXR naming)
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly OVRSkeleton.BoneId WristBone =
        OVRSkeleton.BoneId.XRHand_Wrist;

    // Finger chains, proximal → distal.
    // The metacarpals are intentionally omitted here because the rigid wrist
    // shift already moves them; we only need per-joint projection for phalanges.
    private static readonly OVRSkeleton.BoneId[][] s_FingerChains =
    {
        new[] // Thumb  (no intermediate in XRHand)
        {
            OVRSkeleton.BoneId.XRHand_ThumbMetacarpal,
            OVRSkeleton.BoneId.XRHand_ThumbProximal,
            OVRSkeleton.BoneId.XRHand_ThumbDistal,
            OVRSkeleton.BoneId.XRHand_ThumbTip,
        },
        new[] // Index
        {
            OVRSkeleton.BoneId.XRHand_IndexMetacarpal,
            OVRSkeleton.BoneId.XRHand_IndexProximal,
            OVRSkeleton.BoneId.XRHand_IndexIntermediate,
            OVRSkeleton.BoneId.XRHand_IndexDistal,
            OVRSkeleton.BoneId.XRHand_IndexTip,
        },
        new[] // Middle
        {
            OVRSkeleton.BoneId.XRHand_MiddleMetacarpal,
            OVRSkeleton.BoneId.XRHand_MiddleProximal,
            OVRSkeleton.BoneId.XRHand_MiddleIntermediate,
            OVRSkeleton.BoneId.XRHand_MiddleDistal,
            OVRSkeleton.BoneId.XRHand_MiddleTip,
        },
        new[] // Ring
        {
            OVRSkeleton.BoneId.XRHand_RingMetacarpal,
            OVRSkeleton.BoneId.XRHand_RingProximal,
            OVRSkeleton.BoneId.XRHand_RingIntermediate,
            OVRSkeleton.BoneId.XRHand_RingDistal,
            OVRSkeleton.BoneId.XRHand_RingTip,
        },
        new[] // Little (Pinky)
        {
            OVRSkeleton.BoneId.XRHand_LittleMetacarpal,
            OVRSkeleton.BoneId.XRHand_LittleProximal,
            OVRSkeleton.BoneId.XRHand_LittleIntermediate,
            OVRSkeleton.BoneId.XRHand_LittleDistal,
            OVRSkeleton.BoneId.XRHand_LittleTip,
        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────────────────

    private Dictionary<OVRSkeleton.BoneId, Transform> _boneMap;

    // The sphere the hand is currently snapped to (null = FREE).
    private ClampableSphere _clampedSphere;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True while the hand is snapped to a sphere surface.</summary>
    public bool IsClamped => _clampedSphere != null;

    /// <summary>The sphere currently being hugged, or null if free.</summary>
    public ClampableSphere ClampedSphere => _clampedSphere;

    public void Register(ClampableSphere sphere)
    {
        if (sphere != null && !_spheres.Contains(sphere))
            _spheres.Add(sphere);
    }

    public void Unregister(ClampableSphere sphere)
    {
        _spheres.Remove(sphere);
        if (_clampedSphere == sphere) _clampedSphere = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core loop
    // ─────────────────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (_skeleton == null || !_skeleton.IsInitialized || !_skeleton.IsDataValid)
            return;

        if (_boneMap == null) BuildBoneMap();
        if (_boneMap == null || _boneMap.Count == 0 || _spheres.Count == 0) return;

        if (!_boneMap.TryGetValue(WristBone, out Transform wrist)) return;

        // ── Step 1: Proximity check — update which sphere (if any) is active ──
        UpdateClampState(wrist.position);

        // ── Step 2: If clamped, shift wrist (and entire hand) to sphere surface ─
        if (_clampedSphere != null)
        {
            Vector3 wristOnSurface = SurfacePoint(_clampedSphere, wrist.position, _palmSurfaceOffset);
            Vector3 shift = wristOnSurface - wrist.position;
            wrist.position = wristOnSurface;

            // Step 3: Per-finger surface projection.
            // The rigid shift already moved all children, but fingers that curl
            // inward may still clip. Walk each chain and push any buried joint out.
            foreach (var chain in s_FingerChains)
                ProjectFingerChain(chain, _clampedSphere);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State machine
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateClampState(Vector3 trackedWristPos)
    {
        if (_clampedSphere == null)
        {
            // FREE → look for any sphere whose snap zone contains the wrist.
            ClampableSphere nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var s in _spheres)
            {
                if (s == null) continue;
                float dist = Vector3.Distance(trackedWristPos, s.WorldCenter);
                if (dist < _snapRadius && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = s;
                }
            }

            _clampedSphere = nearest; // null if none found
        }
        else
        {
            // CLAMPED → release only when wrist pulls beyond snapRadius + hysteresis.
            float dist = Vector3.Distance(trackedWristPos, _clampedSphere.WorldCenter);
            if (dist > _snapRadius + _releaseHysteresis)
                _clampedSphere = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Finger-chain surface projection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Walk one finger proximal → distal against a specific sphere.
    /// Once a joint is inside the sphere, all more-distal joints are also
    /// projected — they cannot physically reach past the surface.
    /// </summary>
    private void ProjectFingerChain(OVRSkeleton.BoneId[] chain, ClampableSphere sphere)
    {
        bool locked = false;

        foreach (var id in chain)
        {
            if (!_boneMap.TryGetValue(id, out Transform t)) continue;

            float dist = Vector3.Distance(t.position, sphere.WorldCenter);
            bool inside = dist < sphere.WorldRadius + _fingerSurfaceOffset;

            if (inside || locked)
            {
                t.position = SurfacePoint(sphere, t.position, _fingerSurfaceOffset);
                locked = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Geometry
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the point on the sphere surface in the direction of worldPos,
    /// offset outward by the given amount.
    /// </summary>
    private static Vector3 SurfacePoint(ClampableSphere s, Vector3 worldPos, float offset)
    {
        Vector3 toPoint = worldPos - s.WorldCenter;
        float dist = toPoint.magnitude;
        Vector3 dir = dist < 1e-4f ? Vector3.up : toPoint / dist;
        return s.WorldCenter + dir * (s.WorldRadius + offset);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialisation
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildBoneMap()
    {
        _boneMap = new Dictionary<OVRSkeleton.BoneId, Transform>();
        foreach (var bone in _skeleton.Bones)
            if (bone?.Transform != null)
                _boneMap[bone.Id] = bone.Transform;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Editor debug
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private bool _debugDraw = false;

    private void OnDrawGizmos()
    {
        if (!_debugDraw || !Application.isPlaying) return;

        // Draw snap radius around each sphere.
        foreach (var s in _spheres)
        {
            if (s == null) continue;
            Gizmos.color = (_clampedSphere == s)
                ? new Color(0f, 1f, 0.3f, 0.15f)
                : new Color(1f, 1f, 0f, 0.08f);
            Gizmos.DrawSphere(s.WorldCenter, _snapRadius);
            Gizmos.color = (_clampedSphere == s) ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(s.WorldCenter, _snapRadius);
        }

        // Draw wrist position.
        if (_boneMap != null && _boneMap.TryGetValue(WristBone, out Transform w))
        {
            Gizmos.color = IsClamped ? Color.green : Color.red;
            Gizmos.DrawSphere(w.position, 0.008f);
        }
    }
#endif
}