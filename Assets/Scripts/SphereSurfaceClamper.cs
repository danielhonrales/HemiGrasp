using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clamps the entire hand to a sphere's surface once a reference joint enters
/// a proximity threshold, keeping the hand hugging the surface until it pulls
/// far enough away to release.
///
/// ── Reference joint ──────────────────────────────────────────────────────────
///   Snapping and surface anchoring are driven by XRHand_MiddleIntermediate
///   (the middle finger PIP / middle interphalangeal joint). This sits at the
///   natural contact centre when the palm cups a sphere, so the sphere aligns
///   under the knuckle crease rather than the wrist.
///
/// ── Behaviour ────────────────────────────────────────────────────────────────
///   FREE    — hand moves freely with tracking; no bone transforms modified.
///   CLAMPED — the reference joint is projected onto the sphere surface, and
///             the same world-space delta is applied to the wrist, which Unity
///             propagates to all children. Every joint in every finger chain
///             (including metacarpals) is then individually projected to ensure
///             nothing clips through.
///
/// ── Processing order each LateUpdate ─────────────────────────────────────────
///   1. Read the tracked (pre-modification) position of the reference joint.
///   2. Proximity check → enter/exit CLAMPED state with hysteresis.
///   3. Compute surfaceDelta = SurfacePoint(refJoint) - refJoint.position.
///   4. Translate the wrist by surfaceDelta (all children follow for free).
///   5. Walk every finger chain proximal→distal; project each joint that is
///      inside the sphere, propagating the lock to all more-distal joints.
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
    [Tooltip("Distance from sphere CENTER at which the hand snaps to the surface, " +
             "measured from the middle-finger PIP joint. Roughly sphere-radius + 0.03 m " +
             "is a good starting point.")]
    [SerializeField] private float _snapRadius = 0.1f;

    [Tooltip("Extra distance beyond snapRadius the reference joint must travel before " +
             "releasing. Prevents jitter at the snap boundary.")]
    [SerializeField, Range(0f, 0.05f)] private float _releaseHysteresis = 0.02f;

    [Header("Surface Offsets")]
    [Tooltip("Outward offset (metres) applied when anchoring the reference joint to the " +
             "surface. Accounts for the distance between the PIP bone and the skin.")]
    [SerializeField, Range(0f, 0.2f)] private float _palmSurfaceOffset = 0.1f;

    [Tooltip("Outward offset (metres) for individual finger joints during per-joint " +
             "projection. Fingers are thin so this can stay near 0.")]
    [SerializeField, Range(0f, 0.05f)] private float _fingerSurfaceOffset = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Bone ID tables  (XRHand / OpenXR naming)
    // ─────────────────────────────────────────────────────────────────────────

    // Reference joint used for proximity detection and surface anchoring.
    // Middle PIP sits at the natural centre of the palm cup.
    private const OVRSkeleton.BoneId RefJoint = OVRSkeleton.BoneId.XRHand_MiddleIntermediate;

    // Wrist is the root of the entire hand hierarchy — translating it moves everything.
    private const OVRSkeleton.BoneId WristBone = OVRSkeleton.BoneId.XRHand_Wrist;

    // Full finger chains, proximal → distal, INCLUDING metacarpals.
    // Every joint is listed so nothing is skipped and no joint can clip through.
    private static readonly OVRSkeleton.BoneId[][] s_FingerChains =
    {
        new[] // Thumb  (no intermediate phalanx in XRHand)
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
    private ClampableSphere _clampedSphere; // null = FREE

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsClamped => _clampedSphere != null;
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

        if (!_boneMap.TryGetValue(RefJoint, out Transform refJointT)) return;
        if (!_boneMap.TryGetValue(WristBone, out Transform wristT)) return;

        // Step 1: Read the raw tracked position BEFORE we modify anything.
        Vector3 trackedRefPos = refJointT.position;

        // Step 2: Proximity check — enter or exit CLAMPED state.
        UpdateClampState(trackedRefPos);
        if (_clampedSphere == null) return;

        // Step 3: Compute how far the reference joint needs to move to sit on
        //         the sphere surface, then apply that same delta to the wrist.
        //         Because the wrist is the hierarchy root, every child bone
        //         (palm, metacarpals, all fingers) translates by the same delta.
        Vector3 refOnSurface = SurfacePoint(_clampedSphere, trackedRefPos, _palmSurfaceOffset);
        Vector3 surfaceDelta = refOnSurface - trackedRefPos;
        wristT.position += surfaceDelta;

        // Step 4: Per-joint projection for every finger chain.
        //         The rigid shift above handles most of the hand, but curled
        //         fingers or knuckle joints can still land inside the sphere.
        //         Walk every joint and project any that are inside.
        foreach (var chain in s_FingerChains)
            ProjectFingerChain(chain, _clampedSphere);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State machine
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateClampState(Vector3 trackedRefPos)
    {
        if (_clampedSphere == null)
        {
            // FREE → snap to the nearest sphere whose snap zone contains refJoint.
            ClampableSphere nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var s in _spheres)
            {
                if (s == null) continue;
                float snapR = s.WorldRadius + _snapRadius; // snap zone = surface + margin
                float dist = Vector3.Distance(trackedRefPos, s.WorldCenter);
                if (dist < snapR && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = s;
                }
            }

            _clampedSphere = nearest;
        }
        else
        {
            // CLAMPED → release when refJoint pulls beyond surface + snapRadius + hysteresis.
            float releaseR = _clampedSphere.WorldRadius + _snapRadius + _releaseHysteresis;
            float dist = Vector3.Distance(trackedRefPos, _clampedSphere.WorldCenter);
            if (dist > releaseR)
                _clampedSphere = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Finger-chain surface projection
    // ─────────────────────────────────────────────────────────────────────────

    private void ProjectFingerChain(OVRSkeleton.BoneId[] chain, ClampableSphere sphere)
    {
        bool locked = false;

        foreach (var id in chain)
        {
            if (!_boneMap.TryGetValue(id, out Transform t)) continue;

            bool inside = Vector3.Distance(t.position, sphere.WorldCenter)
                          < sphere.WorldRadius + _fingerSurfaceOffset;

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
        if (!_debugDraw || !Application.isPlaying || _boneMap == null) return;

        foreach (var s in _spheres)
        {
            if (s == null) continue;
            float snapR = s.WorldRadius + _snapRadius;
            bool active = (_clampedSphere == s);

            // Snap zone.
            Gizmos.color = active ? new Color(0f, 1f, 0.3f, 0.12f) : new Color(1f, 1f, 0f, 0.06f);
            Gizmos.DrawSphere(s.WorldCenter, snapR);
            Gizmos.color = active ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(s.WorldCenter, snapR);

            // Sphere surface.
            Gizmos.color = active ? new Color(0f, 1f, 0.3f, 0.25f) : new Color(1f, 1f, 1f, 0.1f);
            Gizmos.DrawWireSphere(s.WorldCenter, s.WorldRadius);
        }

        // Reference joint (middle PIP).
        if (_boneMap.TryGetValue(RefJoint, out Transform rj))
        {
            Gizmos.color = IsClamped ? Color.green : Color.red;
            Gizmos.DrawSphere(rj.position, 0.007f);
        }

        // Wrist.
        if (_boneMap.TryGetValue(WristBone, out Transform w))
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(w.position, 0.005f);
        }
    }
#endif
}