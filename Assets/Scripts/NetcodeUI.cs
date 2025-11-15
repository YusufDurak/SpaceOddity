using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetcodeUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
 

    private void Awake()
    {
        startHostButton.onClick.AddListener(StartHost);
        startClientButton.onClick.AddListener(StartClient);
    }

    private void StartHost()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log(" Host started");
        }
        else
        {
            Debug.LogWarning(" Failed to start host");
        }
    }

    private void StartClient()
    {
        if (NetworkManager.Singleton.StartClient())
        {
           
            Debug.Log(" Connecting to server...");
        }
        else
        {
            Debug.LogWarning(" Failed to start client");
        }
    }
}
