using UnityEngine;

public class EnginePartSeparator : MonoBehaviour
{
    [System.Serializable]
    public class Part
    {
        public string label = "Part";
        [TextArea(3, 12)]
        public string details = "";                 // paste the part's description here
        public Transform target;                    // drag the part here
        public float distance = 0f;                 // how far along the explode axis (can be negative)
        public Vector3 extraOffset = Vector3.zero;  // optional sideways nudge (world space)
    }

    [Header("Parts (add 13-15, ordered front to back)")]
    public Part[] parts;

    [Header("Explode Direction")]
    public Vector3 explodeAxis = Vector3.forward;   // the engine's length direction
    [Tooltip("Optional: use this object's rotation for the axis. Empty = world axes.")]
    public Transform axisReference;

    [Header("Motion")]
    public float smoothness = 6f;                   // higher = faster

    [Header("Auto Spacing helper")]
    public float autoSpacing = 1f;

    private Vector3[] currentOffset;
    private bool separated;

    public bool IsSeparated => separated;

    void Start()
    {
        currentOffset = new Vector3[parts.Length];
    }

    public void Separate() { separated = true; }
    public void Assemble() { separated = false; }

    void Update()
    {
        if (currentOffset == null || currentOffset.Length != parts.Length)
            currentOffset = new Vector3[parts.Length];

        Vector3 axis = explodeAxis.normalized;
        if (axisReference != null) axis = axisReference.TransformDirection(axis);

        float k = 1f - Mathf.Exp(-smoothness * Time.deltaTime);

        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p == null || p.target == null) continue;

            Vector3 goal = separated ? axis * p.distance + p.extraOffset : Vector3.zero;
            Vector3 next = Vector3.Lerp(currentOffset[i], goal, k);

            if ((next - goal).sqrMagnitude < 1e-8f) next = goal;

            p.target.position += next - currentOffset[i];
            currentOffset[i] = next;
        }
    }

    [ContextMenu("Auto Space Parts (centered)")]
    void AutoSpace()
    {
        if (parts == null) return;
        for (int i = 0; i < parts.Length; i++)
            parts[i].distance = (i - (parts.Length - 1) / 2f) * autoSpacing;
    }
}