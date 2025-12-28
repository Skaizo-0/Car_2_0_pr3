using System;
using UnityEngine;

public class KartAreo : MonoBehaviour
{
    [Header("Aero Drag")]
    [SerializeField] private float airDensity = 1.225f;
    [SerializeField] private float dragCoefficient = 0.9f;
    [SerializeField] private float frontalArea = 0.6f;

    private Rigidbody rb;

    [Header("Rear Wing")]
    [SerializeField] private Transform rearWing;
    [SerializeField] private float wingArea = 0.4f;
    [SerializeField] private float liftCoefficientSlope = 0.05f;
    public float wingAngleDeg = 10f; 

    [Header("Ground Effect")]
    [SerializeField] private float groundEffectStrength = 3000f;
    [SerializeField] private float groundRayLength = 1.0f;

    
    [HideInInspector] public float lastDragForce;
    [HideInInspector] public float lastDownforce;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void ApplyGroundEffect()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -transform.up, out hit, groundRayLength))
        {
            float h = hit.distance;
            if (h < 0.01f) h = 0.01f;
            float geForce = groundEffectStrength / h;
            Vector3 force = -transform.up * geForce;
            rb.AddForce(force, ForceMode.Force);
        }
    }

    private void ApplyWingDownforce()
    {
        if (rearWing == null) return;

        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.01f) { lastDownforce = 0; return; }

        float alphaRad = wingAngleDeg * Mathf.Deg2Rad;
        float Cl = liftCoefficientSlope * alphaRad;

        float downforce = 0.5f * airDensity * Cl * wingArea * speed * speed;
        lastDownforce = downforce; 

        Vector3 force = -transform.up * downforce;
        rb.AddForceAtPosition(force, rearWing.position, ForceMode.Force);
    }

    private void FixedUpdate()
    {
        ApplyDrag();
        ApplyWingDownforce();
        ApplyGroundEffect();
    }

    private void ApplyDrag()
    {
        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;

        if (speed < 0.01f) { lastDragForce = 0; return; }

        float dragForce = 0.5f * airDensity * dragCoefficient * frontalArea * speed * speed;
        lastDragForce = dragForce; 

        Vector3 drag = -v.normalized * dragForce;
        rb.AddForce(drag, ForceMode.Force);
    }
}