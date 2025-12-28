using System;
using UnityEngine;

public class CarSuspension : MonoBehaviour
{
    [Header("Suspension Points")]
    [SerializeField] private Transform fl;
    [SerializeField] private Transform fr;
    [SerializeField] private Transform rl;
    [SerializeField] private Transform rr;

    [Header("Suspension Settings")]
    [SerializeField] private float restLength = 0.4f;
    [SerializeField] private float springTravel = 0.2f;
    [SerializeField] private float springStiffness = 20000f;
    [SerializeField] private float damperStiffness = 3500f;
    [SerializeField] private float wheelRadius = 0.35f;

    private Rigidbody rb;

    private float lastFLcompression;
    private float lastFRcompression;
    private float lastRLcompression;
    private float lastRRcompression;

    
    private float ui_FL_force, ui_FR_force, ui_RL_force, ui_RR_force;

    [Header("Anti-Roll Bar")]
    [SerializeField] private float frontAntiRollStiffness = 8000f;
    [SerializeField] private float rearAntiRollStiffness = 6000f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        SimulateWheel(fl, ref lastFLcompression);
        SimulateWheel(fr, ref lastFRcompression);
        SimulateWheel(rl, ref lastRLcompression);
        SimulateWheel(rr, ref lastRRcompression);
        ApplyAntiRollBars();
    }

    private void ApplyAntiRollBars()
    {
        float frontDiff = lastFLcompression - lastFRcompression;
        float frontForce = frontDiff * frontAntiRollStiffness;

        if (lastFLcompression > -0.0001f)
            rb.AddForceAtPosition(-transform.up * frontForce, fl.position, ForceMode.Force);
        if (lastFRcompression > -0.0001f)
            rb.AddForceAtPosition(transform.up * frontForce, fr.position, ForceMode.Force);

        float rearDiff = lastRLcompression - lastRRcompression;
        float rearForce = rearDiff * rearAntiRollStiffness;

        if (lastRLcompression > -0.0001f)
            rb.AddForceAtPosition(-transform.up * rearForce, rl.position, ForceMode.Force);
        if (lastRRcompression > -0.0001f)
            rb.AddForceAtPosition(transform.up * rearForce, rr.position, ForceMode.Force);
    }

    private void SimulateWheel(Transform pivot, ref float lastCompression)
    {
        Vector3 origin = pivot.position;
        Vector3 direction = -pivot.up;
        float maxDist = restLength + springTravel + wheelRadius;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist))
        {
            float currentLength = hit.distance - wheelRadius;
            currentLength = Mathf.Clamp(currentLength, restLength - springTravel, restLength + springTravel);
            float compression = restLength - currentLength;
            float springForce = compression * springStiffness;
            float compressionVelocity = (compression - lastCompression) / Time.fixedDeltaTime;
            float damperForce = compressionVelocity * damperStiffness;

            lastCompression = compression;
            float totalForce = springForce + damperForce;

            
            if (pivot == fl) ui_FL_force = totalForce;
            else if (pivot == fr) ui_FR_force = totalForce;
            else if (pivot == rl) ui_RL_force = totalForce;
            else if (pivot == rr) ui_RR_force = totalForce;

            Vector3 force = pivot.up * totalForce;
            rb.AddForceAtPosition(force, pivot.position, ForceMode.Force);
        }
    }

    private void OnGUI()
    {
        
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(Screen.width - 310, 10, 300, 220), "");
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 15;

        int x = Screen.width - 300;
        int y = 20;

        GUI.Label(new Rect(x, y, 280, 20), "=== SUSPENSION TELEMETRY ===", style); y += 30;

        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Normal;

        GUI.Label(new Rect(x, y, 280, 20), $"FL Force: {ui_FL_force:F0} N"); y += 20;
        GUI.Label(new Rect(x, y, 280, 20), $"FR Force: {ui_FR_force:F0} N"); y += 20;
        GUI.Label(new Rect(x, y, 280, 20), $"RL Force: {ui_RL_force:F0} N"); y += 20;
        GUI.Label(new Rect(x, y, 280, 20), $"RR Force: {ui_RR_force:F0} N"); y += 30;

        
        float comHeight = transform.InverseTransformPoint(rb.worldCenterOfMass).y;
        GUI.Label(new Rect(x, y, 280, 20), $"Center of Mass Height: {comHeight:F3} m"); y += 20;

        
        var aero = GetComponent<KartAreo>();
        if (aero != null)
        {
            GUI.color = Color.cyan;
            GUI.Label(new Rect(x, y, 280, 20), $"Wing Angle: {aero.wingAngleDeg:F1}°");
        }
    }
}