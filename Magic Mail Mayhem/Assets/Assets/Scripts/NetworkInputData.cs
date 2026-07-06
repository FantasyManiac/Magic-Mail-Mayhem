using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 LookDelta;
    public NetworkBool Sprint;
}



// using Fusion;
// using UnityEngine;

// public struct NetworkInputData : INetworkInput
// {
//     // public Vector3 Direction;

//     // WASD movement
//     public Vector2 Move;

//     // Absolute look angles:
//     // x = yaw
//     // y = pitch
//     public Vector2 LookRotation;

//     // Sprint button
//     public NetworkBool Sprint;
// }