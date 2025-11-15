using UnityEngine;
using System.Collections;
using Unity.Netcode;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Unity.Netcode.Components.NetworkTransform))]
public class SpaceShipController : NetworkBehaviour
{
    [Header("Ship Settings")]
    public float rotationSpeed = 2f;
    public Transform helmSeat;

    [Header("Speed Settings")]
    public float slowSpeed = 2f;
    public float normalSpeed = 5f;
    public float fastSpeed = 10f;

    [Header("UI Settings")]
    public PilotUI pilotUI;
    public Canvas pilotUICanvas;

    public enum SpeedMode { Stop, Slow, Normal, Fast }
    public SpeedMode currentSpeedMode = SpeedMode.Stop;

    private NetworkVariable<SpeedMode> speedMode = new NetworkVariable<SpeedMode>(
        SpeedMode.Stop,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isControlled = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsControlled => isControlled.Value;

    private NetworkVariable<ulong> currentPilotId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public PlayerEquipmentManager CurrentPilot { get; private set; }

    private Rigidbody rb;
    private Vector2 input;
    private float lockedPilotZ;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;

        currentPilotId.OnValueChanged += OnPilotChanged;
        speedMode.OnValueChanged += OnSpeedModeChanged;

        if (IsOwner && pilotUI != null)
        {
            pilotUI.Init(this);
            pilotUI.UpdateSpeedUI();
            if (pilotUICanvas != null) pilotUICanvas.enabled = false; // start disabled
        }
    }

    public override void OnNetworkDespawn()
    {
        currentPilotId.OnValueChanged -= OnPilotChanged;
        speedMode.OnValueChanged -= OnSpeedModeChanged;
        base.OnNetworkDespawn();
    }

    private void OnPilotChanged(ulong oldId, ulong newId)
    {
        if (newId != 0 && NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(newId))
        {
            NetworkObject p = NetworkManager.SpawnManager.SpawnedObjects[newId];
            CurrentPilot = p.GetComponent<PlayerEquipmentManager>();
        }
        else
        {
            CurrentPilot = null;
        }
    }

    private void OnSpeedModeChanged(SpeedMode oldMode, SpeedMode newMode)
    {
        currentSpeedMode = newMode;
        if (IsOwner && pilotUI != null) pilotUI.UpdateSpeedUI();
    }

    void Update()
    {
        if (!isControlled.Value || CurrentPilot == null) return;
        if (CurrentPilot.OwnerClientId != NetworkManager.Singleton.LocalClientId) return;

        // SPEED MODE INPUT
        if (Input.GetKeyDown(KeyCode.W)) ChangeSpeedModeServerRpc(+1);
        if (Input.GetKeyDown(KeyCode.S)) ChangeSpeedModeServerRpc(-1);

        // ROTATION INPUT
        float turnInput = Input.GetAxis("Horizontal");
        Vector2 newInput = new Vector2(0, turnInput);

        if (newInput != input)
        {
            input = newInput;
            MoveShipServerRpc(input);
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || !isControlled.Value) return;

        float currentSpeed = speedMode.Value switch
        {
            SpeedMode.Stop => 0f,
            SpeedMode.Slow => slowSpeed,
            SpeedMode.Normal => normalSpeed,
            SpeedMode.Fast => fastSpeed,
            _ => 0f
        };

        Vector3 targetVel = transform.right * currentSpeed;
        rb.linearVelocity = new Vector3(targetVel.x, targetVel.y, rb.linearVelocity.z);

        float angVel = -input.y * rotationSpeed;
        rb.angularVelocity = new Vector3(0, 0, angVel);

        if (CurrentPilot != null && helmSeat != null)
        {
            Vector3 p = helmSeat.localPosition;
            p.z = lockedPilotZ;
            CurrentPilot.transform.localPosition = p;
            CurrentPilot.transform.localRotation = helmSeat.localRotation;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void MoveShipServerRpc(Vector2 moveInput)
    {
        if (!isControlled.Value || CurrentPilot == null) return;
        input = moveInput;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangeSpeedModeServerRpc(int dir)
    {
        int newMode = (int)speedMode.Value + dir;
        newMode = Mathf.Clamp(newMode, 0, 3);
        speedMode.Value = (SpeedMode)newMode;
    }

    [ServerRpc(RequireOwnership = false)]
    public void EnableControlServerRpc(bool state, ulong pilotId)
    {
        EnableControl(state, pilotId);
    }

    public void EnableControl(bool state, ulong pilotId)
    {
        if (!IsServer) return;

        isControlled.Value = state;

        if (state)
        {
            if (pilotId != 0 && NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(pilotId))
            {
                NetworkObject pObj = NetworkManager.SpawnManager.SpawnedObjects[pilotId];
                PlayerEquipmentManager pilot = pObj.GetComponent<PlayerEquipmentManager>();

                if (pilot != null)
                {
                    currentPilotId.Value = pilotId;
                    CurrentPilot = pilot;

                    // Enable UI for controlling client
                    if (CurrentPilot.OwnerClientId == NetworkManager.Singleton.LocalClientId && pilotUICanvas != null)
                        pilotUICanvas.enabled = true;

                    PlayerMovement pm = pilot.GetComponent<PlayerMovement>();
                    if (pm != null) pm.SetMovementDisabled(true);

                    Rigidbody prb = pilot.GetComponent<Rigidbody>();
                    if (prb != null)
                    {
                        prb.isKinematic = true;
                        prb.detectCollisions = false;
                        prb.constraints = RigidbodyConstraints.FreezeAll;
                    }

                    StopAllCoroutines();
                    StartCoroutine(MovePlayerToHelm(pilot, pObj));
                }
            }
        }
        else
        {
            if (CurrentPilot != null)
            {
                // Disable UI and reset input/speed
                if (CurrentPilot.OwnerClientId == NetworkManager.Singleton.LocalClientId && pilotUICanvas != null)
                    pilotUICanvas.enabled = false;

                input = Vector2.zero;
                currentSpeedMode = SpeedMode.Stop;
                speedMode.Value = SpeedMode.Stop;

                PlayerMovement pm = CurrentPilot.GetComponent<PlayerMovement>();
                if (pm != null) pm.SetMovementDisabled(false);

                Rigidbody prb = CurrentPilot.GetComponent<Rigidbody>();
                
                // Store current Z position before unparenting
                float preservedZ = CurrentPilot.transform.position.z;
                
                // Unparent the player first
                NetworkObject pObj = CurrentPilot.GetComponent<NetworkObject>();
                pObj.TryRemoveParent();

                // Calculate exit position in world space (behind the helm)
                Vector3 exitPos = helmSeat.position + transform.right * -1.5f;
                exitPos.z = preservedZ;
                
                // Set position and rotation in world space
                // Match the ship's rotation so movement directions align with ship orientation
                CurrentPilot.transform.position = exitPos;
                CurrentPilot.transform.rotation = transform.rotation;

                // Reset physics properties
                if (prb != null)
                {
                    // Reset velocity to prevent unstable movement
                    prb.linearVelocity = Vector3.zero;
                    prb.angularVelocity = Vector3.zero;
                    
                    prb.isKinematic = false;
                    prb.detectCollisions = true;
                    prb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
                }

                CurrentPilot = null;
            }

            currentPilotId.Value = 0;
        }
    }

    private IEnumerator MovePlayerToHelm(PlayerEquipmentManager pilot, NetworkObject pilotObj)
    {
        Vector3 start = pilot.transform.position;
        Vector3 target = helmSeat.position;
        float preservedZ = start.z;
        target.z = preservedZ;

        float duration = 0.4f;
        float t = 0f;

        pilotObj.TrySetParent(GetComponent<NetworkObject>());

        Vector3 startLocal = transform.InverseTransformPoint(start);
        Vector3 targetLocal = transform.InverseTransformPoint(target);
        targetLocal.z = transform.InverseTransformPoint(new Vector3(target.x, target.y, preservedZ)).z;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.SmoothStep(0f, 1f, t / duration);

            Vector3 pos = Vector3.Lerp(startLocal, targetLocal, a);
            pos.z = targetLocal.z;
            pilot.transform.localPosition = pos;

            yield return null;
        }

        pilot.transform.localPosition = targetLocal;
        pilot.transform.localRotation = helmSeat.localRotation;

        lockedPilotZ = targetLocal.z;
    }


    [System.Serializable]
    public class PilotUI
    {
        public Image speedFillBar;
        private SpaceShipController controller;

        public void Init(SpaceShipController c) => controller = c;

        public void UpdateSpeedUI()
        {
            if (speedFillBar == null || controller == null) return;

            switch (controller.currentSpeedMode)
            {
                case SpeedMode.Stop: speedFillBar.fillAmount = 0f; break;
                case SpeedMode.Slow: speedFillBar.fillAmount = 0.3f; break;
                case SpeedMode.Normal: speedFillBar.fillAmount = 0.6f; break;
                case SpeedMode.Fast: speedFillBar.fillAmount = 1f; break;
            }
        }
    }
}
