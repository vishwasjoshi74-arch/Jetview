using UnityEngine;
using System.Collections.Generic;

public class EngineSpin : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [System.Serializable]
    public class BladeGroup
    {
        public string name = "Group";
        public Transform[] parts;            // drag your meshes here
        public float speed = 360f;           // degrees per second at full throttle
        public bool clockwise = true;        // untick if it spins the wrong way

        [Tooltip("Optional: an empty object on the engine's center line. " +
                 "Its position is remembered per part, so it follows the part when separated.")]
        public Transform pivotOverride;
    }

    [Header("Blade Groups")]
    public BladeGroup fan = new BladeGroup { name = "Fan (big, front)", speed = 360f };
    public BladeGroup compressor = new BladeGroup { name = "Compressor (small, middle)", speed = 900f };
    public BladeGroup turbine = new BladeGroup { name = "Turbine (normal, rear)", speed = 600f };

    [Header("Rotation Axis (local to each part)")]
    public Axis axis = Axis.Z;

    [Header("Engine State")]
    public bool engineOn = false;

    [Header("Engine Sounds")]
    public AudioClip startSound;                         // plays once when the engine starts
    public AudioClip runningSound;                       // loops while the engine is running
    public AudioClip stopSound;                          // plays once when the engine stops
    [Range(0f, 1f)] public float volume = 0.7f;          // master volume (live slider)

    [Header("Timing")]
    [Tooltip("On = read the length from each audio clip. Off = use the fallback seconds below.")]
    public bool useClipLengths = true;
    public float startFallbackSeconds = 18f;             // used if no clip / not using clip length
    public float runningFallbackSeconds = 19f;           // info only, the running sound loops
    public float stopFallbackSeconds = 20f;
    [Tooltip("Seconds of overlap when one sound blends into the next.")]
    public float crossfade = 1f;

    private float throttle;                  // 0 = stopped, 1 = full speed
    private bool lastOn;

    private AudioSource startSrc, loopSrc, stopSrc;
    private float startLevel, loopLevel, stopLevel;   // 0..1 fade levels per sound

    private Dictionary<Transform, Vector3> localCenters = new Dictionary<Transform, Vector3>();

    public bool IsOn => engineOn;
    public void StartEngine() { engineOn = true; }
    public void StopEngine() { engineOn = false; }

    // Time the blades take to reach full speed / stop = length of the matching sound
    public float StartDuration => Mathf.Max(0.5f, (useClipLengths && startSound != null) ? startSound.length : startFallbackSeconds);
    public float StopDuration => Mathf.Max(0.5f, (useClipLengths && stopSound != null) ? stopSound.length : stopFallbackSeconds);
    public float RunningDuration => (useClipLengths && runningSound != null) ? runningSound.length : runningFallbackSeconds;

    void Start()
    {
        CacheCenters(fan);
        CacheCenters(compressor);
        CacheCenters(turbine);

        startSrc = MakeSource(false);
        loopSrc = MakeSource(true);
        stopSrc = MakeSource(false);

        lastOn = engineOn;
        if (engineOn) throttle = 1f;
    }

    AudioSource MakeSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;     // 2D sound, same volume from any camera angle
        s.volume = 0f;
        return s;
    }

    void CacheCenters(BladeGroup g)
    {
        if (g.parts == null) return;

        foreach (Transform part in g.parts)
        {
            if (part == null) continue;

            if (g.pivotOverride != null)
            {
                localCenters[part] = part.InverseTransformPoint(g.pivotOverride.position);
                continue;
            }

            Renderer[] rends = part.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { localCenters[part] = Vector3.zero; continue; }

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            localCenters[part] = part.InverseTransformPoint(b.center);
        }
    }

    void Update()
    {
        if (engineOn != lastOn)
        {
            lastOn = engineOn;
            OnToggled();
        }

        // Blades reach full speed in StartDuration seconds and stop in StopDuration seconds
        float rate = engineOn ? 1f / StartDuration : 1f / StopDuration;
        throttle = Mathf.MoveTowards(throttle, engineOn ? 1f : 0f, rate * Time.deltaTime);

        UpdateSound();

        if (throttle <= 0f) return;

        Spin(fan);
        Spin(compressor);
        Spin(turbine);
    }

    // ---------------- SOUND ----------------

    // Called the moment the engine is switched on or off
    void OnToggled()
    {
        if (engineOn)
        {
            // Start sound begins at the point matching the current blade speed
            if (startSound != null)
            {
                startSrc.clip = startSound;
                startSrc.time = Mathf.Clamp(throttle * startSound.length, 0f, startSound.length - 0.05f);
                startSrc.Play();
                startLevel = 1f;
            }
        }
        else
        {
            // Stop sound begins at the point matching the current blade speed
            if (stopSound != null)
            {
                stopSrc.clip = stopSound;
                stopSrc.time = Mathf.Clamp((1f - throttle) * stopSound.length, 0f, stopSound.length - 0.05f);
                stopSrc.Play();
                stopLevel = 1f;
            }
        }
    }

    void UpdateSound()
    {
        float fade = Mathf.Max(0.05f, crossfade);
        float fadeStep = Time.deltaTime / fade;

        float startTarget = 0f, loopTarget = 0f, stopTarget = 0f;

        if (engineOn)
        {
            // Running loop fades in during the last part of the start sound
            float overlap = Mathf.Min(fade, StartDuration * 0.5f);
            bool loopWanted = throttle >= 1f - overlap / StartDuration;
            loopTarget = loopWanted ? 1f : 0f;
            startTarget = loopWanted ? 0f : 1f;
            stopTarget = 0f;
        }
        else
        {
            stopTarget = 1f;
        }

        startLevel = Mathf.MoveTowards(startLevel, startTarget, fadeStep);
        loopLevel = Mathf.MoveTowards(loopLevel, loopTarget, fadeStep);
        stopLevel = Mathf.MoveTowards(stopLevel, stopTarget, fadeStep);

        // Running loop
        if (runningSound != null)
        {
            if (loopTarget > 0f && !loopSrc.isPlaying)
            {
                loopSrc.clip = runningSound;
                loopSrc.Play();
            }
            if (loopLevel <= 0f && loopTarget == 0f && loopSrc.isPlaying) loopSrc.Stop();
        }

        // Start / stop sounds cut off when fully faded out
        if (startLevel <= 0f && startSrc.isPlaying) startSrc.Stop();
        if (stopLevel <= 0f && stopSrc.isPlaying) stopSrc.Stop();

        // If the engine is fully stopped, silence everything
        if (!engineOn && throttle <= 0f && !stopSrc.isPlaying)
        {
            if (loopSrc.isPlaying) loopSrc.Stop();
            loopLevel = 0f;
        }

        startSrc.volume = volume * startLevel;
        loopSrc.volume = volume * loopLevel;
        stopSrc.volume = volume * stopLevel;
    }

    // ---------------- SPIN ----------------

    void Spin(BladeGroup g)
    {
        if (g.parts == null) return;

        float angle = g.speed * throttle * Time.deltaTime * (g.clockwise ? -1f : 1f);

        Vector3 localAxis = axis == Axis.X ? Vector3.right :
                            axis == Axis.Y ? Vector3.up : Vector3.forward;

        foreach (Transform part in g.parts)
        {
            if (part == null) continue;

            Vector3 worldAxis = part.TransformDirection(localAxis);
            Vector3 center = part.TransformPoint(
                localCenters.TryGetValue(part, out Vector3 c) ? c : Vector3.zero);

            part.RotateAround(center, worldAxis, angle);
        }
    }
}