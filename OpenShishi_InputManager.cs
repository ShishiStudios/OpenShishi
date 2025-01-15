using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;

public class Ballad_InputManager : MonoBehaviour
{
    public static OpenShishi_InputManager Instance { get; private set; }

    [SerializeField] private int playerId = 0; // Rewired Player ID
    private Player playerInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        playerInput = ReInput.players.GetPlayer(playerId); // Default player
    }

    private void Update()
    {
        
    }

    public Player GetPlayerInput()
    {
        return playerInput;
    }

}
